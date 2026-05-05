# Content System (Markdown / Notion / sources)

This document is the developer reference for the content system, covering uniform models, provider implementations, and field normalization rules.

Implementation: `src/Bukit.Content/*`, `src/Bukit.Engine.Abstractions/ContentItem.cs`

## Unified Model: ContentItem

All content sources ultimately land on `ContentItem`:
- `Id`: Unique identifier
- `Title`: Content title
- `Slug`: URL slug
- `PublishAt`: Publish date
- `Language`: Content language
- `Meta`: Metadata affecting engine decisions (type, language, draft, route, tags, categories, etc.)
- `Fields`: Custom fields for template consumption (`page.fields.<key>`)
- `ContentHtml`: HTML body (may be null with BodyKey)
- `BodyKey`: Deferred body lookup key

## Meta vs Fields Division

- **Meta**: Engine decisions — `type`, `language`, `draft`, `route`, `sourceMode`, `tags`, `categories`, `collection`, `i18nKey`
- **Fields**: Template consumption — SEO fields, business fields, images, reading time, etc.

## Markdown Provider

`MarkdownFolderProvider.cs`: Recursively reads `*.md` files, parses YAML front matter, extracts body.

Front matter normalization:
- Reserved keys go to Meta: `title`, `slug`, `type`, `language`, `draft`, `publishAt`, `tags`, `categories`, `summary`, `collection`
- Everything else goes to Fields
- Field names are case-insensitive

## Notion Provider

`NotionContentProvider.cs`: Fetches pages from Notion database, renders blocks, maps properties.

Property mapping:
- `Published` (checkbox) → filter
- `Title` → `ContentItem.Title`
- `Slug` → `ContentItem.Slug`
- `Type` → `meta.type`
- `PublishAt` → `ContentItem.PublishAt`
- `language` → `meta.language`
- `i18n_key` → `meta.i18nKey`
- `tags`/`categories` → `meta.tags`/`meta.categories`
- `Collection` → `meta.collection`
- Custom fields → `page.fields.*` (controlled by `fieldPolicy`)

Field normalization: `SEO Title` → `seo_title`, etc.

## Composite Provider (sources mode)

`CompositeContentProvider.cs`: Concurrently aggregates multiple sources. Each source item gets a prefix `<sourceKey>:<sourceId>`.

`mode: content` items generate routes; `mode: data` items are injected into `site.modules`.

## Image Localization (`content.media`)

Unified across all providers: downloads remote images locally, replaces URLs.
