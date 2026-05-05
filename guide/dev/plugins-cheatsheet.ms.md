# Helaian Tipu Plugin Bukit

## Plugin Terbina Dalam
| Plugin | Output | Kebergantungan |
|---|---|---|
| sitemap | `sitemap.xml` | `site.url` diperlukan |
| rss | `rss.xml` | `site.url` diperlukan |
| search-index | `search.json` | Tiada |
| taxonomy | `/tags/`, `/categories/` | `meta.tags`/`meta.categories` |

## Konfigurasi Plugin
```yaml
site:
  plugins:
    sitemap: true
    path-report:
      enabled: true
      options: {}
```

## Plugin taksonomi
Halaman: `/tags/`, `/tags/<slug>/`, `/categories/`, `/categories/<slug>/`
Templat: `pages/page.html` (boleh dikonfigurasi)

### Konfigurasi taksonomi
```yaml
taxonomy:
  template: pages/page.html
  indexTemplate: pages/tax-index.html
  termTemplate: pages/tax-term.html
  pageSize: 10
```

## Penyetempatan Imej (`content.media`)
```yaml
content:
  media:
    downloadToLocal: true
    downloadDir: assets/uploads
    urlBase: /assets/uploads
    defaultImageUrl: /assets/images/default.jpg
    fieldKeys: [cover, image, thumbnail, og_image]
```
