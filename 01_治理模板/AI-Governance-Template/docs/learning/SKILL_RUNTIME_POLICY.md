# Skill Runtime Policy｜V1.10

## Production

唯一机器权威：

```text
SKILL_REGISTRY.yaml
```

Production Skill 至少绑定：

```text
validated_content_hash
validated_receipt_id
source_project_id
source_learning_event_id
first_promoted_at
first_reused_project_id
```

## Activation Hash Guard

`resolve-skills` 在注入前必须验证：

```text
Current Skill File SHA256
=
SKILL_REGISTRY.validated_content_hash
=
PROJECT_GOVERNANCE_APPLIED_MANIFEST.sha256
```

任何不一致：

```text
SKILL_DRIFT / HASH_MISMATCH
→ BLOCK
```

## LOADED

表示：

```text
Skill content 已注入 Worker Context
```

证据：

```text
trusted Dispatch.loaded_skill_ids
```

不代表 Skill 实际参与任务。

## USED

只有 Runtime / Worker Evidence 证明：

```text
trusted Dispatch.used_skill_ids
```

才允许：

```text
record-skill-usage --state USED
```

## REUSED_IN_NEXT_PROJECT

必须同时满足：

```text
usage_state = USED
current_project_id != source_project_id
```

同项目重复使用不算 Cross-project Reuse。

## Effectiveness

同一个使用 Skill 的 Dispatch：

```text
SELF_PROVISIONAL
```

只能写：

```text
PROVISIONAL_EFFECTIVE
PROVISIONAL_INEFFECTIVE
```

不同 Dispatch 且来自：

```text
QA / Product Reviewer / Code Reviewer / Automated Test
```

才允许：

```text
CONFIRMED_EFFECTIVE
CONFIRMED_INEFFECTIVE
```

只有 Independent Confirmed 才推进：

```text
EFFECTIVENESS_RECORDED
```

Schema Guard 必须拒绝逻辑冲突字段。
