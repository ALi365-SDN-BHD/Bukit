---
name: bukit-templating
description: Use for Scriban templates, page/list models, layout directives, and render failures.
---

# Bukit Templating

Templates use Scriban. Core model objects are `site`, `page`, `pages`, `items`,
`pagination`, `collection`, `taxonomy`, and `filter`.

`SiteModel`, `PageInfo`, and `ListPageModel` are the source of truth. Layout
directives are parsed before rendering and `{{ content }}` receives child body
content.

Named data sources remain available as arrays under `site.data.<source>`.
Sources configured with `dataIndex` additionally expose scalar values through
`site.data_index.<source>.<scope>.<key>`.
