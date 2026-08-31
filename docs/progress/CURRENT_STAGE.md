
# CURRENT_STAGE｜Readable View

> ⚠️ **仓库边界（2026-08-31 起）**：本仓库只维护 macOS 端（`DeepSeekBalanceWidget-Mac`）。Windows（WPF）端已拆分为独立仓库，本仓库内 `src/DeepSeekBalanceWidget/` 只读不维护。下文中 "Windows WPF build BLOCKED" 等属于本机环境限制记录，不再是本仓库要达成的目标——按此边界解读，不要把它当作待办阻塞项。

> **本文件不是 authoritative state。**

机器权威：

```text
governance-state.yaml
```

状态变更只能：

```text
request-state-transition
→ State Guard
→ Trusted Transition Receipt
→ State Writer
```

## Stage Goal

完成现有 DeepSeek Balance Widget 到 ORCA V2.3.1 / Delivery V1.10 治理模板的结构迁移，并保持业务工程原入口可运行。

## Stage P0 Acceptance

- [x] P0-GOV-MIGRATION-1：治理结构与项目指纹已接入
- [ ] P0-GOV-MIGRATION-2：业务构建/测试与本项目 Runtime 审计完成

## In Scope

治理目录、角色文档、状态/证据/学习接口、Mac-first Runtime 适配器、治理契约测试与迁移审计。

## Out of Scope

业务功能重构、真实 API Key/用户配置、发布包制作、Windows/WSL Runtime 适配器生产验证。

## Current Status

- State View: DRAFT（无 Trusted State Transition Receipt）
- Current Commit: N/A (no git repo)
- Active Tasks: P0-GOV-MIGRATION-2 (build/test evidence + Mac runtime audit)
- Active Dispatches: None
- Blocking P0: positive-contract dispatch gateway FAIL; Windows WPF build BLOCKED

## Evidence Summary

- Automated: 治理契约测试 27/27；stale-template-guard PASS；learning-gate PASS；macOS Avalonia build PASS；runtime-health-check PASS；all-active-governance-tests 22/22；negative-canary PASS；positive-contract FAIL（dispatch gateway）；full-cross-project-loop PASS
- QA: 未执行真实桌面交互 QA
- Product: 未执行产品验收
- Visual: 未执行本轮视觉验收
- Review: 项目 XML/解决方案引用与 shell 脚本语法检查 PASS

## Human Gates

None / TBD

## Next Suggested Action

1. 调研 positive-contract dispatch gateway 身份适配问题（Task Manager 角色是否需要 Dispatch Gateway 特殊配置）
2. 在 Windows 或具备 WindowsDesktop SDK 的构建环境执行 `dotnet build DeepSeekBalanceWidget.sln` 与 `dotnet test DeepSeekBalanceWidget.sln`

## Continuation View

- current_goal: P0-GOV-MIGRATION-2 — 完成业务构建/测试证据与 Mac Runtime 审计
- current_p0_status: INCOMPLETE — positive-contract dispatch 失败 + Windows build 阻塞
- next_action: 调研 positive-contract dispatch gateway 身份问题
- stop_requested: false
- stop_reason: N/A (continuing)
- blocking_evidence: EVD-20260830-008, EVD-20260830-010

## Learning View

- Open Learning Events: governance-contract-test 27/27 (upgraded from 22); negative-canary flaky in standalone vs migration-audit
- Reusable Candidates: N/A
- Pending Promotions: N/A
- PARTIAL Promotions: N/A
- Learning Gate: PASS
