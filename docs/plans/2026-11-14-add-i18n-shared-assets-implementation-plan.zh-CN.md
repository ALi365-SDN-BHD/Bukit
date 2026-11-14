# 多语言共用资源（`add-i18n-shared-assets` v2）：实施方案与任务计划

- 状态：**待实施**。proposal.md 中的决策 A1–A5 已于 2026-11-14 确认，全部采用推荐项（A1 默认关闭、A2 默认语言归属、A3 只支持根目录布局、A4 非资源输出照常按语言输出、A5 媒体另立提案）。实施作为单独的 Core 任务，在用户明确开始后进行。
- 日期：2026-11-14
- 依据：
  - 规范性提案 `openspec/changes/add-i18n-shared-assets/`（proposal、design、specs、tasks），v2。
  - 2026-11-14 按 `main` 对 v1 所做的逐条代码核查（结论已写入 design.md "Context" 和 "Alternatives"）。
- 代码基线：`e7651fb1`（main，本计划写作时）。
- 范围：只覆盖 Core 的实现、测试和文档。commit、push、PR、发布、full/release gate 均需另行授权。

---

## 0. 先读这一段

1. **决策门。** A1–A5 已确认（全部采用推荐项），决策门已通过。以后若改选某项，先按第 8 节修改提案和本计划，再开工。
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
| `SharedAssetScope` | `Bukit.Engine/SharedAssetScope.cs`（新文件） | internal | 有效共用集合的唯一实现。`TryCreate(config, languages, outputRoot, logger?)` 在站点层创建一次，向下传递。不满足启用条件时返回 `null`。成员见第 1.3 节第 14 条 |
| `ImageVariantPlan` | `Bukit.Engine/Plugins/BuiltIn/ImageProcessing/ImageVariantPlan.cs`（新文件） | internal，顶层，非插件 | 尺寸规划（生成文件 / 候选）、命名、反解析的纯函数 |
| `BuildVariantContext.SharedAssets` | `Bukit.Engine/BuildVariantContext.cs` | internal | 开启共用时，所有语言都拿到同一个 scope（含输出根和根 baseUrl） |
| `AssetPipelineContext.OmittedSharedAssets` | `Bukit.Engine/AssetPipeline.cs` | internal | 只对非默认语言赋值，`AssetOutputPlan.Create` 据此过滤 |
| `AssetOutputPlan.OmittedSharedItems` | `Bukit.Engine/AssetOutputPlan.cs` | internal | 被过滤掉的条目，用作非默认语言的共用原图清单（第 1.3 节第 8 条） |
| `RenderDependencyContext.SharedAssetPrefixes` | `Bukit.Engine/Incremental/RenderDependencyContext.cs` | internal | 已规范化、已排序的有效前缀，供 `SiteIdentityAndModesContributor` 哈希 |
| `BuildDataKeys.SharedAssets` | `Bukit.Engine/BuildDataKeys.cs` | internal | 把 scope 和共用原图清单交给 `ImageProcessingPlugin` |
| `I18nWorkerRequest.SharedAssetsRoot` | `Bukit.Engine/I18nWorktreeProtocolModels.cs` | internal | Worker 拿到 staging 根目录 |
| `SiteModel.AssetBaseUrl` → `site.asset_base_url` | `Bukit.Rendering/Models.cs`、`ScribanSiteModelMapper` | public 属性，`string?`，非 required | 模板变量；为空时回退到 `BaseUrl`（第 1.3 节第 17 条） |

配置校验之外，计划过滤、地址规则、图片插件、渲染哈希和 Worktree 都只调用 `SharedAssetScope`，不各自拼前缀或判断前缀。初稿中独立的 `SharedAssetUrls` 已并入 scope。

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

### 1.3 实现要点与陷阱

1. **tokens 不经过计划写文件。** `AssetPipeline.GenerateTokensAsync` 只看 `tokens is not null` 就写 `assets/css/theme-tokens.css`（`AssetPipeline.cs:109-112,190`），从计划中删掉 Tokens 条目并不能阻止写出。必须改为"计划中有 Tokens 条目才生成"，否则非默认语言仍会写出 tokens，而且这个文件不在它的 manifest 里。
2. **过滤位置。** 在 `AssetOutputPlan.Create` 中，于 `FileWriter.GetSafeFullPath` 和 `Validate` 之前过滤（`AssetOutputPlan.cs:131-141`）。只删 `Category ∈ {Assets, Tokens, Static}` 且 `Operation ∈ {Copy, Generate}` 的条目。渲染型 static 是 `Operation.Render`，媒体是 `Category.Media`，都不受影响。被删的条目保存在 `AssetOutputPlan.OmittedSharedItems` 中，供第 8 条使用。
3. **非默认语言的旧副本。** `clean: false` 时，非默认语言上次归属的 `<lang>/assets/**` 会由现有的 `BuildManifestTracker.PrepareAssetPlanOutputs`、`TrackAssetPlanOutputs` 和 `PublicOutputLifecycle.PrepareProjectionWrites` 删除。这里依赖现有行为，必须用测试固定（T03.6）。
4. **默认语言先构建。** `SiteEngine.cs:333-375` 现有的语义是"默认语言总会跑完，其他语言在出错后提前停止"，并且 `results[i]` 按原索引存放。开启共用时，先用同一个 lambda 单独跑默认语言，失败则直接进入现有的失败选择（`SelectPrimaryFailure`）；成功后对其余语言执行现有的 `Parallel.ForEachAsync`。保持索引和日志事件不变。
5. **渲染哈希。**
   - `RenderDependencyContext` 只有 `AppConfig` 和 `SiteModel`（`RenderDependencyContext.cs:6-11`），contributor 拿不到 scope。只哈希 `asset_base_url` 也不够：调整 `paths` 时 `asset_base_url` 不变，但 SEO 地址、占位图地址和 WebP 改写都会变。
   - 做法：`RenderDependencyContext` 新增可选的 `SharedAssetPrefixes`（规范化、排序后的列表）。`RenderDependencyHasher.ComputeSnapshot`（`RenderDependencyHasher.cs:37-50`）增加对应参数，由 `VariantRenderStage` 从 `BuildVariantContext.SharedAssets` 传入。contributor 不得自己调用 `TryCreate`。
   - 保持稳定：`SiteIdentityAndModesContributor` 只对非默认值写入哈希（如 `defaultLanguageInSubdir`）。共用设置照此处理：列表非空时才追加。未开启时现有站点的哈希不变，不会触发全量重渲染。
   - `asset_base_url` 不单独写入哈希。`SiteModel` 不会整体进入哈希：`SiteIdentityAndModesContributor` 只写显式字段（`SiteIdentityAndModesContributor.cs:10-36`），`SiteModelDataContributor` 只写 `site.modules`、`site.data`、`site.data_index`。`asset_base_url` 完全由已入哈希的值推出：开启时等于根 baseUrl，而变体配置的 `site.baseUrl` 由根 baseUrl 和语言决定，`site.baseUrl`、`site.language`、`site.defaultLanguage` 都已写入；是否开启由 `SharedAssetPrefixes` 是否为空体现；关闭时它等于 `site.baseUrl`。无条件追加会改变未开启站点的哈希，因此不追加，改由用例直接断言"根 baseUrl 变化时非默认语言的哈希变化"。
6. **SEO 地址。**
   - 叶子调用点：`BuildMaybeAbsoluteUrl` 共有 8 处，即 `SeoModelBuilder.cs:141`，`SeoImageResolver.cs:26,36,43`，`SeoJsonLdBuilder.cs:310,315,499,707`。新增一个带 `SharedAssetScope?` 参数的重载，原签名转调它并传 `null`。站内相对地址用 `scope.ContainsSitePath`（第 14 条）判断，为真时使用 `scope.RootBaseUrl`，原地址的 query 和 fragment 原样保留。
   - 接线链：scope 必须沿 `VariantSeoStage` → `SeoPipeline.Execute`（`SeoPipeline.cs:22-36`）→ `SeoIndexBuilder.Build`（`SeoIndexBuilder.cs:43,84,137`）→ `SeoModelBuilder.BuildForContent` / `BuildForList` → `SeoImageResolver`、`SeoJsonLdBuilder.BuildJsonLd`（`SeoModelBuilder.cs:50,175`）一路传下去。每层新增可选参数，默认 `null`。
   - 不要从变体配置里反推根 baseUrl：变体配置的 `Site.BaseUrl` 已被改写成语言 baseUrl（`SiteEngine.cs:348-355`）。
7. **占位图。**
   - `ContentMediaOutput.RewriteUrl` 已能识别 `_rootBaseUrl + fallback` 形式的输入（`ContentMediaOutput.cs:197`）。需要改的是：非默认语言中占位图在 scope 内时，输出改为根 baseUrl + 占位图。
   - `ValidateOutputs` 改为检查共用资源根下的文件，不再要求它出现在本语言的计划输出中（`:126-131`）。路径一律用 `FileWriter.GetSafeFullPath(scope.OutputRoot, relative)` 解析，不要用 `Path.Combine` 直接拼。`defaultImageUrl` 来自配置，属于信任边界。
8. **共用原图清单与共用映射。**
   - 问题：`ImageProcessingPlugin.AfterBuildAsync` 在本语言没有原图时直接返回（`ImageProcessingPlugin.cs:58-66`）。原图来自 `FindSourceImages`，它只扫描 `context.OutputDir`（`:307-331`）。开启共用后，非默认语言目录通常没有图片，WebP 改写（`:108-124`）根本不会执行。
   - 清单来源：**不扫描共用资源根**，直接取 `AssetOutputPlan.OmittedSharedItems` 中扩展名为 `.jpg`、`.jpeg`、`.png` 的条目，取其 `Destination`（相对输出根）。这些正是默认语言写出的共用原图。好处有三点：
     - 默认语言生成的 `-{w}w`、`.webp` 变体不在清单里，不需要默认语言 manifest 里的归属信息。
     - 不会靠文件名模式把用户自己的 `hero-480w.jpg` 误判成变体。
     - 进程内构建和 Worktree 构建得到的清单相同。
   - 执行时机：共用映射在 `imageFiles.Count == 0` 的提前返回**之前**构建。提前返回的分支改为：共用映射非空时，仍然执行 `RewriteCurrentHtmlWithWebp`。
   - 候选：用 `ImageVariantPlan.CandidateWidths` 和 `VariantFileName` 推出候选文件；文件路径以 `scope.OutputRoot` 为根解析；改写用的 URL 为 `scope.RootBaseUrl` + 相对输出根的路径。不能沿用 `GetRelativeIdentity(context.OutputDir, ...)`，那样会得到 `../assets/...`。
   - 清单的设置时机：`OmittedSharedItems` 要到 `AssetPipeline.PrepareAsync`（`VariantBuildPipeline.cs:127-130`）才有值，而 `BuildDataKeys.MediaDownloadDir` 在更早的 `VariantRouteStage.cs:52` 设置，不能放在同一处。`BuildDataKeys.SharedAssets` 在 `PrepareAsync` 之后、`VariantPluginStages.RunAfterBuildAsync`（`VariantBuildPipeline.cs:174`）之前写入 `routePipelineResult.PluginContext`，AfterBuild 读到的是同一个 context。
   - key 空间：现有映射的 key 是站内 URL 路径 `{NormalizeBaseUrl(context.BaseUrl).TrimEnd('/')}/{sourceRelative}`（`ImageVariantGenerator.cs:269,318-319`），`RewriteCurrentHtmlWithWebp` 用 `TryNormalizeLocalImageUrl` 返回的路径查找（`ResponsiveImageHtmlTransform.cs:82`），后者已去掉 query 和 fragment（`:131-135`）。共用映射的 key 定为 `NormalizeBaseUrl(scope.RootBaseUrl).TrimEnd('/') + "/" + destination`，候选 URL 用同一个前缀。根 baseUrl 为 `/` 时前缀为空串，key 为 `/assets/hero.jpg`；为 `/repo` 时为 `/repo/assets/hero.jpg`。这个前缀只计算一次，key 和候选共用，不另写归一化。
   - 原图宽度：`CandidateWidths(sizes, sourceWidth)` 需要原图宽度，用 `ImageVariantGenerator.TryGetImageWidth`（`ImageVariantGenerator.cs:25`）读取共用资源根中的原图。不能直接用 `TryReadImageWidth`（`:43`）：它不捕获 IO、`InvalidDataException` 等异常，坏图会让非默认语言构建失败。`TryGetImageWidth` 捕获这些异常，记录现有的 `image_processing.skip` 警告并返回 `false`，此时跳过该原图。
9. **只读 freshness。** 复用现有的只读方法 `ImageVariantFreshness.TryReadFreshness(path, outputDir, sourceFile, ...)`（`ImageVariantFreshness.cs:18`），`outputDir` 传 `scope.OutputRoot`，不新增方法。共用映射不得写入、修复或删除任何文件。
10. **架构约束。**
    - `BuiltInPluginSplitTests` 要求 ImageProcessing 下的辅助类型保持顶层、internal、不是插件。把 `"ImageVariantPlan"` 加进 `Responsibility_IsTopLevelInternalAndNotAPlugin` 的 `InlineData`，由测试强制约束，不靠人工检查。
    - 新增 public 类型 `SharedAssetsConfig` 要把 `PublicApiBaselineFacts.GovernedTypeCount` 从 425 改为 426，并更新 `docs/governance/bukit-core-public-api-baseline.v1.json`。
    - `SiteModel` 新增属性同样要更新 API 基线。
11. **Worker 请求。** 在 `I18nWorkerRequest` 末尾追加可空的 `SharedAssetsRoot`。父进程和 Worker 是同一个二进制，不升 `i18n-worker-request.v1`。AOT 序列化依赖源生成的 JSON context，新增属性后要跑 AOT 编译确认（T08.2）。
12. **媒体前缀的归一化**复用 `ResponsiveImageHtmlTransform.NormalizeMediaUrlBase`，以及与 `ContentMediaOutput` 一致的有效媒体配置，不新写一份。Worktree 会强制把 `DownloadDir` 设为 `assets/uploads`（`I18nWorktreeBuild.cs:79,196`），但 `UrlBase` 不变，所以媒体前缀只取 `UrlBase`。
13. **Starter 主题的根 baseUrl。** `BaseLayout.html:1-2` 只把 `base_url` 的 `/` 映射为空串，第 11 行的样式表引用基于它。改用 `asset_base_url` 时，必须为它单独增加同样的映射，例如 `{{ asset_base_url = site.asset_base_url }}{{ if asset_base_url == "/" }}{{ asset_base_url = "" }}{{ end }}`。`base_url` 继续用于 RSS 和 sitemap 链接。否则 `site.baseUrl: /` 时会渲染出 `//assets/style.css`。未开启共用时 `asset_base_url` 等于 `base_url`，同样会出错，阶段 3"默认输出不变"的承诺也就不成立了。
14. **`SharedAssetScope` 的成员与语义。**
    - `TryCreate(AppConfig config, IReadOnlyList<string> languages, string outputRoot, ILogger? logger)`：
      - 同时满足"已开启、2 种及以上语言、根目录布局"时返回实例，否则返回 `null`。
      - 在站点层（`SiteEngine`；Worktree 父进程和 Worker 各一次）创建一次，再随 `BuildVariantContext` 下传，不让每种语言重新探测。
      - 内部调用一次 `OutputDestinationIdentityComparer.ForOutputRoot(outputRoot)`。这个方法会在输出根创建临时探针文件（`OutputDestinationIdentityComparer.cs:5-30`），所以只调一次。
      - `logger` 为 `null` 时不记录推导警告。Worker 传 `null`，警告只在父进程记录一次。
    - `Includes`：配置的 `paths`，或默认的 `assets/` 加 SCSS 输出目录（只在 `theme.scss.enabled: true` 时加入；输出目录规范化后为根目录时不加入并警告）。
    - `Excludes`：媒体前缀。
    - `Contains(relativePath)`：**命中任一 `Includes` 前缀，且不命中任一 `Excludes` 前缀**，大小写规则使用上面的 comparer。不能实现成"前缀集合减去媒体前缀"后再做普通 `StartsWith`：`assets/uploads/x.jpg` 同样以 `assets/` 开头，会被误判为共用。
    - `ContainsSitePath(sitePath)`：给 SEO 和占位图用的站内地址判断。去掉 query 和 fragment、`Uri.UnescapeDataString` 解码、去掉开头的 `/` 后调用 `Contains`。截断方式与 `ContentMediaOutput.RewriteUrl`（`ContentMediaOutput.cs:184-186`）一致，这样 `/assets/og.png?v=2` 也能命中。
    - `CanonicalPrefixes`：供渲染哈希使用的稳定表示，包含规范化排序后的 `Includes` 和 `Excludes`。
    - 另外暴露 `OutputRoot`（共用资源根）、`RootBaseUrl`（规范化后的站点根 baseUrl）和 `Comparer`。
15. **`TryNormalizeLocalImageUrl` 的根前缀分支必须检查 scope。** 新增的"接受 `asset_base_url + "/"`"分支，只在去掉前缀后的路径满足 `scope.Contains` 时才成立。站点 baseUrl 是 `/` 时，`asset_base_url` 规范化后是空串，`"" + "/"` 会匹配所有站内地址；不加这个检查，中文页里任何 `/xxx.jpg` 都会被当成共用资源，到共用资源根中查找。
16. **配置校验的生产入口**是 `ConfigValidator.Validate`（`ConfigValidator.cs:12-15`）。跨节规则（媒体前缀、SCSS 输出目录、`defaultLanguageInSubdir`）放在这里，才能同时读取 `content.media.urlBase` 和 `theme.scss`。`ConfigPhase2ValidatorTests` 只是测试类名。
17. **`SiteModel.AssetBaseUrl` 的形态。**
    - 声明：`public string? AssetBaseUrl { get; init; }`，非 required。仓库里有 44 处 `new SiteModel`（生产代码 1 处，测试 43 处），required 会让它们和外部构造方编译失败。
    - 回退：`ScribanSiteModelMapper` 输出 `asset_base_url` 时用 `model.AssetBaseUrl ?? model.BaseUrl`，第三方自己构造的 `SiteModel` 也能得到正确值。
    - 赋值：`VariantDataSitePlanner.BuildSiteModel` 总是显式赋值，开启时为根 baseUrl，否则为语言 baseUrl。

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
| 4 | 地址清单、SEO 接线与规则、媒体占位图 | 开启时 Core 拼出的地址指向根 | T04.1–T04.4 |
| 5 | 非默认语言只读复用共用 WebP 变体 | 开启时非默认语言页面得到 `<picture>` | T05.1–T05.4 |
| 6 | Worktree：默认语言 Worker 先行、请求下发共用资源根 | 与进程内构建一致 | T06.1–T06.3 |
| 7 | 文档、CHANGELOG、Starter 主题说明 | — | T07.1–T07.4 |
| 8 | 验证与统一审查 | — | T08.1–T08.3 |

## 4. 任务清单

格式：**编号 名称** — 改动 / 测试与完成标准 / 依赖。源码路径以 `src/Bukit-Core/` 为根，测试以 `tests/` 为根。

### 阶段 0：准备

**T00.1 决策确认（已完成，2026-11-14）**
- 结果：A1–A5 全部采用推荐项，提案决策表已注明。
- 做法：用户确认 A1–A5。结果与推荐不同时，按第 8 节修改提案和本计划，重新跑 `openspec validate add-i18n-shared-assets --strict --no-interactive`。
- 完成标准：提案的决策表标注确认日期。
- 依赖：无。

**T00.2 验证闭包与策略映射**
- 做法：按第 2 节新增 `i18n-shared-assets-change` 规则，内容如下：
  - `matches`：
    - `openspec/changes/add-i18n-shared-assets/*`
    - `docs/plans/2026-11-14-add-i18n-shared-assets-implementation-plan.zh-CN.md`
    - `src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/BaseLayout.html`
    - `tests/Bukit.Engine.Tests/ImageVariantPlanTests.cs`
    - `tests/Bukit.Engine.Tests/SharedAssetScopeTests.cs`
    - `tests/Bukit.Engine.Tests/I18nSharedAssetsTests.cs`
    - 提案归档时再补 `openspec/changes/archive/<日期>-add-i18n-shared-assets/*`。
  - `specialtyTests`：
    - `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~ImageVariantPlan|FullyQualifiedName~SharedAssetScope|FullyQualifiedName~I18nSharedAssets|FullyQualifiedName~ImageProcessingPlugin|FullyQualifiedName~I18nWorktree"`
    - `dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release --filter "FullyQualifiedName~StarterThemeTemplateResource|FullyQualifiedName~ScribanModelKnownFields|FullyQualifiedName~ScribanModelBinder"`
    - `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter "FullyQualifiedName~WorktreeBuildCoordinator"`
    - `openspec validate add-i18n-shared-assets --strict --no-interactive`
    - `bash scripts/checks/docs/size-policy.sh`
    - `bash scripts/checks/docs/active-links.sh`
  - 新加的过滤先用 `--list-tests` 确认能选中测试。阶段 1–6 新建的测试文件路径如有变化，在建文件的同一任务内更新 `matches`。
  - 然后跑 `bash scripts/checks/codex-workflow-self-test.sh`。
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
  - 在 `tests/Bukit.Architecture.Tests/BuiltInPluginSplitTests.cs` 的 `Responsibility_IsTopLevelInternalAndNotAPlugin` 中加入 `[InlineData("ImageVariantPlan")]`。
- 测试：新增 `tests/Bukit.Engine.Tests/ImageVariantPlanTests.cs`，覆盖两种规划、原图宽度等于或小于某个配置尺寸、重复尺寸、命名与反解析互逆、大小写扩展名；`Bukit.Architecture.Tests`：`BuiltInPluginSplit`。
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
  - `SiteDefaultsApplier` 新增 `ReadSharedAssetsConfig(siteNode)`；`ConfigLoader` 构造 `SiteConfig` 时（`ConfigLoader.cs:63-97`）赋值 `SharedAssets = SiteDefaultsApplier.ReadSharedAssetsConfig(siteNode)`。只改 `SiteDefaultsApplier` 不会被调用。
  - `ConfigStrictFieldValidator`：`SiteKeys` 加 `sharedAssets`，再为 `RequireOnly` 加 `enabled`、`paths`。
  - `ConfigJsonSchemaGenerator.SiteSchema` 加入对应 schema。
  - 公共 API：基线加入新类型和新属性，`GovernedTypeCount` 425 → 426。
- 测试：
  - `Bukit.Config.Tests`：`ConfigLoader|StrictYamlContract|ConfigSchemaNestedContract|ConfigJsonSchemaGenerator|ConfigContractDrift`，新增"缺省值"、"读取 `paths`"、"未知子字段报错"三个用例。
  - `Bukit.Architecture.Tests` 完整项目。
  - `public-api-drift.sh check`。
- 依赖：T01.3。

**T02.2 配置校验**
- 改动：按第 1.3 节第 16 条，在生产入口 `ConfigValidator.Validate`（`Bukit.Config/ConfigValidator.cs`）中加入以下规则；只涉及 `site` 节的规则也可以放在它调用的 `SiteConfigValidator.ValidateSite` 中：
  - 空列表、绝对路径、含 `..`、规范化后为根目录的条目报错。
  - 条目等于或位于媒体前缀之内时报错，提示媒体共用不受支持。
  - `enabled: true` 且 `defaultLanguageInSubdir: true` 时报错，信息中同时写出两个字段名。
  - 规范化规则（`\`→`/`、去开头 `/`、补结尾 `/`）只实现一次，供校验和 `SharedAssetScope` 共用，可以放在 `Bukit.Config` 的 internal helper 中，用 `InternalsVisibleTo` 或公开的静态方法提供，以现有工程的可见性约定为准。
- 测试：`Bukit.Config.Tests`：`ConfigPhase2Validator|GeoAiBotModeValidation`。新增用例放在 `ConfigPhase2ValidatorTests`，覆盖上述每条规则的正反情况。SCSS 启用与否只影响默认前缀推导，不是校验规则，对应用例在 T02.4。
- 依赖：T02.1。

**T02.3 忽略警告**
- 改动：在 `I18nLanguagePolicy` 中，与 `WarnLayoutSettingsWithoutLanguages` 并列新增 `WarnSharedAssetsWithoutLanguages`：少于 2 种语言且 `Enabled` 时记录 `event=i18n.config.shared_assets_ignored`。在 `SiteEngine.cs:161` 一并调用。
- 测试：`Bukit.Engine.Tests`：`SiteEngineLoggerTests`，`languages: [en]` 加 `enabled: true` 时出现警告，输出与不写 `sharedAssets` 相同。
- 依赖：T02.2。

**T02.4 `SharedAssetScope`**
- 改动：新建 `Bukit.Engine/SharedAssetScope.cs`，成员与语义按第 1.3 节第 14 条：
  - `TryCreate(config, languages, outputRoot, logger?)`。
  - `Includes` / `Excludes`：SCSS 输出目录只在 `theme.scss.enabled: true` 时加入默认值。
  - `Contains`：包含且不排除。
  - `ContainsSitePath`：去 query / fragment、解码、去开头 `/` 后调用 `Contains`。
  - 另外实现 `CanonicalPrefixes`、`OutputRoot`、`RootBaseUrl`、`Comparer`。
- 测试：新增 `tests/Bukit.Engine.Tests/SharedAssetScopeTests.cs`，覆盖：
  - 默认推导；自定义 `paths`。
  - **`assets/uploads/x.jpg` 不在 scope 内，而 `assets/css/a.css` 在**（默认 `urlBase`）。
  - `urlBase: /media` 时 `assets/uploads/x.jpg` 在 scope 内。
  - `theme.scss.enabled: false` 且 `outputDir: styles` 时，`styles/a.css` 不在 scope 内；启用后在。
  - 大小写不敏感文件系统上的去重；SCSS 输出在根目录时不纳入，且 `logger` 非空时记录一次警告、`logger` 为 `null` 时不记录。
  - 单语言、带前缀布局、未开启时返回 `null`。
  - `CanonicalPrefixes` 与 `paths` 的书写顺序无关。
  - `ContainsSitePath`：`/assets/og.png?v=2`、`/assets/og.png#x`、`/assets/a%20b.png` 在 scope 内；`/assets/uploads/x.jpg?v=1` 不在。
- 依赖：T02.3。

**T02.5 阶段测试**
- 测试：`Bukit.Config.Tests`、`Bukit.Architecture.Tests` 完整项目；`Bukit.Engine.Tests` 的 `SharedAssetScope|SiteEngineLoggerTests|I18nLanguagePolicy`；`public-api-drift.sh check`。
- 依赖：T02.4。

**阶段 2 审查**：校验和 scope 是否共用同一套规范化；默认推导是否与规范的"Default"场景逐条一致；不开启时配置对象之外没有任何行为变化。

### 阶段 3：资源计划、构建顺序、模板变量、渲染哈希

**T03.1 上下文接线**
- 改动：
  - `BuildVariantContext` 新增可选参数 `SharedAssets`（`SharedAssetScope?`；共用资源根和根 baseUrl 都由 scope 提供）。
  - `SiteEngine` 多语言分支用 `TryCreate(config, languages, outputDir, buildLogger)` 创建一次 scope，传给每种语言。
  - `AssetPipelineContext` 新增 `OmittedSharedAssets`，由 `VariantRenderAssetPlanner` 只为非默认语言赋值（用 `I18nOutputLayout.IsRootLanguageVariant` 判断）。
- 测试：`Bukit.Engine.Tests`：`VariantBuildPipeline`。
- 依赖：T02.5。

**T03.2 计划过滤与 tokens**
- 改动：
  - `AssetOutputPlan.Create` 按第 1.3 节第 2 条过滤，并保存 `OmittedSharedItems`。
  - `AssetPipeline.ExecutePreparedAsync` 改为计划中存在 Tokens 条目时才调用 `GenerateTokensAsync`（第 1.3 节第 1 条）。
- 测试：`Bukit.Engine.Tests`：`AssetPipeline`，新增以下用例：
  - 非默认语言计划不含 `assets/**`、tokens、`static/assets/**`；`OmittedSharedItems` 恰好是这些条目。
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
  - `SiteModel` 新增 `public string? AssetBaseUrl { get; init; }`，非 required（第 1.3 节第 17 条）。现有 44 处 `new SiteModel` 不用改。
  - `VariantDataSitePlanner.BuildSiteModel` 总是显式赋值：开启时为 `scope.RootBaseUrl`，否则为语言 baseUrl。
  - `ScribanSiteModelMapper` 输出 `asset_base_url = model.AssetBaseUrl ?? model.BaseUrl`，规范化形式与 `base_url` 相同。
  - `ScribanModelKnownFields` 登记新字段。
  - 更新 API 基线（新增属性，不新增类型）。
- 测试：
  - `Bukit.Rendering.Tests`：`ScribanModelKnownFields|ScribanModelBinder`，新增"`AssetBaseUrl` 为空时 `asset_base_url` 等于 `base_url`"。
  - `Bukit.Engine.Tests`：`VariantBuildPipeline|RouteMetadataRendering`，新增开启 / 关闭 × 根目录 baseUrl / `/repo` 四个组合。
- 完成标准：`asset_base_url` 的类型、默认值和回退行为与第 1.3 节第 17 条一致；`public-api-drift.sh check` 通过。
- 依赖：T03.3。

**T03.5 Starter 主题与渲染哈希**
- 改动：
  - `Bukit.Cli/Resources/StarterTheme/BaseLayout.html`：按第 1.3 节第 13 条为 `asset_base_url` 单独增加 `/` → 空串的映射；样式表改用它；RSS 和 sitemap 链接继续用 `base_url`。
  - 渲染哈希按第 1.3 节第 5 条接线：`RenderDependencyContext` 新增 `SharedAssetPrefixes`，`RenderDependencyHasher.ComputeSnapshot` 增加对应参数，`VariantRenderStage` 传入 `scope?.CanonicalPrefixes`，`SiteIdentityAndModesContributor` 在列表非空时追加。
- 测试：
  - `Bukit.Rendering.Tests`：`StarterThemeTemplateResource`。新增 `site.base_url` 和 `site.asset_base_url` 都为 `/` 时样式表为 `/assets/style.css`（不是 `//assets/style.css`）；二者都为 `/repo` 时为 `/repo/assets/style.css`；共用开启、`base_url` 为 `/repo/zh-CN`、`asset_base_url` 为 `/repo` 时，样式表为 `/repo/assets/style.css`，RSS 为 `/repo/zh-CN/rss.xml`。
  - `Bukit.Engine.Tests`：`RenderDependency|IncrementalEngineIdentity`，覆盖未开启时哈希与基线相同、开启后变化、`paths` 增加一个前缀时变化、前缀书写顺序不影响哈希；开启时根 baseUrl 由 `/` 改为 `/repo`，非默认语言的哈希变化（第 1.3 节第 5 条：`asset_base_url` 不单独入哈希，由此断言覆盖）。
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

**T04.1 地址清单复核（先于改动）**
- 做法：全仓检索 `_baseUrl +`、`BaseUrl +`、`BuildAbsoluteUrl(`、`BuildMaybeAbsoluteUrl(` 等拼接点，列出所有可能把共用资源路径拼上语言 baseUrl 的位置，确定 T04.2–T04.4 的完整范围；清单之外如有新发现，补入本阶段的任务。顺带确认 `SeoImageResolver.cs:36` 对 `record.Media` 的地址是否重复拼接 baseUrl。这个问题与本提案无关，有问题另行记录，不在本阶段修复。
- 完成标准：清单写入阶段记录，每一项标明归入哪个任务，或说明无需修改的理由。
- 依赖：T03.6。

**T04.2 SEO 接线**
- 改动：按第 1.3 节第 6 条的接线链，给以下每层增加可选的 `SharedAssetScope?` 参数（默认 `null`）：
  - `VariantSeoStage`（从 `BuildVariantContext.SharedAssets` 取得）。
  - `SeoPipeline.Execute`。
  - `SeoIndexBuilder.Build`，以及其中调用 `BuildForContent` / `BuildForList` 的三处。
  - `SeoModelBuilder.BuildForContent` / `BuildForList`。
  - `SeoImageResolver`，以及 `SeoJsonLdBuilder.BuildJsonLd` 和其内部辅助方法。
  - 这一步只传参，不改变地址结果。
- 测试：`Bukit.Engine.Tests`：`SeoModelBuilder|SeoAuthorProfile|SeoPublisherJsonLd|SeoJsonLd|GeoSeoModelBuilder`，未开启时结果与基线一致。
- 依赖：T04.1。

**T04.3 SEO 地址规则**
- 改动：按第 1.3 节第 6 条新增 `BuildMaybeAbsoluteUrl` 重载，8 个叶子调用点改为传入 scope。
- 测试：同 T04.2 的过滤。新增用例：
  - 规范场景"Default SEO image on a non-default language page"。
  - `og_image` 在 scope 内与 scope 外各一个。
  - `og_image` 为 `/assets/uploads/x.jpg`（媒体前缀内）时仍使用语言 baseUrl。**测试层级**：这是 `SeoModelBuilder` 单元层断言，直接传入未经媒体规范化的字段值。完整构建中，`ContentMediaOutput.NormalizeDocument`（`ContentMediaOutput.cs:29-44`）会先把自定义字段里的 `/assets/uploads/x.jpg` 改写为 `/zh-CN/assets/uploads/x.jpg`，SEO 拿到的已是本语言地址。
  - `og_image` 为 `/assets/og.png?v=2` 时使用根 baseUrl，query 保留。
  - 作者图片在 scope 内。
  - 绝对 http 地址不变。
  - 未开启时与基线一致。
- 依赖：T04.2。

**T04.4 媒体占位图**
- 改动：按第 1.3 节第 7 条修改 `ContentMediaOutput.RewriteUrl` 和 `ValidateOutputs`。scope 从 `BuildVariantContext.SharedAssets` 取得，路径用 `FileWriter.GetSafeFullPath(scope.OutputRoot, ...)` 解析。
- 测试：
  - `Bukit.Engine.Tests`：`SiteEngineIntegrationTests.MediaLifecycle_|MediaPlaceholderProbe`。
  - 新增规范场景"Media fallback on a non-default language page"。
  - 新增"共用资源根缺少占位图时，非默认语言报出现有的占位图缺失错误"。
  - 新增"`defaultImageUrl` 含 `..` 等越界路径时，仍由 `GetSafeFullPath` 拒绝"。
  - 阶段测试：`Bukit.Engine.Tests` 完整项目。
- 依赖：T04.3。

**阶段 4 审查**：地址规则只依赖 `SharedAssetScope`，没有再写一份前缀判断；未开启时 SEO 输出与基线逐字节一致。

### 阶段 5：图片

**T05.1 插件输入**
- 改动：新增 `BuildDataKeys.SharedAssets`，内容为 scope 加上共用原图清单（`AssetOutputPlan.OmittedSharedItems` 中扩展名为 `.jpg`、`.jpeg`、`.png` 的 `Destination`）。只对非默认语言设置。**设置时机**：清单要等 `AssetPipeline.PrepareAsync`（`VariantBuildPipeline.cs:127-130`）创建 `AssetOutputPlan` 后才有值，不能放在设置 `MediaDownloadDir` 的 `VariantRouteStage.cs:52`。在 `PrepareAsync` 之后、`VariantPluginStages.RunAfterBuildAsync`（`:174`）之前，把 `assetPipelinePreparation.OutputPlan.OmittedSharedItems` 过滤成图片清单，与 scope 一起 `Set` 到 `routePipelineResult.PluginContext`（第 1.3 节第 8 条）。
- 测试：`Bukit.Engine.Tests`：`VariantBuildPipeline`。非默认语言拿到的清单只含 scope 内的图片，不含 `assets/uploads/**`；用一个测试插件在 AfterBuild 中读取 `BuildDataKeys.SharedAssets`，断言清单非空且与计划中被过滤的图片条目一致；默认语言和未开启时读不到这个 key。
- 依赖：T04.4。

**T05.2 只读共用映射**
- 改动（第 1.3 节第 8、9 条）：
  - key 按第 1.3 节第 8 条：`NormalizeBaseUrl(scope.RootBaseUrl).TrimEnd('/') + "/" + destination`，与现有映射同一个 URL 路径空间。
  - 原图只取自 `BuildDataKeys.SharedAssets` 中的清单，不扫描共用资源根。
  - 原图宽度用 `ImageVariantGenerator.TryGetImageWidth` 从共用资源根读取（不用不捕获异常的 `TryReadImageWidth`），返回 `false` 时跳过该原图。
  - 用 `ImageVariantPlan.CandidateWidths` 和 `VariantFileName` 推出 WebP 候选；文件以 `scope.OutputRoot` 为根解析；key 和候选 URL 共用同一个前缀 `NormalizeBaseUrl(scope.RootBaseUrl).TrimEnd('/')`，后接 `/` 和相对输出根的路径，只计算一次。不要直接拼 `scope.RootBaseUrl + "/"`：根 baseUrl 为 `/` 时会得到 `//assets/...`。
  - 有效性用现有的 `ImageVariantFreshness.TryReadFreshness`，`outputDir` 传 `scope.OutputRoot`，不新增方法，也不写入或修复任何文件。
  - 共用映射在 `imageFiles.Count == 0` 的提前返回**之前**构建。
- 测试：`Bukit.Engine.Tests`：`ImageProcessingPlugin`，新增用例：
  - 候选全部存在。
  - 部分候选缺失：只收录存在的。
  - sidecar 与原图哈希不符：不收录。
  - sidecar 缺失：不收录。
  - 共用资源根里有用户自己放的 `hero-480w.jpg`（不在清单中、没有 sidecar）：既不当作原图，也不当作候选。
  - 生成的 URL 以根 baseUrl 开头，不含 `../`。
  - key：根 baseUrl 为 `/` 时为 `/assets/hero.jpg`，为 `/repo` 时为 `/repo/assets/hero.jpg`，均能被 `TryNormalizeLocalImageUrl` 的结果命中。
  - 原图损坏（`InvalidDataException`）或读取失败：跳过该原图，记录 `image_processing.skip` 警告，构建成功。
  - 根 baseUrl 为 `/` 时，候选 URL 以 `/assets/` 开头，不含 `//`。
- 依赖：T05.1。

**T05.3 合并改写与地址识别**
- 改动：
  - `AfterBuildAsync` 的提前返回分支改为：共用映射非空时，仍然执行 `RewriteCurrentHtmlWithWebp`；`RewriteCurrentHtmlWithWebp` 使用"自身映射 ∪ 共用映射"。
  - `ResponsiveImageHtmlTransform.TryNormalizeLocalImageUrl` 新增 `asset_base_url + "/"` 前缀分支，**只在去掉前缀后的路径满足 `scope.Contains` 时**成立，并解析到共用资源根中的文件（第 1.3 节第 15 条）。
  - 默认语言和未开启时的行为不变。
- 测试：`Bukit.Engine.Tests`：`ImageProcessingPlugin|I18nSharedAssets`，新增用例：
  - 规范场景"Shared theme image on two language pages"（`/repo`）。
  - **非默认语言目录中没有任何自有图片时，中文页仍然得到共用 WebP `<picture>`。**
  - 站点 baseUrl 为 `/` 时，中文页中不在 scope 内的 `/images/x.jpg` 不按共用资源处理。
  - 站点 baseUrl 为 `/` 时，中文页中的 `/zh-CN/assets/uploads/x.jpg` 走本语言媒体映射。
  - 单元层（`TryNormalizeLocalImageUrl` 与 `SharedAssetScope`）：`/assets/uploads/x.jpg` 不属于共用集合，也不被当作本语言媒体，查不到映射，保持原 `<img>`。
  - 完整构建层（`SiteEngine`）：`ContentMediaOutput.RewriteUrl` 的前缀列表含 `_rootBaseUrl + _mediaPrefix`（`ContentMediaOutput.cs:187`），中文页里的 `/assets/uploads/x.jpg` 会先被改写为 `/zh-CN/assets/uploads/x.jpg`，再走本语言媒体映射。断言改写后的地址与本语言 WebP，不断言保持原 `<img>`。
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
  - `I18nWorkerRequest` 末尾追加 `string? SharedAssetsRoot = null`。现有构造点 `WorktreeBuildRun.cs:334`、`I18nWorktreeProtocolTests.cs:255`、`I18nWorktreeNotionSnapshotTests.cs:177` 因默认值不用改；生产构造点 `WorktreeBuildRun.cs:334` 显式传入。
  - `WorktreeBuildRun.cs:334-353` 开启共用时传入 staging 根目录。
  - Worker 侧在 `I18nWorktreeBuild.cs:183-230` 构造 `BuildVariantContext` 时，`request.SharedAssetsRoot` 为 `null`（共用关闭或旧请求）时 scope 直接为 `null`，不调用 `TryCreate`；否则用 `TryCreate(request.Config, languages, request.SharedAssetsRoot, logger: null)` 创建。`TryCreate` 内部要对 `outputRoot` 调 `OutputDestinationIdentityComparer`，不能传入 `null`。传 `null` 是为了不在每个 Worker 中重复记录推导警告，警告只在父进程记录一次。
  - 父进程在 `WorktreeBuildRun` 准备阶段用 staging 根目录和父进程 logger 调用一次 `TryCreate`，用于记录推导警告和调度判断。
- 测试：`Bukit.Engine.Tests`：`I18nWorktree`。请求往返序列化时，新字段保留；缺失时还原为 `null`，Worker 得到的 scope 为 `null`。
- 依赖：T05.4。

**T06.2 默认语言 Worker 先行**
- 改动：`WorktreeBuildRun.RunWorkersAsync` 在开启共用时先运行默认语言 Worker，成功后再对其余 Worker 执行现有的并发调度；失败与取消语义沿用现有实现。
- 测试：`Bukit.Cli.Tests`：`WorktreeBuildCoordinator`，新增"默认语言 Worker 完成后其他 Worker 才启动"和"默认语言 Worker 失败时其他 Worker 不启动"两个用例。
- 依赖：T06.1。

**T06.3 等价测试**
- 测试：
  - 新增 `WorktreeBuild_MatchesInProcessOutputs_ForSharedAssets`，仿照现有的 `WorktreeBuild_MatchesInProcessOutputs_ForRootDefaultLanguage`（`WorktreeBuildCoordinatorTests.cs:917`），覆盖规范场景"Worktree and in-process equivalence"：输出包含 WebP 主题图片、占位图和 `seo.defaultImage`，两种构建的相对路径集合和文件内容一致。
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
  - 站点根 baseUrl 为 `/` 时，`asset_base_url` 和 `base_url` 一样也是 `/`，需要先映射为空串再拼路径，否则会得到 `//assets/...`（附 Starter 主题的写法）。
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
| Core-generated site URLs follow the shared set | T04.1–T04.4 | T04.3、T04.4 |
| Non-default languages reuse shared image variants read-only | T01.1–T01.2、T05.1–T05.4 | T01.1、T05.2–T05.4 |
| Shared asset settings affect incremental rendering | T03.5 | T03.5、T03.6 |
| Worktree builds share assets like in-process builds | T06.1–T06.2 | T06.3 |

## 6. 风险与回退

| 风险 | 出现位置 | 应对 |
|---|---|---|
| 非默认语言仍写出 tokens | T03.2 | 第 1.3 节第 1 条；用例断言 `dist/zh-CN/assets/css/theme-tokens.css` 不存在 |
| 拆分调度后失败或取消语义改变 | T03.3、T06.2 | 复用同一个 lambda 和 `SelectPrimaryFailure`；新增失败用例 |
| 渲染哈希变化导致所有现有站点全量重渲染 | T03.5 | 只在生效时追加；用例断言未开启时哈希与基线相同 |
| Starter 主题在根 baseUrl 下输出 `//assets/...`，且未开启共用时也会出现 | T03.5 | 第 1.3 节第 13 条；`/`、`/repo` 和开启共用三组模板用例 |
| 媒体前缀被误判为共用 | T02.4 起各阶段 | 第 1.3 节第 14 条"包含且不排除"；`assets/uploads/x.jpg` 不在 scope 内的精确用例 |
| 非默认语言没有自有图片时，共用 WebP 不生效 | T05.2、T05.3 | 第 1.3 节第 8 条：清单取自计划，共用映射在提前返回之前构建；专门用例 |
| 共用原图清单在 AfterBuild 读到时为空（设置得太早） | T05.1 | 第 1.3 节第 8 条：在 `PrepareAsync` 之后、`RunAfterBuildAsync` 之前设置；AfterBuild 读取用例 |
| 共用映射与查找用的 key 归一化不一致 | T05.2、T05.3 | 第 1.3 节第 8 条写定 key；`/` 与 `/repo` 两组用例 |
| 根 baseUrl 为 `/` 时，把任意站内图片当作共用资源 | T05.3 | 第 1.3 节第 15 条：根前缀分支要求 `scope.Contains` |
| 遗漏某个 Core 拼地址的位置 | 阶段 4 | T04.1 先全仓检索列清单；8 个叶子调用点和接线链逐一测试 |
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
| 0. 前置与核查 | T00.1（决策）、T04.1（地址清单与 `record.Media` 复核） |
| 1. 尺寸规划纯函数 | T01.1–T01.3 |
| 2. 配置 | T02.1–T02.4 |
| 3. 资源计划与构建顺序 | T03.1–T03.3 |
| 4. 模板变量与 Core 拼出的地址 | T03.4、T03.5、T04.1–T04.4 |
| 5. 图片处理 | T05.1–T05.4 |
| 6. 增量 | T03.5 |
| 7. Worktree | T06.1、T06.2 |
| 8. 测试 | T03.6、T06.3 及各任务内新增测试 |
| 9. 文档与契约 | T07.1–T07.3 |
| 10. 验证 | T08.1–T08.3 |

## 8. 决策与推荐不同时的调整

A1–A5 已全部采用推荐项，本节不再适用，保留作日后改选时的参考。

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
| 审计 1 | 2026-11-14 | 按外部审阅修订，共 36 个任务。严重问题：Starter 为 `asset_base_url` 单独映射 `/`（第 1.3 节第 13 条、T03.5）；共用原图清单改取计划中被过滤的条目，共用映射在提前返回之前构建，候选 URL 用根 baseUrl（第 8 条、T05.1–T05.3）；`Contains` 明确为"包含且不排除"（第 14 条、T02.4）。重要问题：渲染哈希经 `RenderDependencyContext` 接线（第 5 条）；`TryCreate` 增加输出根和 logger 并在站点层创建一次（第 14 条）；SEO 接线链拆为 T04.2，地址清单复核提前为 T04.1；`SiteModel.AssetBaseUrl` 定为可空、非 required，并回退到 `BaseUrl`（第 17 条）。次要问题：校验入口改为 `ConfigValidator.Validate`；T00.2 写出具体路径和命令；修正测试名；`ImageVariantPlan` 登记进架构测试；复用 `TryReadFreshness`；SCSS 输出目录只在启用时纳入默认值；占位图路径用 `GetSafeFullPath`。另补审阅未提的一条：根前缀分支须检查 `scope.Contains`（第 15 条）。未采纳"Worktree 漏记单语言警告"：Worktree 只在 2 种及以上语言时启用（`BuildCommand.cs:154`），单语言 + Worktree 的组合不会出现。同步修订 design.md（D2、D6–D9）、规范（改写 4 条要求的措辞，新增 7 个场景）和 tasks.md |
| 决策确认 | 2026-11-14 | 用户确认 A1–A5 全部采用推荐项；T00.1 完成，提案、design.md、tasks.md 同步标注 |
| 审计 2 | 2026-11-14 | 按第二轮外部审阅修订，任务数不变。严重问题：`BuildDataKeys.SharedAssets` 改为在 `PrepareAsync` 之后、`RunAfterBuildAsync` 之前设置（第 1.3 节第 8 条、T05.1），原写法"与 `MediaDownloadDir` 同处"拿不到清单。重要问题：明确 `asset_base_url` 不单独入哈希、由已入哈希的值推出，并加根 baseUrl 变化的断言（第 5 条、T03.5）；写定共用映射的 key（第 8 条、T05.2）。次要问题：T02.1 补 `ConfigLoader` 赋值；T02.2 去掉没有对应规则的 SCSS 用例；`new SiteModel` 计数改为 44；`I18nWorkerRequest.SharedAssetsRoot` 默认 `null`；Worker 字段为空时不调用 `TryCreate`；原图宽度用 `TryReadImageWidth`；新增 `ContainsSitePath` 处理 query、fragment 和编码（第 14 条）。全部 10 条均已对照代码核实并采纳。同步修订 design.md（D8、D9）、规范（增量要求改为"变化即改变哈希"，新增根 baseUrl 变化与带 query 的 SEO 图片 2 个场景）和 tasks.md |
| 复核 | 2026-11-14 | 第三轮复核无严重或重要问题，收紧 3 处实施细节：原图宽度改用会捕获异常的 `TryGetImageWidth`（第 1.3 节第 8 条、T05.2）；T05.2 写明 key 和候选 URL 共用去掉结尾 `/` 的前缀，避免 `//assets/...`；T05.3 媒体用例区分 `/zh-CN/assets/uploads/` 与默认语言的 `/assets/uploads/`。再次复核后注明测试层级：T05.3 的 `/assets/uploads/x.jpg` 和 T04.3 的媒体前缀 `og_image` 在单元层与完整构建层的预期不同，完整构建中 `ContentMediaOutput` 会先改写为本语言媒体地址 |
