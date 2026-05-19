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
| `site.externalPlugins` | dict | - | External protocol plugin configs |
| `site.externalAssemblyTrustMode` | string | `warn` | DLL trust governance: `warn`/`strict` |
| `site.externalAssemblyAllowlist` | dict | - | Filename → SHA256 allowlist |
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

## logging.* Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `logging.level` | string | `info` | Log level |
