
# Model Qualification Harness

模型不能因为官方 Benchmark 或宣传就直接进入 Production。

## 推荐固定历史任务集

第一版至少 30 个真实历史案例：

```text
10 个 P0 / P1 分类
5 个 Screenshot Feedback
5 个 Task Routing
5 个 Project Recovery
5 个 Conflict Resolution
```

按角色需要再增加：

- Coding；
- Fix；
- Review；
- Vision / GUI；
- Tool Calling；
- Long-context recovery。

## 评分维度

- 正确率；
- 关键漏项；
- P0 误判 / 漏判；
- 路由合理性；
- 恢复能力；
- Coding / Debug；
- Vision；
- Tool Calling；
- 时延；
- 成本；
- Rate Limit；
- 上下文连续性；
- 输出可执行性。

## 生命周期

```text
CANDIDATE
↓
历史任务 Harness
↓
QUALIFIED
↓
User / Formal Governance Approval
↓
PRODUCTION
```

External Maintenance 可以维护事实与测试结果。

Production Mapping：

> 不允许因为兼容维护、新模型发布或一次跑分自动改变。
