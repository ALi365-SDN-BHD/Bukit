# 16 Parameter Cheatsheet

## CLI

| Area | Parameters |
|---|---|
| Config selection | `--config`, `--site` |
| URL overrides | `--base-url`, `--site-url` |
| Output | `--output`, `--dir` |
| Build mode | `--clean`, `--no-clean`, `--draft`, `--ci`, `--incremental`, `--no-incremental`, `--cache-dir`, `--metrics`, `--jobs`, `--log-format` |
| Servers | `--host`, `--port`, `--strict-port`, `--no-watch`, `--allow-lan`, `--public` |
| Audits | `--report`, `--strict`, `--external`, `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| Deploy | `--dry-run`, `--skip-build`, `--branch`, `--message`, `--force` |

## Config Defaults

| Field | Default |
|---|---|
| `site.baseUrl` | `/` |
| `site.language` | `zh-CN` |
| `site.timezone` | `Asia/Shanghai` |
| `site.outputPathEncoding` | `none` |
| `site.sitemapMode` | `split` |
| `site.searchIncludeDerived` | `false` |
| `site.pluginFailMode` | `strict` |
| `site.deriveConflictPolicy` | `fail` |
| `site.seo.enabled` | `true` |
| `site.seo.renderMode` | `inject` |
| `site.seo.diagnostics` | `warn` |
| `site.seo.homeTitleTemplate` | `{siteTitle}` |
| `site.seo.pageTitleTemplate` | `{pageTitle}` |
| `site.seo.titleSeparator` | ` \| ` |
| `site.seo.organization.type` | `Organization` |
| `site.seo.organization.sameAs` | empty array |
| `site.seo.schema.webPage` | `true` |
| `site.seo.schema.collectionPage` | `true` |
| `site.seo.schema.searchAction` | `true` |
| `site.seo.geo.enabled` | `true` |
| `site.seo.geo.llmsTxt` | `true` |
| `site.seo.geo.llmsFullTxt` | `false` |
| `site.seo.geo.llmsTxtMaxArticles` | `20` |
| `site.seo.geo.aiBotMode` | `allow` |
| `site.plugins.analytics.enabled` | `true` |
| `site.analytics.enabled` | `true` |
| `site.analytics.productionOnly` | `true` |
| `site.analytics.consent.google` | none; required with GA/GTM |
| `site.analytics.csp.mode` | none; optional `requirements-report` |
| `site.analytics.providers` | empty array |
| Plausible provider `snippetMode` | none; required as `site-specific` or `legacy` |
| Plausible provider `scriptUrl` | none; explicit URL required |
| `site.feed.mode` | `split` |
| `site.feed.formats` | `rss` |
| `site.feed.limit` | `20` |
| `site.feed.path` | `feed` |
| `site.sitemapDetail.defaultPriority` | `0.5` |
| `site.sitemapDetail.defaultChangefreq` | `weekly` |
| `site.sitemapDetail.imageEnabled` | `false` |
| `site.sitemapDetail.videoEnabled` | `false` |
| `site.pagination.enabled` | `false` |
| `site.pagination.pageSize` | `10` |
| `site.search.mode` | `split` |
| `site.search.ui` | `default` |
| `site.search.uiTheme` | `light` |
| `site.search.maxContentLength` | `8000` |
| `site.search.route` | none |
| `site.related.enabled` | `false` |
| `site.related.threshold` | `80` |
| `site.related.limit` | `5` |
| `site.collections.<name>.pagination.enabled` | `false` |
| `site.collections.<name>.noindexWhenEmpty` | `false` |
| `site.collections.<name>.pagination.pageSize` | `10` |
| `site.collections.<name>.pagination.urlPattern` | `page/:num/` |
| `site.collections.<name>.pagination.firstPageUsesListRoute` | `true` |
| `site.collections.<name>.output.rss` | `true` |
| `site.collections.<name>.output.sitemap` | `true` |
| `site.collections.<name>.output.archive` | `false` |
| `site.collections.<name>.output.archiveDetail.depth` | `monthly` |
| `site.collections.<name>.filteredLists[].operator` | `equals` |
| `site.collections.<name>.filteredLists[].emptyBehavior` | `render` |
| `content.sources[].mode` | `content` |
| `content.sources[].notion.pageSize` | `50` |
| `content.sources[].notion.fieldPolicy.mode` | `whitelist` |
| `content.sources[].notion.filterProperty` | `Published` |
| `content.sources[].notion.filterType` | `checkbox_true` |
| `content.sources[].notion.sortDirection` | `ascending` |
| `content.sources[].notion.includeSlugProperty` | `Slug` |
| `content.sources[].notion.cacheMode` | `off` |
| `content.sources[].markdown.dir` | `content` |
| `content.sources[].markdown.defaultType` | empty string |
| `content.media.downloadToLocal` | `true` |
| `content.media.downloadDir` | `assets/uploads` |
| `content.media.urlBase` | `/assets/uploads` |
| `content.media.defaultImageUrl` | `/assets/images/noneimg-news.jpg` |
| `content.media.fieldKeys` | `cover`, `image`, `thumbnail`, `og_image`, `seo_image`, `icon` |
| `content.media.maxConcurrency` | `4` |
| `content.media.maxRetries` | `3` |
| `content.media.timeoutMs` | `10000` |
| `content.media.maxFileSizeBytes` | `52428800` |
| `content.media.blockPrivateNetworks` | `true` |
| `content.media.retryBaseDelayMs` | `500` |
| `content.modelSchema.requireMediaAlt` | `true` |
| `content.modelSchema.requireRelationTargets` | `true` |
| `build.output` | `dist` |
| `build.clean` | `true` |
| `build.draft` | `false` |
| `build.listPageContentMode` | `auto` |
| `build.schemaFailMode` | `warn` |
| `build.report.enabled` | `true` |
| `build.report.securityFailMode` | `auto` |
| `build.fingerprintMode` | `size-time` |
| `build.publishDotFiles` | `false` |
| `build.followSymlinks` | `false` |
| `build.languageJobs` | `1` |
| `theme.layouts` | `layouts` |
| `theme.assets` | `assets` |
| `theme.static` | `static` |
| `theme.scss.enabled` | `false` |
| `theme.scss.outputDir` | `assets` |
| `theme.images.enabled` | `false` |
| `theme.images.formats` | `webp` |
| `theme.images.sizes` | `480`, `768`, `1200` |
| `theme.images.quality` | `80` |
| `theme.componentValidation` | `off` |
| `taxonomy.outputMode` | `both` |
| `taxonomy.pageSize` | `10` |
| `taxonomy.indexEnabled` | `true` |
| `taxonomy.pinField` | `pinned` |
| `taxonomy.kinds[].hierarchical` | `false` |
| `logging.level` | `info` |
| `deploy.branch` | `gh-pages` |
| `deploy.message` | `bukit deploy` |
| `deploy.keepHistory` | `false` |

Safety semantics that do not fit in the default-value table:

- `site.search.maxContentLength` is a positive UTF-16 code-unit cap for search
  `content`, not title, summary, or generated snippet.
- `content.media.maxConcurrency` limits active downloads within one rewrite
  operation; it is separate from render jobs.
- `build.followSymlinks` is limited to supported copy paths. Default recursive
  content, static, media, and report scans skip directory symlinks.
- `clean --dir` requires a safe project subdirectory and, when non-empty, a
  `.bukit-output-marker`.

## Allowed Values

| Field | Values |
|---|---|
| `site.outputPathEncoding` | `none`, `slug`, `urlencode`, `sanitize` |
| `site.sitemapMode` | `split`, `merged`, `index` |
| `site.feed.mode` | `split`, `merged` |
| `site.search.mode` | `split`, `merged`, `index` |
| `site.search.uiTheme` | `light`, `dark`, `auto` |
| `site.search.route` | Internal path beginning with `/`; no scheme, `//`, backslash, query, fragment, control character, or `.`/`..` segment |
| `site.pluginFailMode` | `strict`, `warn` |
| `site.deriveConflictPolicy` | `fail`, `warn`, `last-wins` |
| `site.seo.renderMode` | `theme`, `inject`, `off` |
| `site.seo.diagnostics` | `off`, `warn`, `strict` |
| `site.seo.homeTitleTemplate` | `{pageTitle}`, `{siteTitle}`, and `{separator}` placeholders; must include page or site title |
| `site.seo.pageTitleTemplate` | `{pageTitle}`, `{siteTitle}`, and `{separator}` placeholders; must include page title |
| `site.seo.organization.type` | `Organization`, `NewsMediaOrganization` |
| `site.seo.organization.url`, `logo` | Absolute HTTP(S), or root-relative with `site.url`; output is absolute |
| `site.seo.organization.sameAs[]` | Explicit identity/profile URLs; empty values are omitted |
| `site.seo.geo.aiBotMode` | `allow`, `block`, `selective` |
| Provider `type` | `google-analytics`, `google-tag-manager`, `plausible`, `umami` |
| Provider `measurementId` | `^G-[A-Z0-9]+$`; Google Analytics only |
| Provider `containerId` | `^GTM-[A-Z0-9]+$`; Google Tag Manager only |
| Provider `domain` | DNS host name without scheme, port, path, credentials, or IP address; Plausible only |
| Provider `snippetMode` | `site-specific` or `legacy`; Plausible only |
| Provider `websiteId` | UUID; Umami only |
| Provider `scriptUrl` | Absolute HTTPS `.js` URL without credentials or fragment; Plausible or Umami only |
| Google consent `mode` | `advanced` |
| Google consent defaults | `adStorage`, `analyticsStorage`, `adUserData`, `adPersonalization`: `granted` or `denied` |
| Google consent `waitForUpdateMs` | integer `0..5000` |
| `site.analytics.csp.mode` | `requirements-report`; requires build reports |
| `site.collections.<name>.schemaFailMode` | `off`, `warn`, `strict` |
| `site.collections.<name>.filteredLists[].operator` | `equals`, `contains`, `in` |
| `site.collections.<name>.filteredLists[].emptyBehavior` | `render`, `skip` |
| `content.sources[].type` | `markdown`, `notion` |
| `content.sources[].mode` | `content`, `data` |
| `content.sources[].notion.filterType` | `checkbox_true`, `checkbox_false`, `select_equals`, `status_equals`, `rich_text_equals`, `none` |
| `content.sources[].notion.sortDirection` | `ascending`, `descending` |
| `content.sources[].notion.cacheMode` | `off`, `readwrite`, `readonly` |
| `content.sources[].notion.fieldPolicy.mode` | `whitelist`, `all` |
| `content.sources[].notion.propertyMap` | `Title`, `Slug`, `Type`, `PublishAt`, `Language`, `I18nKey`, `Summary`, `Collection`, `SeoTitle`, `SeoDescription`, `SeoImage`, `Canonical` |
| `build.listPageContentMode` | `auto`, `always`, `never` |
| `build.schemaFailMode` | `off`, `warn`, `strict` |
| `build.report.securityFailMode` | `auto`, `off`, `warn`, `strict` |
| `build.fingerprintMode` | `size-time`, `sha256` |
| `theme.componentValidation` | `off`, `warn`, `strict` |
| `taxonomy.outputMode` | `both`, `pages`, `data`, `fields_only` |
| `logging.level` | `debug`, `info`, `warn`, `error` |
| `deploy.provider` | `github-pages` |
