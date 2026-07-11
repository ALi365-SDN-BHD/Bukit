# 05 Markdown Content

The Markdown provider reads files from `content.sources[].markdown.dir`, parses
YAML front matter, renders the body with Markdig, and stores the body lazily
through `MarkdownBodyStore`.

## Source Config

```yaml
content:
  sources:
    - type: markdown
      mode: content
      collection: news
      markdown:
        dir: content/news
        defaultType: article
        includeGlobs:
          - "**/*.md"
```

`markdown.defaultType` fills only a missing document `type`. In this example it
produces `type: article`, while the source independently provides
`collection: news`. Omitting collection from both the source and front matter
causes a content build error; `defaultType: article` cannot satisfy collection
ownership. A source collection overrides an item's front matter collection but
does not change its type. If content type is otherwise absent, it defaults to
`page`.

## Front Matter

Common fields:

| Field | Purpose |
|---|---|
| `title` | Page title. |
| `slug` | URL segment used by route patterns. |
| `type` | Document kind; independent from collection and defaults to `page`. |
| `collection` | Selects `site.collections.<key>`. |
| `publishAt` | Publish timestamp used by routes and feeds. |
| `language` | Variant selection for i18n. |
| `summary` | Feed, list, and SEO summary. |
| `draft` | Excluded unless `--draft` is passed. |
| `tags`, `categories` | Taxonomy and related content inputs. |
| `route` object with `url` | Optional route override; output path is derived from URL. |

Top-level `outputPath` and the `outputPath` field inside the `route` object are
rejected in Bukit 1.0. Use the `route` object's `url` field instead.

Front matter can provide collection ownership instead of the source:

```markdown
---
title: Product launch
slug: product-launch
type: article
collection: news
---
```

Even a complete `route.url` override does not make collection optional.
`addToCollections` on the source creates explicit cloned documents and routes
for its target collections; it is not implicit multi-membership.

## Markdown Behavior

- Headings are used to build `page.table_of_contents`.
- `bodyFingerprint` is derived from the body and participates in incremental
  rendering.
- When `site.autoSummary` is true, missing summaries can be generated from the
  rendered text.
- Media URLs in body HTML can be localized through `content.media`.
