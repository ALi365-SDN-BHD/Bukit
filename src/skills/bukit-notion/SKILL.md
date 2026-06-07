---
name: bukit-notion
description: Use when using bukit with Notion as a content source, troubleshooting Notion content fetch failures or incomplete data in bukit, understanding property mapping rules, or dealing with bukit image localization issues

status: stable
since: "v3.0.0"
verified_by:
  - "src/Bukit.Engine/ContentProviderFactory.cs"
  - "src/Bukit.Shared/Notion/NotionBlockTypes.cs"
source_anchors:
  - "src/Bukit.Engine/ContentProviderFactory.cs"
  - "src/Bukit.Shared/Notion/NotionApiUrls.cs"
guide_chapters:
  - "guide/user/06-notion-content.md"
---

# Notion Content Source

## Overview

Bukit converts Notion database pages into raw content documents and then normalizes them into canonical `ContentDocument` records, supporting HTML rendering for 26 block types and field mapping for 18 property types.

**REQUIRED BACKGROUND:** Notion-related config is in site.yaml's `content.notion` and `content.media` nodes — you must understand bukit-config first.
**REQUIRED SUB-SKILL:** Verify Notion connectivity with `bukit doctor`, build with `bukit build`. CLI commands reference bukit-cli-reference.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "Notion 内容源"、"Notion 数据拉不到"、"Notion API 配置"、"Notion 属性映射" |
| English | "Notion content source", "Notion data not fetching", "Notion API setup", "Notion property mapping" |
| Bahasa Melayu | "sumber kandungan Notion", "data Notion tidak ditarik", "persediaan API Notion", "pemetaan sifat Notion" |

## Prerequisites

### 1. Create a Notion Integration

1. Visit [Notion Integrations](https://www.notion.so/my-integrations)
2. Create an Internal Integration and get the API Key (= `NOTION_TOKEN`)
3. Set the environment variable: `NOTION_TOKEN=secret_xxxx`

### 2. Get the Database ID

Extract it from the Notion database page URL. For example:
- `https://www.notion.so/workspace/abc123?v=...` → database ID is `abc123`

### 3. Authorize the Integration to Access the Database

In your Notion database, click `...` in the top-right → Connections → add the Integration you just created.

### Verify

```bash
bukit doctor
```
This checks Notion API connectivity and database reachability.

## Notion Property Mapping Rules

| Notion Type | Mapped Type | Value Source | Template Access |
|------------|---------|--------|---------|
| `title` | `text` | Plain text extraction | `page.fields.Name.value` |
| `rich_text` | `text` | Plain text extraction | `page.fields.Description.value` |
| `url` | `text` | URL string | `page.fields.Link.value` |
| `email` | `text` | Email string | `page.fields.Email.value` |
| `phone_number` | `text` | Phone number | — |
| `number` | `number` | Numeric value | `page.fields.Price.value` |
| `checkbox` | `bool` | true/false | `page.fields.Published.value` |
| `date` | `date` | DateTimeOffset | `page.fields.Date.value` |
| `created_time` | `date` or `text` | Creation time | — |
| `last_edited_time` | `date` or `text` | Last edited time | — |
| `created_by` | `text` | Username or ID | — |
| `last_edited_by` | `text` | Username or ID | — |
| `select` | `text` | Selected item name | `page.fields.Category.value` |
| `status` | `text` | Status name | `page.fields.Status.value` |
| `multi_select` | `list` | Array of selected item names | `for tag in page.fields.Tags.value` |
| `people` | `list` | Array of usernames | — |
| `files` | `list` | Array of file URLs | — |
| `relation` | `list` | Array of related page IDs | — |
| `formula` | Dynamic | Depends on formula result type | — |
| `rollup` | Dynamic | Depends on rollup result type | — |

**Field filtering**: When `fieldPolicy.mode: whitelist`, properties outside the `fieldPolicy.allowed` list will not appear in `page.fields`.

## Block Rendering Support

**Property name mapping** (`propertyMap`): When your Notion database uses custom property names, map them to Bukit internal fields via `content.notion.propertyMap`. Supports 12 fields: `Title`, `Slug`, `Type`, `PublishAt`, `Language`, `I18nKey`, `Summary`, `Collection`, `SeoTitle`, `SeoDescription`, `SeoImage`, `Canonical`. Example: `propertyMap: { Title: "Page Name", Slug: "URL Slug" }`.

**Filter value** (`filterValue`): Required when `filterType` is `select_equals`, `status_equals`, or `rich_text_equals`. Specifies the value to match against the filter property.


26 Notion block types are automatically rendered to HTML:

| Block Type | Rendered As | CSS Class |
|--------|--------|--------|
| Paragraph | `<p>` | — |
| Heading 1/2/3 | `<h1>`/`<h2>`/`<h3>` | — |
| Quote | `<blockquote>` | — |
| Code | `<pre><code>` + language annotation | — |
| Divider | `<hr>` | — |
| Image | `<figure><img><figcaption>` | — |
| Callout | `<div class="callout">` + icon | `.callout`, `.callout-icon`, `.callout-content` |
| To-do | `<div class="to-do"><input type="checkbox">` | `.to-do` |
| Toggle | `<details><summary>` | — |
| Bookmark | `<a class="bookmark">` | `a.bookmark` |
| Embed | `<div class="video-embed"><iframe>` | `.video-embed` |
| Equation | `<div class="math-block">` | `.math-block`, `.math-inline` |
| Table | `<table>` | — |
| Column layout | `<div class="notion-columns">` | `.notion-columns`, `.notion-column` |
| Audio | `<audio>` | — |
| Video | `<video>` | — |
| File | Download link | `.notion-file` |
| PDF | PDF viewer | `.notion-pdf` |
| Link preview | Link card | — |
| Link to page | Internal link | `.notion-child-page` |
| Synced block | Inline rendering | — |
| Table of contents | Placeholder | — |
| Child entity | Child page reference | `.notion-child-database` |

The default theme CSS (generated by `bukit init`) already includes styles for all these CSS classes.

## Relation Handling

`relation` type fields are parsed into an array of related page IDs. Bukit automatically resolves relations and provides reference information in `page.fields.RelatedField.value`.

## Image Localization

Bukit automatically downloads remote images from Notion to local storage and rewrites URLs in HTML:

```yaml
content:
  media:
    downloadToLocal: true        # Enable downloading
    downloadDir: assets/uploads  # Download directory
    urlBase: /assets/uploads     # URL prefix after replacement in HTML
    fieldKeys: [cover, image, thumbnail, og_image, icon]  # Fields to process
    maxConcurrency: 4            # Download concurrency
    maxFileSizeBytes: 52428800   # Max file size 50MB
    blockPrivateNetworks: true   # Block internal network addresses
```

## Common Issues

| Issue | Cause | Solution |
|------|------|------|
| Content fetch returns empty | Database not authorized for Integration | Add Integration in Notion database Connections |
| `NOTION_TOKEN is required` | Environment variable not set | `export NOTION_TOKEN=secret_xxx` or on Windows `$env:NOTION_TOKEN = "secret_xxx"` |
| 401 Unauthorized | Token invalid or expired | Regenerate Integration token |
| 404 Not Found | Wrong databaseId | Re-extract database ID from Notion URL |
| Some properties not showing | `fieldPolicy.mode: whitelist` restriction | Add desired field names to `fieldPolicy.allowed` |
| Images not downloaded | `downloadToLocal: false` | Set to true; check that `fieldKeys` includes the field name |
| Image 404 | Download failed or path misconfigured | Check `downloadDir` and `urlBase` config |
| API rate limit (429) | Too many requests | Lower `renderConcurrency` and `maxRps` |
| Build is slow | Too many Notion API calls | Enable `cacheMode: readwrite` for caching |
| Related page content missing | Relation resolution incomplete | Ensure the related database is also authorized for the Integration |
| filterProperty filter not working | Property name mismatch or wrong type | Confirm `filterProperty` is a checkbox type and `filterType: checkbox_true` |
