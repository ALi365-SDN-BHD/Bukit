# 04 Site YAML Config: Core 1.0 Field Guide

`site.yaml` is the Core site contract. Bukit Core 1.0 validates unknown fields
strictly, so remove old or experimental keys instead of leaving them commented
into active YAML.

## Override Priority

Effective settings come from:

1. CLI options such as `--output`, `--base-url`, `--site-url`, `--draft`
2. `site.yaml`
3. Engine defaults

## Minimal Markdown Config

```yaml
site:
  name: my-site
  title: My Site
  url: https://example.com
  baseUrl: /
  language: en
  collections:
    page:
      permalink: /{slug}/
      template: pages/page.html
      listRoute: /
      listTemplate: pages/index.html
content:
  sources:
    - type: markdown
      name: pages
      mode: content
      collection: page
      markdown:
        dir: content
build:
  output: dist
  clean: true
theme:
  name: starter
logging:
  level: info
```

Validate:

```bash
bukit config check
```

## Top-Level Nodes

| Node | Purpose |
|---|---|
| `site` | Identity, URLs, language, collections, menus, feed, search, SEO/GEO |
| `content` | Markdown/Notion sources, data sources, media handling, model schema |
| `build` | Output, clean, drafts, incremental/cache, reports, security |
| `theme` | Active theme name, directories, params, components, SCSS, images |
| `taxonomy` | Taxonomy kinds, term output, index pages, pinning |
| `logging` | Log level |
| `deploy` | GitHub Pages deployment settings |

## `site`

Common fields:

| Field | Meaning | Example |
|---|---|---|
| `site.name` | Internal site id | `docs` |
| `site.title` | Display title | `Project Docs` |
| `site.description` | Site description | `Documentation site` |
| `site.url` | Absolute public URL | `https://example.com` |
| `site.baseUrl` | Deployment path prefix | `/` or `/my-repo` |
| `site.language` | Default language | `en` |
| `site.languages` | Enabled languages | `[en, zh-CN, ms]` |
| `site.defaultLanguage` | Default language in multilingual mode | `en` |
| `site.timezone` | IANA timezone | `Asia/Kuala_Lumpur` |
| `site.sitemapMode` | Sitemap mode | `split`, `merged`, `index` |
| `site.search.mode` | Search mode | `split`, `merged`, `index` |
| `site.permalinks` | Optional advanced fallback URL rules by content type | `post: "/{year}/{month}/{slug}/"` |
| `site.feed.formats` | Feed formats | `[rss, atom, json]` |
| `site.collections` | Route and template rules | see below |
| `site.menus` | Menu records for templates | `main: [...]` |
| `site.seo` | SEO and GEO config | see [11](./11-i18n-seo.md) and [17](./17-geo.md) |

## `site.permalinks`

`site.permalinks` is a lightweight global routing branch that applies when a content item does not match a collection rule and has no route override.

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
    page: "/{type}/{slug}/"
```

Lookup precedence is:

1. Front matter route override
2. `site.collections.<type>`
3. `site.permalinks.<type>` (advanced global fallback)

The pattern supports placeholders such as `{slug}`, `{year}`, `{month}`, `{day}`, `{type}`, and `{collection}`.

## Collections

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
      listTemplate: pages/list.html
      pagination:
        enabled: true
        pageSize: 10
      output:
        rss: true
        sitemap: true
        archive: true
```

Rules:

- Collection keys must be non-empty.
- `permalink` is required and must include `{slug}`.
- `listRoute`, when set, must start with `/`.
- `schemaFailMode`, when set, is `off`, `warn`, or `strict`.
- `filteredLists` can create additional list routes based on an explicit field/operator match when `listRoute` is set.

### Filtered Lists

`site.collections.<name>.filteredLists` is for manually configured static entry
pages. Use it when the route itself is an editorial or product decision, such as
a fixed country page, industry page, topic landing page, or curated list.
Each entry is an explicit match against one `field`; Bukit does not expand tags,
categories, topics, or other terms into filtered-list routes.

```yaml
site:
  collections:
    company:
      permalink: /companies/{slug}/
      template: pages/company.html
      listRoute: /companies/
      filteredLists:
        - field: country
          operator: equals
          value: Malaysia
          listRoute: /companies/malaysia/
          listTemplate: pages/company-list.html
          pageSize: 10
          urlPattern: page/{page}/
          emptyBehavior: render
        - field: category
          operator: in
          values:
            - 市场观察
            - 政策动态
          listRoute: /companies/market/
```

Do not use `filteredLists` as a replacement for dynamic taxonomy pages. If a
site should generate one page per tag, category, topic, or term from content
metadata, configure `taxonomy.kinds` instead. `filteredLists` stays intentionally
explicit: each route is declared by hand and represents one fixed filter.
Filtered-list routes are generated only when the parent collection also defines
`listRoute`; without that collection list route, Bukit warns and skips the
filtered-list route.

`operator` defaults to `equals`. Supported operators are:

- `equals`: matches a scalar field, select field, list item, or date field by
  case-insensitive text, slug-equivalent text, or calendar date.
- `contains`: matches when a scalar text field contains `value`; list fields
  match when any item contains `value`.
- `in`: matches when a scalar field or any list item equals one of `values`.

Use `value` with `equals` and `contains`; use `values` with `in`.

Filtered lists use build-time pagination. `pageSize` controls the number of
matched items per static page, `urlPattern` controls page 2 and later under the
filtered `listRoute`, and `emptyBehavior` is `render` or `skip`. With the example
above, Bukit can generate `/companies/malaysia/` and
`/companies/malaysia/page/2/`.

For a step-by-step migration from JavaScript pagination or browser-side filters
to build-time list routes, see [18 Static List Routes Migration](./18-static-list-routes-migration.md).

## `content`

Bukit Core 1.0 uses `content.sources` for all content:

```yaml
content:
  sources:
    - type: markdown
      name: posts
      mode: content
      collection: post
      markdown:
        dir: content/posts
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data/modules
```

Source rules:

- `type` is `markdown` or `notion`.
- `mode` is `content` or `data`.
- `name`, when set, must be unique.
- `collection` should match a `site.collections` key for routable content.
- `addToCollections` may add an item to additional collection contexts.

Markdown fields:

| Field | Meaning |
|---|---|
| `markdown.dir` | Directory to scan |
| `markdown.defaultType` | Default content type metadata |
| `markdown.maxItems` | Positive item limit |
| `markdown.includePaths` | Explicit paths under `markdown.dir` |
| `markdown.includeGlobs` | Glob filters under `markdown.dir` |

Notion fields:

| Field | Meaning |
|---|---|
| `notion.databaseId` | Required database id |
| `notion.pageSize` | 1 to 100 |
| `notion.filterProperty` | Property used for filtering |
| `notion.filterType` | `checkbox_true`, `checkbox_false`, `select_equals`, `status_equals`, `rich_text_equals`, `none` |
| `notion.filterValue` | Required for select/status/rich text equality filters |
| `notion.sortProperty` | Sort property |
| `notion.sortDirection` | `ascending` or `descending` |
| `notion.cacheMode` | `off`, `readwrite`, `readonly` |
| `notion.fieldPolicy.mode` | `whitelist` or `all` |
| `notion.propertyMap` | Map Notion property names to canonical fields |

Notion sources require `NOTION_TOKEN` from the environment for validation and
builds.

## Media

```yaml
content:
  media:
    downloadToLocal: true
    downloadDir: assets/uploads
    urlBase: /assets/uploads
    defaultImageUrl: /assets/images/noneimg-news.jpg
    fieldKeys: [cover, image, thumbnail, og_image, icon]
    blockPrivateNetworks: true
```

Keep `blockPrivateNetworks` enabled unless you have a controlled local-media
workflow.

## `build`

| Field | Meaning | Default |
|---|---|---|
| `build.output` | Output directory | `dist` |
| `build.clean` | Clean before build | `true` |
| `build.draft` | Render drafts | `false` |
| `build.listPageContentMode` | `auto`, `always`, `never` | `auto` |
| `build.schemaFailMode` | `warn` or `strict` for content model checks | `warn` |
| `build.fingerprintMode` | `size-time` or `sha256` | `size-time` |
| `build.report.securityFailMode` | `auto`, `off`, `warn`, `strict` | `auto` |

Useful CLI overrides:

```bash
bukit build --output dist --base-url /my-repo --site-url https://owner.github.io/my-repo --clean
```

## `theme`

```yaml
theme:
  name: starter
  params:
    brand: My Site
```

Allowed Core fields include `name`, `layouts`, `assets`, `static`,
`staticTemplate`, `params`, `shortcodes`, `components`, `scss`, `images`, and
`componentValidation`.

Theme inheritance belongs in `themes/<name>/theme.yaml` through `extends`, not
in `site.yaml`.

## `taxonomy`

```yaml
taxonomy:
  outputMode: both
  pageSize: 10
  indexEnabled: true
  kinds:
    - key: tags
      kind: tags
      title: Tags
      termTemplate: pages/taxonomy-term.html
```

`taxonomy.outputMode` is `both`, `pages`, `data`, or `fields_only`.

Use taxonomy when routes should be generated from content metadata terms, such
as every tag or category. Use `site.collections.*.filteredLists` only for fixed,
manually selected filter pages.

For category route migration examples, see [18 Static List Routes Migration](./18-static-list-routes-migration.md).

## `deploy`

Only GitHub Pages is Core 1.0:

```yaml
deploy:
  provider: github-pages
  branch: gh-pages
  message: "bukit deploy"
  cname: example.com
  keepHistory: true
```

Verify with:

```bash
bukit deploy --dry-run
```

## Common Validation Failures

| Symptom | Fix |
|---|---|
| Unknown config field | Remove old or Labs-only fields from active YAML |
| `content.sources is required` | Add at least one source |
| Notion token missing | Set `NOTION_TOKEN` in the environment |
| Collection without source assignment | Add `collection: <name>` to relevant content sources |
| Output path rejected | Use a relative safe path such as `dist` |
