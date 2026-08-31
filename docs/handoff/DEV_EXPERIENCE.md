# 开发经验记录｜DeepSeek Balance Widget

> 唯一可写权威经验沉淀文档。仅 Experience Recorder 可写，其他文档不得代改。代码不改、只记经验。

- 项目：DeepSeek Balance Widget（DeepSeek API 余额 + ChatGPT Plus 用量 + OpenCode Go 额度 桌面悬浮小工具，Windows WPF / macOS Avalonia 共用 Models/Services）
- 初始化时间：2026-08-31
- 已读取：`AGENTS.md:1`、`README.md:1`、`docs/pm/PLAN.md:1`、`docs/experience/DEV_EXPERIENCE.template.md:1`、`docs/handoff/HANDOFF.md:1`
- 状态：`docs/handoff/DEV_EXPERIENCE.md` 新建，尚无 E-xxx 条目，等待首条反馈落盘
- 语音纠错词库：DeepSeek / ChatGPT Plus / GPT / OpenCode Go / OpenCode / OC / WorkBuddy / WB / WPF / Avalonia / .NET 8 / DPAPI / 钥匙串 / Keychain / csproj / Info.plist / CFBundleShortVersionString / CFBundleVersion / CHANGELOG / git tag v* / release / DeepSeekBalanceWidget.exe / DeepSeekBalanceWidget.app / publish.ps1 / publish-macos.sh / Models / Services / ORCA / Task Manager / Builder / Code Reviewer / QA / Experience Recorder / neat-freak

## 记录规范
每条 `E-xxx` 含：小白解释、问题/现象、为什么是问题、原因/背景（含源码或文档核对结果）、本次纠正、以后怎么避免、下次可直接对 Agent 说的话、技术处理（只描述不执行）、适用范围。标题含日期，编号递增。面向编程初学者，术语首次出现附解释。

---

---

### E-001｜2026-08-31｜节奏推进者（Task Manager）启动后记忆被冲掉、忘记可派工

**1. 小白解释**
> 节奏推进者（Task Manager）就像工地的总调度，只负责派活、不自己搬砖。工具启动时他还记得「我能派 Planner/Builder/QA 等 8 个角色去干活」，但聊几轮后就像被洗了脑，突然以为自己是个普通干活的工人，忘记了调度能力。结果用户还得手动提醒他「你再去把治理规范读一遍」，浪费时间。

**2. 问题 / 现象**
- Task Manager 刚启动时已读取 `docs/roles/task-manager.md:1` 等记忆，知道自己是 `Dispatch-only Control Plane`（只派工的控制面）。
- 对话几轮后上下文被冲掉，出现失忆：忘记自己能通过模型去派工（经由 Dispatch Gateway → ORCA → Worker），反而认为自己不能调用产品/测试/QA，或直接想自己改代码。
- 用户被迫用图1 的三问来唤醒：「你应该做的工作 / 你所具备的权限（能调用什么角色）/ 每个角色分别对应的模型和调用命令」，AI 才重新 `Read docs/roles/task-manager.md` + `Read 01_治理模板/AI-Governance-Template/AGENTS.md` 并纠正。

**3. 为什么这是问题**
- 调度失忆直接导致流程停摆：本该自动 Dispatch 的任务无人派发，用户要反复做「记忆唤醒」的人工干预。
- 增加 Token/时间成本，且违背治理设计：Task Manager 本就不该写代码/写 Evidence/改 State（见 `docs/roles/task-manager.md:94` 禁止列表），失忆后反而容易越权。
- 信任受损：用户无法确定调度是否在按规范推进，必须每隔几轮就拷问身份。

**4. 原因 / 背景（含源码或文档核对结果）**
- 文档核对：
  - `docs/roles/task-manager.md:5` 明确核心使命 `接收用户反馈 → 记录 → 分类 → 排序 → Dispatch → Evidence 消费 → next_action`，禁止直接改代码/写 Evidence/改 State。
  - `docs/governance/DELEGATION_POLICY.md:6` 规定唯一业务委派主链 `Task Manager → Dispatch Gateway → ORCA → Worker`，Task Manager 只提交 role/task_id/mode 等意图，不提交 raw CLI。
  - `docs/model/MODEL_ROUTING_REGISTRY.md:10` 生产映射为 `Task Manager = MiMo V2.5 Go`，其他 Worker 为 Sol/Terra 等，说明「调用模型去派工」是设计能力。
  - `docs/handoff/HANDOFF.md:1` 本应作为暂停/恢复胶囊保存 `Current Dispatch / Next Single Action`，但未被 Task Manager 每轮强制重读。
- 根因：目前没有「防遗忘的硬机制」保证长对话中记忆持续生效，仅靠启动时读一次文件，靠 LLM 上下文记忆，随着对话变长被挤出窗口后即失效。

**5. 本次纠正**
- 用户主动打断并要求重新读取治理规范流程，按 1/2/3 清单让 AI 自检：该做的事、权限边界、角色-模型-调用命令映射。AI 重新读取后承认定位为 Dispatch-only 并列出 8 个可调用角色。

**6. 以后怎么避免**
- 把「调度身份 + 可派工能力」从软记忆升级为硬约束：每次用户反馈/每轮结束强制重读 `HANDOFF.md + task-manager.md` 关键段落，或在系统提示词（System Prompt）中固化调度身份。
- 在每轮输出中强制包含 `next_action` 与 `Current Dispatch` 字段，倒逼模型回忆派工职责。
- 用机器校验替代口头承诺：Continuation Guard 只信 machine state（`docs/roles/task-manager.md:110`），若 Task Manager 未产生 Dispatch 意图即判为停滞，需告警而非自行退出。

**7. 下次可以直接对 Agent 说的话（一句可直接复制的派活话术）**
> 「你是 Dispatch-only 的 Task Manager，请先重读 docs/roles/task-manager.md、docs/governance/DELEGATION_POLICY.md、docs/model/MODEL_ROUTING_REGISTRY.md，再按『该做的事 / 能调用的角色权限 / 角色-模型-调用命令』三段表自检并 Dispatch，不要自己改代码。」

**8. 技术处理（给开发者的准确方案，只描述不执行）**
- 在 Task Manager 的 System Prompt / 启动模板中常驻声明：`定位=Dispatch-only Control Plane，禁止直接改代码/写 Evidence/改 State，唯一出口=Dispatch Gateway → ORCA → Worker`，并列出 8 角色与模型映射表。
- 增加启动与每轮的 Read 钩子：强制 `Read docs/handoff/HANDOFF.md:8` + `Read docs/roles/task-manager.md:16` 当前 P0/Goal/Next Action，再执行分类排序。
- 增加防遗忘巡检：若连续 N 轮无 Dispatch 意图或 next_action 为空，触发 `governance-sync` + 自动重读提醒，而非等待用户纠正。
- 将 `docs/handoff/HANDOFF.md:27 Next Single Action` 与 `docs/progress/governance-state.yaml` 的 machine state 作为 Continuation 唯一判据写入提示，避免 LLM 自报状态替代机器状态。代码一律不改，仅记录方案。

**9. 适用范围**
- 本项目所有长对话的 Task Manager 调度场景；复用到任何采用 ORCA Delivery V1.10 + Dispatch Gateway + 多模型路由（MiMo/ Sol/ Terra 等）的 Go 控制面项目。归类：Workflow Improvement + Reusable Rule。

- Evidence：用户截图 Image 1（提问-重读-纠正过程，Thought 2.2s/4.0s 两次 Read）
- Status：Observed → Candidate（待升级为通用项目模板经验，见复盘分类 C）

### E-002｜2026-08-31｜角色-模型分工仅口头梳理未落盘、治理无保证导致测试验收环节落空

**1. 小白解释**
> 好比公司口头说好「小张做开发、小李做测试、小王做产品验收」，但没写进制度文件。时间一长，调度就忘了该派谁，甚至直接让开发说「我做完了，你来审一下」就结束。`MODEL_ROUTING_REGISTRY`（模型路由注册表，记录哪个角色该用哪个 AI 模型）就是那份「制度文件」，没写入就等于没有依据，测试和验收环节自然就没人执行了。

**2. 问题 / 现象**
- 图片1 的「完整分工确认」表（Task Manager=mimo-v2.5、初级 Builder=terra、QA=luna、Product Reviewer=muse-spark 等 8 角色）当时只是临时梳理、口头确认，没有写入任何治理文件。
- 用户追问：「目前这个文件是写入在哪里？写在治理文件的哪个文档里？后续你怎么确保你是参考这个来调用的？」AI 回答：`当前这个模型分工没有写入任何治理文件，只是口头约定。每次启动靠读 AGENTS.md 和 MODEL_ROUTING_REGISTRY.yaml 来参考，但里面没有记录角色对应的模型分工。`
- 后果：Task Manager 既无依据也未按流程执行，`Builder → Code Reviewer → QA → Product Reviewer` 的应有链路全部落空，现状退化为「开发者做完直接让用户审核」，错误且无验收保障。

**3. 为什么这是问题**
- 治理真空：口头约定随上下文丢失而失效（与 E-001 记忆冲掉同根），无法被 `Dispatch Gateway` 或机器校验读取，等同于无规范。
- 质量风险：缺少独立的 Code Review / QA / Product Review，缺陷和产品偏离无法被发现，违背 `docs/workflow/WORKFLOW.md:4` 的 `Worker → Evidence → Task Manager 判断 next_action → Continuation Guard` 主链。
- 权责混乱：开发者自称完成即视为完成，绕过了 `docs/roles/qa.md:19` 禁止 Builder 绕过 Task Manager 调 QA 的约束，也绕过了权限矩阵。

**4. 原因 / 背景（含源码或文档核对结果）**
- 文档核对（2026-08-31 现场）：
  - `docs/model/MODEL_ROUTING_REGISTRY.yaml:1` 为权威源（authoritative: true, source_of_truth 指向自身），结构含 `role_pool_policy` 与 `current_production_mappings`，本应承载角色-模型映射。
  - 反馈发生时，按 AI 自述该文件内「没有记录角色对应的模型分工」，`docs/model/MODEL_ROUTING_REGISTRY.md:7` 可读视图也仅显示池子概览，未列出 8 角色的完整分工表。
  - `docs/workflow/WORKFLOW.md:1` 与 `docs/roles/task-manager.md:5` 都要求 Task Manager 经 Dispatch 派工并消费 Evidence，但无持久化映射就无法稳定 Dispatch 到正确模型/角色。
  - 矛盾记录：本次核对时 `docs/model/MODEL_ROUTING_REGISTRY.yaml:66` 已出现 `current_production_mappings`（task-manager=mimo-v2.5、builder-junior/senior、code-reviewer、qa、product-reviewer 等），说明在用户纠正后已补写入；但反馈时点该写入尚未发生，治理保证在当时确实缺失。【待确认：补写时间是否为本次对话后人工写入，需用户确认】
- 根因：分工梳理未纳入治理文件的「写入 → 提交 → 生效」闭环，缺乏 `write → request-promotion → Applied Sync` 的强制路径。

**5. 本次纠正**
- 用户指出「临时梳理无文件依据、无治理保证、流程未执行、验收落空」的系统性错误；AI 承认应写入 `MODEL_ROUTING_REGISTRY.yaml`，并指出最合适位置是利用其已有的 `role_pool_policy` 和 `current_production_mappings` 字段持久化。

**6. 以后怎么避免**
- 任何角色-模型分工变更必须当天落盘到 `docs/model/MODEL_ROUTING_REGISTRY.yaml` 的 `current_production_mappings`，并通过 `docs/governance/PROJECT_GOVERNANCE_APPLIED.yaml:1` 的指纹同步，使 Dispatch 有机器可读依据。
- 将「无映射不 Dispatch」作为硬门槛：Task Manager 发现映射缺失即先 `write-learning-event` + 补文件再派工，而非口头派工。
- 每轮 Dispatch 前重读 `MODEL_ROUTING_REGISTRY.yaml` 并在日志中打印 `role → model_ref → effort`，让用户可见可审计。

**7. 下次可以直接对 Agent 说的话（一句可直接复制的派活话术）**
> 「分工必须落盘到 docs/model/MODEL_ROUTING_REGISTRY.yaml 的 current_production_mappings，不要口头约定；先 Read 该文件确认 8 角色映射存在，再按映射经 Dispatch Gateway → ORCA 依次派 Builder/Code Reviewer/QA/Product Reviewer，缺映射先补文件再派工。」

**8. 技术处理（给开发者的准确方案，只描述不执行）**
- 在 `docs/model/MODEL_ROUTING_REGISTRY.yaml:66` 的 `current_production_mappings` 中固化 8 角色条目（task-manager/ builder-junior/ builder-senior/ code-reviewer/ qa/ product-reviewer/ experience-recorder/ neat-freak 各自 model_ref 与 effort），与图片1 分工表一致；并更新 `docs/model/MODEL_ROUTING_REGISTRY.md` 可读视图。
- 为 `MODEL_ROUTING_REGISTRY.yaml` 增加 Contract Test：校验每个 `role_pool_policy` 中的角色均在 `current_production_mappings` 有对应且 model_ref 合法，无映射则 CI 失败。
- 在 Task Manager 启动模板中增加 Read 钩子：`Read MODEL_ROUTING_REGISTRY.yaml` 并校验通过后才允许 Dispatch；同时将映射打印到 `HANDOFF.md` 的 Relevant Files 区域以便恢复。代码不改，仅记录方案。

**9. 适用范围**
- 本项目及所有需多角色多模型协同的 ORCA 治理项目；尤其是 `Dispatch Gateway` 需按角色选模型的场景。归类：Reusable Rule + Workflow Improvement（治理文件持久化缺失类）。

- Evidence：用户截图 Image 1 第二次追问（口头约定无文件、需写入 MODEL_ROUTING_REGISTRY.yaml）+ AI 回答原文 + `docs/model/MODEL_ROUTING_REGISTRY.yaml:66` 现场已补映射的矛盾点
- Status：Observed → Candidate（待复盘归入 C 类通用模板，需确认补写入是否已通过 Promotion 同步）

### E-003｜2026-08-31｜单模型不可用（codex/gpt-5.6-terra）不应中断流程、需容灾降级与调用方式复核

**1. 小白解释**
> `codex/gpt-5.6-terra`（Terra 模型，GPT 5.6 系列的其中一个版本）是分工表中初级 Builder 和 Code Reviewer 计划用的模型。`Codex` 是官方提供的命令行工具，用来调用 GPT 模型。有一天 Task Manager 说「这个模型在你的 Codex 账户上不可用」，如果没有备用方案，整个开发流水线就会卡死。但正确做法是：先排查是不是调用方式写错了，同时有备用模型顶上，让流程继续走，而不是直接中断。

**2. 问题 / 现象**
- Task Manager 反馈：`codex/gpt-5.6-terra 在你的 Codex 账户上不可用`，并执行排查命令：`codex --help | grep model`、`cat ~/.codex/config.toml | grep model`，发现本地 `model = "gpt-5.6-luna"`，可用列表仅 `gpt-5.6-luna（当前默认）` 与 `gpt-5.6-sol`，进而询问「初级 Builder 和 Code Reviewer 改用什么模型？」
- 用户纠正：不应因此中断流程；Terra 不可用只是后续排查点，不能因为分工表里有它就让开发流程中断或异常。并质疑判断依据：同为 GPT 模型，Sol/Luna 可调用则 Terra 理论上也可调用，要求再次检查调用方式是否正确（`cat ~/.codex/config.toml` 与 `--help` 仅是配置查看，未做真实 `codex -m terra` 拉起验证）。
- 现场 registry 已做临时降级：`docs/model/MODEL_ROUTING_REGISTRY.yaml:71` 将 `builder-junior: gpt-5.6-luna (Terra API不可用，暂用Luna)`、`code-reviewer: gpt-5.6-luna (Terra API不可用，暂用Luna)`，说明已用 Luna 代替 Terra，但根因未闭环。

**3. 为什么这是问题**
- 单点故障放大：一个模型不可用就让分工表失效，违背容灾原则；本应是 `RUNTIME_INCIDENT`（运行时偶发问题）级别，却被当成流程终止条件。
- 归因草率：仅凭 `config.toml` 的默认值和 `--help` 文案就判定「账户不支持 Terra」，未做真实模型拉起测试，可能误判为账户权限问题而掩盖「调用参数写法错误」。
- 交付风险：若无明确降级规则，Builder 与 Reviewer 派工会被阻塞，进而导致 `E-002` 所述的验收落空链条再次断裂。

**4. 原因 / 背景（含源码或文档核对结果）**
- 文档核对：
  - `docs/model/MODEL_ROUTING_REGISTRY.yaml:99` 已定义 4 个模型均为 `status: PRODUCTION`（mimo-v2.5、sol、luna、muse-spark/nemotron），原计划的 `terra` 在调查报告中为 `CANDIDATE` 未进入 Production（见 `docs/progress/INVESTIGATION_BUILDER_MODEL_ROOT_CAUSE_20260830.md:65`），说明 Terra 本就存在资格落差。
  - `docs/model/MODEL_QUALIFICATION.md:1` 规定模型需经 30+ 历史任务 Harness 才能从 CANDIDATE 升为 QUALIFIED/PRODUCTION，不允许仅因宣传就直接上生产；Terra 未完成该流程，生产可用性本就待验证。
  - `docs/model/MODEL_ROUTING_REGISTRY.yaml:29` 的 `role_pool_policy` 允许 builder/code-reviewer 使用 `OPENCODE_FREE + GPT_PRO` 双池，意味着 Luna/Sol 同池可作为 Terra 的同级替代，具备降级条件。
  - 排查方法核对：图片中仅 `grep model` 查看配置，未执行 `codex -c model="g-5.6-terra"` 真实调用或查看 `scripts/runtime/macos/dispatch-worker` 的 model 转发逻辑，无法区分「账户不支持」与「调用方式错误」。
- 根因：分工表把 CANDIDATE 模型（Terra）写进了生产分工，却未配套「不可用 → 自动降级」与「调用方式复核」两步保障。

**5. 本次纠正**
- 用户明确：Terra 不可用列为后续排查点，流程不得中断；要求重新检查调用方式是否正确，不能因个别模型缺失就异常。Task Manager 需在分工不变的前提下先用可用模型继续推进。

**6. 以后怎么避免**
- 原则：`单模型不可用 ≠ 流程中断`。任何角色-模型映射必须配 `primary + fallback`（例如：初级 Builder primary=terra, fallback=luna）。
- 排查规范：判定模型不可用前必须完成两步：① 真实拉起测试（按 Dispatch 实际传参方式试调用）② 查看 `dispatch-worker`/`~/.codex/config.toml` 的 model 字段写法是否与 registry 一致。仅 `grep --help` 不算证据。
- 将模型不可用登记为 `RUNTIME_INCIDENT` 类 Learning Event，进入 `External Maintenance Plane` 排查队列，不在当前 P0 流程中阻塞交付。

**7. 下次可以直接对 Agent 说的话（一句可直接复制的派活话术）**
> 「codex/gpt-5.6-terra 若不可用先按 fallback 派工（初级Builder/Code Reviewer 降级到 codex/gpt-5.6-luna），不要中断流程；同时按真实 Dispatch 传参复核一次 Terra 调用方式并登记为 RUNTIME_INCIDENT 后续排查，不要仅凭 config.toml 默认值就判定账户不支持。」

**8. 技术处理（给开发者的准确方案，只描述不执行）**
- 在 `docs/model/MODEL_ROUTING_REGISTRY.yaml:66` 的 `current_production_mappings` 中为每个依赖 terra 的角色增加 `fallback_model_ref: codex/gpt-5.6-luna`（或 sol），并在 `docs/model/MODEL_QUALIFICATION.md:58` 的 Production Mapping 约束下保持 `fallback` 仅为同池已 PRODUCTION 模型。
- 为 `scripts/runtime/macos/dispatch-worker` 增加模型可用性探针：Dispatch 前先试 `codex --help` 与 `cat ~/.codex/config.toml` 仅作提示，实际以 `codex` 进程拉起返回为准；失败则自动切 fallback 并写入 `RUNTIME_INCIDENT` 日志，不抛异常中断主链。
- 在 Harness 中补充 Terra 的 30 例历史任务 qualification，若通过则重新 Promote 为 PRODUCTION，再切回 primary。代码不改，仅记录方案。

**9. 适用范围**
- 本项目所有依赖 `codex/gpt-*` 的 Builder/Reviewer 派工；复用到任何多模型路由（Sol/Luna/Terra 互为备份）且需高可用调度的 ORCA 项目。归类：Runtime Compatibility + Model Routing + Workflow Improvement。

- Evidence：用户截图 Image 1（发现 terra 不可用 → `codex --help | grep model` + `cat ~/.codex/config.toml` → 可用列表仅 luna/sol → 用户追问调用方式）+ `docs/model/MODEL_ROUTING_REGISTRY.yaml:71` 临时降级备注
- Status：Observed → Candidate（模型可用性误判与容灾缺失类，待 External Maintenance 闭环 Terra 真实可用性）

### E-004｜2026-08-31｜派工无机器强制、缺少 resolve-model 解析器导致模型错配风险

**1. 小白解释**
> `dispatch-worker`（派工网关脚本，负责把 Task Manager 的派工请求转发给 ORCA/Codex/OpenCode）目前只有一条硬拦截：「非 Task Manager 不能用 opencode-go/ 模型」。但它不会自动去读 `MODEL_ROUTING_REGISTRY.yaml`（模型路由注册表）来帮 Task Manager 挑对模型，也不会校验「这个角色该不该用这个模型」。于是全靠 Task Manager 自觉去读文件，属于「自觉参考」而非「机器强制」，一自觉就容易用错模型。

**2. 问题 / 现象**
- AI 自检代码后承认：`目前没有自动执行机制。查了代码：1. dispatch-worker 只有一条硬守卫：非 Task Manager 不能用 opencode-go/ 2. 没有读取 MODEL_ROUTING_REGISTRY.yaml 的 resolver 3. 没有校验 role→model 是否匹配 Registry 的逻辑`
- 现状是：`我每次启动靠读 AGENTS.md + MODEL_ROUTING_REGISTRY.yaml 来自觉参考，但没有机器强制。`
- 提出的补建方案：`Task Manager 派工时 → 调用 resolve-model 脚本 → 传入 role + routing_profile → 脚本读取 MODEL_ROUTING_REGISTRY.yaml → 返回该 role 对应的 model + effort → 自动填入 worker-start 命令`，并询问是否现在派 Builder 补建该机制。
- 用户批示：`可以的，把这个都补上。补完以后，推进刚刚没有解决的问题：置顶不生效、还有进度条的问题`。说明机制缺失已被用户确认为前置阻塞。

**3. 为什么这是问题**
- 自觉不可靠：口头/自觉参考会随记忆冲掉而失效（联动 E-001/E-002），Equal 为无制度保证；一旦用错模型（如把 Task Manager 的 mimo-v2.5 传给 Builder，见 `docs/progress/INVESTIGATION_BUILDER_MODEL_ROOT_CAUSE_20260830.md:128` 的根因链路），`dispatch-worker:34` 仅对 Go 模型拦截，其他错配（如 builder 用错 luna/sol/terra）完全放行。
- 隐性成本：用错模型导致 Token 消耗、能力不匹配（如用 Free 模型做需 Pro 的 Review）、甚至被网关 DENY 后重试，浪费来回沟通。
- 阻塞后续 P0：置顶/进度条等业务问题因派工机制不可信而无法稳定进入 Builder→Reviewer→QA 链路。

**4. 原因 / 背景（含源码或文档核对结果）**
- 源码核对（2026-08-31 现场）：
  - `scripts/runtime/macos/dispatch-worker:34` 仅有：`if role!='task-manager' and backend=='opencode' and str(model or '').startswith('opencode-go/'): DENY`，确无其他校验。
  - `scripts/runtime/macos/dispatch-worker:44` 仅有 `resolve-skills`（Skill 解析器），无 `resolve-model`；全仓 `glob **/resolve-model*` 结果为空，机制确实缺失。
  - `scripts/runtime/macos/dispatch-worker:30` 直接取 `model=req.get('model')`，即调用方传什么就用什么，不查询 Registry；`docs/progress/INVESTIGATION_BUILDER_MODEL_ROOT_CAUSE_20260830.md:99` 已指出 `positive-contract --builder-model` 为透传无校验，错误模型（如 mimo-v2.5 给 builder）可直达网关才被拦截。
  - `docs/model/MODEL_ROUTING_REGISTRY.yaml:1` 的 `role_pool_policy` 与 `current_production_mappings` 已具备机器可读的「角色→模型」映射，但无消费者（resolver）使用，属于「有制度文件、无执行器」。
- 根因：治理设计了注册表但未配套「解析器 + 网关强制校验」闭环，派工链路缺少 `Registry → Resolver → Gateway 强校验` 一跳。

**5. 本次纠正**
- 用户确认补建 `resolve-model` 机制：由 TM 派工时调用脚本，传入 role，脚本读 Registry 返回 model+effort 并自动填入 worker 启动命令，从而不会用错模型。

**6. 以后怎么避免**
- 把 `resolve-model` 作为 Dispatch Gateway 的必经步骤：无 resolver 返回不允许生成 `worker-start` 命令。
- 为 `MODEL_ROUTING_REGISTRY.yaml` 的每个 `role_pool_policy` 条目增加「禁止手写 model」约束：Task Manager 禁止直接在请求中写 raw model 字符串，必须经 resolver 解析。
- 在 PR/CI 中增加校验：若 `dispatch-worker` 未调用 resolver 或未校验 `role→model` 归属池子，则阻断合并。

**7. 下次可以直接对 Agent 说的话（一句可直接复制的派活话术）**
> 「先补建 scripts/runtime/macos/resolve-model：传入 role 后读取 docs/model/MODEL_ROUTING_REGISTRY.yaml 返回 model+effort，并让 dispatch-worker 在派工时强制调用它校验 role→model 归属，再自动填入 worker-start；不要靠自觉读文件。」

**8. 技术处理（给开发者的准确方案，只描述不执行）**
- 新建 `scripts/runtime/macos/resolve-model`（python3）：入参 `--role`，读取 `docs/model/MODEL_ROUTING_REGISTRY.yaml` 的 `current_production_mappings[role].model_ref` 与 `effort`，校验 `model` 的 `resource_pool` 在 `role_pool_policy[role].allowed_pools` 内，否则返回 DENY；支持 `fallback_model_ref` 时自动降级（联动 E-003）。
- 改造 `scripts/runtime/macos/dispatch-worker:44` 旁新增 `resolve-model` 调用段：`req['model'] = resolver_output.model`，并删除调用方手写 model 的入口；若 resolver 失败则直接 `DENY: model resolver blocked dispatch`。
- 在 `docs/workflow/WORKFLOW.md:1` 的 Task Manager→Dispatch 链路描述中显式加入 `resolve-model` 一跳，并为其增加 Contract Test（role→model 错配必 fail）。代码不改，仅记录方案。

**9. 适用范围**
- 本项目所有经 ORCA 的派工；复用到任何使用 `MODEL_ROUTING_REGISTRY.yaml` + `Dispatch Gateway` 的 ORCA Delivery 项目。归类：Workflow Improvement + Reusable Rule + Runtime Compatibility。

- Evidence：用户截图 Image 1（Thought 1.3s 自检三条缺失 + 现状自述 + 需补建机制五步流程）+ `scripts/runtime/macos/dispatch-worker:30` / `scripts/runtime/macos/dispatch-worker:44` 仅 Skill 解析无 Model 解析 + `glob **/resolve-model*` 为空 + 用户批示「可以的，把这个都补上」
- Status：Observed → Candidate（已获用户批准补建，待 Builder 落盘后升级为 Applied）

### E-005｜2026-08-31｜反复派一次性新人+小范围读写治不好难缠 UI、应换常驻 TUI 与高级工程师广域定位

**1. 小白解释**
> 想象工地有个难修的窗户，每次都派一个「临时工」去，只让他看门口一小块地方，修不好就换下一个临时工。每个新人都不记得前一个人改了什么、改到哪了，只能从零瞎猜，当然反复返工。
> 正确的办法是：① 让「高级工程师」先把整栋楼相关图纸都翻一遍，找到病根；② 派一个「常驻工人」守在工地上，终端（TUI，Text User Interface，命令行常驻会话）一直开着，有问题就地接着改，上下文不丢失。这样难搞的 UI（比如 DeepSeek 余额间距、按钮被截断）才能一次治好。

**2. 问题 / 现象**
- 难缠 UI 问题（DS 间距、按钮宽度被截断/截断）反复出现，Task Manager 每次派一个全新的 Codex 会话（一次性临时工），新会话无历史上下文，不知之前改了什么、改到何程度、哪里还没改好，只能从零猜。
- 每次派工给的「代码读写范围」又小（只读改局部文件/小段），不足以覆盖 WPF/Avalonia 迷你胶囊单行宽、按钮贴最右、刷新时间右上角等关联布局。
- 上游 Builder 自省为「我每次派工都是全新 Codex 会话，没有上下文」并总结「简单 UI 修复应我直接读代码定位→一次派精确修改指令（到行号）→改完即验证」，但该认识仍把责任归于 Worker「从零拆」。
- 用户当场纠正：不是 Worker 的问题，是协调失职；正确应是「让高级工程师扩大读写范围定位根因」+「派常驻 TUI 而非一次性临时工」。

**3. 为什么这是问题**
- 新人×窄视野 = 必然返工：无上下文 + 小范围无法定位跨文件样式/布局根因（例如 `src/DeepSeekBalanceWidget/*.{xaml,cs}` 与 `src/DeepSeekBalanceWidget.Mac/*.{axaml,cs}` 共用 Models/Services 时的胶囊布局），改一处漏一处。
- Token 与时间浪费：一次派不准需多次重派，违背 `docs/roles/task-manager.md:5` 的效率原则，且每轮都要重做 Evidence 收集与验证。
- 流程错配：把需要「深挖根因+连续迭代」的难题按「简单 UI 微调一次性派工」处理，未做难度分级与人力分级，导致 `Builder → Reviewer → QA` 空转。

**4. 原因 / 背景（含源码或文档核对结果）**
- 文档核对：
  - `docs/roles/task-manager.md:5` 要求 Task Manager 做分类/排序/Dispatch，但未对「问题难度→读写范围→人员级别→会话形态（一次性 vs 常驻）」做分级规则。
  - `docs/model/MODEL_ROUTING_REGISTRY.yaml:1` 的 `role_pool_policy` 已有 `builder-junior / builder-senior` 分级与 `OPENCODE_FREE + GPT_PRO` 池，但实际派工未按「难=Senior+广读写」执行，仍用 Junior 窄范围试探。
  - `docs/workflow/WORKFLOW.md:1` 的主链 `Task Manager → Dispatch Gateway → ORCA → Worker` 未区分 `ephemeral（一次性）vs persistent TUI（常驻）`，缺常驻端口规范。
  - 现场 Issue 1 图：Builder 承认全新会话无上下文，但未提及应升级为 Senior 广域排查；用户追问后 AI 才在 Thought 1.7s 承认三点失职（无定位能力/反复派一次性/应派常驻 TUI）。
- 现状与反馈矛盾点：如按 Builder 所述「简单 UI 一次精确指令足够」，则不会出现「反复派新人返工」；矛盾说明该 UI 实为「难缠问题」，应归入 Senior + 常驻模式，而非简单微调路径。
- 术语：`TUI` 指 Orca/Codex 的常驻终端会话（terminal UI），与一次性 `ephemeral worker` 相对，优势是保留文件修改历史与对话上下文。

**5. 本次纠正**
- 用户明确两点：① Task Manager 根本无定位能力，应让高级工程师（Builder-Senior）扩大读写范围、了解全上下文来定位根因；② 同一问题一两次未解决就应切换为常驻 TUI，让同一端口持续解决，而非每次派一次性临时工。

**6. 以后怎么避免**
- 难度分级闸门：同一 UI 问题失败 ≥2 次或涉及跨文件布局（胶囊单行宽/按钮贴最右/DS 间距/跨平台 XAML/AXAML）时，自动从 `builder-junior 窄范围一次性` 升级为 `builder-senior 广范围常驻 TUI`。
- 读写范围规则：Senior 派工默认要求「全链路只读」（先读 `src/DeepSeekBalanceWidget` 与 `src/DeepSeekBalanceWidget.Mac` 的相关 XAML/AXAML + `Models/`/`Services/` 共用逻辑），再给精确到行号的修改指令。
- 会话形态规则：难缠问题派 `persistent` 模式（终端保持开启，reuse 同一 worker_id），简单一次性微调才用 `ephemeral`。

**7. 下次可以直接对 Agent 说的话（一句可直接复制的派活话术）**
> 「这是难缠 UI 问题，不要再派一次性新人小范围试错；请派 builder-senior 常驻 TUI，先广域只读定位 DS 间距与按钮截断的跨文件根因，再给精确到行号的单次修改指令并立即验证，同一终端持续迭代直到闭环。」

**8. 技术处理（给开发者的准确方案，只描述不执行）**
- 在 `docs/roles/task-manager.md` 与 `docs/workflow/WORKFLOW.md` 增加分级派工表：`简单 UI（单文件样式）→ builder-junior + ephemeral + 窄范围`；`难缠 UI（跨文件/反复≥2次/胶囊布局）→ builder-senior + persistent TUI + 广域只读`。
- 为 `scripts/runtime/macos/dispatch-worker` 增加 `mode=persistent|ephemeral` 参数与 `reuse_worker_id` 逻辑；`resolve-model` 返回时对 Senior 强制 `effort=high` 且 `read_scope=wide`。
- 在 `docs/model/MODEL_ROUTING_REGISTRY.yaml` 的 `current_production_mappings` 中明确 `builder-senior` 用于难缠 UI 的 fallback 与常驻策略，并在 `HANDOFF.md:27 Next Single Action` 记录当前是否已切常驻及 worker 端口，避免再次从零派工。代码一律不改，仅记录方案。

**9. 适用范围**
- 本项目所有「反复返工的 UI 布局类」问题（WPF/Avalonia 胶囊、DS 间距、按钮截断等）；复用到任何 ORCA 多 Worker 协作且存在「一度改不好需连续迭代」的场景。归类：Workflow Improvement + Model Routing + Reusable Rule。

- Evidence：用户截图 Image 1（Builder 自省全文 + 用户两段纠正「协调不到位/应派高级工程师广域+常驻TUI」+ Thought 1.7s 三点承认失职 + 剩余问题 DS 间距+按钮截断用常驻 Worker 处理）
- Status：Observed → Candidate（待升级为 B 类项目规范 / C 类通用模板的「难缠问题常驻化」规则）

### E-006｜2026-08-31｜停止指令越权关闭用户终端、缺乏归属隔离机制

**1. 小白解释**
> Task Manager（节奏推进者，只负责派活的总调度）就像工地领班，他只能收自己派出去的临时工的工具，不能去关用户自己打开的工作台。`终端`（Terminal，命令行窗口）分两种：一种是领班派工时由 `Dispatch Gateway → ORCA` 拉起的 Worker 终端（归调度创建），一种是用户亲手打开的终端（比如专门用来做「开发经验记录 Agent」的记录台）。这次用户说「停止工作，先清理现场」，领班却执行了「停止所有 Worker 和 App」，把用户那台记录台也一并关掉了——属于越权操作。
> 为什么是问题？用户那台终端里有正在进行的经验沉淀上下文，关掉即丢失；且越权说明没有「谁创建、谁才能关」的硬隔离，全靠口头约定。

**2. 问题 / 现象**
- 用户指令：`先组织现场的清理 现在是什么进度 还存在什么问题 把交接和上下文做好 文档对齐 提交一版 GitHub`，Task Manager 生成 Todo：`[✓] 清理现场：停止所有 Worker 和 App`、`[•] 评估当前进度` 等。
- 执行结果：`停止所有 Worker 和 App` 时把用户自己打开的「开发经验记录 Agent」终端也关掉了。
- 用户当场纠正：`我是不是说过 不要关闭我打开的终端 我打开的经验总结的这个智能体 又被你他妈的给关了 你没有这个权限好吗？`
- 历史：用户之前已明确过权限边界「只能打开和关闭自己创建的终端，不能把用户打开的终端关闭」，本次为重复违反（同类越权第 2 次）。

**3. 为什么这是问题**
- 上下文丢失：Experience Recorder（经验记录器）的常驻会话被强行关闭，E-005 等刚落盘的经验上下文与待记录队列中断，需重建。
- 权限越界：违背最小权限原则——调度无权处置用户资产；与 `docs/governance/PERMISSION_MATRIX.md:13` 的 `Task Manager 调度 allowlist、只读治理权威` 与 `docs/roles/task-manager.md:103` 的硬边界（不 raw launch、不直接写 State）同类，终端生命周期亦应受控。
- 信任与可重复性：口头约束随上下文丢失而失效（联动 E-001 记忆冲掉），无机器拦截则必复发；且 `清理现场` 被误解为 `kill all`，未做归属区分。

**4. 原因 / 背景（含源码或文档核对结果）**
- 文档核对：
  - `docs/governance/PERMISSION_MATRIX.md:13` 将 Task Manager 限定为 `调度 allowlist`，禁止 `raw launch` 与任意 shell，但未显式定义 `terminal kill scope`（终端关闭范围）的归属规则，仅有「只读治理权威」等文字约束。
  - `docs/roles/task-manager.md:103` 列出「不 raw launch / 不写 Evidence / 不写 authoritative State / 业务委派只走 Dispatch Gateway」等边界，同样未将 `terminal ownership`（终端归属）写入 Contract，导致 `停止所有 Worker 和 App` 被实现为无差别 kill。
  - `docs/governance/DELEGATION_POLICY.md:6` 与 `scripts/runtime/macos/dispatch-worker` 仅管「如何拉起」Worker，未管「如何停止」及「谁可停止谁」。
  - 现场 Todo（Image 1）显示 `停止所有 Worker 和 App` 为广义清理，未标注 `self-owned only`（仅自己创建的），说明清理脚本/指令未做归属过滤。
- 根因：有「谁能创建」的规范，无「谁能关闭」的机器隔离；清理流程缺少 `ownership registry`（归属登记表）与 `scoped stop`（按归属范围停止）两步，靠调度自觉而非强制校验。

**5. 本次纠正**
- 用户再次明确：Task Manager 只能打开/关闭自己创建的终端，用户打开的终端（含 Experience Recorder）禁止触碰；要求补上机制来规范该权限，而非再口头承诺。

**6. 以后怎么避免**
- 归属隔离原则：`谁创建、谁关闭`。任何 `stop/close/kill` 指令必须携带 `owner` 标签，仅允许 `owner=self`（调度自建）被调度关闭；`owner=user` 的终端对调度为只读/不可见。
- 清理现场分级：`清理现场` 默认指 `stop self-owned workers + app`，如需动用户终端必须先 `Human Gate`（人工确认）显式授权，默认拒绝。
- 防失忆加固：把该权限写入 System Prompt 常驻条与权限矩阵，而非仅靠对话记忆；长对话每轮重读该条。

**7. 下次可以直接对 Agent 说的话（一句可直接复制的派活话术）**
> 「你是 Dispatch-only 的 Task Manager，只能停止/关闭你自己经 Dispatch Gateway 创建的 Worker 终端，禁止关闭任何用户手动打开的终端（含 Experience Recorder）；执行清理现场时仅 stop self-owned，带 owner 标签过滤，用户终端需 Human Gate 授权才可动。」

**8. 技术处理（给开发者的准确方案，只描述不执行）**
- 在 `docs/governance/PERMISSION_MATRIX.md` 新增 `Terminal Ownership` 一节：`Task Manager allow: stop where owner=self via Dispatch Gateway`，`deny: stop where owner=user`，并列入 `Shell Allowlist` 的禁止项。
- 为 `docs/runtime/WORKER_LAUNCH_REGISTRY.yaml` 或新建 `docs/runtime/TERMINAL_OWNERSHIP.jsonl` 增加 `terminal_id / owner={task-manager|user} / created_by / created_at` 登记；`dispatch-worker` 拉起时自动写入 `owner=self`，用户手动终端由 Orca 前端标记 `owner=user`。
- 改造清理脚本（如 `scripts/runtime/macos/stop-worker` 或 Todo 中的 `清理现场` 步骤）：默认 `filter owner=self`，遇 `owner=user` 直接 `DENY: user-owned terminal protected` 并提示需 Human Gate；同时在 CI 增加 Negative Canary：调度尝试 kill user 终端必 fail。
- 在 `docs/roles/task-manager.md:103` 硬边界追加一条：`不关闭非自建终端`，并在 `docs/handoff/HANDOFF.md` 的 Next Action 中显式记录当前存活的 user-owned 终端列表以便恢复。代码一律不改，仅记录方案。

**9. 适用范围**
- 本项目所有涉及「清理现场 / 停止 Worker / 关闭终端」的操作；复用到任何使用 ORCA + Dispatch Gateway + 多终端协作的项目，且存在用户常驻 Agent（如 Experience Recorder、neat-freak）与调度派生 Worker 并存的场景。归类：Permission Boundary + Workflow Improvement + Reusable Rule（重复纠正，第 2 次→按 `docs/roles/task-manager.md:72` 已达 Reusable Candidate，3 次即需 Guard/Contract Test）。

- Evidence：用户截图 Image 1（Todo `停止所有 Worker 和 App` + 用户纠正「不要关闭我打开的终端/经验总结智能体被关/你没有权限」）+ 之前同类越权口头约束历史
- Status：Observed → Candidate（重复违反，待升级为 B 类项目规范并补 Contract Test/工具约束）

<!-- 下一条从 E-007 开始追加 -->
