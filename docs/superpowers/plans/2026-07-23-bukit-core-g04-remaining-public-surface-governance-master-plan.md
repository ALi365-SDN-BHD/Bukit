# Bukit Core G-04 Remaining Public-Surface Governance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不把“仓内零引用”误写为“可安全删除”、不改变受支持产品契约的前提下，完成 G-04 剩余公共面资格审计、受控收窄或保留决策、验证和正式关闭。

**Architecture:** 以当前 `2.0` 公共 API baseline、已关闭的 136-entry 消费者声明 manifest 和 owner 传播图为治理事实。42个内部任务合并为4个执行任务组，每组包含10～12项，使用一个分支、一次组内完整测试和一次轻量复审。组内按依赖顺序连续实施；四组全部完成后只执行一次跨组全量复审，资格审计仍可得出“保留 public”，不能预设所有候选都应 internalize。

**Tech Stack:** .NET 10、C#、Native AOT、xUnit、JSON governance baseline、Bash targeted gates、Git任务组分支、组级轻量复审和计划级全量复审。

## Global Constraints

- 基线分支为 `2.0`；每个任务组必须从前一任务组已合并且验证完成的最新 `2.0` 建立一个 `codex/` 分支。
- 当前可观察起点为 `2.0@757fb149`：baseline 为 14 assemblies / 508 public types / 104 `2.0-candidate`；历史消费者 manifest 固定为 136 entries，不随类型删除而重写。
- `G-04D2B2` 正在执行；它并入任务组G1，不再单独执行测试和复审。只有G1全部10项完成、组内完整测试通过并完成轻量复审后，D2B2才随G1一起关闭。
- 1.x 公共面不在本计划中收窄；所有 breaking CLR 变化仅针对明确批准的 2.0 分支。
- C# `public` 不是自动 SDK 承诺，但“没有仓内引用”也不是删除授权。
- 每个资格审计必须检查 public/protected signature、跨程序集调用、反射、序列化、source generator、Native AOT、测试、活动文档和外部消费者新证据。
- `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` 是声明窗口关闭时的不可变历史证据；只更新当前 public API baseline 和当前态文档。
- 不修改配置 schema、`bukit-plugin-v1`、build-report schema、asset URL、路径安全策略、HTTP/TLS策略或持久化格式，除非另立非 G-04 产品契约任务并获得明确批准。
- 不以 public 类型数量下降、文件行数下降或测试总数不下降作为单独验收条件。
- 每个内部任务先写治理或行为断言，再做最小实现；测试文件可以在组内逐项准备，但组内不运行单任务test、focused gate或复审。
- 每个任务组只在全部内部任务完成后运行一次组内完整测试，包括该组列出的全部受影响测试项目、Architecture、public API drift、aggregate targeted gate和需要的Native AOT；`GROUP_BASE`必须在建立任务组分支时记录为当时`2.0`的完整commit SHA。
- 允许一个任务组包含多个相邻owner，但每个内部任务仍必须保持候选范围、传播图和兼容目标清晰，禁止借合组扩张schema、协议或运行时行为。
- CI、release、full、`scripts/test-all.sh`、`scripts/smoke-all.sh` 和 whole-solution tests 不在默认授权内。
- 每个任务组完整测试后只执行一次轻量复审；只检查越界修改、测试失败、baseline/manifest错误、明显正确性或安全回归。确认没有阻断项即可合并进入下一组。
- 四个任务组全部完成后，只执行一次覆盖42项、136个历史候选和所有跨组影响的全量只读复审；最终Critical / Important / Minor必须为`0 / 0 / 0`。
- 任何新消费者、二进制插件、反射/AOT/序列化绑定或受支持文档命中都会触发停止；转为保留、obsolete window、facade 或迁移任务。

---

## 1. 当前事实基线

### 1.1 已完成范围

- G-04A：治理分类纠正。
- G-04B1～B3：消费者声明准备、开放、刷新和窗口关闭。
- G-04C：单类型试点。
- G-04D1：Content Notion renderer cluster；D1A、D1B、D1C-M1、D1C-M2 已形成关闭证据。
- G-04D2A：`PluginSecretMasker` internalization。
- G-04D2B1：`PluginHostErrorCodes` diagnostic contract 迁移证据。

### 1.2 当前剩余候选

| Assembly / owner | 当前候选 | 本计划 owner 批次 |
|---|---:|---|
| `Bukit.PluginHost` / External plugin host | 15 | G-04D2 |
| `Bukit.Content` / Content acquisition | 5 | G-04D3 |
| `Bukit.Shared` / Shared foundation | 17 | G-04D4 |
| `Bukit.Cli.Shared` / CLI contract infrastructure | 5 | G-04D5 |
| `Bukit.Rendering` / Rendering and theme model | 2 | G-04D6 |
| `Bukit.Routing` / Routing | 1 | G-04D7 |
| `Bukit.Theme` / Theme runtime | 3 | G-04D8 |
| `Bukit.Engine` / Build engine | 56 | G-04D9 |
| **合计** | **104** | |

若 D2B2 最终批准并 internalize `PluginHostErrorCodes`，下一基线应为 507 public types / 103 candidates。若整个 D2 的其他 14 项最终都获得收窄资格，D2 关闭后理论基线为 493 public types / 89 candidates；这是推演值，不是预先授权。

### 1.3 任务组与内部任务总数

本计划保留42个可追踪内部任务，但执行、测试和复审单位改为以下4个任务组：

| 任务组 | 内部任务 | 数量 | 组级范围 |
|---|---|---:|---|
| G-04 Group 1 | Task 1～10 | 10 | PluginHost D2全部与Content D3前半 |
| G-04 Group 2 | Task 11～20 | 10 | Content D3后半、Shared D4与CLI Shared D5 |
| G-04 Group 3 | Task 21～30 | 10 | Rendering D6、Routing D7与Theme D8实施 |
| G-04 Group 4 | Task 31～42 | 12 | Theme D8收口、Engine D9全部与G-04最终关闭 |
| **合计** | **Task 1～42** | **42** | **4个任务组，每组不少于10项** |

资格审计可能批准收窄、要求迁移契约、要求obsolete window，或决定保留。内部实施任务不会因“保留”而消失，而是转换为可验证的保留决策闭环。每个任务组内只允许一次完整测试和一次轻量复审；深度、逐类型、跨组复审仅在整个计划结束后执行一次。

## 2. 共享文件与验证协议

### 2.1 每个资格审计读取

- `docs/governance/bukit-core-public-api-baseline.v1.json`
- `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
- `guide/dev/public-api-governance.md`
- `docs/compatibility-governance.md`
- `docs/compatibility-governance.zh-CN.md`
- `src/Bukit-Core/Bukit.PluginHost/`
- `src/Bukit-Core/Bukit.Content/`
- `src/Bukit-Core/Bukit.Shared/`
- `src/Bukit-Core/Bukit.Cli.Shared/`
- `src/Bukit-Core/Bukit.Rendering/`
- `src/Bukit-Core/Bukit.Routing/`
- `src/Bukit-Core/Bukit.Theme/`
- `src/Bukit-Core/Bukit.Engine/`
- `tests/Bukit.PluginHost.Tests/`
- `tests/Bukit.Content.Tests/`
- `tests/Bukit.Shared.Tests/`
- `tests/Bukit.Cli.Tests/`
- `tests/Bukit.Rendering.Tests/`
- `tests/Bukit.Routing.Tests/`
- `tests/Bukit.Theme.Tests/`
- `tests/Bukit.Engine.Tests/`
- `tests/Bukit.Architecture.Tests/`
- 当前 G-04 owner 报告与前一 owner closure ledger

### 2.2 每个可见性变更更新

- 对应 production declaration。
- 对应 owner tests，必要时增加精准 `InternalsVisibleTo`，不得授予无关生产程序集。
- `docs/governance/bukit-core-public-api-baseline.v1.json`
- `guide/dev/public-api-governance.md` 的当前 public/candidate 计数及批准决策。
- 对应的 `tests/Bukit.Architecture.Tests/G04D*.cs`
- 对应分析账本 `docs/analysis/bukit-core-g04d*-*.zh-CN.md`

### 2.3 任务组内执行协议

- 每个内部任务完成源码、测试文件、baseline投影和ledger更新后，只记录`group-verification-pending`。
- 组内不运行`dotnet test`、`post-change-focused.sh`、`post-change-targeted.sh`、Native AOT或单任务复审。
- 组内允许使用只读搜索、编译前静态检查和`git diff --check`定位明显编辑错误，但这些不构成测试通过证据。
- 如果组内前一任务产生无法继续编译的中间态，必须在同一任务组内立即补齐其已批准的原子传播图，不得通过扩大范围处理。

### 2.4 每个任务组的唯一完整测试

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore
bash scripts/checks/public-api-drift.sh check Release
mapfile -t GROUP_CHANGED_PATHS < <(git diff --name-only "$GROUP_BASE"..HEAD)
bash scripts/checks/post-change-targeted.sh --base "$GROUP_BASE" -- "${GROUP_CHANGED_PATHS[@]}"
git diff --check "$GROUP_BASE"..HEAD
```

每组还必须运行该组列出的owner tests和Native AOT证明。组内完整测试必须全部通过；环境阻塞与真实回归分开记录。

### 2.5 轻量复审与最终全量复审

每个任务组的轻量复审只检查：

1. changed paths是否全部属于该组授权范围；
2. 组内完整测试是否真实执行并全部通过；
3. public API baseline计数是否与实际程序集一致；
4. 历史136-entry manifest是否保持不变；
5. 是否存在明显schema、protocol、config、security或runtime行为漂移；
6. 是否存在会阻断下一组的正确性、安全性或兼容性问题。

轻量复审不逐文件深审实现质量，不重复逐类型消费者调查，不重新执行测试，也不要求为非阻断观察项立即修改代码。非阻断观察项进入最终全量复审台账。

四组全部完成后执行一次最终全量复审，覆盖：

- G-04A～D9和G-04Z完整aggregate diff；
- 42个内部任务的范围与终态；
- 136个历史候选的逐项去向；
- 跨组signature、reflection、serialization、AOT、friend access和测试迁移；
- 所有迁移说明、baseline投影、保留决策和越界风险。

只有最终全量复审达到Critical / Important / Minor=`0 / 0 / 0`，G-04和AD-04才可申请正式关闭。

---

## 3. Task Group G1：PluginHost与Content前半（Task 1～10）

**Group branch:** `codex/g04-group1-pluginhost-content-a`

**Group base:** 建立分支前记录最新`2.0`完整SHA为`GROUP_BASE`。

**Group rule:** Task 1～9只实现和记录`group-verification-pending`；Task 10统一运行PluginHost、Content、CLI、Architecture、public API drift、targeted gate、Native AOT和一次轻量复审。

### Task 1: G-04D2B2 `PluginHostErrorCodes` eligibility/internalization

**Purpose:** 在B1已将测试从public const CLR引用迁移到入口诊断与固定词汇fixture后，在G1内决定并实施单类型收窄。

**Files:**
- Modify: `src/Bukit-Core/Bukit.PluginHost/PluginHostErrorCodes.cs`
- Modify: `docs/governance/bukit-core-public-api-baseline.v1.json`
- Modify: `guide/dev/public-api-governance.md`
- Modify: `tests/Bukit.Architecture.Tests/G04D2B1PluginHostErrorCodeContractTests.cs`
- Test: `tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs`
- Create: `docs/analysis/bukit-core-g04d2b2-plugin-host-error-codes-internalization-2026-07-23.zh-CN.md`

**Scope:** 只允许把 `PluginHostErrorCodes` 从 `public static` 改为 `internal static`。六个字符串值、入口异常码选择、`permissionDenied` 保留词汇、protocol DTO 和异常消息不变。

**Steps:**

- [ ] 冻结任务组基点并确认B1既有aggregate与复审证据可用。
- [ ] 新增架构失败断言：编译后的 `Bukit.PluginHost` 不再导出该类型，但 fixture 仍精确包含六个值。
- [ ] 运行 Architecture 测试，确认因类型仍 public 而失败。
- [ ] 做单行 access modifier 变更并更新当前 baseline；历史 136-entry manifest 不变。
- [ ] 更新ledger为`group-verification-pending`，不运行Task 1单任务测试或复审。

**Acceptance:** 507 public types / 103 candidates；五个运行时错误码和一个保留词汇均无变化；0 protocol/schema/runtime drift。

### Task 2: G-04D2R execution-report contract decision

**Purpose:** 决定 `PluginExecutionReport`、`PluginExecutionReporter`、`PluginExecutionResponseSummary` 是版本化支持工件还是内部诊断工件。

**Files:**
- Inspect: `src/Bukit-Core/Bukit.PluginHost/PluginExecutionReport.cs`
- Inspect: `src/Bukit-Core/Bukit.PluginHost/PluginExecutionReporter.cs`
- Inspect: `src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs`
- Inspect: `src/Bukit-Core/Bukit.Cli/Cli/PluginCliLoader.cs`
- Inspect: `tests/Bukit.PluginHost.Tests/PluginExecutionReporterTests.cs`
- Create: `docs/analysis/bukit-core-g04d2r-execution-report-contract-decision-2026-07-23.zh-CN.md`

**Steps:**

- [ ] 枚举三个候选的所有构造、返回、序列化和文件写入传播。
- [ ] 对照活动文档确认报告路径、字段和保留期限是否已被公开承诺。
- [ ] 建立JSON shape检查输入并记录Native AOT serializer可达性，留待Task 10统一运行。
- [ ] 在“versioned supported artifact”“internal diagnostic artifact”“暂时保留”三种结论中选择一种并说明版本策略。
- [ ] 若选择 versioned artifact，明确 CLR DTO 可继续 internal 而 JSON schema 独立版本化，或明确保留 CLR public 的理由。
- [ ] 只提交决策报告；本任务不改 schema、writer 或类型访问级别。

**Acceptance:** 三个类型都有明确 contract owner、兼容目标和后续 D2G 行为；不存在“先 internalize、以后再决定报告契约”的倒序。

### Task 3: G-04D2C Host construction-boundary design

**Purpose:** 解除 retained public constructors/methods 对14项候选的传播阻断，同时保持 CLI→PluginHost→process plugin 产品边界。

**Files:**
- Inspect: `src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs`
- Inspect: `src/Bukit-Core/Bukit.PluginHost/PluginProcessInvoker.cs`
- Inspect: `src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs`
- Inspect: `src/Bukit-Core/Bukit.PluginHost/PluginConfigLoader.cs`
- Inspect: `src/Bukit-Core/Bukit.PluginHost/PluginPermissionEvaluator.cs`
- Inspect: `src/Bukit-Core/Bukit.Cli/Cli/PluginCliLoader.cs`
- Create: `docs/analysis/bukit-core-g04d2c-host-construction-boundary-design-2026-07-23.zh-CN.md`

**Steps:**

- [ ] 为每个 retained constructor/method 建立 candidate-type propagation graph。
- [ ] 区分 CLI composition contract、测试 seam 和纯实现构造参数。
- [ ] 选择最小方案：internal composition factory、internal constructor、或保留 public dependency；禁止新增通用 service locator。
- [ ] 验证方案不要求 CLI 链接编译 PluginHost 源文件，不扩大 `InternalsVisibleTo` 到生产 assembly。
- [ ] 给 D2D、D2E、D2F、D2G 写出精确 companion-member migration 顺序。
- [ ] 完成设计报告并标记`group-verification-pending`；不在Task 3单独测试或复审。

**Acceptance:** 14项候选逐项映射到后续原子图，没有签名传播遗漏。

### Task 4: G-04D2D permission evaluator/normalizer graph

**Purpose:** 原子处理 `PluginFileSystemPermissionEvaluator` 与 `PluginPermissionPathNormalizer` 及 retained `PluginPermissionEvaluator` 的构造传播。

**Files:**
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/PluginFileSystemPermissionEvaluator.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/PluginPermissionPathNormalizer.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/PluginPermissionEvaluator.cs`
- Test: `tests/Bukit.PluginHost.Tests/PluginPermissionEvaluatorTests.cs`
- Test: `tests/Bukit.Architecture.Tests/PluginBoundaryTests.cs`
- Create: `tests/Bukit.Architecture.Tests/G04D2DPermissionGraphTests.cs`
- Create: `docs/analysis/bukit-core-g04d2d-permission-graph-decision-2026-07-23.zh-CN.md`

**Steps:**

- [ ] 添加入口行为测试，固定读/写权限、路径规范化、symlink/reparse 和拒绝原因。
- [ ] 按 D2C 方案迁移 retained constructor，不改变 permission semantics。
- [ ] 若传播解除且无新消费者，原子 internalize 两个候选；否则记录保留理由。
- [ ] 更新当前 baseline 和治理计数，只包含本图实际变化。
- [ ] 将PluginHost、Architecture和安全断言加入G1测试集合，留待Task 10统一运行。

**Acceptance:** 不出现权限放宽、路径工具重写或 wire error 新语义；两个候选不会留下不可构造的半图。

### Task 5: G-04D2E runtime-only context

**Purpose:** 处理 `PluginRuntimeOnlyContext` 与 retained `PluginConfigLoader` 构造传播。

**Files:**
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/PluginRuntimeOnlyContext.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/PluginConfigLoader.cs`
- Test: `tests/Bukit.PluginHost.Tests/PluginConfigLoaderTests.cs`
- Create: `tests/Bukit.Architecture.Tests/G04D2ERuntimeContextTests.cs`
- Create: `docs/analysis/bukit-core-g04d2e-runtime-context-decision-2026-07-23.zh-CN.md`

**Steps:**

- [ ] 固定 runtime-only config filtering 的入口行为和序列化结果。
- [ ] 按 D2C 方案消除 public signature 传播，保持配置字段和默认值不变。
- [ ] 无阻断时 internalize enum；有阻断时形成保留决策。
- [ ] 更新baseline并把配置/secret断言加入G1测试集合，留待Task 10统一运行。

**Acceptance:** runtime-only过滤行为完全一致，0 config/schema drift。

### Task 6: G-04D2F process/protocol eight-type graph

**Purpose:** 原子审理进程运行与协议编排图：

- `IPluginProcessInvoker`
- `IPluginRequestIdFactory`
- `IProcessRunner`
- `PluginProcessRequest`
- `PluginProcessResult`
- `ProcessOutputStream`
- `ProcessRunRequest`
- `ProcessRunResult`

**Files:**
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/IPluginProcessInvoker.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/IPluginRequestIdFactory.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/IProcessRunner.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/PluginProcessRequest.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/PluginProcessResult.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/ProcessOutputStream.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/ProcessRunRequest.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/ProcessRunResult.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/PluginProcessInvoker.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs`
- Test: `tests/Bukit.PluginHost.Tests/PluginProtocolClientTests.cs`
- Test: `tests/Bukit.PluginHost.Tests/SystemProcessRunnerTests.cs`
- Create: `tests/Bukit.Architecture.Tests/G04D2FProcessGraphTests.cs`
- Create: `docs/analysis/bukit-core-g04d2f-process-graph-decision-2026-07-23.zh-CN.md`

**Steps:**

- [ ] 固定 timeout、cancel、stdout/stderr、output limit、exit code、request ID 和 process disposal 行为。
- [ ] 检查测试替身是否可迁移到 internal seam，且不会迫使无关生产 assembly 获得 friend access。
- [ ] 按 D2C companion-member 顺序迁移；八项必须作为完整图评审，允许评审后拆成更小原子提交，但不得形成不可编译中间态。
- [ ] 不修改 `bukit-plugin-v1` JSON DTO、handshake 或错误码。
- [ ] 更新baseline并把PluginHost、CLI、Architecture和AOT场景加入G1测试集合，留待Task 10统一运行。

**Acceptance:** 外部进程插件行为与协议字节形状不变；所有测试 seam 有明确 owner。

### Task 7: G-04D2G execution-report CLR graph resolution

**Purpose:** 按 D2R 决策处理 `PluginExecutionReport`、`PluginExecutionReporter`、`PluginExecutionResponseSummary`。

**Files:**
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/PluginExecutionReport.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/PluginExecutionReporter.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs`
- Test: `tests/Bukit.PluginHost.Tests/PluginExecutionReporterTests.cs`
- Create: `tests/Bukit.Architecture.Tests/G04D2GExecutionReportTests.cs`
- Create: `docs/analysis/bukit-core-g04d2g-execution-report-resolution-2026-07-23.zh-CN.md`

**Steps:**

- [ ] 若 D2R 选择版本化 artifact，先用 golden JSON 和独立 schema validator 固定 shape。
- [ ] 若 D2R 选择 internal artifact，先固定现有 JSON 字段、顺序无关语义、redaction 和报告路径。
- [ ] 处理 `PluginProtocolClient` constructor/return propagation。
- [ ] 仅在 CLR identity 无外部承诺且传播解除时 internalize；否则更新为明确 retained classification。
- [ ] 更新baseline并把report JSON、PluginHost、CLI、Architecture和AOT序列化场景加入G1测试集合。

**Acceptance:** D2R 决策完整落地；JSON contract与CLR visibility不再混为一谈。

### Task 8: G-04D2 PluginHost decision consolidation

**Purpose:** 对D2A、D2B1和本组Task 1～7形成PluginHost终态矩阵并加入G1待验证集合；不在Task 8运行测试或复审。

**Files:**
- Create: `docs/analysis/bukit-core-g04d2-pluginhost-final-aggregate-closure-2026-07-23.zh-CN.md`
- Review: all G-04D2 reports, commits, tests, baseline and activity docs

**Steps:**

- [ ] 从 D2 parent base 生成 aggregate diff 和逐类型状态矩阵。
- [ ] 对16个原始 PluginHost候选标注 removed/internalized、retained、migrated或blocked。
- [ ] 核对历史 manifest 136 entries 和 Git blob 未被重写。
- [ ] 对16个原始PluginHost候选标注internalized、retained、migrated或blocked。
- [ ] 核对protocol/schema/config/security待验证断言完整。
- [ ] 标记`group-verification-pending`并进入Task 9。

**Acceptance:** PluginHost 16项全部有正式状态；不存在“仍是candidate但没有下一动作”的悬空条目。

---

### G-04D3：Content acquisition

### Task 9: G-04D3 eligibility audit

**Purpose:** 审计五项候选并形成两个原子图：

- Body/Markdown graph：`CompositeContentBodyStore`、`DictionaryContentBodyStore`、`BasicMarkdownToHtml`、`MarkdownBodyStore`
- Notion transport facade：`NotionClientStats`

**Files:**
- Inspect: `src/Bukit-Core/Bukit.Content/CompositeContentBodyStore.cs`
- Inspect: `src/Bukit-Core/Bukit.Content/DictionaryContentBodyStore.cs`
- Inspect: `src/Bukit-Core/Bukit.Content/Markdown/BasicMarkdownToHtml.cs`
- Inspect: `src/Bukit-Core/Bukit.Content/Markdown/MarkdownBodyStore.cs`
- Inspect: `src/Bukit-Core/Bukit.Content/Notion/NotionApiClient.cs`
- Inspect: `tests/Bukit.Content.Tests/`
- Create: `docs/analysis/bukit-core-g04d3-content-acquisition-eligibility-audit-2026-07-23.zh-CN.md`

**Acceptance:** 五项逐类型给出消费者、构造传播、行为替代和AOT结论；`NotionClientStats`不得混入body-store实施。

### Task 10: G-04D3A body/Markdown graph resolution and Group 1 closure

**Purpose:** 对四个body/Markdown候选执行经批准的internalize、facade迁移或保留决策。

**Files:**
- Modify conditionally: four source files listed in Task 9
- Test: `tests/Bukit.Content.Tests/ContentBodyStoreTests.cs`
- Test: `tests/Bukit.Content.Tests/MarkdownBodyStoreTests.cs`
- Create: `tests/Bukit.Architecture.Tests/G04D3AContentBodyGraphTests.cs`
- Create: `docs/analysis/bukit-core-g04d3a-content-body-graph-resolution-2026-07-23.zh-CN.md`

**Group tests:**

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore
```

**Acceptance:** fallback顺序、body identity、Markdown safety和async disposal不变；G1组内完整测试、Native AOT和一次轻量复审通过。

---

## 4. Task Group G2：Content后半、Shared与CLI Shared（Task 11～20）

**Group branch:** `codex/g04-group2-content-b-shared-cli`

**Group rule:** Task 11～19不单独测试或复审；Task 20统一执行Content、Notion、Shared、CLI、Engine、Architecture、public API drift、targeted gate和AOT完整测试，随后只做一次轻量复审。

### Task 11: G-04D3B `NotionClientStats` transport-facade resolution

**Purpose:** 与body-store图分开决定legacy stats CLR identity是否迁移到canonical Notion owner、保留或收窄。

**Files:**
- Modify conditionally: `src/Bukit-Core/Bukit.Content/Notion/NotionApiClient.cs`
- Inspect: `src/Bukit-Core/Bukit.Notion/`
- Test: `tests/Bukit.Content.Tests/`
- Test: `tests/Bukit.Content.Notion.Tests/`
- Test: `tests/Bukit.Notion.Tests/`
- Create: `tests/Bukit.Architecture.Tests/G04D3BNotionStatsTests.cs`
- Create: `docs/analysis/bukit-core-g04d3b-notion-client-stats-resolution-2026-07-23.zh-CN.md`

**Acceptance:** request/throttle统计含义和transport lifetime不变；不顺带修改Notion API、retry或rate limit。

### Task 12: G-04D3 Content decision consolidation

**Purpose:** 对五项Content候选形成统一终态矩阵并加入G2待验证集合；不在Task 12运行测试或复审。

**Acceptance:** 五项均有终态和待验证断言，无媒体、SEO或配置范围扩张。

---

## 5. G-04D4：Shared foundation

### Task 13: G-04D4 eligibility audit

**Purpose:** 将17项候选拆为16项legacy Notion model/tokenizer图和单独的`ValueCoercion`工具。

**Files:**
- Inspect: `src/Bukit-Core/Bukit.Shared/Notion/NotionBlockTypes.cs`
- Inspect: `src/Bukit-Core/Bukit.Shared/Notion/HtmlTokenizer.cs`
- Inspect: `src/Bukit-Core/Bukit.Shared/ValueCoercion.cs`
- Inspect: `src/Bukit-Core/Bukit.Notion/`
- Inspect: `tests/Bukit.Shared.Tests/`
- Create: `docs/analysis/bukit-core-g04d4-shared-foundation-eligibility-audit-2026-07-23.zh-CN.md`

**Acceptance:** inheritance、record equality、token shape、serializer/reflection和Content/Engine消费者全部纳入传播图。

### Task 14: G-04D4A legacy Notion model/tokenizer graph resolution

**Purpose:** 原子处理`NotionBlock`继承图、`RichTextSegment`、`HtmlTokenizer`及其两个nested types。

**Files:**
- Modify conditionally: `src/Bukit-Core/Bukit.Shared/Notion/NotionBlockTypes.cs`
- Modify conditionally: `src/Bukit-Core/Bukit.Shared/Notion/HtmlTokenizer.cs`
- Test: `tests/Bukit.Shared.Tests/`
- Test: `tests/Bukit.Content.Tests/`
- Create: `tests/Bukit.Architecture.Tests/G04D4ASharedNotionGraphTests.cs`
- Create: `docs/analysis/bukit-core-g04d4a-shared-notion-graph-resolution-2026-07-23.zh-CN.md`

**Acceptance:** 16个CLR identity不形成半删除继承图；canonical replacement可编译；1.x facade freeze陈述不被改写成2.0无条件删除。

### Task 15: G-04D4B `ValueCoercion` resolution

**Purpose:** 单独审理通用转换工具，避免借Notion批次顺带移动Shared领域逻辑。

**Files:**
- Modify conditionally: `src/Bukit-Core/Bukit.Shared/ValueCoercion.cs`
- Test: `tests/Bukit.Shared.Tests/ValueCoercionTests.cs`
- Create: `tests/Bukit.Architecture.Tests/G04D4BValueCoercionTests.cs`
- Create: `docs/analysis/bukit-core-g04d4b-value-coercion-resolution-2026-07-23.zh-CN.md`

**Acceptance:** null、number、boolean、culture和fallback语义不变；不新增全局conversion abstraction。

### Task 16: G-04D4 Shared decision consolidation

**Purpose:** 汇总17项Shared终态并加入G2待验证集合；不在Task 16运行测试或复审。

**Acceptance:** 17项均有终态与待验证断言；Shared不再承载未经说明的第二套Notion领域实现。

---

### G-04D5：CLI contract infrastructure

### Task 17: G-04D5 eligibility audit

**Purpose:** 审计parse/result graph和error payload：

- `CliBoundCommandFactory`
- `CliParseResult`
- `SimpleParseResult`
- `SubcommandParseResult`
- `CliErrorRenderer.CliErrorPayload`

**Files:**
- Inspect: `src/Bukit-Core/Bukit.Cli.Shared/Cli/Binding/CliBoundCommandFactory.cs`
- Inspect: `src/Bukit-Core/Bukit.Cli.Shared/Cli/Parsing/CliParseResult.cs`
- Inspect: `src/Bukit-Core/Bukit.Cli.Shared/Cli/Rendering/CliErrorRenderer.cs`
- Inspect: `src/Bukit-Core/Bukit.Cli/`
- Inspect: `tests/Bukit.Cli.Tests/`
- Create: `docs/analysis/bukit-core-g04d5-cli-shared-eligibility-audit-2026-07-23.zh-CN.md`

**Acceptance:** CLI exit code、help/error JSON、record inheritance、AOT序列化和Labs调用均有证据。

### Task 18: G-04D5A parse/result graph resolution

**Purpose:** 原子处理factory和三个parse result identity。

**Files:**
- Modify conditionally: factory and parse result files from Task 17
- Test: `tests/Bukit.Cli.Tests/CliParserTests.cs`
- Test: `tests/Bukit.Cli.Tests/CliContractTests.cs`
- Create: `tests/Bukit.Architecture.Tests/G04D5ACliParseGraphTests.cs`
- Create: `docs/analysis/bukit-core-g04d5a-cli-parse-graph-resolution-2026-07-23.zh-CN.md`

**Acceptance:** 参数绑定、subcommand嵌套、diagnostic顺序和退出行为不变；不修改命令树。

### Task 19: G-04D5B error payload resolution

**Purpose:** 单独决定nested `CliErrorPayload`的CLR可见性，同时保留机器可读错误输出。

**Files:**
- Modify conditionally: `src/Bukit-Core/Bukit.Cli.Shared/Cli/Rendering/CliErrorRenderer.cs`
- Test: `tests/Bukit.Cli.Tests/CliErrorRendererTests.cs`
- Create: `tests/Bukit.Architecture.Tests/G04D5BCliErrorPayloadTests.cs`
- Create: `docs/analysis/bukit-core-g04d5b-cli-error-payload-resolution-2026-07-23.zh-CN.md`

**Acceptance:** JSON字段、null处理、stderr/stdout和exit code不变。

### Task 20: G-04D5 CLI decision consolidation and Group 2 closure

**Purpose:** 汇总五项CLI终态，并仅在此处执行G2完整测试和一次轻量复审。

**Group tests:**

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore
```

**Acceptance:** CLI contract golden断言、Content/Notion/Shared跨边界、G2完整测试、AOT和一次轻量复审通过。

---

## 5. Task Group G3：Rendering、Routing与Theme实施（Task 21～30）

**Group branch:** `codex/g04-group3-rendering-routing-theme`

**Group rule:** Task 21～29不单独测试或复审；Task 30统一执行Rendering、Routing、Theme、CLI、Engine、Architecture、public API drift、targeted gate和AOT完整测试，随后只做一次轻量复审。

---

### G-04D6：Rendering

### Task 21: G-04D6 eligibility audit

**Purpose:** 分别审计`FileTemplateLoader`和`ScribanModelBinder`的Scriban接口传播、Engine friend access和Theme消费者。

**Files:**
- Inspect: `src/Bukit-Core/Bukit.Rendering/Scriban/FileTemplateLoader.cs`
- Inspect: `src/Bukit-Core/Bukit.Rendering/Scriban/ScribanModelBinder.cs`
- Inspect: `src/Bukit-Core/Bukit.Rendering/InternalsVisibleTo.cs`
- Inspect: `tests/Bukit.Rendering.Tests/`
- Create: `docs/analysis/bukit-core-g04d6-rendering-eligibility-audit-2026-07-23.zh-CN.md`

**Acceptance:** Scriban interface、reflection/member renaming、template fallback和AOT风险逐项判定。

### Task 22: G-04D6A `FileTemplateLoader` resolution

**Purpose:** 处理template loader可见性而不改变override/child/parent fallback。

**Files:**
- Modify conditionally: `src/Bukit-Core/Bukit.Rendering/Scriban/FileTemplateLoader.cs`
- Test: `tests/Bukit.Rendering.Tests/FileTemplateLoaderTests.cs`
- Create: `tests/Bukit.Architecture.Tests/G04D6AFileTemplateLoaderTests.cs`
- Create: `docs/analysis/bukit-core-g04d6a-file-template-loader-resolution-2026-07-23.zh-CN.md`

**Acceptance:** CG-005 fallback契约和路径安全不变。

### Task 23: G-04D6B `ScribanModelBinder` resolution

**Purpose:** 处理binder可见性并固定成员命名、null、dictionary/list和safe-object投影。

**Files:**
- Modify conditionally: `src/Bukit-Core/Bukit.Rendering/Scriban/ScribanModelBinder.cs`
- Test: `tests/Bukit.Rendering.Tests/ScribanModelBinderTests.cs`
- Create: `tests/Bukit.Architecture.Tests/G04D6BScribanModelBinderTests.cs`
- Create: `docs/analysis/bukit-core-g04d6b-scriban-model-binder-resolution-2026-07-23.zh-CN.md`

**Acceptance:** 模板对象公开shape与Scriban行为不变；不借机重命名模板字段。

### Task 24: G-04D6 Rendering decision consolidation

**Purpose:** 汇总两项Rendering终态并加入G3待验证集合；不在Task 24运行测试或复审。

**Acceptance:** 两项均有终态与待验证断言，模板fallback和模型投影无范围漂移。

---

### G-04D7：Routing

### Task 25: G-04D7 eligibility audit

**Purpose:** 审计`RouteGenerator.RouteGenerationResult`的nested identity、返回传播、序列化和真实消费者。

**Files:**
- Inspect: `src/Bukit-Core/Bukit.Routing/RouteGenerator.cs`
- Inspect: `src/Bukit-Core/Bukit.Engine/`
- Inspect: `tests/Bukit.Routing.Tests/`
- Create: `docs/analysis/bukit-core-g04d7-route-result-eligibility-audit-2026-07-23.zh-CN.md`

**Acceptance:** 与已删除的`RouteInventoryInspectEntry`明确区分；不能复用G-04C结论。

### Task 26: G-04D7A route result resolution

**Purpose:** 根据D7资格结论收窄、迁移或保留单个nested result。

**Files:**
- Modify conditionally: `src/Bukit-Core/Bukit.Routing/RouteGenerator.cs`
- Test: `tests/Bukit.Routing.Tests/RouteGeneratorTests.cs`
- Test: `tests/Bukit.Engine.Tests/`
- Create: `tests/Bukit.Architecture.Tests/G04D7ARouteGenerationResultTests.cs`
- Create: `docs/analysis/bukit-core-g04d7a-route-result-resolution-2026-07-23.zh-CN.md`

**Acceptance:** route precedence、collision、locale和安全校验不变。

### Task 27: G-04D7 Routing decision consolidation

**Purpose:** 对Routing唯一剩余候选形成终态并加入G3待验证集合；不在Task 27运行测试、AOT或复审。

**Acceptance:** Routing候选数为零或该类型获得明确retained终态，并具备G4验证断言。

---

### G-04D8：Theme

### Task 28: G-04D8 eligibility audit

**Purpose:** 将三个候选拆为validation exception graph和doctor result：

- `SchemaValidationError`
- `SchemaValidationException`
- `ThemeDoctorCommand.DoctorResult`

**Files:**
- Inspect: `src/Bukit-Core/Bukit.Theme/SectionSchemaValidator.cs`
- Inspect: `src/Bukit-Core/Bukit.Theme/ThemeDoctorCommand.cs`
- Inspect: `src/Bukit-Core/Bukit.Cli/`
- Inspect: `tests/Bukit.Theme.Tests/`
- Create: `docs/analysis/bukit-core-g04d8-theme-eligibility-audit-2026-07-23.zh-CN.md`

**Acceptance:** exception member传播、doctor输出、schema错误顺序和JSON/AOT风险有明确判定。

### Task 29: G-04D8A schema validation graph resolution

**Purpose:** 原子处理error record和exception，不产生public exception包含internal member的破碎图。

**Files:**
- Modify conditionally: `src/Bukit-Core/Bukit.Theme/SectionSchemaValidator.cs`
- Test: `tests/Bukit.Theme.Tests/SectionSchemaValidatorTests.cs`
- Create: `tests/Bukit.Architecture.Tests/G04D8AThemeValidationGraphTests.cs`
- Create: `docs/analysis/bukit-core-g04d8a-theme-validation-graph-resolution-2026-07-23.zh-CN.md`

**Acceptance:** strict/warn模式、错误文本和排序不变；不修改theme schema。

### Task 30: G-04D8B doctor result resolution and Group 3 closure

**Purpose:** 单独处理`ThemeDoctorCommand.DoctorResult`及CLI输出传播。

**Files:**
- Modify conditionally: `src/Bukit-Core/Bukit.Theme/ThemeDoctorCommand.cs`
- Test: `tests/Bukit.Theme.Tests/ThemeDoctorCommandTests.cs`
- Test: `tests/Bukit.Cli.Tests/DoctorCommandTests.cs`
- Create: `tests/Bukit.Architecture.Tests/G04D8BThemeDoctorResultTests.cs`
- Create: `docs/analysis/bukit-core-g04d8b-theme-doctor-result-resolution-2026-07-23.zh-CN.md`

**Group tests:**

```bash
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore
```

**Acceptance:** doctor exit code、文本/JSON输出和theme.yaml要求不变；G3完整测试、AOT和一次轻量复审通过。

---

## 6. Task Group G4：Theme收口、Engine与G-04最终关闭（Task 31～42）

**Group branch:** `codex/g04-group4-engine-final`

**Group base:** G3合并并验证后的最新`2.0`完整SHA。

**Group rule:** Task 31～41不单独测试或复审；Task 42先执行Theme收口、Engine全部cluster、跨模块消费者、public API drift、targeted gate和Native AOT完整测试，再完成G4轻量复审；确认四组完成后，继续执行整个G-04唯一一次全量复审。

### Task 31: G-04D8 Theme decision consolidation

**Purpose:** 汇总三项Theme终态并加入G4待验证集合；不在Task 31运行测试、AOT或复审。

**Acceptance:** Theme三项均有终态和行为断言，覆盖率不会因删除行为测试而退化。

---

### G-04D9：Engine

### Task 32: G-04D9 master eligibility audit

**Purpose:** 对56项候选建立完整semantic call graph，并固定下面八个原子cluster；本任务不修改可见性。

**Files:**
- Inspect: `src/Bukit-Core/Bukit.Engine/`
- Inspect: `src/Bukit-Core/Bukit.Cli/`
- Inspect: `src/Bukit-Core/Bukit.Rendering/`
- Inspect: `src/Bukit-Core/Bukit.Content/`
- Inspect: `tests/Bukit.Engine.Tests/`
- Inspect: `tests/Bukit.Architecture.Tests/`
- Create: `docs/analysis/bukit-core-g04d9-engine-master-eligibility-audit-2026-07-23.zh-CN.md`

**Required clusters and exact counts:**

1. D9A build orchestration：7。
2. D9B content validation/stage contracts：9。
3. D9C filesystem/output utilities：9。
4. D9D feed/SEO/sitemap generators：8。
5. D9E built-in plugins：13。
6. D9F Notion fetch integration：2。
7. D9G plugin source/capability：3。
8. D9H list/template capability helpers：5。

**Acceptance:** 56/56唯一归属，cluster总数严格等于56；没有把Engine.Abstractions稳定插件契约混入候选。

### Task 33: G-04D9A build orchestration graph

**Candidates:** `BuildOptions`、`BuildPipeline`、`BuildPipelineContext`、`BuildVariantSummary`、`ContentPipelineResult`、`RoutePipeline`、`RoutePipelineResult`。

**Files:** `BuildOptions.cs`、`BuildPipeline.cs`、`BuildResult.cs`、`ContentPipeline.cs`、`RoutePipeline.cs`及对应Engine/CLI tests。

**Resolution:** 先检查`SiteEngine`、CLI和test seam传播；不得把`BuildResult`等已分类稳定报告类型顺带收窄。

**Acceptance:** build orchestration、variant、cancel、report和route pipeline行为不变；断言进入G4最终测试清单。

### Task 34: G-04D9B content validation/stage contract graph

**Candidates:** `ContentCollectionContractValidator`、`ContentSchemaValidator`、`ContentValidationIssue`、`IContentProviderFactory`、`ITemplateRenderer`、`ContentStageInput`、`ContentStageOutput`、`IContentStage`、`TemplateRendererBase`。

**Files:** `ContentCollectionContractValidator.cs`、`ContentSchemaValidator.cs`、`IContentProviderFactory.cs`、`ITemplateRenderer.cs`、`Stages/IContentStage.cs`、`TemplateRendererBase.cs`及Engine/Content/Rendering tests。

**Resolution:** 区分真正extension seam与测试便利public；interface/base class若存在外部实现证据必须停止internalization。

**Acceptance:** validation issue shape、stage order、renderer contract和content factory行为不变。

### Task 35: G-04D9C filesystem/output graph

**Candidates:** `DirectoryCopy`、`DirectoryCopyOptions`、`FileWriter`、`Incremental.HashUtil`、`IOutputFileSystem`、`IOutputPathPolicy`、`OutputPathSecurityException`、`SafeOutputFileSystem`、`SafePathResolver`。

**Files:** `DirectoryCopy.cs`、`FileWriter.cs`、`Incremental/HashUtil.cs`、`Output/*.cs`及Engine security/output tests。

**Resolution:** 以F-01、F-03、F-04关闭契约为安全下界；不得借公共面治理重写路径比较、symlink或output ownership。

**Acceptance:** destructive path guard、collision、atomic write、hash和异常分类不变；安全审计项进入G-04最终全量复审清单。

### Task 36: G-04D9D feed/SEO/sitemap generator graph

**Candidates:** `AtomFeedGenerator`、`JsonFeedGenerator`、`RssGenerator`、`SitemapGenerator`、`SitemapGenerator.Alternate`、`SitemapGenerator.UrlEntry`、`SeoAlternatesService`、`SeoInjectionPolicy`。

**Files:** 对应generator/SEO源文件及Engine SEO/feed/sitemap tests。

**Resolution:** CLR visibility可以变化，但XML/JSON/HTML输出、URL canonicalization、locale alternates和external image audit边界不得变化。

**Acceptance:** feed、sitemap、SEO golden outputs无差异；通过AOT serialization。

### Task 37: G-04D9E built-in plugin graph

**Candidates:** `AliasPlugin`、`ArchivePlugin`、`DataFilesPlugin`、`FeedPlugin`、`ImageProcessingPlugin`、`LlmsTxtPlugin`、`MenuPlugin`、`PagesIndexPlugin`、`PaginationPlugin`、`RelatedContentPlugin`、`SearchIndexPlugin`、`SitemapPlugin`、`TaxonomyPlugin`。

**Files:** `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/*.cs`、plugin registry、Engine plugin tests和Architecture tests。

**Resolution:** 先证明AOT静态注册不依赖public构造可达性；所有13项作为registry-owned cluster处理，不改变插件顺序、capabilities或输出所有权。

**Acceptance:** built-in registration、ordering、derive/after-build行为、reports和AOT smoke不变。

### Task 38: G-04D9F Notion fetch integration graph

**Candidates:** `INotionPageFetcher`、`NotionFetchedPage`。

**Files:** `Plugins/BuiltIn/PagesIndexPlugin.cs`、Notion adapter和Engine/Content.Notion tests。

**Resolution:** interface与record必须原子处理；若外部测试或consumer实现interface，选择保留或明确迁移，不新增第二套Notion client。

**Acceptance:** page fetch、pagination、cancellation和PagesIndex输出不变。

### Task 39: G-04D9G plugin source/capability graph

**Candidates:** `BuiltInPluginSource`、`IPluginSource`、`PluginCapability`。

**Files:** `Plugins/PluginRegistry.cs`、`Plugins/PluginCapability.cs`、Engine plugin tests和AOT registration tests。

**Resolution:** 明确Core built-in plugin source与外部process protocol完全分离；不得把本任务扩张为通用CLR插件SDK。

**Acceptance:** AOT禁用动态assembly插件的CG-019边界和静态注册保持不变。

### Task 40: G-04D9H list/template capability graph

**Candidates:** `SpecialListRouteBuilder`、`TemplateCapabilitiesResolver.ListPageContentResolution`、`TemplateCapabilitiesResolver.TemplateCapabilityFlags`、`TemplateCapabilitiesResolver.TemplateFieldDeclaration`、`TemplateVariableWarning`。

**Files:** `SpecialListRouteBuilder.cs`、`TemplateCapabilitiesResolver.cs`、`ScribanTemplateLinter.cs`及Engine/Theme/Rendering tests。

**Resolution:** route/list/template三个行为面必须共同验证，但禁止修改模板字段名、route precedence或warning文本契约。

**Acceptance:** taxonomy/list routing、template capability detection和lint warning输出不变。

### Task 41: G-04D9 Engine decision consolidation

**Purpose:** 对56项逐一登记终态并形成G4待验证矩阵；不在Task 41运行测试、gate、AOT或复审。

**Files:**
- Create: `docs/analysis/bukit-core-g04d9-engine-final-aggregate-closure-2026-07-23.zh-CN.md`

**Acceptance:** 56/56均有证据终态；八个cluster没有遗漏或重复；所有跨assembly和AOT场景进入Task 42测试清单。

---

## 11. G-04最终关闭

### Task 42: G-04 Group 4 complete verification, light review, and final full review

**Purpose:** 先完成G4唯一一次完整测试和轻量复审；四个任务组全部完成后，再执行整个计划唯一一次覆盖42项及历史治理链的全量只读复审。

**Files:**
- Create: `docs/analysis/bukit-core-g04-final-aggregate-closure-audit-2026-07-23.zh-CN.md`
- Review: all G-04 reports, plans, commits, current baseline, historical manifest, compatibility docs and tests

**Steps:**

- [ ] 建立G-04A、B、C、D1～D9完整提交和决策索引。
- [ ] 对历史136个候选逐项标记：internalized/removed、retained-public、migrated、superseded或blocked；总数必须精确为136。
- [ ] 对当前public API baseline重新统计assembly、owner、classification和compatibility。
- [ ] 验证历史consumer manifest内容和Git blob未被后续实施重写。
- [ ] 检查所有breaking CLR变化都有2.0迁移说明、明确批准和所属任务组验证证据。
- [ ] 执行一次G4 aggregate targeted gate；full/release仍需另行授权。
- [ ] 执行Core+Labs+官方插件编译、真实Native AOT publish/package smoke和公共API drift。
- [ ] 对G4 `GROUP_BASE..HEAD`执行一次轻量复审，确认没有阻断项。
- [ ] 确认G1～G4完整测试和轻量复审均已关闭。
- [ ] 对G-04A～Task 42完整决策链执行唯一一次全量只读aggregate diff审计，严重度必须为`0 / 0 / 0`。
- [ ] 明确判定AD-04为closed、partially closed或retained-by-design；不得仅因候选数量减少而关闭。

**Acceptance:**

- 136/136历史候选全部可追溯。
- 当前baseline无未解释drift、无悬空candidate状态。
- 0 schema、plugin protocol、config、asset URL、path/security或persisted-format越界修改。
- 所有环境阻塞与真实回归分开记录，没有把未执行测试写成通过。
- AD-04关闭结论有当前源码、测试、AOT和消费者证据支持。

**Group tests:**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --no-restore
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore
```

---

## 7. 任务组执行顺序与并行边界

严格任务组顺序：

```text
Group 1（Task 1～10）
  → Group 2（Task 11～20）
  → Group 3（Task 21～30）
  → Group 4（Task 31～42）
```

同一任务组内部按任务编号顺序实施，可以并行进行不写入相同文件的只读源码调查、消费者证据刷新和测试设计。组内不得提前运行正式测试或复审。任务组之间必须串行；下一组只能在前一组完整测试通过、轻量复审无阻断项并合并回`2.0`后开始。

## 8. 任务组分支、提交与复审协议

任务组固定使用以下分支：

- `codex/g04-group1-pluginhost-content-a`
- `codex/g04-group2-content-b-shared-cli`
- `codex/g04-group3-rendering-routing-theme`
- `codex/g04-group4-engine-final`

每个任务组至少包含：

1. 一个group design/eligibility commit；
2. 内部任务按候选图形成的production/test/baseline commits；
3. 一个group verification ledger commit；
4. 一个group light-review blocker修复commit（仅在轻量复审发现阻断项时）；
5. 一个merge后组级定向验证记录。

允许同组多个owner更新baseline，但每个提交仍只能包含一个清晰传播图，不能把整个任务组压成一个不可审计的production commit。轻量复审只确认组级完整测试、授权范围和阻断风险；逐文件质量、跨组影响和42项状态投影统一留给计划结束后的全量复审。

## 14. 停止与止损条件

出现以下任一条件，当前收窄任务停止并转为保留/迁移决策：

- 新的直接或间接CLR消费者证据；
- protected继承、interface实现、delegate callback、reflection、serialization、source generator或AOT root绑定；
- 需要修改受支持CLI/config/theme/template/report/plugin protocol才能完成收窄；
- 需要一次修改多个owner才能保持编译；
- 测试只能通过删除行为断言或扩大friend access；
- Native AOT、Core/Labs/官方插件构建证据不可取得；
- public面维护收益不足以覆盖迁移和验证成本。

止损不是失败。保留public并明确“非通用SDK、retained-by-design”是合法终态；G-04的目标是消除不清晰的公共面责任，而不是追求最小public计数。
