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
      notion:
        databaseId: "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: PublishAt
        sortDirection: descending
        propertyMap:
          Title: Name
          Slug: Slug
          Type: Kind
          Collection: Section
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

`content.sources[].notion.propertyMap.Type` and
`content.sources[].notion.propertyMap.Collection` project different canonical
fields. With Notion values `Kind = article` and `Section = news`, the document
has `type: article` and `collection: news`; neither value is inferred from the
other. Missing content type defaults to `page`, but missing collection after
projection causes the build to fail.

Canonical `Collection` projection happens before the ordinary
`fieldPolicy.mode: whitelist` filtering, so the mapped Collection property does
not need to be repeated in the normal field allowlist. It must resolve to one
scalar string: a `title`, `rich_text`, `url`, `email`, `phone_number`, or
`formula` text-like value, or a `select`/`status` name. Multi-value properties
are rejected with a content error instead of being converted with `ToString()`.

A source-level `collection` can provide ownership instead and overrides the
mapped item collection without changing its mapped type.

## Rendering

Notion blocks are rendered by `NotionBlockRendererRegistry`. Built-in renderers
cover headings, paragraphs, lists, tables, toggles, callouts, media, code,
embeds, equations, synced blocks, and unsupported no-op block types.
