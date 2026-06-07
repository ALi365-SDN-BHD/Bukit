# Panduan Pembangun Bukit (Penyelenggaraan dan Pengembangan)

Versi bahasa: [English](./README.md) | [简体中文](./README.zh-CN.md) | Bahasa Melayu (semasa)

Direktori ini ditujukan kepada penyelenggara dan penyumbang. Ia menerangkan kontrak stabil Bukit seperti konfigurasi, parameter, dan model data, serta butiran implementasi seperti pipeline build, incremental build, dan pemuatan plugin supaya iterasi boleh dibuat dengan lebih selamat dan pantas.

## Laluan Pengenalan Paling Cepat

1. Jalankan dahulu aliran contoh tapak: rujuk [CLI](./cli.ms.md)
2. Fahami medan `site.yaml` dan peraturan validasi: rujuk [Konfigurasi](./config-site-yaml.ms.md)
3. Bina gambaran hujung ke hujung: Config → Content → Routing → Rendering → Plugins → Output, rujuk [Architecture](./architecture.ms.md)

## Peta Tugas Penyelenggara

| Tugas | Titik masuk |
|---|---|
| Ubah tingkah laku CLI | [Rujukan argumen CLI](./cli.ms.md) |
| Ubah skema `site.yaml` | [Rujukan medan `site.yaml`](./config-site-yaml.ms.md) |
| Ubah model kandungan | [Sistem kandungan](./content.ms.md) |
| Ubah routing | [Sistem routing](./routing.ms.md) |
| Ubah rendering / Scriban | [Rendering (Scriban)](./rendering-scriban.ms.md) |
| Ubah tingkah laku tema | [Pembangunan tema](./theme.ms.md), [Sumber tema](./theme-source.ms.md) |
| Ubah plugin | [Sistem plugin](./plugins.ms.md), [Plugin terbina dalam](./built-in-plugins.ms.md) |
| Ubah penyedia Notion | [Sistem kandungan](./content.ms.md) (bahagian Notion) |
| Ubah AOT / prestasi | [AOT](./aot.ms.md), [Nota prestasi](./perf-aot-governance.ms.md) |
| Ubah proses terbitan / dokumen | [Tadbir urus dokumentasi](./documentation-governance.ms.md), [Senarai semak terbitan](./release-checklist.ms.md) |

## Jika Anda Menyelenggara Bukit Melalui AI / Agent

Jika anda menyelenggara Bukit dalam persekitaran yang menyokong skill seperti Trae, Claude Code, Copilot CLI, Codex CLI, atau Gemini CLI, gunakan `src/skills/` sebagai lapisan navigasi untuk agent dan direktori ini sebagai rujukan kontrak serta implementasi untuk penyelenggara.

- Gambaran keseluruhan agent skills: [`src/skills`](../../src/skills/README.ms.md)
- Pintu masuk utama: [`using-bukit`](../../src/skills/using-bukit/SKILL.md)
- Rujukan pelaksanaan arahan: [`bukit-cli-reference`](../../src/skills/bukit-cli-reference/SKILL.md)
- Direktori ini kekal menjadi rujukan untuk seni bina, kontrak konfigurasi, butiran rendering, plugin, kebolehcerapan, pengujian, dan tadbir urus operasi

## Navigasi

- [Code Wiki (gambaran keseluruhan repositori)](./code-wiki.ms.md)
- [Graf panggilan modul](./code-wiki-call-graph.ms.md)
- [Laluan onboarding 30 minit untuk pembangun baharu](./new-developer-30min.ms.md)
- [Titik masuk mengikut jenis perubahan](./maintainer-entrypoints.ms.md)
- [Draf semakan seni bina](./architecture-review.ms.md)
- [Seni bina dan sempadan modul](./architecture.ms.md)
- [Senarai semak tadbir urus penyelenggaraan](./governance-checklist.ms.md)
- [Rujukan argumen CLI](./cli.ms.md)
- [Rujukan medan `site.yaml`](./config-site-yaml.ms.md)
- [Perancah Init/Create](./init-create.ms.md)
- [Sistem kandungan (Markdown / Notion / sources)](./content.ms.md)
- [Sistem routing](./routing.ms.md)
- [Rendering dan templat (Scriban)](./rendering-scriban.ms.md)
- [Pembangunan tema](./theme.ms.md)
- [Sumber tema Git](./theme-source.ms.md)
- [Sumber data Modules (`mode=data`)](./modules-data.ms.md)
- [Output tetap enjin](./engine-outputs.ms.md)
- [Sistem plugin](./plugins.ms.md)
- [Output plugin terbina dalam dan sempadan](./built-in-plugins.ms.md)
- [Integrasi Intent CLI](./intent-cli.ms.md)
- [Mod build AOT dan bukan AOT](./aot.ms.md)
- [Nota prestasi / AOT / tadbir urus](./perf-aot-governance.ms.md)
- [Terbit dan deploy](./publish-deploy.ms.md)
- [Incremental build](./incremental-build.ms.md)
- [Cache dan clean](./cache-clean.ms.md)
- [Semakan Doctor](./doctor.ms.md)
- [Kebolehcerapan (log dan metrik)](./observability.ms.md)
- [I18n dan SEO](./i18n-seo.ms.md)
- [Pencetus webhook dan kekangan keselamatan](./webhook.ms.md)
- [Pengujian dan smoke acceptance](./testing-smoke.ms.md)
- [Tadbir urus dokumentasi](./documentation-governance.ms.md)
- [Senarai semak terbitan](./release-checklist.ms.md)
- [Skop pratonton awam](./public-preview-scope.ms.md)

## Cara Menggunakan Dokumen Lain Dalam Repositori Ini

Direktori `docs/` lebih tertumpu pada topik produk, cadangan, penerimaan, dan proses tadbir urus. Direktori ini bertindak sebagai pintu masuk pembangun dan akan merujuk kepada bahan tersebut daripada menduplikasi kandungannya.

Kemasukan biasa:

- Panduan bina tapak dengan AI: [chatgpt/README.ms.md](../ai/chatgpt/README.ms.md)
- Kontrak dan pemetaan Intent: [intent-cli.ms.md](./intent-cli.ms.md)
- Skema Notion dan model kandungan: [content.ms.md](./content.ms.md)
- Pemodelan Modules untuk laman korporat: [modules-data.ms.md](./modules-data.ms.md)
- Dokumen acceptance dan smoke: [testing-smoke.ms.md](./testing-smoke.ms.md)

## Konsep Pantas

- `ContentDocument`: model kandungan bersatu yang datang daripada Markdown atau Notion
- Record dan Fields: Record mengawal tingkah laku enjin, manakala Fields digunakan oleh templat (`page.fields.*.value`)
- `mode=content` dan `mode=data`: yang pertama menjana route dan halaman, yang kedua hanya menyuntik data ke `site.modules`
- Kitar hayat plugin: cangkuk utama semasa ialah `derive-pages` dan `after-build`

Rujukan kanonik: [README.zh-CN.md](./README.zh-CN.md)
