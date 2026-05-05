# Routing System (Collection Primary Paths and Compatibility Rules)

Maps `ContentItem` to `RouteInfo(url, outputPath, template)`.

Implementation: `src/Bukit.Routing/RouteGenerator.cs`

## Collection-Driven Routing (Primary Model)

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
      listRoute: /pages/
```

Each collection requires `permalink` (must contain `{slug}`) and `template`.

## Permalink Patterns (Compatibility)

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
```

Placeholders: `{slug}`, `{year}`, `{month}`, `{day}`, `{type}`

Priority: Route Override > Collection Rules > Permalink Patterns > Default routing

## Route Override

When meta contains all three of `url`, `outputPath`, `template`, default routing is overridden. Notion `url`/`outputPath`/`template` fields are promoted to meta.

## outputPath Encoding: `none`/`slug`/`urlencode`/`sanitize`

## Fixed Aggregation Pages: The engine also generates `/`, `/blog/`, `/pages/` regardless of content.
