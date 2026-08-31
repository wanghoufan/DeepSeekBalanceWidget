# Closeout Gates｜含 Project Close Learning Gate

## Stage / Project 收尾

```text
P0 Done
↓
Required Evidence
↓
READY_FOR_ACCEPTANCE
↓
QA Acceptance
↓
ACCEPTED
↓
Engineering Closeout
↓
Learning Reconciliation
↓
Project Close Learning Gate
↓
CLOSED
```

## Learning Reconciliation

结束时不是第一次总结，而是最终对账：

- 所有 Learning Events 已分类；
- Project-only 已处理；
- Reusable Candidate 已 promoted / rejected / deferred with reason；
- Low-risk validated improvements 已同步 Current Project + Template；
- High-impact Candidate 有明确处置；
- Skill Candidate 有明确 lifecycle status；
- Promotion Log 无 `PARTIAL`；
- TEMPLATE_VERSION / TEMPLATE_MANIFEST 已同步。

只要：

```text
LEARNING_GATE != PASS
```

State Guard：

```text
ACCEPTED → CLOSED
= DENY
```

## P2 / P3

非阻断 P2 / P3 可以进入 Backlog，不应无限拖延 P0 / Closeout。


## Learning Loop Final Freeze Addendum

- Continuation Guard 只信 Machine State，不信 Task Manager 自报 COMPLETE。
- recurrence_count 只由 LEARNING_EVENTS 历史机器计算。
- Promotion actor 从 trusted Dispatch identity 反查。
- Promotion 必须 Trusted Validation Receipt。
- Production Skill 必须进入 Dispatch activation path。
- 新项目必须 bootstrap-project + Template Fingerprint。
- Close Learning Gate 必须 Promotion Hash Reconciliation。
- Learning Loop Design Freeze 后不再增加新理论 Agent / Plane。
