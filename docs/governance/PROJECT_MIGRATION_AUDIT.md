# 项目治理模板迁移审计

## 迁移目标

将现有 DeepSeek Balance Widget 纳入 `01_治理模板/AI-Governance-Template` 的 ORCA V2.3.1 / Delivery V1.10 治理结构，同时保持现有 .NET 解决方案、Windows WPF、macOS Avalonia、共享 `Models/Services`、发布脚本和业务文档可运行。

## 已执行

- 保留业务目录与入口：`src/`、`tests/DeepSeekBalanceWidget.Tests/`、`DeepSeekBalanceWidget.sln`、`scripts/publish*`、`scripts/install-macos.sh`。
- 新增模板要求的 `docs/` 治理层、`scripts/governance/`、`scripts/runtime/macos/`、`scripts/workers/`、`tests/governance/`、`skills/`。
- 建立 `PROJECT_GOVERNANCE_ORIGIN.yaml` 与 `PROJECT_GOVERNANCE_APPLIED.yaml`，绑定模板版本、Manifest 哈希和 Fingerprint。
- 保留模板的 Fail-Closed 语义；未把任何治理脚本声明为已通过本机生产验证。
- 未复制模板维护者机器的运行健康凭据；本项目的 Mac Runtime 状态为 `IMPLEMENTED_UNVERIFIED`。
- 将项目入口、目录边界和治理约束补充到根目录 `AGENTS.md` 与 `README.md`。

## 结构结论

业务工程不做源码移动。现有 `.sln` 与项目引用仍保持原路径，因此不会因治理迁移破坏构建入口。治理文件与业务文件分层，治理测试通过独立的 `tests/governance/` 入口自动发现。

## 当前证据

- 模板 Manifest：版本 `2.3.1-template.25`，Fingerprint `e2211ea631aa8751030ccfd38c70f4642a664c5bf19afde7a4ae4b1602500169`。
- 本项目 Origin/Applied 已绑定同一版本与 Manifest 哈希。
- `stale-template-guard` 通过；`learning-gate` 通过；治理契约测试 `22/22` 通过。
- 项目 XML/解决方案引用检查与现有 shell 发布脚本语法检查通过。
- .NET SDK `8.0.130` 已恢复并持久化可用；macOS Avalonia 项目单独构建成功（0 warning / 0 error）。
- 整体解决方案构建失败：当前 macOS SDK 缺少 `Microsoft.NET.Sdk.WindowsDesktop.targets`；测试项目引用 Windows WPF 项目，因此现有单元测试不能在本机完成。
- 没有 Git 元数据可用于绑定当前 commit/worktree；本次迁移不创建提交、不推送。

## 未通过或未覆盖的项目

1. `state-guard`、`state-writer`、Evidence Writer、Human Gate、Dispatch/Worker 仍是模板 Fail-Closed 接口，不能作为本机完整自治治理生产链路。
2. `MAC_RUNTIME_V1` 尚未针对本项目执行真实 `runtime-health-check`、Negative Canary、Positive Contract、Migration Audit；不能声称 ORCA/OpenCode/Codex 已在本项目完成验证。
3. Windows/WSL Runtime Adapter 按模板策略延后到 Mac VERIFIED，不能把 Windows WPF 能构建等同于治理 Runtime 已验证。
4. `docs/progress/governance-state.yaml` 保持 `DRAFT`，因为没有合法的 Trusted State Transition Receipt，不伪造 `ACTIVE` 或 `ACCEPTED`。
5. 业务 UI、权限、钥匙串/DPAPI、托盘/菜单栏、发布包和真实 API 登录链路不由本次结构迁移覆盖。
6. 本项目没有 Git 元数据，无法生成当前 commit/worktree 的可信绑定；本次未创建提交或推送。
7. Windows WPF 与现有测试需要 Windows 或具备完整 WindowsDesktop SDK 的构建环境；macOS 只能确认 Avalonia 目标的构建结果。

## 最终结论

**结构上已完成迁移，业务项目仍按原入口运行；但不能宣称已经完全按照新治理模板“可生产运转”。**

可运行部分：治理目录、角色/工作流文档、Fail-Closed 脚本接口、项目指纹状态和治理契约测试入口已具备。

不可直接承诺部分：真实 ORCA Runtime、受信 Dispatch、状态转换、Human Gate、Evidence 闭环以及 Mac/Windows 发布级验证仍需在本项目环境中单独完成。项目当前应视为“治理结构已接入、运行治理未验收”。
