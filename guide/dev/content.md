# Content Pipeline

`ContentPipeline` builds source providers from `content.sources[]`, loads raw
documents, applies body stores, normalizes documents, validates content schema,
and returns the content graph used by the engine.

## Classification Contract

Provider projection establishes two independent fields. For `mode: content`,
missing type defaults to `page`, while collection must be non-empty after item
metadata and source collection are applied. A source collection wins over item
collection but never rewrites type. `ContentCollectionContractValidator` runs
after that projection and before normalization, so a full route override or a
type permalink cannot bypass missing collection.

For `mode: data`, missing type defaults to `module`, collection may remain
empty, and the module does not enter page routing or collection indexes. There
is no `type -> collection` or `collection -> type` derivation.

Collection inputs are Markdown front matter, Notion
`content.sources[].notion.propertyMap.Collection`, or
`content.sources[].collection`.
`markdown.defaultType` affects type only. The source configuration `type`
selects the provider (`markdown`/`notion`) and is unrelated to document type.

## Providers

| Provider | Implementation | Notes |
|---|---|---|
| Markdown | `MarkdownFolderProvider` | Reads files, parses front matter, renders body lazily. |
| Notion | `NotionContentProvider` | Queries Notion database pages and can render blocks. |
| Composite | `CompositeContentProvider` | Combines multiple sources and preserves body stores per source. |

Notion canonical Collection projection precedes ordinary whitelist filtering.
It accepts one string from `rich_text`, `select`, or `status`. `title`, `url`,
`email`, `phone_number`, `formula`, and multi-value Collection properties throw
a `ContentException` that identifies the property type and allowed types.
Composite `addToCollections` handling creates a clone with an explicit target
collection for every extra route.

## Body Stores

Body stores avoid rendering every body up front. Markdown uses
`MarkdownBodyStore`; Notion uses `NotionBodyStore`; multi-source builds use
`CompositeContentBodyStore`; media localization can wrap stores with
`LocalizedContentBodyStore`.

## Media

`ContentImageRewritePipeline` scans HTML image references, downloads remote
media through `ImageAssetLocalizer`, applies SSRF protections, and writes an
index through `MediaIndexManager`.

Each rewrite operation creates a download-level gate shared by documents, HTML,
and media fields. `LocalizedContentBodyStore` uses one lazy store-level gate for
concurrent reads. `content.media.maxConcurrency` therefore limits active
localizer calls in those scopes; it is not the document-transform count or a
process-wide network budget. Cancellation and failure paths release only permits
that were acquired.

Default recursive Markdown and media-cache enumeration does not descend through
directory symlinks or reparse points. This is independent from supported copy
paths that explicitly implement `build.followSymlinks=true`.

## Schema

`content.modelSchema` can require canonical fields, custom fields, entity
mappings, relation mappings, media metadata, provenance, review fields, and
relation targets. Schema validation reports are carried into build and publish
outputs.

`fieldScopes.<collection>` and collection `schemaFailMode` are resolved only
from the real collection. Lists, pagination, filtered lists, archives, feeds,
and sitemap output policy also group by collection. SEO article classification
uses the real type; search emits type, contentType, and collection separately.
