# Scripts｜Fail-Closed Interface Skeletons

这些脚本文件是 **V2.3.1 接口模板**，不是已经通过你本机 Contract Test 的生产实现。

默认：

> **Fail Closed。**

只有在真实电脑上完成：

- 当前 ORCA / OpenCode / Codex 版本确认；
- 当前 permission schema；
- 本地适配；
- Negative Canary；
- Contract Test；
- Launcher Freshness；

之后，External Maintenance / 实现者才能替换模板逻辑，并在 `WORKER_LAUNCH_REGISTRY.yaml` 标记相应版本为 `VERIFIED`。

严禁因为“脚本文件已经存在”就认为系统已经安全落地。


## Learning Loop Final Interfaces

```text
scripts/governance/
├── continuation-guard
├── write-learning-event
├── learning-router
├── write-validation-receipt
├── promotion-gateway
├── template-sync
├── learning-gate
├── bootstrap-project
└── stale-template-guard

scripts/workers/
├── dispatch-worker
└── resolve-skills
```

Production ORCA adapters remain Fail Closed until real-environment Contract Tests.

## V1.9 Final Self-Learning Interfaces

```text
resolve-learning-pattern
write-learning-event
promotion-queue-writer
request-promotion
register-skill
record-skill-usage
record-skill-effectiveness
validate-user-approval-receipt
bootstrap-project
stale-template-guard
learning-gate
```

真实 `dispatch-worker` 仍保持 Fail Closed Stub；下一阶段必须在本机 ORCA Adapter 中接通：

```text
resolve-skills
→ skill content injection
→ trusted loaded_skill_ids
→ usage/effectiveness
```
