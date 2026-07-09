# Content Pipeline

`ContentPipeline` builds source providers from `content.sources[]`, loads raw
documents, applies body stores, normalizes documents, validates content schema,
and returns the content graph used by the engine.

## Providers

| Provider | Implementation | Notes |
|---|---|---|
| Markdown | `MarkdownFolderProvider` | Reads files, parses front matter, renders body lazily. |
| Notion | `NotionContentProvider` | Queries Notion database pages and can render blocks. |
| Composite | `CompositeContentProvider` | Combines multiple sources and preserves body stores per source. |

## Body Stores

Body stores avoid rendering every body up front. Markdown uses
`MarkdownBodyStore`; Notion uses `NotionBodyStore`; multi-source builds use
`CompositeContentBodyStore`; media localization can wrap stores with
`LocalizedContentBodyStore`.

## Media

`ContentImageRewritePipeline` scans HTML image references, downloads remote
media through `ImageAssetLocalizer`, applies SSRF protections, and writes an
index through `MediaIndexManager`.

## Schema

`content.modelSchema` can require canonical fields, custom fields, entity
mappings, relation mappings, media metadata, provenance, review fields, and
relation targets. Schema validation reports are carried into build and publish
outputs.
