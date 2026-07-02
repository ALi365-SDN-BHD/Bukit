# Content

Core 1.0 content starts at `content.sources`. Legacy single-provider fields are
not part of the contract.

Source anchors:

- `src/Bukit-Core/Bukit.Config/AppConfig.cs`
- `src/Bukit-Core/Bukit.Engine/ContentProviderFactory.cs`
- `src/Bukit-Core/Bukit.Content/Markdown/MarkdownFolderProvider.cs`

## Source Shape

```yaml
content:
  sources:
    - type: markdown
      name: pages
      mode: content
      collection: page
      markdown:
        dir: content/pages
    - type: markdown
      name: navigation
      mode: data
      markdown:
        dir: data/navigation
```

Supported `type` values are `markdown` and `notion`. Supported `mode` values
are `content` and `data`.

## Markdown Source

| Field | Purpose |
|---|---|
| `dir` | Source directory |
| `defaultType` | Type used when content omits one |
| `maxItems` | Positive item limit |
| `includePaths` | Explicit relative paths |
| `includeGlobs` | Glob filters |

## Notion Source

Notion sources are Core, but require provider configuration and `NOTION_TOKEN`
for real validation and build access.

```yaml
content:
  sources:
    - type: notion
      name: cms
      collection: post
      notion:
        databaseId: "${NOTION_DATABASE_ID}"
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: PublishAt
        sortDirection: descending
        propertyMap:
          Title: Title
          Slug: Slug
          PublishAt: PublishAt
```

## Content vs Data

`mode: content` creates pages and participates in routing. `mode: data` feeds
structured data into rendering models and does not create public pages by
itself.

## Front Matter

Common Markdown front matter:

```yaml
---
title: Hello
slug: hello
type: post
collection: post
date: 2026-06-14
summary: Short summary
tags: [release]
categories: [news]
draft: false
seoTitle: Hello - Example
canonical: https://example.com/blog/hello/
---
```

Route behavior should be solved through `site.collections`, route fields, and
permalink rules before template changes.

## Media Policy

`content.media` controls localization of remote media. Keep
`blockPrivateNetworks: true` unless the deployment environment has an explicit
trusted local-media workflow.

## Verification

```bash
bukit config check
bukit doctor
bukit build
```

