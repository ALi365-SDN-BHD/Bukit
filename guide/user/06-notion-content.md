# 06 Notion Content

The Notion provider loads database pages through the Notion API, maps selected
properties to Core fields, optionally renders page blocks, and can cache block
HTML.

## Source Config

```yaml
content:
  sources:
    - type: notion
      mode: content
      collection: post
      notion:
        databaseId: "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: PublishAt
        sortDirection: descending
        propertyMap:
          Title: Name
          Slug: Slug
          PublishAt: PublishAt
          Language: Language
```

`NOTION_TOKEN` must be provided by the environment when provider secret
validation is enabled.

## Important Options

| Field | Purpose |
|---|---|
| `pageSize` | Notion query page size, 1 to 100. |
| `maxItems` | Optional cap for loaded pages. |
| `renderContent` | Enables or disables block body rendering. |
| `renderConcurrency` | Parallel block rendering limit. |
| `maxRps`, `maxRetries` | API throttling and retry controls. |
| `filterType` | `checkbox_true`, `checkbox_false`, `select_equals`, `status_equals`, `rich_text_equals`, or `none`. |
| `filterValue` | Required for select, status, and rich text filters. |
| `cacheMode` | `off`, `readwrite`, or `readonly`. |
| `content.sources[].notion.fieldPolicy.mode` | `whitelist` or `all`. |
| `propertyMap` | Uses Core field keys `Title`, `Slug`, `Type`, `PublishAt`, `Language`, `I18nKey`, `Summary`, `Collection`, `SeoTitle`, `SeoDescription`, `SeoImage`, and `Canonical`. |

## Rendering

Notion blocks are rendered by `NotionBlockRendererRegistry`. Built-in renderers
cover headings, paragraphs, lists, tables, toggles, callouts, media, code,
embeds, equations, synced blocks, and unsupported no-op block types.
