---
name: bukit-content
description: Use when configuring Bukit Markdown content, content sources, source modes, collections, front matter, media localization, or multi-source content behavior.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "tests/Bukit.Engine.Tests/ContentProviderFactoryTests.cs"
  - "tests/Bukit.Config.Tests/ConfigLoaderTests.cs"
source_anchors:
  - "src/Bukit.Config/AppConfig.cs"
  - "src/Bukit.Engine/ContentProviderFactory.cs"
  - "src/Bukit.Content/Markdown/MarkdownFolderProvider.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit Content

Bukit Core 1.0 uses `content.sources` as the source list. Do not use legacy single-provider fields.

## Markdown Source

```yaml
content:
  sources:
    - type: markdown
      name: pages
      collection: page
      markdown:
        dir: content
        includeGlobs:
          - "**/*.md"
```

Useful fields:

| Field | Meaning |
|---|---|
| `type` | `markdown` or `notion` |
| `name` | Unique source name |
| `mode` | `content` or `data` |
| `collection` | Primary collection assignment |
| `addToCollections` | Additional collection names |
| `markdown.dir` | Markdown directory |
| `markdown.includePaths` | Explicit relative paths |
| `markdown.includeGlobs` | Glob filters |
| `markdown.maxItems` | Positive item limit |

## Front Matter Guidance

Common fields include `title`, `slug`, `type`, `date`, `summary`, `tags`, `categories`, `language`, `draft`, `seoTitle`, `seoDescription`, and `canonical`.

When route conflicts occur, inspect `slug`, `permalink`, collection config, and list routes before changing templates.

## Media

`content.media` controls image localization:

```yaml
content:
  media:
    downloadToLocal: true
    downloadDir: assets/uploads
    urlBase: /assets/uploads
    blockPrivateNetworks: true
```

Private-network blocking should stay enabled unless the site owner has a controlled local-media workflow.

## Verification

```bash
bukit doctor
bukit build
```
