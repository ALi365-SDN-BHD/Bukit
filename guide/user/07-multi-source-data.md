# 07 Multi-Source Data

Bukit 1.0 requires `content.sources[]`. Each source can produce pages
(`mode: content`) or structured data (`mode: data`).

## Content Source

```yaml
content:
  sources:
    - type: markdown
      mode: content
      collection: news
      markdown:
        dir: content/news
        defaultType: article
```

This source produces documents with `type: article` and `collection: news`.
Content requires a non-empty collection after source/item projection. Type
defaults to `page` when absent and never supplies collection ownership.

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
through `site.modules` and `site.data`, then can be rendered in templates. Data
does not require collection, defaults a missing type to `module`, and is not
indexed as a collection page.

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

Templates read values directly:

```scriban
{{ site.data_index.settings.footer.site_name }}
{{ site.data_index.settings.contact.email }}
```

Each record must have a safe scope and key plus a supported value type:
`text`, `multiline`, `email`, `phone`, `url`, `object`, or `object[]`.
Duplicate keys and missing or empty required values fail the build. Email
values must be valid. URL values must be HTTP(S) absolute URLs or root-relative
paths. A text type (`text`, `multiline`, `email`, `phone`, `url`) whose value is
a mapping or a list fails the build, naming the source, content id and
`scope.key`; use `object` or `object[]` for structured values. Store only public
display data in a static-site data index; secrets are published into generated
HTML if a template renders them.

### Object values

`object` holds one mapping and `object[]` a list of mappings. In Markdown, write
the value as YAML:

```markdown
---
scope: trip
key: info
value_type: object
value:
  title: Penang food trail
  days: 5
  stops:
    - name: George Town
    - name: Balik Pulau
---
```

A Notion `value` property is plain text, so write the value there as JSON:
`{"title":"Penang food trail","days":5,"stops":[{"name":"George Town"}]}`.
Templates read nested fields with dots:

```scriban
{{ site.data_index.settings.trip.info.title }}
{{ for stop in site.data_index.settings.trip.info.stops }}{{ stop.name }}{{ end }}
```

Rules for object values:

- **Every scalar is text.** Markdown front matter already gives every scalar as
  a string, so JSON numbers and booleans from Notion are turned into text too:
  `5` becomes `"5"`, `12.50` stays `"12.50"` (the source digits are kept, never
  rounded), `true` becomes `"true"`. The same value therefore builds the same
  model whichever source it comes from. Convert in the template where a number
  is needed (`info.days | string.to_int`), or declare nested `number` and
  `boolean` fields in a [mini-program dataset](25-miniprogram-data.md#object-fields).
  A YAML or JSON `null` inside the value stays null.
- Nested strings are plain text; templates escape them as usual.
- Keys keep their spelling and are ordered ordinally, so reordering keys in the
  source changes neither the output nor the incremental build hash.
- Values may nest at most 8 levels, where every mapping and every list counts as
  one level. `NaN`, infinity and JSON numbers outside the double range fail.
- Invalid JSON, duplicate JSON keys, a list given to `object`, a mapping given
  to `object[]` or a list item that is not a mapping fail the build with a
  message that names the source, content id, `scope.key` and the nested path
  (for example `stops[1]`).
- An empty value builds an empty mapping (`object`) or an empty list
  (`object[]`); `requiredKeys` treats an empty mapping or list as missing.

## Collection Assignment

| Field | Behavior |
|---|---|
| `collection` | Required content ownership for route matching, grouping, feeds, sitemap policy, and schema scope. Not required for data. |
| `addToCollections` | Creates explicit cloned documents and routes for each target collection. |
| `name` | Data source key; must be unique when set. |

`CollectionsValidator` ensures configured source collections exist in
`site.collections`.

The source `type` above selects the `markdown` or `notion` provider; it is not
document metadata `type`. A source collection overrides an item collection but
never changes document type. Type and collection never derive from each other.
