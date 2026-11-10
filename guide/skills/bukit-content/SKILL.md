---
name: bukit-content
description: Use when adding or fixing Bukit content — content.sources (Markdown and Notion providers, content vs data mode), collection and type rules, Markdown front matter fields, slugs and dates, drafts, data modules and data/ files, dataIndex, and remote media localization.
---

# Bukit Content

Content comes from `content.sources[]`. Each source uses a provider (`markdown` or
`notion`) and a mode (`content` routed pages, or `data` template-only records).

## Sources

```yaml
content:
  sources:
    - type: markdown          # provider, NOT the document type
      name: posts             # source key; unique; used by site.data.<name>
      mode: content           # content (default) | data
      collection: post        # sets collection for every item from this source
      markdown:
        dir: content/posts    # relative; no "..", no leading "/"
        defaultType: article  # fills a missing document `type` only
        includeGlobs: ["**/*.md"]
        maxItems: 500
```

An unnamed source is keyed by its type (`markdown`) when it is the only source of that
type; otherwise unnamed sources become `markdown1`, `markdown2`, ... Names must be
unique. Give sources explicit `name`s when templates reference `site.data.<name>`.

## Collection and type

- `collection` decides routing, lists, feeds, sitemap grouping. Every `mode: content`
  document needs one, or the build fails with `missing required collection` (BKT-0504).
- `type` (default `page`; `module` for data) selects `site.permalinks.<type>`, SEO
  article classification, and theme template matching.
- Type and collection never derive from each other. `defaultType` never supplies a
  collection.
- A source `collection` overrides item front matter `collection`.
- `addToCollections: [news]` emits a cloned document per extra collection; each clone
  needs its own route rule and must not collide on URL.

Rule that trips most configs: when `site.collections` is defined, set `collection:` on
every content source. If some source lacks it, every collection must still be named by
some source (`collection` or `addToCollections`), otherwise validation fails. Front
matter or Notion-only collection assignment does not satisfy this. A typo in a source
`collection` is not caught by config check; it fails later with `No route rule matches
content document`.

## Markdown files

- Every `*.md` under `dir`, recursively, sorted by path. Symlinked files and
  directories are always skipped.
- Front matter must start on line 1 with `---` and close with `---`. Keys are
  case-insensitive.
- Slug: front matter `slug`, else the file name (`content/blog/hello.md` → `hello`,
  not `blog/hello`). Same file names in one language fall back to path slugs
  (`event=markdown.slug.collision_fallback`).
- `content/index.md` is not the home page. It renders at `/index/`. The home page `/` is
  always the home list route (`bukit-routing`).
- Title: `title`, else the first `# H1`, else the slug.
- Body: Markdig with tables, task lists, footnotes, auto links, emoji, GitHub-style
  heading IDs. Raw HTML in Markdown is escaped, not rendered. Put HTML in templates or
  theme shortcodes (`{% name "arg" %}`, see `bukit-theme`). No syntax highlighting or
  math built in.

## Front matter

```yaml
---
title: Brewing Green Tea
slug: green-tea
type: article
collection: post          # ignored if the source sets collection
publishAt: 2026-10-01T09:00:00+08:00
updatedAt: 2026-10-05
summary: Water temperature and timing.
tags: [tea, guide]        # triggers automatic /tags/ pages (bukit-routing)
categories: [Guides]
language: en
draft: false
image: /assets/images/tea.jpg
---
```

| Field | Notes |
|---|---|
| `publishAt` | The only publish date. Drives sort order (newest first) and `{year}{month}{day}`. Missing → warning `content.publish_at.missing` and the Unix epoch. `date:` is just a custom field. |
| `updatedAt` / `updated` / `modified` | Updated date. |
| `summary` | Lists, feeds, SEO description fallback. |
| `url`, `template`, `route: {url, template}` | Route overrides (`bukit-routing`). `outputPath` is removed and rejected. |
| `aliases` | Redirect pages. Currently breaks the build (Core defect, see `bukit-outputs`); do not use. |
| `language`, `i18nKey` | Language variant and translation group (`bukit-i18n`). No `lang` alias. |
| `draft: true` | Dropped unless `--draft` / `build.draft: true`. |
| `noindex`, `canonical`, `seo_title`, `seo_desc`, `geo:` ... | `bukit-seo`, `bukit-geo`. |
| `sitemapexclude`, `searchexclude`, `feedexclude` | Output exclusions. |

`weight` does not affect ordering and `layout` does not select a template; use
`content.ordering.tieBreakField` and `template:`. All other keys become custom fields,
available as `page.fields.<key>.value`.

## Data

```yaml
content:
  sources:
    - type: markdown
      name: faq
      mode: data
      markdown: { dir: data-content/faq }
```

Data items are not routed and need no collection. Templates read
`site.data.<source>` (array of `{id, title, slug, content, fields}`; read custom fields
as `item.fields.<key>.value`) and
`site.modules.<type>`. Items with `enabled: false` are skipped; `order` sorts them.

Key-value settings: add `dataIndex` (data mode only, source name like `settings`) to
expose scalars as `site.data_index.<source>.<scope>.<key>`:

```yaml
    - type: markdown
      name: settings
      mode: data
      markdown: { dir: data-content/settings }
      dataIndex:
        requiredKeys: [contact.email]   # optional; fails the build if missing
```

Each item needs four fields (default names `scope`, `key`, `value`, `value_type`;
rename with `scopeField`, `keyField`, `valueField`, `valueTypeField`):

```markdown
---
scope: contact
key: email
value: hello@example.com
value_type: email      # required: text|multiline|email|phone|url
---
```

`scope` and `key` must match `^[a-z][a-z0-9_]*$`, `scope.key` pairs must be unique,
and `email`/`url` values are validated. Template access:
`{{ site.data_index.settings.contact.email }}`.

Files in `<root>/data/` (`.json`, `.yaml`, `.yml`; `.toml` is an error) are exposed
under `site.data_files`: `data/a.yaml` → `site.data_files.a`, `data/dir/b.yaml` →
`site.data_files.dir.b`. A file and a folder with the same name fail. All of this data
is public once a template prints it.

## Notion

```yaml
    - type: notion
      name: news
      collection: news
      notion:
        databaseId: 0123456789abcdef0123456789abcdef
        filterType: checkbox_true     # default: property "Published" must be checked
        filterProperty: Published
        fieldPolicy: { mode: all }    # default whitelist exports no custom properties
        cacheMode: readwrite          # off (default) | readwrite | readonly
        propertyMap:                  # PascalCase keys -> your property names
          Title: Name
          Slug: Slug
          Type: Kind
          PublishAt: Date
          Collection: Section         # rich_text, select, or status only
```

`NOTION_TOKEN` must be set in the environment (`config check`, `doctor`, and `build`
require it). Properties that differ only by case fail. Use `filterType: none` to load
every page. Without a `PublishAt` property the page creation time is used.

Advanced Notion keys (defaults in brackets): `pageSize` [50, 1–100], `maxItems`
[unlimited], `renderContent` [true for `mode: content`, false for `mode: data`; false
leaves the body empty], `renderConcurrency` [4, 2 on CI], `maxRps` [3], `maxRetries`
[5, reads only], `filterValue` (required for `select_equals`, `status_equals`,
`rich_text_equals`), `sortProperty` / `sortDirection` [`ascending`], `includeSlugs`
(fetch only these slugs) with `includeSlugProperty` [`Slug`], `cacheDir`
[`.cache/notion`, used only when `cacheMode` is not `off`].

## Remote media

```yaml
content:
  media:
    downloadToLocal: true       # default
    urlBase: /assets/uploads
    maxConcurrency: 4           # concurrent downloads per rewrite; not --jobs
    blockPrivateNetworks: true  # SSRF protection (default)
    onFailure: placeholder      # placeholder | keep
    defaultImageUrl: /assets/images/noneimg-news.jpg   # provide this file
    downloadHosts: []           # empty = all hosts
    skipHosts: [cdn.example.com]
```

Remote `http(s)` images in bodies and in `cover`, `image`, `thumbnail`, `og_image`,
`seo_image`, `icon` fields are downloaded, validated (raster images only; SVG is never
accepted), cached in `.cache/media`, and rewritten. Local paths are untouched. When a
host rate-limits, lower `maxConcurrency`.

More media keys (defaults in brackets): `fieldKeys` [`cover, image, thumbnail,
og_image, seo_image, icon`; body `<img>` and Notion file properties are always
processed], `maxRetries` [3], `timeoutMs` [10000], `maxFileSizeBytes` [52428800],
`retryBaseDelayMs` [500, exponential, capped at 30 s], `downloadDir`
[`assets/uploads`, which means `.cache/media`; any other value is a project-relative
directory].

## Model schema

`content.modelSchema` validates content records. It runs even when absent (defaults:
`requireMediaAlt: true`, `requireRelationTargets: true`, other `require*` false).

```yaml
content:
  modelSchema:
    requireSummary: true          # also requireAuthor, requireUpdatedAt, requireProvenance, ...
    statuses: [draft, published, archived, expired]   # default list
    fieldScopes:
      post:                       # key = collection name
        - name: difficulty
          fieldType: string       # string|number|bool|date|list
          enum: [beginner, advanced]
          default: beginner
        - name: reading_time
          fieldType: number
          min: 1
build:
  schemaFailMode: warn            # off | warn (default) | strict
```

- Issues log as `event=canonical.validation code=... field=...`. `strict` fails with
  `Canonical content validation failed with N error(s).`; `off` drops them. A
  collection's `schemaFailMode` overrides the build value.
- `customFields` applies to every document; `fieldScopes.<collection>` to one
  collection. Field options: `required`, `default`, `enum`, `format`
  (`url|email|date|slug`), `min`/`max`.
- `rejectUnknownRawKeys: true` fails the build on any undeclared front matter key,
  whatever `schemaFailMode` says. Use it only with a complete field list.
- `canonicalMappings`, `entityMappings`, `relationMappings` map raw fields to canonical
  fields, entities, and relations for structured data.
- `modelSchema.media.requireAlt|requireDescription|requireLicense` currently have no
  effect; use the top-level `requireMediaAlt|requireMediaDescription|requireMediaLicense`.
  `media.allowedKinds` works.

## Route metadata

`content.routeMetadata` sets titles and summaries for list pages, singleton pages
(collection or type `page`/`singleton`), static-template pages, and breadcrumbs from a
data source. Ordinary collection pages are not affected.

```yaml
content:
  sources:
    - type: markdown
      name: route_meta            # lowercase identifier
      mode: data                  # required; no dataIndex
      markdown: { dir: data-content/route-meta }
  routeMetadata:
    source: route_meta            # required
    requiredRoutes: [/, /blog/]   # build fails if a row is missing
    # field defaults: routeField route, titleField title, summaryField summary,
    # seoTitleField seo_title, seoDescriptionField seo_description
```

Each row needs `route` (starting and ending with `/`), `title`, and `summary`;
duplicate routes fail. The source is hidden from `site.data`.

## Check

```bash
bukit doctor        # front matter, syntax, empty bodies, routes
bukit build
```
