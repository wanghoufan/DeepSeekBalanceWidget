# Changelog

本项目遵循语义化版本号。

## 0.3.0 — 2026-08-15

### 功能

- 新增 macOS 双平台支持：基于 Avalonia 的原生 `.app` 菜单栏应用，支持 Apple Silicon（M 系列）与 Intel Mac
- macOS 版菜单栏实时显示 DeepSeek 余额、ChatGPT Plus 用量百分比和北京时间峰值时段
- macOS 版 API Key 保存到登录钥匙串，支持登录时自动启动
- 新增 macOS 一键发布与安装脚本（`scripts/publish-macos.sh`、`scripts/install-macos.sh`）
- 新增本机 Codex 用量消耗速率追踪（`CodexConsumptionRateTracker`）
- GitHub Releases 同时发布 Windows 与 macOS（arm64/x64）安装包

### 工程化

- CI 增加 macOS 构建验证；Release 工作流扩展为双平台矩阵构建

## 0.2.0 — 2026-08-06

### 功能

- 恢复 ChatGPT Plus 额度监测，每分钟显示用量窗口剩余百分比和重置时间
- 设置中提供额度监测开关、字号和字重选项
- 新增贴边自动隐藏，可从桌面四边悬停唤回
- 清理旧运行产物，统一新版发布入口

## 0.1.0 — 2026-07-31

首个可用版本。

### 功能

- DeepSeek API CNY/USD 余额查询与严格响应解析
- 总余额、充值余额、有效赠送余额和余额变化显示
- 低余额及异常下降告警
- 完整卡片和迷你胶囊模式
- 迷你模式拖动保持迷你状态，双击展开
- 系统托盘、置顶、隐藏、退出和开机自启
- 北京时间峰值时段参考
- 多显示器边界恢复和窗口位置记忆
- DPAPI CurrentUser 加密保存 API Key
- Mock 场景及 31 项自动化测试

### 工程化

- 标准化 `src`、`tests`、`docs`、`artifacts` 和 `scripts` 目录
- 一键启动和单文件自包含发布脚本
- GitHub Actions 构建、测试和标签发布
