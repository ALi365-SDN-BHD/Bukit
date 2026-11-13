# Bukit 默认语言根目录布局的旧地址迁移跳转提案

- 日期：2026-11-13
- 基线：`53b7a510b710d71d2a010da515dbbea937fe359b`
- 前置提案：`docs/analysis/bukit-core-i18n-default-language-root-proposal-2026-11-12.zh-CN.md`（第 9 节"后续事项"第一条）
- 范围：`site.defaultLanguageInSubdir: false` 时，为默认语言旧地址 `/<lang>/...` 派生跳转页
- 本轮性质：**只分析和提案，不实施**。未修改源码、测试、配置、CI/gate；未运行 `dotnet test`。第 6 节的决策已由用户按推荐全部确认（2026-11-13），实施须作为单独的 Core 任务开展（`AGENTS.md` "Website/Core isolation"）。

---

## 1. 问题

PR #128–#136 增加了默认语言根目录布局。站点从带前缀布局（`dist/en/`）切到根目录布局（`dist/`）后：

- 旧首页 `/en/` 有跳转页兜底：`I18nLegacyRedirectDeriver` 通过共用的 `RedirectPageDocument` 派生一个 noindex 跳转页，目标为站点根目录（受 `site.defaultLanguageRedirect` 控制，默认开启）。
- 其他旧地址 `/en/<path>/` 全部 404。文档（`guide/user/11-i18n-seo.md`、i18n skill）要求在托管层配置 `/<lang>/*` → `/*` 的 301。

GitHub Pages 等纯静态托管做不了 301，所以用户只能二选一：放弃根目录布局，或者给每个页面手写 `aliases: [/en/<path>/]`。站点外部的反向链接、书签、搜索结果都指向旧地址，靠手写 alias 维护的成本太高。

## 2. 现状代码

| 位置 | 现状 | 与本提案的关系 |
|---|---|---|
| `Bukit.Engine/I18nLegacyRedirectDeriver.cs` | 只为 `/<lang>/` 派生一个跳转页；检查 RoutedDocuments、DerivedDocuments、StaticHtmlRoutes、默认语言及父主题 `static/` 中是否已有同路径输出，有则跳过并记日志 `event=i18n.legacy_root_redirect.skipped` | 扩展点。现在只处理一个固定路径，迁移跳转需要对每条默认语言路由做同样的事 |
| `Bukit.Engine/RedirectPageDocument.cs` + `RedirectPageHtml.cs` | 共用跳转页文档：meta refresh、canonical、`noindex`、`<title>`；custom fields 设 `excludeFromSitemap/Search/Feed`、`robots: noindex`；路由模板为 `__redirect__`，不经主题渲染 | 直接复用，跳转页自动排除于 sitemap、搜索、feed、llms、agent-manifest、内容投影（`I18nRootLayoutTests.LegacyRedirect_IsExcludedFromIndexesAndReports` 已覆盖首页） |
| `Bukit.Engine/VariantBuildPipeline.cs:59` | 在插件 Derive 阶段（含 `AliasPlugin`）之后调用 `I18nLegacyRedirectDeriver.Derive`，然后 `AddDerivedListRoutesToGraph`、`ValidatePostDeriveRoutes`、`I18nReservedPrefixGuard.ValidateRoutes` | 迁移跳转派生的时机：必须晚于用户 alias 和插件派生页，才能让用户内容优先；又要早于保留前缀检查和渲染 |
| `Bukit.Engine/I18nReservedPrefixGuard.cs` | 根目录布局下检查默认语言所有输出的第一个路径段，不能等于**其他**语言代码；默认语言自身代码不保留（`I18nOutputLayout.ReservedPrefixes`） | 迁移跳转都写在 `<defaultLang>/...` 下，天然通过检查（见 4.2） |
| `Bukit.Engine/BuildTransaction.cs:415-450` `GeneratedOutputLinkPaths` | 根目录布局 + `defaultLanguageRedirect` 时把 `<lang>/index.html` 列为禁止穿越符号链接的生成路径 | 只检查所列路径各段，改成 `<lang>` 目录不能覆盖嵌套链接；保持不变，符号链接处理见 4.8 |
| `Bukit.Engine/Incremental/SiteIdentityAndModesContributor.cs` | `defaultLanguageInSubdir`、`defaultLanguageRedirect` 只在非默认值时写入站点哈希 | 新配置是否写入哈希见决策 D7 |
| `Bukit.Engine/I18nWorktreeProtocol.cs:309-334` `ValidateOutputPaths` | 父进程对根目录语言 Worker 的全部结果路径（含 DerivedDocuments、manifest entries）做保留前缀检查和安全路径检查 | 迁移跳转作为 DerivedDocuments 进入快照，走同一检查 |
| `Plugins/BuiltIn/AliasPlugin.cs` | 用户 `aliases` 生成派生跳转页，目标为页面最终地址 | 用户内容的一种；旧前缀下的 alias 地址是否也迁移见决策 D5 |
| `Plugins/BuiltIn/TaxonomyRedirectWriter.cs` | 分类别名跳转页在 AfterBuild 直接写文件（插件输出），不是派生文档 | 派生阶段看不到，本版本不覆盖（见 D5） |

## 3. 主流工具做法（简述）

本轮没有重新调研外部工具，下面只引用前置提案已有的结论：Hugo 在默认语言放根目录时只生成 `/<lang>/` → `/` 一个跳转（与 Bukit 现状一致），不为全部旧地址生成跳转；Astro、Docusaurus 等交给托管层或 `redirects` 配置。**为全部旧地址自动派生跳转属于 Bukit 自己的扩展**，因此默认关闭、需要显式开启。

## 4. 设计要点

### 4.1 配置项

新增 `site.defaultLanguageLegacyRedirects`，布尔，**默认 `false`**（命名候选见 D1）。

| `defaultLanguageInSubdir` | `defaultLanguageRedirect` | `defaultLanguageLegacyRedirects` | 结果 |
|---|---|---|---|
| `true` | 任意 | `true` | 无旧地址可迁移；记警告日志 `i18n.config.legacy_redirects_ignored`，不派生（见 D3） |
| `false` | `true` | `false` | 现状：只有 `/<lang>/` 跳转 |
| `false` | `true` | `true` | `/<lang>/` 及全部默认语言 HTML 路由的旧地址都有跳转 |
| `false` | `false` | `true` | 首页由 `defaultLanguageRedirect` 单独控制（见 D2），`/<lang>/` 不跳转，其余旧地址有跳转 |
| 单语言站点 | — | `true` | 沿用 `I18nLanguagePolicy.WarnLayoutSettingsWithoutLanguages` 的 `i18n.config.default_language_layout_ignored` 警告（该事件含义正是"`site.languages` 为空"），在其字段中加入新配置。该方法在 `DefaultLanguageInSubdir && DefaultLanguageRedirect` 时提前返回，提前返回条件必须加上"新配置为 `false`"，否则只开新配置时不会有任何警告 |

### 4.2 派生规则

对默认语言变体，在用户 alias 与插件派生页产生之后，对每条**HTML 路由**派生一个跳转文档。**首页路由 `/`（输出 `index.html`）始终排除**，旧首页 `/<lang>/` 只由 `defaultLanguageRedirect` 控制（D2）；首页分页 `/page/2/` 等不受此限。

- 输出路径：`<lang>/` + 路由的 `OutputPath`（如 `blog/post/index.html` → `en/blog/post/index.html`；以扩展名结尾的路由 `/about.html`（输出 `about.html`）→ `en/about.html`）。
- 路由 URL：`/<lang>` + 路由 URL。
- 目标：`SeoModelBuilder.BuildAbsoluteUrl(site.Url, context.BaseUrl, route.Url)`，与旧首页跳转相同：配置了 `site.url` 用绝对地址，否则用 baseUrl 相对地址（项目站 `/repo` 下 `/repo/en/foo/` → `/repo/foo/`）。
- 文档 ID 与 slug：都用 `i18n-legacy-<lang>-<短哈希>`，短哈希取规范化输出路径（小写）的 SHA-256 前 16 位十六进制。这样既稳定、方便增量比对，又是单段、不带扩展名的安全 slug：`BuildReporterSecurity.CheckSlugs` 会对每个派生文档的 slug 调用 `RouteSecurityValidator.ValidateSlugSegment`，它只接受单段、不带文件扩展名的值，直接用路径（如 `blog/post`、`about.html`）会让安全报告的 slug 检查失败（T20）。
- 复用 `RedirectPageDocument.Create`，不新增 HTML 形态。

纳入的路由来源（见 D4）：

| 来源 | 纳入 | 说明 |
|---|---|---|
| 内容页（RoutedDocuments） | 是 | 主体 |
| 集合列表、分页（派生前的 `RouteResult.ListRoutes`） | 是 | 旧 `/en/blog/page/2/` 也有外链。只取派生前已存在的列表路由（见 4.6） |
| 插件派生的普通页面（非跳转 DerivedDocuments） | 是 | 如分类索引页、词条页。它们之后会被 `AddDerivedListRoutesToGraph` 编入列表路由图，不能再从列表路由取一次 |
| 默认语言 `static/` 中的 HTML（StaticHtmlRoutes） | 是 | 旧 `/en/foo.html` |
| 已有跳转文档（alias、旧首页） | 见 D5 | 不做"跳转的跳转"。识别依据：路由模板为 `RedirectPageHtml.RouteTemplate`（`__redirect__`），或 custom field `type` 为 `redirect`；不按 `collection` 或 ID 前缀判断。D5 b 只处理带 `redirectTarget` custom field 的 alias 跳转（旧首页跳转不带此字段） |
| `404.html` | 否 | 旧 `/en/404.html` 不被托管层使用 |
| 非 HTML（feed、sitemap、search.json、资源、媒体） | 否 | meta refresh 只对 HTML 有效；文档说明需托管层处理或重新提交 |

**按输出路径去重**：各来源合并后，按输出路径（`OrdinalIgnoreCase`）去重再派生，同一路径只派生一份跳转。分类页同时是 DerivedDocuments 和 `TaxonomyIndex`/`TaxonomyTermPage` 列表路由，不去重会在 `en/tags/x/index.html` 派生两份，`RouteInventoryValidator.ValidateFinalRoutes` 报重复输出，构建失败（T15）。

### 4.3 与保留前缀检查的关系

- 迁移跳转的第一个路径段恒为默认语言代码，`I18nReservedPrefixGuard` 不保留该代码，因此 `ValidateRoutes`、Worktree 父进程的 `ValidateWorktreeOutputs` 都会放行，无需改动守卫本身。
- 默认语言路由的第一个路径段若等于其他语言代码，守卫在派生之后、渲染之前已经报 `i18n-route-reserved-language-prefix`，所以不会出现 `en/zh-CN/...` 这类跳转。实施时保持"先派生、后检查"的顺序，让守卫同时覆盖派生出的跳转（作为防线）。
- 大小写：派生路径直接用路由的原始大小写；占用比较沿用 `I18nLegacyRedirectDeriver.SamePath` 的 `OrdinalIgnoreCase`，与守卫一致，避免 macOS 与 Linux 结果不同。

### 4.4 用户内容优先

与现有旧首页跳转规则一致：目标路径已被以下任一占用时，跳过该条跳转：

1. RoutedDocuments（如新布局下 slug 恰好以 `en/` 开头的页面 `/en/x/`）；
2. DerivedDocuments（用户 `aliases: [/en/old/]`、插件派生页）；
3. ListRoutes（现有旧首页派生器**没有**检查列表路由，见第 7 节 F1）；
4. StaticHtmlRoutes，以及默认语言和父主题 `static/` 中同路径源文件（`DirectoryCopy.StaticSourceExists`，沿用 `StaticCopyOptions` 的 dotfile 与符号链接规则）。

占用判断先用以上全部来源构建一个 `OrdinalIgnoreCase` 的规范化路径 HashSet，再逐条查询。不能沿用现有派生器每次 `.Any` 线性扫描并重复调用 `NormalizeRelPath` 的写法：N 条路由下那是 O(N²)，5 万条路由约 50 亿次带字符串分配的比较，派生阶段会耗时数分钟（T16 用算法断言固定，不用耗时上限）。

跳过时不再逐条打 Info 日志（数量可能很大），改为汇总：`event=i18n.legacy_redirects.derived count=<n> skipped=<m> bytes=<b>`，`skipped` 大于 0 时附前 N 条路径（D8）。

插件在 AfterBuild 写出的同路径文件（如 `en/foo/index.html`）在派生阶段不可见，由现有 `ValidateGeneratedOutputsDoNotReplacePages` 拦截并使构建失败。这与旧首页跳转现有行为一致，文档里写明"插件输出不应占用默认语言旧前缀"。

### 4.5 增量归属与关闭后的清理

- 迁移跳转是普通派生文档，按输出路径进入 manifest `Entries`，渲染后计入 `CurrentKeys`；`PluginPipeline.ExecuteAsync` 会移除不在 `CurrentKeys` 中的旧条目。关闭配置后，下一次构建不再派生，这些条目和文件应随之清理。
- 已有证据：`I18nRootLayoutTests.DisablingRedirect_RemovesTheLegacyPage`（非增量）、`IncrementalRemoval_DoesNotTouchAnotherLanguage`（增量删除内容页，不影响其他语言）。**未核实**：增量模式下派生文档消失时文件删除走的具体路径；现有旧首页没有增量关闭的测试。实施时须先补测试 T7 再改代码。
- 用户后来新增占用同路径的内容：同一输出路径从"跳转"变为"页面"，manifest 条目被覆盖，不应残留跳转 HTML（T8）。
- 布局切回 `defaultLanguageInSubdir: true`：布局变化触发现有受保护清理，`en/<path>/index.html` 恢复为真实页面；与 `SwitchingLayout_CleansThePreviousLayout` 的首页场景相同，需扩展到非首页路径（T9）。
- 迁移跳转只写在 `<lang>/` 下，不会触碰其他语言目录；关闭时也只删 manifest 记录过的路径，不删用户放在 `<lang>/` 下的 static 文件。

### 4.6 派生时机

**保持现有位置**：插件 Derive 之后、`AddDerivedListRoutesToGraph` 之前（`VariantBuildPipeline.cs:59`）。

`AddDerivedListRoutesToGraph`（`VariantBuildPipeline.ListRoutes.cs`）只做一件事：用 `ListRouteGraphBuilder.AddDerivedTaxonomyRoutes` 把派生阶段已经存在的分类派生文档编入列表路由图。集合列表和分页在派生前就已在 `RouteResult.ListRoutes` 中。因此在现有位置就能拿到全部来源：RoutedDocuments、派生前的 ListRoutes、非跳转 DerivedDocuments、StaticHtmlRoutes。跳转文档不会被 `AddDerivedTaxonomyRoutes` 当成分类路由：`ListRouteGraphBuilder.TryBuildTaxonomyRoute` 要求 custom field `type` 为 `derived` 且带 `taxonomy` 对象字段，跳转文档的 `type` 为 `redirect`，也没有 `taxonomy` 字段；随后的 `ValidatePostDeriveRoutes` 和保留前缀检查能看到全部跳转。不需要拆分派生步骤。

### 4.7 输出体积

单个跳转页大小 ≈ 240 字节固定部分 + 4 × 目标 URL 长度（meta refresh、canonical、正文链接的 href 和文字各一次，HTML 转义后）。目标为 60 字符的绝对地址时约 480 字节。

| 默认语言 HTML 路由数 | 新增文件数 | 新增体积（约） |
|---|---|---|
| 1,000 | 1,000 | 0.5 MB |
| 10,000 | 10,000 | 4.8 MB |
| 50,000 | 50,000 | 24 MB |

字节数不是主要问题，代价主要在下面几处：

- **文件数翻倍**（默认语言部分）：影响上传和部署时间。GitHub Pages 文档建议发布站点不超过 1 GB〔文档，本轮未重新核实〕，对文件数没有明确上限。
- **构建状态**：每条跳转一条 manifest entry，`.bukit-build-state.json` 相应变大。
- **报告与计数**：派生文档会进入以下消费方，开启后默认语言部分约翻倍：
  - `publish-audit-report.json`（现在旧首页以 `indexable: false` 出现，`RootLayout_LegacyHomeRedirectIsNotIndexed`；D6 决定逐条列出）；
  - routes 报告（`RoutesReportWriter`）和 build-report 路由清单（`BuildReporter`）；
  - 路由计数：`BuildResult` 的总路由数和 `--metrics` 中各语言的路由数，按目录或计数监控的外部工具会看到跳变；
  - `BuildReporterSecurity.CheckSlugs` 的 slug 校验（slug 格式要求见 4.2）；
  - AfterBuild 插件收到的 `BuildDataKeys.CurrentHtmlOutputs`（`ImageProcessingPlugin` 会多扫描 N 个 HTML 文件）。
  CHANGELOG 和用户指南需说明开启后上述数字的变化；T17 固定这些计数。
- **Worktree 快照**：派生文档连同 body HTML 序列化进 `I18nVariantSnapshot.DerivedDocuments`，快照大小随 N 线性增长。

不设硬上限；通过 D8 的汇总日志让用户看到数量和体积。

### 4.8 Worktree 构建等价

- 进程内构建和 Worktree Worker 都走 `VariantBuildPipeline`，派生逻辑只写一处，两种模式自然一致。
- 默认语言 Worker 写到 staging 根目录，迁移跳转落在 `staging/<defaultLang>/`；其他语言 Worker 写 `staging/<lang>/`，语言代码去重且不区分大小写，写入区域不重叠。
- 父进程 `ValidateOutputPaths` 已经把 `snapshot.DerivedDocuments` 和 `snapshot.Manifest.Entries` 纳入检查，无需新增分支。
- 符号链接：`FileWriter.GetSafeFullPath` → `SafePathResolver.RejectEscapingSymlinkSegments` 已逐段拒绝**指向输出根目录之外**的符号链接，每条跳转写入都会经过它。它不拒绝指向输出根目录**之内**的链接，例如 `dist/en/blog → ../zh-CN/blog`，这时跳转会写进其他语言目录。`BuildTransaction.GeneratedOutputLinkPaths` 只检查所列路径的各段，把它改成 `<lang>` 目录只多检查 `dist/en` 本身，查不到 `en/blog` 这类嵌套链接，**不提供额外保护**，因此保持现有的 `<lang>/index.html` 不变。实施时二选一并用 T13 固定：a) 写入跳转前对每条跳转路径做与 `RejectGeneratedOutputLinks` 相同的逐段检查（N × 路径深度次 stat）；b) 确认并用测试证明构建总是写入新建的 staging 目录，旧输出中的链接不会出现在写入路径上。
- 规范上在 `i18n-worktree-build` 增加"迁移跳转在 Worktree 与进程内构建结果一致"的场景（T10）。

## 5. 影响与风险

| 编号 | 风险 | 缓解 |
|---|---|---|
| R1 | meta refresh 不等于 301，搜索引擎迁移信号较弱 | 跳转页带 canonical + noindex；文档仍推荐支持 301 的托管层优先用 301，迁移跳转定位为"纯静态托管的兜底" |
| R2 | 托管层已配置 `/<lang>/*` → `/*` 301 时，静态文件可能盖过未加 `force` 的规则（Netlify） | 默认关闭；文档写明已有 301 的站点不要开启 |
| R3 | 文件数和构建状态随页面数线性增长 | 默认关闭；汇总日志报告数量与体积（D8）；报告是否逐条列出见 D6 |
| R4 | 插件 AfterBuild 输出与跳转同路径导致构建失败 | 现有 `ValidateGeneratedOutputsDoNotReplacePages` 报错；文档说明 |
| R5 | 非 HTML 旧地址（`/en/rss.xml`、split 模式 `/en/sitemap.xml`）仍然 404 | 文档说明：feed 订阅者需迁移，sitemap 重新提交；不在本版本处理 |
| R6 | 以后删除页面时旧地址的跳转随之消失 | 符合预期（新地址也已不存在）；需要永久保留的旧地址用 alias |
| R7 | 增量关闭时残留跳转文件 | 先补 T7 测试，确认清理路径后再实施 |

## 6. 已确认的设计决策

用户于 2026-11-13 按推荐确认 D1–D9。D7 带前提：T7 和 T18 都通过才采用 a，否则退回 b。审计后（见修订记录）D2 的执行细节、D3 的事件名、D7 的前提已按审计结论修订，决策方向不变。

| # | 决策 | 选项 | 结果（已确认） |
|---|---|---|---|
| D1 | 配置项名称 | a) `site.defaultLanguageLegacyRedirects`；b) `site.defaultLanguageMigrationRedirects`；c) 把现有布尔 `defaultLanguageRedirect` 改成枚举 `home\|all\|false` | a。c 会改变已发布配置项的类型，属于破坏性变更 |
| D2 | 与 `defaultLanguageRedirect` 的关系 | a) 两者独立：首页只由 `defaultLanguageRedirect` 控制，新配置只管非首页；b) 新配置开启时连首页一并包含 | a，语义清楚、不改现有行为；`defaultLanguageRedirect: false` 且新配置为 `true` 时记一条 Info 说明首页不跳转。派生规则对首页路由 `/` 始终排除（4.2），保证 `defaultLanguageRedirect: false` 时不会经由新配置写出 `/<lang>/` 跳转 |
| D3 | `defaultLanguageInSubdir: true`（多语言）时开启新配置 | a) 警告并忽略；b) 配置校验失败 | a，用户来回切换布局时不会因此构建失败。新设事件 `i18n.config.legacy_redirects_ignored`（`default_language_layout_ignored` 的含义是"`site.languages` 为空"，用户指南也这样记录，不能复用）；单语言站点仍走原事件 |
| D4 | 纳入哪些路由 | 4.2 表格中的内容页、列表/分页、插件派生普通页、static HTML | 按表格；不含 404 与非 HTML |
| D5 | 旧前缀下的 alias 与分类别名 | a) 都不迁移；b) 为 alias 派生 `/<lang>/<alias>/` → 页面最终地址（不经二次跳转），分类别名不迁移；c) 两者都迁移 | b。分类别名是 AfterBuild 插件输出，派生阶段看不到，c 需要改 `TaxonomyPlugin`，代价大。第一段已经是默认语言代码的 alias（如用户为迁移手写的 `/en/old/`）跳过，不派生 `/en/en/old/` |
| D6 | 报告中是否逐条列出迁移跳转 | a) 与旧首页一致，逐条列入 publish-audit 等报告（`indexable: false`）；b) 只在报告中给汇总计数 | a，一致且实现最简单；体积问题可在后续单独优化 |
| D7 | 是否写入增量站点哈希 | a) 不写入：跳转是独立派生文档，开关只增删这些条目，不影响普通页面 HTML；b) 与现有两个配置一致，只在非默认值（`true`）时写入，开关会触发全站重渲染一次 | a，避免无谓的全站重渲染。前提有两条：T7 证明增量清理可靠；T18 证明普通页面 HTML 不依赖派生文档。已核实 `PagesIndexPlugin` 构建 `PagesById` 只用 RoutedDocuments，但未逐一核实主题可见的全部站点数据（如路由计数、页面列表）。任一条不成立即退回 b |
| D8 | 汇总日志 | `event=i18n.legacy_redirects.derived count= skipped= bytes=`，`skipped > 0` 时附前 10 条路径 | 按此实施 |
| D9 | 实施方式 | 单独的 Core 任务，需先写 openspec change（参照 `openspec/changes/archive/2026-11-12-add-i18n-default-language-root/`） | 是。确认决策不等于授权实施 |

## 7. 本轮发现的现有问题（不在本提案内修复）

| 编号 | 级别 | 问题 | 证据 | 建议 |
|---|---|---|---|---|
| F1 | Minor（疑似，未复现） | `I18nLegacyRedirectDeriver` 判断占用时没有检查 ListRoutes。根目录布局下若某个集合的列表路由恰好是 `/en/`，派生的旧首页跳转会与列表路由同路径，预期由 `ValidatePostDeriveRoutes` 报重复路由使构建失败，而不是"用户内容优先" | `I18nLegacyRedirectDeriver.cs:27-32` 只检查 RoutedDocuments、DerivedDocuments、StaticHtmlRoutes、static 源 | 实施本提案时一并把 ListRoutes 纳入占用检查，并加回归测试 |

## 8. 影响文件与测试

### 8.1 代码（均为 Core，需单独授权）

| 文件 | 改动 |
|---|---|
| `src/Bukit-Core/Bukit.Config/AppConfig.cs` | `SiteConfig` 新增 `DefaultLanguageLegacyRedirects`（默认 `false`） |
| `src/Bukit-Core/Bukit.Config/ConfigLoader.cs` | 读取 `site.defaultLanguageLegacyRedirects` |
| `src/Bukit-Core/Bukit.Engine/I18nLanguagePolicy.cs` | 单语言站点：原事件加入新字段；多语言带前缀布局：新增 `i18n.config.legacy_redirects_ignored` 警告 |
| `src/Bukit-Core/Bukit.Engine/I18nLegacyRedirectDeriver.cs` | 扩展为批量派生；排除首页路由；按输出路径去重；占用检查改用 HashSet 并加入 ListRoutes；汇总日志 |
| `src/Bukit-Core/Bukit.Engine/VariantBuildPipeline.cs` | 调整派生时机（4.6） |
| `src/Bukit-Core/Bukit.Engine/BuildTransaction.cs` | 仅当 4.8 符号链接处理选 a：写入跳转前对每条跳转路径逐段拒绝符号链接；`GeneratedOutputLinkPaths` 保持不变 |
| `src/Bukit-Core/Bukit.Engine/Incremental/SiteIdentityAndModesContributor.cs` | 仅当 D7 选 b 时修改 |
| `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/AliasPlugin.cs` | D5 b：alias 跳转文档在 custom fields 中记录 `redirectTarget`（页面最终地址），作为迁移派生的识别依据和目标 |
| `src/Bukit-Core/Bukit.Engine/RedirectPageDocument.cs` | 同上，可选 |
| `docs/governance/bukit-core-public-api-baseline.v1.json` | `SiteConfig` 新增公共属性（新增性变更） |

无需改动：`I18nReservedPrefixGuard.cs`、`I18nWorktreeProtocol.cs`、`RedirectPageHtml.cs`、`I18nOutputLayout.cs`（4.3、4.8 已说明原因）。

### 8.2 测试

| 编号 | 文件 | 用例 |
|---|---|---|
| T1 | `tests/Bukit.Config.Tests/ConfigLoaderFullCoverageTests.cs` | 读取新配置，默认 `false` |
| T2 | `tests/Bukit.Config.Tests/StrictYamlContractTests.cs` | 新键被严格 YAML 契约接受 |
| T3 | `tests/Bukit.Engine.Tests/I18nRootLayoutTests.cs`（警告由引擎 `I18nLanguagePolicy` 发出，配置校验器不记日志） | 多语言带前缀布局开启时记 `i18n.config.legacy_redirects_ignored` 且构建成功；单语言站点**只开新配置**（另两个保持默认）时也记 `default_language_layout_ignored`（D3） |
| T4 | `tests/Bukit.Engine.Tests/I18nRootLayoutTests.cs` | 内容页、分页、分类词条页、static HTML 各有旧地址跳转，目标正确（含项目站 baseUrl 与无 `site.url` 两种） |
| T5 | 同上 | 跳转不进入 sitemap、search、feed、llms、agent-manifest、内容投影；404 与非 HTML 不派生 |
| T6 | 同上 | 用户内容优先：页面、alias、列表路由（含 F1）、static 文件占用时跳过；汇总日志计数正确 |
| T7 | 同上 | **增量**关闭配置后跳转文件被删除，默认语言页面和其他语言不重渲染、不被删除 |
| T8 | 同上 | 增量构建中新增占用同路径的页面后，跳转被页面替换，无残留 |
| T9 | 同上 | 开启迁移跳转时切换布局（两个方向），旧布局清理正确，`en/<path>/` 在两种布局下内容正确 |
| T10 | `tests/Bukit.Cli.Tests/WorktreeBuildCoordinatorTests.cs`、`tests/Bukit.Engine.Tests/I18nWorktreeProtocolTests.cs` | Worktree 与进程内构建产生相同的跳转文件集合；父进程放行 `<defaultLang>/...` 派生路径 |
| T11 | `tests/Bukit.Engine.Tests/I18nReservedPrefixTests.cs` | 默认语言路由使用其他语言前缀时仍先报保留前缀错误，不派生跳转 |
| T12 | `tests/Bukit.Engine.Tests/IncrementalEngineIdentityTests.cs` | 按 D7 的结论：新配置开关不改变 / 改变站点哈希 |
| T13 | `tests/Bukit.Engine.Tests/I18nRootLayoutTests.cs` | 已有输出中 `en/blog` 是指向 `zh-CN/blog` 的符号链接（根目录内）时，跳转不会写进其他语言目录（构建失败或链接被替换，按 4.8 的选择） |
| T14 | 仅 D5 选 b | `/<lang>/<alias>/` 直接跳到页面最终地址，不形成跳转链；alias 已是 `/en/old/` 时不派生 `/en/en/old/`；旧首页跳转不被当作 alias 迁移 |
| T15 | `tests/Bukit.Engine.Tests/I18nRootLayoutTests.cs` | 有标签和分类时开启，分类索引页和词条页各只有一份跳转，构建成功 |
| T16 | 同上 | 占用检查对算法而不是耗时断言：占用集合每次派生只构建一次，路径规范化调用次数与路由数呈线性关系（例如把集合构建抽成可单测的内部方法，用计数断言），不使用墙钟时间上限 |
| T17 | 同上 | 开启后 routes 报告、build-report 路由清单和 `--metrics` 路由数的变化与文档一致 |
| T18 | 同上 | 增量构建中切换配置，普通页面不被重新渲染且内容不变：比较切换前后普通页面的 manifest 条目哈希和渲染计数，不做逐字节比较（站点模型含 `BuildStartedAt`，模板若输出构建时间会造成误报）（D7 a 的前提） |
| T19 | 同上 | `defaultLanguageRedirect: false` + 新配置 `true`：不写 `en/index.html`，其他旧地址有跳转（D2） |
| T20 | 同上 | 内容页 `/blog/post/` 与以扩展名结尾的 `/about.html` 都派生跳转，安全报告 slug 检查通过；同一路径两次构建的 ID 相同 |

专项测试命令在实施时用 `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包后确定；预计限定在 `Bukit.Config.Tests`、`Bukit.Engine.Tests`、`Bukit.Cli.Tests` 中带 `I18n`、`StrictYaml`、`ConfigLoader`、`IncrementalEngineIdentity`、`WorktreeBuildCoordinator` 过滤条件的用例，不运行全量 gate。

### 8.3 文档

- `guide/user/11-i18n-seo.md`：新配置、与托管层 301 的取舍、非 HTML 旧地址的处理。
- `guide/user/04-site-yaml-config.md`：配置表新增一行。
- `guide/skills/bukit-i18n/SKILL.md`：迁移步骤加入"纯静态托管可开启迁移跳转"。
- 站点迁移清单（前置提案 7.2）："默认语言旧地址失效"一条改为指向新配置。
- 中英马 CHANGELOG：新增可选功能，默认关闭，无行为变化。

## 修订记录

| 轮次 | 日期 | 内容 |
|---|---|---|
| 初稿 | 2026-11-13 | 首次提交分析与提案 |
| 确认 | 2026-11-13 | 用户按推荐确认 D1–D9；第 6 节改为已确认决策 |
| 审计 1 | 2026-11-13 | 首页路由始终排除（D2）；按输出路径去重，修正分类页被派生两次的问题；4.6 改为保持现有派生位置；改正符号链接防护说明，`GeneratedOutputLinkPaths` 保持不变；占用检查改用 HashSet；补全报告与计数消费方；D7 增加 T18 前提；D3 改用新事件名；T3 位置修正；去掉 uglyUrls 说法；新增 T15–T19 |
| 审计 2 | 2026-11-13 | 规定 ID 与 slug 用路径短哈希，满足安全报告 slug 检查；D5 b 跳过已带默认语言前缀的 alias；写明跳转文档的识别依据；改正分类路由排除的判断依据；单语言警告补提前返回条件；T16 改为算法断言，T18 改为比较 manifest 哈希与渲染计数；新增 T20 |
