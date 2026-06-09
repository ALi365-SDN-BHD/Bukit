# Skop Kestabilan Bukit Core 1.0

Dokumen ini mentakrifkan komitmen kestabilan untuk Bukit Core 1.0 dan keupayaan yang kekal dalam pratonton.

## Stabil dalam Bukit Core 1.0

| Keupayaan | Penerangan |
|---|---|
| Tapak statik Markdown | Bina dan terapkan tapak dari fail Markdown setempat |
| Tapak kandungan bersandar Notion | Guna pangkalan data Notion sebagai CMS dengan `NOTION_TOKEN` |
| Deployment GitHub Pages | Terapkan output ke GitHub Pages melalui Actions atau CLI |
| Pembangunan tema | Cipta dan sesuaikan tema dengan templat Scriban |
| Validasi SEO/GEO | Output SEO terbina dalam + `bukit geo audit` + llms.txt |
| Konfigurasi bantuan AI | Aliran kerja `intent.yaml` dengan gelung validate/apply/doctor/build |
| Tapak pelbagai bahasa | i18n melalui `site.languages`, sitemap gabung, hreflang |
| Modules (`mode=data`) | Data berstruktur untuk laman korporat (banner, navigasi, FAQ) |
| Plugin luaran (selamat AOT) | Protokol plugin untuk sambungan gaya terbina dalam |
| Binaan incremental | Bendera `--incremental` dengan pelangkauan berasaskan manifest |

## Preview / Next Stage

| Keupayaan | Status |
|---|---|
| Pendaftaran tema | Preview — penemuan dan pengedaran tema tidak diliputi oleh komitmen kestabilan Bukit Core 1.0 |
| Aliran kerja klon-ke-tema | Preview — pengekstrakan pelayar ke penjanaan tema |
| Aliran kerja import html-demo | Preview — import HTML ke penjanaan tema |
| Ekosistem plugin luaran (bukan AOT) | Preview — pemuatan plugin dinamik |
| Automasi AI lanjutan | Preview — saluran paip binaan AI berbilang langkah |
| Panel kawalan setempat BukitJalil | Preview — UI web setempat untuk pengurusan tapak |

## Tidak Termasuk (Bukan dalam Peta Jalan)

| Keupayaan |
|---|
| Platform pengehosan SaaS |
| Editor seret-dan-lepas visual |
| Backend CMS terbina dalam (selain integrasi Notion) |
| Rendering sisi pelayan masa jalan |
| Pelayan pratonton masa nyata |
