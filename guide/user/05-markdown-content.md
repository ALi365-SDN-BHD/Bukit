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
      collection: post
      markdown:
        dir: content/posts
        defaultType: post
        includeGlobs:
          - "**/*.md"
```

## Front Matter

Common fields:

| Field | Purpose |
|---|---|
| `title` | Page title. |
| `slug` | URL segment used by route patterns. |
| `collection` | Selects `site.collections.<key>`. |
| `publishAt` | Publish timestamp used by routes and feeds. |
| `language` | Variant selection for i18n. |
| `summary` | Feed, list, and SEO summary. |
| `draft` | Excluded unless `--draft` is passed. |
| `tags`, `categories` | Taxonomy and related content inputs. |
| `route` object with `url` | Optional route override; output path is derived from URL. |

Top-level `outputPath` and the `outputPath` field inside the `route` object are
rejected in Bukit 1.0. Use the `route` object's `url` field instead.

## Markdown Behavior

- Headings are used to build `page.table_of_contents`.
- `bodyFingerprint` is derived from the body and participates in incremental
  rendering.
- When `site.autoSummary` is true, missing summaries can be generated from the
  rendered text.
- Media URLs in body HTML can be localized through `content.media`.
