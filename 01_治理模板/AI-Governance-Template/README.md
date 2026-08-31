# AI-Governance-Template｜ORCA V2.3.1

**Delivery Version：V1.9**  
**Template Version：2.3.1-template.3**

# AI-Governance-Template｜ORCA V2.3.1 自循环版

模板应位于：

```text
<Governance-Management-Root>/templates/AI-Governance-Template/
```

这是唯一权威模板副本。

## 核心运行闭环

```text
用户反馈
↓
Task Manager 记录 / 分类 / 排序
↓
Dispatch Gateway
↓
Worker 执行
↓
Evidence
↓
State Guard
↓
Continuation Guard
├─ 有合法 Stop → Stop / Human Gate
└─ 无合法 Stop → 必须继续 next_action
↓
运行中出现纠正 / 规则违反 / 重复失败 / workaround / 浪费
↓
Learning Event Capture
↓
Learning Router
├─ Project-only
├─ Guard Rule
├─ Skill
├─ Template
├─ Runtime Incident
└─ Model Fact
↓
当前项目立即修正
↓
可复用低风险改进经过测试
↓
Promotion Gateway
↓
Current Project + Governance Template
↓
Template Version / Manifest 更新
↓
项目结束 Learning Gate 对账
↓
CLOSED
↓
下一个项目记录最新 Template Fingerprint
```

## 纪律固化层级

```text
Memory
< Prompt
< Skill
< Guard
< Contract Test
```

同一问题越重复，约束越应该从“提醒模型”升级为“机器强制”。

## 机器权威 Source of Truth

```text
MODEL_ROUTING_REGISTRY.yaml
WORKER_LAUNCH_REGISTRY.yaml
runtime-health.json
governance-state.yaml
COMPATIBILITY_MATRIX.yaml
EVIDENCE.jsonl
LEARNING_EVENTS.jsonl
PROMOTION_QUEUE.yaml
PROMOTION_LOG.jsonl
SKILL_REGISTRY.yaml
TEMPLATE_VERSION.yaml
TEMPLATE_MANIFEST.json
```

对应 Markdown 只是 readable/generated view。

## 新项目治理来源

新项目初始化时根据：

```text
docs/governance/PROJECT_GOVERNANCE_ORIGIN.template.yaml
```

写入项目自己的：

```text
docs/governance/PROJECT_GOVERNANCE_ORIGIN.yaml
```

记录：

- template_version
- template_fingerprint
- template_manifest_hash
- created_at
- template source repo/commit（可获得时）

## 真实环境落地前

仍必须验证：

- ORCA 当前真实 Run / Task / Dispatch / Worker 接口；
- OpenCode permission schema；
- Codex CLI；
- Launcher；
- Computer Use；
- Negative Canary；
- Migration Audit。

未验证即 Fail Closed。


## Learning Loop Final Freeze Addendum

- Continuation Guard 只信 Machine State，不信 Task Manager 自报 COMPLETE。
- recurrence_count 只由 LEARNING_EVENTS 历史机器计算。
- Promotion actor 从 trusted Dispatch identity 反查。
- Promotion 必须 Trusted Validation Receipt。
- Production Skill 必须进入 Dispatch activation path。
- 新项目必须 bootstrap-project + Template Fingerprint。
- Close Learning Gate 必须 Promotion Hash Reconciliation。
- Learning Loop Design Freeze 后不再增加新理论 Agent / Plane。
