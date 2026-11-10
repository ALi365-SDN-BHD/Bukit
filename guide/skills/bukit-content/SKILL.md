---
name: bukit-content
description: Use when adding or fixing Bukit content — content.sources (Markdown and Notion providers, content vs data mode), collection and type rules, Markdown front matter fields, slugs and dates, drafts, data modules and data/ files, and dataIndex. Notion, remote media, model schema, and route metadata are in bukit-content-model.
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
unique. Give sources explicit `name`s when templates reference `site.data.<name>`, and
do not name a source `taxonomy`, `pages_by_id`, or `taxonomy_ensure_terms` (built-in
`site.data` keys). `markdown.includePaths` loads only the listed files (`.md` is
appended when missing).

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
image_alt: A cup of green tea   # required with image (requireMediaAlt default)
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
| `image`, `cover` (+ `image_alt` / `cover_alt`) | Page image. Without the `_alt` key the build logs `canonical_media_alt_missing` (fails under `schemaFailMode: strict`; see `bukit-content-model`). Optional `_caption`, `_description`, `_license`. |
| `noindex`, `canonical`, `seo_title`, `seo_desc`, `geo:` ... | `bukit-seo`, `bukit-geo`. |
| `sitemapexclude`, `searchexclude`, `feedexclude` | Output exclusions (key case is ignored; `excludeFromSitemap`, `excludeFromSearch`, `excludeFromFeed` also work). |
| `author`, `organization`, `reviewer`, `people`, `source_url`, ... | Ownership, trust, and entity fields (`bukit-content-model`). |

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

Each item needs `scope`, `key`, and `value_type`; `value` may be empty unless the key is
in `requiredKeys` (default field names shown; rename with `scopeField`, `keyField`,
`valueField`, `valueTypeField`):

```markdown
---
scope: contact
key: email
value: hello@example.com
value_type: email      # required: text|multiline|email|phone|url
---
```

`scope` and `key` must match `^[a-z][a-z0-9_]*$`, each `scope` + `key` pair must be unique,
and `email`/`url` values are validated. Template access:
`{{ site.data_index.settings.contact.email }}`.

Files in `<root>/data/` (`.json`, `.yaml`, `.yml`; `.toml` is an error) are exposed
under `site.data_files`: `data/a.yaml` → `site.data_files.a`, `data/dir/b.yaml` →
`site.data_files.dir.b`. A file and a folder with the same name fail. All of this data
is public once a template prints it.

## Notion, media, model schema, route metadata

Notion sources, remote media localization, `content.modelSchema` validation, and
`content.routeMetadata` are covered in `bukit-content-model`.

## Check

```bash
bukit doctor        # front matter, syntax, empty bodies, routes
bukit build
```
