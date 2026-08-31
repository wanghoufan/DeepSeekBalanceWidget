
# HANDOFF｜Task Manager Writable Resume Capsule

用于暂停 / 恢复，避免全仓重读。

## Resume Capsule

- Captured at: 2026-08-30
- Stage ID: STAGE-GOVERNANCE-MIGRATION
- Governance State View: DRAFT
- Repo / Worktree:
- Current Commit:
- Remaining Stage P0: P0-GOV-MIGRATION-2；真实 Mac Runtime 审计
- Current Goal: 验证迁移后的治理契约与原 .NET 工程可运行性
- Current Task: 迁移后验证
- Current Dispatch:
- Active Worker:
- Worker Mode:
- Persistent Session ID（如有）:
- Worker Health:
- Last trusted Evidence IDs:
- Open Human Gates:
- Open Learning Event IDs:
- Promotion Queue pending IDs:
- Promotion PARTIAL IDs:
- Current Blocking: WindowsDesktop SDK 目标缺失；Runtime 与 State Guard 仍为未验证/Fail-Closed
- Next Single Action: 在 Windows/完整 WindowsDesktop SDK 环境运行 `dotnet build DeepSeekBalanceWidget.sln` 与 `dotnet test DeepSeekBalanceWidget.sln`
- Continuation stop_reason:
- Continuation blocking_evidence:

## Relevant Files Only

只列真正需要恢复的文件，不做全仓库目录转储。

## Resume Rule

下次优先读：

1. HANDOFF；
2. governance-state.yaml；
3. runtime-health.json；
4. TASK_PRIORITY；
5. 最近 Evidence；
6. 未处理 Learning / Promotion 状态；
7. Git status/diff；
8. Relevant Files。

只有出现冲突才扩大读取范围。
