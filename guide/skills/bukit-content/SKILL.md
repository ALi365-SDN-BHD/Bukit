---
name: bukit-content
description: Use for Markdown, Notion, media localization, data mode, and source configuration.
---

# Bukit Content

Content is loaded from `content.sources[]`. `type` is `markdown` or `notion`;
`mode` is `content` or `data`.

Markdown uses `MarkdownFolderProvider`. Notion uses `NotionContentProvider` and
requires `NOTION_TOKEN` when provider secret validation is enabled. Media
rewrites use `content.media` and SSRF protections.
