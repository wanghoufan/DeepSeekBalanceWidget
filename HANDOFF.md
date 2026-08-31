# Handoff - 2026-08-31 22:20

> ⚠️ **仓库边界**：本仓库（`wanghoufan/DeepSeekBalanceWidget-Mac`）**只维护 macOS 端**。Windows（WPF）端已拆分为独立仓库。下文出现的 Windows / WPF 字样均为历史语境或构建环境限制说明，不代表本仓库目标平台。

## 当前状态
- Stage: Widget Mac 端 OpenRouter 真实接入（已完成，待发布）
- 构建: 0 Error(s), 0 Warning(s)（`dotnet build Mac.csproj --no-restore`），4 Warning(s)（完整 restore 含既有 Avalonia 警告）
- App: `/Applications/DeepSeekBalanceWidget.app`（待重新打包）
- Git: 待提交并推送（OpenRouter 真实接入 5 文件）
- 进程: 待部署前 pkill -9 清理

## 已完成功能（17项）

### 核心功能
1. ✅ DeepSeek 周末非高峰规则（PeakRange.WeekdaysOnly，2026-08-23 官方政策）
2. ✅ 警报声音扩展至 11 种（Soft/Standard/Urgent/Beep/Ascending/Descending/Chime/Bell/DingDong/Rapid/SlowPulse）
3. ✅ GPT/OpenCode 预警分离（独立配置 5h/周/月额度档位）

### UI 改造
4. ✅ 浅色/暗黑模式 + 跟随系统（ThemeService + Light.xaml + Dark.xaml）
5. ✅ 胶囊置顶面板（Topmost + MiniMode + 数据刷新）
6. ✅ 设置页标签式导航（监测项/预警/界面/通用）
7. ✅ macOS 报警功能（MacAlarmSound + ToastWindow + MacToastService）
8. ✅ 进度条 Track+Fill 模式（不溢出 Border）
9. ✅ 进度条三色区间（<20%红, ≤70%橙, else绿）
10. ✅ 置顶功能（NSWindowSetLevel P/Invoke）
11. ✅ 单实例检测（防止多开）
12. ✅ Dock 图标点击还原窗口

### 设置页
13. ✅ 清除 Key 按钮移至输入旁 + 红色样式 + 二次确认
14. ✅ 设置页 Apply 按钮（应用但不关闭）
15. ✅ 高峰时段移至 DS 余额旁
16. ✅ macOS 监测项独立开关（DS/GPT/OC/WB/OR）

### 基础设施
- ✅ resolve-model 脚本（模型路由解析）
- ✅ MODEL_ROUTING_REGISTRY.yaml 已更新（8角色模型分工）

### OpenRouter 真实接入（2026-08-31）
17. ✅ OpenRouter 额度监测（`GET https://openrouter.ai/api/v1/credits`，需 Management Key）：`OpenRouterUsageProvider` 真实网络实现（Bearer 401/403/非2xx/网络异常/JsonException 分支，Timeout 15s，HttpClient 注入/Dispose 隔离），`OpenRouterUsageParser` 解析 `data.total_credits/total_usage`（number/string 兼容，负值 clamp），`OpenRouterUsageFormatter` 格式化；展开卡片 `OpenRouterPanel`（Row 4）与胶囊 `MiniOpenRouterBlock`（Column 3，24,34,52,34 单行：OR/剩余%/ $剩余/34px 三色进度条），`MainWindow` 新增 `_openRouterTimer/_openRouterProvider/_menuBarOpenRouterText` 及 `RefreshOpenRouterAsync/ApplyOpenRouterUsage/ClearOpenRouterRows` 全生命周期（OnOpened/Refresh/Settings/Visibility/OnClosing/MenuBar），`AppConfig.AgentOrder` 新增 `openrouter` 并 Normalize 补齐，`SettingsWindow` 卡片文案更新为“需 Management Key”。QA 19/19 harness PASS（空 key/401/403/429/网络/JSON/超时/Dispose/Order），Product PASS，`dotnet build` 0 Error。

## 未完成/需改进

### P1
- 无（OpenRouter 已从预留升级为真实接入）

### 后续可选
- WorkBuddy 实际额度接入（仍为占位）
- 打 `v*` tag 发布（需同步 `Mac.csproj` `<Version>` 与 `Info.plist` `CFBundleShortVersionString/CFBundleVersion` 及 README 下载表）

## 下次恢复开发提示词

```
恢复 DeepSeek Balance Widget 开发。

读取 HANDOFF.md 了解当前状态和遗留问题。

当前已完成 OpenRouter 真实接入（QA + Product PASS，待 git 推送与 .app 重新打包）。

下一步可选：
1. pkill -9 清理旧进程 → ./scripts/publish-macos.sh arm64 → 安装到 /Applications 并验证
2. 或择机打 v* tag 升版本发布（同步 Mac csproj / Info.plist / README 版本号）
3. 或补齐 WorkBuddy 实际额度接入

模型分工（已写入 MODEL_ROUTING_REGISTRY.yaml）：
- 初级 Builder: codex/gpt-5.6-luna (medium)
- 高级 Builder: codex/gpt-5.6-sol (medium)
- QA: codex/gpt-5.6-luna (max)
- Product Reviewer: opencode/muse-spark-1.2-contributor-free

注意事项：
- 不要关闭用户自己创建的终端标签
- 每次部署前 pkill -9 彻底清理旧进程
- 简单 UI 修复不要走完整流程，一个 Builder 精确改一次
- 持久问题用常驻 TUI，不要反复派一次性临时工
- Terra 模型 API 不可用，暂用 Luna
```

## 关键代码位置

| 文件 | 说明 |
|------|------|
| `src/DeepSeekBalanceWidget.Mac/MainWindow.axaml` | 胶囊布局（DS/GPT/OC/OR/按钮）Row 8 + Mini 5 列 |
| `src/DeepSeekBalanceWidget.Mac/MainWindow.axaml.cs` | 业务逻辑（置顶/贴边/刷新/主题 + OpenRouter 完整链路） |
| `src/DeepSeekBalanceWidget.Mac/SettingsWindow.axaml` | 设置页（4标签，OpenRouter 卡片 Row 4） |
| `src/DeepSeekBalanceWidget.Mac/Services/MacAlarmSound.cs` | 11种声音合成 |
| `src/DeepSeekBalanceWidget.Mac/ToastWindow.axaml` | 警报弹窗 |
| `src/DeepSeekBalanceWidget/Services/OpenRouterUsageProvider.cs` | OpenRouter 真实 credits 请求（Bearer + 401/403/超时） |
| `src/DeepSeekBalanceWidget/Services/OpenRouterUsageParser.cs` | 解析 `data.total_credits/total_usage` |
| `src/DeepSeekBalanceWidget/Services/OpenRouterUsageFormatter.cs` | 格式化剩余/百分比 |
| `src/DeepSeekBalanceWidget/Models/OpenRouterUsageSnapshot.cs` | 剩余% 与剩余金额计算 |
| `src/DeepSeekBalanceWidget/Models/AppConfig.cs` | AgentOrder 含 openrouter + Normalize |
| `src/DeepSeekBalanceWidget/Themes/Light.xaml` | 浅色主题资源 |
| `src/DeepSeekBalanceWidget/Themes/Dark.xaml` | 暗色主题资源 |
| `src/DeepSeekBalanceWidget/Services/ThemeService.cs` | 主题切换 |
| `docs/model/MODEL_ROUTING_REGISTRY.yaml` | 模型分工（权威） |
| `scripts/runtime/macos/resolve-model` | 模型路由解析脚本 |

## 模型分工（已写入治理文件）

| 角色 | 模型 | effort |
|------|------|--------|
| Task Manager | opencode-go/mimo-v2.5 | — |
| 初级 Builder | codex/gpt-5.6-luna | medium |
| 高级 Builder | codex/gpt-5.6-sol | medium |
| Code Reviewer | codex/gpt-5.6-luna | medium |
| QA | codex/gpt-5.6-luna | max |
| Product Reviewer | opencode/muse-spark-1.2-contributor-free | — |
| neat-freak | opencode/nemotron-3-ultra-free | — |

## 工作流教训（必须遵守）

1. **简单 UI 修复**：不要走完整 Builder→Reviewer→QA→Product 流程，一个 Builder 精确改一次就够了
2. **持久问题**：用常驻 TUI Worker（保持终端开着），不要反复派一次性临时工
3. **不要关闭用户终端**：只关我派工产生的终端，绝对不动用户自己的
4. **部署前彻底 kill**：pkill -9 所有旧进程，确认 0 残留后再启动新版本
5. **Terra 不可用**：codex/gpt-5.6-terra API 受限，暂用 Luna

## 已知限制
- macOS AlarmSound.cs 未链接到 Mac 项目（预存问题）
- Windows WPF solution 需 WindowsDesktop SDK，macOS 无法验证
- 9 处 StaticResource 残留（Style 定义中，不影响运行时切换）
