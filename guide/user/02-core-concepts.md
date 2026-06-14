# 02 Core Concepts: What You Configure and What Bukit Builds

Bukit turns content sources plus a theme into static output. Most user work is
editing `site.yaml`, content files, theme files, and deployment settings.

## Build Pipeline

```text
site.yaml
  |
  +-- content.sources[]: Markdown or Notion records
  +-- routing: collections, list routes, filtered lists, route overrides
  +-- theme: theme.yaml, layouts, assets, static files
  +-- built-in outputs: feeds, sitemap, search, taxonomy, reports
      |
      v
    dist/
```

## `site.yaml`

The config file controls:

- Site identity: `site.name`, `site.title`, `site.url`, `site.baseUrl`
- Content inputs: `content.sources`
- Routing: `site.collections` (primary), `site.permalinks` (global fallback), filtered lists
- Build output: `build.output`, `build.clean`, incremental settings
- Theme lookup: `theme.name`, `theme.layouts`, `theme.assets`, `theme.static`
- SEO, GEO, feeds, search, taxonomy, and deployment

Bukit Core 1.0 requires `site`, `content`, and at least one
`content.sources` entry.

## Content Items

Markdown files and Notion pages are normalized into content items. A content
item can become a page when it comes from a source with `mode: content`.

Fields that commonly affect behavior:

| Field | Purpose |
|---|---|
| `collection` | Matches `site.collections.<name>` for routing and templates |
| `slug` | Stable URL segment |
| `title` | Page title |
| `summary` | Card, feed, SEO, or search summary |
| `language` | i18n filtering and output grouping |
| `i18nKey` | Translation grouping |
| `tags`, `categories` | Taxonomy and related-content signals |
| `draft` | Draft workflows when building with `--draft` |

Custom fields are still available to templates through `page.fields`.

## Collections and Routes

The recommended routing model is explicit collection routing:

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
      listTemplate: pages/list.html
```

Each collection `permalink` must include `{slug}`. Content reaches this rule by
setting `collection: post`, or by using a source default:

```yaml
content:
  sources:
    - type: markdown
      collection: post
      markdown:
        dir: content/posts
```

## Themes and Templates

Themes are filesystem directories. A Core theme normally contains:

- `theme.yaml`: manifest and template roles
- `layouts/`: Scriban templates
- `assets/`: CSS, JavaScript, images, and other files copied under `/assets/`
- `static/`: files copied to the output root

Scriban templates receive `site`, `page`, `page.items`, `site.data`, and
`site.modules` depending on the route type and configured data sources.

## Content Mode vs Data Mode

`content.sources[].mode` controls whether a source creates pages:

| Mode | Behavior |
|---|---|
| `content` | Creates routable content pages |
| `data` | Does not create routes; feeds template data through `site.data` and `site.modules` |

Use data mode for homepage sections, navigation records, pricing tables, FAQs,
or taxonomy term metadata that should not be standalone pages.

## Built-in Outputs

Bukit Core can generate built-in artifacts such as sitemap, feeds, search
indexes, taxonomy pages, pagination pages, archive pages, alias redirects,
localized media, SEO/GEO reports, and publish audit reports.

Run quality gates after builds:

```bash
bukit seo audit --dir dist
bukit geo audit --dir dist
bukit publish audit --dir dist
```
