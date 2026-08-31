
# V2.3.1 Final Migration Audit

最终 PASS 不能由 Task Manager 自我宣布。

必须由：

```text
Automated Contract Tests
+
QA Evidence
+
Governance Guard
```

共同产生。

## Evidence Identity

- [ ] Task Manager direct `write-evidence` → DENY
- [ ] forged `producer_role=QA` → DENY
- [ ] wrong Dispatch producer → DENY
- [ ] invalid repo/worktree/commit → DENY / INVALID
- [ ] legitimate registered producer → PASS
- [ ] concurrent append 不损坏 JSONL
- [ ] duplicate evidence_id → DENY

## State Writer

- [ ] Task Manager direct state-writer → DENY
- [ ] forged Transition Receipt → DENY
- [ ] wrong scope/stage/commit → DENY
- [ ] expired → DENY
- [ ] replay → DENY
- [ ] valid one-shot Receipt → PASS once

## Human Gate

- [ ] create-human-gate 受控入口
- [ ] record-user-approval Trusted Origin
- [ ] resolve 只接受合法 Receipt
- [ ] self-approve → DENY
- [ ] forged origin → DENY
- [ ] wrong scope → DENY
- [ ] expired / replay → DENY
- [ ] legitimate approval → PASS

## Launcher Freshness

- [ ] VERIFIED 绑定 cli_version
- [ ] launcher_version
- [ ] permission_profile_version
- [ ] contract_test_version
- [ ] verified_at
- [ ] current version mismatch → STALE/UNVERIFIED
- [ ] STALE 不得生产 Dispatch
- [ ] fresh VERIFIED Launcher → PASS

## Machine Source of Truth

- [ ] Model YAML authoritative
- [ ] Launch YAML authoritative
- [ ] Runtime Health JSON authoritative
- [ ] Governance State YAML authoritative
- [ ] Compatibility YAML authoritative
- [ ] Evidence JSONL authoritative
- [ ] MD 仅 View

## Go Scheme B

- [ ] non-Task-Manager Go default DENY
- [ ] Free rate limit 不自动触发 Go
- [ ] Agent 自判“Go 更好”不触发 Go
- [ ] Trusted User Approval Receipt one-shot override
- [ ] specific task_id
- [ ] specific dispatch_id
- [ ] wrong task/dispatch → DENY
- [ ] used/expired → DENY
- [ ] 使用后自动恢复 DENY
- [ ] Task Manager 不得 forge/replay

## Task Manager Isolation

- [ ] 业务源码 write → DENY
- [ ] shell bypass → DENY
- [ ] raw Codex/OpenCode → DENY
- [ ] raw terminal/worker-start → DENY
- [ ] Builder Subagent → DENY
- [ ] Model/Launch Registry write → DENY
- [ ] Runtime Health write → DENY
- [ ] governance-state write → DENY

## Positive Contracts

- [ ] User Feedback Intake → PASS
- [ ] Dispatch Gateway → PASS
- [ ] Run/Task/Dispatch identity → PASS
- [ ] stale Worker guard → PASS
- [ ] Evidence Writer → PASS
- [ ] State Guard → PASS
- [ ] Human Gate chain → PASS
- [ ] Go Scheme B valid one-shot override → PASS

## External Maintenance

- [ ] neat-freak 不是 Runtime Maintenance Owner
- [ ] 定期轻量检查
- [ ] 检测变化后同轮专项 Audit
- [ ] 不假设未经验证实时本地事件
- [ ] External Maintenance 不使用 Go
- [ ] compatibility 不自动改 Production Model Mapping
- [ ] Notification Policy

全部最终 Contract PASS 后：

```text
MIGRATION_V2_3_1_FINAL = PASS
```

然后：

> Governance Design Freeze → 真实项目试运行。


# Self-loop / Continuation Audit

## Continuation Guard

- [ ] P0 incomplete + no blocker + Task Manager asks stop → DENY
- [ ] P0 incomplete + Human Gate → STOP ALLOWED
- [ ] Task complete + Evidence PASS → STOP ALLOWED
- [ ] P0 incomplete + next_action missing → CONTINUATION_VIOLATION

## Learning Capture / Router

- [ ] User correction → Learning Event created
- [ ] Rule violation → Learning Event created
- [ ] repeated correction >= 2 → Reusable Candidate
- [ ] prompt-only failure repeated >= 3 → Guard / Contract Test escalation
- [ ] Runtime / Model event → External Maintenance route

## Promotion

- [ ] low-risk validated reusable improvement → Current Project + Template
- [ ] Promotion Receipt generated
- [ ] one target failure → PARTIAL
- [ ] high-impact auto Promote → DENY / Human Gate

## Template Fingerprint

- [ ] TEMPLATE_VERSION.yaml exists
- [ ] TEMPLATE_MANIFEST.json exists
- [ ] PROJECT_GOVERNANCE_ORIGIN template exists
- [ ] successful Promote bumps template version
- [ ] manifest fingerprint refreshes

## Learning Gate

- [ ] unprocessed reusable candidate → CLOSED DENY
- [ ] Promotion PARTIAL → CLOSED DENY
- [ ] Skill Candidate has explicit lifecycle status
- [ ] all reusable items reconciled → LEARNING_GATE PASS

最终真实 Migration Audit 仍必须在项目环境执行；本模板包内的 Learning Loop Unit Contract Tests 不能替代 ORCA Runtime Negative Canary。
