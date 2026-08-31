
# HANDOFF｜Task Manager Writable Resume Capsule

用于暂停 / 恢复，避免全仓重读。

## Resume Capsule

- Captured at:
- Stage ID:
- Governance State View:
- Repo / Worktree:
- Current Commit:
- Remaining Stage P0:
- Current Goal:
- Current Task:
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
- Current Blocking:
- Next Single Action:
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
