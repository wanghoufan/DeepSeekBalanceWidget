# Governance State Machine｜Readable Policy

机器权威：

```text
docs/progress/governance-state.yaml
```

本文件只是状态机规则说明。

## 状态

```text
DRAFT
ACTIVE
BLOCKED
READY_FOR_ACCEPTANCE
ACCEPTED
CLOSED
```

## 唯一合法链路

```text
Task Manager
↓
request-state-transition
↓
State Guard
↓
读取 Stage P0 / Evidence / Dispatch / Commit / Human Gate
↓
PASS
↓
Trusted Transition Receipt
↓
State Writer
↓
governance-state.yaml
```

Task Manager direct `state-writer`：

> DENY

## Receipt

至少：

```text
receipt_id
stage_id
from_state
to_state
commit_sha
guard_result
issued_at
expires_at
used_at
one_shot
```

必须防 forged / expired / replay / wrong scope。

## DRAFT → ACTIVE

- `PROJECT_GOVERNANCE_APPLIED.yaml` 必须通过 Stale Template Guard；

- `bootstrap-project` / Stale Template Guard 已 PASS；
- `PROJECT_GOVERNANCE_ORIGIN.yaml` 存在且 Fingerprint 合法；

- Stage Contract 已确认；
- Stage P0 已定义；
- In / Out Scope 明确。

## ACTIVE → READY_FOR_ACCEPTANCE

- Stage P0 有实现结果；
- Blocking Defect P0 = 0；
- 无未处理 P0 active/failed Dispatch；
- 当前 Commit 已记录；
- 必要 Automated Evidence 对应当前版本；
- 必要 Engineering / GUI QA 已完成或明确 not-triggered。

## READY_FOR_ACCEPTANCE → ACCEPTED

- Stage P0 checklist；
- 当前 Repo / Worktree / Commit Evidence 完整；
- 必要 QA Acceptance PASS；
- 未覆盖显式记录；
- Blocking P0 = 0；
- 高 Action Risk Gate 已处理。

## ACCEPTED → CLOSED

- Engineering Closeout；
- 文档同步；
- Git 状态符合要求；
- unresolved blocking task = 0；
- Improvement Capture 已执行；
- **Project Close Learning Gate = PASS；**
- 所有 Learning Event 已有最终 disposition；
- Promotion Log 无 `PARTIAL`；
- Template Version / Manifest 已同步；
- Stage Closeout Summary。


## Negative Canary

```text
Task Manager direct state-writer → DENY
forged receipt → DENY
wrong scope/stage/commit → DENY
expired receipt → DENY
replayed receipt → DENY
valid one-shot receipt → PASS once
```
