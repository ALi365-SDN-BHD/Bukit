# Scriban Template Cheatsheet (Bukit Theme Development)

## Basic Syntax

```scriban
{{ site.title }}
{{ "hello" | string.upcase }}
{{ x = "value" }}
```

## Conditionals

```scriban
{{ if page.summary }}<p>{{ page.summary }}</p>{{ end }}
{{ page.summary ?? "Default summary" }}
```

## Loops

```scriban
{{ for item in pages }}
  <a href="{{ item.url }}">{{ item.title }}</a>
  {{ if !for.last }}<hr>{{ end }}
{{ end }}
```

Loop variables: `for.index`, `for.first`, `for.last`, `for.even`, `for.odd`
Parameters: `{{ for item in pages limit:10 offset:2 }}`

## Layout and Include

```scriban
{{ layout "layouts/base.html" }}
<h1>{{ page.title }}</h1>
```

In base.html: `{{ content }}` receives child output. Include: `{{ include "partials/header.html" }}`

## Bukit Template Variables

### site: `site.name`, `site.title`, `site.url`, `site.description`, `site.base_url`, `site.language`, `site.params`, `site.modules`, `site.data`

### page: `page.title`, `page.url`, `page.content`, `page.summary`, `page.publish_date`, `page.fields.<key>.type/value`

### pages (list pages only): same structure as page

### site.modules (data modules): `site.modules.<type>[]`

### site.data (plugin-injected): e.g. `site.data.pages_by_id[pageId]`

## Common Functions

Strings: `string.upcase`, `string.truncate 200`, `string.strip`, `string.replace "a" "b"`, `string.contains "kw"`, `string.split ","`, `string.starts_with "http"`
Dates: `date.to_string "%Y-%m-%d"`, `date.add_days 7`
Arrays: `array.size`, `array.first`, `array.last`, `array.sort_by "field"`, `array.reverse`

## Required Templates: `pages/index.html`, `pages/list.html`, `pages/post.html`, `pages/page.html`, `layouts/base.html`

## Resource Paths: Always concatenate `site.base_url`:
```html
<link rel="stylesheet" href="{{ site.base_url }}/assets/style.css" />
```
