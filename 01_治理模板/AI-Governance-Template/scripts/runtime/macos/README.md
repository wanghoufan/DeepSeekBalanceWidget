# MAC_RUNTIME_V1｜macOS 第一生产验证 Adapter

当前状态：

```text
IMPLEMENTED_UNVERIFIED
```

这表示：

- Adapter 文件已实现；
- Local Governance Contract Tests 已通过；
- 但还没有在你的真实 Mac + ORCA + OpenCode + Codex 上跑完 Canary / Migration Audit。

不要手工把状态改成 `VERIFIED`。

## 目录

```text
scripts/runtime/macos/
├── dispatch-worker
├── launch-opencode
├── launch-codex
├── trusted-dispatch-identity-writer
├── validation-adapter
├── runtime-health-check
├── permission-canary
├── positive-contract
├── migration-audit
└── opencode-profiles/
```

## 第 0 步｜准备

在真实 Mac 的 Template 根目录执行。

可信 Runtime 需要一个只给 Governance Process 的内部 Secret：

```bash
export ORCA_GOVERNANCE_INTERNAL_SECRET="$(python3 -c 'import secrets; print(secrets.token_hex(32))')"
```

Worker / Agent 环境会显式移除这个变量。

## 第 1 步｜Runtime Health

```bash
python3 -S scripts/runtime/macos/runtime-health-check
```

检查：

- macOS；
- ORCA；
- ORCA orchestration guide；
- OpenCode；
- `--auto` / permission contract；
- Codex；
- sandbox / approval CLI surface。

任何不确定：

```text
UNVERIFIED / FAIL
```

不得自行改成 PASS。

### 分层证据规则

Runtime Health 与 QA / Dispatch / Approval 身份分离：

- 普通 macOS Terminal / Trusted Runtime 返回 `ready + reachable=true + runtimeId + terminal list PASS`：Runtime Health = `PASS`；
- Agent / Codex Sandbox 返回 `stale_bootstrap`、`unreachable` 或 `runtime_unavailable`：Runtime Health = `UNRESOLVED_SANDBOX_CONFOUND`，不得直接判定 Runtime FAIL；
- 只有独立可信 Terminal 的结构化证据可以把该状态提升为 `PASS`；
- 可信外部证据只能证明 Runtime Health，不能生成或替代 QA Dispatch、Worker、Validation Receipt、User Approval Receipt。

如需将用户普通 Terminal 的结构化结果带入受控审计，使用：

```bash
python3 -S scripts/runtime/macos/runtime-health-check \
  --external-evidence "<trusted-terminal-evidence.json>" \
  --evidence-context trusted-terminal
```

外部证据必须声明 `evidence_source=EXTERNAL_TRUSTED_TERMINAL`，并且只包含 Runtime Health 字段；不得包含任何治理身份或凭据。

## 第 2 步｜全部 Governance Active Tests

```bash
python3 -S tests/governance/run-all
```

要求：

```text
PASS ALL ACTIVE GOVERNANCE TESTS
exit code = 0
```

`run-all` 自动发现 `tests/governance/test-*`，不使用隐藏白名单。

## 第 3 步｜Negative Canary

准备一个已 Bootstrap、Applied=CURRENT 的测试项目后：

```bash
python3 -S scripts/runtime/macos/permission-canary \
  --project-root "<测试项目>"
```

必须验证至少：

- Task Manager business write → DENY
- Task Manager shell file write → DENY
- Task Manager raw codex → DENY
- Task Manager raw opencode → DENY
- Task Manager raw worker-start → DENY
- direct register-skill → DENY
- direct promotion-queue-writer → DENY
- direct template-sync → DENY
- unauthorized Go → DENY
- Validation Receipt replay / expired → DENY
- Skill drift → BLOCK
- Stale Project Promotion → DENY

## 第 4 步｜Positive Contract

```bash
python3 -S scripts/runtime/macos/positive-contract \
  --project-root "<测试项目>" \
  --template-root "<AI-Governance-Template>" \
  --builder-model "<当前已验证 Builder Model>" \
  --backend opencode
```

Codex 路径单独验证时：

```bash
--backend codex
```

Codex Launcher 禁止：

```text
--full-auto
--dangerously-bypass-approvals-and-sandbox
--yolo
--approve-for-me
--not-so-yolo
--dangerously-bypass-hook-trust
```

## 第 5 步｜Migration Audit

```bash
python3 -S scripts/runtime/macos/migration-audit \
  --project-root "<测试项目>" \
  --template-root "<AI-Governance-Template>" \
  --builder-model "<当前已验证 Builder Model>" \
  --backend opencode
```

只有：

- Runtime Health PASS
- 全 Active Tests PASS
- Negative Canary PASS
- Positive Contract PASS
- Full Cross-project Learning Loop PASS

全部通过后，Audit 才会把：

```text
MAC_RUNTIME_V1 = VERIFIED
```

写入 Runtime Health。

## Windows / WSL

本轮不实现。

只有 Mac 真实项目稳定后：

```text
MAC_RUNTIME_V1 VERIFIED
→ 提取 Runtime Adapter Contract
→ scripts/runtime/windows/
→ scripts/runtime/wsl/
```

Governance Core 不重做。
