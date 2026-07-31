# PLAN_v2 审核报告

审核对象：`PLAN_v2.md`  
审核日期：2026-07-31  
结论：相比 v1 已明显完善，可以进入脚手架阶段，但仍有若干实现级问题需要在编码前修订。

## 1. 已正确修复的内容

以下 v1 高风险问题已处理：

- DTO 与业务模型分离；
- API 的 snake_case 字段使用 `JsonPropertyName`；
- 不再静默把 CNY 切换成 USD；
- 首次刷新、零余额、网络失败和下降百分比规则已明确；
- 增加低余额/异常下降的冷却和迟滞；
- 加入托盘恢复入口；
- 增加取消、有限重试和 HTTP 状态分类；
- DPAPI、PasswordBox、配置损坏恢复和 API Key 不落日志；
- 增加 Mock 命名场景和核心纯服务测试。

## 2. 必须在编码前修订的问题

### 2.1 WPF 的 `Topmost` 绑定目前不可用

方案示例使用：

```xml
Topmost="{Binding IsAlwaysOnTop}"
```

但方案明确“不引入 MVVM”，且没有说明 `DataContext` 或绑定对象初始化。没有设置 `DataContext` 时该绑定不会按预期工作，窗口可能始终使用默认值。

二选一：

1. 使用纯代码后置：启动或设置变更时执行 `Topmost = config.IsAlwaysOnTop;`；
2. 明确创建并设置一个 ViewModel/DataContext。

对于这个小工具，建议采用第一种，删除 XAML 绑定，避免引入半套 MVVM。

### 2.2 告警状态没有完整建模

方案说 `AlertEvaluator` 是“无状态输入的纯函数”，但实际规则需要多个状态：

- `HasBaseline`；
- `LastSuccessfulBalance`；
- `InLowBalanceState`；
- 上次低余额告警时间；
- 上次异常下降告警时间；
- 上一次已处理的异常事件或事件基线。

当前配置 JSON 只有余额和刷新时间，没有 cooldown 时间戳，也没有明确状态对象。需要明确哪些状态只存在内存、哪些跨重启保存。

建议定义：

```csharp
public sealed record AlertState(
    bool InLowBalanceState,
    DateTimeOffset? LastLowBalanceAlertUtc,
    DateTimeOffset? LastAbnormalAlertUtc);
```

建议默认策略：

- 余额基线跨重启保存；
- 告警冷却时间戳只保存在内存；
- 重启后首次成功刷新不重复播放旧告警；
- 如果要求重启后仍保持低余额状态，则必须明确“显示状态”和“弹窗告警”分开处理。

否则“保存基线”和“重启后不重放历史事件”之间会产生矛盾：应用重启后余额大幅下降，到底算新事件还是历史事件必须写清楚。

### 2.3 `File.Replace` 的首次保存路径有问题

`File.Replace(temp, target, backup)` 要求目标文件通常已经存在。第一次保存配置时 `config.json` 不存在，直接调用可能失败。

实现应区分：

```text
目标不存在：File.Move(temp, target)
目标存在：File.Replace(temp, target, backup)
```

同时应保证临时文件与目标文件位于同一目录，并在异常时删除临时文件。备份文件名和是否保留旧配置也应明确。

### 2.4 关闭窗口与退出应用的生命周期必须明确

方案写“窗口关闭仅隐藏，真正退出走托盘菜单”。需要补充：

- `App.ShutdownMode = OnExplicitShutdown`；
- `MainWindow.Closing` 中取消关闭并执行隐藏；
- 托盘“退出”菜单先取消托盘事件，再关闭窗口并调用 `Application.Shutdown()`；
- `NotifyIcon.Dispose()` 必须在退出时执行；
- 应用退出时取消 `CancellationTokenSource`，等待正在进行的刷新结束或在超时后退出。

否则点击窗口关闭按钮可能直接结束进程，或托盘图标残留。

### 2.5 单实例不应简单列为 backlog

应用有“开机自启”，又允许用户手动启动；如果没有单实例锁，很容易出现两个置顶窗口、两组轮询和重复告警。

建议至少在首版增加 named Mutex：

- 第二次启动时把已存在的窗口激活到前台，然后退出第二进程；
- 不需要复杂的跨进程通信，首版可以只实现“单实例 + 激活已有窗口”。

如果确实暂缓，必须在验收清单中明确这是已知限制。

## 3. 需要澄清的设计问题

### 3.1 重试缺少等待策略

方案写“429 读取 Retry-After”，同时写“最多简单重试 1 次”，但没有说明重试是在何时进行。

建议：

- `Retry-After` 合法时按其值等待，但设置上限，例如 30 秒；
- 其他可重试错误等待 500–1000ms；
- 等待支持 `CancellationToken`；
- 轮询下一次 Tick 不应与本次重试叠加。

立即重试可能在 429 时再次触发限流。

### 3.2 `BalanceParser` 不应过早拒绝未知币种

方案写“未知货币 → 解析错误（或按配置降级）”，但 `CurrencySelector` 才负责按币种选择。建议：

- Parser 只校验 `Currency` 非空、金额格式和非负数；
- Selector 决定是否支持目标币种；
- 未知币种应保留为“未支持币种”状态，而不是让整份响应解析失败。

这样 API 将来增加币种时不会导致整个余额查询失败。

### 3.3 Mock 场景与验证步骤不完全一致

v2 的 `normal/drop/low/unavailable/error` 看起来是固定场景，但验证又写“依次跑 normal → drop → low”。如果每个场景是独立启动参数，就无法自然共享上一次余额基线。

需要二选一：

- 增加 `--mock-scenario sequence`，一次运行依次返回多个响应；或
- 明确每个场景只验证单独状态，并在测试中注入上一条基线。

建议保留固定场景 + 增加 `sequence` 场景，以便人工演示和自动测试。

### 3.4 “失败保留余额”不是 `AlertEvaluator` 的测试职责

`AlertEvaluator` 只负责给定输入下的告警判定；网络失败保留 UI 数据是刷新协调器或 ViewModel 的职责。测试应拆开：

- `AlertEvaluator`：输入成功余额和状态，输出告警决策；
- `RefreshCoordinator`：网络失败时保留上一次成功数据显示。

当前文档把两者混在一起，后续实现容易让纯服务承担过多职责。

## 4. 仍建议补充的配置字段

如果采用上面的告警状态设计，建议补充：

```json
{
  "configVersion": 1,
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

并明确 `DateTimeOffset` 使用 UTC 序列化。`windowLeft/windowTop` 为空时走默认位置，而不是把 0 当作有效坐标。

## 5. WPF 与托盘实现注意点

- `UseWPF=true` 与 `UseWindowsForms=true` 可以共存，但要避免同名 `Application`、`Timer`、`MessageBox` 类型冲突，必要时使用完整限定名；
- 托盘对象应由 `App` 持有，而不是由窗口临时创建；
- 托盘图标必须设置 `Icon`，并在退出时 `Dispose`；
- 双击托盘图标应显示并激活窗口，而不是“恢复默认位置”；“恢复默认位置”应是单独菜单项；
- `ShowInTaskbar=false` 的主窗口应通过托盘菜单恢复；
- `Topmost` 配置变更要立即作用于现有窗口；
- `DragMove` 必须只从空白区域触发并包裹异常处理；
- 出屏检测要保留负坐标和多显示器工作区，不能只判断 `Left < 0`。

## 6. .NET 8 决策的风险说明

使用 .NET 8 可以满足当前开发机“无需额外安装”的要求，但它将在 2026-11-10 结束支持；.NET 10 是当前活跃 LTS，支持至 2028-11-14。若该工具会长期运行，应把升级到 .NET 10 作为发布前决策，而不是无限期 backlog。

## 7. 建议增加的验收项

- 第一次保存配置时目标文件不存在；
- 配置写入中断或临时文件残留；
- 点击窗口关闭按钮后只隐藏不退出；
- 通过托盘退出后无残留进程和托盘图标；
- 连续启动两次只出现一个窗口和一个轮询器；
- 429 `Retry-After` 等待可取消；
- API 返回未知币种时 UI 显示明确的未支持状态；
- 重启后低余额显示状态与弹窗告警行为符合定义；
- `Topmost` 设置变更立即生效；
- `normal/drop/low` Mock 场景有明确的基线共享方式；
- WinForms 与 WPF 类型名冲突可正常编译。

## 8. 结论与执行建议

PLAN_v2 已从“概念可行”提升到“可以开始搭建”，但开始写 UI 之前应先修订以下 6 项：

1. 删除或实现 `Topmost` 的 WPF Binding；
2. 明确并建模告警状态及重启行为；
3. 修复首次配置保存时 `File.Replace` 的问题；
4. 明确窗口关闭、托盘退出和应用 ShutdownMode；
5. 将单实例锁提升为首版功能；
6. 统一 Mock sequence 与纯服务测试职责。

修订后即可进入“脚手架 + 数据层”阶段。
