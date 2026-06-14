# Module Data Source (mode=data → site.modules)

Modules inject structured data blocks into `site.modules.<type>[]` without generating routes.

Implementation: `src/Bukit.Engine/DataModuleBuilder.cs`

## Configuration

```yaml
content:
  sources:
    - name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
```

Items with `mode: data` do not generate routes; they are grouped by `type` and injected into `site.modules.<type>[]`.

## Recommended Module Fields

| Field | Purpose |
|---|---|
| `type` | Module type (grouping key, required) |
| `title` | Module title |
| `order` | Sort order (number, smaller = earlier) |
| `locale` | Language filtering |
| `enabled` | Toggle |

## Template Usage

```scriban
{{ for b in site.modules.banner }}
  <h2>{{ b.title }}</h2>
  {{ if b.fields.link }}<a href="{{ b.fields.link.value }}">{{ b.title }}</a>{{ end }}
{{ end }}
```

## Multiple data Sources

Multiple `mode: data` sources are merged into `site.modules`. Item IDs are prefixed `<sourceKey>:<sourceId>` to avoid conflicts.

```yaml
content:
  sources:
    - name: modules_marketing
      mode: data
      markdown: { dir: data/marketing, defaultType: module }
    - name: modules_ops
      mode: data
      notion:
        databaseId: "db_modules_ops"
        filterProperty: Enabled
        filterType: checkbox_true
        fieldPolicy: { mode: all }
```

## Taxonomy Supplement

`mode: data` sources with `name: categories` or `name: tags` are used for taxonomy: even unused taxonomy terms generate empty aggregation pages to avoid 404s.
