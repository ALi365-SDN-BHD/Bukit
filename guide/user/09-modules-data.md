# 09 Modules Data

Data modules let templates consume structured source data without generating a
page route.

## Flow

1. A source uses `mode: data`.
2. `ContentPipeline` loads documents normally.
3. `VariantBuildPipeline` selects data items during its data-module preparation stage.
4. `DataModuleBuilder` exposes grouped data as `site.modules` and `site.data`.

## Template Access

```scriban
{{ for item in site.modules.faq }}
  <h2>{{ item.title }}</h2>
  <div>{{ item.content }}</div>
{{ end }}
```

`site.data` is useful for source-keyed data. `site.modules` is useful for lists
of document-like data with title, slug, content, and fields.

## When To Use

- FAQ lists.
- Navigation data.
- Product or service facts.
- Cross-page references that should not produce standalone URLs.

Keep large datasets small enough for static rendering. Core does not turn data
modules into a database runtime.
