# Pembangunan Tema dan Penggunaan Parameter

Tema adalah templat + aset + fail statik. Menggunakan enjin templat Scriban.

Contoh tema: `examples/starter/themes/alt/`

## Struktur Direktori
```text
themes/<name>/
  layouts/        # Akar templat Scriban
  assets/         # Disalin ke output /assets/
  static/         # Disalin sebagaimana adanya ke akar output
```

## Peraturan Resolusi Tema
Apabila `theme.name` tidak kosong dan `theme.layouts/assets/static` pada lalai:
- layoutsDir = `themes/<name>/layouts`, assetsDir = `themes/<name>/assets`, staticDir = `themes/<name>/static`

## Perintah Tema
```bash
bukit theme list --config site.yaml
bukit theme use alt --config site.yaml
```

## Susun Atur dan Sertakan
```scriban
{{ layout "layouts/base.html" }}
```
Dalam base.html: `{{ content }}`. Sertakan: `{{ include "partials/header.html" }}`

## Parameter Tema (theme.params → site.params)
```yaml
theme:
  params:
    brand: ALT THEME
```
Templat: `{{ site.params.brand }}`

## Aset Statik dan base_url
```html
<link rel="stylesheet" href="{{ site.base_url }}/assets/style.css" />
```
