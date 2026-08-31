# New Project Bootstrap｜Origin / Applied Final Contract

受控入口：

```text
scripts/governance/bootstrap-project
```

Bootstrap：

```text
Template Manifest verify
→ Template Version
→ Governance Assets
→ Production Skills
→ PROJECT_GOVERNANCE_ORIGIN.yaml
→ PROJECT_GOVERNANCE_APPLIED.yaml
→ Stale Template Guard
→ PASS
→ ACTIVE
```

## Origin｜Immutable

```text
origin_template_version
origin_manifest_hash
origin_fingerprint
created_at
```

只回答：

> 项目出生于哪个模板。

Promotion 后不修改。

## Applied｜Mutable Trusted Governance State

```text
current_applied_template_version
current_applied_manifest_hash
current_applied_fingerprint
last_promotion_id
updated_at
pin
```

只回答：

> 当前项目实际吸收到了哪个模板状态。

当前项目成功 Promotion 后自动同步。

## Stale Guard

比较：

```text
PROJECT_GOVERNANCE_APPLIED
vs
Current Approved Template
```

不比较 Origin。

## Pin Old Template

只有：

```text
Trusted User Approval Receipt
scope=TEMPLATE_PIN
```

通过 Validator 后，才能进入：

```text
PINNED_OLD_TEMPLATE
```

否则 Fail Closed。
