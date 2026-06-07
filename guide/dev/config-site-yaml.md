# Configuration (site.yaml) Field Reference (Developer Edition)

This document provides an authoritative field reference for `site.yaml`, including validation rules and defaults.

Implementation: `src/Bukit.Config/AppConfig.cs`, `src/Bukit.Config/ConfigLoader.cs`, `src/Bukit.Config/ConfigValidator.cs`

Related: [User docs: 04 Config](../user/04-site-yaml-config.md)

## Override Priority

Final effective priority (high to low):
1. CLI parameters (`--output/--base-url/--site-url/--clean/--draft`)
2. `site.yaml`
3. Engine defaults

## site.* Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `site.name` | string | - | Internal site identifier |
| `site.title` | string | - | Display title |
| `site.description` | string | null | Site description |
| `site.baseUrl` | string | `/` | Deployment sub-path |
| `site.url` | string | null | Absolute URL for sitemap/rss |
| `site.language` | string | `en-US` | Default language |
| `site.languages` | string[] | null | Multilingual list (enables i18n) |
| `site.defaultLanguage` | string | first in languages | Default under multilingual |
| `site.timezone` | string | `UTC` | Timezone for dates |
| `site.pluginFailMode` | string | `strict` | `strict` or `warn` |
| `site.sitemapMode` | string | `split` | `split`/`merged`/`index` |
| `site.rssMode` | string | `split` | `split`/`merged` |
| `site.searchMode` | string | `split` | `split`/`merged`/`index` |
| `site.autoSummary` | bool | false | Auto-extract summary from body |
| `site.autoSummaryMaxLength` | int | 200 | Max auto summary characters |
| `site.outputPathEncoding` | string | `none` | Path encoding: `none`/`slug`/`urlencode`/`sanitize`. Applies to both content and derived pages. |
| `site.permalinks` | dict | - | Type-to-pattern mapping |
| `site.collections` | dict | - | Collection-driven routing |
| `site.plugins` | dict | - | Plugin toggles and parameters |

### Collection Schema Fields

Each collection under `site.collections.{name}` supports:

| Field | Type | Default | Description |
|---|---|---|---|
| `name` | string | (required) | Collection identifier |
| `label` | string | - | Human-readable label |
| `source` | string | - | Content source: `notion` or `markdown` |
| `permalink` | string | - | URL pattern, e.g. `/blog/{slug}/` |
| `template` | string | - | Template file, e.g. `pages/post.html` |
| `listRoute` | string | - | List page URL, e.g. `/blog/` |
| `sortBy` | string | - | Sort field name |
| `sortDirection` | string | `desc` | `asc` or `desc` |
| `filter` | string | - | Filter expression |
| `pageSize` | int | 10 | Items per list page |
| `content.modelSchema.fieldScopes.<collection>` | array | - | Scoped content model fields for validation |
| `taxonomy` | bool | false | Enable taxonomy for this collection |
| `deriveArchive` | bool | false | Generate archive pages |

Each scoped field (`content.modelSchema.fieldScopes.<collection>[].*`) supports:

| Field | Type | Default | Description |
|---|---|---|---|
| `name` | string | (required) | Field name in front matter |
| `type` | string | `string` | Field type: `string`, `text`, `number`, `bool`, `date`, `list`, `enum` |
| `label` | string | - | Human-readable label |
| `required` | bool | false | Whether field is required |
| `default` | any | - | Default value when missing |
| `enum` | string[] | - | Allowed values for `enum` type |
| `format` | string | - | Value format: `url`, `email`, `date`, `datetime`, `slug` |
| `min` | double | - | Minimum value (number) or length (string) |
| `max` | double | - | Maximum value (number) or length (string) |

**Validation error codes:**

| Code | Description |
|---|---|
| `required` | Required field is missing |
| `type_mismatch` | Value type does not match schema type |
| `enum_mismatch` | Value not in allowed enum set |
| `format_mismatch` | Value does not match format constraint |
| `range_mismatch` | Value outside min/max range |
| `unknown_field` | Field present in content but not declared in schema |

Notes:
- `text` is an alias for `string` — both accept string values and produce the same validation.
- `unknown_field` warnings skip known system fields (collection, type, draft, title, slug, seo_title, seo_desc, description, summary, etc.).
- Schema error count is available in `dist/.bukit/build-report.json` under `summary.schemaErrorCount`.
- Set `build.schemaFailMode: strict` to abort the build on schema validation errors.
| `site.externalPlugins` | dict | - | External protocol plugin configs |
| `site.externalPluginPolicy` | string | `warn` | External plugin safety policy: `deny`/`warn`/`allow`. Invalid values throw `ConfigException` with `BKT-0002`. |
| `site.externalAssemblyTrustMode` | string | `warn` | DLL trust governance: `warn`/`strict` |
| `site.externalAssemblyAllowlist` | dict | - | Filename → SHA256 allowlist |
| `site.analytics.enabled` | bool | true | Whether analytics code output is allowed |
| `site.analytics.google_analytics_id` | string | - | GA4 Measurement ID (e.g., `G-XXXXXXXXXX`); must start with `G-`. When configured and `enabled` is not `false`, the analytics partial outputs gtag. |
| `site.searchIncludeDerived` | bool | false | Include derived pages in search |
| `site.externalProtocolIncludeRoutedPages` | bool | false | Include full routedPages in after-build |
| `site.deriveConflictPolicy` | string | `fail` | Derived page conflict: `fail`/`warn`/`last-wins`. Content-page conflicts always fail regardless. |

## content.* Fields

### provider=markdown

| Field | Type | Default | Description |
|---|---|---|---|
| `content.provider` | string | `markdown` | Content source type |
| `content.markdown.dir` | string | `content` | Markdown root directory |
| `content.markdown.defaultType` | string | `page` | Default type |
| `content.markdown.maxItems` | int | 0 | Max items (0=unlimited) |
| `content.markdown.includePaths` | string[] | - | Only read specified paths |
| `content.markdown.includeGlobs` | string[] | - | Only read matching globs |

### provider=notion

| Field | Type | Default | Description |
|---|---|---|---|
| `content.provider` | string | - | Content source type |
| `content.notion.databaseId` | string | - | Database ID |
| `content.notion.pageSize` | int | 50 | Page size |
| `content.notion.filterProperty` | string | - | Filter field name |
| `content.notion.filterType` | string | - | Filter type |
| `content.notion.sortProperty` | string | - | Sort field name |
| `content.notion.sortDirection` | string | - | Sort direction |
| `content.notion.fieldPolicy.mode` | string | - | `whitelist`/`all` |
| `content.notion.fieldPolicy.allowed` | string[] | - | Whitelist fields |
| `content.notion.maxItems` | int | 0 | Max items |
| `content.notion.includeSlugs` | string[] | - | Only fetch specified slugs |
| `content.notion.includeSlugProperty` | string | `Slug` | Field for includeSlugs |
| `content.notion.cacheMode` | string | `off` | `off`/`readwrite`/`readonly` |
| `content.notion.cacheDir` | string | `.cache/notion` | Cache directory |
| `content.notion.renderConcurrency` | int | 4 | Render concurrency |
| `content.notion.maxRps` | int | 3 | Rate limit |
| `content.notion.maxRetries` | int | 5 | 429 retry count |

### content.media (image localization)

| Field | Type | Default | Description |
|---|---|---|---|
| `content.media.downloadToLocal` | bool | false | Download remote images |
| `content.media.downloadDir` | string | `assets/uploads` | Download directory |
| `content.media.urlBase` | string | `/assets/uploads` | URL base in output |
| `content.media.defaultImageUrl` | string | - | Fallback image |
| `content.media.fieldKeys` | string[] | `[cover]` | Which fields to process |
| `content.media.maxConcurrency` | int | 4 | Download concurrency |
| `content.media.maxRetries` | int | 3 | Download retries |
| `content.media.timeoutMs` | int | 10000 | Download timeout |
| `content.media.maxFileSizeBytes` | int | 52428800 | Max file size (50MB) |
| `content.media.blockPrivateNetworks` | bool | **true** | **P1-3**：阻止下载内网地址图片（127.0.0.0/8、10.0.0.0/8、172.16.0.0/12、192.168.0.0/16、link-local）。默认已启用 SSRF 防护。实现：`src/Bukit.Engine/Content/SsrfGuard.cs` |

> **SSRF 防护**：`SsrfGuard.cs` 覆盖 loopback、RFC1918 私有网络、link-local 地址。`CloneCommand` 和 `SeoExternalAuditor` 也已添加 `SsrfGuard.SsrfSafeConnectAsync` 保护（P1-6）。

## build.* Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `build.output` | string | `dist` | Output directory |
| `build.clean` | bool | true | Clean before build |
| `build.draft` | bool | false | Render drafts |
| `build.listPageContentMode` | string | `auto` | `auto`/`always`/`never` |

## theme.* Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `theme.name` | string | - | Theme name (looks under `themes/<name>/`) |
| `theme.layouts` | string | `layouts` | Templates directory |
| `theme.assets` | string | `assets` | Assets directory |
| `theme.static` | string | `static` | Static files directory |
| `theme.params` | dict | - | Theme parameters (injected to templates) |

## taxonomy.* Fields

| Field | Type | Required | Default | Description |
|---|---:|---:|---|---|
| `taxonomy.template` | string | No | null | Optional template for taxonomy derived pages (used for index/term). If omitted, the active theme must declare templates accepting `kind: taxonomy_index` / `kind: taxonomy_term`. |
| `taxonomy.indexTemplate` | string | No | null | Taxonomy index page template (e.g., `/tags/`, `/categories/`); falls back to `taxonomy.template` when empty |
| `taxonomy.termTemplate` | string | No | null | Taxonomy term page template (e.g., `/tags/<slug>/`); falls back to `taxonomy.template` when empty |
| `taxonomy.kinds` | list | No | null | Generalized taxonomy definition list; generates arbitrary kinds (not just tags/categories). Each entry requires at least `key`, optional `kind/title/singularTitlePrefix/template/indexTemplate/termTemplate/indexEnabled/hierarchical` |
| `taxonomy.kinds[].hierarchical` | bool | No | false | (v3.0.0+) Enable hierarchical taxonomy. When enabled, automatically computes `children` and `ancestors` per term, injected into template variables and JSON output |
| `taxonomy.templates.tags.template` | string | No | null | tags derived page default template (falls back to `taxonomy.template`) |
| `taxonomy.templates.tags.indexTemplate` | string | No | null | tags index page template (falls back to `taxonomy.indexTemplate` or `taxonomy.templates.tags.template`) |
| `taxonomy.templates.tags.termTemplate` | string | No | null | tags term page template (falls back to `taxonomy.termTemplate` or `taxonomy.templates.tags.template`) |
| `taxonomy.templates.categories.template` | string | No | null | categories derived page default template |
| `taxonomy.templates.categories.indexTemplate` | string | No | null | categories index page template |
| `taxonomy.templates.categories.termTemplate` | string | No | null | categories term page template |
| `taxonomy.outputMode` | string | No | `both` | `both` (HTML + JSON) \| `pages` (HTML only) \| `data` (JSON only) \| `fields_only` (fields only, no files) |
| `taxonomy.itemFields` | string[] | No | null | Extra fields exposed on term page items (e.g., `[cover, image, date]`); each must be non-empty string |
| `taxonomy.pageSize` | int | No | 10 | Term page pagination size |
| `taxonomy.indexEnabled` | bool | No | true | Whether to generate taxonomy index pages |
| `taxonomy.pinField` | string | No | `pinned` | Pin field name; items where this field is true appear first in term pages |
| `taxonomy.pinOrderField` | string | No | null | Pin ordering field; pinned items sorted ascending by this field before `publishAt` descending |
| `taxonomy.pinFieldBySource` | object | No | null | Per-source pin field mapping (key = sourceKey, value = field name); falls back to global `pinField` |
| `taxonomy.pinOrderFieldBySource` | object | No | null | Per-source pin ordering field mapping; falls back to global `pinOrderField` |

### Notes

- Without `taxonomy.kinds`: legacy behavior, generates only tags/categories derived pages.
- With `taxonomy.kinds`: generates arbitrary taxonomy per the kinds list; `taxonomy.kinds[]` template fields have highest priority.
  If `kind` is `tags/categories`, `taxonomy.templates.<kind>.*` still serves as fallback (only applies when not specified in kinds).
- `taxonomy.kinds[]` validation: `key` is required; `kind`, `title`, `singularTitlePrefix`, `template`, `indexTemplate`, `termTemplate` are optional but must be non-empty strings if set.
- `taxonomy.kinds[].hierarchical`: when enabled, automatically computes hierarchy. Terms associate with parent via `parent` metadata (data source or `_index.md`); terms without `parent` are root nodes.
- **Term metadata** supports two loading sources:
  1. **data mode content source**: entries in `taxonomy_ensure_terms` dict (e.g., `content/data/tags.yaml`), supporting `description`, `image`, `weight`, `parent` fields
  2. **_index.md convention** (Hugo-style): `content/_taxonomy/<kind>/<slug>/_index.md` in YAML front matter format
- **RSS feeds**: each term automatically generates `<output>/<kind>/<slug>/feed.xml`
- **Alias redirects**: terms with `Aliases` automatically generate HTML redirect pages

### Template Priority (high to low)

1. `taxonomy.templates.<kind>.indexTemplate` / `taxonomy.templates.<kind>.termTemplate`
2. `taxonomy.indexTemplate` / `taxonomy.termTemplate`
3. `taxonomy.templates.<kind>.template`
4. `taxonomy.template`
5. Theme template declaration via `theme.yaml templates.*.accepts.kind` (`taxonomy_index` / `taxonomy_term`)

### Complete Example

```yaml
taxonomy:
  template: pages/page.html
  indexTemplate: pages/taxonomy-index.html
  termTemplate: pages/taxonomy-term.html
  kinds:
    - key: tags
      kind: tags
      title: Tags
      singularTitlePrefix: Tag
      termTemplate: pages/tag.html
    - key: categories
      kind: categories
      title: Categories
      singularTitlePrefix: Category
      hierarchical: true
      termTemplate: pages/category.html
    - key: series
      kind: series
      title: Series
      singularTitlePrefix: Series
      template: pages/series.html
  templates:
    tags:
      template: pages/tag.html
      indexTemplate: pages/tag-index.html
      termTemplate: pages/tag-term.html
    categories:
      template: pages/category.html
      indexTemplate: pages/category-index.html
      termTemplate: pages/category-term.html
```

## logging.* Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `logging.level` | string | `info` | Log level |

## JSON Schema Generation (P3-2)

`bukit config schema` generates a complete `site.yaml` JSON Schema file. In vNext, collection routing fields stay under `site.collections`, while scoped content validation lives under `content.modelSchema.fieldScopes`; the old collection-level `schema` key is no longer part of the config contract.

## Config Deprecation Scanner (P3-3)

`ConfigDeprecationScanner` detects 7 legacy config patterns and emits migration warnings:

| Legacy Pattern | Replacement | Rule |
|---|---|---|
| `site.rss` (old RSS config) | `site.feed` | RSS→Feed migration |
| `site.collections.<k>.outputPath` | `site.collections.<k>.permalink` | OutputPath→Permalink |
| `content.notion.rootPageId` | `content.notion.rootBlockId` | PageId→BlockId |
| `content.markdown.rootPageId` | `content.markdown.rootBlockId` | PageId→BlockId |
| `theme.sourceRef` (old ref syntax) | `theme.source` with `@` version | SourceRef→Source |
| `site.rssMode` (old toggle) | `site.plugins.feed.enabled` | RssMode→Plugin toggle |
| `build.outputPath` (old output) | `build.output` | OutputPath→Output |

Deprecation warnings appear during `bukit doctor` and at build start. Use `bukit config check` to validate fixes.
