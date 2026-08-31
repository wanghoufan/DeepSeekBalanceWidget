# Workflow｜V2.3.1 自循环版

## 项目执行主链

```text
用户反馈
↓
Task Manager 记录 / 分类 / 排序
↓
Dispatch Gateway
↓
Worker
↓
worker_done
↓
Evidence Writer
↓
Evidence
↓
Task Manager 判断 next_action
↓
Continuation Contract
↓
Continuation Guard
├─ 合法 Stop → Stop / Human Gate
└─ 不合法 Stop → 必须继续 next_action
```

## 运行中的 Learning Loop

任何以下事件：

```text
用户纠正
规则违反
重复失败
人工 workaround
Prompt 重复
Token 浪费
可复用模式
```

必须：

```text
write-learning-event
↓
LEARNING_EVENTS.jsonl
↓
Learning Router
├─ PROJECT_PATCH
├─ GUARD_RULE
├─ SKILL
├─ TEMPLATE
├─ RUNTIME_INCIDENT
└─ MODEL_FACT
```

当前项目能立刻修正的先修正。

可复用低风险改进：

```text
Contract Test PASS
↓
request-promotion
↓
Approved Promotion Gateway
↓
Current Project + Governance Template
↓
Template Sync
↓
Promotion Receipt
```

高影响改进：

```text
Candidate
→ Human Gate / Formal Governance Revision
```

## Worker

Short 默认。

Persistent 仅在连续 Context 明显有价值时。

Retry 必须新 Dispatch。

## External Maintenance 边界

`RUNTIME_INCIDENT` / `MODEL_FACT` / Launcher Compatibility 交 External ChatGPT Desktop Maintenance Plane。

Project Learning Loop 不接管 ORCA / OpenCode / Codex 定时维护。


## Learning Loop Final Freeze Addendum

- Continuation Guard 只信 Machine State，不信 Task Manager 自报 COMPLETE。
- recurrence_count 只由 LEARNING_EVENTS 历史机器计算。
- Promotion actor 从 trusted Dispatch identity 反查。
- Promotion 必须 Trusted Validation Receipt。
- Production Skill 必须进入 Dispatch activation path。
- 新项目必须 bootstrap-project + Template Fingerprint。
- Close Learning Gate 必须 Promotion Hash Reconciliation。
- Learning Loop Design Freeze 后不再增加新理论 Agent / Plane。

## V1.9 Self-Learning Runtime Path

```text
Learning Event
→ Canonical Pattern Resolver
→ Machine Recurrence
→ Router
→ Trusted Validation
→ Promotion
→ Skill Auto Registration（如适用）
→ Template Sync
→ Project Applied Sync
→ New Project Bootstrap
→ Skill Runtime Activation
→ Usage
→ Effectiveness
```
