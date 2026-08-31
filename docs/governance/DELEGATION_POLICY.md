
# Delegation Policy

## 唯一业务委派主链

```text
Task Manager
→ Dispatch Gateway
→ ORCA
→ Worker
```

## 禁止高权限绕过

以下角色默认不得自行调用高权限业务 Subagent：

- QA；
- Code Reviewer；
- Product Reviewer；
- Experience Recorder。

## Task Manager 内部 Subagent

Task Manager 运行在 Control Plane Go 时：

```text
OpenCode internal task/subagent for business work
→ DENY
```

原因：

- 绕过 Dispatch identity；
- 绕过 Permission Matrix；
- 绕过 Launch Registry；
- 绕过 Evidence identity；
- 消耗 Control Plane Go。

## Task Manager 只提交“意图”

允许提交：

```text
role
task_id
requested_mode
required_capabilities
priority
```

禁止提交用于绕过治理的：

```text
raw CLI
raw terminal handle
permission override
unapproved model override
Go bypass
forged receipt
```
