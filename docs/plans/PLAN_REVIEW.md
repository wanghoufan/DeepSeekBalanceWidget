# DeepSeek 余额悬浮小工具：方案审核与 v2 修改建议

审核对象：`PLAN.md`  
审核日期：2026-07-31  
审核结论：总体方向可行，但建议先按本文修订后再进入正式编码。

## 1. 总体结论

原方案的技术路线基本合理：WPF、独立余额服务接口、DPAPI 保存 API Key、AppData 配置、Mock 模式和分阶段实现都可以保留。

当前版本主要问题不在架构方向，而在实现细节：JSON 字段映射可能失败，示例 XAML 可能无法编译，告警逻辑定义不完整，多币种降级策略可能误导用户，配置和网络故障恢复不充分，窗口及发布后的桌面体验还缺少边界处理。

官方 API 契约已核对：

- `GET https://api.deepseek.com/user/balance`
- `Authorization: Bearer <API_KEY>`
- `is_available`
- `balance_infos[].currency` 为 `CNY` 或 `USD`
- `total_balance`、`granted_balance`、`topped_up_balance` 为字符串

参考：[DeepSeek 官方英文文档](https://api-docs.deepseek.com/api/get-user-balance/)、[DeepSeek 官方中文文档](https://api-docs.deepseek.com/zh-cn/api/get-user-balance/)。

## 2. 必须修正的问题

### 2.1 JSON 字段必须显式映射

`PropertyNameCaseInsensitive = true` 只能忽略大小写，不能把 `topped_up_balance` 映射为 `ToppedUpBalance`。模型必须使用 `[JsonPropertyName]`：

```csharp
public sealed class BalanceInfo
{
    [JsonPropertyName("currency")]
    public string Currency { get; init; } = "";

    [JsonPropertyName("total_balance")]
    public string TotalBalance { get; init; } = "";

    [JsonPropertyName("granted_balance")]
    public string GrantedBalance { get; init; } = "";

    [JsonPropertyName("topped_up_balance")]
    public string ToppedUpBalance { get; init; } = "";
}
```

这是最高优先级问题，否则 UI 可能显示默认的 0 值。

### 2.2 修正 XAML 示例

XML 注释不能放在元素开始标签的属性列表中。以下写法不要使用：

```xml
<Window WindowStyle="None" <!-- comment --> AllowsTransparency="True">
```

注释放在开始标签外部，或直接删除示例中的行内注释。

### 2.3 重新表述 .NET 版本策略

截至 2026 年 7 月，.NET 8 仍受支持，但已进入维护阶段，并将于 2026-11-10 结束支持；.NET 10 是当前活跃的 LTS 版本，支持至 2028-11-14。

建议：

- 若目标是立即在现有机器开发，可以暂时使用 .NET 8。
- 若目标是长期使用或公开发布，优先安装并使用 .NET 10。
- 将 .NET 10 写为长期发布目标，而不是仅作为未来备选。

参考：[Microsoft .NET Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy)。

### 2.4 增加严格响应校验

不能直接使用：

```csharp
JsonSerializer.Deserialize<BalanceResponse>(json)!
```

需要处理：空响应、缺失 `balance_infos`、空数组、未知货币、非法金额字符串、负数和不一致的余额合计。建议 API DTO 和业务模型分离，增加解析结果类型：

```csharp
public sealed record ParsedBalance(
    string Currency,
    decimal Total,
    decimal Granted,
    decimal ToppedUp,
    bool IsAvailable);
```

解析失败应进入明确错误态，而不能默认为 0。

### 2.5 不要静默从 CNY 切换到 USD

“没有 CNY 就取第一条”会让用户在监控人民币时突然看到美元余额。建议：

- 设置项增加 `selectedCurrency`，默认 `CNY`；
- 目标币种不存在时显示“未返回 CNY 余额”；
- 可选增加“自动选择”模式；
- UI 始终显示货币单位。

## 3. 余额变动与告警规则

必须明确以下规则：

- 第一次成功刷新没有基线，不显示变动告警；
- 网络失败不应把余额当成 0；
- 上次余额为 0 时不计算下降百分比；
- 只有余额下降才触发异常下降告警；
- 余额上升显示正数，但默认不告警；
- 变动百分比公式：`(上次余额 - 本次余额) / 上次余额 * 100`。

建议保存：

```text
LastSuccessfulBalance
LastSuccessfulRefreshUtc
HasBaseline
```

### 3.1 低余额告警

不要只使用一个布尔标志。建议使用“进入状态 + 冷却 + 迟滞”：

```text
低于阈值时第一次提醒
持续低于阈值时按冷却时间限制提醒
恢复到阈值以上再解除低余额状态
```

建议默认冷却 30 分钟，并允许设置。

### 3.2 异常下降告警

建议每次独立下降事件最多提醒一次，并增加默认 10 分钟冷却时间。应用重启后不要重复提醒历史事件。

## 4. 网络层修改建议

- 401、403：不重试，提示认证或权限问题；
- 429：优先读取 `Retry-After`；
- 408、429、5xx、网络异常：最多重试 1 次，并加入指数退避和随机抖动；
- 请求支持 `CancellationToken`，应用退出时取消；
- 轮询失败时保留上次成功余额，并显示“上次成功刷新时间”；
- 连续失败达到阈值后再显示网络异常提示；
- 不要无依据地把所有 402 响应硬编码为“欠费”，至少保留 HTTP 状态码和服务端错误信息；
- 不要每次轮询都创建 `HttpClient`；更换 API Key 时再重建客户端或更新请求头。

## 5. 安全与配置修改建议

DPAPI `CurrentUser` 方案适合单用户 Windows 工具。entropy 不是必须项，也不会替代 DPAPI 的用户边界。

需要补充：

- 设置窗口用 `PasswordBox`，默认不显示完整 API Key；
- 不把 API Key 写入日志、异常或调试输出；
- 保存配置时采用临时文件 + 原子替换；
- JSON 损坏时保留 `.corrupt.bak` 并重建默认配置；
- 增加 `configVersion`，为后续迁移做准备；
- 提供清除 API Key 功能；
- 修改 API Key 后立即重建或更新 API client；
- 明确 DPAPI 只防止配置文件被直接拷走，不防止当前用户权限下的恶意进程。

建议配置结构：

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

所有用户输入都要校验：刷新间隔不能过小，阈值不能为负，异常百分比应限制在合理范围。

## 6. WPF 桌面体验修改建议

### 6.1 增加托盘入口

主窗口 `ShowInTaskbar=false` 且没有托盘入口时，窗口被拖出屏幕后很难恢复。建议增加托盘菜单：

- 显示窗口；
- 立即刷新；
- 设置；
- 恢复默认位置；
- 退出。

### 6.2 窗口位置与拖动

- 拖动结束后防抖保存位置，不只在 `OnClosing` 保存；
- 启动时检查窗口是否完全在屏幕外；
- 支持负坐标和多显示器；
- 处理 DPI 缩放；
- 只允许卡片空白区域拖动，避免按钮误触发 `DragMove()`；
- 对 `DragMove()` 异常做保护。

### 6.3 置顶模式

建议提供“始终置顶/普通层级”选项，并允许调节透明度或临时隐藏。`Topmost=true` 不应成为唯一固定行为。

### 6.4 Toast

首版使用自绘 Toast 可以保留，但不要声称“100% 可靠”。需要处理全屏应用、UAC 安全桌面、多显示器、屏幕边缘和高 DPI 情况。Toast 位置应根据工作区边界计算。

## 7. Mock 与测试修改建议

除了按序列轮换 Mock，建议支持固定场景参数：

```text
--mock-scenario normal
--mock-scenario drop
--mock-scenario low
--mock-scenario unavailable
--mock-scenario error
```

将以下逻辑抽成可单元测试的纯服务：

- `BalanceParser`
- `BalanceChangeCalculator`
- `AlertEvaluator`
- `CurrencySelector`

必须增加的测试：

- 空 `balance_infos`；
- 只有 USD；
- 同时返回 CNY 和 USD；
- 非法金额、空金额、负金额；
- 余额为 0；
- 首次刷新无基线；
- 401、403、429、500、超时和断网；
- 配置 JSON 损坏；
- API Key 更换后立即生效；
- 多显示器、负坐标、高 DPI、窗口完全出屏；
- 应用退出时仍有请求进行；
- 应用重复启动；
- 发布目录带空格；
- 开机时网络尚未连接。

不要把真实 API Key 直接写进 shell 命令历史。真实 Key 测试应通过设置窗口或安全的临时环境变量完成。

## 8. 推荐的 v2 实施顺序

1. 脚手架、DTO、`JsonPropertyName`、配置版本和输入校验。
2. 余额解析、多币种选择、差额计算和告警判定的纯业务逻辑及单元测试。
3. Mock UI、错误态、位置恢复、多显示器和 DPI 处理。
4. DPAPI、PasswordBox、原子配置写入和损坏恢复。
5. 真实 API、超时、取消、有限重试、HTTP 状态分类。
6. Toast、冷却、迟滞和托盘入口。
7. 自启动、单实例锁、发布验证、代码签名和升级策略。

## 9. 最终验收标准

在进入发布阶段前，应满足：

- 真实 API 的 snake_case 字段能正确显示；
- CNY/USD 不会静默混用；
- 首次刷新、零余额、上升、下降和网络失败状态定义清晰；
- 低余额和异常下降不会重复轰炸；
- API Key 不出现在日志和命令行历史中；
- 配置损坏可恢复；
- 窗口移出屏幕后可恢复；
- 托盘可以重新显示和退出应用；
- 退出时网络请求可取消；
- 发布后的目标机器可以正常启动并保存配置；
- .NET 目标版本与长期支持计划一致。

## 10. 简短结论

原方案可以继续作为基础，但不建议直接照原文编码。至少先修复 JSON 映射、XAML 示例、响应校验、告警状态机、多币种策略、配置原子写入和网络取消这几项。完成这些修订后，方案才适合交给 Claude 继续实现。
