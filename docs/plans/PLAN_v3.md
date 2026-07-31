# DeepSeek 余额悬浮小工具 —— 实现方案 v3

- 日期：2026-07-31
- 技术栈：C# WPF，**.NET 8 LTS**（`net8.0-windows`，用户已确认，开发机 SDK 8.0.423 已装）
- 状态：已通过两轮审查（`PLAN_REVIEW.md` 一轮、`PLAN_v2_REVIEW.md` 二轮），本 v3 整合两轮全部必须修正项
- 版本关系：v3 取代 v2。**changelog**：v2 → v3 的修订见第 14 节

## 1. 需求概述（不变）

Windows 11 桌面悬浮小工具，监控 DeepSeek 开放平台 API 余额（CNY）：

- 透明无边框、置顶、不进任务栏、可拖动、贴角落
- 显示：总余额、与上次的变动（`+/-` 和颜色）、充值 vs 赠送细分、上次刷新时间；`is_available=false` 醒目变色
- 默认 30 秒轮询（可配置）
- 低余额告警 + 异常下降告警（防轰炸）
- 设置窗口 + 配置持久化 + 可选开机自启
- **单实例**（第二实例激活已有窗口后退出）

## 2. API 契约（已核实）

- `GET https://api.deepseek.com/user/balance`
- 请求头：`Authorization: Bearer <API_KEY>`，无参数
- 响应（余额字段为**字符串**，可能含 CNY 和 USD 两条目）：

```json
{
  "is_available": true,
  "balance_infos": [
    { "currency": "CNY", "total_balance": "110.00", "granted_balance": "10.00", "topped_up_balance": "100.00" }
  ]
}
```

## 3. 项目结构

```
orca_1/
├── DeepSeekBalanceWidget.sln
├── src/DeepSeekBalanceWidget/
│   ├── DeepSeekBalanceWidget.csproj   # net8.0-windows, UseWPF, UseWindowsForms, ProtectedData 包
│   ├── Program.cs                     # 入口：单实例 Mutex + 事件激活
│   ├── App.xaml / App.xaml.cs         # ShutdownMode=OnExplicitShutdown，持有托盘与全局资源
│   ├── MainWindow.xaml / .cs          # 悬浮主窗口（刷新协调者，代码后置）
│   ├── SettingsWindow.xaml / .cs      # 设置窗口（PasswordBox）
│   ├── ToastWindow.xaml / .cs         # 告警弹窗
│   ├── Models/
│   │   ├── BalanceResponse.cs         # API DTO（[JsonPropertyName] snake_case）
│   │   ├── ParsedBalance.cs           # 业务模型 record
│   │   ├── AlertState.cs              # 告警状态 record
│   │   └── AppConfig.cs               # 配置模型
│   ├── Services/
│   │   ├── IBalanceProvider.cs        # 统一余额获取接口
│   │   ├── DeepSeekApiClient.cs       # 真实 API 实现
│   │   ├── MockBalanceService.cs      # 离线模拟（--mock-scenario 含 sequence）
│   │   ├── BalanceParser.cs           # 纯函数：JSON→ParsedBalance
│   │   ├── CurrencySelector.cs        # 纯函数：按 selectedCurrency 选择/不支持态
│   │   ├── BalanceChangeCalculator.cs # 纯函数：差额/百分比
│   │   ├── AlertEvaluator.cs          # 纯函数：告警判定（输入 AlertState，输出决策）
│   │   ├── ConfigService.cs           # config.json 原子读写 + DPAPI + 损坏恢复
│   │   ├── AutoStartService.cs        # 注册表自启
│   │   ├── ToastService.cs            # 弹/关告警窗口
│   │   └── TrayIconService.cs         # 托盘图标 + 菜单（WinForms NotifyIcon）
│   └── Assets/icon.ico
└── tests/DeepSeekBalanceWidget.Tests/  # 仅两个核心测试文件
    ├── BalanceParserTests.cs
    └── AlertEvaluatorTests.cs
```

csproj 要点：`OutputType=WinExe`、`UseWPF=true`、`UseWindowsForms=true`（托盘）、`PackageReference System.Security.Cryptography.ProtectedData 8.0.0`。

**类型名冲突注意**：WPF + WinForms 并存时 `Application`/`Timer`/`MessageBox` 等同名类型必须用全限定名（`System.Windows.Application` / `System.Windows.Forms.Timer` / 各自 `MessageBox`）。

## 4. 数据模型与解析

DTO 与业务模型分离，snake_case 用 `[JsonPropertyName]` 显式映射（`PropertyNameCaseInsensitive` 只忽略大小写，**不能**映射下划线）：

```csharp
public sealed class BalanceInfo
{
    [JsonPropertyName("currency")] public string Currency { get; init; } = "";
    [JsonPropertyName("total_balance")] public string TotalBalance { get; init; } = "";
    [JsonPropertyName("granted_balance")] public string GrantedBalance { get; init; } = "";
    [JsonPropertyName("topped_up_balance")] public string ToppedUpBalance { get; init; } = "";
}

public sealed record ParsedBalance(string Currency, decimal Total, decimal Granted, decimal ToppedUp, bool IsAvailable);
```

**`BalanceParser` 校验范围（二轮修订）**：
- 校验：空响应 / 缺 `balance_infos` / 空数组 / 币种为空 / 金额非法 / 金额为负 → 解析错误（进明确错误态，**不默认为 0**）
- **不拒绝未知币种**（如未来新增 `EUR`）：币种非空即通过，由 `CurrencySelector` 判定支持与否
- 余额合计一致性（`total == granted + topped_up`）不一致时按解析错误处理

**`CurrencySelector`**：优先 `selectedCurrency`（默认 CNY）；目标币种缺失/不支持 → 返回「币种不支持」状态，UI 显示「未返回 CNY 余额」或「未支持币种」。**不静默取第一条，不静默切 USD**。UI 始终带货币单位（`¥110.00`）。

## 5. 告警状态机（二轮修订：完整建模）

`AlertEvaluator` 为**纯函数**（无 UI/无 I/O），状态通过参数传入：

```csharp
public sealed record AlertState(
    bool HasBaseline,                          // 首次成功刷新后为 true
    decimal? LastSuccessfulBalance,            // 上次成功余额（跨重启持久化）
    DateTimeOffset? LastSuccessfulRefreshUtc,  // 上次成功刷新时间（跨重启持久化）
    bool InLowBalanceState,                    // 当前是否处于低余额状态（跨重启持久化）
    DateTimeOffset? LastLowBalanceAlertUtc,    // 上次低余额告警时间（仅内存）
    DateTimeOffset? LastAbnormalAlertUtc);     // 上次异常下降告警时间（仅内存）
```

输入：`AlertState` + 本次 `ParsedBalance` + 配置（阈值/百分比/冷却）。输出：`AlertDecision`（是否弹低余额、是否弹异常下降、新的 `AlertState`）。

### 规则

- **首次成功刷新**：`HasBaseline=false → true`，只建立基线，不显示变动、不告警
- **重启后首次成功刷新**：**不告警、不重放历史事件**（避免重启后骤降被当作新事件误报）；基线取持久化的 `LastSuccessfulBalance` 用于显示连续性；此后按正常逻辑
- **网络失败**：保留上次成功余额，显示「上次成功刷新时间」，**不清零、不告警**
- **上次余额为 0**：不计算下降百分比
- **只有下降**触发异常下降告警；上升显示正数（绿色）但不告警
- **变动百分比**：`(上次余额 - 本次余额) / 上次余额 * 100`

### 低余额告警（进入状态 + 冷却 + 迟滞）

1. 首次从「≥ 阈值」变「< 阈值」→ 立即提醒一次
2. 持续低于阈值 → 按 `lowBalanceCooldownSeconds`（默认 1800s，仅内存时间戳）限频
3. 恢复到 ≥ 阈值 → `InLowBalanceState = false`（持久化），解除；之后再次跌破可再提醒

### 异常下降告警

- 每次独立下降事件最多提醒一次，冷却 `abnormalAlertCooldownSeconds`（默认 600s，仅内存）
- 重启后不重放历史事件（见上）

**持久化策略**：基线（`LastSuccessfulBalance` / `LastSuccessfulRefreshUtc`）与 `InLowBalanceState` 存配置；告警冷却时间戳仅存内存。

## 6. 网络层

`DeepSeekApiClient`：

- 复用单个 `HttpClient`（实例字段，超时 10s），**更换 API Key 时重建客户端**
- 请求支持 `CancellationToken`，应用退出时取消
- 状态分类与重试（二轮修订：明确等待策略）：
  - `401 / 403` → 不重试，提示认证/权限问题
  - `429` → 读 `Retry-After`：合法则**按其等待（上限 30s，可取消）**；不合法则提示限流
  - `408 / 5xx / 网络异常` → **最多重试 1 次**，等待 500–1000ms（可取消）
  - 其他 → 保留状态码 + 服务端错误信息（**不把 402 硬编码为「欠费」**）
- 本轮重试在**同一刷新周期内**完成；`_isRefreshing` 保证下次 Tick 不与本次重试叠加
- 轮询失败 → 保留上次成功余额并显示「上次成功刷新时间」

## 7. 配置与安全

位置：`%APPDATA%\DeepSeekBalanceWidget\config.json`。

- **原子写入（二轮修订）**：写 `config.json.tmp`（同目录）→ 目标**存在**则 `File.Replace(tmp, target, backup)`，目标**不存在**则 `File.Move(tmp, target)`；异常时删临时文件
- **损坏恢复**：读取失败 → 改名 `config.json.corrupt.bak`，重建默认配置并提示
- **API Key**：DPAPI 加密（`ProtectedData.Protect`，CurrentUser）+ Base64；设置窗口 `PasswordBox`，默认不显示完整 Key；**不落日志/异常/调试输出**；提供「清除 API Key」
- `configVersion` 为迁移准备；**输入校验**：刷新间隔 5–3600s、阈值 ≥ 0、异常百分比 0–100、冷却 ≥ 0
- **`DateTimeOffset` 一律以 UTC 序列化**；`windowLeft/windowTop` 为 `null` 表示「未设置 → 走默认位置」（**0 是合法坐标，不能当「未设置」**）

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
  "lastSuccessfulRefreshUtc": null,
  "inLowBalanceState": false
}
```

## 8. WPF 桌面体验

### 8.1 悬浮主窗口

```xml
<Window x:Class="DeepSeekBalanceWidget.MainWindow"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        Topmost="True" ShowInTaskbar="False" ResizeMode="NoResize"
        WindowStartupLocation="Manual" Width="260" Height="150">
  <Border Background="#E6141418" CornerRadius="12" BorderBrush="#33FFFFFF"
          BorderThickness="1" Padding="12">
    <!-- 余额 / 变动 / 充值vs赠送 / 刷新时间 / 币种单位 -->
  </Border>
</Window>
```

**二轮修订（2.1）：`Topmost` 不用 XAML 绑定**。代码后置：启动时 `Topmost = config.IsAlwaysOnTop;`，设置变更保存后立即重新赋值。XML 注释不写在开始标签属性列表内。

右键菜单：立即刷新 / 设置 / 恢复默认位置 / 退出。

### 8.2 拖动与位置

- `MouseLeftButtonDown → DragMove()`，**只允许卡片空白区域触发**（内容按钮不触发），包 try/catch
- 拖动结束防抖（约 500ms）保存 `Left/Top`，不只在 OnClosing 保存
- **出屏检测**：用 `System.Windows.Forms.Screen.AllScreens` 工作区判断窗口是否完全出屏（**保留负坐标与多显示器**，不只判 `Left < 0`）；出屏则恢复默认位置

### 8.3 托盘（二轮修订）

- `TrayIconService` 由 **`App` 持有**（不随窗口创建销毁），创建时**必须设 `Icon`**，退出时 `Dispose()`
- **双击托盘 = 显示并激活窗口**；「恢复默认位置」为**独立菜单项**（不是双击行为）
- 菜单：显示窗口 / 立即刷新 / 设置 / 恢复默认位置 / 退出

### 8.4 窗口关闭 vs 应用退出（二轮修订 2.4）

- `App.ShutdownMode = OnExplicitShutdown`
- `MainWindow.Closing`：非显式退出时 `e.Cancel = true; Hide();`（关闭仅隐藏）
- 托盘「退出」：置 `_isExiting=true` → `_cts.Cancel()` → `NotifyIcon.Dispose()` → `Close()` → `Application.Shutdown()`
- 退出时取消 `CancellationTokenSource`，等待在途刷新（带超时）

### 8.5 Toast 告警弹窗

自绘 `ToastWindow`：`Topmost` + `ShowActivated="False"`（不抢焦点）+ `ShowInTaskbar="False"` + `Owner=主窗口`，显示主窗旁，5s 自动关闭。位置按工作区边界计算。不声称「100% 可靠」；全屏/UAC 安全桌面场景列 backlog。

## 9. 单实例（二轮修订 2.5：提档为首版）

- `Program.cs`：named `Mutex`（如 `"DeepSeekBalanceWidget_SingleInstance"`）+ 命名 `EventWaitHandle`（如 `"DeepSeekBalanceWidget_Activate"`）
- 首实例：持有 Mutex，后台线程 `WaitOne()` 监听激活事件 → `Dispatcher.Invoke(() => { Show(); Activate(); })`
- 第二实例：`Mutex.WaitOne(0)` 失败 → `EventWaitHandle.Set()` 通知首实例显示窗口 → 立即退出（约 10 行，无需跨进程通信框架）

## 10. Mock 与测试

### Mock 模式

`IBalanceProvider` 接口，`MockBalanceService` 实现之。启动参数 `--mock-scenario`：

```text
--mock-scenario normal       # 正常 110（granted 10 / topped_up 100）
--mock-scenario drop         # 下降 2.5 元
--mock-scenario low          # 掉到 8 元（触发低余额告警）
--mock-scenario unavailable  # is_available=false（红色提示）
--mock-scenario error        # 抛异常（验证错误态不清零）
--mock-scenario sequence     # 单次运行依次返回：normal→drop→low→unavailable→error（共享基线，二轮修订 3.3）
```

### 单元测试（只写 2 个核心纯服务）

- `BalanceParserTests`：空响应 / 缺 balance_infos / 空数组 / 未知币种（不拒）/ 非法金额 / 空金额 / 负金额 / 余额为 0 / 合计不一致
- `AlertEvaluatorTests`：首次无基线 / 重启不重放 / 失败不清零（协调职责，见下）/ 零余额不算百分比 / 上升不告警 / 下降触发 / 低余额冷却 / 迟滞解除
- **职责边界**：「网络失败保留上次余额」是 `MainWindow` 协调者的职责，**不属于** `AlertEvaluator`，不在其测试中测；由 Mock `error` 场景肉眼验证

## 11. 开机自启

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`：`Set("DeepSeekBalanceWidget", "\"<exe路径>\"")`，取消时 `DeleteValue`。无需管理员权限，路径加双引号防空格。

## 12. 实现步骤

1. **脚手架 + 单实例**：`dotnet new sln` + `dotnet new wpf`（net8.0-windows，UseWPF+UseWindowsForms），加 ProtectedData 包，`Program.cs` 单实例 Mutex/EventWaitHandle，`dotnet build` 通过
2. **纯逻辑层 + 单测**：DTO/`ParsedBalance`/`AlertState`/`JsonPropertyName` → `BalanceParser`、`CurrencySelector`、`BalanceChangeCalculator`、`AlertEvaluator`；写 `BalanceParserTests` + `AlertEvaluatorTests`
3. **Mock UI + 悬浮窗**：主窗口（代码后置 Topmost、拖动防抖、出屏恢复）、`MockBalanceService`（含 sequence）、错误态
4. **配置 + 设置窗口**：`ConfigService`（DPAPI / 原子写含首次分支 / 损坏恢复 / UTC / null=默认位置）、`SettingsWindow`（PasswordBox / 清除 Key / 币种选择）
5. **真实 API + 轮询**：`DeepSeekApiClient`、30s `DispatcherTimer`、取消、重试等待策略、失败保留上次余额
6. **告警 + 托盘 + 生命周期**：`AlertEvaluator` 接入、`ToastService`/`ToastWindow`、`TrayIconService`（App 持有）、`ShutdownMode=OnExplicitShutdown`、关闭即隐藏
7. **自启 + 发布验证**：`AutoStartService`、`dotnet publish -c Release -r win-x64 --self-contained true`

## 13. 验证

### Mock（无 Key）

`--mock-scenario sequence` 单次运行即可验证全链路：正常显示 → 变动差值 → 低余额告警（+冷却）→ 红色不可用态 → 错误态不清零。再单独跑 `unavailable`/`error` 复核。

### 真实 Key

1. 用**临时环境变量**注入 Key 给 curl 验证（**不写进 shell 历史**）：`curl -H "Authorization: Bearer $DSK_KEY" https://api.deepseek.com/user/balance`
2. 设置窗口填 Key → 主窗显示 `¥` 余额（含充值/赠送细分），验证 snake_case 正确映射
3. 立即刷新 → 变动值带 `+/-` 和颜色
4. 错 Key → 401 提示且不重试
5. 阈值 > 余额 → 低余额告警（不重复轰炸）；变动阈值设 0.1% → 异常告警
6. 连续启动两次 → **只有一个窗口**，第二次激活已有窗口
7. 关窗口 → 仅隐藏不退出；托盘退出 → 无残留进程/图标
8. `regedit` 查 `HKCU\...\Run`
9. 重启 → 位置/设置/基线恢复；删 config.json → 自动重建（旧的留 `.corrupt.bak`）
10. 拖出屏 → 托盘「恢复默认位置」
11. 429 时确认 `Retry-After` 等待可取消

## 14. 验收标准（发布前清单，含二轮新增项）

- [ ] snake_case 字段正确显示（非空）
- [ ] CNY/USD 不静默混用；缺目标币种显示「未返回 CNY 余额」/「未支持币种」
- [ ] 首次刷新 / 重启后首次 / 零余额 / 上升 / 下降 / 网络失败语义清晰
- [ ] 低余额与异常下降不重复轰炸（冷却生效）
- [ ] 重启后低余额显示状态与弹窗告警行为符合定义
- [ ] API Key 不进日志与命令行历史
- [ ] 配置损坏可恢复；**第一次保存配置（目标不存在）不报错**
- [ ] 配置写入中断/临时文件残留可清理
- [ ] 点击关闭只隐藏不退出；托盘退出无残留
- [ ] 窗口出屏可经托盘恢复（含负坐标/多显示器）
- [ ] **连续启动两次只有一个窗口和一个轮询器**
- [ ] **429 `Retry-After` 等待可取消**
- [ ] **API 返回未知币种时 UI 显示明确未支持状态**
- [ ] **`Topmost` 设置变更立即生效**
- [ ] **`normal/drop/low` Mock 场景有明确基线共享方式（sequence）**
- [ ] **WPF + WinForms 类型名冲突可正常编译**
- [ ] 退出时在途请求被取消
- [ ] 发布后目标机可启动并保存配置
- [ ] .NET 目标版本符合 LTS 计划（**已知项：.NET 8 于 2026-11-10 EOL，发布前评估升级 .NET 10**）

## 15. Backlog（本期不做）

- 透明度调节、临时隐藏
- 完整多显示器/高 DPI 矩阵（本期仅「出屏检测 + 恢复默认位置」）
- 全屏应用 / UAC 安全桌面下 Toast 行为
- 代码签名 / 自动升级策略
- 完整单元测试矩阵扩展

## 16. v2 → v3 修订记录

| 修订 | 来源 |
|---|---|
| `Topmost` 改为代码后置，删 XAML 绑定 | 二轮 2.1 |
| `AlertState` record 完整建模 + 持久化策略 + 重启语义 | 二轮 2.2 |
| `File.Replace` 增加首次保存 `File.Move` 分支 | 二轮 2.3 |
| 关闭/退出生命周期（ShutdownMode、Closing 隐藏、托盘退出清理） | 二轮 2.4 |
| 单实例 Mutex + 激活已有窗口 提档为首版 | 二轮 2.5 |
| 重试等待策略（Retry-After 上限 30s / 500-1000ms，可取消） | 二轮 3.1 |
| Parser 不拒未知币种，改由 CurrencySelector 判定 | 二轮 3.2 |
| Mock 增加 `sequence` 场景共享基线 | 二轮 3.3 |
| 失败保留余额归 MainWindow 协调者，不进 AlertEvaluator 测试 | 二轮 3.4 |
| UTC 序列化；`windowLeft/Top` null=默认位置 | 二轮 4 |
| WPF+WinForms 类型全限定名；托盘 App 持有；双击=显示激活 | 二轮 5 |
| 新增对应验收项 | 二轮 7 |

## 关键文件

- `src/DeepSeekBalanceWidget/DeepSeekBalanceWidget.csproj`
- `src/DeepSeekBalanceWidget/Program.cs`（单实例）
- `src/DeepSeekBalanceWidget/App.xaml.cs`（生命周期/托盘）
- `src/DeepSeekBalanceWidget/MainWindow.xaml`（悬浮窗 + 代码后置 Topmost + 拖动）
- `src/DeepSeekBalanceWidget/Services/AlertEvaluator.cs`（告警状态机 + AlertState）
- `src/DeepSeekBalanceWidget/Services/ConfigService.cs`（原子写含首次分支 + DPAPI + 损坏恢复）
- `src/DeepSeekBalanceWidget/Services/BalanceParser.cs`（严格解析，不拒未知币种）
