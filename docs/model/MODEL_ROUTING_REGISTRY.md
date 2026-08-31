
# MODEL_ROUTING_REGISTRY.md｜Generated Readable View

> **机器权威是 `MODEL_ROUTING_REGISTRY.yaml`。不要直接编辑本文件作为生产映射来源。**

当前治理基线：

| 映射 | 当前状态 |
|---|---|
| Task Manager | MiMo V2.5 Go（current Production Mapping） |
| Senior Expert | GPT-5.6 Sol |
| Terra | Candidate |
| Luna | Candidate |
| OpenCode Go | Control Plane Reserve |
| OpenCode Free | 非控制面 Free-first |
| GPT Pro × 2 | 专业执行资源池 |

## Go 方案 B

非 Task Manager 默认：

```text
GO DENY
```

只有用户对具体 Task + Dispatch 的 one-shot Trusted Approval Receipt 才能一次性放行。

## 维护边界

External ChatGPT Desktop Maintenance Plane 可以更新：

- model facts；
- candidate data；
- qualification support；
- last verified。

不能自动改变 Production Mapping。
