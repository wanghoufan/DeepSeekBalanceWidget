# Learning Policy｜V1.9 Final Self-Learning Loop

## 1. Canonical Pattern

机器权威：

```text
docs/learning/LEARNING_PATTERN_REGISTRY.yaml
```

Task Manager 可以提出：

```text
pattern alias
rule key
symptom description
```

但不能决定：

```text
canonical_pattern_id
recurrence_count
```

流程：

```text
Learning Event
→ resolve-learning-pattern
→ Canonical Pattern
→ Pattern Registry occurrence
→ LEARNING_EVENTS
```

无法可靠匹配时：

```text
NEW_PATTERN_CANDIDATE
```

而不是把调用者给出的名字直接当 Canonical ID。

## 2. Repeated User Correction

第 1 次：

```text
Capture + Project Fix
```

第 2 次：

```text
Reusable Candidate + Root Cause Review
```

第 3 次仍复发：

```text
Prompt-only FAILED
→ Canonical Pattern Policy
→ machine enforceable?
   → Guard / Contract Test / Tool Constraint
→ stable multi-step SOP?
   → Skill
→ otherwise
   → Template / Prompt Rule
```

## 3. Pattern Effectiveness

Pattern Registry 至少保留：

```text
canonical_id
aliases
occurrences
first_seen
last_seen
current_enforcement
last_enforcement_change
recurrence_after_enforcement
effectiveness
```

新 enforcement 上线后：

- 再复发 → `RECURRENCE_DETECTED`
- 真实 Skill Effectiveness 证明有效 → 可记录 `EFFECTIVE`

## 4. Promotion Identity / Validation

Task Manager 只能：

```text
Capture
Submit Candidate
Request Promotion
Read Result
```

不能 direct Promote。

Promotion Gateway 必须：

```text
trusted Dispatch identity
+
Trusted Validation Receipt
+
content hash
```

不得信任：

```text
actor_role 自报
validated=true
tests=PASS
```

## 5. Skill Auto Registration

Skill Promotion 正确链：

```text
Skill Candidate
→ Implemented
→ Trusted Validation Receipt
→ Promotion Gateway
→ Skill file dual-write
→ register-skill
→ SKILL_REGISTRY.status = PRODUCTION
→ Template Sync
```

Skill 文件存在：

> 不等于 Production。

只有 Registry Production 才能进入运行路径。

## 6. Skill Runtime Activation

Dispatch Gateway 必须：

```text
Task / Feedback
→ resolve-skills
→ selected_skill_ids
→ load Skill content
→ Worker Context
→ trusted Dispatch metadata.loaded_skill_ids
```

## 7. Usage / Effectiveness

实际加载后：

```text
record-skill-usage
```

必须从 trusted Dispatch identity 证明：

- Skill 真被加载
- Task / Dispatch
- Worker / Role

任务结果后：

```text
record-skill-effectiveness
```

记录真实：

- expected / actual
- helped
- failure
- manual intervention
- rework_count
- effectiveness_result

Lifecycle：

```text
OBSERVED
→ CANDIDATE
→ IMPLEMENTED_LOCAL
→ VALIDATED_ON_REAL_TASK
→ PROMOTED_TO_GOVERNANCE
→ INCLUDED_IN_TEMPLATE
→ REUSED_IN_NEXT_PROJECT
→ EFFECTIVENESS_RECORDED
```

## 8. Origin / Applied

`PROJECT_GOVERNANCE_ORIGIN.yaml`：

> 项目出生时来自哪个 Template。Immutable。

`PROJECT_GOVERNANCE_APPLIED.yaml`：

> 当前项目已经实际吸收哪个 Template 状态。

Stale Guard 只比较：

```text
Applied
vs
Approved Current Template
```

当前项目自己的 Promotion 成功后：

> 只更新 Applied，不改 Origin。

## 9. Old Template Pin

旧模板 Pin 必须使用：

```text
Trusted User Approval Receipt
scope = TEMPLATE_PIN
```

校验：

- project_id
- approved_template_version
- approved_manifest_hash
- trusted_user_origin
- expiry
- one_shot

无可信 Receipt：

> Fail Closed。

## 10. Project Close

CLOSED 前：

- Promotion Hash Reconciliation
- promoted Skill 已注册
- Production Skill 文件存在且 Manifest 包含
- reused Skill 有 Usage
- Effectiveness Lifecycle 有 Effectiveness Record
- repeated Canonical Pattern 有 Enforcement / Decision
- Applied Governance 不 stale（或存在合法 Pin）

## 11. V1.10 Novel Candidate Merge / Internal Promotion

未知新问题不立即创建彼此割裂的 Canonical Pattern。

```text
Unknown Event
→ NEW_PATTERN_CANDIDATE
→ category + normalized symptom + root cause
→ Candidate Dedup
→ Existing Candidate or New Candidate
```

Experience Recorder 可以：

```text
request-pattern-merge
```

真正 Registry Merge 走：

```text
pattern-merge-gateway
```

并在同一个 Pattern Registry Lock 中完成 alias / occurrence / decision 合并。

Promotion Transaction：

```text
Stale Preflight
→ Shared Template Promotion Lock
→ Dual-target Write
→ Internal Skill Registration
→ Internal Template Sync
→ Applied Sync
```

所有 Internal Writer 都禁止 Agent 直调。
