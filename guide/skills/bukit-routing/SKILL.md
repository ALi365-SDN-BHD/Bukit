---
name: bukit-routing
description: Use when configuring Bukit URLs, collection permalinks, list routes, pagination URLs, route security, output paths, or route conflict diagnostics.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "tests/Bukit.Routing.Tests/RoutePathBuilderTests.cs"
  - "tests/Bukit.Engine.Tests/RouteGeneratorTests.cs"
source_anchors:
  - "src/Bukit.Routing/RoutePathBuilder.cs"
  - "src/Bukit.Routing/RouteGenerator.cs"
  - "src/Bukit.Routing/RouteSecurityValidator.cs"
  - "src/Bukit.Engine/RouteInventoryValidator.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit Routing

Routing is controlled by content front matter plus `site.collections` and related list-page settings.

## Collection Permalinks

```yaml
site:
  collections:
    post:
      permalink: "/blog/{slug}/"
      template: pages/post.html
      listRoute: "/blog/"
      listTemplate: pages/list.html
```

Common tokens include `{slug}`, date parts, language, and collection-aware fields supported by the route builder.

## Pagination

```yaml
site:
  collections:
    post:
      pagination:
        enabled: true
        pageSize: 10
        urlPattern: "page/:num/"
        firstPageUsesListRoute: true
```

## Conflict Handling

Route conflicts usually come from duplicate slugs, overlapping list routes, unsafe output paths, or permalink patterns that collapse multiple documents to the same URL.

Use:

```bash
bukit doctor
bukit build
```

Doctor performs route inventory validation before a build artifact is published.

## Security Rules

- Do not create routes that escape the output root.
- Do not use path traversal in slugs or output paths.
- Prefer clean trailing-slash URLs for pages and list routes.
- Keep language prefixes and collection paths deterministic.
