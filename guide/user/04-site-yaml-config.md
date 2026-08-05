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
| `site.permalinks` | none | Document-type route map, checked after collection routing. It does not assign collection membership. |
| `site.collections` | none | Collection route and list route definitions. |
| `site.plugins` | none | Per-plugin enable flags and options. |
| `site.menus` | none | Named menu arrays with nested children. |

## Site Output Settings

| Field | Default | Notes |
|---|---|---|
| `site.feed.mode` | `split` | `split` or `merged`. |
| `site.feed.formats` | `rss` | Feed formats list. |
| `site.feed.limit` | `20` | Positive item limit; non-positive values use `20`. All enabled feed formats and publish-audit expectations use the same newest-item window, ordered by canonical publish time and then canonical URL. |
| `site.feed.path` | `feed` | Feed output path prefix. |
| `site.sitemapDetail.defaultPriority` | `0.5` | Number from 0 to 1. |
| `site.sitemapDetail.defaultChangefreq` | `weekly` | Change frequency string written into sitemap metadata. |
| `site.sitemapDetail.imageEnabled` | `false` | Enables image sitemap detail. |
| `site.sitemapDetail.videoEnabled` | `false` | Enables video sitemap detail. |
| `site.search.mode` | `split` | `split`, `merged`, or `index`. |
| `site.search.ui` | `default` | Search UI identifier. |
| `site.search.uiTheme` | `light` | `light`, `dark`, or `auto`. |
| `site.search.placeholderText` | none | Search input placeholder. |
| `site.search.maxContentLength` | `8000` | Positive UTF-16 code-unit cap for the `content` field in document, list, plugin, publish-projection, and all multilingual modes: root merged records and each language's split/index `search.json`. Title, summary, and generated snippet are not capped. |
| `site.search.route` | none | Optional final HTML route that explicitly declares the site's search experience, for example `/search/`. |
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
| `site.seo.organization.type` | `Organization` | `Organization` or `NewsMediaOrganization`; invalid values fail configuration validation. |
| `site.seo.organization.name` | none | Organization schema name. |
| `site.seo.organization.url` | none | Absolute HTTP(S) URL or root-relative URL resolved against `site.url`; only an absolute HTTP(S) result is emitted. |
| `site.seo.organization.logo` | none | Absolute HTTP(S) URL or root-relative URL resolved against `site.url`; only an absolute HTTP(S) result is emitted. |
| `site.seo.organization.sameAs` | empty | Explicit organization identity/profile URLs. Empty values are omitted and Bukit never guesses them. |
| `site.seo.robotsTxt.enabled` | `false` | Controls generated `robots.txt`. |
| `site.seo.schema.webPage` | `true` | Emits WebPage JSON-LD. |
| `site.seo.schema.collectionPage` | `true` | Emits CollectionPage JSON-LD. |
| `site.seo.schema.searchAction` | `true` | Allows SearchAction JSON-LD only when `site.search.route` is also declared and exists in the final HTML route inventory. |
| `site.seo.geo.enabled` | `true` | Enables GEO report data. |
| `site.seo.geo.llmsTxt` | `true` | Writes `llms.txt` when build output is indexable. |
| `site.seo.geo.llmsFullTxt` | `false` | Writes `llms-full.txt`. |
| `site.seo.geo.llmsTxtMaxArticles` | `20` | `0` writes every published, indexable article in each collection; a positive integer caps each collection; a negative value fails configuration validation. |
| `site.seo.geo.aiBotMode` | `allow` | `allow`, `block`, or `selective`. |
| `site.seo.geo.aiBotAllowList` | none | Bot names allowed in selective mode. |
| `site.seo.geo.aiBotBlockList` | none | Bot names blocked in selective mode. |
| `site.seo.geo.llmsTxtOptionalLinks[]` | none | Items with `title`, `url`, and optional `description`. |
| `site.plugins.analytics.enabled` | `true` | Enables the Core built-in Analytics plugin lifecycle. When false, Bukit does not create or run its HTML transform. |
| `site.analytics.enabled` | `true` | Enables Analytics output after the plugin lifecycle switch has allowed the plugin to run. |
| `site.analytics.productionOnly` | `true` | Injects during production builds and removes Bukit-managed blocks from development/preview responses. Set false to retain Analytics in development and preview. |
| `site.analytics.consent.google` | none | Required when any GA or GTM provider exists; defines explicit Consent Mode v2 defaults. |
| Google consent `mode` | none | Required as `advanced`. |
| Google consent `defaults` | none | Requires `adStorage`, `analyticsStorage`, `adUserData`, and `adPersonalization`, each `granted` or `denied`. |
| Google consent `waitForUpdateMs` | none | Optional integer from 0 through 5000. |
| `site.analytics.csp.mode` | none | Optional `requirements-report`; requires `build.report.enabled: true` and writes incomplete deployment requirements rather than a full CSP. |
| `site.analytics.providers` | empty | Ordered provider array. An empty array produces no Analytics output. |
| Provider `type` | required | `google-analytics`, `google-tag-manager`, `plausible`, or `umami`. |
| Provider `measurementId` | none | Required only for Google Analytics; must match `^G-[A-Z0-9]+$`. |
| Provider `containerId` | none | Required only for Google Tag Manager; must match `^GTM-[A-Z0-9]+$`. |
| Provider `domain` | none | Required only for Plausible; must be a DNS host name without scheme, port, path, credentials, or an IP address. |
| Provider `snippetMode` | none | Required only for Plausible; must be `site-specific` or `legacy`. |
| Provider `websiteId` | none | Required only for Umami; must be a UUID. |
| Provider `scriptUrl` | none | Required for Plausible and Umami. It must be an absolute HTTPS `.js` URL without credentials or a fragment; site-specific Plausible Cloud URLs use `/js/pa-<site-id>.js`. |

For collections whose article count grows over time, use
`site.seo.geo.llmsTxtMaxArticles: 0` to avoid silently truncating `llms.txt`.
Monitor the generated file size when using this unlimited mode.

Both Analytics switches must be enabled, at least one provider must be valid,
and the execution-mode policy must allow output. Analytics is a Core built-in
plugin, independent of SEO render mode; it is not an external protocol plugin
and is not exposed to themes or Scriban as a template model. See
[19 Analytics](19-analytics.md) for provider examples and command behavior.
Google consent advanced mode is not a zero-network guarantee, and Bukit does
not own CMP updates or per-response CSP nonces.

Breaking removal: the former googleAnalyticsId and disableInPreview keys have
been deleted. They are unknown fields, not deprecated aliases, and Bukit does
not map or fall back from them.

Title templates accept only the case-insensitive placeholders `{pageTitle}`,
`{siteTitle}`, and `{separator}`. Unknown, unopened, or unclosed placeholders
are rejected. The resolved result is trimmed and repeated whitespace is
collapsed before it is stored in `page.seo.document_title`.

`site.search.route` is a capability declaration, not a route generator. It must
start with `/` and cannot contain a scheme, `//`, a backslash, query, fragment,
control character, or `.`/`..` path segment. When SEO and SearchAction are both
enabled, Bukit requires the declared route to match a final content, derived,
list, or managed static HTML route and requires `site.url`; otherwise the build
fails with `ConfigInvalidValue`. Matching ignores case and trailing-slash
differences. With no route, or when SEO or SearchAction is disabled, Bukit omits
SearchAction and does not perform the final-route check.

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
| `site.collections.<name>.noindexWhenEmpty` | `false` | When true, an empty list route uses `noindex,follow` and is excluded from sitemap, search, `llms.txt`, and `llms-full.txt`. |
| `site.collections.<name>.indexPolicy.minimumItems` | `0` | Minimum content threshold; must be an integer >= 0. |
| `site.collections.<name>.indexPolicy.belowMinimum` | `index` | `index` or `noindex-follow`. |
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

### Minimum Collection Index Policy

`indexPolicy` noindexes thin collection list routes. When the collection item
count is strictly below `minimumItems`, `belowMinimum: noindex-follow` applies
`noindex,follow` to the collection list, pagination, and filtered list pages;
reaching the threshold restores indexability. The legacy `noindexWhenEmpty`
flag remains supported and equals `minimumItems: 1` with `belowMinimum:
noindex-follow`; declaring both fields on the same collection fails
configuration validation. Thin list routes stay in the route map with
indexable false, and feeds keep emitting eligible content items.

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
| `content.sources[].type` | required | Provider identifier: `markdown` or `notion`. It is unrelated to document metadata `type`. |
| `content.sources[].name` | none | Unique source key when set; used by data modules. |
| `content.sources[].mode` | `content` | `content` or `data`. |
| `content.sources[].collection` | none | Required ownership for `mode: content` unless each item supplies a collection. A source value overrides item collection without changing item type. |
| `content.sources[].addToCollections` | none | Creates an explicit cloned document and route for every target collection. |
| `content.sources[].markdown.dir` | `content` | Relative content directory. |
| `content.sources[].markdown.defaultType` | empty | Sets only a missing document type; it never supplies collection membership. |
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
| `content.sources[].dataIndex.scopeField` | `scope` | Scope field for a `mode: data` scalar index. |
| `content.sources[].dataIndex.keyField` | `key` | Key field for a `mode: data` scalar index. |
| `content.sources[].dataIndex.valueField` | `value` | Scalar value field. |
| `content.sources[].dataIndex.valueTypeField` | `value_type` | Validation type field: `text`, `multiline`, `email`, `phone`, or `url`. |
| `content.sources[].dataIndex.requiredKeys` | none | Required non-empty scope and key pairs. |

`NOTION_TOKEN` must come from the environment when Notion provider secret
validation is enabled.

### Strict Type And Collection Contract

For content pages, `type` describes the document kind while `collection`
defines ownership and grouping. They are independent and never derive from one
another. Content type defaults to `page`; a `mode: content` document must still
have a non-empty collection after provider projection or the build fails.
Collection can come from the source, Markdown front matter, or Notion
`content.sources[].notion.propertyMap.Collection`.

```yaml
site:
  collections:
    news:
      permalink: /{collection}/{type}/{slug}/
  permalinks:
    article: /articles/{slug}/

content:
  sources:
    - type: markdown       # provider type
      mode: content
      collection: news    # collection ownership
      markdown:
        dir: content/news
        defaultType: article  # document type only
```

Here `{type}` expands to `article` and `{collection}` to `news`. Route
resolution uses a complete route override, then
`site.collections.<collection>`, then `site.permalinks.<type>`; a partial
override overlays the resolved base route. Neither a full override nor an
`article` permalink removes the content collection requirement.

Lists, pagination, filtered lists, archives, feeds, sitemap output policy,
field scopes, and collection schema mode use `collection`. SEO
Article/BlogPosting classification uses `type`. Search records retain `type`
and `contentType` and also emit `collection`.

A `mode: data` source does not require collection, defaults missing document
type to `module`, and is not routed or indexed as a collection page.

`dataIndex` requires `mode: data` and a source `name`. Source names, field
names, scopes, and keys use `^[a-z][a-z0-9_]*$`. Duplicate scope and key pairs,
missing required values, unknown value types, invalid email values, and URLs
other than HTTP(S) or root-relative paths fail the build.

## Content Media

| Field | Default | Notes |
|---|---|---|
| `content.media.downloadToLocal` | `true` | Downloads remote media into local output. |
| `content.media.downloadDir` | `assets/uploads` | Relative directory; traversal is rejected. |
| `content.media.urlBase` | `/assets/uploads` | Public URL base for localized media. |
| `content.media.defaultImageUrl` | `/assets/images/noneimg-news.jpg` | Fallback image URL. |
| `content.media.fieldKeys` | `cover`, `image`, `thumbnail`, `og_image`, `seo_image`, `icon` | Fields scanned for media URLs. |
| `content.media.maxConcurrency` | `4` | Positive maximum for active localization downloads. Each rewrite operation has its own gate shared across documents, HTML, and media fields; each localized body store has a separate gate shared across its concurrent reads. It is not a process-wide network limit. |
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

Publish audit trust-presence warnings follow this schema. `requireAuthor`
enables `publish.author_missing`, `requireProvenance` enables
`publish.source_missing` when both canonical source fields are empty, and any
required `entityMappings[]` entry enables `publish.entity_missing`. These
presence fields are not required by an omitted/default model schema.

Canonical `authorType` accepts `Person` or `Organization`, case-insensitively.
When `author` is present and `authorType` is absent, Bukit defaults the author
type to `Person`. For a Notion property or another provider-specific raw key,
map both values explicitly:

```yaml
content:
  modelSchema:
    canonicalMappings:
      - canonicalField: author
        rawKey: Author
      - canonicalField: authorType
        rawKey: Author Type
```

For an editorial desk or other collective byline, set `Author Type` to
`Organization`. Do not use canonical `organization` or
`site.seo.organization` as an author-type discriminator: those fields describe
content/site ownership rather than the article author. Invalid values produce
`canonical_author_type_invalid`; declaring `authorType` without `author`
produces `canonical_author_type_without_author`. `build.schemaFailMode: strict`
blocks either issue, while `warn` reports it and omits the untrusted structured
author.

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
| `build.followSymlinks` | `false` | Allows symlink traversal only in supported copy paths with real-path checks. Default recursive content/static/media/report discovery still skips directory symlinks and reparse points. |
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

Taxonomy metadata defaults follow `site.language`: languages beginning with
`zh` use Chinese titles, punctuation, counts, and pagination ranges; other
languages fall back to English. Override priority is
`content.routeMetadata` SEO fields, route metadata visible fields, term
metadata, `taxonomy.kinds[]`, then the localized Core default. No additional
taxonomy localization field is required.

## Logging And Deploy

| Field | Default | Notes |
|---|---|---|
| `logging.level` | `info` | `debug`, `info`, `warn`, or `error`. |
| `deploy.provider` | required when `deploy` exists | Only `github-pages` is supported. |
| `deploy.branch` | `gh-pages` | Must be a valid Git branch name. |
| `deploy.message` | `bukit deploy` | Commit message, at most 4096 characters. |
| `deploy.cname` | none | Optional valid domain name. |
| `deploy.keepHistory` | `false` | Keeps target branch history when supported. |
