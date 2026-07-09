# 07 Multi-Source Data

Bukit 1.0 requires `content.sources[]`. Each source can produce pages
(`mode: content`) or structured data (`mode: data`).

## Content Source

```yaml
content:
  sources:
    - type: markdown
      mode: content
      collection: post
      markdown:
        dir: content/posts
```

Content sources are routed and rendered as pages unless a document is marked as
data by the source.

## Data Source

```yaml
content:
  sources:
    - type: markdown
      name: faq
      mode: data
      markdown:
        dir: data/faq
```

Data documents are excluded from `RoutePipeline` page routing. They are exposed
through `site.modules` and `site.data`, then can be rendered in templates.

## Collection Assignment

| Field | Behavior |
|---|---|
| `collection` | Primary collection for route matching. |
| `addToCollections` | Additional collection memberships for indexing and grouping. |
| `name` | Data source key; must be unique when set. |

`CollectionsValidator` ensures configured source collections exist in
`site.collections`.
