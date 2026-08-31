# Continuation Guard｜Trusted-State Final Contract

Task Manager 每轮可提交：

```text
current_goal
current_task_id
current_task_status
current_p0_status
next_action
stop_requested
stop_reason
```

但 `current_task_status / current_p0_status / stop_reason` 只是 request / explanation，不是权威事实。

Guard 必须独立读取：

```text
governance-state.yaml
task-runtime.json
EVIDENCE.jsonl
Human Gate trusted state
Fix Budget trusted state
runtime-health.json
```

## 合法自治 STOP

1. P0 真完成 + 当前承诺 Task terminal + 必要 Evidence PASS；
2. Human Gate；
3. 无 VERIFIED fallback 的外部硬阻塞；
4. Fix Budget exhausted；
5. Safety Policy 阻断。

否则：

```text
P0 / Current Task incomplete
+
no trusted blocker
+
Task Manager asks to stop
→ CONTINUATION_VIOLATION
```

## 用户主动暂停工作时段

这是外部用户控制，不是 Task Manager 自行停。

可信用户边界将 `trusted_user_session_pause=true` 写入 trusted runtime projection，Guard 返回：

```text
SUSPEND_ALLOWED
requires_handoff=true
```

Task Manager 无权伪造。
