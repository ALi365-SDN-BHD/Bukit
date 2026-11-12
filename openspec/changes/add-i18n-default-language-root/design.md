## Context

多语言构建目前基于一个假设：每种语言各占 `dist/<lang>/`，根目录只放聚合文件。这个假设散落在多个位置（均已在 `main` 上核实）：

- baseUrl：`I18nLanguagePolicy.CombineBaseUrlWithLanguage`，调用点为 `SiteEngine`、`I18nWorktreeBuild.CreateVariantContext`、`SeoAlternatesService`。
- 输出目录：`SiteEngine`（`Path.Combine(outputDir, lang)`）、`WorktreeBuildRun`（`Path.Combine(_stagingOutputDir, language)`）、`WorktreeBuildPaths.RewriteManifestRoots` / `CopyVariantManifest`。
- 语言代码被当作输出子目录：`I18nMergedVariantState.MergeRoute`、`SeoAuditReportWriter`（两处）。
- 根目录聚合：`PublicOutputLifecycle.CollectAndClean` 把各语言计划输出与根目录计划输出合并后，用 `AssetOutputPlan.Validate` 检查目标路径是否重复。这个检查只覆盖资源和插件输出，**不覆盖路由 HTML**。
- 报告：每种语言在自己的输出目录调用 `SeoAuditReportWriter.Write`，写出 `.bukit/seo-report.json`、`publish-audit-report.json`、`seo-route-map.json`、`geo-report.json`；`VariantReportStage` 另写 `.bukit/analytics-report.json`。根目录的 `WriteMerged` 用同一套 writer 写出同名文件。
- Worktree：每个 Worker 直接写 `staging/<lang>/`，父进程在 `I18nWorktreeProtocol.ValidateOutputPaths` 中**事后**验证结果引用的所有路径（计划输出、插件输出、路由、列表、派生、投影、static、manifest 各项）都在该 Worker 的输出目录内。

已核实、不需要改的部分：

- `sitemapMode: merged` 和 `feed.mode: merged` 时，`SitemapPlugin`、`FeedPlugin` 在各语言中已跳过写文件（但计划输出清单仍列出这些文件）。
- `search.mode: index` 的根目录 `search.index.json` 和 merged 搜索都按 `result.BaseUrl` 计算路径；根目录 robots、agent-manifest、feed、llms writer 也按 `result.BaseUrl` 计算。
- 恢复标记 `.bukit-output-marker`、`.bukit-build-state.json` 只在根目录这一层由 `BuildPlanner`/`SiteEngine` 写入，语言那一轮不写。
- 输出目录的整体清理（`OutputDirectoryCleaner`）只由根目录这一层的 `BuildPlanner.PrepareOutputDirectory` 执行；Worker 和语言那一轮不执行整体清理。
- `PublicOutputPrivacyCheck`、`BuildOutputInventory` 只在根目录这一层扫描整个输出目录。
- 模板只拿到当前语言的 `site.base_url`；内置 Starter 主题的 hreflang 来自 `page.seo.alternates`。dev 服务器和 deploy 不感知语言。GitHub Pages deploy 复制输出时跳过任意层级的 `.bukit/`。
- 现有两份跳转页生成代码：`AliasPlugin.BuildRedirectHtml` 和 `TaxonomyRedirectWriter.RenderRedirect`，都输出 meta refresh 和 canonical，都没有 `<title>` 和 `noindex`。

## Prior Art

调研了主流静态站点生成器和框架（2026-11），结论：多数工具默认把默认语言放在根目录，Bukit 现在"所有语言都带前缀"的做法是少数。

| 产品 | 默认语言默认位置 | 两种布局的跳转处理 | 与本设计相关的做法 |
|---|---|---|---|
| Hugo | 根目录（`defaultContentLanguageInSubdir: false`） | 带前缀时生成根目录跳转页到 `/<lang>/`；放根目录时生成 `/<lang>/` 跳转页到 `/`；均可用 `disableDefaultLanguageRedirect`（v0.140）/ `disableDefaultSiteRedirect`（v0.154.5）关闭 | 放根目录时，根目录 `sitemap.xml` 是索引，默认语言的 sitemap 仍写在 `/<lang>/sitemap.xml`，其中地址不带前缀（源码测试 `TestSitemapIssue12266`） |
| Astro | 根目录（`prefixDefaultLocale: false`） | 开启前缀时自动开启 `redirectToDefaultLocale`，根 `index` 页生成 meta refresh 跳转 | 根 `index` 页是必需的 |
| Docusaurus | 根目录，其他语言 `/<locale>/` | 不处理，交给托管层 | 每种语言独立构建；文档说明 GitHub Pages 上只能显示默认语言的 404 |
| VitePress / Starlight | 用 `root` 语言键表示放在根目录 | VitePress 明确不自动跳转 `/` | — |
| Zola | 根目录，其他语言 `/{code}/` | — | feed 和搜索索引按语言分别开关 |
| Jekyll Polyglot | 根目录，其他语言带前缀 | — | `exclude_from_localization` 指定只放根目录的文件（sitemap、CNAME、资源）；每种语言一个独立进程 |
| Nuxt i18n | 可选 `prefix_except_default` / `prefix` / `prefix_and_default` / `no_prefix` | — | `prefix_and_default` 让默认语言有两个地址，存在重复内容风险 |
| Eleventy | 无内置方案 | 文档给出 Netlify 跳转写法，包括"默认语言不带前缀" | — |

本设计采纳的做法：配置命名参照 Hugo；sitemap 放法参照 Hugo；两种布局各自生成对应跳转页，参照 Hugo 和 Astro；按浏览器语言跳转交给托管层，与 Docusaurus、VitePress、Eleventy 一致；不提供让默认语言同时有两个地址的模式（Nuxt `prefix_and_default` 的反例）。各产品文档均未提到路由与语言前缀冲突的校验，本设计的保留前缀检查比它们更严格。

## Goals / Non-Goals

**Goals:**

- `defaultLanguageInSubdir: false` 时，默认语言完整输出在根目录，首页和 `404.html` 可以直接由静态托管提供。
- 无论选哪种布局，GitHub Pages 等纯静态托管上的首页 `/` 都不再返回 404。
- 所有和语言布局相关的计算只有一个来源，不再在各处拼接语言代码。
- 根目录每个文件只有一个归属方，任何冲突都在**写文件前**报错，不允许悄悄覆盖。
- 默认值下，除新增的根目录跳转页 `index.html` 外，URL、页面输出、报告路径和增量哈希不变。

**Non-Goals:** 见 proposal。

## Decisions

### 1. 配置形态

`site.defaultLanguageInSubdir: bool`，默认 `true`。只在 `site.languages` 非空时生效；`languages` 为空却写了 `false` 时只记录警告，与现在对 `defaultLanguage` 的处理一致。`languages` 只有一项且为 `false` 时，该语言即站点根，同样遵守下面的归属规则。

**建议采用此名。** 理由：现有 `language`、`languages`、`defaultLanguage`、`sitemapMode` 都平铺在 `site` 下，没有 `site.i18n` 块；只为一个字段新开一个块会让配置形态不一致。备选 `site.i18n.prefixDefaultLanguage` 留待将来 i18n 配置成组时再统一迁移。

### 2. 单一布局策略 `I18nOutputLayout`

新增内部静态类，提供：

- `ResolveBaseUrl(site, rootBaseUrl, language)`
- `ResolveOutputDir(site, outputRoot, language)`
- `ResolveRelativePrefix(site, language)`：默认语言在根目录时返回空字符串，否则返回 `<lang>`
- `IsRootVariant(site, language)`
- `ReservedFirstSegments(site)`：默认语言在根目录时返回其他语言代码，否则返回空
- `ResolveReportDirectory(site, language)`：根目录语言返回 `.bukit/variants/<lang>`，否则返回 `.bukit`

Context 中列出的所有拼接点都改为调用该策略。`BuildTransaction.RejectGeneratedOutputLinks` 按布局生成路径清单，不能把空前缀当作语言目录加入。

### 3. 根目录文件归属

| 文件 | 带前缀（现状） | 默认语言在根目录 |
|---|---|---|
| `agent-manifest.json` | 根目录 + 各语言 | 只由根目录生成；默认语言不计划、不写出 |
| `robots.txt` | 根目录 + 各语言 | 只由根目录生成；默认语言 `static/robots.txt` 的处理见决策 4 |
| `llms.txt` / `llms-full.txt` | 根目录 + 各语言 | 只由根目录生成 |
| `sitemap.xml`，split | 各语言 | 默认语言写 `/sitemap.xml`，没有冲突 |
| `sitemap.xml`，merged | 只有根目录 | 不变；各语言计划输出不再列出 |
| `sitemap.xml`，index | 根目录索引 + 各语言 | 根目录 `/sitemap.xml` 仍是索引；默认语言 sitemap 写在 `/<lang>/sitemap.xml`（与 Hugo 相同），其中地址不带前缀；索引和 robots 都引用这个地址 |
| feed，split | 各语言 | 默认语言写在根目录，没有冲突 |
| feed，merged | 只有根目录 | 不变；各语言计划输出不再列出 |
| `search.json`，split / index | 各语言（index 另有根目录 `search.index.json`） | 默认语言写 `/search.json`，没有冲突 |
| `search.json`，merged | 根目录 + 各语言 | 本版本校验失败 |
| `.bukit/` 下 5 种报告 | 根目录 + `<lang>/.bukit/` | 默认语言改写到 `.bukit/variants/<lang>/` |

做法：`PublicOutputLifecycle.ProjectionPlan` 新增参数，表示"当前语言是根目录语言"。为 true 时排除根目录独占的文件，并按 mode 处理 sitemap 文件名；同时让所有语言的计划输出按 sitemap/feed mode 生成，与插件的实际写出行为一致。`CollectAndClean` 保留现有重复检查作为最后防线。

**index 模式下默认语言 sitemap 放在 `/<lang>/sitemap.xml`。** 理由：与 Hugo 的做法一致；每种语言的 sitemap 都在 `/<lang>/sitemap.xml`，结构对称，根目录索引和 robots 的生成逻辑不需要为默认语言单独处理，Search Console 中各语言 sitemap 的地址也与带前缀布局下相同，切换布局时不用重新提交。该文件由默认语言那一轮写出，相对默认语言输出目录的路径为 `<lang>/sitemap.xml`；默认语言自身代码不保留（决策 5），所以不会触发保留前缀检查。曾考虑 `sitemap-<lang>.xml`，因需要特殊命名且与其他语言不对称而放弃。

split 模式下没有根目录索引，默认语言的 sitemap 仍写在 `/sitemap.xml`。

**`search.mode: merged` 建议本版本报错。** 根目录合并索引和默认语言的单语言索引必须共用 `/search.json`。如果改名，内置搜索页 `bukit-search.html` 和主题读取地址都要跟着改，超出本变更范围；先失败关闭，后续单独设计。

### 4. 默认语言 `static/` 与根目录独占文件

默认语言放到根目录后，`static/` 里的文件会直接落到站点根目录，可能和根目录独占文件同名。规则：

- `static/robots.txt`：沿用现有"static 覆盖优先"的约定，作为整站 robots，根目录不再生成 robots。构建记录一条 info 日志，并在文档中说明此时 robots 不会自动列出各语言 sitemap。
- `static/` 中名为 `agent-manifest.json`、`llms.txt`、`llms-full.txt`、根目录 sitemap 文件名（merged/index 模式下的 `sitemap.xml`）、merged feed 文件名的文件：构建失败，报 `i18n-root-output-conflict`，指出 static 源文件和对应设置。
- 其他文件（`404.html`、`favicon.ico`、`CNAME` 等）正常落到根目录。这正是本变更想要的效果。

带前缀布局下，这些 static 文件仍写到 `<lang>/`，行为不变。

### 5. 路由保留前缀校验

默认语言在根目录时，以下所有输出路径的第一个路径段不得等于其他已配置语言代码：内容路由、列表和分页路由、分类路由、alias、派生文档、`static/` 文件、插件输出。报错码 `i18n-route-reserved-language-prefix`，报错信息包含来源文档 id 或文件路径。

- **比较一律不区分大小写，与操作系统无关。** 否则在 macOS 开发机上会冲突（大小写不敏感的文件系统），在 Linux CI 上却通过，两边结果不一致。
- **检查时机必须在写文件之前。** 路由类检查放在 `RoutePipeline` 完成路由后、渲染前；static 和插件输出检查放在资源计划确定后、复制前（插件输出在 AfterBuild 才确定，此时检查只能报错并阻止提交，因为输出在 staging 中，不会进入正式目录）。
- 父进程 `ValidateOutputPaths` 已经汇总了一个 Worker 的全部输出路径，在这里再加一道相同的检查，作为 Worktree 模式下的事后防线。
- 默认语言自身的代码**不**保留。默认语言可以有 `/en/...` 输出：决策 8 的 `/en/` 跳转页、index 模式下的 `/en/sitemap.xml`，以及用户用 alias 为其他旧地址手动生成的跳转页（见风险 R1）。

### 6. 布局切换和增量构建

`SiteIdentityAndModesContributor` 只在值为 `false` 时写入哈希，这样未启用的站点升级后不会全量重渲染。根 manifest 记录本次布局，和上次不同时，`BuildPlanner.PrepareOutputDirectory` 按现有 migration 路径执行受保护的清理。默认语言 manifest（`build-manifest.<lang>.json`）的 `OutputRoot` 改为输出根目录，`WorktreeBuildPaths.RewriteManifestRoots` 和 `CopyVariantManifest` 通过 `I18nOutputLayout` 计算路径。

默认语言 manifest 和根 manifest 的 `OutputRoot` 会相同。两者都只按各自记录的归属删除旧文件，`BuildManifestTracker.DeleteEmptyDirectoriesUpToRoot` 和 `PublicOutputLifecycle.DeleteOwnedFile` 都在输出根目录处停止，不会越界；但需要专项测试覆盖"删除默认语言最后一个文件后，空目录清理不影响其他语言目录"。

### 7. Worktree 写入区域

现状下各 Worker 的写入区域互不重叠（`staging/<lang>/`），一个 Worker 出错只会破坏自己的子树。默认语言放到 staging 根目录后，它的写入区域**包含**其他 Worker 的区域，这个隔离性质不再天然成立。保障方式：

- 默认语言 Worker 在渲染前执行决策 5 的保留前缀检查（Worker 和进程内共用同一实现）。
- 父进程在 `ValidateOutputPaths` 中加同样的检查，任何 Worker 结果引用了另一语言目录下的路径都会被拒绝，staging 不会被提交。
- Worker 和语言那一轮不得执行整体清理或裁剪式同步。`DirectoryCopy.Sync(prune: true)` 目前没有调用方；加一条测试或断言，禁止在语言输出目录上以裁剪模式调用它，防止以后有人引入后误删其他语言目录。

### 8. 默认语言跳转页（两种布局都生成）

参照 Hugo 和 Astro，新增 `site.defaultLanguageRedirect: bool`，默认 `true`，只在 `site.languages` 非空时生效。

- **带前缀布局（`defaultLanguageInSubdir: true`）**：根目录生成 `index.html`，跳转到 `<baseUrl>/<defaultLang>/`。这就是此前单独讨论的"方案 A"，现在并入本设计。它是根目录独占文件，由新增的根目录 writer 在聚合阶段写出，加入根目录计划输出。
- **根目录布局（`defaultLanguageInSubdir: false`）**：生成 `<lang>/index.html`，跳转到站点根 `<baseUrl>/`，让从带前缀布局迁移来的旧首页 `/en/` 不会 404。它由默认语言那一轮作为派生跳转文档写出，与 alias 生成的跳转页走同一条路径。
- **跳转页内容**：meta refresh 和 canonical 指向目标地址；带 `<title>` 和 `<meta name="robots" content="noindex">`；配置了 `site.url` 时使用绝对地址，否则使用 baseUrl 相对地址。不进入 sitemap、搜索索引、`llms.txt`、`agent-manifest.json` 和 feed。实施时把 `AliasPlugin.BuildRedirectHtml` 和 `TaxonomyRedirectWriter.RenderRedirect` 合并为一份共用实现，并补上 `<title>` 和 `noindex`（alias 和分类跳转页随之获得这两个标签，属于有意的小改动）。
- **用户内容优先**：根目录布局下，如果默认语言已有输出落在 `<lang>/index.html`（页面、alias 或 static 文件），不生成跳转页，记录一条 info 日志。带前缀布局下根目录没有任何语言输出，不存在这种冲突。
- **关闭**：`defaultLanguageRedirect: false` 时两种跳转页都不生成，适合已在 CDN 或托管层配置了 301 的站点。
- **不包括 404**：带前缀布局下不复制默认语言 `404.html` 到根目录（Hugo 也不做）。需要根目录 404 的站点应选择根目录布局。

`defaultLanguageRedirect` 只在值为 `false` 时写入增量站点哈希。根目录跳转页不属于任何语言的页面渲染，生成它不会触发页面重新渲染。

## Impact Inventory

### 构建结果与公开输出

- 默认语言的 URL、canonical、hreflang、`x-default`、sitemap 条目、feed 链接全部去掉语言段。`x-default` 从此指向一个真实页面，而不是 `/en/`。
- `feed.mode: merged` 时，默认语言模板中 `{{ site.base_url }}/rss.xml` 会指向根目录的多语言合并 feed；现状下各语言的这个地址都是 404。这是行为变化，需要在文档中说明。
- 内置搜索页、`/search/` 路由在默认语言下变为根路径，其他语言不变。
- 仍然只有根目录的 `404.html` 会被 GitHub Pages 使用，其他语言的 404 页仍然用不上（与现状一致）。

### 报告与 CLI

- 默认语言报告移到 `.bukit/variants/<lang>/`。原来通过 `bukit seo audit --dir dist/en` 审计单一语言的脚本，需要改为 `--report dist/.bukit/variants/en/seo-report.json`。`--dir dist` 读取的合并报告不变。
- `seo-route-map.json`、`geo-report.json`、`publish-audit-report.json` 的根目录版本仍是合并版本，`seo insights` 等命令的默认 `--routes` 路径不变。
- `--metrics` 输出里默认语言的 `variants[].outputDir` 会等于输出根目录。外部工具如果假设各语言目录互不重叠（例如按目录统计大小），会重复计算。
- `bukit seo diff` 在切换布局的那次构建上会看到所有默认语言路由被删除又新增，设置了路由删除上限的 CI 会失败。需要在迁移文档中说明：切换后重新建立基线。

### 内容创作与配置演进

- 默认语言作者不能再使用与其他语言代码相同的 slug、标签、分类或 permalink 首段。例如 `languages: [en, zh-CN, ms]` 时，英文内容中名为 `ms` 的标签会让构建失败。
- **新增一种语言是破坏性操作**：例如在已上线的根目录布局站点中加入 `ms`，原来正常的默认语言 `/ms/...` 内容会立即触发报错。报错信息需要说明是因为新增了语言 `ms`。
- `defaultLanguage` 换成另一种语言，等同于整站 URL 重排，必须走受保护清理，并作为迁移处理。
- `data/<lang>/`、`locale` 过滤、`i18nKey` 分组、Notion 语言属性均不受影响。

### 插件与主题

- 第三方插件可能把 `context.OutputDir` 当作站点根目录。在带前缀布局下这个假设本来就不成立；在根目录布局下，它对默认语言碰巧成立，对其他语言仍不成立，行为会因语言不同而不一致。插件写出与根目录独占文件同名的输出时会触发重复路径报错。
- Service worker（`sw.js`）、`manifest.webmanifest` 这类按路径限定作用范围的文件，由默认语言写到根目录后，作用范围会覆盖所有语言子目录。这是行为变化，需要写进插件作者文档。
- 主题如果手写了 `/{{ site.language }}/` 或 `href="/"` 当作首页链接，在根目录布局下默认语言会出错。内置主题已核实没有这类写法；第三方主题需要自查，写进主题作者文档。

### 托管、SEO 与运维

- **默认值下的升级变化**：现有多语言站点升级后，`dist/` 根目录会多出跳转页 `index.html`。
  - GitHub Pages 等纯静态托管：首页从 404 变为跳转到默认语言，这正是想要的修复。
  - Netlify 等托管：如果站点已配置 `/` → `/en/` 的 301 规则且未加 `force`，Netlify 的"文件优先"规则会让新生成的 `index.html` 盖过这条 301，首页跳转从 301 退化为 meta refresh。这类站点应设置 `defaultLanguageRedirect: false`，CHANGELOG 和升级说明中必须写明。
  - 构建后用脚本自行往根目录写 `index.html` 的站点（方案 C 的做法），脚本会覆盖生成的跳转页，功能上不冲突，但建议删除脚本。
- GitHub Pages（根目录布局）：首页和 404 问题直接解决，不需要额外配置。
- Netlify / Cloudflare / Vercel / CDN：已经配置了 `/` → `/en/` 跳转的站点，切换后必须删掉这条规则，并改成 `/en/*` → `/:splat` 的 301；否则访问 `/` 会被跳到已不存在的 `/en/`。切换后需要清理 CDN 缓存。
- SEO：已上线站点切换会改变全部默认语言 URL。GitHub Pages 不能做 301，只能用 meta refresh，排名迁移效果比 301 差。建议新站点或支持 301 的托管平台才启用；Search Console 中 index 模式的各语言 sitemap 地址不变，但 split 模式下默认语言 sitemap 从 `/en/sitemap.xml` 移到 `/sitemap.xml`，需要重新提交。
- Analytics：按 `/en/` 路径前缀筛选的报表和看板会失效。
- CI 和部署脚本：写死 `dist/en/...` 的冒烟测试、链接检查，以及为了绕过根目录 404 而把 `dist/en/*` 复制到根目录的临时脚本（方案 C 的做法），在启用后都要删除或修改，否则会和新布局冲突。

### 隐私与安全

- 默认语言报告移到根目录 `.bukit/` 下之后，会被 `PublicOutputPrivacyCheck` 跳过（它只跳过根目录 `.bukit/`）。现状下 `<lang>/.bukit/` 会被扫描，两种布局下的扫描范围因此不同。实施时确认这是预期行为，并补一条测试固定下来。
- `examples/github-pages-workflow.yml` 直接上传整个输出目录，包括 `.bukit/` 报告。这是现有问题，不是本变更引入的，记录为后续事项。

## Risks / Trade-offs

- **R1 旧地址失效**：已上线站点切换后，`/<lang>/...` 会失效。缓解：默认不启用；旧首页 `/<lang>/` 由决策 8 的跳转页兜底；文档给出托管层 301 示例；用户可以用 alias 为其他旧地址生成跳转页；为全部旧地址自动生成迁移跳转留给后续变更。
- **R7 默认值新增根目录文件**：带前缀站点升级后多出根目录 `index.html`，可能盖过托管层已有的 301 规则（见 Impact Inventory）。缓解：可用 `defaultLanguageRedirect: false` 关闭；CHANGELOG 和升级说明标为行为变化。
- **R2 写入区域重叠**：Worktree 下默认语言 Worker 包含其他 Worker 的区域。缓解：决策 5、决策 7 中的写前检查加父进程事后检查。
- **R3 根目录聚合单一化**：根目录 `llms.txt`、`agent-manifest.json` 只保留合并版本，默认语言单独的版本不再存在。这和其他语言在根目录的处境一致，可以接受。
- **R4 计划输出清单变化**：按 mode 生成计划输出后，带前缀布局的 manifest 也会少几项（插件本来就不写的文件）。需要确认不会误删文件，也不会触发全量重建。
- **R5 大小写和文件系统差异**：统一不区分大小写比较，消除不同平台结果不一致。
- **R6 新增语言变成破坏性操作**：报错信息需要指明是哪个新增的语言代码导致冲突，文档单独说明。

## Migration Plan

1. 引入 `I18nOutputLayout`，现有拼接点全部改为调用它；默认值下全部现有测试保持通过，作为行为不变的对照。
2. 加入配置项、根目录归属规则、static 冲突规则、报告目录、保留前缀校验、布局切换清理，以及两种布局的跳转页（含统一跳转页生成代码）。
3. 进程内构建和 Worktree 构建分别加入根目录布局的专项测试，比较两者输出是否一致。
4. 文档（含站点迁移清单、插件和主题作者说明）、Schema、公共 API 基线、CHANGELOG。
5. commit、push、发布和跨平台 CI 另行授权。

## Open Questions

全部已确定，用户于 2026-11-12 确认：

- 跳转页默认开启：`site.defaultLanguageRedirect` 默认 `true`（决策 8）。接受带前缀站点升级后新增根目录 `index.html` 的行为变化（R7），由 CHANGELOG 和升级说明告知，需要时可设为 `false` 关闭。
- 布局配置项名称：`site.defaultLanguageInSubdir`（决策 1，与 Hugo 命名一致）。
- index 模式默认语言 sitemap 位置：`/<lang>/sitemap.xml`（决策 3，与 Hugo 一致）。
- `search.mode: merged`：本版本不支持，与根目录布局组合时配置校验失败（决策 3）。
- 跳转页配置项名称：`site.defaultLanguageRedirect`（决策 8）。

没有未决问题。
