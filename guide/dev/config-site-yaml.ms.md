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
