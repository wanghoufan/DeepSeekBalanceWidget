# DeepSeek 余额悬浮小工具 —— 实现方案 v4

- 日期：2026-07-31
- 技术栈：C# WPF，**.NET 8 LTS**（`net8.0-windows`，用户已确认，开发机 SDK 8.0.423 已装）
- 状态：已通过三轮审查（`PLAN_REVIEW.md` / `PLAN_v2_REVIEW.md` / `PLAN_v3_REVIEW.md`），本 v4 整合三轮全部必须修正项
- 版本关系：v4 取代 v3。changelog：v3 → v4 见第 16 节

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

## 3. 项目结构（三审修订：去掉自定义 Program.cs）

```
orca_1/
├── DeepSeekBalanceWidget.sln
├── src/DeepSeekBalanceWidget/
│   ├── DeepSeekBalanceWidget.csproj   # net8.0-windows, UseWPF, UseWindowsForms, ProtectedData 包
│   ├── App.xaml / App.xaml.cs         # 唯一入口：单实例 Mutex + 生命周期 + 持有托盘
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
│   │   ├── AlertEvaluator.cs          # 纯函数：告警判定
│   │   ├── ConfigService.cs           # config.json 原子读写 + DPAPI + 损坏恢复
│   │   ├── AutoStartService.cs        # 注册表自启
│   │   ├── ToastService.cs            # 弹/关告警窗口
│   │   └── TrayIconService.cs         # 托盘图标 + 菜单（WinForms NotifyIcon）
│   └── Assets/icon.ico
└── tests/DeepSeekBalanceWidget.Tests/  # 仅核心测试（2 文件 + 并入的 Calculator）
    ├── BalanceParserTests.cs
    └── AlertEvaluatorTests.cs         # 含 BalanceChangeCalculator 用例
```

**入口策略（三审 2.1，真编译问题）**：**不写自定义 `Program.cs`**。标准 WPF SDK 项目从 `App.xaml` 自动生成 `Main`，再自定义入口会触发 CS0017 重复入口。单实例 Mutex 检查放在 **`App.OnStartup` 最前面**（在初始化配置/托盘/网络**之前**），第二实例快速 `Shutdown()` 退出，绝不先初始化资源。

csproj 要点：`OutputType=WinExe`、`UseWPF=true`、`UseWindowsForms=true`（托盘）、`PackageReference System.Security.Cryptography.ProtectedData 8.0.0`。**类型名冲突**：`Application`/`Timer`/`MessageBox` 等用全限定名（`System.Windows.Application` / `System.Windows.Forms.Timer`）。

## 4. 数据模型与解析

DTO 与业务模型分离，snake_case 用 `[JsonPropertyName]` 显式映射：

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

**`BalanceParser`**：
- 校验：空响应 / 缺 `balance_infos` / 空数组 / 币种为空 / 金额非法 / 金额为负 → 解析错误（进明确错误态，**不默认为 0**）
- **不拒绝未知币种**：币种非空即通过，由 `CurrencySelector` 判定支持与否
- **合计一致性（三审 2.3）**：改为容差**非阻断诊断** —— `Math.Abs(total - (granted + topped_up)) > 0.01m` 时仍显示 `total`（权威值），细分行标注「待核对」，**不把整次响应当网络错误、不清零**

**`CurrencySelector`**：优先 `selectedCurrency`（默认 CNY）；目标币种缺失/不支持 → 「币种不支持」状态，UI 显示「未返回 CNY 余额」/「未支持币种」。**不静默取第一条，不静默切 USD**。UI 始终带货币单位（`¥110.00`）。

## 5. 告警状态机

```csharp
public sealed record AlertState(
    bool HasBaseline,                          // 派生：由 lastSuccessfulBalance.HasValue 推导，不单独存储
    decimal? LastSuccessfulBalance,            // 上次成功余额（跨重启持久化）
    DateTimeOffset? LastSuccessfulRefreshUtc,  // 上次成功刷新时间（跨重启持久化）
    bool InLowBalanceState,                    // 低余额状态（跨重启持久化）
    DateTimeOffset? LastLowBalanceAlertUtc,    // 上次低余额告警时间（仅内存）
    DateTimeOffset? LastAbnormalAlertUtc);     // 上次异常下降告警时间（仅内存）
```

`AlertEvaluator` 纯函数：输入 `AlertState` + 本次 `ParsedBalance` + 配置 → 输出 `AlertDecision`（是否弹低余额 / 是否弹异常下降 / 新 `AlertState`）。

**`HasBaseline` 派生规则（三审 2.2）**：由 `lastSuccessfulBalance.HasValue` 推导（`decimal?`，合法余额 0 时 `HasValue=true`，不误判）。**重启语义**：
- 配置**有**基线：重启后第一轮成功刷新**只更新显示和基线，不弹异常告警**（不重放历史事件）
- 配置**无**基线：第一轮成功刷新建立基线，不显示差额、不告警

**规则**：
- 首次成功刷新：建立基线，不显示变动、不告警
- 网络失败：保留上次成功余额，显示「上次成功刷新时间」，不清零、不告警
- 上次余额为 0：不计算下降百分比
- 只有下降触发异常下降告警；上升显示正数（绿色）但不告警
- 变动百分比：`(上次余额 - 本次余额) / 上次余额 * 100`

**低余额**（进入状态 + 冷却 + 迟滞）：首次从「≥ 阈值」变「< 阈值」立即提醒一次；持续低于按 `lowBalanceCooldownSeconds`（默认 1800s，仅内存）限频；恢复 ≥ 阈值 → `InLowBalanceState=false`（持久化）解除，之后再次跌破可再提醒。
**异常下降**：每次独立事件最多提醒一次，冷却 `abnormalAlertCooldownSeconds`（默认 600s，仅内存）；重启不重放。

## 6. 网络层

`DeepSeekApiClient`：

- 复用单个 `HttpClient`（超时 10s）；**换 API Key 时 Dispose 旧客户端再重建**（三审 5）
- 请求带 `CancellationToken`，退出取消；**取消不显示为网络错误 Toast**（三审 5）
- 状态分类与重试：
  - `401 / 403` → 不重试，提示认证问题；**暂停轮询**，直到改 Key 或手动刷新（三审 5）
  - `429` → **用 `response.Headers.RetryAfter` 类型化解析**（兼容秒数与 HTTP 日期，三审 5），合法则按值等待（上限 30s，可取消）
  - `408 / 5xx / 网络异常` → 最多重试 1 次，等待 500–1000ms（可取消）
  - 其他 → 保留状态码 + 服务端错误信息；**不把 402 硬编码为「欠费」**；**不原样显示/记录 HTTP 错误体**（可能含敏感信息，三审 5）
- 本轮重试在同一刷新周期内；`_isRefreshing` 防下次 Tick 叠加
- 轮询失败 → 保留上次成功余额并显示「上次成功刷新时间」

## 7. 配置与安全

位置：`%APPDATA%\DeepSeekBalanceWidget\config.json`。

- **原子写入**：写 `config.json.tmp`（同目录，UTF-8）→ 目标**存在**则 `File.Replace(tmp, target, backup)`，**不存在**则 `File.Move(tmp, target)`；异常时删临时文件。**保存失败保留旧配置**（三审 4）
- **写入串行化**：配置写入用锁串行，避免定时刷新与设置保存并发覆盖（三审 4）
- **启动清理**：清理崩溃遗留的 `.tmp`（三审 4）
- **损坏恢复**：读取失败 → 改名为 **`config.json.corrupt-<UTC时间戳>.bak`**（带时间戳防同名覆盖失败，三审 4）→ 重建默认配置并提示
- **API Key**：DPAPI 加密（`ProtectedData.Protect`，CurrentUser）+ Base64；`PasswordBox` 默认不显示完整 Key；不落日志/异常/调试输出；提供「清除 API Key」
- `configVersion`；**输入校验**：刷新间隔 5–3600s、阈值 ≥ 0、异常百分比 0–100、冷却 ≥ 0
- `DateTimeOffset` 一律 UTC 序列化；`windowLeft/windowTop` 为 `null` 表示「未设置 → 走默认位置」（**0 是合法坐标**）

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

**`Topmost` 用代码后置**（不用 XAML 绑定）：启动 `Topmost = config.IsAlwaysOnTop;`，设置变更保存后立即重新赋值（三审 6）。XML 注释不写在开始标签属性列表内。右键菜单：立即刷新 / 设置 / 恢复默认位置 / 退出。

### 8.2 拖动与位置

- `MouseLeftButtonDown → DragMove()`，只允许卡片空白区域触发，包 try/catch
- 拖动结束防抖（约 500ms）保存 `Left/Top`
- **出屏检测（DPI 注意，三审 6）**：`Screen.AllScreens` 是 WinForms 像素坐标，WPF `Left/Top` 是 DIP，不能直接比较 → 用 WPF 原生 `SystemParameters.WorkArea`（DIP）做判断，或做 DPI 换算；出屏则恢复默认位置。完整多显示器矩阵仍列 backlog

### 8.3 托盘

- `TrayIconService` 由 `App` 持有，创建时设 `Icon`，退出时 `Dispose()`（三审 6）
- **双击托盘 = 显示并激活窗口**；「恢复默认位置」为独立菜单项
- 菜单：显示窗口 / 立即刷新 / 设置 / 恢复默认位置 / 退出
- **`Closing` 区分**用户点关闭（取消 + 隐藏）、托盘退出（`_isExiting=true`，不拦截）

### 8.4 生命周期

- `App.ShutdownMode = OnExplicitShutdown`
- `MainWindow.Closing`：非显式退出时 `e.Cancel = true; Hide();`
- 托盘「退出」：`_isExiting=true` → `_cts.Cancel()` → `NotifyIcon.Dispose()` → `Close()` → `Application.Shutdown()`
- 退出时取消 `CancellationTokenSource`，等待在途刷新（带超时）

### 8.5 Toast

自绘 `ToastWindow`：`Topmost` + `ShowActivated="False"` + `ShowInTaskbar="False"` + `Owner=主窗口`，显示主窗旁，5s 自关。位置按工作区边界计算。不声称「100% 可靠」；全屏/UAC 场景列 backlog。

## 9. 单实例（三审 2.1 / 3：入口与细节）

**放在 `App.OnStartup` 最前**，在初始化配置/托盘/网络之前：

- 首实例：`new Mutex(true, "DeepSeekBalanceWidget_SingleInstance", out createdNew)`；持有后，后台线程在命名 `EventWaitHandle`（**`EventResetMode.AutoReset`**）上 `WaitOne()`，收到激活事件 → `Dispatcher.Invoke(() => { Show(); WindowState = Normal; Activate(); })`（托盘隐藏时也要能重新显示）
- 第二实例：`Mutex.WaitOne(0)` 失败 → `EventWaitHandle.Set()` 通知首实例 → **立即 `Shutdown()`，不初始化任何资源**；通知失败则安全退出，不阻塞
- **处理 `AbandonedMutexException`**（首实例被异常终止后 Mutex 遗留）→ 视为可获得，继续启动

## 10. Mock 与测试

### Mock 模式

`IBalanceProvider` 接口，`MockBalanceService` 实现。启动参数 `--mock-scenario`：

```text
--mock-scenario normal       # 正常 110（granted 10 / topped_up 100）
--mock-scenario drop         # 下降 2.5 元
--mock-scenario low          # 掉到 8 元（低余额告警）
--mock-scenario unavailable  # is_available=false（红色提示）
--mock-scenario error        # 抛异常（错误态不清零）
--mock-scenario sequence     # 单次运行依次：normal→drop→low→unavailable→error（共享基线）
```

### 单元测试（维持「只测核心纯服务」裁剪，Calculator 并入）

- `BalanceParserTests`：空响应 / 缺 balance_infos / 空数组 / 未知币种（不拒）/ 非法金额 / 空金额 / 负金额 / 余额为 0 / **合计不一致容差（>0.01 才标记，不阻断）**
- `AlertEvaluatorTests`（**含 BalanceChangeCalculator 用例**）：首次无基线 / 重启不重放 / 零余额不算百分比 / 上升不告警 / 下降触发 / 低余额冷却 / 迟滞解除 / 上升/下降/零基线差值
- 职责边界：「网络失败保留余额」是 `MainWindow` 协调者职责，不在 AlertEvaluator 测试中；由 Mock `error` 场景验证
- 其余（ConfigService 并发、单实例激活、退出取消）走 Mock + 手动验证（三审 7 部分采纳，维持裁剪决策）

## 11. 开机自启

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`：`Set("DeepSeekBalanceWidget", "\"<exe路径>\"")`，取消时 `DeleteValue`。无需管理员权限，路径加双引号防空格。

## 12. 实现步骤

1. **脚手架 + 单实例**：`dotnet new sln` + `dotnet new wpf`（net8.0-windows，UseWPF+UseWindowsForms），加 ProtectedData 包；`App.OnStartup` 首行做 Mutex/EventWaitHandle 单实例；`dotnet build` 通过
2. **纯逻辑层 + 单测**：DTO/`ParsedBalance`/`AlertState`/`JsonPropertyName` → `BalanceParser`、`CurrencySelector`、`BalanceChangeCalculator`、`AlertEvaluator`；写 `BalanceParserTests` + `AlertEvaluatorTests`
3. **Mock UI + 悬浮窗**：主窗口（代码后置 Topmost、拖动防抖、出屏恢复）、`MockBalanceService`（含 sequence）、错误态
4. **配置 + 设置窗口**：`ConfigService`（DPAPI / 原子写含首次分支 / 时间戳备份 / tmp 清理 / 串行化 / UTC / null=默认位置）、`SettingsWindow`（PasswordBox / 清除 Key / 币种选择）
5. **真实 API + 轮询**：`DeepSeekApiClient`、30s `DispatcherTimer`、取消、RetryAfter 类型化、401/403 暂停轮询、失败保留上次余额
6. **告警 + 托盘 + 生命周期**：`AlertEvaluator` 接入、`ToastService`/`ToastWindow`、`TrayIconService`（App 持有）、`ShutdownMode=OnExplicitShutdown`、关闭即隐藏
7. **自启 + 发布验证**：`AutoStartService`、`dotnet publish -c Release -r win-x64 --self-contained true`

## 13. 验证

### Mock（无 Key）

`--mock-scenario sequence` 单次运行验证全链路：正常显示 → 变动差值 → 低余额告警（+冷却）→ 红色不可用态 → 错误态不清零。再单独跑 `unavailable`/`error` 复核。

### 真实 Key（三审 2.4：修正 PowerShell 写法）

```powershell
$env:DSK_KEY = "sk-..."    # 用临时环境变量，不写进 shell 历史
curl.exe -H "Authorization: Bearer $env:DSK_KEY" https://api.deepseek.com/user/balance
```

1. 设置窗口填 Key → 主窗显示 `¥` 余额（含充值/赠送细分），验证 snake_case 映射
2. 立即刷新 → 变动值带 `+/-` 和颜色
3. 错 Key → 401 提示、**轮询暂停**；改 Key 或手动刷新恢复
4. 阈值 > 余额 → 低余额告警（不轰炸）；变动阈值设 0.1% → 异常告警
5. 连续启动两次 → 只有一个窗口，第二次激活已有窗口
6. 关窗口 → 仅隐藏；托盘退出 → 无残留进程/图标
7. `regedit` 查 `HKCU\...\Run`
8. 重启 → 位置/设置/基线恢复；删 config.json → 自动重建（旧的留 `.corrupt-时间戳.bak`）
9. 拖出屏 → 托盘「恢复默认位置」
10. 429 时确认 `Retry-After` 等待可取消

## 14. 验收标准（发布前清单）

- [ ] snake_case 字段正确显示（非空）
- [ ] CNY/USD 不静默混用；缺目标币种显示明确未支持状态
- [ ] 首次刷新 / 重启后首次 / 零余额 / 上升 / 下降 / 网络失败语义清晰
- [ ] 低余额与异常下降不重复轰炸（冷却生效）
- [ ] 重启后低余额显示状态与弹窗告警符合定义
- [ ] API Key 不进日志与命令行历史
- [ ] 配置损坏可恢复（时间戳备份）；第一次保存（目标不存在）不报错；写入中断/tmp 残留可清理；并发写入不互相覆盖
- [ ] 点击关闭只隐藏；托盘退出无残留
- [ ] 窗口出屏可经托盘恢复（含负坐标/多显示器）
- [ ] 连续启动两次只有一个窗口和一个轮询器；第二实例不初始化资源
- [ ] `AbandonedMutexException` 可正常恢复
- [ ] 429 `Retry-After`（秒数/HTTP 日期）等待可取消
- [ ] 401/403 后轮询暂停，改 Key 恢复
- [ ] 退出取消请求不显示错误 Toast
- [ ] `Topmost` 设置变更立即生效
- [ ] Mock `sequence` 场景基线共享正确
- [ ] WPF + WinForms 类型名冲突可正常编译
- [ ] 出屏检测基于 DIP（SystemParameters.WorkArea），非像素直比
- [ ] 退出时在途请求被取消
- [ ] 发布后目标机可启动并保存配置
- [ ] .NET 目标版本符合 LTS 计划（**已知项：.NET 8 于 2026-11-10 EOL，发布前评估升级 .NET 10**）

## 15. Backlog（本期不做）

- 透明度调节、临时隐藏
- 完整多显示器/高 DPI 矩阵（本期仅 DIP 出屏检测 + 恢复默认位置）
- 全屏应用 / UAC 安全桌面下 Toast 行为
- 代码签名 / 自动升级策略
- 完整单元测试矩阵扩展（ConfigService 并发、单实例、退出取消等）

## 16. v3 → v4 修订记录

| 修订 | 来源 |
|---|---|
| 去掉自定义 `Program.cs`，单实例 Mutex 移入 `App.OnStartup`（防 CS0017 重复入口），第二实例不初始化资源 | 三审 2.1 / 3 |
| `HasBaseline` 由 `lastSuccessfulBalance.HasValue` 派生，不单独存储；重启首刷语义明确 | 三审 2.2 |
| 合计一致性改容差（>0.01m）非阻断诊断，不清零 | 三审 2.3 |
| PowerShell 验证命令改 `$env:DSK_KEY` + `curl.exe` | 三审 2.4 |
| `EventWaitHandle` 用 `EventResetMode.AutoReset`；处理 `AbandonedMutexException` | 三审 3 |
| 腐坏备份名带 UTC 时间戳；启动清理 `.tmp`；写入串行化；保存失败保留旧配置；UTF-8 | 三审 4 |
| `Retry-After` 用 `response.Headers.RetryAfter` 类型化解析 | 三审 5 |
| 401/403 暂停轮询直到改 Key/手动刷新 | 三审 5 |
| 取消请求不显示错误 Toast；换 Key Dispose 旧 HttpClient；不原样显示错误体 | 三审 5 |
| 出屏检测用 DIP（SystemParameters.WorkArea），标注 DPI 已知限制 | 三审 6 |
| 测试：BalanceChangeCalculator 并入 AlertEvaluatorTests，其余维持裁剪 | 三审 7 |

## 关键文件

- `src/DeepSeekBalanceWidget/DeepSeekBalanceWidget.csproj`
- `src/DeepSeekBalanceWidget/App.xaml.cs`（唯一入口：单实例 + 生命周期 + 托盘持有）
- `src/DeepSeekBalanceWidget/MainWindow.xaml`（悬浮窗 + 代码后置 Topmost + 拖动 + DIP 出屏检测）
- `src/DeepSeekBalanceWidget/Services/AlertEvaluator.cs`（告警状态机 + AlertState + 容差）
- `src/DeepSeekBalanceWidget/Services/ConfigService.cs`（原子写含首次分支 + 时间戳备份 + 串行化 + DPAPI）
- `src/DeepSeekBalanceWidget/Services/BalanceParser.cs`（严格解析，不拒未知币种）
- `src/DeepSeekBalanceWidget/Services/DeepSeekApiClient.cs`（RetryAfter 类型化 + 401/403 暂停轮询）
