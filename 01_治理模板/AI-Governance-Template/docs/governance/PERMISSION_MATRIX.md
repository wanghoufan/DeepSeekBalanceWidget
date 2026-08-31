# Permission Matrix｜V2.3.1

> 必须落到真实 ORCA / OpenCode 权限，不得只写 Prompt。

| 角色 | 允许 | 禁止 |
|---|---|---|
| Planner | read/search/architecture analysis | 未授权大规模业务写入、push、destructive |
| Builder Development/Fix | 业务源码 read/edit、test、build | 未授权 push、destructive、secrets、高 Action Risk |
| Builder Investigation | read/grep/logs/diagnostics/test | 业务源码 edit |
| Code Reviewer | read/grep/diff/test | edit/commit/push/destructive |
| QA | read/test/build/logs/Browser/Computer Use | 业务源码 edit、自行 Fix、push、destructive |
| Product Reviewer | read/Browser/Computer Use/screenshots | 业务源码 edit、push、destructive |
| Task Manager | 调度 allowlist、只读治理权威 | 业务写、治理宪法写、Evidence 写、State 写、raw launch、业务 Subagent |
| Experience Recorder | 经验/模板候选记录（批准路径） | 普通业务源码 edit、绕过 Task Manager 委派 Builder |
| neat-freak | 项目工程/治理收尾 | Runtime Compatibility Maintenance Owner、业务越权开发 |

## Task Manager Writable Paths

本模板明确允许：

```text
docs/inbox/USER_FEEDBACK_INBOX.jsonl
docs/progress/SCHEDULING_NOTES.md
docs/progress/TASK_PRIORITY.md
docs/progress/USER_DECISIONS.md
docs/handoff/HANDOFF.md
```

这些都不是机器治理权威。

## Task Manager Read-only Authority

```text
docs/model/MODEL_ROUTING_REGISTRY.yaml
docs/runtime/WORKER_LAUNCH_REGISTRY.yaml
docs/runtime/runtime-health.json
docs/progress/governance-state.yaml
docs/progress/EVIDENCE.jsonl
docs/governance/COMPATIBILITY_MATRIX.yaml
docs/governance/*
scripts/workers/launch-*
scripts/governance/write-evidence
scripts/governance/state-writer
```

## 业务目录硬只读

按项目实例调整，至少：

```text
src/
app/
packages/
server/
client/
migrations/
生产脚本
业务配置
```

## Shell Allowlist

Task Manager 不得拥有可任意运行：

```text
echo / sed / perl / python / node / powershell
```

来绕过写权限的万能 shell。

只允许：

- read-only Git；
- ORCA read/status；
- Dispatch Gateway；
- State Transition Request；
- Human Gate Create；
- Registry / Runtime Health read。

## Preflight

每次 permission schema 变化或 OpenCode 升级：

```text
opencode --version
→ 读取该版本当前官方 permission schema
→ 生成配置
→ Negative Canary
```

没有 Canary PASS：

> `TASK_MANAGER_ISOLATION` 不得标记 VERIFIED。


## Learning Loop 权限补充

Task Manager 允许通过受控入口：

```text
continuation-guard
write-learning-event
request-promotion
```

Task Manager 对以下只读 / 无直接生产写权限：

```text
PROMOTION_QUEUE.yaml（除 request-promotion 受控入口）
PROMOTION_LOG.jsonl
SKILL_REGISTRY.yaml
TEMPLATE_VERSION.yaml
TEMPLATE_MANIFEST.json
production Governance Template
promotion-gateway production write path
```

Task Manager 不能直接安装未经验证 Skill，也不能直接执行高影响 Governance Promote。


## Learning Loop Final Permissions

Task Manager Read-only：

```text
docs/runtime/TRUSTED_DISPATCH_IDENTITY.jsonl
docs/runtime/task-runtime.json
docs/learning/VALIDATION_RECEIPTS.jsonl
docs/learning/SKILL_REGISTRY.yaml
docs/learning/SKILL_EFFECTIVENESS.jsonl
```

Task Manager 允许：

```text
Capture Learning Event
Submit Candidate
Request Promotion
Read Promotion Result
```

禁止：

```text
direct promotion
forge actor role
forge recurrence_count
forge Validation Receipt
direct write production Skill Registry
```

## V1.9 Learning Authority

Task Manager 可：

```text
propose pattern alias
capture learning event
request promotion
read pattern / skill / usage / effectiveness
```

Task Manager 不可：

```text
write canonical pattern identity
write recurrence_count
direct register Production Skill
fabricate loaded_skill_ids
write Skill Usage / Effectiveness without trusted Dispatch proof
modify PROJECT_GOVERNANCE_APPLIED directly
forge TEMPLATE_PIN approval
```

机器权威新增：

```text
LEARNING_PATTERN_REGISTRY.yaml
SKILL_REGISTRY.yaml
SKILL_USAGE.jsonl
SKILL_EFFECTIVENESS.jsonl
PROJECT_GOVERNANCE_APPLIED.yaml
TRUSTED_USER_APPROVAL_RECEIPTS.jsonl
```

## V1.10 Internal Writer / Mac Runtime Boundary

Agent / Worker 禁止直接执行：

```text
register-skill
promotion-queue-writer
template-sync
trusted-dispatch-identity-writer
```

Internal Writer 只接受 Trusted Governance Process 的 scoped one-shot capability。

Worker 环境必须移除：

```text
ORCA_GOVERNANCE_INTERNAL_SECRET
```

Task Manager 继续：

```text
read-only governance
+
request-promotion
+
Dispatch Gateway
```

不得：

```text
raw opencode
raw codex
raw worker-start
direct internal writer
```

第一阶段 Permission Canary 只在真实 macOS 验证；Windows / WSL 不在本轮生产验证范围。
