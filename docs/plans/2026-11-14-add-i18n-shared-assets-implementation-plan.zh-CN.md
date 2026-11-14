# 多语言共用资源（`add-i18n-shared-assets` v2）：实施方案与任务计划

- 状态：**待实施**。proposal.md 中的决策 A1–A5 **尚未确认**；本计划按推荐选项编写（A1 默认关闭、A2 默认语言归属、A3 只支持根目录布局、A4 非资源输出照常按语言输出、A5 媒体另立提案）。决策确认后，实施作为单独的 Core 任务在用户明确开始后进行。
- 日期：2026-11-14
- 依据：
  - 规范性提案 `openspec/changes/add-i18n-shared-assets/`（proposal、design、specs、tasks），v2。
  - 2026-11-14 按 `main` 对 v1 所做的逐条代码核查（结论已写入 design.md "Context" 和 "Alternatives"）。
- 代码基线：`e7651fb1`（main，本计划写作时）。
- 范围：只覆盖 Core 的实现、测试和文档。commit、push、PR、发布、full/release gate 均需另行授权。

---

## 0. 先读这一段

1. **决策门。** A1–A5 未确认前不开工。确认结果与推荐不同时，先按第 8 节修改提案和本计划，再开工。
2. **先做纯重构。** 阶段 1 把尺寸规划与变体命名收敛为一个纯函数，对外行为不变，单独提交。它是阶段 5 只读映射的前提，也可以脱离本提案单独合入。
3. **功能默认关闭，每个阶段结束时默认输出都不变。** 新行为只在 `sharedAssets.enabled: true`、2 种及以上语言、根目录布局时出现。所以阶段 2–6 的每个提交都不影响现有站点，可以在任意阶段边界暂停。
4. **按依赖顺序推进**：配置（阶段 2）→ 资源计划、构建顺序、模板变量、渲染哈希（阶段 3）→ Core 拼出的地址（阶段 4）→ 图片（阶段 5）→ Worktree（阶段 6）。阶段 3 结束时，开启共用已经能让 CSS/JS 正确引用根目录资源，但 SEO 图片、媒体占位图和 WebP 改写要到阶段 4、5 才正确，相关测试按阶段补齐。
5. **单写者。** 同一时刻只有一个实施者写仓库（AGENTS.md）。调用方检索、测试清单这类只读调查可以并行。
6. **提交边界**：阶段 1–7 各一个提交，阶段内按任务顺序执行和记录，阶段末提交（提交需授权）。阶段 0、8 不产生代码提交；阶段 0 的闭包策略修改单独提交。

## 1. 实施方案

### 1.1 组件

| 组件 | 位置 | 可见性 | 职责 |
|---|---|---|---|
| `SharedAssetsConfig`（`Enabled`、`Paths`） | `Bukit.Config/AppConfig.cs`，挂在 `SiteConfig.SharedAssets` | public（新增公共类型） | `site.sharedAssets` 的配置模型 |
| `SharedAssetScope` | `Bukit.Engine/SharedAssetScope.cs`（新文件） | internal | 由 `AppConfig` 和语言列表算出有效共用集合；提供 `Contains(relativePath)`；不满足启用条件时返回 `null` |
| `ImageVariantPlan` | `Bukit.Engine/Plugins/BuiltIn/ImageProcessing/ImageVariantPlan.cs`（新文件） | internal，顶层，非插件 | 尺寸规划（生成文件 / 候选）、命名、反解析的纯函数 |
| `BuildVariantContext.SharedAssets`、`.SharedAssetsRoot` | `Bukit.Engine/BuildVariantContext.cs` | internal | 开启共用时，所有语言都拿到 scope 和共用资源根（输出根） |
| `AssetPipelineContext.OmittedSharedAssets` | `Bukit.Engine/AssetPipeline.cs` | internal | 只对非默认语言赋值，`AssetOutputPlan.Create` 据此过滤 |
| `SharedAssetUrls`（根 baseUrl + scope） | `Bukit.Engine/`（新 record，或并入 `SharedAssetScope`） | internal | SEO 地址和占位图地址选择 baseUrl |
| `BuildDataKeys.SharedAssets` | `Bukit.Engine/BuildDataKeys.cs` | internal | 把共用资源根和 scope 交给 `ImageProcessingPlugin` |
| `I18nWorkerRequest.SharedAssetsRoot` | `Bukit.Engine/I18nWorktreeProtocolModels.cs` | internal | Worker 拿到 staging 根目录 |
| `SiteModel.AssetBaseUrl` → `site.asset_base_url` | `Bukit.Rendering/Models.cs`、`ScribanSiteModelMapper` | public 属性 | 模板变量 |

`SharedAssetScope` 是有效共用集合的**唯一实现**：配置校验之外，计划过滤、地址规则、图片插件和 Worktree 都只调用它，不各自拼前缀。

### 1.2 数据流（开启共用，`languages: [en, zh-CN]`，默认 `en` 在根目录）

```
SiteEngine / WorktreeBuildRun
  ├─ SharedAssetScope.TryCreate(config, languages)  → scope（null 则走现有流程）
  ├─ 第一段：只构建 en（输出目录 = dist/）
  │    资源计划不过滤 → 写 dist/assets/**、tokens、static/assets/**
  │    AfterBuild 照常生成 dist/assets/** 的变体与 freshness，归属记在 en 的 manifest
  └─ 第二段：按 languageJobs 并发构建 zh-CN（输出目录 = dist/zh-CN/）
       资源计划去掉 scope 内的 Assets/Tokens/Static 复制和生成条目
       模板：site.asset_base_url = 根 baseUrl
       SEO / 占位图：路径在 scope 内 → 根 baseUrl；占位图存在性查 dist/
       AfterBuild：自身映射 ∪ 只读共用映射（读 dist/assets/** 已有且 freshness 有效的 WebP）
```

### 1.3 实现要点与陷阱

1. **tokens 不经过计划写文件。** `AssetPipeline.GenerateTokensAsync` 只看 `tokens is not null` 就写 `assets/css/theme-tokens.css`（`AssetPipeline.cs:109-112,190`），从计划中删掉 Tokens 条目并不能阻止写出。必须改为"计划中有 Tokens 条目才生成"，否则非默认语言仍会写出 tokens，而且这个文件不在它的 manifest 里。
2. **过滤位置。** 在 `AssetOutputPlan.Create` 中，于 `FileWriter.GetSafeFullPath` 和 `Validate` 之前过滤（`AssetOutputPlan.cs:131-141`）。只删 `Category ∈ {Assets, Tokens, Static}` 且 `Operation ∈ {Copy, Generate}` 的条目。渲染型 static 是 `Operation.Render`，媒体是 `Category.Media`，都不受影响。
3. **非默认语言的旧副本。** `clean: false` 时，非默认语言上次归属的 `<lang>/assets/**` 会由现有的 `BuildManifestTracker.PrepareAssetPlanOutputs`、`TrackAssetPlanOutputs` 和 `PublicOutputLifecycle.PrepareProjectionWrites` 删除。这里依赖现有行为，必须用测试固定（T03.6）。
4. **默认语言先构建。** `SiteEngine.cs:333-375` 现有的语义是"默认语言总会跑完，其他语言在出错后提前停止"，并且 `results[i]` 按原索引存放。开启共用时，先用同一个 lambda 单独跑默认语言，失败则直接进入现有的失败选择（`SelectPrimaryFailure`）；成功后对其余语言执行现有的 `Parallel.ForEachAsync`。保持索引和日志事件不变。
5. **渲染哈希保持稳定。** `SiteIdentityAndModesContributor` 对非默认值才写入哈希（如 `defaultLanguageInSubdir`）。共用设置照此处理：只有开启且确实生效（多语言、根目录布局）时才追加有效前缀集合。关闭时现有站点的哈希不变，不会触发全量重渲染。
6. **SEO 地址。** `BuildMaybeAbsoluteUrl` 共有 8 个调用点：`SeoModelBuilder.cs:141`，`SeoImageResolver.cs:26,36,43`，`SeoJsonLdBuilder.cs:310,315,499,707`。新增一个带 `SharedAssetUrls?` 参数的重载，原签名转调它并传 `null`。站内相对地址去掉开头的 `/` 后落在 scope 内时，使用根 baseUrl。实施时先确认 SEO 构建链能拿到 `BuildVariantContext.RootBaseUrl`；拿不到时，把 `SharedAssetUrls` 放进 `VariantSeoStage` 的输入，不要从变体配置里反推根 baseUrl，因为变体配置的 `Site.BaseUrl` 已被改写成语言 baseUrl（`SiteEngine.cs:348-355`）。
7. **占位图。** `ContentMediaOutput.RewriteUrl` 已能识别 `_rootBaseUrl + fallback` 形式的输入（`ContentMediaOutput.cs:197`）。需要改的是：非默认语言中占位图在 scope 内时，输出改为 `_rootBaseUrl + fallback`；`ValidateOutputs` 改为检查 `SharedAssetsRoot` 下的文件是否存在，不再要求它出现在本语言的计划输出中（`:126-131`）。
8. **WebP 映射的 key。** `GenerateWebpVariantsAsync` 返回的映射（`ImageProcessingPlugin.cs:110-123`）由 `RewriteCurrentHtmlWithWebp` 按 `TryNormalizeLocalImageUrl` 的结果查找。T05.2 先确认 key 的语义（输出相对路径还是 URL 路径），再让共用映射使用同一个 key 空间。不要为此新增第二套归一化逻辑。
9. **只读 freshness。** 共用映射只读文件并校验 sidecar（owner、相对路径、大小、sha256，见 `ImageVariantFreshness.cs:18-46,139-151`），不得写入、修复或删除。sidecar 中的相对路径以默认语言的输出目录为基准，在根目录布局下就是共用资源根。
10. **架构约束。**
    - `BuiltInPluginSplitTests` 要求 ImageProcessing 下的辅助类型保持顶层、internal、不是插件，`ImageVariantPlan` 也要遵守。
    - 新增 public 类型 `SharedAssetsConfig` 要把 `PublicApiBaselineFacts.GovernedTypeCount` 从 425 改为 426，并更新 `docs/governance/bukit-core-public-api-baseline.v1.json`。
    - `SiteModel` 新增属性同样要更新 API 基线。
11. **Worker 请求。** 在 `I18nWorkerRequest` 末尾追加可空的 `SharedAssetsRoot`。父进程和 Worker 是同一个二进制，不升 `i18n-worker-request.v1`。AOT 序列化依赖源生成的 JSON context，新增属性后要跑 AOT 编译确认（T08.2）。
12. **媒体前缀的归一化**复用 `ResponsiveImageHtmlTransform.NormalizeMediaUrlBase`，以及与 `ContentMediaOutput` 一致的有效媒体配置，不新写一份。Worktree 会强制把 `DownloadDir` 设为 `assets/uploads`（`I18nWorktreeBuild.cs:79,196`），但 `UrlBase` 不变，所以媒体前缀只取 `UrlBase`。

## 2. 约束与执行方式

- **开工前（阶段 0）**：
  - 用 `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包。目前未映射的有：本提案目录、本计划、`src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/BaseLayout.html` 和新增测试文件。在 `scripts/checks/codex-workflow-policy.v1.json` 中新增规则 `i18n-shared-assets-change`，参照 `unify-site-languages-change`，然后跑 `bash scripts/checks/codex-workflow-self-test.sh`。
  - 注意：现有规则 `i18n-default-language-root-change` 把 `I18nWorktreeProtocol` 过滤用在了 `Bukit.Cli.Tests` 上，但这个类在 `Bukit.Engine.Tests` 中，新规则不要照抄这个错误。
  - 用 `codex-workflow.py classify` 分类：`dotnet test` 归为 `dotnet-serial`；含 `languageJobs` 并发、Worktree 的测试单独运行。
  - 用 `codex-workflow.py queue init` 建立任务队列，实施者 acquire 后才写文件。
- **测试**：
  - 每个任务只跑所列专项测试，命令格式为 `dotnet test tests/<项目> -c Release --filter "<过滤>"`。过滤写成 `FullyQualifiedName~A|FullyQualifiedName~B`，下文简写为 `A|B`。
  - 新加的过滤先用 `--list-tests` 确认能选中测试。
  - 每个阶段末跑一次该阶段涉及的完整测试项目。
  - 不跑 `scripts/test-all.sh`、`scripts/smoke-all.sh`、整个解决方案的测试和 full/release gate，除非用户明确授权。
- **证据**：GREEN 结果用 `codex-workflow.py cache record` 缓存；`HEAD`、闭包、命令、环境、SDK 不变时复用。
- **审查**：每个阶段测完后做一次专项审查，只有严重或重要问题才回到实施。全部阶段完成后生成一次 `review-scope`，做一次只看增量的统一审查。
- **公共 API**：阶段 2、3 结束时跑 `bash scripts/checks/public-api-drift.sh check`，在引入漂移的任务内更新基线。
- **记录**：各阶段耗时、缓存命中、重跑、写者冲突用 `codex-workflow.py metrics add` 记录。

## 3. 总体顺序

| 阶段 | 内容 | 对外行为 | 任务 |
|---|---|---|---|
| 0 | 决策门、闭包、队列、基线测试 | 不变 | T00.1–T00.4 |
| 1 | 尺寸规划纯函数（纯重构） | 不变 | T01.1–T01.3 |
| 2 | 配置模型、校验、警告、`SharedAssetScope` | 新增可选字段；不开启时不变 | T02.1–T02.5 |
| 3 | 资源计划过滤、默认语言先构建、`asset_base_url`、渲染哈希 | **开启时**非默认语言不再复制共用资源 | T03.1–T03.6 |
| 4 | SEO 地址、媒体占位图 | 开启时 Core 拼出的地址指向根 | T04.1–T04.3 |
| 5 | 非默认语言只读复用共用 WebP 变体 | 开启时非默认语言页面得到 `<picture>` | T05.1–T05.4 |
| 6 | Worktree：默认语言 Worker 先行、请求下发共用资源根 | 与进程内构建一致 | T06.1–T06.3 |
| 7 | 文档、CHANGELOG、Starter 主题说明 | — | T07.1–T07.4 |
| 8 | 验证与统一审查 | — | T08.1–T08.3 |

## 4. 任务清单

格式：**编号 名称** — 改动 / 测试与完成标准 / 依赖。源码路径以 `src/Bukit-Core/` 为根，测试以 `tests/` 为根。

### 阶段 0：准备

**T00.1 决策确认**
- 做法：用户确认 A1–A5。结果与推荐不同时，按第 8 节修改提案和本计划，重新跑 `openspec validate add-i18n-shared-assets --strict --no-interactive`。
- 完成标准：提案的决策表标注确认日期。
- 依赖：无。

**T00.2 验证闭包与策略映射**
- 做法：按第 2 节新增 `i18n-shared-assets-change` 规则。
  - `matches`：`openspec/changes/add-i18n-shared-assets/*`、本计划、`src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/BaseLayout.html`、阶段 1–6 的新增测试文件。
  - `specialtyTests`：第 5 节各需求对应的测试命令，加上 `openspec validate`、`docs/size-policy.sh`、`docs/active-links.sh`。
  - 然后跑 `scripts/checks/codex-workflow-self-test.sh`。
- 完成标准：`closure` 没有未映射文件。这一项改的是验证配置文件，单独提交。
- 依赖：T00.1。

**T00.3 基线测试取证**
- 做法：在未改动的基线上跑以下测试，并 `cache record`：
  - `Bukit.Config.Tests`：`ConfigLoader|StrictYamlContract|ConfigSchemaNestedContract|ConfigContractDrift|ConfigJsonSchemaGenerator|ConfigPhase2Validator|MediaConfigDefaults`
  - `Bukit.Engine.Tests`：`AssetPipeline|VariantBuildPipeline|I18n|SiteEngineIntegrationTests|SiteEngineLoggerTests|SeoModelBuilder|SeoAuthorProfile|SeoPublisherJsonLd|SeoJsonLd|GeoSeoModelBuilder|MediaPlaceholderProbe|ImageProcessingPlugin|ImageSourceFingerprint|RenderDependency|IncrementalEngineIdentity|IncrementalRenderReads|RouteMetadataRendering`
  - `Bukit.Rendering.Tests`：`ScribanModelKnownFields|ScribanModelBinder|StarterThemeTemplateResource`
  - `Bukit.Cli.Tests`：`WorktreeBuildCoordinator|NotionOfflineSnapshotMultilingual`
  - `Bukit.Architecture.Tests`：完整项目
- 完成标准：全部通过。
- 依赖：T00.2。

**T00.4 任务队列**
- 做法：`queue init` 写入 T01.1–T08.3；实施者 acquire 后进入 `writing`。
- 依赖：T00.3。

**阶段 0 审查**：闭包是否覆盖第 1.1 节的每个组件文件及其直接消费方。

### 阶段 1：尺寸规划纯函数（行为不变）

**T01.1 新增 `ImageVariantPlan`**
- 改动：在 `Plugins/BuiltIn/ImageProcessing/` 新建顶层 internal static 类，提供以下成员：
  - `GeneratedWidths(sizes, sourceWidth)`：要生成的降尺寸回退文件宽度，不含原图宽度，对应现在 `ImageProcessingPlugin.cs:173` 的规则。
  - `CandidateWidths(sizes, sourceWidth)`：srcset / WebP 候选宽度，含原图宽度，对应 `ImageVariantGenerator.cs:288` 和 `ResponsiveImageHtmlTransform.cs:172-177`。
  - `VariantFileName(source, width, extension)`：生成 `{base}-{w}w{ext}`。
  - `TryParseVariantFileName(...)`：反解析变体文件名。
  - 现有的 `NormalizeSizes` 和 `TryReadImageWidth` 保留在原处，不搬动。
- 测试：新增 `tests/Bukit.Engine.Tests/ImageVariantPlanTests.cs`，覆盖两种规划、原图宽度等于或小于某个配置尺寸、重复尺寸、命名与反解析互逆、大小写扩展名。
- 依赖：T00.4。

**T01.2 调用方切换**
- 改动：以下位置全部改为调用 `ImageVariantPlan`，删除重复实现：
  - 尺寸规划：`ImageProcessingPlugin.cs:173,187`、`ImageVariantGenerator.cs:288`、`ResponsiveImageHtmlTransform.cs:172-177`。
  - 命名：`ImageVariantGenerator.cs:70,295`、`ImageProcessingPlugin.cs:273,284`、`ResponsiveImageHtmlTransform.cs:261-265`。
  - 反解析：`ImageVariantOwnership.cs:148-163,226-233,247-253`。
- 测试：`Bukit.Engine.Tests`：`ImageProcessingPlugin|ImageVariantPlan|SiteEngineIntegrationTests.MediaLifecycle_`；`Bukit.Architecture.Tests`：`BuiltInPluginSplit`。
- 完成标准：现有图片测试不改预期，全部通过。
- 依赖：T01.1。

**T01.3 阶段测试**
- 测试：`Bukit.Engine.Tests`、`Bukit.Architecture.Tests` 完整项目。
- 依赖：T01.2。

**阶段 1 审查**：全仓检索 `w{ext}`、`"w."`、`-{width}w` 等模式，确认没有遗漏的命名或规划实现；输出字节与基线一致。

### 阶段 2：配置

**T02.1 配置模型与读取**
- 改动：
  - `AppConfig.cs` 新增 `public sealed record SharedAssetsConfig { bool Enabled = false; IReadOnlyList<string>? Paths }`，并新增 `SiteConfig.SharedAssets` 属性。
  - `SiteDefaultsApplier` 读取 `site.sharedAssets`。
  - `ConfigStrictFieldValidator`：`SiteKeys` 加 `sharedAssets`，再为 `RequireOnly` 加 `enabled`、`paths`。
  - `ConfigJsonSchemaGenerator.SiteSchema` 加入对应 schema。
  - 公共 API：基线加入新类型和新属性，`GovernedTypeCount` 425 → 426。
- 测试：
  - `Bukit.Config.Tests`：`ConfigLoader|StrictYamlContract|ConfigSchemaNestedContract|ConfigJsonSchemaGenerator|ConfigContractDrift`，新增"缺省值"、"读取 `paths`"、"未知子字段报错"三个用例。
  - `Bukit.Architecture.Tests` 完整项目。
  - `public-api-drift.sh check`。
- 依赖：T01.3。

**T02.2 配置校验**
- 改动：在现有的跨节校验入口（`ConfigPhase2Validator` 覆盖的位置）加入：
  - 空列表、绝对路径、含 `..`、规范化后为根目录的条目报错。
  - 条目等于或位于媒体前缀之内时报错，提示媒体共用不受支持。
  - `enabled: true` 且 `defaultLanguageInSubdir: true` 时报错，信息中同时写出两个字段名。
  - 规范化规则（`\`→`/`、去开头 `/`、补结尾 `/`）只实现一次，供校验和 `SharedAssetScope` 共用，可以放在 `Bukit.Config` 的 internal helper 中，用 `InternalsVisibleTo` 或公开的静态方法提供，以现有工程的可见性约定为准。
- 测试：`Bukit.Config.Tests`：`ConfigPhase2Validator`，新增上述每条规则的正反用例。
- 依赖：T02.1。

**T02.3 忽略警告**
- 改动：在 `I18nLanguagePolicy` 中，与 `WarnLayoutSettingsWithoutLanguages` 并列新增 `WarnSharedAssetsWithoutLanguages`：少于 2 种语言且 `Enabled` 时记录 `event=i18n.config.shared_assets_ignored`。在 `SiteEngine.cs:161` 一并调用。
- 测试：`Bukit.Engine.Tests`：`SiteEngineLoggerTests`，`languages: [en]` 加 `enabled: true` 时出现警告，输出与不写 `sharedAssets` 相同。
- 依赖：T02.2。

**T02.4 `SharedAssetScope`**
- 改动：新建 `Bukit.Engine/SharedAssetScope.cs`：
  - `TryCreate(AppConfig config, IReadOnlyList<string> languages)`：同时满足"已开启、2 种及以上语言、根目录布局"时返回实例，否则返回 `null`。
  - 有效前缀 =（配置的 `paths`，或默认的 `assets/` 加 SCSS 输出目录）减去媒体前缀。SCSS 输出目录为根目录时不纳入，并记录警告。
  - `Contains(relativePath)` 使用 `OutputDestinationIdentityComparer` 的大小写规则。
- 测试：新增 `tests/Bukit.Engine.Tests/SharedAssetScopeTests.cs`，覆盖默认推导、自定义 `paths`、`assets/` 包含媒体前缀时内部扣除、`urlBase: /media` 时不扣除、大小写去重、SCSS 输出在根目录、单语言或带前缀布局返回 `null`。
- 依赖：T02.3。

**T02.5 阶段测试**
- 测试：`Bukit.Config.Tests`、`Bukit.Architecture.Tests` 完整项目；`Bukit.Engine.Tests` 的 `SharedAssetScope|SiteEngineLoggerTests|I18nLanguagePolicy`；`public-api-drift.sh check`。
- 依赖：T02.4。

**阶段 2 审查**：校验和 scope 是否共用同一套规范化；默认推导是否与规范的"Default"场景逐条一致；不开启时配置对象之外没有任何行为变化。

### 阶段 3：资源计划、构建顺序、模板变量、渲染哈希

**T03.1 上下文接线**
- 改动：
  - `BuildVariantContext` 新增可选参数 `SharedAssets`（`SharedAssetScope?`）和 `SharedAssetsRoot`（`string?`）。
  - `SiteEngine` 多语言分支计算一次 scope；开启时为每种语言传入 scope 和 `outputDir`（输出根）。
  - `AssetPipelineContext` 新增 `OmittedSharedAssets`，由 `VariantRenderAssetPlanner` 只为非默认语言赋值（用 `I18nOutputLayout.IsRootLanguageVariant` 判断）。
- 测试：`Bukit.Engine.Tests`：`VariantBuildPipeline`。
- 依赖：T02.5。

**T03.2 计划过滤与 tokens**
- 改动：
  - `AssetOutputPlan.Create` 按第 1.3 节第 2 条过滤。
  - `AssetPipeline.ExecutePreparedAsync` 改为计划中存在 Tokens 条目时才调用 `GenerateTokensAsync`（第 1.3 节第 1 条）。
- 测试：`Bukit.Engine.Tests`：`AssetPipeline`，新增以下用例：
  - 非默认语言计划不含 `assets/**`、tokens、`static/assets/**`。
  - 渲染型 static 和媒体条目保留。
  - 未开启时计划不变。
  - tokens 在被过滤时不写文件。
- 依赖：T03.1。

**T03.3 默认语言先构建**
- 改动：`SiteEngine.cs:333-375` 按第 1.3 节第 4 条拆成两段；未开启时代码路径不变。
- 测试：`Bukit.Engine.Tests`：`SiteEngineIntegrationTests`，放在现有 `languageJobs` 测试旁边：
  - `languages: [zh-CN, en]`、默认 `en`、`languageJobs: 2` 时，`en` 的 `build.variant.done` 早于 `zh-CN` 的 `build.variant.start`。
  - 默认语言失败时，其他语言没有 `build.variant.start`，正式输出不变。
  - 现有的 `BuildAsync_LanguageJobs_ProducesIdenticalOutputRegardlessOfConcurrency` 在开启共用时同样成立。
- 依赖：T03.2。

**T03.4 `site.asset_base_url`**
- 改动：
  - `SiteModel` 新增 `AssetBaseUrl`。
  - `VariantDataSitePlanner.BuildSiteModel` 增加参数：开启时传 `RootBaseUrl`，否则传语言 baseUrl。
  - `ScribanSiteModelMapper` 输出 `asset_base_url`，规范化形式与 `base_url` 相同。
  - `ScribanModelKnownFields` 登记新字段。
  - 更新 API 基线。
- 测试：
  - `Bukit.Rendering.Tests`：`ScribanModelKnownFields|ScribanModelBinder`。
  - `Bukit.Engine.Tests`：`VariantBuildPipeline|RouteMetadataRendering`，新增开启 / 关闭 × 根目录 baseUrl / `/repo` 四个组合。
- 依赖：T03.3。

**T03.5 Starter 主题与渲染哈希**
- 改动：
  - `Bukit.Cli/Resources/StarterTheme/BaseLayout.html` 的资源引用改用 `site.asset_base_url`，保留第 2 行把 `/` 映射为空串的写法。
  - `SiteIdentityAndModesContributor` 按第 1.3 节第 5 条追加共用设置。
- 测试：
  - `Bukit.Rendering.Tests`：`StarterThemeTemplateResource`。
  - `Bukit.Engine.Tests`：`RenderDependency|IncrementalEngineIdentity`，覆盖未开启时哈希与基线相同、开启后变化、前缀集合顺序不影响哈希。
- 依赖：T03.4。

**T03.6 端到端与开关切换**
- 测试：在 `Bukit.Engine.Tests` 的 `I18nRootLayoutTests` 旁新增 `I18nSharedAssetsTests.cs`，覆盖规范中以下场景：
  - "Sharing not enabled"、"Sharing enabled"。
  - "Static under assets is shared"、"Other static files are not shared by default"、"Configured static prefix"、"Theme tokens shared"。
  - "Plugin output within the shared set"（用测试插件写 `assets/plugin-data.json`）。
  - "Media stays per language"。
  - "Asset URL on a non-default language page"、"Asset URL with sharing disabled"。
  - "Enabling / Disabling sharing on an incremental build"（`build.clean: false`；验证第 1.3 节第 3 条）。
  - 只改一种语言时，`dist/assets/**` 的修改时间和内容不变。
- 完成标准：阶段测试 `Bukit.Engine.Tests`、`Bukit.Rendering.Tests`、`Bukit.Architecture.Tests` 完整项目通过；`public-api-drift.sh check` 通过。
- 依赖：T03.5。

**阶段 3 审查**：
- 重点 1：非默认语言的 manifest 是否还残留共用路径。
- 重点 2：默认语言输出与未开启时是否逐字节相同。
- 重点 3：顺序拆分后，失败和取消语义是否与原实现一致。

### 阶段 4：Core 拼出的地址

**T04.1 SEO 地址**
- 改动：按第 1.3 节第 6 条新增重载；8 个调用点改为传入 `SharedAssetUrls`（未开启时为 `null`）。
- 测试：`Bukit.Engine.Tests`：`SeoModelBuilder|SeoAuthorProfile|SeoPublisherJsonLd|SeoJsonLd|GeoSeoModelBuilder`。新增用例：
  - 规范场景"Default SEO image on a non-default language page"。
  - `og_image` 在 scope 内与 scope 外各一个。
  - 作者图片在 scope 内。
  - 绝对 http 地址不变。
  - 未开启时与基线一致。
- 依赖：T03.6。

**T04.2 媒体占位图**
- 改动：按第 1.3 节第 7 条修改 `ContentMediaOutput.RewriteUrl` 和 `ValidateOutputs`；scope 和共用资源根从 `BuildVariantContext` 取得。
- 测试：
  - `Bukit.Engine.Tests`：`SiteEngineIntegrationTests.MediaLifecycle_|MediaPlaceholderProbe`。
  - 新增规范场景"Media fallback on a non-default language page"。
  - 新增"共用资源根缺少占位图时，非默认语言报出现有的占位图缺失错误"。
- 依赖：T04.1。

**T04.3 阶段测试与地址清单复核**
- 做法：全仓检索 `_baseUrl +`、`BaseUrl +`、`BuildAbsoluteUrl(` 等拼接点，确认没有其他把共用资源路径拼上语言 baseUrl 的位置；如有，补入本阶段。顺带确认 `SeoImageResolver.cs:36` 对 `record.Media` 的地址是否重复拼接 baseUrl。这个问题与本提案无关，有问题另行记录，不在本阶段修复。
- 测试：`Bukit.Engine.Tests` 完整项目。
- 依赖：T04.2。

**阶段 4 审查**：地址规则只依赖 `SharedAssetScope`，没有再写一份前缀判断；未开启时 SEO 输出与基线逐字节一致。

### 阶段 5：图片

**T05.1 插件输入**
- 改动：新增 `BuildDataKeys.SharedAssets`（共用资源根 + scope），只对非默认语言设置，设置位置与现有的 `BuildDataKeys.MediaDownloadDir` 相同。
- 测试：`Bukit.Engine.Tests`：`VariantBuildPipeline`。
- 依赖：T04.3。

**T05.2 只读共用映射**
- 改动：
  - 先按第 1.3 节第 8 条确认映射的 key 语义。
  - 在 `ImageVariantFreshness` 中新增只读校验方法，不写也不修复任何文件。
  - `ImageProcessingPlugin` 在非默认语言中，对共用资源根里落在 scope 内的原图：用 `ImageVariantPlan.CandidateWidths` 和 `VariantFileName` 推出 WebP 候选，只收录文件存在且 freshness 有效的候选，构建共用映射。
- 测试：`Bukit.Engine.Tests`：`ImageProcessingPlugin`，新增用例：
  - 候选全部存在。
  - 部分候选缺失：只收录存在的。
  - sidecar 与原图哈希不符：不收录。
  - sidecar 缺失：不收录。
- 依赖：T05.1。

**T05.3 合并改写与地址识别**
- 改动：
  - `RewriteCurrentHtmlWithWebp` 使用"自身映射 ∪ 共用映射"。
  - `ResponsiveImageHtmlTransform.TryNormalizeLocalImageUrl` 同时接受 `asset_base_url + "/"` 前缀，并解析到共用资源根中的文件。
  - 默认语言和未开启时的行为不变。
- 测试：`Bukit.Engine.Tests`：`ImageProcessingPlugin|I18nSharedAssets`，新增用例：
  - 规范场景"Shared theme image on two language pages"（`/repo`）。
  - 规范场景"Default language variant failure in warn mode"。
  - 已改写的 `<picture>` 再次经过改写时输出不变（复用现有的幂等测试方式）。
- 依赖：T05.2。

**T05.4 不写共用资源根**
- 改动：确认非默认语言 AfterBuild 的扫描、孤儿清理和 freshness 清理只作用于自己的输出目录（`FindSourceImages` 以 `context.OutputDir` 为根，见 `ImageProcessingPlugin.cs:307-331`）。
- 测试：新增用例：构建前后对 `dist/assets/**` 做文件快照（路径、大小、修改时间），非默认语言 AfterBuild 前后快照一致。阶段测试：`Bukit.Engine.Tests`、`Bukit.Architecture.Tests` 完整项目。
- 依赖：T05.3。

**阶段 5 审查**：
- 只读路径是否有任何写、删、`File.SetLastWriteTime` 调用。
- 共用映射与默认语言实际产物是否由同一个规划函数推出。
- `warn` 模式下是否会引用不存在的变体。

### 阶段 6：Worktree

**T06.1 请求字段**
- 改动：
  - `I18nWorkerRequest` 末尾追加 `string? SharedAssetsRoot`。
  - `WorktreeBuildRun.cs:334-353` 开启共用时传入 staging 根目录。
  - Worker 侧在 `I18nWorktreeBuild.cs:183-230` 构造 `BuildVariantContext` 时，按 `request.Config` 重新计算 scope，并传入 `SharedAssetsRoot`。
- 测试：`Bukit.Engine.Tests`：`I18nWorktree`。请求往返序列化时，新字段保留；缺失时还原为 `null`。
- 依赖：T05.4。

**T06.2 默认语言 Worker 先行**
- 改动：`WorktreeBuildRun.RunWorkersAsync` 在开启共用时先运行默认语言 Worker，成功后再对其余 Worker 执行现有的并发调度；失败与取消语义沿用现有实现。
- 测试：`Bukit.Cli.Tests`：`WorktreeBuildCoordinator`，新增"默认语言 Worker 完成后其他 Worker 才启动"和"默认语言 Worker 失败时其他 Worker 不启动"两个用例。
- 依赖：T06.1。

**T06.3 等价测试**
- 测试：
  - 新增 `WorktreeBuild_MatchesInProcessOutputs_SharedAssets`，仿照现有的 `WorktreeBuild_MatchesInProcessOutputs_Root`，覆盖规范场景"Worktree and in-process equivalence"：输出包含 WebP 主题图片、占位图和 `seo.defaultImage`，两种构建的相对路径集合和文件内容一致。
  - 阶段测试：`Bukit.Cli.Tests` 的 `WorktreeBuildCoordinator|NotionOfflineSnapshotMultilingual`，`Bukit.Engine.Tests` 的 `I18nWorktree`。
- 依赖：T06.2。

**阶段 6 审查**：Worker 与进程内构建是否走同一个 scope 构造和同一条过滤路径；请求字段在未开启时为 `null`，旧流程不变。

### 阶段 7：文档与契约

**T07.1 用户指南与 skill**
- 改动：
  - `guide/user/04-site-yaml-config.md` 和 `guide/dev/config-site-yaml.md`：新增 `site.sharedAssets`，写明字段、默认值、校验规则，以及只支持根目录布局。
  - `guide/user/11-i18n-seo.md`：新增"共用资源"一节，写明 `asset_base_url`、默认语言先构建、`languageJobs > 1` 时的耗时代价、媒体不共用。
  - `guide/skills/bukit-i18n` 和参数速查。
- 测试：`Bukit.Config.Tests`：`ConfigContractDrift`；`bash scripts/checks/docs/active-links.sh`、`bash scripts/checks/docs/size-policy.sh`。
- 依赖：T06.3。

**T07.2 主题作者说明**
- 改动：在 `guide/dev/theme.md` 中写明：
  - 共用资源用 `asset_base_url`，按语言变化的输出（页面、插件输出、媒体）用 `base_url`。
  - JS 运行时拼地址和 CSS 根绝对路径 `url()` 的注意事项。
  - 只支持根目录布局。
- 依赖：T07.1。

**T07.3 CHANGELOG**
- 改动：在 `CHANGELOG.md`、`CHANGELOG.zh-CN.md`、`CHANGELOG.ms.md` 的 Unreleased 下写明：
  - 新增 `site.sharedAssets`（默认关闭）和模板变量 `site.asset_base_url`。
  - Starter 主题改用 `asset_base_url`。
  - 开启共用后的布局变化：非默认语言不再有自己的 `assets/`。
- 依赖：T07.2。

**T07.4 openspec tasks 勾选**
- 改动：勾选 `openspec/changes/add-i18n-shared-assets/tasks.md` 中已取得证据的条目。
- 依赖：T07.3。

### 阶段 8：验证与统一审查

**T08.1 专项测试汇总**
- 测试：闭包给出的专项测试，即 `Bukit.Config.Tests`、`Bukit.Engine.Tests`、`Bukit.Rendering.Tests`、`Bukit.Architecture.Tests` 完整项目，以及 `Bukit.Cli.Tests` 的 `WorktreeBuildCoordinator|NotionOfflineSnapshotMultilingual`。未变化的结果复用缓存。
- 依赖：T07.4。

**T08.2 格式、契约与 AOT**
- 测试：
  - `bash scripts/checks/dotnet-format.sh`
  - `bash scripts/checks/code-analysis-ratchet.sh check`
  - `bash scripts/checks/public-api-drift.sh check`
  - `openspec validate add-i18n-shared-assets --strict --no-interactive`
  - 当前平台 AOT 编译：`bash scripts/build/native-aot.sh <version> osx-arm64 <output-root> Release`
  - Windows CRLF 下的格式风险：链式调用中的注释在 Windows 上会触发 IDE0055（见提交 `a7c232b7`）。新增代码避免在链式调用中写注释，Windows 结果由 CI 确认。
- 依赖：T08.1。

**T08.3 统一审查与收尾**
- 做法：`codex-workflow.py review-scope` 生成审查范围，做一次只看增量的统一审查；`metrics report` 汇总。
- 完成标准：没有严重或重要问题。full/release gate 只在用户授权后执行。
- 依赖：T08.2。

## 5. 规范追溯

| 规范要求（`specs/i18n-shared-assets/spec.md`） | 实现任务 | 测试任务 |
|---|---|---|
| Shared assets are opt-in for multilingual root-layout sites | T02.1–T02.3、T02.4 | T02.2、T02.3、T03.6 |
| The effective shared set is a set of output prefixes | T02.2、T02.4 | T02.2、T02.4、T03.6 |
| The default language owns shared assets | T03.1、T03.2 | T03.2、T03.6 |
| The default language builds first when sharing is enabled | T03.3、T06.2 | T03.3、T06.2 |
| Templates reference shared assets through asset_base_url | T03.4、T03.5 | T03.4、T03.5、T03.6 |
| Core-generated site URLs follow the shared set | T04.1、T04.2 | T04.1、T04.2 |
| Non-default languages reuse shared image variants read-only | T01.1–T01.2、T05.1–T05.4 | T01.1、T05.2–T05.4 |
| Shared asset settings affect incremental rendering | T03.5 | T03.5、T03.6 |
| Worktree builds share assets like in-process builds | T06.1–T06.2 | T06.3 |

## 6. 风险与回退

| 风险 | 出现位置 | 应对 |
|---|---|---|
| 非默认语言仍写出 tokens | T03.2 | 第 1.3 节第 1 条；用例断言 `dist/zh-CN/assets/css/theme-tokens.css` 不存在 |
| 拆分调度后失败或取消语义改变 | T03.3、T06.2 | 复用同一个 lambda 和 `SelectPrimaryFailure`；新增失败用例 |
| 渲染哈希变化导致所有现有站点全量重渲染 | T03.5 | 只在生效时追加；用例断言未开启时哈希与基线相同 |
| 遗漏某个 Core 拼地址的位置 | 阶段 4 | 8 个调用点逐一测试；T04.3 全仓检索 |
| 只读映射引用不存在的变体 | T05.2、T05.3 | 只收录存在且 freshness 有效的候选；`warn` 失败用例 |
| 非默认语言误写共用资源根 | T05.4 | 文件快照用例 |
| 公共 API 或类型计数漂移 | T02.1、T03.4 | 在引入的任务内更新基线和 `GovernedTypeCount` |
| Worktree 请求在 AOT 下序列化失败 | T06.1 | T08.2 的 AOT 编译；`I18nWorktree` 往返测试 |
| Windows 下的 IDE0055 | 各阶段 | 不在链式调用中写注释；由 CI 确认 |

每个阶段结束时代码都能编译、测试通过，而且未开启时行为不变，可以在任意阶段边界暂停。回退时按阶段倒序 revert：

- 阶段 5、6 可以单独撤回。撤回后开启共用时非默认语言页面没有 WebP `<picture>`，或 Worktree 输出与进程内构建不一致，此时应同时在文档中标注功能未完成。
- 阶段 3 撤回时，阶段 4–6 一并撤回。

## 7. 与 openspec `tasks.md` 的对应

| tasks.md 小节 | 本计划任务 |
|---|---|
| 0. 前置与核查 | T00.1（决策）、T04.3（地址清单与 `record.Media` 复核） |
| 1. 尺寸规划纯函数 | T01.1–T01.3 |
| 2. 配置 | T02.1–T02.4 |
| 3. 资源计划与构建顺序 | T03.1–T03.3 |
| 4. 模板变量与 Core 拼出的地址 | T03.4、T03.5、T04.1、T04.2 |
| 5. 图片处理 | T05.1–T05.4 |
| 6. 增量 | T03.5 |
| 7. Worktree | T06.1、T06.2 |
| 8. 测试 | T03.6、T06.3 及各任务内新增测试 |
| 9. 文档与契约 | T07.1–T07.3 |
| 10. 验证 | T08.1–T08.3 |

## 8. 决策与推荐不同时的调整

| 决策 | 改选 | 对本计划的影响 |
|---|---|---|
| A1 | a) 多语言根目录布局站点默认开启 | T02.1 默认值改为"生效条件满足时为 `true`"。第 0 节第 3 条"每个阶段默认输出不变"不再成立，阶段 3–6 必须合为一个提交，否则中间提交会在 SEO 图片、占位图、WebP 尚未完成时就改变默认输出。还要更新所有断言 `dist/<lang>/assets/` 的现有多语言测试，并在 CHANGELOG 中列为默认行为变化。 |
| A2 | b) 根目录归属层（v1） | 本计划不适用，需要按 v1 设计重写，并先解决 design.md "Alternatives"列出的问题。 |
| A3 | b) 两种布局都支持 | 带前缀布局下默认语言不在根目录，没有天然的归属方，需要 v1 的根目录归属层。建议另立提案，本计划保持只支持根目录布局。 |
| A4 | b) 非资源输出落在共用前缀下时报错 | 在 T03.2 之后新增一个任务：路由完成、资源计划过滤之前（`VariantBuildPipeline.cs:127` 之前），检查非默认语言的页面和渲染型 static；插件输出在提交前检查。T03.6 中"Plugin output within the shared set"场景的预期改为报错。 |
| A5 | b) 媒体作为本提案第二步 | 在阶段 6 之后追加媒体阶段，需要跨语言引用并集、根目录归属和 Worktree 预测，参照 design.md "Alternatives"中保留的 v1 内容另行编写任务。 |

## 修订记录

| 轮次 | 日期 | 内容 |
|---|---|---|
| 初稿 | 2026-11-14 | 依据提案 v2 和代码核查编写；9 个阶段、35 个任务；决策 A1–A5 按推荐选项编写，待确认 |
