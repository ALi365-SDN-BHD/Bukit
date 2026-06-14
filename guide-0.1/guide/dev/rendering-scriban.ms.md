# Rendering dan Templat (Scriban)

Lapisan rendering bertanggungjawab untuk menghasilkan model janaan enjin kepada HTML menggunakan Scriban.

Pelaksanaan: `src/Bukit.Rendering/Models.cs`, `src/Bukit.Rendering/Scriban/`

## Saluran Paip Perenderan Bersatu

Perenderan halaman, senarai, dan HTML statik kini berkongsi gelung penghantaran bersatu `PageRenderDispatcher.DispatchAsync()` (pelaksanaan: `src/Bukit.Engine/PageRenderDispatcher.cs`). Tiga jenis pintu masuk ditakrifkan dalam `RenderEntry.cs`:

| Jenis | Sumber | Kaedah Perenderan |
|---|---|---|
| `Page` | Item kandungan dengan laluan | `renderer.RenderPage(template, pageModel)` |
| `List` | Laluan senarai khas (laman utama, taksonomi, penomboran) | `renderer.RenderList(template, listModel)` |
| `Static` | Fail `.html` dalam `static/` apabila `theme.staticTemplate` ditetapkan | `renderer.RenderPage(template, pageModel)` |

Ketiga-tiganya berkongsi logik langkauan binaan tambahan, suntikan SEO, dan pengendalian ralat yang sama.

## Pemeriksaan Ejaan Pemboleh Ubah Templat

Apabila `EnableRelaxedMemberAccess` didayakan (lalai), Scriban secara senyap mengembalikan `null` untuk pemboleh ubah salah eja seperti `{{ page.titel }}`. Perintah `doctor` Bukit kini merangkumi pemeriksaan ejaan pemboleh ubah templat melalui `ScribanTemplateLinter` yang menghuraikan semua templat `.html` menggunakan AST Scriban dan membandingkan silang dengan senarai putih medan model yang diketahui.

Pelaksanaan: `src/Bukit.Engine/ScribanTemplateLinter.cs`

## Konvensyen Direktori
```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```
- `static/`：Aset statik. Fail bukan HTML disalin terus. Apabila `theme.staticTemplate` ditetapkan, fail `.html` diberikan melalui Scriban menggunakan gelung penghantaran bersatu (saluran paip yang sama dengan halaman kandungan). Pelaksanaan：`src/Bukit.Engine/RenderEntry.cs` → `ForStaticDir()`。
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
