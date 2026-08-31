# DeepSeek Balance Widget｜治理迁移计划

## 目标

在不移动会破坏运行的业务文件前提下，将项目接入 `01_治理模板/AI-Governance-Template` 的目录、角色、证据、状态、Runtime 与 Learning 结构。

## 范围

- 纳入模板治理文档、Fail-Closed 脚本、Runtime 适配器与治理契约测试。
- 建立项目 Governance Origin / Applied 指纹。
- 保持现有 .NET 业务目录、解决方案、发布脚本和测试入口不变。
- 记录真实 Runtime 尚未验证的风险边界。

## 不在范围

- 不重构 WPF/Avalonia 业务代码。
- 不更改 DeepSeek、ChatGPT、OpenCode 的实际额度逻辑。
- 不生成发布包、不修改 API Key、钥匙串、DPAPI 或用户配置。
- 不创建 Git 提交或推送。

## 验收

- 治理目录按模板存在，业务入口路径不变。
- Origin/Applied 指纹一致且 stale guard 可检查。
- 治理契约测试与 .NET 构建/测试分别给出真实结果。
- 未验证能力和风险点被记录，不以静态文件存在冒充真实 Runtime 通过。
