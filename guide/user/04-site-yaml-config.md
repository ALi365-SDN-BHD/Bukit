# 04 Site YAML Config

`site.yaml` maps to `AppConfig`. Unknown fields are rejected before defaults are
applied, so spelling, case, and nesting matter. The JSON Schema emitted by
`bukit config schema` follows the same contract.

A second YAML document after a `...` or `---` separator is rejected; move the
settings above the separator and remove it.

## Top-Level Sections

| Section | Required | Purpose |
|---|---:|---|
| `site` | yes | Site identity, route rules, SEO/GEO, plugins, feed, sitemap detail, search, related content, and menus. |
| `content` | yes | Markdown/Notion/CloudBase source list, media localization, and optional content model schema. |
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
| `site.i18n.<lang>` | none | Multilingual sites only. Per-language site identity: `title`, `description` and `organization` (`name`, `url`, `logo`, `sameAs`) replace the top-level `site.title`, `site.description` and matching `site.seo.organization` fields in that language's build. Unset fields keep the top-level value, and the organization type always stays the top-level one. `<lang>` must be a `site.languages` entry, written the same way; a blank `title`, an unknown key, or a single-language site fails validation. Root merged outputs (merged feed, root `llms.txt`) keep the top-level identity. See [11 i18n and SEO](11-i18n-seo.md#per-language-site-identity). |
| `site.baseUrl` | `/` | Base path for generated URLs. CLI `--base-url` can override it. |
| `site.languages` | `[zh-CN]` | The only language setting (the old `language` key was removed). Letters and digits in hyphen-separated subtags, such as `en` or `zh-CN` (not `en_US`). One entry builds a single-language site at the output root; two or more build language variants, each with its own code. Templates read the current language as `{{ site.language }}`. |
| `site.defaultLanguage` | first `site.languages` entry | Default language of a multilingual site; must be one of `site.languages`. With one entry it must be that entry or omitted. |
| `site.defaultLanguageInSubdir` | `false` | With two or more languages, the default language builds into the output root and other languages keep their code directories. Set `true` to put every language, including the default, under `/<code>/`. Ignored (with a warning when `true`) for a single-language site, except that `site.sharedAssets.enabled: true` still rejects `true`. |
| `site.defaultLanguageRedirect` | `true` | Writes a noindex home redirect page so `/` does not 404 on static hosting. |
| `site.defaultLanguageLegacyRedirects` | `false` | With the root layout, writes a noindex redirect at the old `/<defaultLanguage>/...` path of every default-language HTML page, for static hosts that cannot issue 301s. |
| `site.sharedAssets` | `enabled: false`, `paths: null` | Root-layout multilingual sites only. `enabled: true` keeps theme assets, SCSS output, tokens, and copied `static/assets/` once under the default language's root output; non-default languages reference them through `site.asset_base_url`. `paths` is an optional list of output prefixes; it replaces the default `assets/` plus the enabled SCSS output directory. The media prefix (`content.media.urlBase`) is always excluded. Paths are relative output prefixes; an empty list, a blank entry, an absolute path (a leading `/` or `\`, a drive letter, or a UNC prefix), `..`, or a path normalizing to the root fails validation, and an entry equal to or inside the media prefix is rejected. `.` segments are normalized, so `./fonts/` means `fonts/`. When `paths` is omitted and `content.media.urlBase` covers every default prefix, validation fails instead of sharing nothing. |
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
| `site.feed.path` | `feed` | Atom and JSON Feed output path prefix. RSS is always `/rss.xml`. |
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
| `site.seo.defaultAuthor` | none | Author name for SEO output when a page has no author relation, no `author` field, and no GEO author. |
| `site.seo.defaultAuthorType` | `Person` | `Person` or `Organization` (case-insensitive); requires `defaultAuthor`. |
| `site.seo.authors` | none | Author profiles keyed by author name or `authoredBy` id (case-insensitive). Each entry accepts `name`, `type` (`Person` or `Organization`), `url`, `image`, and `sameAs`; see [11 i18n and SEO](11-i18n-seo.md). |
| `site.seo.organization.type` | `Organization` | `Organization` or `NewsMediaOrganization`; invalid values fail configuration validation. |
| `site.seo.organization.name` | none | Organization schema name. |
| `site.seo.organization.url` | none | Absolute HTTP(S) URL or root-relative URL resolved against `site.url`; only an absolute HTTP(S) result is emitted. |
| `site.seo.organization.logo` | none | Absolute HTTP(S) URL or root-relative URL resolved against `site.url`; only an absolute HTTP(S) result is emitted. |
| `site.seo.organization.sameAs` | empty | Explicit organization identity/profile URLs. Empty values are omitted and Bukit never guesses them. |
| `site.seo.robotsTxt.enabled` | `false` | Controls generated `robots.txt`. |
| `site.seo.robotsTxt.contentSignal` | unset | `search`, `aiInput`, `aiTrain` (each `true` or `false`, all optional). Writes a `Content-Signal:` line in the `User-agent: *` group; see [17 GEO](17-geo.md#content-signals). |
| `site.seo.schema.webPage` | `true` | Emits WebPage JSON-LD. |
| `site.seo.schema.collectionPage` | `true` | Emits CollectionPage JSON-LD. |
| `site.seo.schema.breadcrumbHome` | `false` | Starts every BreadcrumbList JSON-LD with the home page. |
| `site.seo.schema.breadcrumbParent` | none | Taxonomy kind (for example `categories`) whose first term page becomes the breadcrumb parent of content listed under it. |
| `site.seo.schema.articleType` | `BlogPosting` | Schema type of article pages that set none: `BlogPosting` or a schema.org Article type such as `Article`, `NewsArticle`, or `TechArticle`, in exact case. |
| `site.seo.schema.searchAction` | `true` | Allows SearchAction JSON-LD only when `site.search.route` is also declared and exists in the final HTML route inventory. |
| `site.seo.geo.enabled` | `true` | Enables GEO report data. |
| `site.seo.geo.llmsTxt` | `true` | Writes `llms.txt` when build output is indexable. |
| `site.seo.geo.llmsFullTxt` | `false` | Writes `llms-full.txt`. |
| `site.seo.geo.llmsTxtMaxArticles` | `20` | `0` writes every published, indexable article in each collection; a positive integer caps each collection; a negative value fails configuration validation. |
| `site.seo.geo.aiBotMode` | `allow` | `allow`, `block-training`, `block`, or `selective`. Takes effect only when `site.seo.robotsTxt.enabled` is `true`; see [17 GEO](17-geo.md#ai-crawlers). |
| `site.seo.geo.aiBotAllowList` | none | Crawler names always allowed. In `selective` mode, only these are allowed. |
| `site.seo.geo.aiBotBlockList` | none | Crawler names always blocked. A name on both lists is blocked. |
| `site.seo.geo.llmsTxtOptionalLinks[]` | none | Items with `title`, `url`, and optional `description`. |
| `site.plugins.analytics.enabled` | `true` | Enables the Core built-in Analytics plugin lifecycle. When false, Bukit does not create or run its HTML transform. |
| `site.analytics.enabled` | `true` | Enables Analytics output after the plugin lifecycle switch has allowed the plugin to run. |
| `site.analytics.productionOnly` | `true` | Injects during production builds and removes Bukit-managed blocks from development/preview responses. Set false to retain Analytics in development and preview. |
| `site.analytics.consent.google` | none | Optional; defines explicit Consent Mode v2 defaults for GA/GTM. Without it, Google loads with its own defaults (effectively granted). Rejected when no GA or GTM provider exists. |
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
| `site.collections.<name>.permalink` | required | Must include `{slug}`. Other placeholders: `{title}`, `{year}`, `{month}`, `{day}`, `{type}`, `{collection}`, and `{fields.<name>}` for a top-level text or whole-number field, slugified like `{title}`, such as `/malaysia/{fields.city}/attractions/{slug}/`. A page whose field is missing, empty or slugifies to nothing fails the build (`RouteInvalidPattern`), and two pages with the same URL fail as a route conflict. |
| `site.collections.<name>.template` | none | Content page template. |
| `site.collections.<name>.listRoute` | none | Collection list route; must start with `/` when set. |
| `site.collections.<name>.listTitle` | none | List page title. |
| `site.collections.<name>.listDescription` | none | List page description. |
| `site.collections.<name>.listTemplate` | none | List route template. |
| `site.collections.<name>.schemaFailMode` | none | `off`, `warn`, or `strict`. |
| `site.collections.<name>.seo.articleType` | none | Overrides `site.seo.schema.articleType` for this collection's articles. |
| `site.collections.<name>.routeWhen.field` | none | Gives a page only to entries whose field passes the test. See [Entries Without A Page](#entries-without-a-page). |
| `site.collections.<name>.routeWhen.eq` | required with `field` | The value the field must equal. |
| `site.collections.<name>.noindexWhenEmpty` | `false` | When true, an empty list route uses `noindex,follow` and is excluded from sitemap, search, `llms.txt`, and `llms-full.txt`. |
| `site.collections.<name>.indexPolicy.minimumItems` | `0` | Minimum content threshold; must be an integer >= 0. |
| `site.collections.<name>.indexPolicy.belowMinimum` | `index` | `index` or `noindex-follow`. |
| `site.collections.<name>.pagination.enabled` | `false` | Enables collection pagination. |
| `site.collections.<name>.pagination.pageSize` | `10` | Positive page size. |
| `site.collections.<name>.pagination.urlPattern` | `page/:num/` | Relative pattern with `:num`, `{num}`, or `{page}`. |
| `site.collections.<name>.pagination.firstPageUsesListRoute` | `true` | First page reuses `listRoute`. |
| `site.collections.<name>.output.rss` | `true` | Includes the collection's posts in the site feed. |
| `site.collections.<name>.output.feed` | unset | Writes the collection's own feed under `feedPath` (default `<site.feed.path>/<name>/`). Unset means `true` only when two or more collections have `output.rss: true`, so a single-collection site does not repeat its site feed; set `true` or `false` to choose explicitly. |
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

### Entries Without A Page

Every entry of a collection gets a page by default. `routeWhen` gives a page only to the entries
whose field equals a value, so a table can hold 18 cities while only 8 of them get pages:

```yaml
site:
  collections:
    city:
      permalink: /cities/{slug}/
      routeWhen: { field: has_page, eq: true }
```

The field is a top-level content field. Text is compared ignoring case and surrounding spaces, and a
boolean or whole-number value is compared as `true`, `false` or its digits. An entry without the
field gets no page.

An entry without a page is left out of collection lists, feeds, the sitemap, search and every other
page output, like a data document. It is still loaded, so [content links](#content-links) can name
it: its link has a null `url`. It does not link to other documents itself and gets no
[backref](#backrefs) list.

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

## Named Queries

`site.queries` builds named lists from one collection, so templates do not need ordered lists
prepared outside Bukit. Read them as `site.queries.<name>`.

```yaml
site:
  queries:
    route_cities:
      from: city
      where: [{ field: has_page, eq: true }, { hasBackref: routes }]
      select: [image]
    featured_routes:
      from: route
      sort: [{ field: featured, direction: desc }]
      limit: 6
    routes_by_month:
      from: route
      group: { field: best_months, values: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12] }
      limit: 3
    attraction_types:
      from: attraction
      distinct: type
```

| Field | Default | Notes |
|---|---|---|
| `from` | required | Collection read: its pages and its entries without a page. |
| `where` | none | Every filter must match: `{ field, eq }`, `{ field, in: [...] }`, or `{ hasBackref: <name> }`. |
| `sort` | collection order | `{ field, direction }` (`asc` or `desc`), or `{ field, values: [...] }` to order by position in the list. |
| `select` | none | Extra fields copied into each entry. |
| `distinct` | none | Return the distinct values of this field instead of entries. |
| `group` | none | `{ field, values }`: one group per value. Cannot be combined with `distinct`. |
| `limit` | none | At least 1. Applies per group when grouped. |

Entries start in the collection list order (newest first, then
[`content.ordering`](#content-ordering)), and each query works on the current language only.
Filters use the [filtered list](#filtered-lists) matching: text is compared without case, and a
list field matches when any item matches, so `eq: 3` finds routes whose `best_months` holds 3.
`hasBackref` keeps entries that at least one page links to through that [backref](#backrefs).
Sorting is stable, and an entry without the field (or with a value missing from `values`) comes
last in both directions.

Each entry has `key` (the slug), `title`, `slug`, `url` (null for an entry without a page),
`collection` and the `select` fields, like a [content link](#content-links). A grouped query
returns a list of `{ value, items }` in the order of `values`, where `value` is the text written
in the config. `distinct` returns plain values in entry order; list fields are flattened, and
values differing only in case appear once.

Queries see the final page fields, including backrefs and [derived fields](#derived-fields).
Incremental builds render a page again when a query it reads changes. Per-page queries, such as
"two experiences in this page's city", stay in templates, filtered with the Scriban array functions.

## Content Sources

| Field | Default | Notes |
|---|---|---|
| `content.sources[].type` | required | Provider identifier: `markdown`, `notion`, or `cloudbase`. It is unrelated to document metadata `type`. |
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
| `content.sources[].notion.propertyMap` | none | Uses `Title`, `Slug`, `Type`, `PublishAt`, `Language`, `I18nKey`, `Summary`, `Collection`, `SeoTitle`, `SeoDescription`, `SeoImage`, `Canonical`, `OriginalUrl`, `References`, `EntitiesJson`, `Cover`, `CoverAlt`, and `CoverCaption`. See [Notion Content](06-notion-content.md#canonical-source-entity-and-cover-mapping) for property types and structured values. |
| `content.sources[].cloudbase` | required when `type: cloudbase` | Reads a CloudBase PG table or view; see [27 CloudBase Content](27-cloudbase-content.md). Key fields: `envId`, `region` (`cn`/`intl`), `table`, `idField`, `pageSize`, `bodyField`, `bodyFormat`, `fieldPolicy`, `fileFields`, `storageBucket`, `signExpiresSeconds`, `cacheMode`. |
| `content.sources[].dataIndex.scopeField` | `scope` | Scope field for a `mode: data` scalar index. |
| `content.sources[].dataIndex.keyField` | `key` | Key field for a `mode: data` scalar index. |
| `content.sources[].dataIndex.valueField` | `value` | Scalar value field. |
| `content.sources[].dataIndex.valueTypeField` | `value_type` | Validation type field: `text`, `multiline`, `email`, `phone`, `url`, `object`, or `object[]` (see [07](07-multi-source-data.md#object-values)). |
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

A permalink is a file route only when the pattern itself ends in a literal
`.html`, `.htm`, `.xml`, `.json`, or `.txt` extension, such as
`/blog/{slug}.html` → `blog/my-post.html`. All other patterns produce a
directory with an `index.html`: `/blog/{slug}` with slug `release.2026` gives
`blog/release.2026/index.html`, and `/download/{slug}.php` gives
`download/<slug>.php/index.html`. A pattern that ends in a static file
extension such as `/files/{slug}.pdf` or `/js/{slug}.js` fails the build: put
those files in the theme's `static/` or `assets/` directory. Only the literal
pattern is checked, so a dotted slug value in `/blog/{slug}` is allowed.

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

## Content Ordering

Lists put the newest entries first. When entries share a publish time (for example, a date with
no time), `content.ordering` decides their order everywhere: the home page `pages` list,
collection lists and pagination, archives, site and collection feeds, and `llms.txt`. Term pages
use it too unless `taxonomy.tieBreakField` is set.

| Field | Default | Notes |
|---|---|---|
| `content.ordering.tieBreakField` | none | `url` (the final route URL), `title` (the document title, compared case-insensitively like the built-in term-page order), or any content field such as a numeric `ID`. Same rules as `taxonomy.tieBreakField`: numbers compare by value (whole numbers exactly, however long) and come before text, text compares digit runs numerically (`SR-999` before `SR-1000`), dates compare chronologically, a list field uses its first value, and entries without the field come last. Unset, each list keeps its built-in order: route URL for lists and site or collection feeds; title, then URL, for term pages and term feeds. |
| `content.ordering.tieBreakOrder` | `asc` | Direction for `tieBreakField`: `asc` or `desc`. |

```yaml
content:
  ordering:
    tieBreakField: ID
    tieBreakOrder: desc
```

## Content Links

`content.links` turns keys that a page stores in a field into the documents they name. A route page
that stores `cities: [penang, kl]` gets a `cities_links` field listing the Penang and Kuala Lumpur
documents, in the same order. Bukit writes the list next to the key field and never changes the key
field itself, so templates, SEO and search can use the links from the page fields.

| Field | Default | Notes |
|---|---|---|
| `content.links[].collection` | required | The collection whose pages hold the keys. Only content pages get links; data documents do not. |
| `content.links[].field` | required | The key field: a field name, or a nested path through structured fields such as `field: days[].attractions`. `name[]` walks every object in a list and `name` enters an object. A path can have at most 8 names. The last name holds one key or a list of keys, and every key must be text. |
| `content.links[].target.collection` | required | The collection holding the documents the keys name. It can hold pages or data documents (`mode: data`). |
| `content.links[].target.key` | `slug` | The target field matched against the keys: `slug`, or any text or whole-number content field (a Notion number `101` matches the key `"101"`). Keys are matched case-insensitively. |
| `content.links[].select` | none | Top-level target fields copied into each link, such as `image` or `summary`. A missing field is null. These names are reserved and cannot be selected: `key`, `title`, `slug`, `url`, `collection`. |
| `content.links[].backref.name` | none | Turns on the reverse list: the field written on each target page, such as `routes`. See [Backrefs](#backrefs). |
| `content.links[].backref.limit` | `0` | The most entries kept on each target page. `0` keeps them all. |
| `content.links[].backref.select` | none | Top-level fields of the linking page copied into each entry. The reserved names are the same as for `select`. |

Each link is a map with `key`, `title`, `slug`, `url` and `collection`, plus the `select`ed fields.
`url` is the target page URL relative to the site base URL, like `page.url`, so prefix it with
`base_url` as list templates do. It is null when the target is a data document with no page. Links are
not followed further: a link does not carry the target's own links.

```yaml
content:
  links:
    - collection: route
      field: cities
      target: { collection: city }          # key defaults to slug
    - collection: route
      field: days[].attractions
      target: { collection: attraction, key: code }
      select: [image, summary]
```

```html
{{ base_url = site.base_url }}{{ if base_url == "/" }}{{ base_url = "" }}{{ end }}
{{ for city in page.fields.cities_links.value }}<a href="{{ base_url }}{{ city.url }}">{{ city.title }}</a>{{ end }}
{{ for day in page.fields.days.value }}
  {{ for a in day.attractions_links }}{{ a.title }}{{ end }}
{{ end }}
```

A nested path writes its links in two places:

- In each object that holds the key field: `attractions_links` sits next to `attractions` inside every day.
- In one top-level field for the whole page, with each target listed once: `days_attractions_links`. The name joins the path names with `_`. Use it for SEO, Open Graph images or search.

Problems are reported under the page collection's `schemaFailMode` (see
[Content Model Schema](#content-model-schema)). Problems that are reported:

- A key that no target document of the page's language has (`content.link_target_unresolved`). The link is left out.
- A key that more than one target document of the page's language has (`content.link_target_duplicate`). The first document wins.
- A key value that is not text, or a path that does not match the field's shape (`content.link_value_invalid`).
- An output field that already exists (`content.link_output_conflict`). Bukit never overwrites it.

Links resolve per language, after routing. A page links only to targets built for the same
language, so an `ms` page does not link to an `en` document. Documents without a language belong to
the default language, and data documents without a `locale` belong to every language. Validation
checks each site language the same way, so a key that exists only in another language is
reported as unknown. When a target's title, URL or
selected fields change, every page that links to it is rebuilt in incremental builds. Media
targets are not supported yet.

### Backrefs

A link with `backref` also works in reverse. Each target page gets a list of the pages that link to
it, so a city page can list the routes that pass through it.

```yaml
content:
  links:
    - collection: route
      field: days[].attractions
      target: { collection: attraction, key: code }
      backref: { name: routes, limit: 6, select: [image] }
```

Every page of the target collection gets two fields, even when no page links to it:

- `routes`: the linking pages. Each entry is a map with `key` (the linking page's slug), `title`, `slug`, `url`, `collection` and the fields listed in the backref `select`.
- `routes_count`: how many pages link to it, counted before `limit`. Use it for text such as "6 of 23 routes".

The list follows the collection list order: newest first, then
[`content.ordering`](#content-ordering), then URL. A page appears once, however many times it names the target.
Only content pages count as linking pages. Only targets that have a page get a backref list; data
documents do not. When a key names more than one target, only the first one lists the page, the same
target the link itself uses. Backrefs follow the same per-language rules as links.

Backrefs are page fields on the target, so incremental builds rebuild a target page when a page
that links to it is added, removed or changes its title, URL or selected fields. Two entries that
would write the same field in one collection are rejected when the config loads. A target page that
already has the field, or `<name>_count`, keeps it, and the conflict is reported
(`content.link_output_conflict`) under the target page collection's `schemaFailMode`.

## Derived Fields

`content.derivedFields` computes page fields from other fields of the same page, such as an SEO
title from a Chinese and an English name. Each value is a Scriban template whose variables are the
page's top-level fields, including the `<field>_links` lists from [content links](#content-links).

```yaml
content:
  derivedFields:
    city:
      seo_title: { value: "{{ name }} {{ name_en }} travel guide" }
      seo_desc: { value: "{{ summary | seo.describe 150 }}" }
      summary: { value: "{{ name }}: {{ summary }}", mode: override }
```

- `mode: default` (the default) writes the field only when it is missing or empty, and skips an empty result. `mode: override` always writes it.
- Values run in the order written, so a later value can read an earlier one.
- The result is trimmed text. Writing `summary`, `description` or `excerpt` also updates the page summary that lists and SEO read.
- `seo.describe <width>` turns text into a plain description of at most that many display columns (a CJK character counts as two). It removes tags, prefers to cut after a sentence end, and otherwise cuts with `…`.
- Templates run with the invariant culture and a loop limit of 1000. The clock and random functions (`now`, `random` and `uuid`) are not available, so the same content builds the same value every time.
- A value can read only its own page. It cannot read other pages or `site` data.

Values are computed after content links and before lists, SEO, feeds and search read the pages,
and incremental builds rebuild a page when a derived value changes. Field names must be valid
names and cannot be built-in fields such as `slug`, `title`, `route` or `language`. A template
that does not parse fails when the config loads. A template that fails on a page (for example, by
hitting the loop limit) leaves the field unchanged and is reported
(`content.derived_field_failed`) under the collection's `schemaFailMode`.

## Content Media

| Field | Default | Notes |
|---|---|---|
| `content.media.downloadToLocal` | `true` | Downloads remote media into local output. |
| `content.media.downloadDir` | `assets/uploads` | Relative directory; traversal is rejected. |
| `content.media.urlBase` | `/assets/uploads` | Public URL base for localized media. |
| `content.media.defaultImageUrl` | `/assets/images/noneimg-news.jpg` | Fallback image URL. |
| `content.media.fieldKeys` | `cover`, `image`, `thumbnail`, `og_image`, `seo_image`, `icon` | Fields scanned for media URLs, at the top level and, by their own key, inside front matter mappings and lists at any depth. |
| `content.media.maxConcurrency` | `4` | Positive maximum for active localization downloads. Each rewrite operation has its own gate shared across documents, HTML, and media fields; each localized body store has a separate gate shared across its concurrent reads. It is not a process-wide network limit. |
| `content.media.maxRetries` | `3` | Non-negative retry count. A refused (400/401/403) URL whose own signature has expired is not retried. |
| `content.media.timeoutMs` | `10000` | Positive timeout. |
| `content.media.maxFileSizeBytes` | `52428800` | Positive maximum file size. |
| `content.media.blockPrivateNetworks` | `true` | SSRF guard for private networks. |
| `content.media.retryBaseDelayMs` | `500` | Non-negative retry delay. |
| `content.media.onFailure` | `placeholder` | Result of a failed download: `placeholder` uses `defaultImageUrl`; `keep` keeps the original URL. When the theme and site have no `defaultImageUrl` file (under `static/` or `assets/`), `placeholder` keeps the original URL too and logs `event=media.placeholder_missing` with the image and page; `bukit doctor` and `bukit config check` warn about it. Failures are still listed in the build log. An empty image field always uses `defaultImageUrl`. |
| `content.media.downloadHosts` | empty (all hosts) | When set, only images on these hosts are downloaded; others keep their original URL. |
| `content.media.skipHosts` | empty | Images on these hosts are never downloaded and keep their original URL. Takes precedence over `downloadHosts`. |

Host patterns are an exact host, such as file.notion.so, or `*.` plus a host,
such as *.amazonaws.com, which matches subdomains but not amazonaws.com itself.
An exact IPv4 address in canonical form, such as 203.0.113.5, is also accepted;
leading zeros (010.0.0.1), wildcards on addresses and IPv6 literals are not. Matching is case-insensitive and uses the original URL, not a redirect target.
Skipped URLs make no request and are not counted as failures. A URL that fails
once is not retried for the rest of the build.

A response with `Content-Type: application/octet-stream`, `binary/octet-stream`,
or no content type (common for Notion file storage) is identified from the
file's signature. It is stored only if it is a supported raster image that
passes decoding; SVG is never accepted.

A localized file is named after a hash of its URL. Signature and expiry
parameters are left out of that hash when the URL uses one of these signing
schemes, so a re-signed link to the same object keeps its file and is not
downloaded again:

| Scheme | Recognized by | Parameters left out |
|---|---|---|
| AWS SigV4 | `X-Amz-Signature` | `X-Amz-Algorithm`, `X-Amz-Credential`, `X-Amz-Date`, `X-Amz-Expires`, `X-Amz-SignedHeaders`, `X-Amz-Signature`, `X-Amz-Security-Token` |
| Google Cloud Storage V4 | `X-Goog-Signature` | `X-Goog-Algorithm`, `X-Goog-Credential`, `X-Goog-Date`, `X-Goog-Expires`, `X-Goog-SignedHeaders`, `X-Goog-Signature` |
| AWS SigV2, GCS V2, CloudFront | `Signature` together with `AWSAccessKeyId`, `GoogleAccessId` or `Key-Pair-Id` | `Signature`, `Expires`, `Policy`, `AWSAccessKeyId`, `GoogleAccessId`, `Key-Pair-Id` |
| Notion file proxy | host notion.so, *.notion.so or *.notion.site | `expirationTimestamp`, `signature` |
| CloudBase gateway | host `*.api.tcloudbasegateway.com` or `*.api.intl.tcloudbasegateway.com` with path `/v1/storages/object/sign/` | `token` |
| CloudBase storage CDN | host `*.storage.tcloudbase.com` | `token` |

Parameter names match case-insensitively. All other parameters, such as S3
`versionId` or image sizes, still select different files, and the download
always uses the full signed URL. If one reference to an image has an expired
link and another has a fresh one, the fresh link is used, and the expired
reference reuses the downloaded file. Other schemes (Azure SAS, Alibaba OSS,
Tencent COS) are not recognized; their URLs are named by the full address.

After upgrading to a version with this rule, signed images are downloaded once
more under a new, stable name; unsigned images keep their file names. A file an
earlier version stored is still used for the exact signed URL it came from, so a
`readonly` Notion cache whose links have expired keeps its local images. Delete
old files by hand only after a `readwrite` build has refreshed the links.

Example: download only Notion-hosted images and keep other links on failure.

```yaml
content:
  media:
    onFailure: keep
    downloadHosts:
      - "*.amazonaws.com"
      - "*.notion.so"
```

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
| `content.modelSchema.requireRelationTargets` | Defaults to `true`. Blank targets are always invalid; when enabled, non-blank targets must resolve to a content record or entity. `translation-of` language keys and absolute HTTP(S) URLs are exempt. |

Canonical validation reports `canonical_duplicate_id` when two documents share an id; with two or more `site.languages`, only documents in the same language collide, so translations may share one, and a document without a language counts as the default language, `canonical_relation_target_missing` for a blank target, and `canonical_relation_target_unresolved` when a target cannot be resolved. Content targets resolve by id or slug; entity targets resolve by id or name. Root-relative paths are not treated as external targets.

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
| `build.clean` | `true` | Empty the output before each build. Incremental builds reuse pages only when `false`; see [Clean And Incremental Builds](#clean-and-incremental-builds). |
| `build.draft` | `false` | Include draft content. |
| `build.listPageContentMode` | `auto` | `auto`, `always`, or `never`. |
| `build.schemaFailMode` | `warn` | `off`, `warn`, or `strict`. |
| `build.report.enabled` | `true` | Writes build, route, asset, and incremental reports. Security report still writes when disabled. |
| `build.report.securityFailMode` | `auto` | `auto`, `off`, `warn`, or `strict`. |
| `build.fingerprintMode` | `size-time` | `size-time` or `sha256`. |
| `build.publishDotFiles` | `false` | Allows dotfiles in copied output. |
| `build.followSymlinks` | `false` | Allows symlink traversal only in supported copy paths with real-path checks. Default recursive content/static/media/report discovery still skips directory symlinks and reparse points. |
| `build.languageJobs` | `1` | Positive language build concurrency. |
| `build.templateLoopLimit` | `1000` | Positive Scriban loop iteration limit for page, component and section templates. See [Loop Limit](08-themes-templates.md#loop-limit). |

### Clean And Incremental Builds

Incremental builds (on by default) reuse a page only when its output file from
the previous build is still there and none of its inputs changed. With
`build.clean: true`, the default, the output is emptied first, so every page
renders again. Set `build.clean: false` (or pass `--no-clean`) for day-to-day
builds: unchanged pages are skipped, a changed page and the pages whose
templates read the changed data render again, and files Bukit generated earlier
but no longer produces are still removed. Files you put in the output yourself
are kept.

Use `build.clean: true` or `bukit build --clean` when you need output built from
nothing: in CI and release builds, or with a Bukit binary built without git
metadata, which incremental builds cannot tell apart from its release. `bukit dev`
always starts with one clean build and rebuilds incrementally after that.
`<output>/.bukit/incremental-manifest.json` reports why each page rendered.

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
| `theme.images.enabled` | `false` | Enables responsive original-format variants and requested next-generation formats. |
| `theme.images.formats` | `webp` | Case-insensitive output formats. WebP uses the Core-bundled SkiaSharp encoder and needs no PATH tool. `[]` disables next-generation generation and projection without deleting already owned WebP. AVIF remains fail-closed until Core has an approved encoder and full decoder validation path. |
| `theme.images.sizes` | `480`, `768`, `1200` | Positive widths below the source width; images are never upscaled. Resized original-format variants are generated for media images only; WebP candidates use these widths for every local JPEG/PNG image. |
| `theme.images.quality` | `80` | WebP/JPEG quality setting, clamped to `1..100` by the built-in WebP encoder. |
| `theme.images.sizesAttribute` | none | CSS `sizes` value (for example `(max-width: 768px) 100vw, 768px`) added beside a generated media `srcset` that has no author `sizes`; lazy images get `auto, ` in front. Must not itself start with `auto`. |
| `theme.images.fallbackVariants` | `all` | `all` or `none`. With WebP in `formats`, `none` skips resized original-format variants of JPEG/PNG media images; the fallback `img` keeps only its original `src`. Without WebP it has no effect. |
| `theme.componentValidation` | `off` | `off`, `warn`, or `strict`. |

When WebP is enabled, Bukit emits validated full-width and configured smaller
WebP candidates for local JPEG/PNG content images. Final HTML uses one
`<picture>` with `source type="image/webp"`; the original `img`, original-format
`src`/`srcset`, `sizes`, accessibility text, dimensions, loading hints, classes,
IDs, and other attributes remain the fallback. Existing `picture` markup and
external, data, SVG, or GIF images are not rewritten.

Only media images (under `content.media.urlBase`) get an original-format
`srcset`, so Bukit generates resized original-format variants (for example
`photo-480w.jpg`) for media images only. Theme and static images elsewhere under
`assets/` keep their single original file and, when WebP is enabled, still get
their WebP candidates and `<picture>`. Resized variants an earlier build made for
those images are deleted on the next build.

A media image (under `content.media.urlBase`) whose size Bukit can read also
gets `width` and `height` from the displayed size (after EXIF orientation) when
it has neither. Every media image on a page except the first also gets
`loading="lazy"` when it has no `loading` attribute; the first, often the
largest above-the-fold image, loads immediately. Set `loading="lazy"` on it to
defer it anyway, or `loading="eager"` on a later image to load that one
immediately; author-written `width`, `height`, and `loading` are never changed.

A generated `srcset` without `sizes` makes the browser assume the image fills the
viewport (`100vw`), so it downloads a wider candidate than a narrow column needs.
Set `theme.images.sizesAttribute` to the layout width of content images, for
example `(max-width: 768px) 100vw, 768px`. Lazy images then get
`sizes="auto, <value>"`, so the browser uses the laid-out width and falls back to
the value; an eager image (the first one, or one marked `loading="eager"`) gets
the plain value, since the spec allows `auto` only on lazy images. The value also
reaches the WebP `<source>`. Images whose author wrote `sizes` or `srcset` are
left alone. Unset, Bukit adds no `sizes`.

With WebP on, every JPEG/PNG media image gets resized WebP candidates and, by
default, resized original-format ones for the fallback `img` `srcset`.
`theme.images.fallbackVariants: none` skips the original-format variants: the
`<picture>` keeps its WebP `<source>` (with the `sizes` value), and the fallback
`img` keeps only its original `src`, so a browser without WebP support downloads
the full original. Every current browser supports WebP, so this mainly saves
output size and build time. Variants an earlier build made are deleted on the
next build.
Incremental builds re-render pages when a media image's width or height changes,
and the first incremental build after upgrading re-renders every page.

Static WebP media images get resized WebP candidates and an `img` `srcset` the
same way JPEG/PNG media images get original-format ones, but no `<picture>` (the
source is already WebP). A WebP file is left as uploaded when it is animated,
when its name already ends in `-<width>w.webp`, or when a JPEG/PNG with the same
name sits beside it, compared without regard to case (their WebP candidates
would share file names).

Resizing always uses the built-in SkiaSharp encoder for every source format. No
external image tool is used or needed, and Bukit ignores ImageMagick (`magick` or
`convert`) even when it is on PATH.

Image validation uses SkiaSharp for JPEG/PNG/GIF/WebP/BMP and TiffLibrary for
standard TIFF pages. TIFF validation does not add TIFF optimization.
APNG conversion is skipped because the pinned decoder cannot expose every frame.
WeChat conversion also rejects animated GIF/WebP rather than producing a still
image; supported JPEG/PNG files within the upload limit retain their original bytes.
Built-in conversion preserves display orientation, including PNG eXIf, and normalizes colors to sRGB;
responsive candidate widths follow the displayed image dimensions.
Owned variants recorded with an older encoder identity, including ones an earlier
Bukit made with ImageMagick, are regenerated once.
Native Skia assets are included for macOS, Windows, and Linux, so resizing and WebP
generation need no image tool installation.

Pages reference every planned variant, so a variant that fails to generate fails the build
under the default `site.pluginFailMode: strict`; the previous published output stays in place.
With `warn`, the build continues and those `srcset` candidates may return 404 until the next
successful build. Failures stop after 5 variants in strict mode;
warn mode attempts every variant. A variant blocked by a leftover
`*.bukit-freshness.json` file that Bukit does not own counts as a failure; delete that
file so the variant can be generated. Existing variant files Bukit does not own are kept.

Incremental builds re-render pages when a source image's path or display width changes,
because that is all the built-in responsive markup depends on. A custom template or plugin
whose page HTML depends on other image properties (height, format, bytes) should be built
with `--clean` after replacing images.

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
| `taxonomy.emptyTerms` | `render` | Term pages with no content: `render` emits them as before, `noindex` emits them with robots noindex and keeps them out of the sitemap, `skip` emits none (a hierarchical parent with child terms is kept). |
| `taxonomy.tieBreakField` | `content.ordering.tieBreakField`, else `title` | Orders term-page entries with the same publish time (after pinned entries): `title`, `url` (the final route URL, so a content field named `url`/`URL` cannot be selected), or any content field such as a numeric `ID`. Numbers compare by value and come before text in either direction; text compares digit runs numerically (`SR-999` before `SR-1000`); dates compare chronologically; a list field uses its first value. Entries without the field come last. `title` `asc` is the built-in order (title compared case-insensitively, then URL), so writing it changes nothing; `title` `desc` is its exact reverse. Term RSS feeds order same-time entries the same way as their term pages. This decides which page each entry lands on. |
| `taxonomy.tieBreakOrder` | `asc` | Direction for `tieBreakField`: `asc` or `desc`. Unset, it follows `content.ordering.tieBreakOrder` when the field is inherited. Set on its own, it applies to the inherited field, or to `title` without `content.ordering`. |
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

Taxonomy metadata defaults follow the site language (`{{ site.language }}`): languages beginning with
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
| `deploy.repo` | project origin | GitHub repository as `owner/name` (for example `ALi365-SDN-BHD/site`). Overrides origin; `--repo` overrides it. Not a URL, and no `.git` suffix. An empty value is an error; remove the key to use origin. |
| `deploy.message` | `bukit deploy` | Commit message, at most 4096 characters. |
| `deploy.cname` | none | Optional valid domain name. |
| `deploy.keepHistory` | `false` | Keeps target branch history when supported. |
