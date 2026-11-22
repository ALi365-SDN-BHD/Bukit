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
a mapping or a list fails the build, naming the source, content id, scope and
key; use `object` or `object[]` for structured values. Store only public
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

- **Booleans and numbers are real booleans and numbers**, so
  `{{ if info.open }}` is false for `false`, `{{ if info.days > 3 }}` compares
  numbers and `{{ info.days + 1 }}` adds. JSON (Notion) says which scalars are
  booleans and numbers. Markdown front matter gives every scalar as text, so a
  Markdown scalar becomes a boolean when it is `true` or `false` (also `True`,
  `TRUE`, `False`, `FALSE`) and a number when it is written like a JSON number
  (`5`, `-12`, `12.50`, `1e3`). Everything else stays text, including `0123`,
  `+60`, `.5`, `yes` and `NaN`, so leading zeros in codes and phone numbers are
  kept. Quoting does not help in Markdown: `code: "5"` is also the number 5, so
  write such a value with a leading zero or another character, or use a Notion
  JSON string (`"code":"5"`), which stays text.
- Whole numbers within the 64-bit range are integers; other numbers keep their
  digits as decimals (`12.50` renders as `12.50`). Only numbers beyond about
  29 significant digits become floating point and may round. A YAML or JSON
  `null` inside the value stays null. The same value builds the same model and
  incremental hash from either source.
- Nested strings are plain text; templates escape them as usual.
- Keys keep their spelling and are ordered ordinally, so reordering keys in the
  source changes neither the output nor the incremental build hash. A blank key
  fails, since templates cannot read it.
- Values may nest at most 8 levels, where every mapping and every list counts as
  one level. A number outside the double range (such as `1e400`) fails.
- A single mapping given to `object[]` is a list of one, as for a mini-program
  `object[]` field.
- Invalid JSON, duplicate JSON keys, a list given to `object` or a list item
  that is not a mapping fail the build with a message that names the source,
  content id, scope and key, and the nested path (for example `stops[1]`).
- An empty value builds an empty mapping (`object`) or an empty list
  (`object[]`). `requiredKeys` treats a mapping or list as missing when it holds
  nothing but empty values, so `{title: null}` is missing too.

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
