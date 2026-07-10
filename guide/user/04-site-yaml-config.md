# 04 Site YAML Config

`site.yaml` maps to `AppConfig`. Unknown fields are rejected before defaults are
applied, so spelling, case, and nesting matter. The JSON Schema emitted by
`bukit config schema` follows the same contract.

## Top-Level Sections

| Section | Required | Purpose |
|---|---:|---|
| `site` | yes | Site identity, route rules, SEO/GEO, plugins, feed, sitemap detail, search, related content, and menus. |
| `content` | yes | Markdown/Notion source list, media localization, and optional content model schema. |
| `build` | no | Output directory, cleaning, draft mode, report behavior, fingerprinting, and language concurrency. |
| `theme` | no | Local theme path, template roots, params, shortcodes, components, SCSS, image optimization, and component validation. |
| `taxonomy` | no | Taxonomy kinds, output mode, page size, pin fields, and term injection. |
| `logging` | no | Runtime log level. |
| `deploy` | no | GitHub Pages deployment options. |

## Required Minimal Fields

```yaml
site:
  name: docs
  title: Docs
  collections:
    page:
      permalink: /{slug}/
      template: pages/page.html

content:
  sources:
    - type: markdown
      mode: content
      collection: page
      markdown:
        dir: content
```

## Site Fields

| Field | Default | Notes |
|---|---|---|
| `site.name` | required | Stable site identifier. |
| `site.title` | required | Human-facing site title. |
| `site.url` | none | Absolute site URL for canonical URLs, feeds, sitemap, robots, and deploy output. |
| `site.description` | none | Default description used by templates and SEO models. |
| `site.baseUrl` | `/` | Base path for generated URLs. CLI `--base-url` can override it. |
| `site.language` | `zh-CN` | Single-language default. |
| `site.languages` | none | Enables language variants when set. |
| `site.defaultLanguage` | none | Language considered the root/default variant. |
| `site.timezone` | `Asia/Shanghai` | Must resolve to a valid time zone identifier. |
| `site.outputPathEncoding` | `none` | `none`, `slug`, `urlencode`, or `sanitize`. |
| `site.sitemapMode` | `split` | `split`, `merged`, or `index`. |
| `site.searchIncludeDerived` | `false` | Includes derived pages in search output when enabled. |
| `site.pluginFailMode` | `strict` | `strict` or `warn`. |
| `site.deriveConflictPolicy` | `fail` | `fail`, `warn`, or `last-wins`. |
| `site.permalinks` | none | Type-to-route fallback map. Collection routing wins over this fallback. |
| `site.collections` | none | Collection route and list route definitions. |
| `site.plugins` | none | Per-plugin enable flags and options. |
| `site.menus` | none | Named menu arrays with nested children. |

## Site Output Settings

| Field | Default | Notes |
|---|---|---|
| `site.feed.mode` | `split` | `split` or `merged`. |
| `site.feed.formats` | `rss` | Feed formats list. |
| `site.feed.limit` | `20` | Positive item limit. |
| `site.feed.path` | `feed` | Feed output path prefix. |
| `site.sitemapDetail.defaultPriority` | `0.5` | Number from 0 to 1. |
| `site.sitemapDetail.defaultChangefreq` | `weekly` | Change frequency string written into sitemap metadata. |
| `site.sitemapDetail.imageEnabled` | `false` | Enables image sitemap detail. |
| `site.sitemapDetail.videoEnabled` | `false` | Enables video sitemap detail. |
| `site.search.mode` | `split` | `split`, `merged`, or `index`. |
| `site.search.ui` | `default` | Search UI identifier. |
| `site.search.uiTheme` | `light` | `light`, `dark`, or `auto`. |
| `site.search.placeholderText` | none | Search input placeholder. |
| `site.search.maxContentLength` | `8000` | Positive content length cap for search records. |
| `site.related.enabled` | `false` | Enables related-content data. |
| `site.related.threshold` | `80` | Match threshold. |
| `site.related.limit` | `5` | Positive result limit. |
| `site.related.indices[].name` | `tags`, `categories` | Index names used for matching. |
| `site.related.indices[].weight` | `80`, `60` | Index weights. |

## SEO, GEO, And Analytics

| Field | Default | Notes |
|---|---|---|
| `site.seo.enabled` | `true` | Enables SEO model generation and injection. |
| `site.seo.renderMode` | `inject` | `theme`, `inject`, or `off`. |
| `site.seo.diagnostics` | `warn` | `off`, `warn`, or `strict`. |
| `site.seo.homeTitleTemplate` | `{siteTitle}` | Final HTML title template for `/`; must contain `{pageTitle}` or `{siteTitle}`. |
| `site.seo.pageTitleTemplate` | `{pageTitle}` | Final HTML title template for every non-home route; must contain `{pageTitle}`. |
| `site.seo.titleSeparator` | ` \| ` | Text substituted for `{separator}`; may be explicitly empty. |
| `site.seo.defaultImage` | none | Default social image. |
| `site.seo.twitterSite` | none | Twitter/X site handle. |
| `site.seo.organization.name` | none | Organization schema name. |
| `site.seo.organization.url` | none | Organization URL. |
| `site.seo.organization.logo` | none | Organization logo. |
| `site.seo.robotsTxt.enabled` | `false` | Controls generated `robots.txt`. |
| `site.seo.schema.webPage` | `true` | Emits WebPage JSON-LD. |
| `site.seo.schema.collectionPage` | `true` | Emits CollectionPage JSON-LD. |
| `site.seo.schema.searchAction` | `true` | Emits SearchAction JSON-LD. |
| `site.seo.geo.enabled` | `true` | Enables GEO report data. |
| `site.seo.geo.llmsTxt` | `true` | Writes `llms.txt` when build output is indexable. |
| `site.seo.geo.llmsFullTxt` | `false` | Writes `llms-full.txt`. |
| `site.seo.geo.llmsTxtMaxArticles` | `20` | Positive article cap. |
| `site.seo.geo.aiBotMode` | `allow` | `allow`, `block`, or `selective`. |
| `site.seo.geo.aiBotAllowList` | none | Bot names allowed in selective mode. |
| `site.seo.geo.aiBotBlockList` | none | Bot names blocked in selective mode. |
| `site.seo.geo.llmsTxtOptionalLinks[]` | none | Items with `title`, `url`, and optional `description`. |
| `site.analytics.enabled` | `true` | Enables analytics model data. |
| `site.analytics.googleAnalyticsId` | none | GA identifier. |
| `site.analytics.disableInPreview` | `true` | Keeps analytics off in preview-style output. |

Title templates accept only the case-insensitive placeholders `{pageTitle}`,
`{siteTitle}`, and `{separator}`. Unknown, unopened, or unclosed placeholders
are rejected. The resolved result is trimmed and repeated whitespace is
collapsed before it is stored in `page.seo.document_title`.

## Collections

| Field | Default | Notes |
|---|---|---|
| `site.collections.<name>.permalink` | required | Must include `{slug}`. |
| `site.collections.<name>.template` | none | Content page template. |
| `site.collections.<name>.listRoute` | none | Collection list route; must start with `/` when set. |
| `site.collections.<name>.listTitle` | none | List page title. |
| `site.collections.<name>.listDescription` | none | List page description. |
| `site.collections.<name>.listTemplate` | none | List route template. |
| `site.collections.<name>.schemaFailMode` | none | `off`, `warn`, or `strict`. |
| `site.collections.<name>.pagination.enabled` | `false` | Enables collection pagination. |
| `site.collections.<name>.pagination.pageSize` | `10` | Positive page size. |
| `site.collections.<name>.pagination.urlPattern` | `page/:num/` | Relative pattern with `:num`, `{num}`, or `{page}`. |
| `site.collections.<name>.pagination.firstPageUsesListRoute` | `true` | First page reuses `listRoute`. |
| `site.collections.<name>.output.rss` | `true` | Includes collection feed output. |
| `site.collections.<name>.output.sitemap` | `true` | Includes collection items in sitemap output. |
| `site.collections.<name>.output.archive` | `false` | Enables archive routes. |
| `site.collections.<name>.output.feedPath` | none | Collection feed path. |
| `site.collections.<name>.output.feedTitle` | none | Collection feed title. |
| `site.collections.<name>.output.feedDescription` | none | Collection feed description. |
| `site.collections.<name>.output.archiveDetail.depth` | `monthly` | Archive grouping depth. |
| `site.collections.<name>.output.archiveDetail.template` | none | Archive template. |
| `site.collections.<name>.output.archiveDetail.routePrefix` | none | Archive route prefix. |

## Filtered Lists

| Field | Default | Notes |
|---|---|---|
| `field` | required | Field used for matching. |
| `operator` | `equals` | `equals`, `contains`, or `in`. |
| `value` | none | Required for `equals` and `contains`. |
| `values` | none | Required for `in`. |
| `listRoute` | required | Must start with `/`. |
| `title` | none | List title. |
| `description` | none | List description. |
| `listTemplate` | none | List template override. |
| `pageSize` | none | Optional positive page size. |
| `urlPattern` | none | Optional pagination URL pattern. |
| `emptyBehavior` | `render` | `render` or `skip`. |

## Content Sources

| Field | Default | Notes |
|---|---|---|
| `content.sources[].type` | required | `markdown` or `notion`. |
| `content.sources[].name` | none | Unique source key when set; used by data modules. |
| `content.sources[].mode` | `content` | `content` or `data`. |
| `content.sources[].collection` | none | Primary collection. |
| `content.sources[].addToCollections` | none | Extra collection memberships. |
| `content.sources[].markdown.dir` | `content` | Relative content directory. |
| `content.sources[].markdown.defaultType` | empty | Default content type. |
| `content.sources[].markdown.maxItems` | none | Positive item cap. |
| `content.sources[].markdown.includePaths` | none | Relative include path list. |
| `content.sources[].markdown.includeGlobs` | none | Relative glob list; traversal is rejected. |
| `content.sources[].notion.databaseId` | required | Notion database ID. |
| `content.sources[].notion.pageSize` | `50` | 1 to 100. |
| `content.sources[].notion.maxItems` | none | Positive item cap. |
| `content.sources[].notion.renderContent` | none | Enables/disables block rendering. |
| `content.sources[].notion.renderConcurrency` | none | Positive render concurrency. |
| `content.sources[].notion.maxRps` | none | Positive API rate limit. |
| `content.sources[].notion.maxRetries` | none | Non-negative retry count. |
| `content.sources[].notion.fieldPolicy.mode` | `whitelist` | `whitelist` or `all`. |
| `content.sources[].notion.fieldPolicy.allowed` | none | Allowed property list for whitelist mode. |
| `content.sources[].notion.filterProperty` | `Published` | Filter property when filter is active. |
| `content.sources[].notion.filterType` | `checkbox_true` | `checkbox_true`, `checkbox_false`, `select_equals`, `status_equals`, `rich_text_equals`, or `none`. |
| `content.sources[].notion.filterValue` | none | Required for select, status, and rich text filters. |
| `content.sources[].notion.sortProperty` | none | Notion sort property. |
| `content.sources[].notion.sortDirection` | `ascending` | `ascending` or `descending`. |
| `content.sources[].notion.includeSlugs` | none | Slug allowlist. |
| `content.sources[].notion.includeSlugProperty` | `Slug` | Property used for slug allowlist checks. |
| `content.sources[].notion.cacheMode` | `off` | `off`, `readwrite`, or `readonly`. |
| `content.sources[].notion.cacheDir` | none | Non-empty cache directory when set. |
| `content.sources[].notion.propertyMap` | none | Uses `Title`, `Slug`, `Type`, `PublishAt`, `Language`, `I18nKey`, `Summary`, `Collection`, `SeoTitle`, `SeoDescription`, `SeoImage`, and `Canonical`. |

`NOTION_TOKEN` must come from the environment when Notion provider secret
validation is enabled.

## Content Media

| Field | Default | Notes |
|---|---|---|
| `content.media.downloadToLocal` | `true` | Downloads remote media into local output. |
| `content.media.downloadDir` | `assets/uploads` | Relative directory; traversal is rejected. |
| `content.media.urlBase` | `/assets/uploads` | Public URL base for localized media. |
| `content.media.defaultImageUrl` | `/assets/images/noneimg-news.jpg` | Fallback image URL. |
| `content.media.fieldKeys` | `cover`, `image`, `thumbnail`, `og_image`, `icon` | Fields scanned for media URLs. |
| `content.media.maxConcurrency` | `4` | Positive concurrency when set. |
| `content.media.maxRetries` | `3` | Non-negative retry count. |
| `content.media.timeoutMs` | `10000` | Positive timeout. |
| `content.media.maxFileSizeBytes` | `52428800` | Positive maximum file size. |
| `content.media.blockPrivateNetworks` | `true` | SSRF guard for private networks. |
| `content.media.retryBaseDelayMs` | `500` | Non-negative retry delay. |

## Content Model Schema

| Field | Purpose |
|---|---|
| `content.modelSchema.contentTypes` | Allowed content types. |
| `content.modelSchema.statuses` | Allowed publish statuses. |
| `content.modelSchema.reviewStatuses` | Allowed review statuses. |
| `content.modelSchema.syncStatuses` | Allowed sync statuses. |
| `content.modelSchema.canonicalMappings[]` | Maps raw keys to canonical fields with optional semantic type and required flag. |
| `content.modelSchema.customFields[]` | Defines custom fields with `name`, `fieldType`, `required`, `semanticType`, `label`, `format`, `enum`, `min`, `max`, `default`, `sourcePolicy`, and `reference`. |
| `content.modelSchema.fieldScopes.<collection>[]` | Collection-scoped custom fields. |
| `content.modelSchema.entityMappings[]` | Entity extraction mappings with raw key, type, id/name/description/url/sameAs fields, required flag, and reference rule. |
| `content.modelSchema.relationMappings[]` | Relation mappings with raw key, relation type, target fields, required flag, and reference rule. |
| `content.modelSchema.media` | Media policy with `requireAlt`, `requireDescription`, `requireLicense`, and `allowedKinds`. |
| `content.modelSchema.rejectUnknownRawKeys` | Reject raw keys not covered by schema. |
| `content.modelSchema.requireSummary` | Require summaries. |
| `content.modelSchema.requireAuthor` | Require author metadata. |
| `content.modelSchema.requireOrganization` | Require organization metadata. |
| `content.modelSchema.requireUpdatedAt` | Require updated timestamp. |
| `content.modelSchema.requireProvenance` | Require provenance metadata. |
| `content.modelSchema.requireReviewedAt` | Require review timestamp. |
| `content.modelSchema.requireMediaAlt` | Defaults to `true`. |
| `content.modelSchema.requireMediaDescription` | Require media descriptions. |
| `content.modelSchema.requireMediaLicense` | Require media licenses. |
| `content.modelSchema.requireEntityIds` | Require entity IDs. |
| `content.modelSchema.requireRelationTargets` | Defaults to `true`. |

## Build Fields

| Field | Default | Notes |
|---|---|---|
| `build.output` | `dist` | Relative output directory. |
| `build.clean` | `true` | Clean before build. |
| `build.draft` | `false` | Include draft content. |
| `build.listPageContentMode` | `auto` | `auto`, `always`, or `never`. |
| `build.schemaFailMode` | `warn` | `off`, `warn`, or `strict`. |
| `build.report.enabled` | `true` | Writes build, route, asset, and incremental reports. Security report still writes when disabled. |
| `build.report.securityFailMode` | `auto` | `auto`, `off`, `warn`, or `strict`. |
| `build.fingerprintMode` | `size-time` | `size-time` or `sha256`. |
| `build.publishDotFiles` | `false` | Allows dotfiles in copied output. |
| `build.followSymlinks` | `false` | Allows symlink traversal in supported copy paths. |
| `build.languageJobs` | `1` | Positive language build concurrency. |

## Theme Fields

| Field | Default | Notes |
|---|---|---|
| `theme.name` | none | Local theme under `themes/<name>`. |
| `theme.layouts` | `layouts` | Layout directory. |
| `theme.assets` | `assets` | Asset directory. |
| `theme.static` | `static` | Static directory. |
| `theme.staticTemplate` | none | Template for static HTML entries. |
| `theme.params` | none | Free-form values exposed to templates. |
| `theme.shortcodes` | none | Shortcode name-to-template map. |
| `theme.components.<name>.template` | required in each component | Component template path. |
| `theme.components.<name>.props` | none | Component prop aliases. |
| `theme.scss.enabled` | `false` | Enables SCSS compilation. |
| `theme.scss.entryPoint` | none | SCSS entry point. |
| `theme.scss.outputDir` | `assets` | SCSS output directory. |
| `theme.images.enabled` | `false` | Enables image optimization. |
| `theme.images.formats` | `webp` | Output formats. |
| `theme.images.sizes` | `480`, `768`, `1200` | Positive widths. |
| `theme.images.quality` | `80` | Image quality setting. |
| `theme.componentValidation` | `off` | `off`, `warn`, or `strict`. |

## Taxonomy Fields

| Field | Default | Notes |
|---|---|---|
| `taxonomy.outputMode` | `both` | `both`, `pages`, `data`, or `fields_only`. |
| `taxonomy.itemFields` | none | Fields used for taxonomy extraction. |
| `taxonomy.pageSize` | `10` | Positive page size. |
| `taxonomy.indexEnabled` | `true` | Enables taxonomy index pages. |
| `taxonomy.pinField` | `pinned` | Boolean pin field. |
| `taxonomy.pinOrderField` | none | Numeric pin order field. |
| `taxonomy.pinFieldBySource` | none | Source-specific pin field map. |
| `taxonomy.pinOrderFieldBySource` | none | Source-specific pin order field map. |
| `taxonomy.kinds[].key` | required | Source field key. |
| `taxonomy.kinds[].kind` | same as `key` | Public taxonomy kind. |
| `taxonomy.kinds[].title` | none | Index title. |
| `taxonomy.kinds[].description` | none | Index description. |
| `taxonomy.kinds[].singularTitlePrefix` | none | Term page prefix. |
| `taxonomy.kinds[].template` | none | Shared taxonomy template. |
| `taxonomy.kinds[].indexTemplate` | none | Index template override. |
| `taxonomy.kinds[].termTemplate` | none | Term template override. |
| `taxonomy.kinds[].indexEnabled` | inherits global | Per-kind index switch. |
| `taxonomy.kinds[].hierarchical` | `false` | Builds hierarchy from slash-like terms. |
| `taxonomy.kinds[].routePrefix` | none | Absolute route prefix. |

## Logging And Deploy

| Field | Default | Notes |
|---|---|---|
| `logging.level` | `info` | `debug`, `info`, `warn`, or `error`. |
| `deploy.provider` | required when `deploy` exists | Only `github-pages` is supported. |
| `deploy.branch` | `gh-pages` | Must be a valid Git branch name. |
| `deploy.message` | `bukit deploy` | Commit message, at most 4096 characters. |
| `deploy.cname` | none | Optional valid domain name. |
| `deploy.keepHistory` | `false` | Keeps target branch history when supported. |
