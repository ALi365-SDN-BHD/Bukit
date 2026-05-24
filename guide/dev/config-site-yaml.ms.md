# Konfigurasi (site.yaml) Rujukan Medan

Rujukan medan berwibawa untuk `site.yaml`, termasuk peraturan pengesahan dan lalai.

Pelaksanaan: `src/Bukit.Config/AppConfig.cs`, `src/Bukit.Config/ConfigLoader.cs`, `src/Bukit.Config/ConfigValidator.cs`

## Keutamaan Tindihan (Tertinggi ke Terendah)
1. Parameter CLI
2. `site.yaml`
3. Lalai enjin

## Medan site.* Utama
| Medan | Jenis | Lalai | Penerangan |
|---|---|---|---|
| `site.name` | string | - | Pengecam dalaman |
| `site.title` | string | - | Tajuk paparan |
| `site.baseUrl` | string | `/` | Sub-laluan penerapan |
| `site.url` | string | null | URL mutlak untuk sitemap/rss |
| `site.language` | string | `en-US` | Bahasa lalai |
| `site.languages` | string[] | null | Senarai pelbagai bahasa |
| `site.timezone` | string | `UTC` | Zon waktu |
| `site.pluginFailMode` | string | `strict` | `strict` atau `warn` |
| `site.sitemapMode` | string | `split` | `split`/`merged`/`index` |
| `site.rssMode` | string | `split` | `split`/`merged` |
| `site.searchMode` | string | `split` | `split`/`merged`/`index` |
| `site.outputPathEncoding` | string | `none` | Pengekodan laluan: `none`/`slug`/`urlencode`/`sanitize`. Digunakan untuk halaman kandungan dan terbitan. |
| `site.deriveConflictPolicy` | string | `fail` | Konflik laluan terbitan: `fail`/`warn`/`last-wins`. Konflik kandungan sentiasa gagal. |
| `site.collections` | dict | - | Penghalaan dipacu collection |
| `site.plugins` | dict | - | Togol dan parameter plugin |

## Medan content.*
- `content.provider`: `markdown`, `notion`, atau `sources`
- Markdown: `content.markdown.dir`, `defaultType`, `maxItems`
- Notion: `databaseId`, `filterProperty`, `sortProperty`, `fieldPolicy`
- Media: `content.media.downloadToLocal`, `downloadDir`, `urlBase`

## Medan build.*
- `build.output` (lalai `dist`), `build.clean` (lalai `true`), `build.draft` (lalai `false`), `build.listPageContentMode` (`auto`/`always`/`never`)

## Medan theme.*
- `theme.name`, `theme.layouts`, `theme.assets`, `theme.static`, `theme.params`

## Medan taxonomy.*

| Medan | Jenis | Wajib | Lalai | Penerangan |
|---|---:|---:|---|---|
| `taxonomy.template` | string | Tidak | `pages/page.html` | Templat lalai untuk halaman terbitan taksonomi (digunakan untuk indeks/term) |
| `taxonomy.indexTemplate` | string | Tidak | null | Templat halaman indeks taksonomi (cth., `/tags/`, `/categories/`); sandar ke `taxonomy.template` apabila kosong |
| `taxonomy.termTemplate` | string | Tidak | null | Templat halaman term taksonomi (cth., `/tags/<slug>/`); sandar ke `taxonomy.template` apabila kosong |
| `taxonomy.kinds` | list | Tidak | null | Senarai definisi taksonomi teritlak; menjana jenis sewenang-wenangnya (bukan hanya tags/categories). Setiap entri memerlukan sekurang-kurangnya `key`, pilihan `kind/title/singularTitlePrefix/template/indexTemplate/termTemplate/indexEnabled/hierarchical` |
| `taxonomy.kinds[].hierarchical` | bool | Tidak | false | (v3.0.0+) Dayakan taksonomi hierarki. Apabila didayakan, mengira `children` dan `ancestors` setiap term secara automatik, disuntik ke dalam pembolehubah templat dan output JSON |
| `taxonomy.templates.tags.template` | string | Tidak | null | Templat lalai halaman terbitan tags (sandar ke `taxonomy.template`) |
| `taxonomy.templates.tags.indexTemplate` | string | Tidak | null | Templat halaman indeks tags |
| `taxonomy.templates.tags.termTemplate` | string | Tidak | null | Templat halaman term tags |
| `taxonomy.templates.categories.template` | string | Tidak | null | Templat lalai halaman terbitan categories |
| `taxonomy.templates.categories.indexTemplate` | string | Tidak | null | Templat halaman indeks categories |
| `taxonomy.templates.categories.termTemplate` | string | Tidak | null | Templat halaman term categories |
| `taxonomy.outputMode` | string | Tidak | `both` | `both` (HTML + JSON) \| `pages` (HTML sahaja) \| `data` (JSON sahaja) \| `fields_only` (medan sahaja, tiada fail) |
| `taxonomy.itemFields` | string[] | Tidak | null | Medan tambahan yang didedahkan pada item halaman term (cth., `[cover, image, date]`) |
| `taxonomy.pageSize` | int | Tidak | 10 | Saiz penomboran halaman term |
| `taxonomy.indexEnabled` | bool | Tidak | true | Sama ada menjana halaman indeks taksonomi |
| `taxonomy.pinField` | string | Tidak | `pinned` | Nama medan sematan; item dengan medan ini true muncul dahulu dalam halaman term |
| `taxonomy.pinOrderField` | string | Tidak | null | Medan tertib sematan; item tersemat diisih menaik mengikut medan ini sebelum `publishAt` menurun |
| `taxonomy.pinFieldBySource` | object | Tidak | null | Pemetaan medan sematan setiap sumber (kunci = sourceKey, nilai = nama medan); sandar ke `pinField` global |
| `taxonomy.pinOrderFieldBySource` | object | Tidak | null | Pemetaan medan tertib sematan setiap sumber; sandar ke `pinOrderField` global |

### Nota

- Tanpa `taxonomy.kinds`: kelakuan legasi, hanya menjana halaman terbitan tags/categories.
- Dengan `taxonomy.kinds`: menjana taksonomi sewenang-wenangnya mengikut senarai kinds; medan templat `taxonomy.kinds[]` mempunyai keutamaan tertinggi.
- `taxonomy.kinds[].hierarchical`: apabila didayakan, mengira hierarki secara automatik. Term dikaitkan dengan induk melalui metadata `parent` (sumber data atau `_index.md`); term tanpa `parent` adalah nod akar.
- **Metadata term** menyokong dua sumber pemuatan:
  1. **sumber kandungan mod data**: entri dalam dict `taxonomy_ensure_terms` (cth., `content/data/tags.yaml`), menyokong medan `description`, `image`, `weight`, `parent`
  2. **konvensyen _index.md** (gaya Hugo): `content/_taxonomy/<kind>/<slug>/_index.md` dalam format YAML front matter
- **Suapan RSS**: setiap term menjana `<output>/<kind>/<slug>/feed.xml` secara automatik
- **Alihan alias**: term dengan `Aliases` menjana halaman alihan HTML secara automatik

### Keutamaan Templat (tinggi ke rendah)

1. `taxonomy.templates.<kind>.indexTemplate` / `taxonomy.templates.<kind>.termTemplate`
2. `taxonomy.indexTemplate` / `taxonomy.termTemplate`
3. `taxonomy.templates.<kind>.template`
4. `taxonomy.template`
5. Sandar `pages/page.html`

### Contoh Lengkap

```yaml
taxonomy:
  template: pages/page.html
  indexTemplate: pages/taxonomy-index.html
  termTemplate: pages/taxonomy-term.html
  kinds:
    - key: tags
      kind: tags
      title: Tags
      singularTitlePrefix: Tag
      termTemplate: pages/tag.html
    - key: categories
      kind: categories
      title: Categories
      singularTitlePrefix: Category
      hierarchical: true
      termTemplate: pages/category.html
    - key: series
      kind: series
      title: Series
      singularTitlePrefix: Series
      template: pages/series.html
  templates:
    tags:
      template: pages/tag.html
      indexTemplate: pages/tag-index.html
      termTemplate: pages/tag-term.html
    categories:
      template: pages/category.html
      indexTemplate: pages/category-index.html
      termTemplate: pages/category-term.html
```
