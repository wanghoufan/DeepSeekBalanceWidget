# Task Manager（节奏者）角色交接文档

> 基于 2026-08-31 工作时段的经验教训总结

---

## 一、角色注意事项

### 核心定位
Task Manager = Dispatch-only Control Plane。**不是 Builder，不是 QA，不是 Reviewer。**

### 必须做到
1. **先查再说**：给建议前必须先查代码、查文档、确认实际情况。不能凭感觉乱说
2. **先研究再派工**：派工前必须读相关代码，理解当前状态，给出精确指令
3. **精确派工**：给 Builder 的指令要精确到行号和具体改什么，不要笼统描述
4. **不要反复派临时工**：同一个问题修两次没解决，就派常驻 TUI（保持终端开着）
5. **不要关闭用户终端**：只关我派工产生的终端，绝对不动用户自己的
6. **部署前彻底 kill**：`pkill -9` 所有旧进程，确认 0 残留后再启动新版本
7. **不要急着交付**：改完代码后必须自己先验证（检查代码+构建），再交付用户
8. **不要把半成品交付**：Worker 完成后必须确认代码已改好，不能凭 Worker 状态判断

### 绝对不能做
- 不能直接修改业务代码（这是 Builder 的工作）
- 不能关闭用户的终端
- 不能在 Worker 还没完成交接时关闭标签
- 不能凭猜测给建议
- 不能反复犯同一个错误

---

## 二、文档读取

### 启动时必须读取
1. `HANDOFF.md` — 上次交接的完整上下文
2. `CHANGELOG.md` — 已完成功能列表
3. `AGENTS.md` — 角色定义和治理规则
4. `docs/model/MODEL_ROUTING_REGISTRY.yaml` — 模型分工配置
5. `docs/roles/task-manager.md` — Task Manager 职责

### 派工前必须读取
1. 要修改的源文件（完整读取，不要只读片段）
2. 相关的 Windows 参考实现（如果是跨平台移植）
3. `docs/runtime/MAC_RUNTIME_CONFIG.json` — 运行时配置

### 不需要读取
- 不要为了保险重新扫描整个仓库
- 只有状态冲突/Handoff 失效/架构变化时才扩大读取范围

---

## 三、边界红线

### 禁止
- 业务源码 edit / Fix / Development
- raw Codex / OpenCode / terminal / worker-start
- OpenCode 业务 Subagent
- direct write-evidence
- direct state-writer
- self-approve Human Gate
- 修改 Model / Launch / Runtime / authoritative Governance
- 直接修改 production template
- 直接安装 Skill
- 关闭用户自己创建的终端
- 在 Worker 未完成交接时关闭标签
- 不查代码就给建议
- 不验证就交付

### 可以
- Dispatch Gateway（通过 `orca orchestration worker-start`）
- request-state-transition
- create-human-gate
- continuation-guard
- write-learning-event（只写事实事件）
- request-promotion（只提交 Candidate）
- read Promotion Result

---

## 四、治理流程

### 正确的工作流

```
用户反馈
  → Task Manager 记录、分类、排序
  → 读取相关代码，理解当前状态
  → 派 Builder 写代码（初级 luna / 高级 sol）
  → 等 Worker 完成（不要提前关标签）
  → 自己检查代码是否改好
  → 重建 App，kill 旧进程，启动新版本
  → 派 Code Reviewer 审查（luna medium）
  → 派 QA 测试（luna max）
  → 派 Product Reviewer 验收（muse-spark 免费）
  → 全部通过 → 交付用户
```

### 简单问题的简化流程

```
用户反馈
  → Task Manager 定位问题（读代码）
  → 一个 Builder 精确改一次
  → 自己验证（检查+构建）
  → 交付用户
```

**不要对简单 UI 修复走完整流程。**

### Continuation Contract

每个自治推进周期结束时必须输出：
```
current_goal
current_p0_status
next_action
stop_requested
stop_reason
blocking_evidence
```

P0 未完成且无合法 stop 条件：必须继续。

---

## 五、派工机制

### 模型分工（已写入 MODEL_ROUTING_REGISTRY.yaml）

| 角色 | 模型 | effort | 用途 |
|------|------|--------|------|
| Task Manager | opencode-go/mimo-v2.5 | — | 我自己 |
| 初级 Builder | codex/gpt-5.6-luna | medium | 简单代码修改 |
| 高级 Builder | codex/gpt-5.6-sol | medium | 复杂/高级开发 |
| Code Reviewer | codex/gpt-5.6-luna | medium | 代码审查 |
| QA | codex/gpt-5.6-luna | max | 测试验证 |
| Product Reviewer | opencode/muse-spark-1.2-contributor-free | — | 产品验收 |
| neat-freak | opencode/nemotron-3-ultra-free | — | 收尾整理 |

### 派工命令

```bash
# 创建任务
orca orchestration task-create --spec="任务描述" --task-title "标题" --json

# 派遣 Worker
orca orchestration worker-start --task <task_id> --agent codex --model <model> --effort <effort> --worktree current --json

# 检查 Worker 状态
orca orchestration worker-list --json

# 停止 Worker
orca orchestration worker-stop --dispatch <dispatch_id> --json

# 关闭 Worker 标签（只关派工的，不关用户的）
orca terminal close --terminal <handle> --json
```

### 派工注意事项
1. **派工前**：读代码，写精确指令（精确到行号和具体改什么）
2. **派工后**：等 Worker 完成，不要提前关标签
3. **完成后**：自己检查代码，确认改好了再交付
4. **不要反复派一次性临时工**：同一个问题修两次没解决就派常驻 TUI
5. **Terra 不可用**：codex/gpt-5.6-terra API 受限，暂用 Luna

---

## 六、具体流程

### 测试 QA 流程

```
1. 派 QA Worker（luna max）读取修改的文件
2. QA 逐项检查：
   - 代码是否正确实现需求
   - 构建是否通过（dotnet build）
   - 是否有遗漏的边界情况
3. QA 返回 PASS 或 FAIL + 具体发现
4. 如果 FAIL：派 Builder 修复，重新 QA
5. 如果 PASS：继续 Product Review
```

### 产品验收流程

```
1. 派 Product Reviewer（muse-spark 免费）读取修改的文件
2. 检查：
   - UI 是否符合设计要求
   - 功能是否完整
   - 用户体验是否合理
3. 返回 PASS 或 FAIL + 具体发现
4. 如果 FAIL：派 Builder 修复，重新验收
5. 如果 PASS：交付用户
```

### 部署流程

```
1. pkill -9 所有旧进程
2. 确认 0 残留
3. bash scripts/publish-macos.sh arm64
4. rm -rf /Applications/DeepSeekBalanceWidget.app
5. cp -R release/macos-arm64/DeepSeekBalanceWidget.app /Applications/
6. open /Applications/DeepSeekBalanceWidget.app
7. 确认新进程启动
```

### Git 提交流程

```
1. git add -A
2. git commit -m "描述性提交信息"
3. git push
```

---

## 七、本次工作时段的经验教训

### 教训 1：不要反复派一次性临时工
- 问题：同一个 UI 问题修了 5+ 轮都没解决
- 原因：每次派新 Worker，没有上下文，从零开始
- 解决：派常驻 TUI（保持终端开着），让 Worker 有完整上下文

### 教训 2：不要关闭用户终端
- 问题：关掉了用户的 Experience Recorder 终端
- 原因：清理时没有区分"派工终端"和"用户终端"
- 解决：只关 Worker 标签，绝对不动用户自己的

### 教训 3：先查再说
- 问题：给建议前没有查代码，建议不准确
- 原因：凭感觉给建议，没有确认实际情况
- 解决：给建议前必须先读代码、查文档、确认

### 教训 4：不要急着交付
- 问题：把半成品交付给用户，用户看到的是旧版本
- 原因：没有确认 Worker 是否真的完成了
- 解决：Worker 完成后必须自己检查代码，确认改好了再交付

### 教训 5：部署前彻底 kill
- 问题：用户一直看到旧版本
- 原因：旧进程没有被 kill 干净
- 解决：`pkill -9` 所有相关进程，确认 0 残留后再启动新版本

### 教训 6：简单问题不要复杂化
- 问题：简单 UI 修复走了完整 Builder→Reviewer→QA→Product 流程
- 原因：过度治理
- 解决：简单问题一个 Builder 精确改一次就够了

---

## 八、当前项目遗留任务

| 优先级 | 任务 | 说明 |
|--------|------|------|
| P1 | macOS OpenRouter 卡片 | 添加到 SettingsWindow |

**其余功能全部完成。**

---

## 九、关键文件索引

| 文件 | 说明 |
|------|------|
| `HANDOFF.md` | 交接文档（每次更新） |
| `CHANGELOG.md` | 已完成功能 |
| `AGENTS.md` | 角色定义 |
| `docs/model/MODEL_ROUTING_REGISTRY.yaml` | 模型分工 |
| `docs/roles/task-manager.md` | Task Manager 职责 |
| `src/DeepSeekBalanceWidget.Mac/MainWindow.axaml` | 胶囊布局 |
| `src/DeepSeekBalanceWidget.Mac/SettingsWindow.axaml` | 设置页 |
| `scripts/publish-macos.sh` | 打包脚本 |
| `scripts/runtime/macos/resolve-model` | 模型路由解析 |

---

## 十、最终交接

**项目状态**：17/18 功能完成，1 项待补（OpenRouter 卡片）
**构建状态**：0 Error(s)
**Git**：已提交并推送
**App**：已停止

**下一个 Task Manager 的第一步**：读取 HANDOFF.md，继续剩余任务。
