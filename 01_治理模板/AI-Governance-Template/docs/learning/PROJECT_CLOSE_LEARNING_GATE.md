# Project Close Learning Gate｜V1.9 Final

## Promotion Reconciliation

每个 `PROMOTED`：

```text
Current Project file hash
=
Promotion Receipt project_hash_after

Template file hash
=
Promotion Receipt template_hash_after

TEMPLATE_MANIFEST
包含 target + matching sha256

TEMPLATE_VERSION.last_promotion_id
=
最后成功 Promotion
```

任何 `PARTIAL`：

```text
CLOSED DENY
```

## Skill Reconciliation

```text
Promoted Skill
→ SKILL_REGISTRY.status = PRODUCTION

Production Skill
→ Project file exists
→ Template file exists
→ Manifest contains file

Lifecycle = REUSED_IN_NEXT_PROJECT
→ SKILL_USAGE record required

Lifecycle = EFFECTIVENESS_RECORDED
→ SKILL_EFFECTIVENESS record required
```

出现：

```text
Skill file promoted
but registry missing
```

则：

```text
LEARNING_GATE = FAIL
```

## Pattern Reconciliation

任何：

```text
occurrences >= 3
```

的 Canonical Pattern 必须存在：

```text
current_enforcement
or explicit governance decision
```

不能继续只有语言提醒。

## Applied Governance

Project Close 时：

```text
PROJECT_GOVERNANCE_APPLIED.current_applied_fingerprint
=
Current Approved Template fingerprint
```

或存在合法 Trusted Template Pin。

Origin 不参与 stale 比较。

## V1.10 Skill Semantics Reconciliation

Project Close 不能把 LOADED 当 USED。

```text
Lifecycle = REUSED_IN_NEXT_PROJECT
→ 必须有 usage_state = USED
→ usage.project_id != source_project_id

Lifecycle = EFFECTIVENESS_RECORDED
→ 必须有 INDEPENDENT_CONFIRMED Effectiveness
```

Production Skill 同时检查：

```text
Project Skill Hash
Registry validated_content_hash
Applied Manifest Hash
```

任何漂移：

```text
LEARNING_GATE = FAIL
```
