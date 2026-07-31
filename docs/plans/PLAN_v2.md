# DeepSeek 余额悬浮小工具 —— 实现方案 v2

- 日期：2026-07-31
- 技术栈：C# WPF，**.NET 8 LTS**（`net8.0-windows`，开发机已装 SDK 8.0.423，零额外安装）
- 状态：设计稿已通过第三方审查（见 `PLAN_REVIEW.md`），本 v2 已整合全部必须修正项
- 版本关系：v2 取代 v1（`PLAN.md`）。v1 的开放问题已在 v2 中逐条给出裁决

## 1. 需求概述（不变）

Windows 11 桌面悬浮小工具，监控 DeepSeek 开放平台 API 余额（CNY）：

- 透明无边框、置顶、不进任务栏、可拖动、贴角落
- 显示：总余额、与上次的变动（`+/-` 和颜色）、充值 vs 赠送细分、上次刷新时间；`is_available=false` 醒目变色
- 默认 30 秒轮询（可配置）
- 低余额告警 + 异常下降告警（防轰炸）
- 设置窗口 + 配置持久化 + 可选开机自启

## 2. API 契约（已核实）

- `GET https://api.deepseek.com/user/balance`
- 请求头：`Authorization: Bearer <API_KEY>`，无参数
- 响应（余额字段为**字符串**）：

```json
{
  "is_available": true,
  "balance_infos": [
    { "currency": "CNY", "total_balance": "110.00", "granted_balance": "10.00", "topped_up_balance": "100.00" }
  ]
}
```

- 可能含 CNY 和 USD 两条目

## 3. 已裁决的关键设计决策

| 决策点 | 裁决 |
|---|---|
| **.NET 版本** | **.NET 8**（用户已确认）。EOL 2026-11，个人工具风险极低；将来升级只需改 TFM |
| **告警通知** | 自绘 `ToastWindow`（Topmost + `ShowActivated=False` 不抢焦点，5s 自关） |
| **API Key 存储** | DPAPI（`ProtectedData`，CurrentUser 范围，Base64 落盘）。不引入 entropy |
| **托盘** | **必须加**（WinForms `NotifyIcon` 互操作，`<UseWindowsForms>true`，零第三方依赖）。`ShowInTaskbar=false` 时窗口拖丢的唯一恢复手段 |
| **多币种** | 加 `selectedCurrency`（默认 CNY）。目标币种缺失时显示「未返回 CNY 余额」，**不静默切换 USD**。UI 始终带币种单位 |
| **网络重试** | 简单 **1 次重试**（不加指数退避+抖动，30 秒轮询不值得） |
| **测试范围** | 只写 2 个核心纯服务（`BalanceParser`、`AlertEvaluator`）的单元测试，其余靠 Mock 肉眼验证 |
| **暂缓（backlog）** | 单实例锁 / 代码签名 / 升级策略 / 透明度调节 / 多显示器完整矩阵 / 全屏应用与 UAC 安全桌面下 Toast 行为 |

## 4. 项目结构

```
orca_1/
├── DeepSeekBalanceWidget.sln
└── src/DeepSeekBalanceWidget/
    ├── DeepSeekBalanceWidget.csproj   # net8.0-windows, UseWPF, UseWindowsForms, ProtectedData 包
    ├── App.xaml / App.xaml.cs         # 入口，加载配置，初始化托盘
    ├── MainWindow.xaml / .cs          # 悬浮主窗口
    ├── SettingsWindow.xaml / .cs      # 设置窗口（PasswordBox）
    ├── ToastWindow.xaml / .cs         # 告警弹窗
    ├── Models/
    │   ├── BalanceResponse.cs         # API DTO（[JsonPropertyName] snake_case）
    │   ├── ParsedBalance.cs           # 业务模型（record，校验后）
    │   └── AppConfig.cs               # 配置模型
    ├── Services/
    │   ├── IBalanceProvider.cs        # 统一余额获取接口
    │   ├── DeepSeekApiClient.cs       # 真实 API 实现
    │   ├── MockBalanceService.cs      # 离线模拟（--mock-scenario 命名场景）
    │   ├── BalanceParser.cs           # 纯函数：JSON→ParsedBalance，严格校验
    │   ├── CurrencySelector.cs        # 纯函数：按 selectedCurrency 选币种
    │   ├── BalanceChangeCalculator.cs # 纯函数：差额计算
    │   ├── AlertEvaluator.cs          # 纯函数：告警状态机（低余额/异常下降/冷却/迟滞）
    │   ├── ConfigService.cs           # config.json 原子读写 + DPAPI + 损坏恢复
    │   ├── AutoStartService.cs        # 注册表自启
    │   └── ToastService.cs            # 弹/关告警窗口
    └── Assets/icon.ico
```

不引入 MVVM 框架。csproj 要点：`OutputType=WinExe`、`UseWPF=true`、`UseWindowsForms=true`（托盘）、`PackageReference System.Security.Cryptography.ProtectedData 8.0.0`。

## 5. 数据模型与解析（审查 2.1 / 2.4）

DTO 与业务模型分离，**snake_case 必须显式映射**（`PropertyNameCaseInsensitive` 只忽略大小写，**不能**映射下划线）：

```csharp
public sealed class BalanceInfo
{
    [JsonPropertyName("currency")]        public string Currency { get; init; } = "";
    [JsonPropertyName("total_balance")]   public string TotalBalance { get; init; } = "";
    [JsonPropertyName("granted_balance")] public string GrantedBalance { get; init; } = "";
    [JsonPropertyName("topped_up_balance")] public string ToppedUpBalance { get; init; } = "";
}

public sealed record ParsedBalance(string Currency, decimal Total, decimal Granted, decimal ToppedUp, bool IsAvailable);
```

`BalanceParser.Parse(string json)` 严格校验，**解析失败进明确错误态，不默认为 0**：

- 空响应 / 缺失 `balance_infos` / 空数组 → 解析错误
- 非法金额字符串、负金额 → 解析错误
- 未知货币 → 解析错误（或按配置降级）
- 校验通过才产出 `ParsedBalance`

`CurrencySelector`：优先 `selectedCurrency`（默认 CNY）；目标币种缺失 → 返回「币种缺失」状态（UI 显示「未返回 CNY 余额」），**不静默取第一条**。

## 6. 余额变动与告警状态机（审查第 3 节）

`AlertEvaluator` 为**无状态输入的纯函数**，状态由调用方持久化：

```text
上次余额 LastSuccessfulBalance    // 配置/内存中
上次成功刷新时间 LastSuccessfulRefreshUtc
是否有基线 HasBaseline            // 首次成功刷新后为 true
低余额状态 InLowBalanceState      // 低于阈值时为 true
```

规则（全部采纳审查意见）：

- **首次成功刷新**：无基线，只显示余额，不显示变动、不告警
- **网络失败**：保留上次成功余额，显示「上次成功刷新时间」，**不把余额当 0**
- **上次余额为 0**：不计算下降百分比
- **只有下降**才触发异常下降告警；上升显示正数但**不告警**
- **变动百分比**：`(上次余额 - 本次余额) / 上次余额 * 100`
- **低余额**：进入低于阈值状态时提醒一次；持续低于则按冷却限制（默认 30 分钟，可配置）；恢复到阈值以上才解除状态
- **异常下降**：每次独立下降事件最多提醒一次 + 默认 10 分钟冷却；**重启后不重放历史事件**
- 每次成功刷新后保存 `LastSuccessfulBalance` / `LastSuccessfulRefreshUtc` 到配置，供重启后恢复基线

## 7. 网络层（审查第 4 节）

`DeepSeekApiClient`：

- 复用单个 `HttpClient`（实例字段，超时 10s），**更换 API Key 时重建客户端**，不每次轮询创建
- 请求支持 `CancellationToken`，**应用退出时取消**
- 状态分类：
  - `401 / 403` → 不重试，提示认证/权限问题
  - `429` → 优先读取 `Retry-After`
  - `408 / 429 / 5xx / 网络异常` → **最多简单重试 1 次**
  - 其他 → 保留状态码 + 服务端错误信息（**不把 402 硬编码为「欠费」**）
- 轮询失败 → 保留上次成功余额并显示「上次成功刷新时间」

## 8. 配置与安全（审查第 5 节）

位置：`%APPDATA%\DeepSeekBalanceWidget\config.json`。读写要求：

- **原子写入**：临时文件 + `File.Replace` 替换
- **损坏恢复**：读取失败时保留 `.corrupt.bak`，重建默认配置
- **API Key**：DPAPI 加密（`ProtectedData.Protect`，CurrentUser）+ Base64；设置窗口用 `PasswordBox`，**默认不显示完整 Key**
- **不落日志**：API Key 不进日志、异常、调试输出
- 提供**清除 API Key** 功能
- `configVersion` 字段为后续迁移做准备
- **输入校验**：刷新间隔不能过小（≥5s）、阈值不能为负、异常百分比限制在合理范围（如 0.1–100）

```json
{
  "configVersion": 1,
  "apiKeyEncrypted": "base64...",
  "selectedCurrency": "CNY",
  "refreshIntervalSeconds": 30,
  "lowBalanceThreshold": 10.0,
  "abnormalChangePercent": 10.0,
  "lowBalanceCooldownSeconds": 1800,
  "abnormalAlertCooldownSeconds": 600,
  "showToastNotifications": true,
  "isAlwaysOnTop": true,
  "useMockData": false,
  "autoStart": false,
  "windowLeft": null,
  "windowTop": null,
  "lastSuccessfulBalance": null,
  "lastSuccessfulRefreshUtc": null
}
```

## 9. WPF 桌面体验（审查第 6 节，已裁剪）

### 9.1 悬浮主窗口

```xml
<Window WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        Topmost="{Binding IsAlwaysOnTop}" ShowInTaskbar="False" ResizeMode="NoResize"
        WindowStartupLocation="Manual" Width="260" Height="150">
  <Border Background="#E6141418" CornerRadius="12" BorderBrush="#33FFFFFF"
          BorderThickness="1" Padding="12">
    <!-- 内容：余额 / 变动 / 充值vs赠送 / 刷新时间 -->
  </Border>
</Window>
```

- `IsAlwaysOnTop` 可配置（默认 true，符合用户「贴角落置顶」需求）
- 右键菜单：立即刷新 / 设置 / 恢复位置 / 退出

### 9.2 拖动与位置（裁剪后的核心）

- `MouseLeftButtonDown → DragMove()`，**包 try/catch 防异常**
- **只允许卡片空白区域拖动**（内容按钮不触发 `DragMove`）
- **拖动结束后防抖保存**位置（约 500ms），不只在 OnClosing 保存
- **启动时检查**窗口是否完全出屏 → 是则恢复默认位置

### 9.3 托盘（审查 6.1，采纳）

WinForms `NotifyIcon` 互操作，菜单：**显示窗口 / 立即刷新 / 设置 / 恢复默认位置 / 退出**；双击图标恢复默认位置。窗口关闭仅隐藏（最小化到托盘），真正退出走托盘菜单。

### 9.4 Toast（告警弹窗）

- 自绘 `ToastWindow`：Topmost + `ShowActivated=False` + `ShowInTaskbar=False`，显示在主窗口旁，5s 自动关闭
- **位置按工作区边界计算**，不覆盖到屏幕边缘
- 措辞修正：不声称「100% 可靠」；全屏/UAC 等高阶场景列入 backlog（Toast 为辅助提醒，丢失一次不致命）

## 10. Mock 与测试（审查第 7 节，已裁剪）

### Mock 模式

`IBalanceProvider` 接口，`DeepSeekApiClient` 与 `MockBalanceService` 各实现之。支持**命名场景**（配置 `useMockData` 或启动参数）：

```text
--mock-scenario normal      # 正常余额 110（granted 10 / topped_up 100）
--mock-scenario drop        # 下降 2.5 元（验证变动显示）
--mock-scenario low         # 掉到 8 元（验证低余额告警）
--mock-scenario unavailable # is_available=false（验证红色提示）
--mock-scenario error       # 抛异常（验证错误态）
```

### 单元测试（只写核心 2 个）

- `BalanceParser`：空 balance_infos / 只有 USD / 同时 CNY+USD / 非法金额 / 空金额 / 负金额 / 余额为 0
- `AlertEvaluator`：首次刷新无基线 / 失败保留上次余额 / 零余额不算百分比 / 上升不告警 / 下降触发 / 冷却 / 迟滞解除
- 其余分支（网络错误、多显示器等）靠 Mock 肉眼验证

## 11. 开机自启

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，`Set("DeepSeekBalanceWidget", "\"<exe路径>\"")`，取消时 `DeleteValue`。无需管理员权限。路径加双引号防空格。

## 12. 实现步骤（整合审查第 8 节，已裁剪）

1. **脚手架 + 数据层**：建项目（net8.0-windows，UseWPF+UseWindowsForms），DTO `[JsonPropertyName]`，`BalanceParser` / `CurrencySelector` 纯服务，配置模型 + 输入校验
2. **业务逻辑 + 测试**：`BalanceChangeCalculator` / `AlertEvaluator` 状态机，写 2 个核心单元测试
3. **Mock UI + 悬浮窗**：透明圆角置顶卡片、拖动防抖、出屏恢复、错误态；`MockBalanceService` 命名场景
4. **配置 + 设置窗口**：`ConfigService`（DPAPI、原子写入、损坏恢复）、PasswordBox、清除 Key
5. **真实 API + 轮询**：`DeepSeekApiClient`、30s `DispatcherTimer`、取消、1 次重试、失败保留上次余额
6. **告警 + 托盘**：`ToastService` + `ToastWindow`（冷却/迟滞）、`NotifyIcon` 托盘菜单
7. **自启 + 发布验证**：注册表自启接入设置、`dotnet publish -c Release -r win-x64 --self-contained true`

## 13. 验证

### Mock（无 Key）

依次跑 `--mock-scenario normal → drop → low → unavailable → error`，肉眼验证：余额显示、变动差值、低余额告警、红色不可用态、错误态。

### 真实 Key

1. `curl -H "Authorization: Bearer sk-xxx" https://api.deepseek.com/user/balance` 独立验证 Key（**注意：真实 Key 不要写进 shell 历史**，或用临时环境变量）
2. 设置窗口填 Key → 主窗显示 CNY 余额（验证 snake_case 字段正确映射）
3. 立即刷新 → 变动值变化
4. 错 Key → 401 提示，不重试
5. 阈值 > 余额 → 低余额告警（且不重复轰炸）；变动阈值设 0.1% → 异常告警
6. 勾选自启 → `regedit` 查 `HKCU\...\Run`
7. 重启应用 → 位置/设置/上次余额基线恢复；删 config.json → 兜底重建默认配置
8. 故意把窗口拖出屏 → 托盘「恢复默认位置」可找回
9. 退出时正在请求 → 应用正常退出（CancellationToken 生效）

## 14. 验收标准（审查第 9 节）

- [ ] snake_case 字段正确显示
- [ ] CNY/USD 不静默混用
- [ ] 首次刷新 / 零余额 / 上升 / 下降 / 网络失败状态定义清晰
- [ ] 低余额与异常下降不重复轰炸
- [ ] API Key 不进日志和命令行历史
- [ ] 配置损坏可恢复（.corrupt.bak + 重建）
- [ ] 窗口出屏可恢复（托盘 + 出屏检测）
- [ ] 托盘可重新显示和退出应用
- [ ] 退出时网络请求可取消
- [ ] 发布后目标机正常启动并保存配置

## Backlog（本期不做）

- 单实例锁 / 代码签名 / 升级策略
- 透明度调节、临时隐藏
- 多显示器 / 负坐标 / 高 DPI 完整处理矩阵
- 全屏应用与 UAC 安全桌面下 Toast 行为
- 完整单元测试套件扩展

## 关键文件

- `src/DeepSeekBalanceWidget/DeepSeekBalanceWidget.csproj`
- `src/DeepSeekBalanceWidget/MainWindow.xaml`（悬浮窗 + 拖动 + 右键菜单）
- `src/DeepSeekBalanceWidget/Services/DeepSeekApiClient.cs`（JsonPropertyName + ParsedBalance + 网络分类）
- `src/DeepSeekBalanceWidget/Services/AlertEvaluator.cs`（告警状态机）
- `src/DeepSeekBalanceWidget/Services/ConfigService.cs`（原子写入 + DPAPI + 损坏恢复）
- `src/DeepSeekBalanceWidget/Services/BalanceParser.cs`（严格解析）
