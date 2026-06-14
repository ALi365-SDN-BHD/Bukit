# Content System (Markdown / Notion / sources)

This document is the developer reference for the content system, covering uniform models, provider implementations, and field normalization rules.

Implementation: `src/Bukit.Content/*`, `src/Bukit.Engine.Abstractions/ContentDocument.cs`

## Unified Model: ContentDocument

All content sources ultimately land on `ContentDocument`:
- `Id`: Unique identifier
- `Title`: Content title
- `Slug`: URL slug
- `PublishAt`: Publish date
- `Language`: Content language
- `Record`: Canonical semantic record (identity/presentation/classification/lifecycle/ownership/media/relation info)
- `CustomFields`: Custom fields for template consumption (`page.fields.<key>`)
- `Route`: Route policy (`url`, `outputPath`, `template`, `permalink`, `listGroup`)
- `ContentHtml`: HTML body (may be null with BodyKey)
- `BodyKey`: Deferred body lookup key

## Record vs Fields Division

- **Record/Policy**: Engine decisions — `type`, `language`, `draft`, `route`, `sourceMode`, `tags`, `categories`, `collection`, `i18nKey`
- **Fields**: Template consumption — SEO fields, business fields, images, reading time, etc.

## Markdown Provider

`MarkdownFolderProvider.cs`: Recursively reads `*.md` files, parses YAML front matter, extracts body.

Front matter normalization:
- Reserved keys map to canonical record/policy fields: `title`, `slug`, `type`, `language`, `draft`, `publishAt`, `tags`, `categories`, `summary`, `collection`, `route`, `url`, `outputPath`, `template`, `permalink`, `listGroup`, `sourceMode`
- Everything else goes to Fields
- Field names are case-insensitive

## Notion Provider

`NotionContentProvider.cs`: Fetches pages from Notion database, renders blocks, maps properties.

Property mapping:
- `Published` (checkbox) → filter
- `Title` → `ContentDocument.Title`
- `Slug` → `ContentDocument.Slug`
- `Type` → `ContentDocument.Record.Identity.ContentType`
- `PublishAt` → `ContentDocument.PublishAt`
- `language` → `ContentDocument.Record.Presentation.Language`
- `i18n_key` → `ContentDocument.Record.Identity.CanonicalUrlKey`
- `tags`/`categories` → `ContentDocument.Record.Classification.Sections`（`categories` 兼容映射）及 `ContentDocument.Record.Classification.Tags`
- `Collection` → `ContentDocument.Record.Classification.Collection`
- Custom fields → `page.fields.*` (controlled by `fieldPolicy`)

Field normalization: `SEO Title` → `seo_title`, etc.

## Composite Provider (sources mode)

`CompositeContentProvider.cs`: Concurrently aggregates multiple sources. Each source item gets a prefix `<sourceKey>:<sourceId>`.

`mode: content` items generate routes; `mode: data` items are injected into `site.modules`.

## Image Localization (`content.media`)

Unified across all providers: downloads remote images locally, replaces URLs.
