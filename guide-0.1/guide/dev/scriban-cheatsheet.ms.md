# Helaian Tipu Templat Scriban (Pembangunan Tema Bukit)

## Sintaks Asas
```scriban
{{ site.title }}
{{ "hello" | string.upcase }}
{{ x = "value" }}
```

## Syarat
```scriban
{{ if page.summary }}<p>{{ page.summary }}</p>{{ end }}
{{ page.summary ?? "Ringkasan lalai" }}
```

## Gelung
```scriban
{{ for item in pages }}
  <a href="{{ item.url }}">{{ item.title }}</a>
{{ end }}
```
Pembolehubah gelung: `for.index`, `for.first`, `for.last`

## Susun Atur dan Sertakan
```scriban
{{ layout "layouts/base.html" }}
```
Dalam base.html: `{{ content }}`. Sertakan: `{{ include "partials/header.html" }}`

## Pembolehubah Templat Bukit
- **site**: `site.name`, `site.title`, `site.base_url`, `site.modules`
- **page**: `page.title`, `page.content`, `page.summary`, `page.fields`
- **pages**: Tatasusunan halaman dalam halaman senarai

## Fungsi Lazim
Rentetan: `string.upcase`, `string.truncate 200`, `string.replace "a" "b"`
Tarikh: `date.to_string "%Y-%m-%d"`
Tatasusunan: `array.size`, `array.first`, `array.sort_by "field"`

## Templat Diperlukan
`pages/index.html`, `pages/list.html`, `pages/post.html`, `pages/page.html`, `layouts/base.html`

## Laluan Sumber
```html
<link rel="stylesheet" href="{{ site.base_url }}/assets/style.css" />
```
