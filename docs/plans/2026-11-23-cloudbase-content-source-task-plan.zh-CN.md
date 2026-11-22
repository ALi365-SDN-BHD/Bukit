# Bukit Core 内置 `type: cloudbase` 内容源：实施任务计划书

> **执行方式：** 按任务逐个执行。每个任务单独验证、单独提交，提交时一律带 pathspec。未获授权时，不 push、不开 PR、不发布，也不跑 full/release gate。
>
> **状态：** 草案，未批准。**G0 未通过前，T01 及之后的任务一律不得开始。**
>
> **依据：**
> - [实施方案](2026-11-23-cloudbase-content-source-implementation-design.zh-CN.md)：下文简称"方案"，D1～D14 指方案第 4 节的决策；
> - [可行性分析](../analysis/bukit-core-cloudbase-builtin-provider-feasibility-2026-11-22.zh-CN.md)：下文简称"分析文档"。
>
> **代码基线：** 提交 `52699535` 时的仓库内容。源码路径以 `src/Bukit-Core/` 为根，测试路径以 `tests/` 为根。
>
> **工作量：** 下文的行数和工期都是估算。

---

## 0. 执行规则（来自 `AGENTS.md`，先读）

1. **单写者。** 同一时刻只能有一个实施者写仓库。T00 创建队列；每个任务开始前用 `queue acquire` 取得写者槽，结束时转为 `done` 或 `blocked`。
2. **只读并行。** 调用方检索、测试清单、文档草稿可以由只读代理并行完成，报告写到 `/tmp/codex-reports/cloudbase-<task>-<topic>.md`；只读代理不能改仓库。
3. **工作区干净。** 每个任务开始前执行 `git status --short`，只能出现本任务的文件；暂存区里如果有别人的改动，先让所有者处理。提交一律写成 `git commit -m "…" -- <本任务路径>`。
4. **验证边界。**
   - 只跑 closure 输出的 specialtyTests，不跑 full/release gate、`scripts/test-all.sh`、`scripts/smoke-all.sh` 或整个解决方案的测试。
   - GREEN 证据用 `cache record` 记录；`cache check` 命中时直接复用，不重跑。
   - `dotnet-serial` 的测试串行运行，`fixture-exclusive` 的测试单独运行。
5. **审查频率。**
   - 每个实现任务结束后做一次专项审查，只有 Critical/Important 才回到实现；Minor 记录下来，不阻塞。
   - 全部任务结束后，用 `review-scope` 做一次只看增量的统一审查。
6. **失败边界。** 环境、权限或网络故障不构成修改无关代码的理由。同一个错误出现两次后，停止盲目重试，改为定向诊断。
7. **没有真实网络。** 自动化测试一律使用伪造的 HTTP 处理器。真实环境冒烟测试只在 T10 获得授权后进行。

## 1. 里程碑与依赖

```text
Phase 0（S1～S4 调研） ──> G0 决策 ──> T00
T00 ──> T01 配置 ──┬──> T04 Provider ──> T05 正文安全 ──> T06 换链与签名链接 ──> T07 隐私 ──> T08 Doctor ──> T09 文档 ──> T10 最终验证
T00 ──> T02 诊断码 ┤
T00 ──> T03 Transport ┘
```

T01、T02、T03 之间没有代码依赖，但因为单写者规则，必须**串行**执行（顺序是 T02 → T01 → T03，先定诊断码，后面的任务都要用）。只读的准备工作，比如 T03 的测试夹具整理，可以提前并行。

| 里程碑 | 任务 | 产出 | 估算 |
|---|---|---|---|
| M0 决策 | Phase 0、G0 | 调研结论，G0 记录 | 2～4 天（取决于消费方配合） |
| M1 基础 | T00、T02、T01、T03 | policy 规则、诊断码、配置契约、transport | 4～6 天 |
| M2 主体 | T04、T05、T06 | 能端到端构建 | 4～6 天 |
| M3 收口 | T07、T08、T09、T10 | 隐私、doctor、文档、最终验证 | 4～5 天 |
| 合计 | | | **约 14～21 个工作日**（估算，一名实施者） |

## 2. Phase 0 与 G0（不改仓库代码）

**负责人：** 方案负责人加消费方。**产出：** 调研结论，写进 T00 创建的 openspec proposal 的"调研结果"一节。

| 调研 | 内容 | 完成标准 |
|---|---|---|
| S1 | 数据形态；查询路径、分页、过滤语法；分页上限；系统字段清单；一份脱敏的响应样本 | 结论加样本 JSON，样本作为 T03/T04 的测试夹具 |
| S2 | 各种鉴权方式能否只读访问；`Tencent.CloudBase` 的 PublishAot 试编译（临时工程，不入库） | 鉴权矩阵；AOT 和裁剪警告数；复用 SDK 还是自己写的结论 |
| S3 | 私有文件临时链接的域名、签名参数名、`maxAge` 的单位和上限 | 结论加一个脱敏的链接样例 |
| S4 | 正文样本：是否含 HTML、内嵌 `cloud://`、相对链接 | 结论 |

**G0 决策记录**（方案第 2 节，G0-1～G0-6）：逐项写明"通过"或"不通过"、证据和负责人签字。G0-4 不通过且无人书面接受风险时，本计划终止，改走方案 C。

## 3. 任务

### T00：批准、policy、openspec 与队列

**前置：** G0 全部通过，并且用户明确授权这是一个独立的 Core 任务。

**Files：**
- Modify：`scripts/checks/codex-workflow-policy.v1.json`
- Create：`openspec/changes/add-cloudbase-content-source/`（`proposal.md`、`design.md`、`tasks.md`、`specs/cloudbase-content-source/spec.md`）
- Modify：本计划书和方案的"状态"行

**步骤：**

1. 工作区前置检查：

   ```bash
   git status --short
   git diff --cached --quiet || { echo "staged changes exist" >&2; exit 1; }
   ```

2. 在 policy 中新增三条规则：
   - `bukit-cloudbase-source`：匹配 `src/Bukit-Core/Bukit.CloudBase/**`；`publicContract: false`；specialtyTests 为 `dotnet test tests/Bukit.CloudBase.Tests/Bukit.CloudBase.Tests.csproj`；`resource: dotnet-serial`。
   - `bukit-cloudbase-tests`：匹配 `tests/Bukit.CloudBase.Tests/**`。
   - `cloudbase-content-source-change`：匹配 `openspec/changes/add-cloudbase-content-source/*`、对应的归档路径、本计划书和方案；specialtyTests 为 `openspec validate add-cloudbase-content-source --strict`。

   参照现有的 `bukit-notion-source`、`bukit-new-command-plan` 规则写法。
3. 编写 openspec 提案：
   - proposal：写 G0 记录和调研结果；
   - design：引用方案；
   - tasks：列出 T01～T10，作为唯一的勾选来源；
   - spec：用 Requirement/Scenario 写出方案第 1.3 节的 AC1～AC10。
4. 验证规则与闭包：

   ```bash
   bash scripts/checks/codex-workflow-self-test.sh
   BASE=$(git rev-parse HEAD); printf '%s\n' "$BASE" > /tmp/bukit-cloudbase-base
   changed=(); while IFS= read -r f; do changed+=("$f"); done < <(
     git diff --name-only "$BASE"; git ls-files --others --exclude-standard)
   args=(); for f in "${changed[@]}"; do args+=(--changed "$f"); done
   python3 scripts/checks/codex-workflow.py closure \
     --policy scripts/checks/codex-workflow-policy.v1.json "${args[@]}"
   ```

   Expected：self-test 通过，`unmappedFiles` 为空。

   注意：不要写成 `git diff | while ...` 的管道形式。管道里的 `while` 在子 shell 里执行，循环结束后 `args` 是空的。
5. 记录基线：先运行 closure 预计会涉及的测试项目，把 GREEN 结果用 `cache record` 记下来。
6. 创建队列：

   ```bash
   python3 scripts/checks/codex-workflow.py queue init --state /tmp/bukit-cloudbase-queue.json
   ```

7. 提交：

   ```bash
   git commit -m "spec: propose cloudbase content source" -- \
     scripts/checks/codex-workflow-policy.v1.json \
     openspec/changes/add-cloudbase-content-source docs/plans
   ```

**验收：** `openspec validate add-cloudbase-content-source --strict` 通过；closure 没有 unmapped 文件；队列已创建。

**估算：** 0.5～1 天。

---

### T02：诊断码

**Files：**
- Modify：`Bukit.Shared/DiagnosticCode.cs`
- Modify：`Bukit.Shared/DiagnosticCodeFormatter.cs`
- Modify：`docs/governance/bukit-core-public-api-baseline.v1.json`
- Modify：`docs/analysis/bukit-bukitjalil-consumer-contract-2026-11-15.zh-CN.md`（登记 `0Cxx` 区间）
- Test：`tests/Bukit.Shared.Tests/BuildDiagnosticCodeTests.cs`

**步骤：**

1. 执行 `queue acquire --task T02`，状态转为 `writing`。
2. 先写测试：在 `BuildDiagnosticCodeTests` 的区间表里加入 `["CloudBase"] = (0x0C01, 0x0CFF)`，并为方案第 6 节的每个码断言名称和值。这时测试应该失败。
3. 在 `DiagnosticCode` 里新增成员，`DiagnosticCodeFormatter` 补上对应的消息和分类。
4. 运行 public-api-drift，确认只有新增成员，然后更新基线：

   ```bash
   bash scripts/checks/public-api-drift-self-test.sh
   dotnet run --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj \
     --configuration Debug -- check docs/governance/bukit-core-public-api-baseline.v1.json "$PWD" Debug
   ```

5. 跑 closure 输出的测试（预计包含 `Bukit.Shared.Tests` 全量），用 `cache record` 记录。
6. 状态转为 `review_wait`，做专项审查。
7. 提交：

   ```bash
   git commit -m "feat(shared): add CloudBase diagnostic codes" -- \
     src/Bukit-Core/Bukit.Shared tests/Bukit.Shared.Tests \
     docs/governance/bukit-core-public-api-baseline.v1.json \
     docs/analysis/bukit-bukitjalil-consumer-contract-2026-11-15.zh-CN.md
   ```

**验收：** 区间测试通过；drift 只报告预期的新增成员，基线已更新；BukitJalil 契约已登记新区间。

**估算：** 约 50 行生产代码，0.5 天。

---

### T01：配置模型与校验

**Files：**
- Modify：`Bukit.Config/AppConfig.cs`（新增 internal 的 `CloudBaseConfig`，以及 `ContentSourceConfig.CloudBase`）
- Modify：`Bukit.Config/ConfigCollectionReader.cs`、`Bukit.Config/SiteDefaultsApplier.Content.cs`（新增 `ReadCloudBaseConfigFrom`）
- Modify：`Bukit.Config/ConfigStrictFieldValidator.cs`（白名单加入 `cloudbase`，新增 `ValidateCloudBase`）
- Modify：`Bukit.Config/ConfigValidator.cs`、`Bukit.Config/ProviderValidators.cs`（方案第 5 节的校验，以及 D8、D13）
- Modify：`Bukit.Config/ConfigJsonSchemaGenerator.cs`（枚举加入 `cloudbase`，新增 `CloudBaseSchema()`）
- Test：`tests/Bukit.Config.Tests/` 下的 `ConfigContractDriftTests`、`ConfigSchemaNestedContractTests`，以及新增的 `CloudBaseConfigTests`

**步骤：**

1. 执行 `queue acquire --task T01`。
2. 先写测试，覆盖下面这些情况：
   - 合法配置能正确读取；
   - 缺少必填项、越界、未知字段都失败；
   - YAML 里出现凭据字段失败；
   - `kind` 不是选定值失败；
   - `cacheMode` 不是 `off` 失败；
   - `bodyFormat` 不是 `markdown` 失败；
   - `fileFields` 非空但 `downloadToLocal: false` 失败；
   - `fileFields` 非空但 `skipHosts` 排除了临时链接域名失败。
3. 更新两个契约测试里的 `content.sources[]` 键集合，并增加 `content/sources[]/cloudbase` 的快照。
4. 实现读取、校验和 schema。`CloudBaseConfig` 必须是 internal（D1），schema 的 `fieldPolicy` 沿用 `whitelist|all`。
5. 跑 closure 输出的测试（预计 `Bukit.Config.Tests` 全量，加上 drift），用 `cache record` 记录。
6. 专项审查，然后提交：

   ```bash
   git commit -m "feat(config): add cloudbase content source config" -- \
     src/Bukit-Core/Bukit.Config tests/Bukit.Config.Tests
   ```

**验收：** AC1 的配置部分；public-api-drift 没有新增配置类型；`config schema` 输出包含 `cloudbase`。

**估算：** 300～400 行，1～1.5 天。

---

### T03：`Bukit.CloudBase` transport

**前置：** G0-4 已经选定鉴权方式，G0-5 已经选定 transport 方案。

**Files：**
- Create：`src/Bukit-Core/Bukit.CloudBase/Bukit.CloudBase.csproj`（不引用任何项目；`InternalsVisibleTo` 给 `Bukit.Content` 和 `Bukit.CloudBase.Tests`）
- Create：`Bukit.CloudBase/Transport/*`、`Bukit.CloudBase/Models/*`（含源生成的 JSON 上下文）
- Create：`tests/Bukit.CloudBase.Tests/`
- Modify：解决方案文件（`bukit-core.slnx`、`bukit-test.slnx`）
- Modify：`tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs`（或者新增 `CloudBaseBoundaryTests`，要求 `Bukit.CloudBase` 不引用任何项目）

**步骤：**

1. 执行 `queue acquire --task T03`。
2. 先写测试，使用伪造的 `HttpMessageHandler`：
   - 鉴权头的生成；
   - 网关域名按 `region` 选择；
   - 分页直到读完；
   - 429/5xx 按指数退避重试，次数到 `maxRetries` 为止；
   - 限速符合 `maxRps`；
   - 401/403/404 映射到对应的诊断码；
   - 响应体超过上限、畸形 JSON 都报 `CloudBaseResponseInvalid`；
   - 所有异常和日志文本里都不含凭据、签名和查询串（AC7、D14）。
3. 实现 transport。JSON 一律用源生成，以兼容 AOT；不使用反射序列化。
4. 如果 G0-5 选择复用 SDK：改为引入 `Tencent.CloudBase` 包，测试改用 SDK 的扩展点。同时要通过 `Directory.Packages.props` 的中央包管理，这会触发 `central-package-management` 规则。
5. 跑 closure 输出的测试，预计包括：
   - `Bukit.CloudBase.Tests`；
   - `Bukit.Architecture.Tests`；
   - 如果改了 `Directory.Packages.props`，再加上相关项目。

   用 `cache record` 记录。
6. 专项审查（重点是安全），然后提交：

   ```bash
   git commit -m "feat(cloudbase): add CloudBase transport" -- \
     src/Bukit-Core/Bukit.CloudBase tests/Bukit.CloudBase.Tests \
     tests/Bukit.Architecture.Tests bukit-core.slnx bukit-test.slnx
   ```

**验收：** transport 测试全部通过；架构测试通过；`dotnet build` 没有裁剪或 AOT 警告。

**估算：** 600～800 行（复用 SDK 可以减少），2～3 天。

---

### T04：Provider、字段投影与工厂接入

**Files：**
- Create：`Bukit.Content/CloudBase/CloudBaseContentSource.cs`、`CloudBaseContentSourceOptions.cs`、`CloudBaseFieldProjector.cs`、`CloudBaseBodyStore.cs`
- Modify：`Bukit.Content/Bukit.Content.csproj`（引用 `Bukit.CloudBase`）
- Modify：`Bukit.Engine/ContentProviderFactory.cs`（新增 `cloudbase` 分支和 `CreateCloudBaseProvider`）
- Modify：`Bukit.Engine/ContentFieldReservations.cs`、`Bukit.Engine/ContentSchemaValidator.cs`（CloudBase 系统字段）
- Modify：`tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs`（`Bukit.Content` 的项目引用表加入 `Bukit.CloudBase`）
- Test：`tests/Bukit.Content.Tests/CloudBase/*`，以及 `tests/Bukit.Engine.Tests` 的端到端用例

**步骤：**

1. 执行 `queue acquire --task T04`。
2. 先写测试：
   - 字段映射：title、slug、publishAt、updatedAt；
   - 白名单和 `all` 模式；
   - 硬性剔除：`_openid` 等字段在 `all` 模式下也不出现（AC3）；
   - `maxItems` 截断；
   - `mode: data` 的行为；
   - 端到端：用伪造的 HTTP 构建一个站点（AC2）；
   - 没有 cloudbase 源的站点，构建产物逐字节不变。
3. 实现 provider。凭据通过 `SecretStore.Get` 读取；body store 先只做 Markdown 的普通渲染，安全模式在 T05 接入。
4. 跑 closure 输出的测试（预计 `Bukit.Content.Tests`、`Bukit.Engine.Tests`、`Bukit.Architecture.Tests` 全量），用 `cache record` 记录。
5. 专项审查，然后提交：

   ```bash
   git commit -m "feat(content): add CloudBase content provider" -- \
     src/Bukit-Core/Bukit.Content src/Bukit-Core/Bukit.Engine \
     tests/Bukit.Content.Tests tests/Bukit.Engine.Tests tests/Bukit.Architecture.Tests
   ```

**验收：** AC2、AC3。

**估算：** 500～800 行，2～3 天。

---

### T05：正文链接安全

**Files：**
- Modify：`Bukit.Content/Markdown/BasicMarkdownToHtml.cs`（新增"不可信链接"选项，默认关闭）
- Modify：`Bukit.Content/CloudBase/CloudBaseBodyStore.cs`（启用这个选项）
- Test：`tests/Bukit.Content.Tests/`

**步骤：**

1. 执行 `queue acquire --task T05`。
2. 先写测试，确认现状：用 Markdig 渲染 `[x](javascript:alert(1))`，断言输出里**包含** `javascript:`。这个测试同时完成分析文档第 10 节第 6 项的核实。
3. 再写目标测试：开启选项后，`javascript:`、`vbscript:`、`data:`、`//host` 这几种链接和图片被丢弃或降级为纯文本；`http`、`https`、`mailto`、`tel` 和以 `/` 开头的站内路径保留；**关闭选项时输出与现状完全一致**。
4. 实现：遍历 Markdig 语法树里的 `LinkInline`（包括图片），对每个 URL 调用 `Bukit.Notion.Rendering.RenderingSafeUrl.ForLink` 或 `ForMedia`（D9，通过现有的 `InternalsVisibleTo` 访问）。
5. 跑 closure 输出的测试，用 `cache record` 记录。
6. 专项审查（安全），然后提交：

   ```bash
   git commit -m "feat(content): filter unsafe link schemes in CloudBase markdown" -- \
     src/Bukit-Core/Bukit.Content tests/Bukit.Content.Tests
   ```

**验收：** AC4；Markdown provider 的默认行为不变（现有测试全部通过）。

**估算：** 50～100 行，0.5～1 天。

---

### T06：`cloud://` 换链、签名链接与媒体前置条件

**前置：** S3 已经给出临时链接的域名和参数名。

**Files：**
- Create：`Bukit.Content/CloudBase/CloudBaseFileResolver.cs`
- Modify：`Bukit.Content/Media/SignedUrlIdentity.cs`（新增 CloudBase 规则，D10）
- Modify：`guide/user/04-site-yaml-config.md`（签名方案表加一行）
- Test：`tests/Bukit.Content.Tests/`、`tests/Bukit.Engine.Tests/`（小程序签名链接拦截）

**步骤：**

1. 执行 `queue acquire --task T06`。
2. 先写测试：
   - 字段和正文里的 `cloud://` 被收集，每 50 个一批换链，替换回原文；
   - 部分失败时报 `CloudBaseFileResolveFailed`，失败的条目可以定位；
   - `SignedUrlIdentity.IdentityTarget` 去掉 CloudBase 的签名参数；
   - `MiniProgramJson.IsSignedUrl` 能识别 CloudBase 临时链接；
   - 构建产物里不含 `cloud://` 和签名（AC5）。
3. 实现。注意不要把临时链接写进任何持久化位置。
4. 跑 closure 输出的测试（这一步会命中 media-localization 相关规则，可能扩大测试范围），用 `cache record` 记录。
5. 专项审查，然后提交：

   ```bash
   git commit -m "feat(content): resolve CloudBase file ids and recognize signed links" -- \
     src/Bukit-Core/Bukit.Content tests/Bukit.Content.Tests tests/Bukit.Engine.Tests \
     guide/user/04-site-yaml-config.md
   ```

**验收：** AC5。

**估算：** 约 150 行加签名规则，1 天。

---

### T07：公开产物隐私

**Files：**
- Modify：`Bukit.Engine.Audit/PublicOutputPrivacyCheck.cs`
- Modify：`Bukit.Cli/Deploy/DeploymentPrivacyValidator.cs`
- Modify：`Bukit.Engine.Primitives/PublicContentProjectionPolicy.cs`
- Modify：`Bukit.Engine.Feeds/JsonFeedGenerator.cs`
- Modify（按需）：`Bukit.Engine.Audit/PublishUrlSnapshot.cs`，即 `PublishUrlSemanticHasher` 的归一化规则
- Test：`tests/Bukit.Engine.Tests/`、`tests/Bukit.Cli.Tests/`

**步骤：**

1. 执行 `queue acquire --task T07`。
2. 先写测试：
   - 公开 HTML、JSON Feed 或部署暂存目录里出现 envId 或 `cloud://`，检测会失败（AC6）；
   - 投影策略和 JSON Feed 会过滤 CloudBase 来源的敏感实体；
   - 如果正文里包含每次构建都会变的临时链接，语义哈希不应因此改变（只有确认需要时才改 hasher）。
3. 在现有的 Notion 逻辑旁边并列实现（D11），不做通用化重构。
4. 跑 closure 输出的测试，预计包括：
   - `Bukit.Engine.Tests`、`Bukit.Cli.Tests` 全量；
   - `core-report-inventory.py --self-test`；
   - `core-report-contracts` 规则对应的测试。

   用 `cache record` 记录。
5. 专项审查（隐私），然后提交：

   ```bash
   git commit -m "feat(audit): detect CloudBase identifiers in public output" -- \
     src/Bukit-Core/Bukit.Engine.Audit src/Bukit-Core/Bukit.Cli/Deploy \
     src/Bukit-Core/Bukit.Engine.Primitives src/Bukit-Core/Bukit.Engine.Feeds \
     tests/Bukit.Engine.Tests tests/Bukit.Cli.Tests
   ```

**验收：** AC6；不涉及 CloudBase 的站点，审计报告的内容不变。

**估算：** 200～300 行，1～1.5 天。

---

### T08：Doctor

**Files：**
- Create：`Bukit.Cli/Commands/DoctorCloudBaseChecker.cs`
- Modify：`Bukit.Cli/Commands/DoctorCommand.cs`
- Test：`tests/Bukit.Cli.Tests/`

**步骤：**

1. 执行 `queue acquire --task T08`。
2. 先写测试，使用伪造的 HTTP：
   - 连通性正常和失败；
   - 凭据缺失；
   - 401/403；
   - 集合或模型不存在；
   - 映射字段在样本记录中不存在；
   - 使用管理员级凭据时报 `CloudBaseAdminCredentialInUse` 警告（AC8）；
   - doctor 输出里不含凭据。
3. 参照 `DoctorNotionChecker` 和 `DoctorNotionSchemaChecker` 的结构实现。这两个文件只作参照，不改。
4. 跑 closure 输出的测试，用 `cache record` 记录。
5. 专项审查，然后提交：

   ```bash
   git commit -m "feat(cli): add CloudBase doctor checks" -- \
     src/Bukit-Core/Bukit.Cli/Commands tests/Bukit.Cli.Tests
   ```

**验收：** AC8。

**估算：** 150～250 行，1 天。

---

### T09：文档、CHANGELOG 与小改动

**Files：**
- Create：`guide/user/` 下新增 CloudBase 内容源用户文档（编号顺延，参照 `06-notion-content.md`）
- Modify：`guide/user/07-multi-source-data.md`、`16-parameter-cheatsheet.md`
- Modify：`guide/dev/config-site-yaml.md`
- Modify：`guide/skills/bukit-content-model/SKILL.md`
- Modify：`Bukit.Cli/SecretsStdinOption.cs`（帮助文本和示例加入 CloudBase 密钥名）
- Modify：`Bukit.Engine/OutputDirectoryCleaner.cs`（只改文案，可选）
- Modify：`CHANGELOG.md`、`CHANGELOG.zh-CN.md`、`CHANGELOG.ms.md`（记在 Unreleased 下）

**步骤：**

1. 执行 `queue acquire --task T09`。只读的文档准备代理可以提前起草，起草期间不得写仓库。
2. 编写文档，必须包含以下内容：
   - 鉴权方式和权限风险说明；
   - 管理员凭据警告；
   - `fileFields` 必须开启媒体本地化的说明；
   - P1 的限制：只支持 Markdown、不支持缓存、只支持一种数据形态。
3. 跑 closure 输出的检查，预计包括：
   - `docs/active-links.sh`、`docs/size-policy.sh`；
   - `config-docs-contract.sh`；
   - `cli-docs-sync.sh`（如果命中）；
   - `ConfigContractDriftTests`（它会检查文档里引用的字段）。

   用 `cache record` 记录。
4. 提交：

   ```bash
   git commit -m "docs: document CloudBase content source" -- \
     guide CHANGELOG.md CHANGELOG.zh-CN.md CHANGELOG.ms.md \
     src/Bukit-Core/Bukit.Cli/SecretsStdinOption.cs src/Bukit-Core/Bukit.Engine/OutputDirectoryCleaner.cs
   ```

**验收：** AC10 的文档部分。文档漏写不会被自动检查发现，所以要在 T10 的审查中逐项核对。

**估算：** 1～1.5 天。

---

### T10：OpenSpec 校验、最终验证与审查

**步骤：**

1. 执行 `openspec validate add-cloudbase-content-source --strict`，然后把 `tasks.md` 的勾选同步到实际进度。
2. 以 T00 记录的 BASE 计算全部变更，运行 closure，按输出**逐条**执行 specialtyTests 的并集。`cache check` 命中时直接复用，不要为了刷新证据重跑没有变化的测试。
3. Native AOT（**需要授权**）：

   ```bash
   bash scripts/build/native-aot.sh <version> osx-arm64 <output-root> Release
   ```

   用伪造配置跑 `config check`。如果没有授权，就用 `dotnet run` 做一次端到端，并在最终报告里写明"AOT 未验证"。
4. 真实环境冒烟测试（**需要授权，使用消费方的测试环境**）：运行 `doctor` 和 `build`，确认 AC5 和 AC6 在真实数据上成立，结果脱敏后记录。
5. 统一审查：

   ```bash
   python3 scripts/checks/codex-workflow.py review-scope \
     --evidence <evidence.json> --findings <findings.json> --changed <files...>
   ```

   只审查跨任务交叉点、失效的证据和未覆盖的文件；没有 Critical/Important 即结束。
6. 记录指标：

   ```bash
   python3 scripts/checks/codex-workflow.py metrics add --state /tmp/bukit-cloudbase-queue.json \
     --task T10 --phase test --duration-ms <n> --cache-status <hit|miss|none> --status completed
   python3 scripts/checks/codex-workflow.py metrics report --state /tmp/bukit-cloudbase-queue.json
   ```

7. 归档 openspec 变更，并把规格同步到 `openspec/specs/cloudbase-content-source/`。这一步单独提交，需要授权。

**验收：** AC1～AC10 全部满足；最终报告列出每条验收的证据、未验证的项目（例如 AOT、真实环境），以及记录下来的 Minor 问题。

**估算：** 1～2 天（不含等待授权的时间）。

## 4. 每个任务的固定收尾

每个实现任务（T01～T09）结束时，依次执行：

1. `queue transition --to testing`，跑 closure 输出的测试，执行 `cache record`。
2. `queue transition --to review_wait`，做专项审查。只有 Critical/Important 才回到 `writing`。
3. 带 pathspec 提交，然后 `queue transition --to done`。
4. 执行 `metrics add`，记录阶段耗时、缓存命中情况、是否重跑、是否发生写者冲突。

## 5. 风险与应对

| 风险 | 触发信号 | 应对 |
|---|---|---|
| G0-4 只能用管理员级凭据 | S2 的鉴权矩阵里没有只读路径 | 终止 A，改走 C；或者书面接受风险，再继续 |
| SDK 不兼容 AOT | S2 统计到裁剪或 AOT 警告 | 按 D3 自己写 transport |
| 临时链接参数无法识别 | S3 发现参数名与现有规则冲突 | T06 暂停，补充调研；不能放行签名链接 |
| closure 测试范围过大，耗时过长 | 单个任务测试超过 30 分钟 | 用 `cache check` 复用证据；`dotnet-serial` 的测试按顺序排队；不得因为耗时而截断测试 |
| 工作量超出估算 50% | metrics 显示偏差 | 停下来重新评估范围，必要时把功能移到 P2/P3 |
| 工作区里混入别人的改动 | `git status` 出现无关文件 | 停止，交给所有者处理；提交时用 pathspec 隔离 |

## 6. 验收清单

- [ ] G0-1～G0-6 已记录，并由负责人确认
- [ ] T00：policy 规则、openspec 提案、队列；closure 没有 unmapped 文件
- [ ] T02：诊断码；public API 基线；BukitJalil 契约
- [ ] T01：配置契约；两个契约测试已更新
- [ ] T03：transport；架构测试；没有 AOT 警告
- [ ] T04：provider；硬性剔除；端到端测试
- [ ] T05：链接协议白名单；默认行为不变
- [ ] T06：`cloud://` 换链；签名链接规则
- [ ] T07：隐私检测与过滤
- [ ] T08：doctor
- [ ] T09：文档与三语 CHANGELOG
- [ ] T10：closure 测试全部通过；AOT 和真实环境冒烟（已授权的部分）；统一审查；指标；openspec 归档
