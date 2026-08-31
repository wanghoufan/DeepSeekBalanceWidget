# MODEL_ROUTING_BUILDER_MAPPING_FIX_PLAN

> 日期: 2026-08-30 | 类型: 修复方案设计 | 修改文件数: 0（本轮只输出方案）

---

## 一、调查结论回顾

**根因分类**: `MODEL_ROUTING_SOURCE_OF_TRUTH_INCOMPLETE` + `MANUAL_MODEL_INJECTION_WITHOUT_REGISTRY_VALIDATION`

**核心问题**: Governance Template 的 `MODEL_ROUTING_REGISTRY.yaml` 中 `current_production_mappings` 缺少 `builder` 条目，而 `migration-audit` / `positive-contract` 的 `--builder-model` 是无 registry 校验的手动必填参数，导致 Task Manager 错误地将 Task Manager 模型（MiMo）传给 Builder 角色。

---

## 二、真实模型 ID 确认

### Luna

| 字段 | 值 |
|------|-----|
| canonical model id | `gpt-5.6-luna` |
| backend | `codex` |
| provider | OpenAI / GPT Pro |
| Codex CLI 调用 | `codex -m gpt-5.6-luna` |
| reasoning effort 参数 | `-c model_reasoning_effort="high"` |
| 当前 Qualification 状态 | `CANDIDATE`（Registry 中 `gpt-pro/luna-candidate`） |
| 用户 Codex 默认配置 | `model = "gpt-5.6-luna"` + `model_reasoning_effort = "high"` |

### Sol

| 字段 | 值 |
|------|-----|
| canonical model id | `gpt-5.6-sol` |
| backend | `codex` |
| provider | Codex / GPT Pro |
| Codex CLI 调用 | `codex -m gpt-5.6-sol` |
| reasoning effort 参数 | `-c model_reasoning_effort="medium"` |
| 当前 Qualification 状态 | `PRODUCTION`（Registry 中 `codex/gpt-5.6-sol`） |
| role_fit | `senior-expert`, `builder-escalation`, `investigation-escalation`, `review-escalation` |

### MiMo（Task Manager，不修改）

| 字段 | 值 |
|------|-----|
| canonical model id | `opencode-go/mimo-v2.5` |
| backend | `opencode` |
| resource_pool | `OPENCODE_GO` |
| 当前状态 | PRODUCTION |

---

## 三、架构决策

### 3.1 Builder 采用 role + routing profile，不新增角色

**决策**: 保持 `role = builder`，通过 `routing_profile` 区分普通开发和高级开发。

```
role = builder
├── routing_profile = BUILDER_PRIMARY
│   → Codex Luna / MAX / GPT_PRO
├── routing_profile = BUILDER_SENIOR
│   → GPT-5.6 Sol / MEDIUM / GPT_PRO
└── routing_profile = BUILDER_ESCALATION
    → GPT-5.6 Sol（复用 senior-expert mapping）
```

**不新增** `junior-builder` / `senior-builder` 角色。

### 3.2 Task Manager Mapping 保持不变

```
task-manager → opencode-go/mimo-v2.5 → OPENCODE_GO
```

**不修改。**

### 3.3 dispatch-worker Hard Guard 保持不变

```python
if role!='task-manager' and backend=='opencode' and str(model or '').startswith('opencode-go/'):
    raise SystemExit('DENY: non-Task-Manager OpenCode Go requires separate one-shot override flow')
```

**不删除。** 作为 Defense in Depth。

### 3.4 non_task_manager_go_default 保持 DENY

**不修改。**

---

## 四、MODEL_ROUTING_REGISTRY 目标结构

### 4.1 新增 current_production_mappings.builder

```yaml
current_production_mappings:
  task-manager:
    model_ref: opencode-go/mimo-v2.5
    note: Current mapping from governance baseline; runtime launch still requires
      VERIFIED launch contract.
  builder:
    model_ref: codex/gpt-5.6-luna
    routing_profile: BUILDER_PRIMARY
    backend: codex
    resource_pool: GPT_PRO
    reasoning_effort: high
    note: Standard builder execution. Free-first when capability sufficient; GPT Pro
      for professional coding tasks.
  builder-senior:
    model_ref: codex/gpt-5.6-sol
    routing_profile: BUILDER_SENIOR
    backend: codex
    resource_pool: GPT_PRO
    reasoning_effort: medium
    note: High-difficulty coding, root-cause analysis, second-opinion backend.
    trigger: complexity_threshold OR user_escalation OR senior_routing
  senior-expert:
    model_ref: codex/gpt-5.6-sol
    role: backend_not_ninth_role
    note: High-difficulty coding/root-cause/second-opinion backend.
```

### 4.2 更新 models 列表中 Luna 条目

```yaml
- model_ref: codex/gpt-5.6-luna
  display_name: GPT-5.6 Luna
  provider: Codex / GPT Pro
  model_id: gpt-5.6-luna
  resource_pool: GPT_PRO
  status: CANDIDATE  # 需要 Qualification 后升级为 PRODUCTION
  role_fit:
  - builder
  - builder-primary
  forbidden_roles:
  - task-manager-default
  capabilities_required_by_mapping:
  - coding
  - tool_calling
  last_verified: null
  known_issues:
  - Qualification harness not yet completed. See MODEL_QUALIFICATION.md.
```

### 4.3 新增 routing_profiles 定义（可选，增强可读性）

```yaml
routing_profiles:
  BUILDER_PRIMARY:
    description: Standard builder execution
    default_model_ref: codex/gpt-5.6-luna
    fallback_model_ref: null  # 不继承 Task Manager
    backend: codex
    reasoning_effort: high
  BUILDER_SENIOR:
    description: High-difficulty coding and root-cause analysis
    default_model_ref: codex/gpt-5.6-sol
    fallback_model_ref: null
    backend: codex
    reasoning_effort: medium
```

---

## 五、新建 scripts/runtime/macos/resolve-model

### 5.1 职责

```
输入: role, routing_profile (可选), model_override (可选)
↓
读取 MODEL_ROUTING_REGISTRY.yaml
↓
解析 current_production_mappings
↓
输出: { backend, model_ref, resource_pool, reasoning_effort, routing_profile }
```

### 5.2 核心逻辑

```python
def resolve_model(role, routing_profile=None, model_override=None):
    registry = load_registry()  # 解析 MODEL_ROUTING_REGISTRY.yaml
    
    # 1. 如果有 explicit model override（one-shot approval），使用它
    if model_override:
        validate_override(model_override, role, registry)
        return model_override
    
    # 2. 从 current_production_mappings 查找
    mappings = registry.get('current_production_mappings', {})
    
    # 2a. 按 routing_profile 精确匹配
    if routing_profile:
        key = f"{role}-{routing_profile}"  # e.g. "builder-senior"
        if key in mappings:
            return mappings[key]
    
    # 2b. 按 role 匹配
    if role in mappings:
        return mappings[role]
    
    # 2c. Fail Closed — 不 fallback 到任何其他角色
    raise SystemExit(f'DENY: no production mapping for role={role} routing_profile={routing_profile}')
```

### 5.3 YAML 解析策略

PyYAML 不可用。采用**最小 YAML 子集解析器**：

1. 去除注释（`# ...` 行）
2. 处理多行字符串（`|` → 合并为单行）
3. 用正则提取 `key: value` 对
4. 处理嵌套缩进

或更好的方案：**在 bootstrap 时将 MODEL_ROUTING_REGISTRY.yaml 同步生成一份 JSON 副本**（`MODEL_ROUTING_REGISTRY.json`），resolve-model 读 JSON。

**推荐方案**: 使用 `json` 模块读取 JSON 副本，简单可靠。

---

## 六、修改 migration-audit

### 6.1 当前问题

```python
ap.add_argument('--builder-model', required=True)  # 无默认值，无 registry 校验
```

### 6.2 目标改造

```python
# --builder-model 改为可选，有默认值时从 registry 解析
ap.add_argument('--builder-model', default=None)
ap.add_argument('--builder-routing-profile', default='BUILDER_PRIMARY')

# 在 main() 中：
if a.builder_model:
    model = a.builder_model  # explicit override（需通过 one-shot approval）
else:
    resolver = os.path.join(here, 'resolve-model')
    r = subprocess.run([sys.executable, '-S', resolver,
                        '--role', 'builder',
                        '--routing-profile', a.builder_routing_profile],
                       capture_output=True, text=True, timeout=15)
    if r.returncode != 0:
        raise SystemExit('DENY: model resolver failed: ' + r.stdout + r.stderr)
    resolved = json.loads(r.stdout)
    model = resolved['model_ref']
```

---

## 七、修改 positive-contract

### 7.1 当前问题

```python
ap.add_argument('--builder-model', required=True)  # 透传，无校验
```

### 7.2 目标改造

```python
ap.add_argument('--builder-model', default=None)
ap.add_argument('--builder-routing-profile', default='BUILDER_PRIMARY')

# 在 main() 中：
if a.builder_model:
    model = a.builder_model
else:
    # 从 registry 解析
    resolver = os.path.join(here, 'resolve-model')
    r = subprocess.run([sys.executable, '-S', resolver,
                        '--role', 'builder',
                        '--routing-profile', a.builder_routing_profile],
                       capture_output=True, text=True, timeout=15)
    if r.returncode != 0:
        raise SystemExit('DENY: model resolver failed: ' + r.stdout + r.stderr)
    resolved = json.loads(r.stdout)
    model = resolved['model_ref']

# 新增：验证 resolved model 与 registry 一致性
validator = os.path.join(here, 'validate-model-routing')
v = subprocess.run([sys.executable, '-S', validator,
                    '--role', 'builder', '--model', model],
                   capture_output=True, text=True, timeout=15)
if v.returncode != 0:
    raise SystemExit('MODEL_ROUTING_POLICY_MISMATCH: ' + v.stdout + v.stderr)
```

---

## 八、新建 scripts/runtime/macos/validate-model-routing

### 8.1 职责

在正式 Dispatch 前校验 resolved model 是否与 Registry 一致：

```python
def validate_model_routing(role, model, registry):
    mappings = registry['current_production_mappings']
    role_mapping = mappings.get(role)
    
    if not role_mapping:
        return 'DENY', f'no production mapping for role={role}'
    
    if role_mapping['model_ref'] != model:
        return 'DENY', f'model mismatch: expected {role_mapping["model_ref"]}, got {model}'
    
    # 检查 model status
    model_entry = find_model(model, registry)
    if model_entry['status'] not in ('PRODUCTION', 'QUALIFIED'):
        return 'DENY', f'model status={model_entry["status"]}, not PRODUCTION'
    
    # 检查 role_fit
    if role not in model_entry.get('role_fit', []):
        return 'DENY', f'role={role} not in model role_fit'
    
    return 'PASS', None
```

---

## 九、dispatch-worker 改造（最小化）

### 9.1 新增 registry 校验层（在现有 Hard Guard 之前）

```python
# 新增：在 dispatch 前校验 model routing
validator = os.path.join(os.path.dirname(__file__), 'validate-model-routing')
vr = subprocess.run([sys.executable, '-S', validator,
                     '--role', role, '--model', model or ''],
                    capture_output=True, text=True, timeout=15)
if vr.returncode != 0:
    raise SystemExit('MODEL_ROUTING_POLICY_MISMATCH: ' + vr.stdout + vr.stderr)

# 现有 Hard Guard 保持不变
if role!='task-manager' and backend=='opencode' and str(model or '').startswith('opencode-go/'):
    raise SystemExit('DENY: non-Task-Manager OpenCode Go requires separate one-shot override flow')
```

### 9.2 不删除任何现有逻辑

---

## 十、需要新增的 Governance Tests

### 10.1 新建 tests/governance/test-model-routing-builder-mapping

覆盖场景：

| CASE | 输入 | 预期 |
|------|------|------|
| 1 | builder + BUILDER_PRIMARY | → codex/gpt-5.6-luna + PASS |
| 2 | builder + BUILDER_SENIOR | → codex/gpt-5.6-sol + PASS |
| 3 | task-manager | → opencode-go/mimo-v2.5 + PASS |
| 4 | builder + opencode-go/mimo-v2.5 | → DENY (MODEL_ROUTING_POLICY_MISMATCH) |
| 5 | builder + arbitrary string | → DENY (not in registry) |
| 6 | builder + model not in allowed_pool | → DENY |
| 7 | builder + CANDIDATE model as production | → 需要 Qualification 后才能 PASS |
| 8 | Registry 缺 Builder Mapping | → Fail Closed (no fallback to TM) |
| 9 | builder-senior mapping 不存在 | → Fail Closed |
| 10 | Task Manager 手工传 --builder-model MiMo | → validate-model-routing DENY |

### 10.2 更新 test-macos-runtime-adapter-static

新增断言：
- `resolve-model` 脚本存在且可解析
- `validate-model-routing` 脚本存在且可解析
- Builder routing profiles 在 Registry 中有定义

---

## 十一、Model Qualification 要求

### 11.1 Luna 状态

当前: `CANDIDATE`

要升级为 `PRODUCTION`，需要：

1. 完成 `MODEL_QUALIFICATION.md` 中定义的 30 个历史任务 Harness
2. 通过 User / Formal Governance Approval
3. 在 Registry 中将 `status` 从 `CANDIDATE` 改为 `PRODUCTION`

### 11.2 最小 Qualification 方案

如果用户确认 Luna 已在实际使用中验证：

1. 用户发出正式 approval（通过 `record-user-approval` 脚本）
2. 在 Registry 中更新 `status: PRODUCTION`
3. 更新 `last_verified` 时间戳

### 11.3 如果 Luna 尚未完成 Qualification

返回: `LUNA_PRODUCTION_QUALIFICATION_GAP`

建议：
- 临时方案：Builder 使用 Sol（已 PRODUCTION）作为 BUILDER_PRIMARY
- 或：用户发出 one-shot override 允许使用 Luna

---

## 十二、Exact Patch File Set

| 序号 | 文件 | 操作 | 说明 |
|------|------|------|------|
| 1 | `docs/model/MODEL_ROUTING_REGISTRY.yaml`（模板） | EDIT | 新增 builder/builder-senior production mappings + routing_profiles + Luna model 条目更新 |
| 2 | `docs/model/MODEL_ROUTING_REGISTRY.yaml`（项目） | SYNC | 从模板同步（通过 governance-sync） |
| 3 | `scripts/runtime/macos/resolve-model` | NEW | Model routing resolver |
| 4 | `scripts/runtime/macos/validate-model-routing` | NEW | Model routing validator |
| 5 | `scripts/runtime/macos/migration-audit` | EDIT | --builder-model 改为可选，默认从 registry 解析 |
| 6 | `scripts/runtime/macos/positive-contract` | EDIT | --builder-model 改为可选，新增 registry 校验层 |
| 7 | `scripts/runtime/macos/dispatch-worker` | EDIT | 新增 validate-model-routing 调用（Hard Guard 之前） |
| 8 | `tests/governance/test-model-routing-builder-mapping` | NEW | 10+ 个测试用例 |
| 9 | `01_治理模板/AI-Governance-Template/docs/model/MODEL_ROUTING_REGISTRY.yaml` | EDIT | 同 #1 |
| 10 | `01_治理模板/AI-Governance-Template/tests/governance/test-model-routing-builder-mapping` | NEW | 同 #8 |

---

## 十三、执行顺序

```
Phase 1: Registry 修复
├── 1a. 更新 Governance Template MODEL_ROUTING_REGISTRY.yaml
├── 1b. 验证 Registry schema 兼容性
└── 1c. 同步到项目 Applied Copy

Phase 2: 基础设施
├── 2a. 新建 resolve-model 脚本
├── 2b. 新建 validate-model-routing 脚本
└── 2c. 验证脚本可独立运行

Phase 3: 调用方改造
├── 3a. 修改 migration-audit（--builder-model 可选）
├── 3b. 修改 positive-contract（--builder-model 可选 + registry 校验）
└── 3c. 修改 dispatch-worker（新增 validate-model-routing 层）

Phase 4: 测试
├── 4a. 新建 test-model-routing-builder-mapping
├── 4b. 更新 test-macos-runtime-adapter-static
└── 4c. 运行全部 governance tests

Phase 5: 验证
├── 5a. 运行 migration-audit（不传 --builder-model）
├── 5b. 验证 positive-contract 使用 resolved model
└── 5c. 验证 dispatch-worker Hard Guard 仍然有效
```

---

## 十四、Governance Template Promotion

修复完成后，需要通过正式 Governance Sync 将模板变更应用到当前项目：

```
1. 模板变更完成
2. 运行 governance-sync
3. 验证项目 MODEL_ROUTING_REGISTRY.yaml 与模板一致
4. 运行 stale-template-guard 确认 CURRENT
```

**禁止只修改项目 Applied Copy。**

---

## 十五、风险评估

| 风险 | 级别 | 缓解 |
|------|------|------|
| Luna CANDIDATE → PRODUCTION 未完成 | HIGH | 先用 Sol 作为 BUILDER_PRIMARY，或用户发出 one-shot override |
| YAML 解析器边界情况 | MEDIUM | 使用 JSON 副本方案，或充分测试最小解析器 |
| 现有 governance tests 破坏 | LOW | 新增测试不修改现有测试逻辑 |
| dispatch-worker 性能影响 | LOW | validate-model-routing 是轻量本地操作 |

---

## 十六、最终确认

| 项目 | 结论 |
|------|------|
| 当前 authoritative Registry 路径 | `docs/model/MODEL_ROUTING_REGISTRY.yaml` |
| Luna canonical model id | `gpt-5.6-luna` |
| Luna backend | `codex` |
| Luna reasoning effort | `high`（`-c model_reasoning_effort="high"`） |
| Luna Qualification 状态 | `CANDIDATE`（需要 Qualification 或用户 approval） |
| Sol canonical model id | `gpt-5.6-sol` |
| Sol reasoning effort | `medium`（`-c model_reasoning_effort="medium"`） |
| Builder 采用方案 | role + routing profile（BUILDER_PRIMARY / BUILDER_SENIOR） |
| Task Manager Mapping | 保持不变 |
| dispatch-worker Hard Guard | 保持不变 |
| non_task_manager_go_default | 保持 DENY |
| Exact Patch File Set | 10 个文件 |
| 需要 Model Qualification | YES（Luna 需要从 CANDIDATE → PRODUCTION） |
| 需要 Governance Template Promotion | YES |
| 本轮修改文件数 | 0 |

---

**最终状态**: `MODEL_ROUTING_BUILDER_MAPPING_FIX_PLAN_READY`
