# 调查报告：Builder → MiMo V2.5 根因分析

> 日期: 2026-08-30 | 类型: 只读调查 | 修改文件数: 0

---

## 一、调查背景

Migration Audit 中 positive-contract 步骤失败：

```
role = builder
model = opencode-go/mimo-v2.5
→ dispatch-worker 第 34 行拦截：
  DENY: non-Task-Manager OpenCode Go requires separate one-shot override flow
```

本轮任务：查清为什么 Builder/开发者角色被分配了 MiMo V2.5。

---

## 二、调查结果

### 1. 当前 Builder 实际模型

**无正式 Production Mapping。** `MODEL_ROUTING_REGISTRY.yaml` 的 `current_production_mappings`（第 66-74 行）只定义了两个角色：

```yaml
current_production_mappings:
  task-manager:
    model_ref: opencode-go/mimo-v2.5
  senior-expert:
    model_ref: codex/gpt-5.6-sol
```

**Builder 不在其中。** Registry 只规定了 Builder 的 `allowed_pools: [OPENCODE_FREE, GPT_PRO]`（第 42-45 行），但没有指定具体 model_ref。

### 2. 当前 Builder 实际 backend

**opencode**（由调用方传入 `--backend opencode`）

### 3. 当前 Builder resource pool

Registry 规定：`OPENCODE_FREE` + `GPT_PRO`。MiMo 所在的 `OPENCODE_GO` 池**不允许** Builder 使用。

### 4. MiMo 配置的准确来源

- **文件**: `docs/model/MODEL_ROUTING_REGISTRY.yaml:68`
- **字段**: `current_production_mappings.task-manager.model_ref`
- **值**: `opencode-go/mimo-v2.5`

这是 Task Manager 的正式 Production Mapping，不是 Builder 的。

### 5. 当前 MODEL_ROUTING_REGISTRY 中 Builder 配置

```yaml
role_pool_policy:
  builder:
    allowed_pools:
    - OPENCODE_FREE
    - GPT_PRO
```

`models` 列表中 role_fit 包含 builder 的：
- `gpt-pro/terra-candidate`（GPT-5.6 Terra）— status: CANDIDATE，未 PRODUCTION
- `codex/gpt-5.6-sol`（GPT-5.6 Sol）— role_fit 包含 `builder-escalation`

**无任何 model 的 role_fit 包含 `builder` 且 status 为 PRODUCTION。**

### 6. 当前 Task Manager 配置

```yaml
task-manager:
  model_ref: opencode-go/mimo-v2.5
  allowed_pools: [OPENCODE_GO]
```

### 7. current applied template version

`2.3.1-template.25`（与模板源一致，diff 无差异）

### 8. 是否存在旧 V2.2.1 routing residue

**NO。** 项目 `MODEL_ROUTING_REGISTRY.yaml` 与模板 `01_治理模板/AI-Governance-Template/docs/model/MODEL_ROUTING_REGISTRY.yaml` 完全一致（diff 无输出）。不存在项目侧残留旧配置。

### 9. positive-contract 是否硬编码 MiMo

**NO。** positive-contract（第 11 行）的 `--builder-model` 是 `required=True` 参数，无默认值。它原样转发给 dispatch-worker（第 89 行 `'model':a.builder_model`），不做任何 registry 校验。

**但调用方（migration-audit）也是透传**（第 17、35 行），同样无默认值。

**实际传入 `opencode-go/mimo-v2.5` 的是本次 Task Manager 执行 migration-audit 时手动指定的。**

### 10. dispatch-worker 是否覆盖 Registry

**NO。** dispatch-worker（第 30 行 `model=req.get('model')`）直接使用请求中的 model，不查询 MODEL_ROUTING_REGISTRY。它只做一条硬性拦截（第 34 行）：

```python
if role!='task-manager' and backend=='opencode' and str(model or '').startswith('opencode-go/'):
    raise SystemExit('DENY: non-Task-Manager OpenCode Go requires separate one-shot override flow')
```

**不覆盖，但也不校验 model 是否属于角色的 allowed_pools。**

### 11. fallback 是否跨角色污染

**NO。** dispatch-worker 和 launch-opencode 中无任何 fallback 逻辑。model 为 None 时 launch-opencode 会报错（第 24 行 `required=True`）。

### 12. role resolution 是否异常

**NO。** dispatch-worker 第 83-88 行正确识别 builder 角色并加载 `builder-v2.json` profile。角色解析无误。

### 13. Luna / Sol 目标配置是否已正式进入 Registry

**部分。** `codex/gpt-5.6-sol` 已在 Registry 中 status=PRODUCTION，role_fit 包含 `builder-escalation`。但 `gpt-pro/luna-candidate` 和 `gpt-pro/terra-candidate` 仍为 CANDIDATE 状态。

**Builder 的正式 Production Mapping（非 escalation）从未写入 Registry。**

### 14. Governance Template 与当前项目 Registry 是否一致

**YES。** 两者完全一致。

---

## 三、根因链路

```
Task Manager 执行 migration-audit
  → 传入 --builder-model "opencode-go/mimo-v2.5"（手动指定，错误使用了 Task Manager 模型）
    → migration-audit 透传给 positive-contract
      → positive-contract 透传给 dispatch-worker
        → dispatch-worker 第 34 行拦截：
          role!=task-manager && model.startswith('opencode-go/') → DENY
```

---

## 四、根因分类

```
EXPECTED_MODEL_MAPPING_NOT_PROMOTED_TO_REGISTRY
```

叠加：

```
POSITIVE_CONTRACT_MODEL_FIXTURE_DRIFT
```

---

## 五、核心问题

1. **MODEL_ROUTING_REGISTRY 中 Builder 没有正式 Production Mapping** — `current_production_mappings` 缺少 builder 条目
2. **测试基础设施无 registry 自动解析** — migration-audit / positive-contract 的 `--builder-model` 是必填参数但无默认值、无 registry 校验
3. **Task Manager 在无明确 Builder 模型可用时错误使用了 Task Manager 模型** — 因为没有任何文档或配置告诉它 Builder 应该用什么

---

## 六、应该修的是

**TEST INFRASTRUCTURE** — migration-audit 和 positive-contract 应该：
- 从 MODEL_ROUTING_REGISTRY 自动解析 Builder 的 model_ref（如果存在）
- 或在 Registry 中正式定义 Builder 的 Production Mapping
- 而不是依赖调用方手动传入一个无校验的字符串

具体需要：
1. 在 `MODEL_ROUTING_REGISTRY.yaml` 的 `current_production_mappings` 中增加 `builder` 条目
2. 或让 migration-audit/positive-contract 从 registry 自动读取 builder model，而非必填参数

---

## 七、不需要改的

- `non_task_manager_go_default: DENY` — 正确的安全边界，不应修改
- Governance Template — 项目与模板一致，无需改模板
- dispatch-worker — 拦截逻辑正确
- Task Manager 模型配置 — MiMo 用于 TM 是正确的

---

## 八、关键文件索引

| 文件 | 关键行 | 内容 |
|------|--------|------|
| `docs/model/MODEL_ROUTING_REGISTRY.yaml` | 42-45 | builder allowed_pools |
| `docs/model/MODEL_ROUTING_REGISTRY.yaml` | 66-74 | current_production_mappings（缺 builder） |
| `docs/model/MODEL_ROUTING_REGISTRY.yaml` | 76-92 | MiMo model 定义（role_fit: task-manager only） |
| `scripts/runtime/macos/dispatch-worker` | 34-35 | opencode-go deny 守卫 |
| `scripts/runtime/macos/positive-contract` | 11, 89 | --builder-model 透传 |
| `scripts/runtime/macos/migration-audit` | 17, 35 | --builder-model 透传 |
| `docs/runtime/MAC_RUNTIME_CONFIG.json` | 34 | non_task_manager_go_default: DENY |

---

## 九、最终结论

```
MODEL_ROUTING_DRIFT_ROOT_CAUSE_IDENTIFIED
```

Builder 角色从未在 `current_production_mappings` 中获得正式 model_ref，而测试基础设施要求调用方手动指定一个无 registry 校验的 model 参数，导致 Task Manager 错误地将 Task Manager 模型（MiMo）传给了 Builder 角色。

本轮修改文件数：**0**
