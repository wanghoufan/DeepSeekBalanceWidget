# PLAN_v3 审核报告

审核对象：`PLAN_v3.md`  
审核日期：2026-07-31  
结论：v3 已解决 v1/v2 的主要设计问题，整体可以进入实现阶段；但有 4 个实现级问题建议在创建项目后立即修正，其中 2 个可能直接导致编译或验证失败。

## 1. v3 已正确解决的内容

- 增加了单实例 Mutex 和第二实例激活机制；
- 明确了 WPF `Topmost` 使用代码后置，不再使用无 DataContext 的绑定；
- 增加了完整的 `AlertState` 和重启语义；
- 修复了配置首次保存时 `File.Move` / `File.Replace` 分支；
- 明确了窗口关闭仅隐藏、托盘退出和显式 Shutdown 生命周期；
- 明确了 429 的 `Retry-After` 等待和取消；
- Parser 不再拒绝未来新增币种；
- Mock 增加 sequence 场景；
- 托盘由 `App` 持有并在退出时释放；
- 验收清单已经覆盖大部分关键边界。

## 2. 必须在编码前修订的问题

### 2.1 `Program.cs` 与 WPF `App.xaml` 的入口点需要明确

v3 同时规划了自定义 `Program.cs` 作为入口、`App.xaml / App.xaml.cs`，以及 `dotnet new wpf` 创建项目。标准 WPF SDK 项目通常会从 `App.xaml` 生成入口；再增加带 `Main` 的 `Program.cs`，可能出现重复入口点（CS0017），或者自定义入口没有实际生效。

建议采用方案 A：保留 WPF 生成的入口，在 `App.OnStartup` 中完成 Mutex 检查；第二实例发送激活事件后退出。这样不需要 `Program.cs`，也更自然地集成 WPF Dispatcher、托盘和 Shutdown 生命周期。

如果坚持自定义 `Program.Main`，必须在 csproj 中明确 `StartupObject`，并处理 `App.xaml` 的生成入口，先以 `dotnet build` 验证，不能只按目录结构创建文件。

### 2.2 `AlertState` 与 JSON 配置字段不完全一致

`AlertState` 包含 `HasBaseline`、`LastSuccessfulBalance`、`LastSuccessfulRefreshUtc`、`InLowBalanceState` 和两个告警时间戳，但配置 JSON 没有 `hasBaseline`。需要明确 `HasBaseline` 是否由 `LastSuccessfulBalance != null` 推导，以及空余额是否可能是合法基线。

建议显式定义：配置有基线时，重启后的第一轮成功刷新只更新显示和基线，不弹异常告警；配置无基线时，第一轮成功刷新建立基线，不显示差额、不告警。若需要处理低余额状态，应把“显示状态”和“弹窗告警”分开。

### 2.3 合计一致性不建议严格等式判错

`total == granted + topped_up` 可能因舍入、精度或账务更新时序出现极小差异。建议允许 `0.01` 左右的 decimal 容差；超出容差时显示“余额数据不一致”或标记细分待核对，不要默认清零或把整次响应当网络错误。

### 2.4 PowerShell 验证命令写法错误

PowerShell 环境变量应使用 `$env:DSK_KEY`，而不是 `$DSK_KEY`；`curl` 可能是 `Invoke-WebRequest` 别名，建议写 `curl.exe`：

```powershell
$env:DSK_KEY = "sk-..."
curl.exe -H "Authorization: Bearer $env:DSK_KEY" https://api.deepseek.com/user/balance
```

或使用：

```powershell
Invoke-RestMethod -Uri "https://api.deepseek.com/user/balance" `
  -Headers @{ Authorization = "Bearer $env:DSK_KEY" }
```

## 3. 单实例实现需要补充的细节

- `EventWaitHandle` 明确使用 `EventResetMode.AutoReset`；
- 处理 `Mutex` 被异常终止后的 `AbandonedMutexException`；
- 第二实例通知失败时应安全退出，不得阻塞；
- 首实例收到激活事件时，要在 UI 线程执行 `Show()`、`WindowState=Normal`、`Activate()`；
- 主窗口在托盘隐藏时，激活事件必须重新显示；
- `Program` 方案与 `App.OnStartup` 方案只能选一个；
- 第二实例不得先初始化网络、托盘或配置写入再退出。

## 4. 配置原子写入仍需增强

- `config.json.corrupt.bak` 已存在时改名可能失败，建议备份名带 UTC 时间戳；
- `File.Replace` 的 backup 文件覆盖行为要在 Windows 实机验证；
- 保存失败时必须保留旧配置；
- 启动时清理或恢复崩溃留下的 `.tmp` 文件；
- 配置写入应串行化，避免定时刷新和设置保存并发覆盖；
- 统一使用 UTF-8 写入 JSON。

## 5. 网络与轮询边界

- `Retry-After` 可能是秒数或 HTTP 日期格式，应说明支持范围；
- 认证失败后是否继续每 30 秒请求需要定义，建议暂停轮询直到用户修改 Key 或手动刷新；
- 取消请求不应被显示成网络错误 Toast；
- 更换 API Key 时旧 HttpClient 必须 Dispose；
- HTTP 错误体可能含敏感信息，不要原样写日志或完整显示。

## 6. UI、托盘与 DPI

- `NotifyIcon` 应由 App 持有，右键菜单销毁和退出时 Dispose；
- 主窗口 Closing 必须区分用户点关闭、托盘退出和系统退出；
- `Topmost` 变更应立即作用于当前窗口；
- `Screen.AllScreens` 使用 WinForms 像素坐标，而 WPF `Left/Top` 使用 DIP；多显示器高 DPI 下不能直接比较，必须转换或实机验证；
- 托盘双击应显示并激活窗口，“恢复默认位置”保持独立菜单项。

## 7. 测试建议

两个核心测试文件可以作为最低限度，但建议补测：

- ConfigService 首次保存、损坏恢复、临时文件残留和并发写入；
- 单实例第二次启动激活、Mutex 异常终止恢复；
- BalanceChangeCalculator 上升、下降、零基线和首次刷新；
- 退出时取消请求不会弹出错误告警。

## 8. .NET 8 风险

.NET 8 在当前日期仍可使用，但将于 2026-11-10 结束支持；.NET 10 是当前活跃 LTS，支持至 2028-11-14。若工具会长期运行，发布前应重新评估升级到 .NET 10。

## 9. 最小修改清单

1. 选择 `App.OnStartup` 或自定义 `Program.Main`，修复 WPF 入口策略；
2. 明确 `AlertState` 的 `HasBaseline` 和重启首刷转换；
3. 将余额合计严格等式改为容差或非阻断诊断；
4. 修正 PowerShell 的 `$env:DSK_KEY` / `curl.exe` 示例；
5. 为腐坏配置备份、临时文件和并发写入补充处理；
6. 验证 `Screen.AllScreens` 与 WPF DPI 坐标转换；
7. 定义认证失败后的轮询行为。

## 10. 最终结论

PLAN_v3 已经可以作为实现蓝图。修正第 2.1、2.2、2.3、2.4 四项后，可以进入“脚手架 + 单实例 + 纯逻辑层”阶段。其余问题属于实现验证和边界优化，不再阻塞核心开发。
