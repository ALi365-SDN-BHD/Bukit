# Panduan Pengguna Bukit

Versi bahasa: [English](./README.md) | [简体中文](./README.zh-CN.md) | Bahasa Melayu (semasa)

Direktori ini untuk pengguna tapak (bukan penyelenggara enjin). Ia membantu anda membina dan mendeploy tapak statik daripada kandungan Markdown/Notion, termasuk konfigurasi dan penyelesaian masalah yang biasa.

Jika anda perlukan butiran dalaman, titik pengembangan, atau mahu menyumbang kod, rujuk panduan pembangun: [guide/dev](../dev/README.ms.md).

## Pilih Laluan Anda

| Matlamat | Mula di sini | Kemudian baca |
|---|---|---|
| Cuba Bukit secara setempat | [01 Permulaan Pantas](./01-quick-start.ms.md) | [12 Rujukan CLI](./12-cli-reference.ms.md) |
| Guna Notion sebagai CMS | [06 Kandungan Notion](./06-notion-content.ms.md) | [13 Terap GitHub Pages](./13-deploy-github-pages.ms.md) |
| Bina laman korporat | [07 Pelbagai Sumber](./07-multi-source.ms.md) + [09 Modules](./09-modules-data.ms.md) | [08 Tema & Templat](./08-themes-templates.ms.md) |
| Bina dengan AI / ChatGPT | [ChatGPT Prompt Pack](../ai/chatgpt/README.ms.md) | [Intent CLI](../dev/intent-cli.ms.md) |
| Terapkan tapak | [13 Terap GitHub Pages](./13-deploy-github-pages.ms.md) | [14 Penyelesaian Masalah](./14-troubleshooting.ms.md) |
| Optimum untuk SEO/GEO | [11 I18n & SEO](./11-i18n-seo.ms.md) + [17 GEO](./17-geo.ms.md) | [12 Rujukan CLI](./12-cli-reference.ms.md) |
| Bangunkan tema | [08 Tema & Templat](./08-themes-templates.ms.md) | [Tema (dev)](../dev/theme.ms.md) |
| Selesai masalah binaan | [14 Penyelesaian Masalah](./14-troubleshooting.ms.md) | [Doctor (dev)](../dev/doctor.ms.md) |

## Laluan Bacaan Disyorkan

### Kali pertama bermula (Markdown tempatan)

1. [01 Permulaan Pantas](./01-quick-start.ms.md)
2. [04 Konfigurasi YAML Tapak](./04-site-yaml-config.ms.md)
3. [05 Kandungan Markdown](./05-markdown-content.md) (pada masa ini hanya tersedia dalam bahasa Inggeris dan Cina)
4. [12 Rujukan CLI](./12-cli-reference.ms.md)
5. [13 Terap GitHub Pages](./13-deploy-github-pages.ms.md)

### Guna Notion sebagai CMS

1. [01 Permulaan Pantas](./01-quick-start.ms.md)
2. [06 Kandungan Notion](./06-notion-content.ms.md)
3. [10 Ciri Terbina & Output](./10-built-in-features.ms.md)
4. [13 Terap GitHub Pages](./13-deploy-github-pages.ms.md)
5. [14 Penyelesaian Masalah](./14-troubleshooting.ms.md)

### Laman korporat / landing page (data Modules)

1. [07 Pelbagai Sumber](./07-multi-source.ms.md)
2. [09 Modul Data Berstruktur](./09-modules-data.ms.md)
3. [08 Tema & Templat](./08-themes-templates.ms.md)
4. [15 Resipi](./15-recipes.ms.md)

### Bina tapak secara perbualan (ChatGPT / GPT rasmi)

1. Prompt Pack: [ai/chatgpt](../ai/chatgpt/README.ms.md)
2. Kontrak Intent (AI ↔ Bukit): [guide/dev/intent-cli](../dev/intent-cli.ms.md)
3. Perintah wajib (`validate/doctor/build`): [12 Rujukan CLI](./12-cli-reference.ms.md)

### Pengoptimuman untuk enjin carian AI (GEO)

1. [11 I18n & SEO](./11-i18n-seo.ms.md) (asas SEO tradisional)
2. [17 GEO](./17-geo.ms.md) (llms.txt, perangkak AI, data berstruktur FAQ/HowTo)
3. [12 Rujukan CLI](./12-cli-reference.ms.md) (untuk `bukit geo audit`)

### Klon reka bentuk laman web

1. [18 Klon Laman Web](./18-clone-website.ms.md) (pengekstrakan pelayar → penjanaan tema)
2. [08 Tema & Templat](./08-themes-templates.ms.md) (penyesuaian tema)
3. [12 Rujukan CLI](./12-cli-reference.ms.md) (untuk `bukit clone`)

### Teroka ciri v3.0

1. [19 Ciri Baharu v3.0](./19-new-features-v3.ms.md) (feed, sitemap terperinci, UI carian, naik taraf taxonomy, kandungan berkaitan, menu, fail data, alias, pemprosesan imej)
2. [10 Ciri Terbina & Output](./10-built-in-features.ms.md)
3. [04 Konfigurasi YAML Tapak](./04-site-yaml-config.ms.md)

### Laman berbilang bahasa

1. [11 I18n & SEO](./11-i18n-seo.ms.md) (persediaan i18n, penandaan bahasa, gabung sitemap)
2. [04 Konfigurasi YAML Tapak](./04-site-yaml-config.ms.md) (konfigurasi `site.languages`)
3. [12 Rujukan CLI](./12-cli-reference.ms.md)

## Jika Anda Menggunakan Bukit Melalui AI / Agent

Jika anda menggunakan Bukit dalam persekitaran yang menyokong skill seperti Trae, Claude Code, Copilot CLI, Codex CLI, atau Gemini CLI, anggap `src/skills/` sebagai pintu masuk navigasi untuk agent dan direktori ini sebagai panduan operasi untuk pengguna.

- Gambaran keseluruhan agent skills: [`src/skills`](../../src/skills/README.ms.md)
- Pintu masuk utama: [`using-bukit`](../../src/skills/using-bukit/SKILL.md)
- Rujukan pelaksanaan arahan: [`bukit-cli-reference`](../../src/skills/bukit-cli-reference/SKILL.md)
- Panduan pengguna ini masih merangkumi laluan operasi penuh untuk persediaan, konfigurasi, tema, susunan kandungan, deployment, dan penyelesaian masalah

## Contoh Boleh Jalan Dalam Repositori

Kebanyakan contoh dalam panduan ini mempunyai versi boleh jalan dalam `examples/starter/`:

- Konfigurasi Markdown minimum: [examples/starter/site.yaml](../../examples/starter/site.yaml)
- Konfigurasi pelbagai bahasa: [examples/starter/site.i18n.yaml](../../examples/starter/site.i18n.yaml)
- Konfigurasi Modules (`mode=data`): [examples/starter/site.modules.yaml](../../examples/starter/site.modules.yaml)
- Data olok-olok Modules: [examples/starter/data](../../examples/starter/data)
- Contoh multi-site: [examples/starter/sites](../../examples/starter/sites)

## Rujukan Silang ke Dokumen Pembangun

Untuk sempadan medan dan kekangan implementasi yang lebih autoritatif, rujuk:

- Tingkah laku CLI: [guide/dev/cli](../dev/cli.ms.md)
- Kontrak `site.yaml`: [guide/dev/config-site-yaml](../dev/config-site-yaml.ms.md)
- Pemodelan kandungan: [guide/dev/content](../dev/content.ms.md)
- Dalaman tema/templat: [guide/dev/theme](../dev/theme.ms.md), [guide/dev/rendering-scriban](../dev/rendering-scriban.ms.md)
- Peraturan suntikan Modules: [guide/dev/modules-data](../dev/modules-data.ms.md)
- Output terbina dalam dan plugin: [guide/dev/built-in-plugins](../dev/built-in-plugins.ms.md), [guide/dev/plugins](../dev/plugins.ms.md)

## Istilah Pantas

- Konfigurasi tapak: `site.yaml` (atau `sites/<name>.yaml` untuk multi-site).
- Content sources: `content.sources[]` membaca kandungan daripada Markdown/Notion.
- Page/Post: ditentukan oleh `collection: page|post` (atau medan `Collection` di Notion).
- Theme: templat + aset + direktori statik.
- Data Modules: dimuatkan melalui `content.sources[].mode: data`; hanya disuntik ke `site.modules.*`.
- Output terbina dalam: `sitemap.xml`, `rss.xml`, `search.json`, dan lain-lain.

Rujukan kanonik: [README.zh-CN.md](./README.zh-CN.md)
