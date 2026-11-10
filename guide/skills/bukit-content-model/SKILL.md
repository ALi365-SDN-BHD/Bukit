---
name: bukit-content-model
description: Use when a Bukit site loads content from Notion, localizes remote media, validates content with content.modelSchema (required fields, media alt text, schemaFailMode), or sets list and singleton page titles from content.routeMetadata.
---

# Bukit Notion, Media, and Content Model

Source basics (providers, modes, collections, front matter, data) are in
`bukit-content`.

## Notion

```yaml
content:
  sources:
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

Front matter `image` / `cover` need `image_alt` / `cover_alt`, or the default
`requireMediaAlt` check reports `canonical_media_alt_missing`. The same applies to any
exported file-type field whose key contains `image`, `cover`, `icon`, or `gallery`
(Notion file properties): add a text field named `<key>_alt`, for example a Notion
property `Cover Alt` (key `cover_alt`), and export it with `fieldPolicy.mode: all` or
`fieldPolicy.allowed`.

`content.modelSchema` validates content records. It runs even when absent (defaults:
`requireMediaAlt: true`, `requireRelationTargets: true`, other `require*` false).

```yaml
content:
  modelSchema:
    requireSummary: true          # also requireAuthor, requireOrganization, requireUpdatedAt,
                                  # requireReviewedAt, requireProvenance, requireEntityIds
    statuses: [draft, published, archived, expired]   # default list
    # contentTypes: [page, post]  # allowed `type` values; no default (any type)
    # reviewStatuses / syncStatuses: allowed `review_status` / `sync_status` values
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
  (`url|email|date|slug`; `semanticType` is used when `format` is absent), `min`/`max`,
  `sourcePolicy` (`raw|canonical|custom|provider|any`; only the value is checked).
  `label` has no effect.
- `rejectUnknownRawKeys: true` fails the build on any undeclared front matter key,
  whatever `schemaFailMode` says. Use it only with a complete field list.
- Mappings (lists) turn raw fields into canonical fields, entities, and relations:
  - `canonicalMappings`: `{ canonicalField: summary, rawKey: abstract }` copies
    `abstract` to `summary` when `summary` is not set.
  - `entityMappings`: `{ rawKey: brand, entityType: Organization }`. Each value is a
    name or a map; optional `nameField`, `idField`, `urlField`, `descriptionField`,
    `sameAsField` name the map keys (defaults `name`, `url`, `description`, `sameAs`).
  - `relationMappings`: `{ rawKey: related, relationType: related }` plus optional
    `targetType`, `targetField`, `targetIdField` (default map key `id`).
- `modelSchema.media.requireAlt|requireDescription|requireLicense` currently have no
  effect; use the top-level `requireMediaAlt|requireMediaDescription|requireMediaLicense`.
  `media.allowedKinds` works.

## Route metadata

`content.routeMetadata` sets titles and summaries for list pages, static-template
pages, breadcrumbs, and content pages whose `type` is `page` (the default) or
`singleton`, from a data source. Only the type decides, not the collection: a post with
no `type` is affected when a row has its URL, and a page with any other type never is.

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

Each row needs `route` (starting and ending with `/`) and `summary`; `title` falls back
to the row's document title (first `# H1`, else slug). Duplicate routes fail. The source
is hidden from `site.data`. Rows use `seo_description` by default; page front matter
uses `seo_desc` (`bukit-seo`). Do not rename one to match the other.

## Check

```bash
bukit config check  # Notion keys and NOTION_TOKEN
bukit build         # watch event=canonical.validation and media.* events
```
