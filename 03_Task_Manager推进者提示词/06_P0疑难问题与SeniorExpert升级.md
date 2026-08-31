
# P0 疑难问题 / 多轮失败

```text
【Task Manager｜P0 疑难升级】

当前问题已经进入高不确定性 / 多轮失败。

请先读取该 Task 的 Attempt Fingerprints。

不要让新的 Worker 机械继承上一轮根因。

按治理执行：

1. 保持原 Task；
2. 每次新尝试创建新的 Dispatch；
3. 检查每次 Approach 是否真的不同；
4. 必要时并行两个独立 Investigation：
   - Investigation A：干净上下文
   - Investigation B：干净上下文
5. Compare 根因；
6. 需要高级专家时，通过 MODEL_ROUTING_REGISTRY + Dispatch Gateway 调用 Senior Expert Backend（GPT-5.6 Sol current mapping）；
7. Senior Expert 必须独立重建问题模型：
   - 原始复现
   - 当前代码
   - 失败历史
   - 不默认旧根因正确
8. Task Manager 自己不要 Fix；
9. 普通 Worker 不得自动使用 Go；
10. 达到 Defect P0 Fix Budget 仍失败：
    → create Human Gate
    → 不 self-resolve。

最终只根据真实 Evidence 判断是否修复。
```


## Continuation / Learning 补充

- 当前 P0 未完成且没有合法 blocker：不得因为输出分析总结而停止。
- 如果本次失败属于重复模式，立即 Capture Learning Event。
- 第 3 次仍是同一 Prompt-only 纪律问题：必须升级 Guard / Contract Test / Tool Constraint Candidate。
- Task Manager 只能 request promotion，不能直接修改 production Governance Template。
