
# Scheduling Notes｜Task Manager Writable

这是 Task Manager 可写的非权威调度记录。

> ⚠️ **仓库边界（2026-08-31 起）**：本仓库只维护 macOS 端（`DeepSeekBalanceWidget-Mac`）。Windows（WPF）端已拆分为独立仓库，文中 Windows build / WPF 相关条目是拆分前的历史调度记录，不再作为本仓库待办。

## Session

- Date: 2026-08-30
- Stage: STAGE-GOVERNANCE-MIGRATION
- Current P0: P0-GOV-MIGRATION-1 / P0-GOV-MIGRATION-2
- Current Task: 按治理模板迁移现有桌面额度工具
- Current Dispatch: N/A (Task Manager only)
- Worker: N/A
- Worker Mode: N/A
- Current Commit: N/A (no git repo)
- Last Evidence IDs: EVD-20260830-001 through EVD-20260830-010
- Open Human Gates: None
- Next Action: 1) 修复 positive-contract dispatch 失败（需 Dispatch Gateway 身份适配）；2) 在 Windows 环境获取 WPF build/test 证据

## Notes

治理契约测试 27/27 通过（升级自 22/22）；stale-template-guard PASS；learning-gate PASS；macOS Avalonia build PASS；runtime-health-check PASS；all-active-governance-tests 22/22 PASS；negative-canary PASS；positive-contract FAIL（dispatch gateway 身份问题）；full-cross-project-loop PASS。Windows WPF solution 受 WindowsDesktop SDK 平台限制无法在 macOS 构建/测试。


## Continuation

- current_goal: P0-GOV-MIGRATION-2 — 完成业务构建/测试证据与 Mac Runtime 审计
- current_p0_status: INCOMPLETE — positive-contract dispatch 失败 + Windows build 阻塞
- next_action: 调研 positive-contract dispatch gateway 身份问题，确定是否为 Task Manager 角色限制或配置问题
- stop_requested: false
- stop_reason: N/A (continuing)
- blocking_evidence: EVD-20260830-008 (positive-contract FAIL), EVD-20260830-010 (Windows build BLOCKED)

## Learning

- New Learning Events: governance-contract-test upgraded from 22/22 to 27/27; negative-canary result was flaky in standalone vs migration-audit context
- Reusable Candidates: N/A
- Pending Promotions: N/A
- PARTIAL Promotions: N/A
