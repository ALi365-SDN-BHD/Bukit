# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。实施须在用户确认本提案后，作为单独的 Core 任务开展。

## 0. 决策确认

- [x] 跳转页默认开启（`defaultLanguageRedirect` 默认 `true`），用户已于 2026-11-12 确认。
- [x] 布局配置项名称 `site.defaultLanguageInSubdir`、index 模式默认语言 sitemap 位置 `/<lang>/sitemap.xml`、本版本不支持 `search.mode: merged`、跳转页配置项名称 `site.defaultLanguageRedirect`，用户已于 2026-11-12 确认。

## 1. 布局策略（默认值下行为不变）

- [ ] 新增 `I18nOutputLayout`（baseUrl、输出目录、相对前缀、是否根目录语言、保留前缀、报告目录）。
- [ ] `CombineBaseUrlWithLanguage` 的 3 个调用点（`SiteEngine`、`I18nWorktreeBuild`、`SeoAlternatesService`）改用该策略。
- [ ] 输出目录：`SiteEngine`、`WorktreeBuildRun`；manifest 路径：`WorktreeBuildPaths.RewriteManifestRoots`、`CopyVariantManifest`。
- [ ] 去掉把语言代码当作输出子目录的写法：`I18nMergedVariantState.MergeRoute`、`SeoAuditReportWriter` 两处。
- [ ] `BuildTransaction.RejectGeneratedOutputLinks` 的路径清单按布局生成，不把空前缀当作语言目录。
- [ ] 现有 i18n 测试在默认值下全部通过，作为行为不变的对照。

## 2. 配置

- [ ] `SiteConfig.DefaultLanguageInSubdir`；YAML 读取、严格字段白名单、JSON Schema。
- [ ] 校验：`languages` 为空却设为 `false` 时警告；与 `search.mode: merged` 组合时报错。

## 3. 根目录归属、static 和报告

- [ ] `PublicOutputLifecycle.ProjectionPlan` 支持"根目录语言"：排除 agent-manifest、robots、llms、llms-full；index 模式下默认语言 sitemap 计划为 `<lang>/sitemap.xml`；所有语言的计划输出按 sitemap/feed mode 生成。
- [ ] 确认计划输出清单变化不会误删文件，也不会触发带前缀站点全量重建。
- [ ] `SitemapPlugin` 在 index 模式下为根目录默认语言写入 `<lang>/sitemap.xml`（地址不带前缀）；`I18nRootSitemapWriter` 的索引和 `I18nRootRobotsWriter` 引用实际地址。
- [ ] 默认语言 `static/robots.txt` 作为整站 robots（`WriteRootOutputs` 按默认语言 static 计划判断，覆盖"生成 ↔ static"两个方向的过渡构建）；其他根目录独占文件同名的 static 文件在复制前报 `i18n-root-output-conflict`。
- [ ] 默认语言 5 种报告写入 `.bukit/variants/<lang>/`（`SeoAuditReportWriter.Write`、`PublishAuditReportWriter`、`SeoRouteMapWriter`、geo 报告、`AnalyticsReportWriter`）。
- [ ] `ArtifactManifestInventory.AddVariantAnalyticsReports`（`BuildReportInventories.cs`）按 `I18nOutputLayout` 的报告目录查找语言报告。
- [ ] 根目录语言下 agent-manifest、robots、llms、llms-full 的语言级投影不写文件，投影输出沿用现有字段（`Exists = false`）；语言级审计按布局解析读取路径，不读取根目录聚合文件（含上一次构建的 sitemap 索引、合并 sitemap 和 feed），结果与上一次构建无关；默认语言自带的 static `robots.txt` 照常读取。
- [ ] 确认 `PublicOutputPrivacyCheck`、`AssetsReportInventory.Create`（资源清单）和 `ReleaseBundleInventory.Create`（release bundle 校验和清单）在两种布局下对语言报告的扫描和计入范围，并用测试固定。

## 4. 路由保留前缀

- [ ] 共用实现：内容、列表/分页、分类、alias、派生文档、static、插件输出；任何平台都不区分大小写；默认语言自身代码不保留。
- [ ] 检查时机：路由在 `RoutePipeline` 之后、渲染之前；static 和资源计划在复制之前；插件输出在 AfterBuild 之后、提交之前。
- [ ] 默认语言插件输出越界时，汇总失败结果的一方（进程内构建本身，或 Worktree 父进程）把它作为根因优先报告，即使其他语言同时失败。
- [ ] 报错信息包含来源（文档 id 或文件路径）和被占用的语言代码。
- [ ] Worktree：父进程 `I18nWorktreeProtocol.ValidateOutputPaths` 加同一检查。
- [ ] 加测试或断言：禁止在语言输出目录上以裁剪模式调用 `DirectoryCopy.Sync`。

## 4a. 默认语言跳转页

- [ ] `SiteConfig.DefaultLanguageRedirect`；YAML 读取、严格字段白名单、JSON Schema；只在值为 `false` 时写入增量站点哈希。
- [ ] 合并 `AliasPlugin.BuildRedirectHtml` 与 `TaxonomyRedirectWriter.RenderRedirect` 为共用实现，补 `<title>` 和 `noindex`。
- [ ] 带前缀布局：新增根目录跳转页 writer，在根目录聚合 writer 计划执行后单独调用（不注册为聚合表示，不产生投影结果），加入根目录计划输出和 `RejectGeneratedOutputLinks` 的根目录清单；build-report 投影结果和 publish-audit 不变。
- [ ] 根目录布局：默认语言生成 `<lang>/index.html` 派生跳转文档；同路径已有用户输出时跳过并记录 info 日志。
- [ ] 跳转页不进入 sitemap、搜索、llms、llms-full、agent-manifest、feed，也不进入 SEO 路由审计。

## 5. 布局切换和增量构建

- [ ] `SiteIdentityAndModesContributor` 只在值为 `false` 时写入哈希。
- [ ] 根 manifest 记录布局；布局变化时扩展 `BuildPlanner` 的 `needsMigration`，对事务 staging 走受保护清理；Worktree 在派发 Worker 前让 staging 从空目录开始、要求输出标记、丢弃旧的各语言 manifest；构建失败时正式输出不变。

## 6. 测试

- [ ] Config：读取、默认值、警告、`search: merged` 组合报错、Schema。
- [ ] Engine（进程内）：根目录输出和 `404.html`；baseUrl、canonical、hreflang、x-default；`/repo` 项目站点 baseUrl；split/merged/index 三种 sitemap 模式；feed 两种模式；search split/index；根目录文件唯一归属；static robots 覆盖和 static 冲突报错；5 种报告路径及 build-report 产物清单登记默认语言 analytics 报告；默认语言插件越界作为根因报告；保留前缀（分类、permalink 大小写、static、插件输出、新增语言）；默认语言 alias `/en/...` 允许；布局切换清理；增量删除不影响其他语言目录；默认值哈希不变；带前缀布局根目录跳转页（绝对/相对地址、noindex、不进 sitemap 和搜索）；根目录布局 `/en/` 跳转页及用户 alias 优先；`defaultLanguageRedirect: false` 两种布局都不生成；alias 和分类跳转页新增标签后的回归。
- [ ] CLI（Worktree）：默认语言 Worker 输出到 staging 根目录；越界结果被拒绝；manifest 根路径改写；与进程内构建输出的文件路径一致。

## 7. 文档与契约

- [ ] i18n skill、用户指南 `11-i18n-seo.md`、`04-site-yaml-config.md`、开发文档 `config-site-yaml.md`、参数速查。
- [ ] 站点迁移清单：托管层 301 示例（Netlify/Cloudflare）、删除旧的 `/` 跳转规则、清理 CDN 缓存、重新提交 sitemap、重建 `seo diff` 基线、修改 Analytics 路径筛选和写死 `dist/<lang>` 的脚本、新增语言前的检查。
- [ ] 插件和主题作者说明：`context.OutputDir` 不等于站点根目录；service worker 等作用范围变化；不要手写语言前缀或 `href="/"`。
- [ ] 升级说明：带前缀站点升级后新增根目录 `index.html`；已在 Netlify 等托管层配置 `/` 301 的站点应设置 `defaultLanguageRedirect: false`。
- [ ] 中英马 CHANGELOG（标为行为变化）、公共 API 基线、verification closure 策略。

## 8. 验证

- [ ] Config、Engine、CLI 专项测试；API 漂移、格式检查；`openspec validate add-i18n-default-language-root --strict`；当前平台 AOT 二进制编译。
