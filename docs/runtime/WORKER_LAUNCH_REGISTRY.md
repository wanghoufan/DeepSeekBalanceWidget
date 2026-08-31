# WORKER_LAUNCH_REGISTRY.md｜Mac First

> 机器权威：`WORKER_LAUNCH_REGISTRY.yaml`

当前第一阶段只维护：

```text
mac_opencode  UNVERIFIED
mac_codex     UNVERIFIED
```

生产要求：

```text
status = VERIFIED
+
fresh installed CLI version
+
permission / launcher contract test
+
Negative Canary
```

Codex VERIFIED Launcher 必须显式指定：

```text
sandbox
approval policy
```

并禁止危险 / full-autonomy bypass 参数进入 VERIFIED Profile。

Windows / WSL Launcher 本轮不实现，等 `MAC_RUNTIME_V1 = VERIFIED` 后再按同一 Runtime Adapter Contract 分别实现。
