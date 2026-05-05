# 06 Kandungan (Notion): Konfigurasi Lengkap & Contoh untuk Menggunakan Notion sebagai CMS

Jika anda mahu "penulisan dan penyuntingan" berlaku di Notion dan bukannya dalam repositori, mod Notion membolehkan anda merawat pangkalan data Notion sebagai CMS, secara automatik mengambil dan menghasilkan kandungan semasa pembinaan.

Halaman ini menerangkan: medan Notion mana yang diperlukan, cara menapis/mengisih, cara menghantar medan tersuai ke templat, dan isu token/kebenaran lazim.

Untuk peraturan penormalan medan yang mendalam dan kontrak pembangun, lihat: [guide/dev/content](../dev/content.md) dan `docs/notion_schema.md`.

## Apa yang Anda Akan Dapat

- Medan pangkalan data Notion yang disyorkan (boleh dibuat terus mengikut panduan ini)
- Satu `site.yaml` sedia salin (mod Notion)
- Satu "jadual pangkalan data simulasi" (untuk membantu memahami maksud setiap lajur)
- Ralat lazim dan pembaikan (token, databaseId, ketidakpadanan jenis medan)

## Prasyarat & Keperluan Keselamatan

### 1) Pembolehubah persekitaran NOTION_TOKEN mesti ditetapkan

Token Notion **hanya boleh disuntik melalui pembolehubah persekitaran** dan tidak boleh ditulis ke dalam `site.yaml` (atau ke dalam mana-mana fail repositori).

Contoh Windows PowerShell (sesi semasa):

```powershell
$env:NOTION_TOKEN="secret_xxx"
```

Dalam GitHub Actions, gunakan Secrets repositori (lihat: [13 Terap GitHub Pages](./13-deploy-github-pages.ms.md)).

### 2) Integrasi Notion perlu akses kepada pangkalan data anda

Anda perlu mencipta Integrasi dalam Notion dan berkongsi pangkalan data sasaran dengan Integrasi tersebut, jika tidak, anda akan menghadapi ralat "tiada kebenaran / pangkalan data tidak ditemui."

## Konfigurasi Minimum (Notion provider)

```yaml
content:
  provider: notion
  notion:
    databaseId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
```

Adalah disyorkan untuk bermula dengan "konfigurasi minimum", jalankannya, kemudian tambah filter/sort/fieldPolicy secara beransur-ansur.

## Perubahan Konfigurasi Media (Perubahan Pecah)

Bermula dari versi semasa, konfigurasi penyetempatan imej disatukan di bawah `content.media` dan tidak lagi membaca medan media khusus Notion.

Dialih keluar (tiada keserasian):
- `content.notion.downloadImagesToLocal`
- `content.notion.imageDownloadDir`
- `content.notion.imageUrlBase`
- `content.notion.defaultImageUrl`

Sila tukar kepada:

```yaml
content:
  provider: notion
  notion:
    databaseId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
  media:
    downloadToLocal: true
    downloadDir: assets/uploads
    urlBase: /assets/uploads
    defaultImageUrl: /assets/images/noneimg-news.jpg
    fieldKeys: [cover, image, thumbnail, og_image]
    maxConcurrency: 4
    maxRetries: 3
    timeoutMs: 10000
```

## Medan Pangkalan Data yang Disyorkan (ikut persediaan ini)

Nama medan di bawah adalah berdasarkan nama paparan UI Notion dan peka huruf besar/kecil (disyorkan salin-tampal nama medan secara terus).

### Medan Keputusan Enjin (sangat disyorkan)

| Nama Medan | Jenis | Tujuan |
|---|---|---|
| `Published` | checkbox | Sama ada untuk menerbitkan (disyorkan hanya hasilkan kandungan yang diterbitkan) |
| `Title` | title | Tajuk kandungan |
| `Slug` | rich_text atau formula(string) | Slug URL (lalai boleh dijana dari Title, tetapi eksplisit disyorkan untuk kestabilan) |
| `Type` | select atau multi_select | `page`/`post` (untuk lapisan keserasian; disyorkan untuk tambahan mencipta medan `Collection` yang sepadan dengan kunci site.collections) |
| `PublishAt` | date | Tarikh terbit (lalai boleh menggunakan masa semasa, tetapi eksplisit disyorkan) |

### Medan Pelbagai Bahasa (pilihan, tetapi disyorkan)

| Nama Medan | Jenis | Tujuan |
|---|---|---|
| `language` | rich_text / select | Bahasa kandungan (contohnya, `zh-CN`/`en-US`) |
| `i18n_key` | rich_text | Kunci stabil untuk penghubungan kandungan merentas bahasa (contohnya, `about`, `pricing`) |

### Medan Tersuai Templat (mengikut keperluan)

Anda boleh menambah medan sewenang-wenangnya sebagai "medan templat", contohnya:

| Nama Medan | Jenis | Penggunaan Templat |
|---|---|---|
| `SEO Title` | rich_text | `page.fields.seo_title.value` |
| `SEO Desc` | rich_text | `page.fields.seo_desc.value` |
| `cover` | files / url | Imej kulit (`page.fields.cover.value`) |
| `My Link` | url | Pautan (`page.fields.my_link.value`) |
| `reading_time` | number | Masa membaca |

## Data Simulasi (Jadual Pangkalan Data Contoh)

Di bawah adalah jadual "data simulasi" untuk membantu anda memahami bagaimana halaman Notion menjadi kandungan tapak (anda boleh mereplikasi beberapa entri ujian dalam Notion).

| Published | Title | Slug | Type | PublishAt | language | i18n_key | SEO Title | tags | categories |
|---|---|---|---|---|---|---|---|---|---|
| ✅ | Tentang Kami | about | page | 2026-01-01 | zh-CN | about | Tentang Kami - My Site | company,intro | docs |
| ✅ | About | about | page | 2026-01-01 | en-US | about | About - My Site | company,intro | docs |
| ✅ | Catatan Blog Pertama | first-post | post | 2026-01-10 | zh-CN | blog_first | Catatan Blog Pertama - My Site | release,roadmap | updates |
| ⬜ | Draf Belum Diterbitkan | draft-1 | post | 2026-01-20 | zh-CN | draft_1 | Draf - My Site | draft | draft |

Nota:

- `Published` digunakan untuk penapisan pembinaan untuk mencegah draf dari disiarkan
- `language + i18n_key` digunakan untuk penghubungan kandungan tapak pelbagai bahasa (pilihan)
- Medan tersuai seperti `SEO Title` memerlukan `fieldPolicy` untuk membenarkannya ke dalam templat (lihat bahagian seterusnya)

> **Syor: Gunakan site.collections dan bukannya penghalaan lalai type.** Jika anda menambah medan `Collection` (jenis select, nilai seperti `blog`, `docs`) ke pangkalan data Notion anda dan mengisytiharkan peraturan collection yang sepadan dalam site.yaml's site.collections, enjin akan mengutamakan penghalaan dipacu collection berbanding sandaran keserasian type.

## Penapisan & Pengisihan (filter / sort)

### Hanya hasilkan kandungan yang diterbitkan

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    filterProperty: Published
    filterType: checkbox_true
```

### Isih mengikut tarikh terbit menurun

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    sortProperty: PublishAt
    sortDirection: descending
```

## Had, Pengambilan Berskop & Caching (Pangkalan Data Besar / Mengurangkan Permintaan Notion)

### 1) maxItems: Hadkan bilangan maksimum item yang diambil

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    maxItems: 5000
```

### 2) includeSlugs: Hanya ambil halaman dengan slug yang ditentukan

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    includeSlugProperty: Slug
    includeSlugs: [about, first-post]
```

### 3) cacheMode/cacheDir: Cache hasil render badan kandungan

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    cacheMode: readwrite   # off | readwrite | readonly
    cacheDir: .cache/notion
```

### 4) renderConcurrency/maxRps/maxRetries: Render serentak & had kadar

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    renderConcurrency: 4
    maxRps: 3
    maxRetries: 5
```

### 5) notion.stats: Log statistik permintaan/sekatan semasa pembinaan

```
event=notion.stats requests=1234 throttle_wait_count=56 throttle_wait_ms=7890
```

## fieldPolicy: Medan Notion Mana yang Memasuki page.fields

### Whitelist (Disyorkan: terkawal, selamat, templat lebih stabil)

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    fieldPolicy:
      mode: whitelist
      allowed:
        - seo_title
        - seo_desc
        - cover
        - reading_time
        - my_link
```

### Semua (mudah untuk penyahpepijatan, tetapi perubahan medan lebih mudah mempengaruhi templat)

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    fieldPolicy:
      mode: all
```

## Ralat Lazim dan Pembaikan

### 1) Ralat: NOTION_TOKEN Hilang

Gejala: `doctor` atau `build` gagal serta-merta pada peringkat pengesahan konfigurasi.

Pembaikan: Setempat: tetapkan pembolehubah persekitaran `NOTION_TOKEN`; CI: tambah `NOTION_TOKEN` ke GitHub Actions Secrets.

### 2) Ralat: databaseId Tidak Sah / Tiada Kebenaran

Pembaikan: Sahkan databaseId adalah ID "pangkalan data", bukan URL halaman; Integrasi telah dikongsi dengan pangkalan data ini; token tergolong dalam workspace yang sama.

### 3) Ralat: Ketidakpadanan Jenis Medan

Pembaikan: Cipta medan mengikut jenis yang disyorkan di halaman ini (date/checkbox/select, dll.), atau laraskan `filterProperty/sortProperty` untuk menunjuk ke medan yang jenis sebenarnya sepadan.
