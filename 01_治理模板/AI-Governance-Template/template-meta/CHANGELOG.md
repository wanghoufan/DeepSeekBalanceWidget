# Changelog

## V1.10.1｜Mac 真实验收回写

- `MAC_RUNTIME_V1` 完成真实 Mac Migration Audit，状态升级为 `VERIFIED`。
- OpenCode 能力检测固定为 `opencode run --help`。
- OpenCode 启动后重新解析 ORCA 活动 Terminal Handle；禁止持久信任创建回执句柄。
- Run / Task / Dispatch 绑定与 RPC `result` 身份解析纳入运行时契约。
- ORCA 当前不支持 OpenCode `--inject`：正式路径固定为受追踪 Dispatch 后的 Terminal Delivery。
- 正向合同完成条件固定为 Dispatch 完成且 Terminal Idle，不再等待终端关闭。
- 补充 Mac Runtime Compatibility、Worker Launch Registry、Health 与 Canonical Pattern 回写。

## V1.0

- 固化 V2.3.1 最终安全收口；
- Evidence Trusted Producer Identity；
- Trusted Transition Receipt；
- Human Gate Trusted Approval Chain；
- Launcher Freshness；
- Machine Source of Truth；
- Go Scheme B；
- External ChatGPT Desktop Maintenance Plane；
- Task Manager Resume / Handoff writable path。


## V1.1

- Continuation Guard；
- Learning Event Ledger；
- Learning Router；
- Promotion Gateway；
- 双目标 Current Project + Template 同步；
- Promotion Receipt / PARTIAL；
- Template Version / Manifest / Fingerprint；
- Project Governance Origin；
- Project Close Learning Gate；
- Repeated User Correction Escalation；
- Skill 完整生命周期；
- Learning Loop Contract Tests。


## V1.2

- Continuation Guard trusted-state verification；
- Machine recurrence_count + learning_pattern_id；
- Promotion trusted actor identity；
- Trusted Validation Receipt；
- Skill risk fields + Dispatch Skill Activation；
- New Project Bootstrap + Stale Template Guard；
- Project Close hash reconciliation；
- run-all timeout / cleanup / progress；
- Promotion Queue lock + atomic update；
- Learning Ledger stale-lock recovery；
- Delivery / Folder / Manifest / Changelog version consistency。


## V1.9｜自循环最终收口

### Version Truth
- 外部 ZIP、Top-level Folder、README、Delivery Manifest、Version Audit、delivery_version 统一为 V1.9。
- Template Version 升级为 `2.3.1-template.3`。
- 本版为真实内容变更，不是单纯改文件名。

### Canonical Learning Pattern
- 新增 `LEARNING_PATTERN_REGISTRY.yaml`。
- 新增 `resolve-learning-pattern`。
- Alias / symptom 统一解析到 Canonical Pattern。
- recurrence 从机器 Pattern Registry / Ledger 累计。
- Pattern 增加 enforcement / recurrence_after_enforcement / effectiveness。

### User Correction Escalation
- 第 1 次：Project Fix。
- 第 2 次：Reusable Candidate + Root Cause Review。
- 第 3 次：再依据 Canonical Policy 升级 Guard / Contract / Tool / Skill / Template。

### Skill Auto Registration
- 新增 `register-skill`。
- Skill Promotion 自动写 Project + Template `SKILL_REGISTRY.yaml`。
- Production Skill 必须绑定 Trusted Validation Receipt。
- Skill 文件存在不再等价于 Production。

### Skill Runtime Closure
- 新增 `SKILL_USAGE.jsonl`。
- 新增 `record-skill-usage`。
- 新增 `record-skill-effectiveness`。
- Lifecycle 的 REUSED / EFFECTIVENESS 由 trusted Dispatch identity 推进。

### Origin / Applied Split
- Origin immutable。
- 新增 `PROJECT_GOVERNANCE_APPLIED.yaml`。
- Stale Guard 改为比较 Applied。
- Promotion 成功后自动同步 Applied。

### Queue Reliability
- `learning-router` / `request-promotion` / `promotion-gateway` 使用统一 `promotion_queue_lib`。
- FileLock 修复“刚创建但 JSON 尚未写完”被误判 stale 的竞态。
- Atomic Update + stale-lock recovery。

### Template Pin
- 新增 `validate-user-approval-receipt`。
- `TEMPLATE_PIN` 校验 project / version / manifest / trusted origin / expiry / one-shot。
- Stale Guard 接通 Trusted User Approval Receipt。

### Test Reliability
- `run-all` 使用 per-test timeout / overall timeout / flush / child cleanup / exit code。
- 新增 Canonical Alias、Skill Auto Registration、Skill Usage/Effectiveness、Origin/Applied、Pin Receipt、Queue Concurrency 测试。
- 新增 `test-full-cross-project-learning-loop` 完整 E2E。


## V1.10｜Mac First 最终实施收口

- Internal-only：register-skill / promotion-queue-writer / template-sync。
- HMAC one-shot Internal Capability + Agent environment secret scrubbing。
- Promotion Stale Preflight。
- Pinned old project direct public-template promotion DENY。
- governance-sync：Stale → Contract Tests → Applied CURRENT。
- LOADED / USED / REUSED_IN_NEXT_PROJECT / EFFECTIVENESS_RECORDED 分离。
- Skill Registry 新增 source_project / source_event / validated hash / reuse provenance。
- resolve-skills 三方 Hash 校验。
- run-all 自动发现全部 Active test-*。
- 旧测试移入 tests/deprecated + TEST_STATUS。
- Novel Pattern Candidate Dedup / Merge Gateway。
- Independent Skill Effectiveness Confirmation。
- Pattern Registry Lock + Shared Template Promotion Lock。
- 新增 MAC_RUNTIME_V1 Adapter。
- Windows / WSL Adapter 延后。
- Local Active Tests：20/20 PASS。
- MAC_RUNTIME_V1 仍为 IMPLEMENTED_UNVERIFIED，等待真实 Mac Canary / Contract / Migration Audit。
