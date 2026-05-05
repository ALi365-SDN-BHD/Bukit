# 07 Kandungan (Pelbagai Sumber): Menggabungkan pages / posts / modules

Apabila kandungan tapak anda datang dari lebih daripada satu sumber (contohnya: halaman dari Markdown, blog dari Notion, modul halaman utama dari data), anda harus menggunakan `content.provider: sources`.

Halaman ini menerangkan struktur sources, maksud `mode=content|data`, dan contoh lengkap gabungan lazim.

## Apa yang Anda Akan Dapat

- Penjelasan medan untuk sources (type/name/mode)
- 3 konfigurasi gabungan sedia salin (semua Markdown, semua Notion, mod hibrid)
- Cara menyuntik modul sebagai data ke dalam `site.modules`

## Struktur Asas sources

```yaml
content:
  provider: sources
  sources:
    - type: markdown
      name: pages
      mode: content
      markdown:
        dir: content
        defaultType: page
```

> **Disyorkan: pasangkan dengan site.collections.** Apabila menggunakan mod sources, adalah disyorkan untuk juga mengisytiharkan `site.collections` di peringkat atas site.yaml, supaya kandungan setiap source dipadankan dengan peraturan penghalaan melalui kunci collection (dan bukannya bergantung pada lapisan keserasian type).

### Penerangan Medan

| Medan | Tujuan | Nasihat |
|---|---|---|
| `type` | Jenis sumber: `markdown` atau `notion` | Pilih mengikut sumber |
| `name` | Nama sumber (untuk pengenalpastian/penyelesaian masalah) | Kekalkan pendek dan jelas, cth., `pages`, `posts`, `modules` |
| `mode` | `content` atau `data` | Lalai kepada `content`; gunakan `data` untuk modul |

Semantik utama:

- `mode: content`: menjana laluan dan halaman (kandungan biasa)
- `mode: data`: tidak menjana laluan; item dikumpulkan dan disuntik ke dalam `site.modules.<type>[]` (blok kandungan berstruktur)

Tambahan: taxonomy (kategori/teg) dan `mode: data`

- Jika anda mempunyai "pangkalan data kategori / pangkalan data teg", anda boleh menambahnya sebagai sumber `mode: data` dengan `name` ditetapkan kepada `categories` atau `tags`.
- Semasa pembinaan, entri sumber data akan dirawat sebagai senarai terma taxonomy: walaupun kategori/teg tertentu pada masa ini tidak mempunyai artikel yang merujuknya, halaman agregasi kosong yang sepadan akan dijana (mengelakkan 404 pada klik menu).

## Contoh Gabungan 1: Semua Markdown (kandungan + modul)

Lihat contoh boleh laku: `examples/starter/site.modules.yaml`.

```yaml
content:
  provider: sources
  sources:
    - type: markdown
      name: content
      mode: content
      markdown:
        dir: content
        defaultType: page
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
```

Fail data modul sokongan:

- `data/banner-1.md`
- `data/faq-main.md`
- `data/nav-home.md`

Lihat butiran: [09 Modul Data Berstruktur](./09-modules-data.ms.md).

> Jika anda mahukan kawalan penghalaan yang tepat, tambah ke site.yaml:
> ```yaml
> site:
>   collections:
>     page:
>       permalink: /pages/{slug}/
>       template: pages/page.html
>       listRoute: /pages/
> ```

## Contoh Gabungan 2: Semua Notion (pelbagai pangkalan data: pages + posts + modules)

Kes penggunaan:

- Kandungan pasukan diuruskan sepenuhnya dalam Notion, tetapi anda mahukan pangkalan data berasingan (perbezaan kebenaran/proses/medan)

```yaml
content:
  provider: sources
  sources:
    - type: notion
      name: pages
      mode: content
      notion:
        databaseId: "db_pages"
        filterProperty: Published
        filterType: checkbox_true
        fieldPolicy: { mode: whitelist, allowed: [seo_title, seo_desc, cover] }
    - type: notion
      name: posts
      mode: content
      notion:
        databaseId: "db_posts"
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: PublishAt
        sortDirection: descending
        fieldPolicy: { mode: all }
    - type: notion
      name: modules
      mode: data
      notion:
        databaseId: "db_modules"
        filterProperty: Enabled
        filterType: checkbox_true
        fieldPolicy: { mode: all }
    - type: notion
      name: categories
      mode: data
      notion:
        databaseId: "db_categories"
        filterProperty: Enabled
        filterType: checkbox_true
        fieldPolicy: { mode: all }
```

Nota:

- Ketiga-tiga pangkalan data perlu boleh diakses oleh `NOTION_TOKEN` yang sama (atau pisahkan repo/aliran kerja)
- Pangkalan data modules sepatutnya mempunyai medan seperti `type/order/locale/enabled` (lihat: [09 Modul Data Berstruktur](./09-modules-data.ms.md))

> Adalah disyorkan untuk mengisytiharkan site.collections (cth., `blog`, `docs`) dalam site.yaml dan mengisi kunci yang sepadan dalam medan `Collection` setiap pangkalan data Notion. Ini membolehkan enjin memadankan peraturan penghalaan dengan tepat tanpa bergantung pada sandaran keserasian type.

## Contoh Gabungan 3: Hibrid (Halaman dari Markdown + Blog dari Notion + Modul dari Markdown)

Kes penggunaan:

- Halaman syarikat diselenggara oleh pembangun dalam repo (lebih stabil)
- Blog/berita diselenggara oleh operasi dalam Notion (lebih fleksibel)
- Modul syarikat bermula dengan data Markdown untuk berjalan dengan cepat, kemudian beransur-ansur berhijrah ke Notion

```yaml
content:
  provider: sources
  sources:
    - type: markdown
      name: pages
      mode: content
      markdown:
        dir: content/pages
        defaultType: page
    - type: notion
      name: posts
      mode: content
      notion:
        databaseId: "db_posts"
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: PublishAt
        sortDirection: descending
        fieldPolicy: { mode: whitelist, allowed: [seo_title, seo_desc, cover, reading_time] }
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
```

## Petua Penyelesaian Masalah (isu lazim sources)

### 1) Kandungan sumber "tidak sampai ke tapak"

Lakukan tiga perkara ini dahulu:

1. Jalankan `doctor --config site.yaml` untuk menyemak ralat pengesahan konfigurasi
2. Sahkan bahawa `dir/databaseId` sumber menunjuk ke lokasi yang betul
3. Untuk Notion, semak sama ada penapis mengecualikan kandungan (cth., Published tidak ditandakan)

### 2) Modul tidak muncul dalam templat

Semak:

- `mode` sumber adalah `data`
- Item kandungan modul mempunyai `type` (digunakan untuk mengumpulkan ke dalam `site.modules.<type>`)
- Templat tema sebenarnya membaca `site.modules` (lihat: [08 Tema & Templat](./08-themes-templates.ms.md))
