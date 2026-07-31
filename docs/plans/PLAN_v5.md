# DeepSeek 余额悬浮小工具 —— UI 增强方案 v5（待 Codex 审查）

- 日期：2026-07-31
- 状态：计划稿，尚未执行任何代码修改
- 审查对象：`src/DeepSeekBalanceWidget/`（v4 已建成并验证，build 0 错误、18 测试通过）
- 审查目的：5 项改动是否合理、是否有遗漏边界
- 说明：用户对部分确认项未逐条回复，本方案采用"推荐默认值"并在第 8 节标注，请 Codex 审查时确认。

## 1. 需求概述

用户新增/修改的 UI 需求：

1. **标题栏四个按钮**：缩小 / 关闭 / 置顶 / 收进任务栏（当前只有 1 个"缩小"按钮）。
2. **任务栏实时状态**：收起后窗口从屏幕消失，程序常驻**系统托盘**（任务栏右下角通知区域），tooltip 实时显示余额、变动、高峰状态；**图标随高峰/非高峰变色**（高峰红、非高峰绿）。
3. **高峰时段提醒**：按官方定义显示"高峰期/非高峰期"，红色/绿色。
4. **自由定位 + 默认角落选择**：胶囊不再被强制贴回右下角（修复 bug），支持自由拖动、记忆位置，可选默认角落（右下/左下/记忆）。
5. **任务栏面板显示**：托盘图标 + tooltip 作为"放进任务栏"的实现载体。

## 2. 官方高峰时段（已核实）

来源：<https://api-docs.deepseek.com/zh-cn/quick_start/pricing>

> "高峰时段定义：北京时间每日 **9:00～12:00** 和 **14:00～18:00**"
> 高峰期价格为平时 2 倍；"具体时段以官方通知为准"、"该策略即将采用"。

结论：
- 默认区间 `[9,12)` 和 `[14,18)`，**必须可配置**（官方明确"以通知为准"）。
- 基于本地时间判断（用户在中国，本地=北京）；是否强制北京时间见第 8 节待确认项。
- 官方标注"即将采用"，显示为参考性（无成本影响）。

## 3. 现状分析

| 位置 | 现状 | 问题 |
|---|---|---|
| `MainWindow.xaml` | 标题栏仅 1 个"缩小"按钮；有 `Card`/`MiniCard` 两态 | 缺关闭/置顶/收进任务栏 |
| `MainWindow.xaml.cs` | `ApplyMiniMode` L103 `if (IsLoaded) ResetPosition();` | **bug：每次收起/展开胶囊强制贴回右下角**，导致"只能悬浮右下角" |
| `MainWindow.xaml.cs` | `ResetPosition` 固定贴右下角 | 无左下角/记忆选项 |
| `MainWindow.xaml.cs` | `OnClosing`=隐藏到托盘 | 行为正确，保留 |
| `TrayIconService.cs` | 托盘 tooltip=余额+变动；菜单有余额行 | **图标固定 SystemIcons.Application 不变色**；tooltip 缺高峰 |
| `AppConfig.cs` | 有 `UseMiniMode` | 无高峰、无默认角落配置 |

## 4. 修改计划

### 4.1 标题栏四个按钮（MainWindow.xaml / .cs）

| 按钮 | 行为 |
|---|---|
| 缩小 `–` | 完整卡片 ⇄ 迷你胶囊（保留现有 `ApplyMiniMode`） |
| 关闭 `×` | `Hide()` 隐藏到托盘，**不退出**；退出走托盘/右键菜单 |
| 置顶 `📌` | 切换 `Topmost`，按钮高亮表示当前置顶，与设置 `IsAlwaysOnTop` 同步 |
| 收进任务栏 `⇟` | `Hide()` 主窗口，常驻托盘；点托盘图标恢复 |

要点：四按钮 18×18 透明无边框；`Window_MouseLeftButtonDown` 已用 `ButtonBase` 过滤，不触发拖动。

### 4.2 修复定位 bug + 默认角落（MainWindow.xaml.cs / AppConfig.cs）

- **删除** `ApplyMiniMode` 中 `if (IsLoaded) ResetPosition();` → 收起/展开保持当前位置。
- 自由拖动本就支持（`DragMove` + 防抖保存），修 bug 后"拖到哪停在哪"自然成立。
- 新增 `DefaultCorner` 配置：`Remember`（默认，记忆上次）/ `BottomRight` / `BottomLeft`。
- `ResetPosition()` 支持角落参数：
  - 右下：`wa.Right - Width - 20; wa.Bottom - Height - 20`
  - 左下：`wa.Left + 20; wa.Bottom - Height - 20`
  - 记忆：读 `WindowLeft/WindowTop`
- 设置窗口加"默认位置"下拉框。

### 4.3 任务栏面板显示（TrayIconService 增强）

- **tooltip 动态刷新**：`余额 ¥110.00  变动 -2.50  ● 非高峰`（63 字符内精简）。
- **图标随高峰变色**：高峰红 / 非高峰绿（运行时用 `System.Drawing` 生成 16×16 图标，避免外部 .ico）。
- **右键菜单**：首行只读显示 `余额 / 变动 / 高峰状态`；其余保持。
- 主窗口 `ShowInTaskbar=False` 不变，恢复显示走托盘双击/菜单。

### 4.4 高峰时段提醒

- 新增纯服务 `Services/PeakHourCalculator.cs`：
  - `static bool IsPeak(DateTime localNow, IReadOnlyList<PeakHourRange> ranges)`
  - 默认区间 `[(9,12),(14,18)]`，半开区间 `[Start,End)`。
- `AppConfig` 新增：
  ```json
  "peakHourRanges": [ { "startHour": 9, "endHour": 12 }, { "startHour": 14, "endHour": 18 } ],
  "showPeakIndicator": true
  ```
- UI：卡片/胶囊状态行加高峰徽标（高峰红 `#E86656` / 非高峰绿 `#4CC94C`）；托盘 tooltip/图标同步。
- 新增 **60 秒 DispatcherTimer** 单独检查高峰状态（跨整点即时切换，不依赖余额刷新）。
- 设置窗口：两段区间配置（开始/结束小时，0–23、Start<End 校验）。
- 测试：`PeakHourCalculatorTests` 覆盖区间内/外、边界（9:00 峰 / 12:00 非峰）、非法区间。

## 5. 涉及文件

| 文件 | 改动 |
|---|---|
| `MainWindow.xaml` | 四按钮；高峰徽标；收进任务栏动作 |
| `MainWindow.xaml.cs` | 四按钮处理；修定位 bug；默认角落；高峰徽标+60s 定时器；托盘回调 |
| `Services/TrayIconService.cs` | tooltip 加高峰；图标随高峰变色；菜单余额行加高峰 |
| `Services/PeakHourCalculator.cs` | **新增**纯服务 |
| `Models/AppConfig.cs` | `PeakHourRanges`、`ShowPeakIndicator`、`DefaultCorner` |
| `SettingsWindow.xaml/.cs` | 高峰区间、默认位置配置 |
| `tests/DeepSeekBalanceWidget.Tests/PeakHourCalculatorTests.cs` | **新增** |

**不改动**：余额解析、告警状态机、网络层、配置原子写、单实例、DPAPI。

## 6. 实现步骤

1. `PeakHourCalculator` + 配置 + 测试（独立可测，先跑通）。
2. UI 高峰徽标 + 60s 定时器。
3. 四按钮（先"关闭""置顶"，再"收进任务栏"）。
4. 修定位 bug + 默认角落 + 设置项。
5. 托盘图标变色 + tooltip 加高峰 + 菜单增强。
6. 设置窗口高峰区间/默认位置配置。
7. 回归：`dotnet build` 0 错误 0 警告 + `dotnet test` 全过 + Mock `--mock-scenario sequence` 实机验证。

## 7. 验收标准

- [ ] 标题栏出现缩小/关闭/置顶/收进任务栏四按钮，行为正确
- [ ] 置顶按钮切换立即生效，与设置一致
- [ ] 关闭/收进任务栏只隐藏不退出，托盘可恢复
- [ ] 胶囊可自由拖动，收起/展开不重置位置；默认角落可设为右下/左下/记忆
- [ ] 托盘 tooltip 实时显示余额+变动+高峰状态
- [ ] 托盘图标随高峰红/非高峰绿
- [ ] 高峰默认 9-12、14-18；跨整点自动切换（60s 定时器）
- [ ] 高峰区间可在设置修改并即时生效
- [ ] build 0 错误 0 警告；test 全过（含新增高峰测试）
- [ ] Mock `--mock-scenario sequence` 下卡片/胶囊正常，托盘状态实时更新

## 8. 待确认项（推荐默认值，请 Codex/用户确认）

| # | 项 | 推荐默认 |
|---|---|---|
| 1 | "放进任务栏"载体 | 托盘图标 + tooltip（Windows 无其他标准方式） |
| 2 | "收进任务栏"与"关闭" | 两者均隐藏到托盘；如冗余可去掉"收进任务栏"，由"关闭"承担 |
| 3 | 迷你胶囊形态 | 保留为可选（默认完整卡片） |
| 4 | 默认位置 | `Remember`（记忆上次，推荐） |
| 5 | 高峰判断基准 | 本地时间（用户=北京） |
| 6 | 高峰显示位置 | 卡片/胶囊/tooltip/图标颜色 全部 |
| 7 | 进出高峰 Toast 提示 | 不弹（避免打扰；低余额/异常告警已存在） |
| 8 | `ShowInTaskbar` | 保持 `False`，恢复走托盘 |

## 9. 技术约束说明

- Windows 任务栏不允许第三方直接绘制/显示实时文字；"任务栏面板显示"的标准实现为**托盘图标 + tooltip + 图标变色**。
- 图标变色用 `System.Drawing` 运行时绘制，不引入外部 .ico 资源，避免构建依赖。
- 高峰状态用半开区间 `[Start,End)`，避免重叠歧义（12:00 不归上午段）。

## 关键文件（规划）

- `src/DeepSeekBalanceWidget/MainWindow.xaml`（四按钮 + 高峰徽标 + 停靠条）
- `src/DeepSeekBalanceWidget/MainWindow.xaml.cs`（状态机 + 修 bug + 定时器）
- `src/DeepSeekBalanceWidget/Services/TrayIconService.cs`（变色 + tooltip）
- `src/DeepSeekBalanceWidget/Services/PeakHourCalculator.cs`（新增纯服务）
- `src/DeepSeekBalanceWidget/Models/AppConfig.cs`（新配置项）
