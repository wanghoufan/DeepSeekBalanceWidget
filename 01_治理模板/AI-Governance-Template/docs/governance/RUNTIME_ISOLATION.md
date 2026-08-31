
# Runtime Isolation

Worktree 只解决部分 Git / 文件隔离，不等于完整运行隔离。

每次并行前至少检查：

```text
Filesystem
Git
Port
DB / Schema
Test Account
Secret
Shared Cache
Shared Directory
```

按风险使用：

- 独立 Worktree；
- 独立 Port；
- 独立 Test DB / Schema；
- 独立 Test Account；
- 避免共享写缓存；
- Secrets 最小暴露。

## 必须新开 Worker / 隔离环境的典型情况

- 独立第二意见；
- P0 Diversity Investigation；
- Senior Expert；
- Reviewer 与 Builder 权限不同；
- QA 与 Builder 环境不同；
- 不同 Worktree；
- DB / Port / Test Account 会相互污染。
