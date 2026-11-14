# Design: Share assets across languages

## Context

以下事实于 2026-11-14 按 `main`（`e7651fb1`）逐条核实。

### 资源如何生成

| 资源 | 现状 | 位置 |
|---|---|---|
| 主题 / 父主题 `assets/` | 每种语言复制到语言输出目录的 `assets/` | `AssetOutputPlan.Create`（`Category: Assets`） |
| SCSS 结果 | `AssetSourceWorkspace` 只编译一次；每种语言复制到 `ScssConfig.OutputDir`（默认 `assets`） | `AssetOutputPlan.cs:87-94`、`AssetSourceWorkspace.cs:55-105` |
| Theme tokens | 每种语言生成 `assets/css/theme-tokens.css`（`Operation: Generate`） | `AssetOutputPlan.cs:107-115` |
| `static/` 复制型 | 每种语言复制到语言输出目录 | `AssetOutputPlan.Create`（`Category: Static`） |
| `static/` 渲染型 | 只有站点 `static/` 下的 `*.html`，且配置了 `theme.staticTemplate`；输出为 `x/index.html`。集合只由目录和配置决定 | `RenderEntry.ForStaticDir`、`VariantRouteStage.cs:66-84` |
| 内容媒体 | 按语言复制到 `content.media.urlBase` 下；地址改写为 `<语言 baseUrl><urlBase>/...` | `ContentMediaOutput` |
| 媒体占位图 | `content.media.defaultImageUrl`，默认 `/assets/images/noneimg-news.jpg`；`RewriteUrl` 输出 `<语言 baseUrl>` + 占位图；`ValidateOutputs` 要求它在语言计划输出中并存在于语言输出目录 | `AppConfig.cs:434`、`ContentMediaOutput.cs:105-133,197-205` |
| SEO 图片 | `og_image` / `cover` / `image`、`seo.defaultImage`、作者图片等站内相对地址，经 `BuildMaybeAbsoluteUrl` 加上**语言** baseUrl | `SeoImageResolver.cs:26-45`、`SeoModelBuilder.cs:141,395-410`、`SeoJsonLdBuilder.cs:310-315,499,707` |
| 图片变体 | 语言 AfterBuild 扫描 `OutputDir/assets` 和 `OutputDir + urlBase` 生成变体；freshness sidecar 与变体同目录，路径相对语言输出目录；归属记在语言 manifest 的 `PluginOutputs`（`PluginOutputManifestEntry`） | `ImageProcessingPlugin.cs:307-331`、`ImageVariantOwnership`、`ImageVariantFreshness` |
| 未归属变体 | 已存在但没有有效归属的变体只告警并跳过；只有"sidecar 在、变体缺失"计为失败；失败受 `pluginFailMode` 控制 | `ImageVariantGenerator.cs:99-111,365-369`、`PluginRunner.cs:482-492` |
| webp 改写 | AfterBuild 对当前全部 HTML（含增量跳过的页面）改写裸 `<img>`；已有 `<picture>` 原样保留，幂等，有测试 | `ResponsiveImageHtmlTransform.cs:20-85`、`ImageProcessingPluginTests.cs:567` |
| `TryNormalizeLocalImageUrl` | 要求地址以语言 `baseUrl + "/"` 开头；根 baseUrl 时接受任何路径 | `ResponsiveImageHtmlTransform.cs:110-149` |
| 尺寸规划与命名 | 规划散落 4 处（降尺寸回退不含原图宽度；webp 和 srcset 含原图宽度），`{base}-{w}w{ext}` 命名约 5 处，反解析 3 处；宽度来源也不同（下载缓存 / 输出副本） | `ImageProcessingPlugin.cs:173,187`、`ImageVariantGenerator.cs:288`、`ResponsiveImageHtmlTransform.cs:172-177` 等 |
| 图片数据 | 只写入 `__image_srcsets`，没有生产消费方 | `BuildDataKeys.cs:38-39` |
| 模板引用 | `{{ base_url }}/assets/style.css` | Starter `BaseLayout.html:11` |

### 布局、顺序与生命周期

- 默认布局是根目录布局（`AppConfig.cs:42`）。默认语言的输出目录就是 `dist/`，其他语言是 `dist/<lang>/`。带前缀布局没有根目录保留路径机制。
- 多语言的判定是"2 种及以上语言"（`I18nLanguagePolicy.GetLanguages`）。
- 各语言配置只有 `Site.Language` 和 `Site.BaseUrl` 不同（`SiteEngine.cs:348-355`），主题、assets、static、SCSS、tokens 输入完全相同；不支持按语言覆盖主题或 static。
- 进程内语言用 `Parallel.ForEachAsync` 按 `languageJobs`（默认 1）并发，顺序为 `languages` 列表顺序（`SiteEngine.cs:321-380`）。Worktree 同样按 `languageJobs` 调度 Worker（`WorktreeBuildRun.cs:440-445`），Worker 直接写入 staging 中自己的语言目录。
- 语言管线：路由 → 资源计划和过期清理（`VariantBuildPipeline.cs:127-134`）→ 渲染（含 HTML transform）→ 资源管线 → AfterBuild → 校验计划输出 → 报告阶段。所有语言完成后进入根目录收尾（`CollectAndClean`、合并审计、安全检查），最后保存 manifest。
- `build.clean` 默认 `true`，进程内和 Worktree 的 staging 都从空目录开始；`clean: false` 时复制上次输出。构建事务覆盖整个输出根。
- 受保护清理只由 `build.clean`、manifest 版本不是 3、输出根变化、布局变化、上次构建未完成触发（`BuildPlanner.cs:103-127`）。渲染依赖哈希（`SiteIdentityAndModesContributor` 等）只决定是否重新渲染。
- 语言的过期清理（`PrepareProjectionWrites`→`DeleteStale`、`TrackAssetPlanOutputs`）按该语言上次的 `OwnedOutputs` / `Assets` 删除本次不再计划的文件。
- 根目录布局下，`I18nReservedPrefixGuard.ValidateAssetPlan` 已经拒绝默认语言资源落在其他语言代码目录下，所以语言代码 `assets` 与默认语言的 `assets/` 冲突时构建已经会失败。

## Goals

- 根目录布局的多语言站点，可以让主题资源、tokens 和 `static/assets/` 只输出一份。
- 主题用同一个变量引用共用资源，开关切换不需要改模板。
- 不新增归属方、不改 manifest 结构、不新增跨语言同步原语；进程内与 Worktree 构建输出一致。

## Non-Goals

见 proposal.md "Non-Goals"。

## Decisions

### D1. 配置与适用范围

- `site.sharedAssets.enabled` 默认 `false`（A1）。
- 少于 2 种语言时设置无效；显式 `enabled: true` 时记录警告 `event=i18n.config.shared_assets_ignored`，与 `WarnLayoutSettingsWithoutLanguages` 放在一起。
- `enabled: true` 且 `defaultLanguageInSubdir: true` 时配置校验失败（`ConfigException`），提示改用根目录布局或关闭共用（A3）。

### D2. 有效共用集合

- `paths` 是**输出路径前缀**（相对语言输出目录）。不写时为 `assets/` 加 SCSS 输出目录（去重）；写了则完全替换。
- 规范化：`\` 统一为 `/`，去掉开头的 `/`，补结尾 `/`。规范化后为根目录（如 `""`、`/`、`.`）的条目，以及空列表、绝对路径、含 `..` 的条目，配置校验报错。默认推导中 SCSS 输出目录为根目录时不加入，并记录警告。
- 前缀匹配和去重使用 `OutputDestinationIdentityComparer` 的大小写规则。
- **媒体前缀**（`content.media.urlBase` 去掉开头的 `/` 并补 `/`）始终从集合中扣除（A5）。显式条目等于媒体前缀或位于媒体前缀之内时报错，说明媒体共用不受支持；条目包含媒体前缀（如 `assets/` 包含 `assets/uploads/`）时，只在内部扣除。
- **有效共用集合** = 配置前缀（或默认前缀）减去媒体前缀。下文的"共用集合"都指它。

### D3. 默认语言是唯一归属方（A2）

- 默认语言的资源计划、写入、manifest 归属和过期清理**完全不变**。它在根目录布局下本来就写 `dist/assets/` 等路径。
- 非默认语言在资源计划中去掉满足以下全部条件的条目：
  - `Category` 是 `Assets`、`Tokens` 或 `Static`；
  - `Operation` 是 `Copy` 或 `Generate`；
  - `Destination` 落在共用集合内。
- 这些条目仍由默认语言输出（各语言输入相同，见 Context），所以不需要跨语言冲突检测。
- 根 manifest、语言 manifest 结构不变，不升版本号。

### D4. 其他输出不受影响（A4）

页面、渲染型 static、插件输出、投影和媒体照常按语言输出，即使落在共用前缀下也不报错。理由：

- 删掉的只有复制型和生成型条目，剩余输出与默认语言的共用文件不在同一目录（非默认语言写 `dist/<lang>/...`），没有物理冲突。
- 这些输出按语言变化，主题本来就应当用 `base_url` 引用它们。主题作者说明写明："`asset_base_url` 只用于共用集合内的复制型资源。"

### D5. 构建顺序

- 开启共用时，默认语言先完整构建（含 AfterBuild 和报告阶段），之后其他语言再按 `languageJobs` 并发构建。
- 进程内：在 `SiteEngine` 的语言调度中先单独运行默认语言，再对其余语言执行现有的 `Parallel.ForEachAsync`。默认语言失败时不启动其他语言。
- Worktree：父进程先运行默认语言 Worker，成功后再并发运行其余 Worker。同时活动的 Worker 仍不超过 `languageJobs`，满足 `i18n-worktree-build` "两级并发语义必须保持明确"。
- 关闭共用时调度不变。
- 代价：`languageJobs > 1` 时，默认语言阶段不能与其他语言并行，总耗时约为"默认语言 + 其余语言中最慢的一个"。`languageJobs` 默认为 1，默认配置下没有额外代价。
- 这是站点级的语言先后顺序，语言管线内部阶段顺序不变。

### D6. `site.asset_base_url`

- 开启共用：等于站点根 baseUrl（`NormalizeBaseUrl(site.BaseUrl)`，保留 `/repo`，不含语言段），来自现有 `BuildVariantContext.RootBaseUrl`。
- 关闭共用或单语言站点：等于当前语言的 `base_url`。
- 与 `base_url` 采用相同的规范化形式。
- 接入点：`SiteModel` 新增属性（公共 API 基线）、`VariantDataSitePlanner.BuildSiteModel`、`ScribanSiteModelMapper`、`ScribanModelKnownFields`。

### D7. Core 拼出的站内地址

Bukit 不改写主题 HTML，但 Core 自己生成的地址必须跟随共用规则：

- **SEO 地址**：`BuildMaybeAbsoluteUrl` 处理站内相对地址时，若去掉开头 `/` 后的路径落在共用集合内，使用根 baseUrl，否则使用语言 baseUrl。所有调用点（SEO 图片、作者图片和链接、JSON-LD）统一走这一规则。
- **媒体占位图**：非默认语言中，占位图路径落在共用集合内时：
  - `RewriteUrl` 输出根 baseUrl + 占位图；
  - `ValidateOutputs` 改为检查共用资源根中的文件是否存在，不再要求它出现在本语言的计划输出中。默认语言先构建（D5），文件此时已经存在。

### D8. 图片处理

1. **尺寸规划与命名**：抽成同一个纯函数，区分"要生成的文件"（降尺寸回退不含原图宽度）和"srcset / WebP 候选"（含原图宽度），并统一命名和反解析。生成器、webp 映射、`srcset` transform 和非默认语言的只读映射都调用它。这一步也可以作为独立的前置重构先做。
2. **默认语言**：AfterBuild 行为不变，照常扫描 `dist/assets/` 并生成、归属、清理变体。
3. **非默认语言**：
   - 扫描自己的 `assets/` 和媒体目录的逻辑不变；去掉共用条目后，语言目录内一般只剩媒体。
   - 对共用集合内的原图，**只读**地构建共用 WebP 映射：用尺寸规划函数算出候选文件名，只收录文件存在、且 freshness sidecar 对当前原图有效的变体。freshness 路径相对默认语言输出目录，也就是共用资源根。
   - webp 改写使用"本语言映射 ∪ 共用映射"。
   - `TryNormalizeLocalImageUrl` 同时接受本语言 `baseUrl + "/"` 和 `asset_base_url + "/"` 开头的地址，后者解析到共用资源根中的文件。
   - 不在共用集合内生成、改写或删除任何文件，不对共用集合执行孤儿清理和 freshness 清理。
4. **失败策略不变**：默认语言的变体生成失败仍受 `pluginFailMode` 控制。`warn` 模式下变体缺失时，非默认语言的只读映射不收录它，对应页面保留原 `<img>`，与 `responsive-next-gen-images` "HTML 引用必须由成功产物驱动"一致。
5. `srcset` transform 只处理媒体，本版不变。`__image_srcsets` 没有消费方，保持现状。

### D9. 增量与开关切换

- `sharedAssets` 的有效值（`enabled`、有效共用集合）和 `asset_base_url` 进入 `SiteIdentityAndModesContributor`。值变化时全部页面重新渲染，增量跳过的页面不会保留旧地址。
- 开启：非默认语言上次归属的 `<lang>/assets/...` 不再出现在本次计划中，由现有过期清理删除。
- 关闭：非默认语言重新计划并写出这些文件。
- 默认语言的输出不变，不需要新增受保护清理条件。`build.clean` 默认 `true` 时 staging 本来就从空目录开始。

### D10. Worktree

- 父进程按 D5 的顺序调度 Worker。
- Worker 请求新增共用资源根路径（staging 根目录，也就是默认语言 Worker 的输出目录）。父进程和 Worker 是同一二进制，不升 `i18n-worker-request.v1`。非默认语言 Worker 用它完成 D7 的占位图校验和 D8 的只读映射。
- Worker 运行同一条语言管线，D3 的计划过滤在 Worker 内生效。父进程现有的 `ValidateOutputPaths` 和 `CollectAndClean` 冲突检查照常执行。
- 进程内构建和 Worktree 构建产出相同的相对文件路径集合和相同内容。

## Alternatives

### v1：根目录归属层（已放弃）

v1（2026-11-13）让"根目录这一层"在构建前写出共用资源，在根 manifest 中新增插件输出记录并升版本号，收尾时按跨语言引用并集清理，同时覆盖两种布局和媒体。按当前代码核实后放弃，原因：

- **会删掉自己刚写的共用文件**：根目录布局下，默认语言上次的 manifest 列着 `assets/...`。构建前写好的共用文件会被该语言的 `DeleteStale` / `TrackAssetPlanOutputs` 当作过期文件删除。
- **升版本号会清空所有站点**：根 manifest 与语言 manifest 是同一个 `BuildManifest` 类型，`Record` 把版本写死为 3。升版本号会强制所有站点执行受保护清理，而 `PluginOutputs` 字段本来就存在。
- **依赖不存在的机制**：v1 假设"站点哈希变化会触发受保护清理"、"带前缀布局有根目录保留路径"，代码中都不存在。
- **防的冲突不会发生**：跨语言冲突检测保护的场景实际上不会出现（各语言输入相同）。
- **新增致命失败**：v1 为了 Worktree 预测，把共用变体失败一律改成致命错误。这会让一张用户放置的 `hero-480w.jpg` 导致构建失败。
- **不能单独交付**：v1 第一步遗漏了占位图和 SEO 图片地址，无法独立交付。

v1 中仍有价值的部分，即跨语言媒体引用并集、共用结果缓存、Worktree 预测，留给后续的媒体共用提案。

### 只在进程内传递 WebP 映射

在进程内把默认语言的映射对象直接传给其他语言，可以省掉一次磁盘扫描，但 Worktree 仍需另一条路径。D8 统一从磁盘只读构建，两种构建只有一条代码路径。

## Risks

| 风险 | 缓解 |
|---|---|
| 主题仍用 `base_url` 拼共用资源 | 默认关闭；主题作者说明；Starter 主题示范 |
| JS 运行时拼地址、CSS 根绝对路径 | 文档写明须使用 `asset_base_url` 或相对路径 |
| `languageJobs > 1` 时默认语言阶段串行 | 在用户指南中说明；默认配置无影响 |
| 非默认语言只读映射与默认语言实际产物不一致 | 共用同一尺寸规划函数；只收录存在且 freshness 有效的文件；进程内与 Worktree 等价测试 |
| 遗漏某个 Core 拼地址的位置 | 实施前 grep 全部 `BuildMaybeAbsoluteUrl`、`_baseUrl` 拼接点并列入测试 |
| 图片插件改造回归 | 默认语言路径不变；非默认语言只新增只读分支；变体、freshness、webp 改写各有测试 |

## Open Questions

- proposal.md 中的决策 A1–A5 待用户确认。
- 媒体条目 `record.Media` 的地址在 `ContentMediaOutput` 中已带语言 baseUrl，`SeoImageResolver` 又经 `BuildAbsoluteUrl` 拼一次 baseUrl。实施时确认是否存在重复前缀（与本提案无关，有问题则另行记录）。
