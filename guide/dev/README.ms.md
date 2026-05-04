# Panduan Pembangun Bukit (Penyelenggaraan dan Pengembangan)

Versi bahasa: [English](./README.md) | [简体中文](./README.zh-CN.md) | Bahasa Melayu (semasa)

Direktori ini untuk penyelenggara dan penyumbang kod. Ia menerangkan kontrak stabil (konfigurasi/parameter/model data) serta butiran implementasi (pipeline/incremental build/pemuatan plugin) supaya iterasi lebih selamat dan pantas.

## Laluan Onboarding Terpendek

1. Jalankan contoh tapak dahulu (rujuk [CLI](./cli.md)).
2. Fahami medan dan validasi `site.yaml` (rujuk [Config](./config-site-yaml.md)).
3. Fahami aliran hujung-ke-hujung: Config → Content → Routing → Rendering → Plugins → Output (rujuk [Architecture](./architecture.md)).

## Navigasi Dokumen

- [Code Wiki (gambaran keseluruhan repositori)](./code-wiki.md)
- [Graf panggilan modul](./code-wiki-call-graph.md)
- [Onboarding 30 minit untuk pembangun baharu](./new-developer-30min.md)
- [Pintu masuk kod ikut jenis perubahan](./maintainer-entrypoints.md)
- [Draf semakan seni bina](./architecture-review.md)
- [Seni bina dan sempadan modul](./architecture.md)
- [Rujukan argumen CLI](./cli.md)
- [Rujukan medan `site.yaml`](./config-site-yaml.md)
- [Inisialisasi scaffolding (init/create)](./init-create.md)
- [Sistem kandungan (Markdown / Notion / sources)](./content.md)
- [Sistem routing](./routing.md)
- [Rendering dan templat (Scriban)](./rendering-scriban.md)
- [Pembangunan tema](./theme.md)
- [Sumber data Modules (`mode=data`)](./modules-data.md)
- [Output tetap enjin](./engine-outputs.md)
- [Sistem plugin](./plugins.md)
- [Output dan sempadan plugin terbina dalam](./built-in-plugins.md)
- [Integrasi CLI Intent](./intent-cli.md)
- [Mod binaan AOT vs bukan AOT](./aot.md)
- [Nota prestasi/AOT/tadbir urus](./perf-aot-governance.md)
- [Publish dan deploy](./publish-deploy.md)
- [Incremental build](./incremental-build.md)
- [Cache dan clean](./cache-clean.md)
- [Pemeriksaan doctor](./doctor.md)
- [Keterlihatan (log dan metrik)](./observability.md)
- [I18n dan SEO](./i18n-seo.md)
- [Webhook trigger dan kekangan keselamatan](./webhook.md)
- [Ujian dan smoke acceptance](./testing-smoke.md)

## Cara Guna Dokumen Lain Dalam Repositori

Direktori `docs/` menumpukan topik produk/cadangan/penerimaan. Direktori `guide/dev` merujuk ke dokumen tersebut tanpa menggandakan kandungan.

Pautan lazim:

- Panduan bina tapak dengan AI: [chatgpt/README.ms.md](../ai/chatgpt/README.ms.md)
- Kontrak dan pemetaan Intent: [intent-cli.md](./intent-cli.md)
- Templat skema Notion: [content.md](./content.md)
- Pemodelan Modules laman korporat: [modules-data.md](./modules-data.md)
- Dokumen penerimaan: [testing-smoke.md](./testing-smoke.md)

## Konsep Pantas

- `ContentItem`: model kandungan seragam daripada Markdown atau Notion.
- Meta vs Fields: Meta mengawal tingkah laku enjin; Fields digunakan oleh templat (`page.fields.*.value`).
- `mode=content` vs `mode=data`: content menjana route/halaman; data menyuntik ke `site.modules`.
- Plugin: dua hook kitar hayat (`derive-pages`, `after-build`).

Sumber penuh bahasa Cina: [README.md](./README.md)
