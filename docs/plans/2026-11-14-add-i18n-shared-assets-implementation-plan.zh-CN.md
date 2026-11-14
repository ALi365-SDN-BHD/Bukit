# 多语言共用资源（`add-i18n-shared-assets` v2）：实施方案与任务计划

- 状态：**已实施（2026-11-14）**。决策 A1–A5 已于 2026-11-14 确认，全部采用推荐项：默认关闭、默认语言归属、只支持根目录布局、非资源输出照常按语言输出、媒体另立提案。阶段 1–8 已完成；最终证据见 openspec `tasks.md` 与阶段报告。
- 依据：`openspec/changes/add-i18n-shared-assets/`（proposal、design、specs、tasks）。设计理由、被放弃的方案和代码现状核查见 design.md，本计划只写"改什么、按什么顺序、怎么验收"。
- 代码基线：`e7651fb1`。正文用文件和符号定位，基线行号集中在附录 A，实施时以符号为准。
- 范围：Core 的实现、测试和文档。commit、push、PR、发布、full/release gate 均需另行授权。

---

## 0. 先读这一段

1. **先做纯重构。** 阶段 1 把尺寸规划与变体命名收敛为一个纯函数，行为不变，单独提交。它是阶段 5 只读映射的前提，也可以单独合入。
2. **功能默认关闭，每个阶段结束时默认输出都不变。** 新行为只在 `sharedAssets.enabled: true`、2 种及以上语言、根目录布局时出现，所以可以在任意阶段边界暂停。
3. **依赖顺序**：配置（2）→ 资源计划、构建顺序、模板变量、渲染哈希（3）→ Core 拼出的地址（4）→ 图片（5）→ Worktree（6）。阶段 3 结束时 CSS/JS 已能正确引用根目录资源，SEO 图片、占位图和 WebP 要到阶段 4、5 才正确。
4. **单写者。** 同一时刻只有一个实施者写仓库。只读的调用方检索、测试清单可以并行。
5. **提交边界**：阶段 1–7 各一个提交（需授权）；阶段 0 的闭包策略修改单独提交；阶段 8 不产生代码提交。

## 1. 实施方案

### 1.1 组件

| 组件 | 位置 | 可见性 | 职责 |
|---|---|---|---|
| `SharedAssetsConfig`（`Enabled`、`Paths`） | `Bukit.Config/AppConfig.cs`，挂在 `SiteConfig.SharedAssets` | public（新类型） | `site.sharedAssets` 的配置模型 |
| `SharedAssetScope` | `Bukit.Engine/SharedAssetScope.cs`（新） | internal | 有效共用集合的唯一实现，见第 1.3 节第 4 条 |
| `ImageVariantPlan` | `Plugins/BuiltIn/ImageProcessing/ImageVariantPlan.cs`（新） | internal，顶层，非插件 | 尺寸规划、命名、反解析的纯函数 |
| `BuildVariantContext.SharedAssets` | `BuildVariantContext.cs` | internal | 所有语言拿到同一个 scope |
| `AssetPipelineContext.OmittedSharedAssets` | `AssetPipeline.cs` | internal | 只对非默认语言赋值，`AssetOutputPlan.Create` 据此过滤 |
| `AssetOutputPlan.OmittedSharedItems` | `AssetOutputPlan.cs` | internal | 被过滤的条目，用作共用原图清单 |
| `RenderDependencyContext.SharedAssetPrefixes` | `Incremental/RenderDependencyContext.cs` | internal | 供渲染哈希使用的规范化前缀列表 |
| `BuildDataKeys.SharedAssets` | `BuildDataKeys.cs` | internal | 把 scope 和共用原图清单交给图片插件 |
| `I18nWorkerRequest.SharedAssetsRoot` | `I18nWorktreeProtocolModels.cs` | internal | Worker 拿到 staging 根目录 |
| `SiteModel.AssetBaseUrl` → `site.asset_base_url` | `Bukit.Rendering/Models.cs`、`ScribanSiteModelMapper` | public 属性，`string?` | 模板变量 |

除配置校验外，计划过滤、地址规则、图片插件、渲染哈希和 Worktree 都只调用 `SharedAssetScope`，不各自判断前缀。

### 1.2 数据流（开启共用，`languages: [en, zh-CN]`，默认 `en` 在根目录）

```
SiteEngine / WorktreeBuildRun
  ├─ SharedAssetScope.TryCreate(config, languages, outputRoot, logger)  → scope（null 则走现有流程）
  ├─ 第一段：只构建 en（输出目录 = dist/）
  │    资源计划不过滤 → 写 dist/assets/**、tokens、static/assets/**
  │    AfterBuild 照常生成 dist/assets/** 的变体与 freshness，归属记在 en 的 manifest
  └─ 第二段：按 languageJobs 并发构建 zh-CN（输出目录 = dist/zh-CN/）
       资源计划去掉 scope 内的 Assets/Tokens/Static 复制和生成条目
       模板：site.asset_base_url = 根 baseUrl
       SEO / 占位图：路径在 scope 内 → 根 baseUrl；占位图存在性查 dist/
       AfterBuild：共用原图 = 计划中被过滤的图片条目（不扫描磁盘）
                   只读共用映射 = 这些原图在 dist/ 中已有且 freshness 有效的 WebP
                   改写映射 = 自身映射 ∪ 共用映射（自身没有图片时也执行）
```

### 1.3 关键陷阱

1. **tokens 不经过计划写文件。** `AssetPipeline.GenerateTokensAsync` 只要 tokens 存在就写 `assets/css/theme-tokens.css`。必须改为"计划中有 Tokens 条目才生成"，否则非默认语言仍会写出一个不在自己 manifest 里的文件。
2. **过滤的位置与范围。** 在 `AssetOutputPlan.Create` 中、`FileWriter.GetSafeFullPath` 和 `Validate` 之前过滤，只删 `Category ∈ {Assets, Tokens, Static}` 且 `Operation ∈ {Copy, Generate}` 的条目；渲染型 static（`Render`）和媒体（`Media`）不受影响。`clean: false` 时非默认语言上次的 `<lang>/assets/**` 由现有的 manifest 跟踪和 `PublicOutputLifecycle.PrepareProjectionWrites` 删除，这一点依赖现有行为，用测试固定。
3. **默认语言先构建。** `SiteEngine` 现有语义是"默认语言总会跑完，其他语言出错后提前停止"，`results[i]` 按原索引存放。开启共用时先用同一个 lambda 单独跑默认语言，失败则进入现有的 `SelectPrimaryFailure`；成功后对其余语言执行现有的 `Parallel.ForEachAsync`。索引、日志事件、失败与取消语义都不变。Worktree 的 `RunWorkersAsync` 同理。
4. **`SharedAssetScope` 的语义。**
   - `TryCreate(AppConfig config, IReadOnlyList<string> languages, string outputRoot, ILogger? logger)`：已开启、2 种及以上语言、根目录布局时返回实例，否则返回 `null`。在站点层创建一次（`SiteEngine`；Worktree 父进程和 Worker 各一次），随 `BuildVariantContext` 下传。内部只调一次 `OutputDestinationIdentityComparer.ForOutputRoot`，因为它会在输出根写探针文件。`logger` 为 `null` 时不记录推导警告。
   - `Includes`：配置的 `paths`，或默认的 `assets/`，`theme.scss.enabled: true` 时再加 SCSS 输出目录（规范化后为根目录时不加入并警告）。`Excludes`：媒体前缀。
   - `Contains(relativePath)`：**命中任一 `Includes` 且不命中任一 `Excludes`**。不能实现成"前缀集合减去媒体前缀"再 `StartsWith`，否则 `assets/uploads/x.jpg` 会被误判为共用。
   - `ContainsSitePath(sitePath)`：给 SEO 和占位图用。去掉 query 和 fragment、去掉开头的 `/` 后调用 `Contains`，这样 `/assets/og.png?v=2` 也能命中。不做 URL 解码（见第 7 节）。
   - `CanonicalPrefixes`：规范化、排序后的 `Includes` 和 `Excludes`，供渲染哈希使用。媒体前缀变化而有效集合不变时也会触发重渲染，可以接受：媒体前缀变化本来就会改变页面中的媒体地址。
   - 另外暴露 `OutputRoot`（共用资源根）、`RootBaseUrl`（规范化的站点根 baseUrl）、`Comparer`。
   - 条目规范化（`\`→`/`、去开头 `/`、补结尾 `/`）只实现一次，供配置校验和 scope 共用。媒体前缀复用 `ResponsiveImageHtmlTransform.NormalizeMediaUrlBase` 和 `ContentMediaOutput` 的有效媒体配置，只取 `UrlBase`（Worktree 会改写 `DownloadDir`，但不改 `UrlBase`）。
5. **`asset_base_url` 与 Starter 主题。**
   - `SiteModel` 新增 `public string? AssetBaseUrl { get; init; }`，非 required：仓库里有 44 处 `new SiteModel`，required 会让它们和外部构造方编译失败。`ScribanSiteModelMapper` 输出 `model.AssetBaseUrl ?? model.BaseUrl`；`VariantDataSitePlanner.BuildSiteModel` 总是显式赋值。
   - Starter 主题的 `BaseLayout.html` 只把 `base_url` 的 `/` 映射为空串。改用 `asset_base_url` 时必须单独增加同样的映射，例如 `{{ asset_base_url = site.asset_base_url }}{{ if asset_base_url == "/" }}{{ asset_base_url = "" }}{{ end }}`，否则根站点会输出 `//assets/style.css`，未开启共用时也一样。
6. **渲染哈希。** contributor 拿不到 scope，所以 `RenderDependencyContext` 新增 `SharedAssetPrefixes`，经 `RenderDependencyHasher.ComputeSnapshot` 从 `VariantRenderStage` 传入；`SiteIdentityAndModesContributor` 只在列表非空时追加，未开启的站点哈希不变。`asset_base_url` 不单独入哈希：`SiteModel` 不会整体进入哈希，而它完全由已入哈希的 `site.baseUrl`、`site.language`、`site.defaultLanguage` 和共用集合推出；无条件追加会改变未开启站点的哈希。改由用例断言"根 baseUrl 变化时非默认语言哈希变化"。
7. **SEO 与占位图地址。**
   - scope 沿 `VariantSeoStage` → `SeoPipeline.Execute` → `SeoIndexBuilder.Build` → `SeoModelBuilder.BuildForContent` / `BuildForList` → `SeoImageResolver`、`SeoJsonLdBuilder.BuildJsonLd` 逐层传入，每层是可选参数、默认 `null`。`BuildMaybeAbsoluteUrl` 新增带 scope 的重载，`ContainsSitePath` 为真时用 `RootBaseUrl`，query 和 fragment 原样保留。不要从变体配置反推根 baseUrl，变体的 `Site.BaseUrl` 已是语言 baseUrl。
   - 占位图：`ContentMediaOutput.RewriteUrl` 在非默认语言中、占位图落在 scope 内时输出根 baseUrl + 占位图；`ValidateOutputs` 改查共用资源根，路径一律用 `FileWriter.GetSafeFullPath(scope.OutputRoot, relative)`，因为 `defaultImageUrl` 来自配置，属于信任边界。
   - 完整构建中，`ContentMediaOutput.NormalizeDocument` 和 `RewriteUrl` 会先把 `/assets/uploads/...` 改写为本语言媒体地址，SEO 和图片插件拿到的已是改写后的值。单元测试和完整构建测试的预期要分开写。
8. **共用原图清单与共用映射。**
   - 清单取自 `AssetOutputPlan.OmittedSharedItems` 中 `.jpg`、`.jpeg`、`.png` 的 `Destination`，**不扫描共用资源根**：默认语言生成的变体不会混进来，不需要读默认语言 manifest，也不会把用户自己的 `hero-480w.jpg` 误判成变体，进程内和 Worktree 得到的清单相同。
   - 设置时机：清单要到 `AssetPipeline.PrepareAsync` 之后才有值，不能放在设置 `MediaDownloadDir` 的路由阶段。在 `PrepareAsync` 之后、`VariantPluginStages.RunAfterBuildAsync` 之前，把清单和 scope 写入 `routePipelineResult.PluginContext`，AfterBuild 读到的是同一个 context。
   - key 与候选 URL：现有映射的 key 是站内 URL 路径 `{NormalizeBaseUrl(BaseUrl).TrimEnd('/')}/{sourceRelative}`，`RewriteCurrentHtmlWithWebp` 用 `TryNormalizeLocalImageUrl` 的结果查找。共用映射的 key 和候选 URL 共用一个前缀 `NormalizeBaseUrl(scope.RootBaseUrl).TrimEnd('/')`，只算一次。根 baseUrl 为 `/` 时 key 为 `/assets/hero.jpg`；不要直接拼 `RootBaseUrl + "/"`，否则会得到 `//assets/...`。也不能用 `GetRelativeIdentity(context.OutputDir, ...)`，会得到 `../assets/...`。
   - 原图宽度用 `ImageVariantGenerator.TryGetImageWidth`，它捕获坏图异常并记录 `image_processing.skip` 后返回 `false`，此时跳过该原图。不能用不捕获异常的 `TryReadImageWidth`。
   - 有效性先复用只读的 `ImageVariantFreshness.TryReadFreshness`（`outputDir` 传 `scope.OutputRoot`），再要求记录对当前源文件 `Matches`（源大小、mtime、SHA-256、quality、尺寸、格式、编码器）；源不可读时记录 `event=image_processing.skip`。共用映射不写入、修复或删除任何文件。
   - `AfterBuildAsync` 在本语言没有原图时会提前返回。共用映射要在提前返回之前构建；映射非空时仍执行 WebP 改写。
9. **根前缀分支必须检查 scope。** `TryNormalizeLocalImageUrl` 新增的"接受 `asset_base_url + "/"`"分支，只在剩余路径满足 `scope.Contains` 时成立。根站点的 `asset_base_url` 规范化后是空串，不加检查会把任何站内图片都当作共用资源。
10. **契约与工具约束。**
    - 配置校验的生产入口是 `ConfigValidator.Validate`，跨节规则放在这里才能同时读 `content.media.urlBase` 和 `theme.scss`。
    - `ImageVariantPlan` 加进 `BuiltInPluginSplitTests.Responsibility_IsTopLevelInternalAndNotAPlugin` 的 `InlineData`。
    - 新 public 类型使 `PublicApiBaselineFacts.GovernedTypeCount` 从 425 变为 426，并更新 `docs/governance/bukit-core-public-api-baseline.v1.json`；`SiteModel` 新属性同样要更新基线。
    - `I18nWorkerRequest` 末尾追加 `string? SharedAssetsRoot = null`，不升 `i18n-worker-request.v1`（父子进程同一二进制）；AOT 依赖源生成的 JSON context，由最终 AOT 编译确认。
    - 链式调用中不写注释，否则 Windows CRLF 下触发 IDE0055（见提交 `a7c232b7`）。

## 2. 执行方式

- **阶段 0 准备**（T00，一个任务完成）：
  - 在 `scripts/checks/codex-workflow-policy.v1.json` 新增规则 `i18n-shared-assets-change`（参照 `unify-site-languages-change`）。`matches` 为本提案目录、本计划、`src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/BaseLayout.html` 和三个新测试文件 `tests/Bukit.Engine.Tests/{ImageVariantPlan,SharedAssetScope,I18nSharedAssets}Tests.cs`；`specialtyTests` 为第 3 节矩阵 M1–M11；其中 M1–M10 与基线取证一致并缓存，M11 是文档/API 脚本检查，不进入 cache。现有规则 `i18n-default-language-root-change` 把 `I18nWorktreeProtocol` 过滤用在了 `Bukit.Cli.Tests` 上，而这个类在 `Bukit.Engine.Tests`，不要照抄。然后跑 `bash scripts/checks/codex-workflow-self-test.sh`，`codex-workflow.py closure` 不得有未映射文件。
  - `codex-workflow.py classify` 分类：`dotnet test` 为 `dotnet-serial`；含并发和 Worktree 的测试单独运行。
  - 在未改动的基线上跑 M1–M10 并 `cache record`。
  - `codex-workflow.py queue init` 写入 T01.1–T08；实施者 acquire 后才写文件。
- **测试**：任务内只跑所引用的矩阵行，新增的过滤先用 `--list-tests` 确认能选中测试。完整测试项目只在 T08 跑一次，未变化的结果复用缓存。不跑 `scripts/test-all.sh`、`scripts/smoke-all.sh`、整个解决方案的测试和 full/release gate，除非用户明确授权。
- **审查**：每个阶段测完做一次专项审查，只有严重或重要问题才回到实施；全部完成后做一次 `review-scope` 增量统一审查。
- **记录**：阶段耗时、缓存命中、重跑、写者冲突用 `codex-workflow.py metrics add` 记录，T08 时 `metrics report`。

## 3. 测试矩阵

命令格式：`dotnet test tests/<项目>/<项目>.csproj -c Release --filter "FullyQualifiedName~A|FullyQualifiedName~B"`，下表简写过滤。任务只引用行号。

| 行 | 项目 | 过滤 / 命令 |
|---|---|---|
| M1 | `Bukit.Config.Tests` | `ConfigLoader\|StrictYamlContract\|ConfigSchemaNestedContract\|ConfigContractDrift\|ConfigJsonSchemaGenerator\|ConfigPhase2Validator\|GeoAiBotModeValidation\|MediaConfigDefaults` |
| M2 | `Bukit.Engine.Tests` | `SharedAssetScope\|AssetPipeline\|VariantBuildPipeline\|SiteEngineLoggerTests\|I18nLanguagePolicy` |
| M3 | `Bukit.Engine.Tests` | `SiteEngineIntegrationTests\|I18nSharedAssets\|I18nRootLayout\|RouteMetadataRendering` |
| M4 | `Bukit.Engine.Tests` | `RenderDependency\|IncrementalEngineIdentity\|IncrementalRenderReads` |
| M5 | `Bukit.Engine.Tests` | `SeoModelBuilder\|SeoAuthorProfile\|SeoPublisherJsonLd\|SeoJsonLd\|GeoSeoModelBuilder\|MediaPlaceholderProbe` |
| M6 | `Bukit.Engine.Tests` | `ImageVariantPlan\|ImageProcessingPlugin\|ImageSourceFingerprint` |
| M7 | `Bukit.Engine.Tests` | `I18nWorktree` |
| M8 | `Bukit.Rendering.Tests` | `ScribanModelKnownFields\|ScribanModelBinder\|StarterThemeTemplateResource` |
| M9 | `Bukit.Cli.Tests` | `WorktreeBuildCoordinator\|NotionOfflineSnapshotMultilingual` |
| M10 | `Bukit.Architecture.Tests` | `BuiltInPluginSplit\|PublicApiBaseline` |
| M11 | 脚本 | `bash scripts/checks/public-api-drift.sh check`；`openspec validate add-i18n-shared-assets --strict --no-interactive`；`bash scripts/checks/docs/active-links.sh`；`bash scripts/checks/docs/size-policy.sh` |
| F | 最终 | `Bukit.Config.Tests`、`Bukit.Engine.Tests`、`Bukit.Rendering.Tests`、`Bukit.Architecture.Tests` 完整项目，加 M9、M11；`bash scripts/checks/dotnet-format.sh`；`bash scripts/checks/code-analysis-ratchet.sh check`；当前平台 AOT：`bash scripts/build/native-aot.sh <version> osx-arm64 <output-root> Release` |

## 4. 任务

源码路径以 `src/Bukit-Core/` 为根，测试以 `tests/` 为根。每个任务的"测试"写引用的矩阵行和新增用例。

| 阶段 | 内容 | 对外行为 | 任务 |
|---|---|---|---|
| 0 | 闭包、基线、队列 | 不变 | T00 |
| 1 | 尺寸规划纯函数 | 不变 | T01.1–T01.2 |
| 2 | 配置、校验、警告、scope | 新增可选字段 | T02.1–T02.4 |
| 3 | 计划过滤、默认语言先构建、`asset_base_url`、渲染哈希 | 开启时非默认语言不再复制共用资源 | T03.1–T03.6 |
| 4 | SEO 与占位图地址 | 开启时 Core 拼出的地址指向根 | T04.1–T04.4 |
| 5 | 只读复用共用 WebP | 开启时非默认语言页面得到 `<picture>` | T05.1–T05.4 |
| 6 | Worktree | 与进程内一致 | T06.1–T06.3 |
| 7 | 文档与 CHANGELOG | — | T07.1–T07.3 |
| 8 | 最终验证与审查 | — | T08 |

### 阶段 0

**T00 准备**：按第 2 节"阶段 0 准备"完成闭包策略、分类、基线取证和队列。完成标准：`closure` 无未映射文件，M1–M10 基线通过并缓存。策略文件单独提交。

### 阶段 1：尺寸规划纯函数（行为不变）

**T01.1 新增 `ImageVariantPlan`**
- 改动：顶层 internal static 类，提供 `GeneratedWidths(sizes, sourceWidth)`（降尺寸回退宽度，不含原图宽度）、`CandidateWidths(sizes, sourceWidth)`（srcset / WebP 候选，含原图宽度）、`VariantFileName(source, width, extension)`、`TryParseVariantFileName(...)`。`NormalizeSizes`、`TryGetImageWidth`、`TryReadImageWidth` 留在原处。`BuiltInPluginSplitTests` 加 `[InlineData("ImageVariantPlan")]`。
- 测试：新增 `ImageVariantPlanTests`（两种规划、原图宽度等于或小于配置尺寸、重复尺寸、命名与反解析互逆、大小写扩展名）；M6、M10。

**T01.2 调用方切换**
- 改动：`ImageProcessingPlugin`、`ImageVariantGenerator`、`ResponsiveImageHtmlTransform` 的尺寸规划与命名，以及 `ImageVariantOwnership` 的反解析，全部改为调用 `ImageVariantPlan`，删除重复实现（位置见附录 A）。
- 测试：M6、M3 中的 `SiteEngineIntegrationTests.MediaLifecycle_`、M10。现有图片测试不改预期。
- 阶段审查：全仓检索 `w{ext}`、`-{width}w` 等模式，确认没有遗漏的实现；输出字节与基线一致。

### 阶段 2：配置

**T02.1 配置模型与读取**
- 改动：`SharedAssetsConfig` 与 `SiteConfig.SharedAssets`；`SiteDefaultsApplier.ReadSharedAssetsConfig`，并在 `ConfigLoader` 构造 `SiteConfig` 处赋值（只改 `SiteDefaultsApplier` 不会被调用）；`ConfigStrictFieldValidator` 的 `SiteKeys` 加 `sharedAssets`、`RequireOnly` 加 `enabled`、`paths`；`ConfigJsonSchemaGenerator.SiteSchema`；公共 API 基线与 `GovernedTypeCount`。
- 测试：M1（新增缺省值、读取 `paths`、未知子字段报错）、M10、M11 的 API 漂移。

**T02.2 配置校验**
- 改动：在 `ConfigValidator.Validate`（或它调用的 `SiteConfigValidator.ValidateSite`）中：空列表、绝对路径、含 `..`、规范化后为根目录的条目报错；条目等于或位于媒体前缀之内时报错并说明媒体共用不受支持；`enabled: true` 且 `defaultLanguageInSubdir: true` 时报错并写出两个字段名。规范化 helper 只实现一次（第 1.3 节第 4 条）。
- 测试：M1，新增用例放在 `ConfigPhase2ValidatorTests`，每条规则正反各一。

**T02.3 忽略警告**
- 改动：`I18nLanguagePolicy` 新增 `WarnSharedAssetsWithoutLanguages`（与 `WarnLayoutSettingsWithoutLanguages` 并列），少于 2 种语言且开启时记录 `event=i18n.config.shared_assets_ignored`，在 `SiteEngine` 中一并调用。
- 测试：M2，`languages: [en]` 加 `enabled: true` 时出现警告，输出与不写 `sharedAssets` 相同。

**T02.4 `SharedAssetScope`**
- 改动：按第 1.3 节第 4 条实现。
- 测试：新增 `SharedAssetScopeTests`（M2）：默认推导与自定义 `paths`；默认 `urlBase` 下 `assets/uploads/x.jpg` 不在 scope 而 `assets/css/a.css` 在；`urlBase: /media` 时 `assets/uploads/x.jpg` 在；SCSS 未启用时输出目录不纳入、启用后纳入；大小写不敏感文件系统上去重；SCSS 输出为根目录时不纳入，`logger` 非空时警告一次、为 `null` 时不记录；单语言、带前缀布局、未开启时返回 `null`；`CanonicalPrefixes` 与书写顺序无关；`ContainsSitePath` 对 `/assets/og.png?v=2`、`/assets/og.png#x` 为真，对 `/assets/uploads/x.jpg?v=1` 为假。
- 阶段审查：校验和 scope 共用同一套规范化；默认推导与规范逐条一致；未开启时配置对象之外没有行为变化。

### 阶段 3：资源计划、构建顺序、模板变量、渲染哈希

**T03.1 上下文接线**
- 改动：`BuildVariantContext` 新增可选的 `SharedAssets`；`SiteEngine` 多语言分支调用一次 `TryCreate(config, languages, outputDir, buildLogger)`；`VariantRenderAssetPlanner` 只为非默认语言（`I18nOutputLayout.IsRootLanguageVariant` 为假）设置 `AssetPipelineContext.OmittedSharedAssets`。
- 测试：M2。

**T03.2 计划过滤与 tokens**
- 改动：按第 1.3 节第 1、2 条修改 `AssetOutputPlan.Create` 和 `AssetPipeline.ExecutePreparedAsync`。
- 测试：M2，新增：非默认语言计划不含 `assets/**`、tokens、`static/assets/**`，`OmittedSharedItems` 恰好是这些条目；渲染型 static 和媒体保留；未开启时计划不变；被过滤时 tokens 不写文件。

**T03.3 默认语言先构建**
- 改动：按第 1.3 节第 3 条拆分 `SiteEngine` 的语言调度；未开启时代码路径不变。
- 测试：M3，放在现有 `languageJobs` 测试旁：`languages: [zh-CN, en]`、`languageJobs: 2` 时 `en` 的 `build.variant.done` 早于 `zh-CN` 的 `build.variant.start`；默认语言失败时其他语言不启动、正式输出不变；`BuildAsync_LanguageJobs_ProducesIdenticalOutputRegardlessOfConcurrency` 在开启时同样成立。

**T03.4 `site.asset_base_url`**
- 改动：按第 1.3 节第 5 条修改 `SiteModel`、`VariantDataSitePlanner.BuildSiteModel`、`ScribanSiteModelMapper`、`ScribanModelKnownFields`，更新 API 基线。
- 测试：M8（新增 `AssetBaseUrl` 为空时等于 `base_url`）；M3 中开启 / 关闭 × `/` / `/repo` 四个组合；M11 的 API 漂移。

**T03.5 Starter 主题与渲染哈希**
- 改动：`BaseLayout.html` 按第 1.3 节第 5 条映射 `asset_base_url`，样式表改用它，RSS 和 sitemap 继续用 `base_url`；渲染哈希按第 1.3 节第 6 条接线。
- 测试：M8：`base_url` 与 `asset_base_url` 都为 `/` 时样式表为 `/assets/style.css`；都为 `/repo` 时为 `/repo/assets/style.css`；开启、`base_url` 为 `/repo/zh-CN`、`asset_base_url` 为 `/repo` 时样式表为 `/repo/assets/style.css`、RSS 为 `/repo/zh-CN/rss.xml`。M4：未开启时哈希与基线相同；开启后变化；`paths` 增加前缀时变化；前缀书写顺序不影响；开启时根 baseUrl 由 `/` 改为 `/repo`，非默认语言哈希变化。

**T03.6 端到端与开关切换**
- 测试：新增 `I18nSharedAssetsTests`（M3），覆盖规范场景："Sharing not enabled"、"Sharing enabled"、"Static under assets is shared"、"Other static files are not shared by default"、"Configured static prefix"、"Theme tokens shared"、"Plugin output within the shared set"、"Media stays per language"、两个 asset URL 场景、"Enabling / Disabling sharing on an incremental build"（`build.clean: false`）、"Changing shared paths on an incremental build"、"Changing the root base URL with sharing enabled"；另加：只改一种语言时 `dist/assets/**` 的修改时间和内容不变。
- 阶段审查：非默认语言 manifest 不残留共用路径；默认语言输出与未开启时逐字节相同；拆分后失败和取消语义不变。

### 阶段 4：Core 拼出的地址

**T04.1 地址清单复核**
- 做法：全仓检索 `_baseUrl +`、`BaseUrl +`、`BuildAbsoluteUrl(`、`BuildMaybeAbsoluteUrl(`，列出可能把共用资源路径拼上语言 baseUrl 的位置，每项标明归入 T04.2–T04.4 或说明无需修改。清单外的发现补进本阶段；与本提案无关的问题记入第 7 节后续事项，不在本变更修复。

**T04.2 SEO 接线**
- 改动：按第 1.3 节第 7 条的链路逐层加可选 scope 参数，只传参，不改变结果。
- 测试：M5，未开启时与基线一致。

**T04.3 SEO 地址规则**
- 改动：新增 `BuildMaybeAbsoluteUrl` 重载，8 个叶子调用点（附录 A）传入 scope。
- 测试：M5，新增：规范场景"Default SEO image on a non-default language page"、"Shared SEO image with a query string"；`og_image` 在 scope 内外各一；作者图片在 scope 内；绝对 http 地址不变；未开启时与基线一致。`og_image` 为 `/assets/uploads/x.jpg` 时仍用语言 baseUrl，这是 `SeoModelBuilder` 单元层断言，完整构建中该值已被媒体规范化（第 1.3 节第 7 条）。

**T04.4 媒体占位图**
- 改动：按第 1.3 节第 7 条修改 `ContentMediaOutput.RewriteUrl` 和 `ValidateOutputs`。
- 测试：M5、M3 中的 `SiteEngineIntegrationTests.MediaLifecycle_`；新增规范场景"Media fallback on a non-default language page"；共用资源根缺少占位图时报出现有的缺失错误；`defaultImageUrl` 含 `..` 时仍被 `GetSafeFullPath` 拒绝。
- 阶段审查：地址规则只依赖 scope；未开启时 SEO 输出与基线逐字节一致。

### 阶段 5：图片

**T05.1 插件输入**
- 改动：新增 `BuildDataKeys.SharedAssets`，只对非默认语言、按第 1.3 节第 8 条的时机写入 scope 和共用原图清单。
- 测试：M2，非默认语言的清单只含 scope 内图片、不含 `assets/uploads/**`；测试插件在 AfterBuild 中读到的清单非空且与计划中被过滤的图片条目一致；默认语言和未开启时读不到这个 key。

**T05.2 只读共用映射**
- 改动：按第 1.3 节第 8 条构建共用映射（清单、key 与候选前缀、`TryGetImageWidth`、`TryReadFreshness` + `Matches`、提前返回之前构建）。
- 测试：M6，新增：候选全部存在；部分缺失时只收录存在的；sidecar 哈希不符或缺失时不收录；共用资源根里用户自己的 `hero-480w.jpg` 既不当原图也不当候选；根 baseUrl 为 `/` 时 key 为 `/assets/hero.jpg`、候选不含 `//`，为 `/repo` 时 key 为 `/repo/assets/hero.jpg`，均能被 `TryNormalizeLocalImageUrl` 的结果命中；URL 不含 `../`；原图损坏时跳过、记录 `image_processing.skip`、构建成功。

**T05.3 合并改写与地址识别**
- 改动：提前返回分支在共用映射非空时仍执行 `RewriteCurrentHtmlWithWebp`，并使用"自身映射 ∪ 共用映射"；`TryNormalizeLocalImageUrl` 新增受 scope 约束的根前缀分支（第 1.3 节第 9 条）。默认语言和未开启时不变。
- 测试：M6、M3，新增：规范场景"Shared theme image on two language pages"、"Language without its own images"、"Non-shared image at the site root"、"Default language variant failure in warn mode"；根站点中文页的 `/zh-CN/assets/uploads/x.jpg` 走本语言媒体映射；`/assets/uploads/x.jpg` 在单元层既不属于共用也不算本语言媒体、保持原 `<img>`，在完整构建层先被改写为 `/zh-CN/assets/uploads/x.jpg` 再走本语言映射；已改写的 `<picture>` 再次改写时不变。

**T05.4 不写共用资源根**
- 改动：确认非默认语言 AfterBuild 的扫描、孤儿清理和 freshness 清理只作用于自己的输出目录。
- 测试：M3，新增：非默认语言 AfterBuild 前后 `dist/assets/**` 的文件快照（路径、大小、修改时间）一致。
- 阶段审查：只读路径没有写、删或 `File.SetLastWriteTime`；共用映射与默认语言产物由同一规划函数推出；`warn` 模式下不引用不存在的变体。

### 阶段 6：Worktree

**T06.1 请求字段**
- 改动：`I18nWorkerRequest` 追加 `string? SharedAssetsRoot = null`，现有测试构造点因默认值不用改；`WorktreeBuildRun` 开启共用时传入 staging 根目录，并在准备阶段用父进程 logger 调用一次 `TryCreate`，用于推导警告和调度判断。Worker 构造 `BuildVariantContext` 时，字段为 `null` 则 scope 为 `null`、不调用 `TryCreate`，否则以 `logger: null` 调用。
- 测试：M7，请求往返保留新字段；缺失时还原为 `null`，Worker 的 scope 为 `null`。

**T06.2 默认语言 Worker 先行**
- 改动：`WorktreeBuildRun.RunWorkersAsync` 在开启时先运行默认语言 Worker，成功后再并发调度其余 Worker；失败与取消语义沿用现有实现。
- 测试：M9，新增"默认语言 Worker 完成后其他 Worker 才启动"和"默认语言 Worker 失败时其他 Worker 不启动"。

**T06.3 等价测试**
- 测试：M9，新增 `WorktreeBuild_MatchesInProcessOutputs_ForSharedAssets`，仿照 `WorktreeBuild_MatchesInProcessOutputs_ForRootDefaultLanguage`，覆盖规范场景"Worktree and in-process equivalence"，输出包含 WebP 主题图片、占位图和 `seo.defaultImage`；M7。
- 阶段审查：Worker 与进程内走同一个 scope 构造和过滤路径；未开启时请求字段为 `null`、旧流程不变。

### 阶段 7：文档与契约

**T07.1 用户指南与 skill**：`guide/user/04-site-yaml-config.md`、`guide/dev/config-site-yaml.md` 写 `site.sharedAssets` 的字段、默认值、校验规则和布局限制；`guide/user/11-i18n-seo.md` 新增"共用资源"一节（`asset_base_url`、默认语言先构建及 `languageJobs > 1` 的耗时代价、媒体不共用）；`guide/skills/bukit-i18n` 和参数速查。测试：M1 的 `ConfigContractDrift`、M11 的文档检查。

**T07.2 主题作者说明**：`guide/dev/theme.md` 写明共用资源用 `asset_base_url`、按语言变化的输出用 `base_url`；根 baseUrl 为 `/` 时先映射为空串（附 Starter 写法）；JS 运行时拼地址和 CSS 根绝对路径 `url()` 的注意事项；只支持根目录布局。

**T07.3 CHANGELOG**：三语 CHANGELOG 的 Unreleased 写明新增 `site.sharedAssets`（默认关闭）和 `site.asset_base_url`、Starter 主题改用 `asset_base_url`、开启后非默认语言不再有自己的 `assets/`。

### 阶段 8

**T08 最终验证与审查**：跑矩阵 F，未变化的结果复用缓存；勾选 openspec `tasks.md` 中已取得证据的条目；`codex-workflow.py review-scope` 后做一次增量统一审查；`metrics report`。完成标准：F 全部通过，没有严重或重要问题。Windows 结果由 CI 确认；full/release gate 只在用户授权后执行。

## 5. 需求、任务与测试对应

| 规范要求 | tasks.md 小节 | 实现任务 | 测试 |
|---|---|---|---|
| Shared assets are opt-in for multilingual root-layout sites | 2 | T02.1–T02.4 | T02.2、T02.3、T03.6 |
| The effective shared set is a set of output prefixes | 2 | T02.2、T02.4 | T02.2、T02.4、T03.6 |
| The default language owns shared assets | 3 | T03.1、T03.2 | T03.2、T03.6 |
| The default language builds first when sharing is enabled | 3、7 | T03.3、T06.2 | T03.3、T06.2 |
| Templates reference shared assets through asset_base_url | 4 | T03.4、T03.5 | T03.4、T03.5、T03.6 |
| Core-generated site URLs follow the shared set | 0、4 | T04.1–T04.4 | T04.3、T04.4 |
| Non-default languages reuse shared image variants read-only | 1、5 | T01.1–T01.2、T05.1–T05.4 | T01.1、T05.1–T05.4 |
| Shared asset settings affect incremental rendering | 6 | T03.5 | T03.5、T03.6 |
| Worktree builds share assets like in-process builds | 7 | T06.1–T06.2 | T06.1–T06.3 |

tasks.md 第 8 节（测试）分布在上表各任务中，第 9 节对应 T07.1–T07.3，第 10 节对应 T08。

## 6. 风险与回退

| 风险 | 位置 | 应对 |
|---|---|---|
| 非默认语言仍写出 tokens | T03.2 | 第 1.3 节第 1 条；断言 `dist/zh-CN/assets/css/theme-tokens.css` 不存在 |
| 拆分调度后失败或取消语义改变 | T03.3、T06.2 | 复用同一个 lambda 和 `SelectPrimaryFailure`；失败用例 |
| 现有站点因哈希变化全量重渲染 | T03.5 | 只在生效时追加；断言未开启时哈希与基线相同 |
| Starter 在根 baseUrl 下输出 `//assets/...` | T03.5 | 第 1.3 节第 5 条；`/`、`/repo`、开启共用三组用例 |
| 媒体前缀被误判为共用 | T02.4 起 | "包含且不排除"；`assets/uploads/x.jpg` 精确用例 |
| 共用原图清单为空或 WebP 不生效 | T05.1–T05.3 | 第 1.3 节第 8 条的设置时机与提前返回；AfterBuild 读取用例和无自有图片用例 |
| key 归一化不一致或拼出 `//` | T05.2 | 单一前缀；`/` 与 `/repo` 用例 |
| 根站点把任意图片当作共用 | T05.3 | 第 1.3 节第 9 条；非共用图片用例 |
| 坏图让非默认语言构建失败 | T05.2 | `TryGetImageWidth`；坏图用例 |
| 遗漏 Core 拼地址的位置 | 阶段 4 | T04.1 先列清单 |
| 只读映射引用不存在的变体 | T05.2、T05.3 | 只收录存在且 freshness 有效的候选；`warn` 失败用例 |
| 非默认语言写入共用资源根 | T05.4 | 文件快照用例 |
| 公共 API 或类型计数漂移 | T02.1、T03.4 | 在引入的任务内更新基线 |
| Worker 请求 AOT 序列化失败 | T06.1 | M7 往返测试；F 的 AOT 编译 |

每个阶段结束时都能编译、测试通过，且未开启时行为不变。回退按阶段倒序 revert：阶段 5、6 可以单独撤回（撤回后须在文档中标注功能未完成）；撤回阶段 3 时阶段 4–6 一并撤回。

## 7. 后续事项（不在本变更内）

- `SeoImageResolver` 对 `record.Media` 的地址是否重复拼接 baseUrl：T04.1 顺带记录现象，另行处理。
- `ContainsSitePath` 对百分号编码路径（如 `%20`）的解码：本变更只处理 query 和 fragment；规范没有编码路径的场景，有需要时另行补充。
- `BuildResult` 资源 / 媒体计数的现存问题。
- 把 `asset_base_url` 扩展到独立 CDN 域名。
- 媒体及媒体图片变体的跨语言共用（A5）。

## 附录 A：基线证据（`e7651fb1`）

实施时代码会移动，以下行号只用于对照基线，以符号为准。

| 主题 | 位置 |
|---|---|
| tokens 绕过计划写出 | `AssetPipeline.cs:109-112,190` |
| 计划过滤点 | `AssetOutputPlan.cs:131-141` |
| 语言调度 | `SiteEngine.cs:333-375`；变体配置改写 baseUrl `:348-355`；警告调用点 `:161` |
| 资源计划创建与 AfterBuild | `VariantBuildPipeline.cs:127-130`、`:174`；`MediaDownloadDir` 设置于 `VariantRouteStage.cs:52` |
| 渲染哈希 | `RenderDependencyContext.cs:6-11`、`RenderDependencyHasher.cs:37-50`、`SiteIdentityAndModesContributor.cs:10-36` |
| SEO 叶子调用点 | `SeoModelBuilder.cs:141`；`SeoImageResolver.cs:26,36,43`；`SeoJsonLdBuilder.cs:310,315,499,707` |
| SEO 接线 | `SeoPipeline.cs:22-36`、`SeoIndexBuilder.cs:43,84,137`、`SeoModelBuilder.cs:50,175` |
| 占位图与媒体改写 | `ContentMediaOutput.cs:29-44`（`NormalizeDocument`）、`:126-131`（`ValidateOutputs`）、`:184-197`（`RewriteUrl`） |
| 图片插件提前返回与扫描 | `ImageProcessingPlugin.cs:58-66`、`:108-124`、`:307-331` |
| 映射 key | `ImageVariantGenerator.cs:269,318-319`；查找 `ResponsiveImageHtmlTransform.cs:82`，去 query `:131-135` |
| 宽度读取 | `ImageVariantGenerator.cs:25`（`TryGetImageWidth`）、`:43`（`TryReadImageWidth`） |
| freshness | `ImageVariantFreshness.cs:18` |
| 尺寸规划与命名的重复实现 | 规划 `ImageProcessingPlugin.cs:173,187`、`ImageVariantGenerator.cs:288`、`ResponsiveImageHtmlTransform.cs:172-177`；命名 `ImageVariantGenerator.cs:70,295`、`ImageProcessingPlugin.cs:273,284`、`ResponsiveImageHtmlTransform.cs:261-265`；反解析 `ImageVariantOwnership.cs:148-163,226-233,247-253` |
| comparer 探针 | `OutputDestinationIdentityComparer.cs:5-30` |
| 配置读取与校验 | `ConfigLoader.cs:63-97`、`ConfigValidator.cs:12-15` |
| Starter 主题 | `BaseLayout.html:1-2`（`base_url` 映射）、`:11`（样式表） |
| Worktree | `WorktreeBuildRun.cs:334-353`、`I18nWorktreeBuild.cs:79,183-230`；单语言不走 Worktree `BuildCommand.cs:154`；等价测试参照 `WorktreeBuildCoordinatorTests.cs:917` |
| `new SiteModel` | 44 处（生产 1、测试 43） |

## 修订记录

| 轮次 | 日期 | 内容 |
|---|---|---|
| 初稿与三轮审阅 | 2026-11-14 | 提交 `7038ad32`、`2b9d3c26`、`3c8bf033`、`14a06a5d`、`074011b9`。审阅发现的问题均已并入正文，细节见各提交说明；"Worktree 漏记单语言警告"未采纳，理由是单语言不走 Worktree |
| 决策确认 | 2026-11-14 | A1–A5 全部采用推荐项（提交 `e9d55b4c`） |
| 精简 | 2026-11-14 | 产品范围不变：删除失效的决策调整一节；两张对应表合并；测试命令集中为一张矩阵；陷阱由 17 条压为 10 条；行号移入附录 A；阶段 0、8 各合为一个任务，阶段末不再跑完整项目；URL 解码和 `record.Media` 检查移入后续事项。任务数由 36 减为 28 |
