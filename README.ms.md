# Bukit - Enjin Tapak Statik .NET Native AOT

<p align="center">
  <img src="docs/bukit-logo.svg" alt="bukit logo" width="400">
</p>

Versi bahasa: [English](./README.md) | [简体中文](./README.zh-CN.md) | Bahasa Melayu (semasa)

Bukit ialah enjin penjanaan tapak statik .NET Native AOT untuk **Nota-sebagai-CMS**, **aliran kerja ejen AI**, dan **laman web sedia GEO**. Ia menukar kandungan Markdown dan Notion kepada laman statik yang pantas dan boleh dideploy.

## Apa Itu Bukit

Bukit ialah runtime dan enjin binaan:

- pengambilan kandungan
- penjanaan laluan
- pemaparan templat Scriban
- output SEO, GEO, feed, sitemap, dan audit
- penjanaan HTML statik
- deploy ke GitHub Pages melalui Core CLI

BukitJalil ialah panel kawalan tempatan yang berasingan. Ia bukan sebahagian daripada runtime Bukit, dan tidak diperlukan untuk membina laman dengan Bukit.

Bukit bukan platform SaaS, backend CMS penuh, pembina halaman visual, atau pengganti BukitJalil.

## Keupayaan Core 1.0

- **Native AOT CLI**: permulaan pantas, memori rendah, dan edaran binari tunggal untuk Linux, macOS, dan Windows.
- **Sumber kandungan**: provider langsung Core hanyalah Markdown dan Notion.
- **Nota-sebagai-CMS**: Obsidian dan aplikasi nota lain disokong melalui eksport serasi Markdown. Integrasi langsung Feishu, Yuque, dan pangkalan pengetahuan lain ialah kerja masa depan.
- **Templat Scriban**: layout, partial, snippet, halaman koleksi, penomboran, taksonomi, dan output berbilang bahasa.
- **Tema filesystem**: direktori tempatan `themes/<name>/` dengan layouts, assets, static files, dan pilihan `theme.yaml`.
- **Output SEO dan GEO**: sitemap, RSS/Atom/JSON Feed, JSON-LD, Open Graph, Twitter Cards, URL canonical, hreflang, `llms.txt`, `robots.txt`, audit SEO, audit GEO, dan laporan audit penerbitan.
- **Pelayan pembangunan LiveReload**: memantau fail kandungan dan tema, membina semula secara incremental, menyiarkan melalui WebSocket, dan menyegar semula pelayar.
- **Pelayan pratonton statik**: menyajikan direktori output yang telah dibina.
- **Deploy GitHub Pages**: `deploy.provider: github-pages` bersama arahan `bukit deploy`.

## Mula Pantas

Apabila membangunkan Bukit daripada repositori ini:

```bash
dotnet build bukit.slnx -c Release
dotnet run --project src/Bukit.Cli -c Release -- config check --config path/to/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- doctor --config path/to/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config path/to/site.yaml --clean --site-url https://example.com
```

Apabila menggunakan binari Bukit yang telah dipasang atau dimuat turun dari direktori laman:

```bash
bukit config check
bukit doctor
bukit build --clean
bukit dev
```

Gunakan `bukit preview --dir dist` apabila anda hanya mahu menyajikan output binaan sedia ada.

## Arahan Core CLI

Bukit Core 1.0 hanya mendedahkan permukaan arahan stabil ini:

| Arahan | Tujuan |
|---|---|
| `build` | Membina laman statik |
| `doctor` | Mendiagnosis konfigurasi, templat, provider, dan kesediaan binaan |
| `config` | Mengesahkan konfigurasi atau menjana schema konfigurasi |
| `preview` | Menyajikan direktori output yang telah dibina |
| `dev` | Menjalankan pelayan pembangunan LiveReload |
| `clean` | Memadam direktori output dan cache |
| `version` | Mencetak maklumat versi |
| `completion` | Menjana shell completion |
| `seo` | Mengaudit atau membandingkan laporan SEO |
| `geo` | Mengaudit output GEO dan `llms.txt` |
| `publish` | Mengaudit atau membandingkan laporan kesediaan terbit |
| `deploy` | Mendeploy laman yang telah dibina ke GitHub Pages |

Subcommand stabil ialah `config check`, `config schema`, `seo audit`, `seo diff`, `geo audit`, `publish audit`, dan `publish diff`.

## `site.yaml` Minimum

```yaml
site:
  name: my-site
  title: My Site
  url: https://example.com
  baseUrl: /
  language: en
  collections:
    page:
      permalink: /{slug}/
      template: pages/page.html
      listRoute: /
      listTemplate: pages/index.html
content:
  sources:
    - type: markdown
      name: pages
      mode: content
      collection: page
      markdown:
        dir: content
build:
  output: dist
  clean: true
theme:
  name: starter
logging:
  level: info
```

Notion juga provider kandungan Core. Tambah sumber `notion` di bawah `content.sources[]` dan sediakan `NOTION_TOKEN` melalui persekitaran, bukan di dalam `site.yaml`.

## Asas Tema

Tema Core ialah direktori filesystem tempatan:

```text
themes/<name>/
  layouts/
    layouts/base.html
    pages/page.html
    pages/post.html
    pages/index.html
    pages/list.html
    partials/
  assets/
  static/
  theme.yaml
```

Gunakan `theme.name` dalam `site.yaml` untuk memilih tema. Sumber tema jauh, pendaftaran tema, pemasangan tema, dan aliran kerja pasaran tema bukan sebahagian daripada Core 1.0.

## Pembangunan Dan Pratonton

`bukit dev` ialah pelayan pembangunan LiveReload. Ia menjalankan binaan awal, memantau fail kandungan, layout, aset, statik, dan tema aktif, membina semula secara incremental, dan menyegar semula pelayar yang bersambung. Ia tidak menampal komponen framework secara langsung di tempatnya.

`bukit preview` ialah pelayan fail statik untuk output binaan. Gunakannya selepas `bukit build` apabila anda tidak memerlukan pemantauan fail.

## Deploy

Untuk deploy GitHub Pages, konfigurasikan laman:

```yaml
deploy:
  provider: github-pages
  branch: gh-pages
```

Kemudian sahkan dan deploy:

```bash
bukit config check
bukit doctor
bukit build --clean
bukit publish audit --dir dist
bukit deploy
```

Untuk deploy CI, cipta workflow GitHub Pages khusus untuk laman anda. Contoh di [`examples/github-pages-workflow.yml`](examples/github-pages-workflow.yml) boleh menjadi titik mula workflow laman. Jangan salin release workflow repositori ini; ia menerbitkan binari Bukit, bukan laman pengguna.

## Dokumentasi

| Kawasan | Mula di sini |
|---|---|
| Panduan pengguna Core | [`guide/user`](guide/user/README.md) |
| Panduan pembangun Core | [`guide/dev`](guide/dev/README.md) |
| Agent skills selaras Core | [`guide/skills`](guide/skills/README.md) |
| Labs dan workflow preview | [`guide/labs`](guide/labs/README.md) dan [`guide/labs-skills`](guide/labs-skills/README.md) |
| Dokumen sejarah yang diarkib | [`guide/archive`](guide/archive/README.md) |
| Rujukan CLI | [`guide/user/12-cli-reference.md`](guide/user/12-cli-reference.md) |
| Rujukan konfigurasi | [`guide/user/04-site-yaml-config.md`](guide/user/04-site-yaml-config.md) |
| Deploy GitHub Pages | [`guide/user/13-deploy-github-pages.md`](guide/user/13-deploy-github-pages.md) |

Jika panduan menerangkan clone, import, intent, webhook, sumber tema jauh, pendaftaran tema, atau pasaran plugin luaran, anggap ia sebagai Labs, preview, atau bahan sejarah kecuali ia dinaikkan secara jelas ke dalam senarai putih arahan Core.

## AI Agent Skills

Arahan untuk ejen berada di [`guide/skills`](guide/skills/README.md). Pek itu selaras dengan Core 1.0 dan hanya patut mengajar arahan serta kontrak Core yang stabil.

Labs skills berada di [`guide/labs-skills`](guide/labs-skills/README.md). Ia bersifat opt-in dan tidak boleh dianggap sebagai kelakuan Core lalai.

## Skop Kestabilan

**Bukit Core 1.0 Stable** merangkumi:

- arahan CLI yang disenaraikan dalam [Arahan Core CLI](#arahan-core-cli)
- kontrak konfigurasi `content.sources[]`
- provider Markdown dan Notion
- eksport nota serasi Markdown melalui provider Markdown
- `content.media`
- penghalaan koleksi
- pemaparan Scriban
- tema filesystem tempatan
- kelakuan filesystem output yang selamat
- output SEO, RSS, sitemap, JSON Feed, dan carian
- output GEO, `llms.txt`, dan audit penerbitan
- laporan binaan
- binaan incremental
- Native AOT CLI
- deploy GitHub Pages

**Tidak termasuk dalam Core 1.0**:

- clone-to-theme
- import demo HTML
- workflow import seed
- Notion push atau migrasi Notion
- pendaftaran tema, pasaran tema, sumber tema jauh, atau workflow pemasangan tema
- ekosistem plugin luaran atau pasaran plugin
- workflow AI intent
- automasi webhook
- panel kawalan BukitJalil
- integrasi langsung yang lebih luas untuk Feishu, Yuque, dan pangkalan pengetahuan lain

## Pelan Hala Tuju

| Kawasan | Status |
|---|---|
| Bina, pratonton, dev, penghalaan, templat | Stable |
| Markdown, Notion, SEO/GEO, audit penerbitan | Stable |
| Deploy GitHub Pages | Stable |
| Ekosistem tema dan perkakasan templat | Labs / Future |
| Workflow AI intent | Labs / Future |
| Ekosistem plugin luaran dan pasaran | Future |
| Panel kawalan BukitJalil | Future |
| Integrasi sumber pengetahuan langsung yang lebih luas | Future |

## Menyumbang

Sumbangan dialu-alukan. Lihat:

- [`guide/dev/README.md`](guide/dev/README.md)
- [`guide/dev/testing.md`](guide/dev/testing.md)
- [`guide/dev/documentation-governance.md`](guide/dev/documentation-governance.md)

## Lesen

Projek ini dilesenkan di bawah syarat dalam [LICENSE](./LICENSE).
