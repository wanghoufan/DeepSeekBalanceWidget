# DeepSeek 余额悬浮小工具 —— 实现方案（v1，待审查）

- 日期：2026-07-31
- 技术栈：C# WPF，.NET 8 LTS（`net8.0-windows`）
- 状态：设计稿，尚未编码。目标机器：Windows 11
- 审查关注点：见文末「开放问题」

## 1. 背景与需求

用户需要一个 Windows 11 桌面悬浮小工具，时刻监控 **DeepSeek 开放平台的 API 余额**（人民币），避免余额耗尽影响 API 调用。项目根目录 `C:\Users\ZhuanZ\Desktop\orca_1\orca_1` 目前完全空白（仅空 git 仓库），从零搭建。

已与用户确认的功能需求：

- **透明悬浮窗**：无边框、透明背景、置顶（Topmost）、不进任务栏、可随意拖动、贴桌面角落
- **显示内容**：
  - 当前总余额（CNY）
  - 最近变动信息：上次刷新时间、与上次刷新相比的差额（带 `+/-` 和颜色）
  - 充值余额（`topped_up`）vs 赠送余额（`granted`）细分
  - 账户不可用（`is_available = false`）时醒目变色提示
- **定时刷新**：默认每 30 秒轮询，可配置
- **告警**：
  - 低余额告警：余额低于设定阈值时弹出提醒
  - 异常变动监控：单次刷新下降超过设定比例时提醒
- **设置窗口**：API Key、刷新间隔、告警阈值，配置持久化
- **可选开机自启**（注册表 Run 键）

## 2. API 契约（已通过官方文档确认）

- 请求：`GET https://api.deepseek.com/user/balance`
- 请求头：`Authorization: Bearer <API_KEY>`，无其他参数
- 200 响应示例：

```json
{
  "is_available": true,
  "balance_infos": [
    {
      "currency": "CNY",
      "total_balance": "110.00",
      "granted_balance": "10.00",
      "topped_up_balance": "100.00"
    }
  ]
}
```

字段说明：

| 字段 | 类型 | 含义 |
|---|---|---|
| `is_available` | bool | 账户是否有余额可供 API 调用 |
| `balance_infos[].currency` | string | 货币，`CNY` 或 `USD` |
| `balance_infos[].total_balance` | string | 总可用余额（充值 + 赠送） |
| `balance_infos[].granted_balance` | string | 未过期的赠送余额 |
| `balance_infos[].topped_up_balance` | string | 充值余额 |

注意：
- 余额字段是**字符串**，需 `decimal.TryParse(..., CultureInfo.InvariantCulture)` 转换
- 响应可能含 CNY 和 USD 两条目，需按币种处理

## 3. 框架版本决策

- **选 .NET 8 LTS**。依据：开发机已装 `.NET SDK 8.0.423` + `Microsoft.WindowsDesktop.App 8.0.29`，开箱即编译即运行；WPF 自 .NET Core 3 后基本冻结，9/10 对 WPF 无实质新增，不值得为此安装新 SDK。
- 备选：.NET 10 LTS（支持期到 2028），需额外装 SDK；如将来升级只需改 TFM，代码零改动。

## 4. 项目结构

```
orca_1/
├── DeepSeekBalanceWidget.sln
└── src/DeepSeekBalanceWidget/
    ├── DeepSeekBalanceWidget.csproj   # net8.0-windows, UseWPF, ProtectedData 包, icon
    ├── App.xaml / App.xaml.cs         # 入口，加载配置
    ├── MainWindow.xaml / .cs          # 悬浮主窗口（透明/置顶/可拖动/右键菜单）
    ├── SettingsWindow.xaml / .cs      # 设置窗口
    ├── ToastWindow.xaml / .cs         # 告警弹窗（无边框小窗，5s 自动关闭）
    ├── Models/
    │   ├── BalanceResponse.cs         # API 响应模型（余额字段为 string）
    │   └── AppConfig.cs               # 配置模型
    ├── Services/
    │   ├── IBalanceProvider.cs        # 统一余额获取接口
    │   ├── DeepSeekApiClient.cs       # 真实 API 实现
    │   ├── MockBalanceService.cs      # 离线模拟（无 Key 也能测）
    │   ├── ConfigService.cs           # config.json 读写 + DPAPI
    │   ├── AutoStartService.cs        # 注册表自启
    │   └── ToastService.cs            # 弹/关告警窗口
    └── Assets/icon.ico
```

**不引入 MVVM 框架**（Prism/MvvmLight 等），主窗口代码后置直接写逻辑，这是小工具最务实的方式。

csproj 骨架要点：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <RootNamespace>DeepSeekBalanceWidget</RootNamespace>
    <ApplicationIcon>Assets\icon.ico</ApplicationIcon>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Security.Cryptography.ProtectedData" Version="8.0.0" />
  </ItemGroup>
</Project>
```

注意：`System.Security.Cryptography.ProtectedData`（DPAPI）在 .NET Core/5+ 上是 **NuGet 包**，必须显式引用。

## 5. WPF 关键技术点

### 5.1 透明无边框置顶窗口

```xml
<Window x:Class="DeepSeekBalanceWidget.MainWindow"
        WindowStyle="None"               <!-- 去边框 -->
        AllowsTransparency="True"        <!-- 允许透明（圆角透明角的必要条件） -->
        Background="Transparent"
        Topmost="True"                   <!-- 置顶 -->
        ShowInTaskbar="False"            <!-- 不占任务栏 -->
        ResizeMode="NoResize"
        WindowStartupLocation="Manual"   <!-- 用保存的 Left/Top 定位 -->
        Width="260" Height="150">
  <Border Background="#E6141418"         <!-- 半透明深色卡片，E6=90% 不透明 -->
          CornerRadius="12"
          BorderBrush="#33FFFFFF"
          BorderThickness="1"
          Padding="12"
          MouseLeftButtonDown="Window_MouseLeftButtonDown">
    <!-- 余额 / 变动 / 充值vs赠送 / 刷新时间 -->
  </Border>
</Window>
```

要点与坑：
- `AllowsTransparency="True"` + `WindowStyle="None"` 是最简单组合，代价是失去 DWM 阴影、内容走软件渲染（几百像素小卡片无感）。若以后边角出现黑色残影，改用 `WindowChrome` 方案，但首版不建议。
- 无标题栏 ⇒ **必须提供右键菜单**：立即刷新 / 设置 / 开机自启 / 退出。

### 5.2 窗口拖动

```csharp
private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (e.ButtonState == MouseButtonState.Pressed)
        this.DragMove();
}
```

拖完在 `OnClosing` 把 `this.Left` / `this.Top` 存进配置，下次启动按 `WindowStartupLocation="Manual"` 恢复位置。

### 5.3 告警通知：三种方案取舍

| 方案 | 优点 | 缺点 | 结论 |
|---|---|---|---|
| **自定义 WPF 弹窗 ToastWindow** | 完全可控外观、Win11 上 100% 可靠、零第三方依赖、不抢焦点 | 需自写 ~40 行窗口逻辑 | **推荐，采用** |
| NotifyIcon 气泡 | 调用简单（`ShowBalloonTip` 一行） | Win11 上旧式气泡已被弱化/可能被通知中心吞掉，且需托盘 + WinForms 互操作 | 弃用 |
| Windows Toast | 系统级、持久 | 必须注册 AppUserModelID + Start Menu 快捷方式，配置极重 | 弃用 |

`ToastWindow` 关键点：`Topmost` + `ShowActivated="False"`（**不抢键盘焦点**，用户打字不被打断）+ `ShowInTaskbar="False"`，显示在主窗口旁，`DispatcherTimer` 5 秒后自动关闭。

### 5.4 定时刷新

- 用 `DispatcherTimer`（`Interval = 配置的秒数`），回调天然在 UI 线程，无需 `Dispatcher.Invoke`。
- 加 `bool _isRefreshing` 标志防重入（慢网络下防止 Tick 堆叠）。

### 5.5 HttpClient + JSON

```csharp
public class DeepSeekApiClient : IBalanceProvider
{
    private readonly HttpClient _http;
    public DeepSeekApiClient(string apiKey)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<BalanceResponse> GetBalanceAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync("https://api.deepseek.com/user/balance", ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new ApiException((int)resp.StatusCode, json);
        return JsonSerializer.Deserialize<BalanceResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
```

- 用内置 `System.Text.Json`，不需要 `IHttpClientFactory`（单 Key 单客户端，实例字段即可）。
- **多币种**：优先取 `currency == "CNY"` 条目；无 CNY 取第一条并标注币种。
- **错误处理**：`401` = Key 无效 / `402` = 欠费 / `429` = 限流，分别给出中文提示；无网/异常显示错误态而非白屏。

## 6. 配置持久化

- **位置**：`%APPDATA%\DeepSeekBalanceWidget\config.json`（不是 exe 同目录，防止 Program Files 等只读目录 + 升级不丢配置）。
- **API Key：DPAPI 加密存储**（`ProtectedData.Protect`，`DataProtectionScope.CurrentUser`，Base64 落盘）。拷走 config.json 也解不开；同用户同机器的进程本身可解密是 DPAPI 设计使然。
- config.json 结构：

```json
{
  "apiKeyEncrypted": "base64...",
  "refreshIntervalSeconds": 30,
  "lowBalanceThreshold": 10.0,
  "abnormalChangePercent": 10.0,
  "useMockData": false,
  "autoStart": false,
  "windowLeft": 120,
  "windowTop": 120
}
```

## 7. 开机自启

`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`，只改当前用户，**无需管理员权限**：

```csharp
using Microsoft.Win32;

public static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DeepSeekBalanceWidget";

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) != null;
    }
}
```

要点：`Environment.ProcessPath` 返回 apphost exe 全路径；值用双引号包住防路径含空格。

## 8. 实现步骤（先跑起来，再叠加）

1. **阶段 0 — 脚手架**：`dotnet new sln` + `dotnet new wpf`（net8.0），加 ProtectedData 包，`dotnet build` 通过
2. **阶段 1 — 悬浮窗**：透明圆角置顶可拖卡片，先硬编码假数据显示余额/充值vs赠送/刷新时间
3. **阶段 2 — 配置 + 设置窗口**：`AppConfig` + `ConfigService`（DPAPI）+ `SettingsWindow`
4. **阶段 3 — 真实 API + 轮询**：`DeepSeekApiClient` + `DispatcherTimer`；总余额、变动值（`+/-` 和颜色）、细分两行、刷新时间；`is_available=false` 整卡变色；错误态
5. **阶段 4 — 告警**：`ToastService` + `ToastWindow`；低余额 + 异常变动（各带防轰炸标志位）
6. **阶段 5 — 自启 + 打磨**：`AutoStartService` 接入设置、记忆窗口位置、立即刷新按钮
7. **阶段 6 — 发布**：`dotnet publish -c Release -r win-x64 --self-contained true`（目标机免装 .NET）

## 9. 验证方式

### 无真实 Key（离线 Mock 模式）

`IBalanceProvider` 接口，`DeepSeekApiClient` 与 `MockBalanceService` 各实现之；配置 `useMockData` 或启动参数 `--mock` 切换。Mock 按脚本序列依次吐出，专用于触发各分支：

1. 正常：`{ is_available: true, CNY total: 110, granted: 10, topped_up: 100 }`
2. 下降 2.5 元（验证变动差值显示）
3. 骤降 20%（验证异常变动告警）
4. 掉到 8 元（验证低余额告警）
5. `is_available: false`（验证红色提示）
6. 直接抛异常（验证错误态）

每调一次自动换下一组，肉眼即可验证全部 UI 分支。

### 有真实 Key

1. 先独立验证接口：`curl -H "Authorization: Bearer sk-你的key" https://api.deepseek.com/user/balance`，应返回第 2 节的 JSON 结构
2. 设置窗口填入 Key → 主窗显示 CNY 余额
3. 点"立即刷新" → 变动值从 `+0.00` 变为实际差额
4. 故意填错 Key → 提示 401 认证失败
5. 阈值设高于当前余额 → 弹低余额告警；异常变动阈值设 0.1% → 弹异常告警
6. 勾选自启 → `regedit` 检查 `HKCU\...\Run` 下有 `DeepSeekBalanceWidget`
7. 重启应用 → 窗口位置、设置被恢复；删除 config.json → 兜底重建默认配置

## 10. 开放问题（请审查者确认）

1. .NET 8 vs .NET 10 的选择是否认可？（机器现状：仅装 SDK 8）
2. 告警弃用 NotifyIcon 气泡、改用自绘 ToastWindow，是否合理？
3. API Key 用 DPAPI（User scope）加密是否足够？是否需要 entropy？
4. 单 HttpClient 长连接 + 10s 超时，是否需要重试/退避策略？
5. 异常变动阈值「单次下降超 X%」的判定是否需要冷却时间？
6. 多币种处理：仅优先 CNY 是否够，还是 USD 也要显示？
7. 配置放 `%APPDATA%` 而非 exe 目录，是否有异议？
8. Mock 模式的启动参数命名/默认值（默认关）是否合适？

## 关键文件（规划路径）

- `src/DeepSeekBalanceWidget/DeepSeekBalanceWidget.csproj`
- `src/DeepSeekBalanceWidget/MainWindow.xaml`
- `src/DeepSeekBalanceWidget/Services/DeepSeekApiClient.cs`
- `src/DeepSeekBalanceWidget/Services/ConfigService.cs`
- `src/DeepSeekBalanceWidget/Services/ToastService.cs`
