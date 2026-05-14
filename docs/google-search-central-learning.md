# Google Search Central SEO 学习笔记

本文档整理 Google Search Central 文档对 Bukit SEO 引擎的直接要求与实现映射。它不是 Google 文档的镜像，而是面向 Bukit 的工程学习笔记：把 Google 的抓取、索引、canonical、hreflang、结构化数据、Search Console 和 Analytics 规则转成可测试的引擎约束。

参考入口：

- [Google Search Central documentation](https://developers.google.com/search/docs)
- [Google Search technical requirements](https://developers.google.com/search/docs/essentials/technical)
- [SEO Starter Guide](https://developers.google.com/search/docs/fundamentals/seo-starter-guide)
- [Crawling and indexing overview](https://developers.google.com/search/docs/crawling-indexing)
- [Canonical URL guidance](https://developers.google.com/search/docs/crawling-indexing/consolidate-duplicate-urls)
- [robots meta tag and X-Robots-Tag](https://developers.google.com/search/docs/crawling-indexing/robots-meta-tag)
- [Sitemaps overview](https://developers.google.com/search/docs/crawling-indexing/sitemaps/overview)
- [Localized versions and hreflang](https://developers.google.com/search/docs/specialty/international/localized-versions)
- [Structured data introduction](https://developers.google.com/search/docs/appearance/structured-data/intro-structured-data)
- [General structured data guidelines](https://developers.google.com/search/docs/appearance/structured-data/sd-policies)
- [Using Search Console and Google Analytics data for SEO](https://developers.google.com/search/docs/monitor-debug/google-analytics-search-console)

## 1. Google 索引资格的最低门槛

Google Search 技术要求把索引资格压缩为三条底线：

- Googlebot 没有被阻止。
- 页面能正常工作，Google 收到 HTTP 200 成功状态。
- 页面包含可索引内容，并且不违反内容和垃圾内容政策。

Bukit 作为静态站生成器无法在构建时证明线上 HTTP 状态，但可以强制保证静态产物层面的事实：

- `SeoIndex.indexable` 是 sitemap、RSS、search index、audit report 的同一事实源。
- `robots: noindex` 和 `robots: none` 必须从 sitemap/search/RSS 中排除。
- `robots.txt` 只能控制抓取，不可被当成 noindex；如果某 URL 被 robots.txt 阻止，Google 可能看不到页面内 noindex。
- build/audit 应报告 `site.url` 缺失，因为绝对 canonical、sitemap loc、hreflang、schema url 都依赖它。

## 2. Crawling、Indexing 与 robots 规则

Google 将抓取控制和索引控制分开：

- `robots.txt` 控制 crawler 是否可以请求 URL。
- `<meta name="robots" content="noindex">` 或 X-Robots-Tag 控制索引和搜索结果呈现。
- 如果页面被 robots.txt 拦截，页面内 robots meta 可能不会被发现，因此不要用 robots.txt 来实现 noindex。

Bukit 映射：

- HTML head 中的 robots meta 只是输出层；索引策略必须来自 `SeoIndex.indexable`。
- `robots.txt` 启用时需要写入 `Sitemap:`，但不能覆盖用户已有 static robots 文件。
- SEO audit 要检查 robots.txt 与 noindex 策略冲突，例如 noindex 页面同时被 robots.txt 阻止。

## 3. Canonical 规则

Google 支持 HTML `<link rel="canonical">` 和 HTTP Link header。对 Bukit 的静态 HTML 产物，核心规则是：

- canonical 应出现在 HTML `<head>`。
- 推荐使用绝对 URL，不推荐相对 URL。
- canonical 不应包含 URL fragment。
- canonical 不应用于 hreflang、media、type 等 alternate 关系。
- 不建议用 `noindex` 来影响 canonical 选择，因为这会从搜索中完全阻止该页。

Bukit 映射：

- canonical 默认由 `site.url + baseUrl + route.url` 统一生成，避免主题重复拼接。
- audit 报告 relative canonical、fragment canonical、非 HTTPS canonical、非本站 canonical、双斜杠 canonical。
- sitemap URL、canonical URL、最终输出文件路径必须能互相对齐。
- canonical 指向 noindex URL 时必须报警。

## 4. Sitemap 规则

Google 用 sitemap 更高效地发现重要页面，并可读取 lastmod、媒体、替代语言版本等信息。sitemap 不是索引保证，但它应该只声明站点希望被搜索发现的重要 URL。

Bukit 映射：

- sitemap 只读取 `SeoIndex.indexable == true` 的 route。
- `lastmod` 来自 `SeoIndex.lastmod`，而不是各插件自行猜测。
- i18n 合并 sitemap 应包含语言 alternates。
- SEO audit 校验 sitemap XML 可解析、URL 能对应最终输出文件、noindex URL 不泄漏到 sitemap。

## 5. Hreflang 与国际化

Google 对本地化版本的要求可以总结为：

- 每个语言版本要列出自己和所有其他语言版本。
- 语言版本之间需要互链，否则 Google 可能忽略 hreflang。
- URL 应使用完整 URL。
- `x-default` 可用于默认或语言选择页。
- canonical 与 hreflang 是不同信号，不能混用。

Bukit 映射：

- content page 使用 `i18nKey` 建立互链。
- list、taxonomy、pagination 等 derived route 也需要 route 级 alternates，不能只覆盖内容页。
- audit 检查缺少 `x-default`、缺少自引用、缺少返回链接、locale 格式不合法、hreflang href 非绝对 URL。

## 6. 标题、描述和 Search Appearance

Google 的 SEO Starter Guide 强调 title 与 meta description 应帮助搜索引擎和用户理解页面。Google 可能改写搜索结果标题或摘要，但站点仍需要提供清晰、唯一、贴合页面内容的元数据。

Bukit 映射：

- title 缺失、过长、重复应进入 `seo-report.json`。
- description 缺失、过长、重复应进入 `seo-report.json`。
- 每个最终 route 都应能导出 title、description、canonical、robots、schema types、indexable、inclusion 状态。
- 主题没有 SEO partial 时，`renderMode: inject` 应在标准 `<head>` 内自动注入；找不到 `<head>` 时不猜测插入位置，只报告诊断。

## 7. Open Graph、Twitter 与图片

OG/Twitter 不是 Google 索引资格的核心要求，但属于企业 SEO 平台常见的搜索与分享一致性检查。Google 图片搜索和结构化数据图片要求也强调图片 URL 的可访问性与相关性。

Bukit 映射：

- `og:image`、`twitter:image` 和 schema image 应被规范成绝对 URL。
- audit 检查图片 URL 是否绝对、是否使用 HTTP、是否可能是 preview/local 地址。
- 构建期不默认联网验证图片尺寸和 HTTP 状态；这应作为可选外部 audit 阶段。

## 8. 结构化数据

Google 推荐 JSON-LD，并要求结构化数据真实代表页面可见内容。正确标记不保证富结果，但错误或误导性标记会失去富结果资格。

Bukit 映射：

- 使用 JSON serializer 输出 JSON-LD，禁止模板字符串拼接不可信 JSON。
- 默认 schema 覆盖 `WebSite`、`Organization`、`WebPage`、`CollectionPage`、`BreadcrumbList`、`BlogPosting`、`SearchAction`、`ItemList`。
- audit 校验 JSON-LD 可解析、`@context`、`@type`、URL 字段、Article/BlogPosting 必需字段、ItemList 结构。
- 富结果资格仍需 Google Rich Results Test 或 Search Console 做线上验证，Bukit 的静态 audit 只能证明本地产物结构正确。

## 9. JavaScript SEO 与静态生成

Google 能处理 JavaScript，但抓取、渲染和索引链路更复杂。Bukit 的优势是主要内容和 SEO head 在静态 HTML 中直接可见。

Bukit 映射：

- SEO 关键元素必须存在于构建后的 HTML，而不是依赖客户端脚本写入。
- GA4 gtag 是可控 Analytics 输出，不应扩展为任意脚本注入。
- 搜索、列表、taxonomy、pagination 的可索引内容应在 HTML 中存在，不能只靠运行时 JS 渲染。

## 10. Search Console 与 Google Analytics

Google 明确区分两类事实源：

- Search Console 是 Google Search 表现的事实源，包括曝光、点击、查询、Page Indexing、Crawl Stats、canonical 等。
- Google Analytics 是站内用户行为事实源，统计任何带 tracking code 的 URL。

Bukit 映射：

- `site.analytics.google_analytics_id` 只负责 GA4 gtag 输出。
- `disableInPreview` 应避免本地 preview 污染 Analytics。
- SEO audit 产物应能作为 CI artifact 上传，与 Search Console 的线上问题做 diff。
- 后续外部 audit 命令可接 Search Console API、Rich Results Test、Lighthouse、HTTP status、broken link 检查。

## 11. 企业级 Bukit SEO 验收标准

Bukit 达到“主题无关、引擎强保证、完整搜索引擎优化套件”至少需要满足以下条件：

- 所有最终 route 都进入 `SeoIndex`：首页、内容页、列表页、taxonomy、term、pagination、derived page。
- sitemap、RSS、search index、robots.txt、HTML head、audit report 都从 `SeoIndex` / `SeoModel` 读取。
- `renderMode: theme` 保持兼容并诊断主题缺失核心 SEO 标签。
- `renderMode: inject` 对标准 HTML `<head>` 强注入、去重、转义，并有端到端 fixture 证明。
- `renderMode: off` 不输出 HTML SEO，但索引策略仍可服务 sitemap/search。
- `seo-report.json` 暴露 URL inventory、route inclusion、schema types、warnings/errors。
- `bukit seo audit` 可以独立审计构建产物，strict 模式可作为 CI gate。
- external audit 层明确作为可选增强：Rich Results、Search Console、Lighthouse、HTTP status、broken links、robots 在线模拟。

## 12. 后续实现优先级

1. 补齐 derived route 的 i18n alternates：taxonomy、term、pagination、列表页都要进入 HTML hreflang 和 audit report。
2. 强化 structured data required property 检查：按 `WebSite`、`WebPage`、`CollectionPage`、`ItemList`、`BlogPosting` 分类型校验。
3. 增加 canonical 与 sitemap/output 文件的一致性审计。
4. 增加 robots.txt 与 noindex/sitemap/search 策略冲突审计。
5. 增加外部 audit 子命令或选项，做可选联网验证：HTTP status、image status、Rich Results、Lighthouse、broken links。
6. 将 `seo-report.json` 固定为 CI artifact 契约，保持字段稳定并补 schema 文档。
