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
