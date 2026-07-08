# Routing

Routing maps `ContentDocument` instances to `RouteInfo(url, outputPath,
template)`.

Source anchors:

- `src/Bukit-Core/Bukit.Routing/RouteGenerator.cs`
- `src/Bukit-Core/Bukit.Routing/RoutePathBuilder.cs`
- `src/Bukit-Core/Bukit.Routing/RouteSecurityValidator.cs`
- `src/Bukit-Core/Bukit.Engine/RouteInventoryValidator.cs`

## Collection Rules

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
      listTemplate: pages/list.html
```

Each collection needs a deterministic permalink. `template`, `listRoute`, and
`listTemplate` make list and page output explicit.

## Route Overrides

Content can override its public URL:

```yaml
---
title: Custom URL
route:
  url: /custom/
  template: pages/page.html
---
```

Output paths are derived from URLs. Manual output-path overrides are not part
of the Core 1.0 route contract.

## Output Path Encoding

`site.outputPathEncoding` supports:

| Value | Behavior |
|---|---|
| `none` | Preserve normalized path segments |
| `slug` | Slugify output path segments |
| `urlencode` | URL-encode output path segments |
| `sanitize` | Sanitize filesystem path segments |

The same encoding path applies to content pages and built-in derived pages.

## Conflict Detection

Route conflicts can happen between:

- two content pages;
- content pages and static HTML routes;
- content pages and built-in derived pages;
- two built-in derived pages.

`RouteInventoryValidator` validates content routes before rendering and final
routes before output is accepted.

`site.deriveConflictPolicy` applies to derived-page conflicts:

- `fail`: fail the build;
- `warn`: log and skip the conflicting derived page;
- `last-wins`: allow later derived output.

Content-page conflicts always fail.

## Verification

```bash
bukit doctor
bukit build
```

Doctor is the fastest route inventory check before a full build.

