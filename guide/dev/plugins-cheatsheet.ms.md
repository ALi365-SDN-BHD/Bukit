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
Penomboran: `taxonomy.pageSize` (lalai 10)
Jenis tersuai: `taxonomy.kinds[]` untuk dimensi taksonomi sewenang-wenangnya

### Konfigurasi taksonomi
```yaml
taxonomy:
  template: pages/page.html
  indexTemplate: pages/tax-index.html
  termTemplate: pages/tax-term.html
  pageSize: 10
  kinds:
    - key: tags
      kind: tags
      title: Tags
    - key: categories
      kind: categories
      title: Categories
      hierarchical: true   # Dayakan hierarki induk-anak
    - key: series
      kind: series
      title: Series         # Dimensi taksonomi tersuai
```

### Pembolehubah Templat Taksonomi

**Halaman indeks** (`/tags/`):
- `page.fields.terms.value[]` → `{ title, slug, url, count, description?, image?, weight?, parent?, children?, ancestors?, aliases? }`

**Halaman butiran** (`/tags/<slug>/`):
- `page.fields.items.value[]` → `{ title, url, publish_date, summary }`
- `page.fields.taxonomy.value` → `{ kind, term, slug, count, description?, image?, weight?, parent?, children?, ancestors?, aliases? }`
- `page.fields.pagination.value` → `{ page, page_size, total, total_pages, has_prev, has_next }`

### Medan Baharu (v3.0.0+)

| Medan | Jenis | Sumber | Penerangan |
|------|------|------|------|
| `description` | string? | sumber data atau _index.md | Teks penerangan term |
| `image` | string? | sumber data atau _index.md | Imej kulit term |
| `weight` | int? | sumber data atau _index.md | Berat isihan (lebih tinggi = dahulu) |
| `parent` | string? | sumber data atau _index.md | Slug term induk |
| `children` | string[]? | auto-dikira (hierarki) | Slug term anak |
| `ancestors` | string[]? | auto-dikira (hierarki) | Rantaian slug leluhur |
| `aliases` | string[]? | sumber data | Senarai alias (alihan auto) |

### Output Auto-Dijana (v3.0.0+)

| Artifak | Laluan | Penerangan |
|------|------|------|
| `taxonomy.json` | `<output>/taxonomy.json` | Data berstruktur (skema v2) |
| Suapan RSS | `<output>/<kind>/<slug>/feed.xml` | RSS 2.0 setiap term |
| Alihan alias | `<output>/<kind>/<alias>/index.html` | Alihan segar-semula meta HTML |

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
