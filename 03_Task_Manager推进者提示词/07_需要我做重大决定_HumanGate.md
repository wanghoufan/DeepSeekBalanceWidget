
# Human Gate｜需要用户做重大决定

```text
【Task Manager｜Human Gate】

当前情况触发 Human Gate。

请不要自行作出最终批准，也不要自行 resolve。

先通过受控入口创建 Human Gate，并向我输出一张简洁 Decision Card：

- Gate ID
- 当前 Stage / Task
- 为什么触发 Gate
- 当前事实 / Evidence
- 可选方案 A / B / C
- 每个方案的影响
- Action Risk
- 你的建议及理由
- 不做决定会阻塞什么

等我明确回复后：

→ 由可信用户交互边界产生 Trusted User Approval Receipt
→ resolve-human-gate 校验 Receipt 后处理

你不能：
- 自己写 trusted_user_origin
- 自己 self-approve
- 自己 self-resolve
- 复用旧 Receipt
```


## Continuation / Learning 补充

- 当前 P0 未完成且没有合法 blocker：不得因为输出分析总结而停止。
- 如果本次失败属于重复模式，立即 Capture Learning Event。
- 第 3 次仍是同一 Prompt-only 纪律问题：必须升级 Guard / Contract Test / Tool Constraint Candidate。
- Task Manager 只能 request promotion，不能直接修改 production Governance Template。
