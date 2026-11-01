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
| `propertyMap` | Uses Core field keys `Title`, `Slug`, `Type`, `PublishAt`, `Language`, `I18nKey`, `Summary`, `Collection`, `SeoTitle`, `SeoDescription`, `SeoImage`, `Canonical`, `OriginalUrl`, `References`, `EntitiesJson`, `Cover`, `CoverAlt`, and `CoverCaption`. |

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

## Canonical Source, Entity And Cover Mapping

Each mapping value names a Notion property. These mappings read raw properties
directly and project canonical inputs independently of ordinary field whitelist
filtering; the mapped properties do not need to be repeated in the allowlist:

| Mapping key | Accepted Notion property types | Projected value |
|---|---|---|
| `OriginalUrl` | `url` | `original_url` source URL. |
| `References` | `multi_select`, `rich_text` | `references` list; one rich-text value becomes one list item. |
| `EntitiesJson` | `rich_text` | `entities` parsed from a JSON array of objects. |
| `Cover` | `rich_text`, `url`, `files` | `cover`; a file list uses its first value. |
| `CoverAlt` | `rich_text` | `cover_alt` text. |
| `CoverCaption` | `rich_text` | `cover_caption` text. |

Every `EntitiesJson` object requires non-empty string fields `type`, `name`,
and `description`, for example:

```json
[{"type":"Organization","name":"Example News","description":"Editorial publisher"}]
```

Malformed JSON, a non-array value, a non-object item, or a missing/empty required
string fails content loading. A mapped property with an unsupported Notion
type also fails with a diagnostic identifying the page and property.

## Rendering

Notion blocks are rendered by `NotionBlockRendererRegistry`. Built-in renderers
cover headings, paragraphs, lists, tables, toggles, callouts, media, code,
embeds, equations, synced blocks, and unsupported no-op block types.

## Rate Limiting, Concurrency And Lifecycle

One source load owns one Notion client and one body concurrency gate. The
database query, relation resolution, summary prefetch, cross-source relation
lookup, and lazy body reads for that load share the same rate-limit state, so
`maxRps` and retry behaviour cover the whole load instead of resetting per page.
`renderConcurrency` bounds active block-body renders for the same load.

Each load result has independent resources. Multiple sources, even with the same
token, each have their own budget; allocate `maxRps` per source when several
sources share a token. Disposing a body store closes new reads, waits for
already accepted renders, cancels and observes background work, then releases
the client and gate.

A cancelled waiter does not cancel a shared body render for other waiters. If a
shared render fails or is cancelled after its last waiter left, the failure is
removed so the next read starts a new attempt instead of replaying the old
failure. Successfully prefetched empty or whitespace-only bodies are reused with
`cacheMode: off`, so they are not requested twice.

## Caching And Incremental Rendering

`readwrite` creates the cache directory; `readonly` reads an existing cache and
fails on a miss; `off` does not read or create the cache. The effective
`renderContent` mode is part of the stable incremental fingerprint. Switching it
in either direction invalidates the affected pages once; after the switch is
stable, the next build reuses them again.

A `readwrite` cache directory must not equal, contain, or sit inside a configured
Markdown content source (or the reverse). Create writable Notion caches in a
dedicated directory such as `.cache/notion`.
