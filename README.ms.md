# Bukit — Enjin Tapak Statik .NET Native AOT

<p align="center">
  <img src="docs/bukit-logo.svg" alt="bukit logo" width="400">
</p>

Versi bahasa: [English](./README.md) | [简体中文](./README.zh-CN.md) | Bahasa Melayu (semasa)

Bukit ialah enjin penjanaan tapak statik .NET Native AOT untuk **Nota-sebagai-CMS**, **aliran kerja Ejen AI**, dan **laman web sedia GEO**. Tukar pangkalan data Notion dan Markdown kepada tapak statik yang pantas dan boleh dideploy.

## Apa itu Bukit?

```
 Bukit
 = Enjin Tapak Statik
 = Teras Binaan
 = Penghamilan kandungan, penjanaan laluan, pemaparan templat Scriban, output SEO/GEO

 BukitJalil
 = Aplikasi Tempatan / Panel Kawalan
 = Pengurusan projek, pengurusan tema, aliran kerja perbualan AI, kawalan bina & deploy

 Nota-sebagai-CMS
 = Penghasilan Kandungan
 = Notion / Markdown / Obsidian / Feishu / Yuque / pangkalan pengetahuan lain
```

Bukit mengendalikan penghamilan kandungan, penjanaan laluan, pemaparan templat Scriban, output SEO/GEO, dan penjanaan HTML statik. Ia sesuai untuk laman web syarikat, laman dokumentasi, laman kandungan, halaman pendaratan, dan aliran kerja penerbitan berbantu AI.

**BukitJalil** ialah panel kawalan tempatan yang berasingan — bukan sebahagian daripada enjin runtime Bukit, dan tidak diperlukan untuk membina tapak dengan Bukit.

Bukit **bukan** platform SaaS, backend CMS penuh, pembina halaman visual, atau pengganti untuk BukitJalil.

## Kenapa Bukit?

- **Native AOT** — permulaan bawah 50ms, memori rendah, deployan binari tunggal di Linux, macOS, dan Windows
- **Nota-sebagai-CMS** — tulis kandungan dalam Notion atau Markdown; Bukit menukarnya menjadi tapak statik
- **Ejen AI asli** — `src/skills/` menyediakan lapisan pengetahuan untuk ejen pengekodan AI
- **Sedia GEO** — pengoptimuman enjin carian AI terbina dengan `llms.txt`, data berstruktur FAQ/HowTo, dan audit GEO

## Ciri Teras

- **Penyedia kandungan Markdown & Notion** dengan pemetaan medan boleh konfigurasi
- **Enjin templat Scriban** dengan pewarisan susun atur, separa, dan pustaka coretan
- **Penghalaan berasaskan koleksi** dengan pautan kekal, halaman senarai, penomboran, dan taksonomi
- **Sokongan pelbagai bahasa** — binaan mengikut bahasa, gabungan sitemap/RSS/carian
- **SEO** — sitemap, RSS/Atom/JSON Feed, JSON-LD, Open Graph, Twitter Cards, URL kanonik, hreflang
- **GEO** — `llms.txt`, peraturan robots.txt perangkak AI, data berstruktur FAQ/HowTo, audit Skor GEO
- **Sistem tema** dengan token reka bentuk, tema berkomponen, pengedaran tema, dan pendaftaran
- **Pelayan pembangunan HMR** dengan muat semula langsung WebSocket; pelayan pratonton untuk output binaan
- **Sistem plugin** — cangkuk `derive-pages` dan `after-build`; sokongan plugin WASM dan proses
- **Binaan inkremental** — pengesanan perubahan sedar kandungan; pilihan pengecaman aset SHA256
- **Deployan GitHub Pages** melalui CLI atau aliran kerja GitHub Actions

## Mula Pantas

```bash
# Bina CLI
dotnet build bukit.slnx -c Release

# Sahkan konfigurasi tapak contoh
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml

# Bina tapak contoh
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com

# Pratonton setempat
dotnet run --project src/Bukit.Cli -c Release -- preview --dir examples/starter/dist --port auto
```

Untuk panduan lengkap, lihat [Panduan Mula Pantas](guide/user/01-quick-start.ms.md).

## Dokumentasi

| Khalayak | Mula di sini |
|---|---|
| Pengguna baharu | [`guide/user`](guide/user/README.ms.md) — Mula Pantas, konfigurasi, kandungan, deployan, penyelesaian masalah |
| Penyelenggara / penyumbang | [`guide/dev`](guide/dev/README.ms.md) — seni bina, kontrak CLI, pemaparan, plugin, kebolehcerapan |
| Pengguna Ejen AI | [`src/skills`](src/skills/README.ms.md) — fail kemahiran untuk Codex, Claude Code, Copilot, Gemini CLI |
| Bina tapak dengan AI | [`guide/ai/chatgpt`](guide/ai/chatgpt/README.ms.md) — pek dorongan ChatGPT dan kontrak intent |
| Rujukan CLI | [`guide/user/12-cli-reference.ms.md`](guide/user/12-cli-reference.ms.md) |
| Rujukan konfigurasi | [`guide/user/04-site-yaml-config.ms.md`](guide/user/04-site-yaml-config.ms.md) |
| Deployan | [`guide/user/13-deploy-github-pages.ms.md`](guide/user/13-deploy-github-pages.ms.md) |

## Aliran Kerja Notion CMS

- Tetapkan `content.provider: notion` dalam `site.yaml`
- Sediakan token anda sebagai pemboleh ubah persekitaran: `NOTION_TOKEN` (jangan letak dalam `site.yaml`)
- Medan pangkalan data lalai: `Published` (checkbox), `Title`, `Slug`, `Type` (post/page), `PublishAt`
- Panduan penuh: [`guide/user/06-notion-content.ms.md`](guide/user/06-notion-content.ms.md)
- Rujukan skema: [`guide/dev/content.ms.md`](guide/dev/content.ms.md)

## Aliran Kerja AI / Ejen

`src/skills/` ialah lapisan pengetahuan Ejen AI — bukan kod runtime. Ia membantu ejen pengekodan memahami CLI Bukit, konfigurasi, tema, templat, Notion, penghalaan, i18n, deployan, SEO/GEO, dan penyahpepijatan.

- Sesuai untuk: Codex CLI, Claude Code, Copilot CLI, Gemini CLI, dan alat serupa
- Pengguna biasa: mula dari [`guide/user`](guide/user/README.ms.md)
- Pengguna ejen: mula dari [`src/skills/using-bukit/SKILL.md`](src/skills/using-bukit/SKILL.md) atau [`src/skills/bukit-cli-reference/SKILL.md`](src/skills/bukit-cli-reference/SKILL.md)
- Katalog kemahiran: [`src/skills/README.ms.md`](src/skills/README.ms.md)

## Penerapan (Deployment)

Templat aliran kerja GitHub Actions tersedia di [`.github/workflows/release.yml`](.github/workflows/release.yml).

1. Pergi ke GitHub **Settings → Pages** dan pilih "GitHub Actions"
2. Jika menggunakan Notion, tambah `NOTION_TOKEN` dalam rahsia repositori
3. Tolak ke `main` — aliran kerja akan membina dan menerapkan tapak anda

Lihat [`guide/user/13-deploy-github-pages.ms.md`](guide/user/13-deploy-github-pages.ms.md) untuk panduan terperinci.

## Status Projek

**Bukit kini dalam pratonton awam.** Ia sesuai untuk:

- Penjanaan tapak statik setempat daripada Markdown dan Notion
- Deployan GitHub Pages
- Pembangunan dan penyesuaian tema
- Pengesahan output SEO/GEO
- Pembinaan tapak berbantu Ejen AI

**Masih berkembang:** pendaftaran tema, aliran kerja klon-ke-tema, ekosistem plugin luaran, panel kawalan BukitJalil, dan aliran kerja intent AI lanjutan. Ciri-ciri ini belum stabil.

## Pelan Hala Tuju

| Bidang | Status |
|---|---|
| Bina, pratonton, penghalaan, templat | Stabil |
| Markdown, Notion, SEO/GEO | Stabil |
| Ekosistem tema, perkakasan templat | Diperbaiki |
| Aliran kerja intent AI | Diperbaiki |
| Panel kawalan BukitJalil | Akan datang |
| Pasaran / pendaftaran plugin | Akan datang |
| Integrasi sumber pengetahuan lebih luas | Akan datang |

## Menyumbang

Sumbangan dialu-alukan. Lihat panduan pembangun untuk dokumen seni bina, prosedur pengujian, dan aliran kerja sumbangan:

- [`guide/dev/README.ms.md`](guide/dev/README.ms.md)
- [`guide/dev/testing-smoke.ms.md`](guide/dev/testing-smoke.ms.md)

## Lesen

Projek ini dilesenkan di bawah syarat dalam [LICENSE](./LICENSE).
