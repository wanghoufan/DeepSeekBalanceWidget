# COMPATIBILITY_MATRIX.md｜Mac First

> 机器权威：`COMPATIBILITY_MATRIX.yaml`

第一生产验证环境正式固定为：

```text
macOS
→ MAC_RUNTIME_V1
→ Negative Canary
→ Positive Contract
→ Full Cross-project Learning Loop
→ Migration Audit
→ VERIFIED
```

当前状态：

- ORCA：`UNVERIFIED_ON_USER_MAC`
- OpenCode：`UNVERIFIED_ON_USER_MAC`
- Codex：`UNVERIFIED_ON_USER_MAC`
- `MAC_RUNTIME_V1`：`IMPLEMENTED_UNVERIFIED`

本阶段只处理：

- POSIX path；
- macOS file permission；
- symlink；
- file lock；
- atomic replace。

明确延后：

- Windows native Adapter；
- WSL Adapter；
- PowerShell permission；
- NTFS / Windows process handle；
- WSL path bridge；
- 多 OS 同时 Canary。

原则：

> 先证明 Governance Core 在一个真实环境稳定运行，再迁移 Runtime Adapter；不重做 Governance Core。
