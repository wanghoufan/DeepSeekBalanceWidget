
# Quality Gates

## Stage P0 与 Defect P0

Stage P0 = 用户 / 产品必须交付。

Defect P0 至少：

- 阻断 Stage P0；
- 数据损坏；
- 严重安全；
- 核心路径无法完成；
- Release 级致命回归。

## P0 Priority Guard

Stage P0 未完成时：

- P2 / P3；
- Future；
- 非必要重构；
- UI 微调；
- 文档美化；

默认进入 Backlog，不抢主线。

## Fix Budget

| 级别 | 最大有效 Fix → Verify |
|---|---:|
| Defect P0 | 3 |
| P1 | 2 |
| P2 | 0–1 |
| P3/Future | 0 |

每个有效 Attempt 记录：

```text
Attempt ID
Task ID
Dispatch ID
Root Cause Hypothesis
Model / Backend
Approach
Files Changed
Verification
Failure Reason
Difference From Previous Attempt
```

高不确定性 P0 允许 Diversity Investigation，Senior Expert 必须独立重建问题模型。

## Risk-driven QA

```text
L1 Automated Regression
L2 Engineering QA
L3 Human-like Interaction QA
L4 Product Review
L5 Visual Acceptance
```

不是每次机械全跑。

## QA Acceptance

Evidence-based：

- 当前 Commit Automated Evidence；
- Engineering QA；
- P0 Fix Verify；
- Human-like QA（若触发）；
- 未覆盖；
- Stage P0。

## Evidence

Task Manager 只读。

Producer Identity 必须 trusted runtime 反查。

## State

Acceptance / Closed 必须走 State Guard / Writer。

## Action Risk

即使只是 P2，如果操作本身是不可逆生产 Migration，也必须 Human Gate。
