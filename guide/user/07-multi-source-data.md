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

## Scalar Data Index

Use `dataIndex` when each data record represents one public configuration
value. The original record array remains available through `site.data`.

```yaml
content:
  sources:
    - type: notion
      name: settings
      mode: data
      notion:
        databaseId: "<database-id>"
        renderContent: false
        fieldPolicy:
          mode: all
      dataIndex:
        scopeField: scope
        keyField: key
        valueField: value
        valueTypeField: value_type
        requiredKeys:
          - footer.site_name
          - contact.email
```

Templates read scalar values directly:

```scriban
{{ site.data_index.settings.footer.site_name }}
{{ site.data_index.settings.contact.email }}
```

Each record must have a safe scope and key plus a supported value type:
`text`, `multiline`, `email`, `phone`, or `url`. Duplicate keys and missing or
empty required values fail the build. Email values must be valid. URL values
must be HTTP(S) absolute URLs or root-relative paths. Store only public display
data in a static-site data index; secrets are published into generated HTML if
a template renders them.

## Collection Assignment

| Field | Behavior |
|---|---|
| `collection` | Primary collection for route matching. |
| `addToCollections` | Additional collection memberships for indexing and grouping. |
| `name` | Data source key; must be unique when set. |

`CollectionsValidator` ensures configured source collections exist in
`site.collections`.
