---
name: bukit-content
description: Use when working with Bukit Markdown, Notion, media localization, data mode, or source configuration.
---

# Bukit Content

## Quick Contract

Content is loaded from `content.sources[]`. Source `type` selects the
`markdown` or `notion` provider and is unrelated to document metadata `type`.

| Mode | Type | Collection |
|---|---|---|
| `content` | Explicit or defaults to `page`. | Required after provider/source projection. |
| `data` | Explicit or defaults to `module`. | Optional; module is not routed or collection-indexed. |

Type and collection never derive from each other. Collection can come from
`content.sources[].collection`, Markdown front matter, or Notion
`propertyMap.Collection`. A source collection overrides item collection but
never changes item type. `markdown.defaultType` fills type only.

## Markdown

`defaultType: article` without any collection does not build in content mode.
Use an explicit source or front matter collection, for example
`type: article, collection: news`. `addToCollections` creates explicit cloned
documents/routes for its target collections; it is not implicit membership.

## Notion

Map the canonical fields independently:

```yaml
propertyMap:
  Type: Kind
  Collection: Section
```

`Kind = article` and `Section = news` project `type: article` and
`collection: news`. Canonical Collection projection occurs before ordinary
field whitelist filtering, so it is not accidentally removed by the normal
allowlist. Collection accepts one string from `title`, `rich_text`, `url`,
`email`, `phone_number`, `formula`, `select`, or `status`; multi-value
properties fail with an explicit content error.

## Common Mistakes

- Omitting collection because content type is present.
- Mapping Collection to a multi-value Notion property.

Markdown uses `MarkdownFolderProvider`. Notion uses `NotionContentProvider` and
requires `NOTION_TOKEN` when provider secret validation is enabled. Media
rewrites use `content.media` and SSRF protections.

For key-value configuration records, `dataIndex` is available only on a named
`mode: data` source. It exposes scalar values under
`site.data_index.<source>.<scope>.<key>` while preserving the raw records under
`site.data.<source>`. Treat indexed values as public static-site data.
