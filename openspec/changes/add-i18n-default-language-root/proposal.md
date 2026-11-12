# Serve the default language at the site root

## Why

配置了 `site.languages` 后，Bukit 把每种语言（包括默认语言）都输出到 `dist/<lang>/`，根目录只放聚合文件，没有 `index.html` 和 `404.html`。GitHub Pages 等纯静态托管平台不支持服务端重定向，结果是站点首页 `/` 返回 404，自定义 404 页也不生效；即使配置了 `defaultLanguage` 也一样。仓库自带的 `examples/github-pages-workflow.yml` 部署出的多语言站点打开即是坏的。

另一种常见且对 SEO 友好的布局是：默认语言不带前缀，直接放在根目录（`/about/`），其他语言保留前缀（`/zh-CN/about/`）。Hugo、Astro、Docusaurus、Zola、Jekyll Polyglot 等主流工具默认都采用这种布局；Hugo 和 Astro 同时支持两种布局，并分别生成对应的跳转页（调研见 design.md Prior Art）。本变更参照它们：新增根目录布局作为可选项，并在两种布局下都生成默认语言跳转页，使任何布局下的首页都不再 404。

## What Changes

- 新增 `site.defaultLanguageInSubdir`（布尔，可选，默认 `true`）。`true` 时行为与现在完全一致；`false` 时默认语言的 baseUrl 等于站点根 baseUrl，输出写入 `dist/` 根目录，其他语言不变。
- 新增内部策略 `I18nOutputLayout`，统一提供每种语言的 baseUrl、输出目录、相对输出前缀、报告目录和保留前缀。现有直接拼接语言代码的位置全部改为调用该策略。
- 根目录归属规则：`agent-manifest.json`、`robots.txt`、`llms.txt`、`llms-full.txt` 只由根目录聚合生成；`sitemapMode: index` 时默认语言 sitemap 写在 `/<lang>/sitemap.xml`（与 Hugo 相同），其中地址不带前缀；所有语言的计划输出清单按当前 sitemap/feed mode 生成。
- 新增 `site.defaultLanguageRedirect`（布尔，可选，默认 `true`）：带前缀布局下在根目录生成 `index.html` 跳转到 `/<defaultLang>/`（原"方案 A"并入本变更）；根目录布局下生成 `/<defaultLang>/` 跳转到 `/`。跳转页带 `noindex`，不进入 sitemap、搜索、llms、agent-manifest 和 feed；用户已有的同路径输出优先。现有两份跳转页生成代码合并为一份，alias 和分类跳转页随之补上 `<title>` 和 `noindex`。
- 默认语言 `static/` 的处理：`static/robots.txt` 作为整站 robots；与其他根目录独占文件同名的 static 文件在复制前报 `i18n-root-output-conflict`。
- `site.search.mode: merged` 与 `defaultLanguageInSubdir: false` 组合在本版本中校验失败。
- 默认语言自己的 5 种构建报告（seo、publish-audit、seo-route-map、geo、analytics）改为写到 `.bukit/variants/<lang>/`，避免被根目录合并报告悄悄覆盖；读取语言报告位置的 `ArtifactManifestInventory`（`BuildReportInventories.cs`）同步改为按布局查找，否则默认语言的 analytics 报告会从 build-report 产物清单中消失。
- 新增路由保留前缀校验：默认语言所有输出的第一个路径段不得与其他已配置语言代码相同，在任何平台上都不区分大小写，报 `i18n-route-reserved-language-prefix`。路由、static 和资源计划在写文件前检查；插件输出要到 AfterBuild 才确定，只能在提交前检查并阻止提交，此时父进程把默认语言的越界输出作为根因优先报告。默认语言自身的代码不保留，用户可以用 alias 为旧地址做跳转。
- Worktree 构建：默认语言的 Worker 输出到 staging 根目录；该 Worker 写前检查保留前缀，父进程拒绝越界结果。
- 切换该配置会改变输出布局：设置值进入增量构建的站点哈希，且与上次构建不同时对输出目录执行受保护的清理。
- 配置读取、严格字段白名单、`SiteConfigValidator`、JSON Schema、公共 API 基线、i18n skill、用户指南、开发文档、站点迁移清单、插件和主题作者说明、中英马 CHANGELOG 同步更新。

## Impact

- 受影响代码：`Bukit.Config`（`SiteConfig`、`ConfigLoader`、`ConfigStrictFieldValidator`、`SiteConfigValidator`、`ConfigJsonSchemaGenerator`），`Bukit.Engine`（`I18nLanguagePolicy`、`SiteEngine`、`I18nWorktreeBuild`、`I18nWorktreeProtocol`、`SeoAlternatesService`、`I18nMergedVariantState`、`PublicOutputLifecycle`、`AssetOutputPlan`、`RoutePipeline`/`RouteInventoryValidator`、`BuildReportPipeline`、`VariantReportStage`、`SitemapPlugin`、`I18nRootSitemapWriter`、`I18nRootRobotsWriter`、`I18nRootProjectionCoordinator`/`I18nOutputMerger`（新增根目录跳转页 writer，在聚合表示注册表之外单独调用）、`AliasPlugin`、`TaxonomyRedirectWriter`、`BuildTransaction`、`Incremental/SiteIdentityAndModesContributor`），`Bukit.Engine.Audit`（`SeoAuditReportWriter` 及其报告 writer），`Bukit.Engine.Analytics`（`AnalyticsReportWriter`），`Bukit.Engine`（`BuildReportInventories.cs` 中的 `ArtifactManifestInventory`：按布局查找语言 analytics 报告；`AssetsReportInventory`、`ReleaseBundleInventory`：确认语言报告计入范围），`Bukit.Shared`（`DiagnosticCode`：新增两个报错码成员），`Bukit.Cli`（`WorktreeBuildRun`、`WorktreeBuildPaths`）。
- 规范：新增能力 `i18n-output-layout`；修改 `i18n-worktree-build` 的"Worker 结果必须完整且可验证"和"根级输出必须保持现有语义"两条要求。
- 公共契约：`SiteConfig` 新增两个属性 `DefaultLanguageInSubdir`、`DefaultLanguageRedirect`（新增性变更），公共 API 基线随之更新；`site.yaml` 新增两个可选字段；新增两个报错码 `i18n-route-reserved-language-prefix`、`i18n-root-output-conflict`，即公共枚举 `DiagnosticCode` 新增两个成员，公共 API 基线一并更新（`search.mode: merged` 组合走配置校验报错）。
- 兼容性：不配置时，URL、已有输出路径、报告路径和增量站点哈希都不变，但有以下变化：
  - **行为变化**：现有多语言站点升级后，根目录会新增跳转页 `index.html`。纯静态托管上首页从 404 变为跳转，这是想要的修复。但在 Netlify 等平台，如果已配置未加 `force` 的 `/` → `/<lang>/` 301 规则，新文件会盖过这条规则，跳转从 301 退化为 meta refresh。这类站点应设置 `defaultLanguageRedirect: false`，CHANGELOG 中标为行为变化。
  - **行为变化**：`sitemapMode: merged` 和 `feed.mode: merged` 下，各语言不再生成自己的 sitemap 和 feed 副本（main 上由语言级投影写出，与文档约定不符），带前缀布局同样受影响，CHANGELOG 中标为行为变化；需要测试确认不会误删其他文件或触发全量重建。
  - alias 和分类跳转页的 HTML 新增 `<title>` 和 `noindex` 标签；派生跳转页的内容哈希包含 HTML，增量构建会自动重新生成。
- 启用后的站点侧影响（由站点自行承担，详见 design.md Impact Inventory）：旧地址失效需在托管层配 301；CDN 已有的 `/` → `/<lang>/` 跳转规则要删除；`seo diff` 基线要重建；按路径前缀的 Analytics 报表、写死 `dist/<lang>` 的 CI 脚本要修改；以后新增语言可能让已有内容报错。
- 跨仓影响：SRBiz-bukit 等网站仓库无需改动；是否启用由各站点自行决定，本变更不修改任何站点仓库。
- 测试：约 24 个 i18n 相关测试文件需逐一复核，其中断言 `dist/<lang>` 路径的用例保持不变（默认值行为不变），新布局另加用例。

## Approval Scope

- 本提案只覆盖 Core 实现、测试和文档。commit、push、PR、发布、网站仓库改动和 full/release gate 均需另行授权。
- design.md 中全部决策已由用户于 2026-11-12 确认。
- 实施属于 Core 任务，须作为单独的 Core 任务开展；确认决策不等于授权开始实施。

## Non-Goals

- 除旧首页 `/<defaultLang>/` 外，不为其他旧地址 `/<defaultLang>/*` 自动生成迁移跳转；已上线站点切换布局时需在托管层配置 301，或自行使用 alias。全量迁移跳转可作为后续变更。
- 带前缀布局下不把默认语言的 `404.html` 复制到根目录（Hugo 也不做）；需要根目录 404 的站点应选择根目录布局。
- 不让资源目录在各语言间共用（Jekyll Polyglot `exclude_from_localization` 的做法），可作为后续优化。
- 不支持按浏览器语言自动跳转。
- 不改变带前缀布局下任何对外行为，不支持把非默认语言放在根目录。
- 本版本不支持 `search.mode: merged` 与根目录默认语言组合使用。
- 不处理 GitHub Pages 示例工作流上传 `.bukit/` 报告这一现有问题（记录为后续事项）。
