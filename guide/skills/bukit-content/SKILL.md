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

For key-value configuration records, `dataIndex` is available only on a named
`mode: data` source. It exposes scalar values under
`site.data_index.<source>.<scope>.<key>` while preserving the raw records under
`site.data.<source>`. Treat indexed values as public static-site data.
