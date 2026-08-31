
# AI Model Guide｜V2.3.1

本文件只解释模型治理原则。

**动态模型事实与 Production Mapping 的机器权威只在：**

```text
MODEL_ROUTING_REGISTRY.yaml
```

## 原则

- Core Governance 不写死“某模型永远第一”。
- 模型先 Candidate，再 Qualification，再 Approval，再 Production。
- Task Manager 必须具备其角色所需能力，例如用户截图输入需要 Vision。
- 普通 Worker 能力足够时 Free-first。
- 专业开发可使用已批准 GPT Pro 资源池。
- GPT-5.6 Sol 保留为 Senior Expert Backend。
- Claude Code 当前不纳入。
- External Maintenance 可更新事实 / Candidate / 验证日期，但不能自动改变 Production Mapping。

## OpenCode Go

```text
默认仅 Task Manager
```

非 Task Manager 只有方案 B：

```text
Trusted User Approval Receipt
+ specific task_id
+ specific dispatch_id
+ one-shot
→ ALLOW ONCE
```

## 模型选择输入

Task Manager / Dispatch Gateway 应按：

```text
Role
Task Type
Required Capabilities
Risk
Vision
Coding
Tool Calling
Worker Mode
Resource Pool Policy
Qualification
Runtime Health
```

做选择，而不是按“模型名印象”临场拍脑袋。
