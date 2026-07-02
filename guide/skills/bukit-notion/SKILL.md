---
name: bukit-notion
description: Use when configuring or troubleshooting Bukit Notion content sources, property mapping, filters, caching, block rendering, relation handling, or NOTION_TOKEN validation.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "tests/Bukit.Content.Tests/NotionPropertyParserTests.cs"
  - "tests/Bukit.Engine.Tests/NotionSchemaDrivenMappingTests.cs"
source_anchors:
  - "src/Bukit-Core/Bukit.Config/ProviderValidators.cs"
  - "src/Bukit-Core/Bukit.Content/Notion/NotionContentProvider.cs"
  - "src/Bukit-Core/Bukit.Content/Notion/NotionPropertyParser.cs"
  - "src/Bukit-Core/Bukit.Content/Notion/NotionBlockRendererRegistry.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit Notion

Notion is a Core content source, not a separate CLI command. Configure it under `content.sources`.

## Minimal Source

```yaml
content:
  sources:
    - type: notion
      name: cms
      collection: post
      notion:
        databaseId: "${NOTION_DATABASE_ID}"
        pageSize: 50
        filterProperty: Published
        filterType: checkbox_true
```

`NOTION_TOKEN` must come from the environment for validation and builds.

## Property Map

```yaml
content:
  sources:
    - type: notion
      notion:
        databaseId: "${NOTION_DATABASE_ID}"
        propertyMap:
          Title: Title
          Slug: Slug
          Type: Type
          PublishAt: PublishAt
          Language: Language
          I18nKey: I18nKey
          Summary: Summary
          Collection: Collection
          SeoTitle: SeoTitle
          SeoDescription: SeoDescription
          SeoImage: SeoImage
          Canonical: Canonical
```

## Filters and Cache

Allowed `filterType` values:

`checkbox_true`, `checkbox_false`, `select_equals`, `status_equals`, `rich_text_equals`, `none`.

Allowed `cacheMode` values:

`off`, `readwrite`, `readonly`.

`pageSize` must be between 1 and 100. `maxItems`, `renderConcurrency`, and `maxRps` must be positive when set. `maxRetries` must be non-negative.

## Field Policy

```yaml
fieldPolicy:
  mode: whitelist
  allowed:
    - title
    - slug
    - tags
    - published
```

Allowed modes are `whitelist` and `all`.

## Verification

```bash
bukit config check
bukit doctor
bukit build
```
