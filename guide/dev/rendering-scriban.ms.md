# Rendering dan Templat (Scriban)

Lapisan rendering bertanggungjawab untuk menghasilkan model janaan enjin kepada HTML menggunakan Scriban.

Pelaksanaan: `src/Bukit.Rendering/Models.cs`, `src/Bukit.Rendering/Scriban/`

## Konvensyen Direktori
```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```
- `static/`: Disalin sebagaimana adanya ke akar output
- `assets/`: Disalin ke `assets/` output
- `layouts/`: Direktori akar templat

## Struktur Pembolehubah Templat
### site: `site.name`, `site.title`, `site.url`, `site.base_url`, `site.language`, `site.params`, `site.modules`, `site.data`
### page: `page.title`, `page.url`, `page.content`, `page.summary`, `page.publish_date`, `page.fields`
### pages (halaman senarai): Struktur sama seperti page

## Konvensyen fields
```scriban
{{ if page.fields.seo_title }}
  {{ page.fields.seo_title.value }}
{{ else }}
  {{ page.title }}
{{ end }}
```
