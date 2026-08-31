# Handoff - 2026-08-31 15:40

## 当前状态
- Stage: Widget Mac 端 UI 改造
- 构建: 0 Error(s), 0 Warning(s)
- App: `/Applications/DeepSeekBalanceWidget.app`
- Git: 未提交（大量改动待提交）

## 已完成功能（16项）

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
16. ✅ macOS 监测项独立开关（DS/GPT/OC/WB）

### 基础设施
- ✅ resolve-model 脚本（模型路由解析）
- ✅ MODEL_ROUTING_REGISTRY.yaml 已更新（8角色模型分工）

## 未完成/需改进

### P0（必须修）
1. ❌ DS 块间距偏大：`MiniDeepSeekBlock` Border Width=180 应移除，让内容自适应
2. ❌ 按钮"关闭"被截断：所有按钮 Width 需增加到 56-60px，MiniButtonBar Width 增加到 230

### P1（后续补）
3. ❌ macOS OpenRouter 卡片未添加到 SettingsWindow

## 关键代码位置

| 文件 | 说明 |
|------|------|
| `src/DeepSeekBalanceWidget.Mac/MainWindow.axaml` | 胶囊布局（DS/GPT/OC/按钮） |
| `src/DeepSeekBalanceWidget.Mac/MainWindow.axaml.cs` | 业务逻辑（置顶/贴边/刷新/主题） |
| `src/DeepSeekBalanceWidget.Mac/SettingsWindow.axaml` | 设置页（4标签） |
| `src/DeepSeekBalanceWidget.Mac/Services/MacAlarmSound.cs` | 11种声音合成 |
| `src/DeepSeekBalanceWidget.Mac/ToastWindow.axaml` | 警报弹窗 |
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
