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

The canonical publish date is resolved deterministically: the mapped
`PublishAt` value wins, otherwise Bukit uses the Notion Page object's top-level
`created_time`. If neither value is a valid ISO 8601 timestamp, loading fails
with the page id and both field names. Top-level `last_edited_time` remains an
updated-at/cache value and is never used as the publish date.

`content.sources[].notion.propertyMap.Type` and
`content.sources[].notion.propertyMap.Collection` project different canonical
fields. With Notion values `Kind = article` and `Section = news`, the document
has `type: article` and `collection: news`; neither value is inferred from the
other. Missing content type defaults to `page`, but missing collection after
projection causes the build to fail.

Canonical `Collection` projection happens before the ordinary
`fieldPolicy.mode: whitelist` filtering, so the mapped Collection property does
not need to be repeated in the normal field allowlist. It must resolve to one
scalar string from `rich_text`, `select`, or `status`. `title`, `url`, `email`,
`phone_number`, `formula`, and multi-value properties are rejected with a
`ContentException` that identifies the property type and lists the allowed
types; values are never converted with `ToString()`.

A source-level `collection` can provide ownership instead and overrides the
mapped item collection without changing its mapped type.

## Rendering

Notion blocks are rendered by `NotionBlockRendererRegistry`. Built-in renderers
cover headings, paragraphs, lists, tables, toggles, callouts, media, code,
embeds, equations, synced blocks, and unsupported no-op block types.
