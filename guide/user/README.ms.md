# Panduan Pengguna Bukit

Versi bahasa: [English](./README.md) | [简体中文](./README.zh-CN.md) | Bahasa Melayu (semasa)

Direktori ini untuk pengguna tapak (bukan penyelenggara enjin). Ia membantu anda membina dan mendeploy tapak statik daripada kandungan Markdown/Notion, termasuk konfigurasi dan penyelesaian masalah yang biasa.

Jika anda perlukan butiran dalaman, titik pengembangan, atau mahu menyumbang kod, rujuk panduan pembangun: [guide/dev](../dev/README.ms.md).

## Laluan Bacaan Disyorkan

### Kali pertama bermula (Markdown tempatan)

1. [01-快速开始](./01-快速开始.md)
2. [04-配置-site-yaml](./04-配置-site-yaml.md)
3. [05-内容-Markdown](./05-内容-Markdown.md)
4. [12-命令行参考](./12-命令行参考.md)
5. [13-部署-GitHub-Pages](./13-部署-GitHub-Pages.md)

### Guna Notion sebagai CMS

1. [01-快速开始](./01-快速开始.md)
2. [06-内容-Notion](./06-内容-Notion.md)
3. [10-内置功能与输出](./10-内置功能与输出.md)
4. [13-部署-GitHub-Pages](./13-部署-GitHub-Pages.md)
5. [14-故障排查](./14-故障排查.md)

### Laman korporat / landing page (data Modules)

1. [07-内容-多源-sources](./07-内容-多源-sources.md)
2. [09-Modules-结构化数据](./09-Modules-结构化数据.md)
3. [08-主题与模板](./08-主题与模板.md)
4. [15-场景化示例（Recipes）](./15-场景化示例（Recipes）.md)

### Bina tapak secara perbualan (ChatGPT / GPT rasmi)

1. Prompt Pack: [ai/chatgpt](../ai/chatgpt/README.ms.md)
2. Kontrak Intent (AI ↔ Bukit): [guide/dev/intent-cli](../dev/intent-cli.md)
3. Perintah wajib (`validate/doctor/build`): [12-命令行参考](./12-命令行参考.md)

## Contoh Boleh Jalan Dalam Repositori

Kebanyakan contoh dalam panduan ini mempunyai versi boleh jalan dalam `examples/starter/`:

- Konfigurasi Markdown minimum: [examples/starter/site.yaml](../../examples/starter/site.yaml)
- Konfigurasi pelbagai bahasa: [examples/starter/site.i18n.yaml](../../examples/starter/site.i18n.yaml)
- Konfigurasi Modules (`mode=data`): [examples/starter/site.modules.yaml](../../examples/starter/site.modules.yaml)
- Data olok-olok Modules: [examples/starter/data](../../examples/starter/data)
- Contoh multi-site: [examples/starter/sites](../../examples/starter/sites)

## Rujukan Silang ke Dokumen Pembangun

Untuk sempadan medan dan kekangan implementasi yang lebih autoritatif, rujuk:

- Tingkah laku CLI: [guide/dev/cli](../dev/cli.md)
- Kontrak `site.yaml`: [guide/dev/config-site-yaml](../dev/config-site-yaml.md)
- Pemodelan kandungan: [guide/dev/content](../dev/content.md)
- Dalaman tema/templat: [guide/dev/theme](../dev/theme.md), [guide/dev/rendering-scriban](../dev/rendering-scriban.md)
- Peraturan suntikan Modules: [guide/dev/modules-data](../dev/modules-data.md)
- Output terbina dalam dan plugin: [guide/dev/built-in-plugins](../dev/built-in-plugins.md), [guide/dev/plugins](../dev/plugins.md)

## Istilah Pantas

- Konfigurasi tapak: `site.yaml` (atau `sites/<name>.yaml` untuk multi-site).
- Content provider: membaca kandungan daripada Markdown/Notion.
- Page/Post: ditentukan oleh `type: page|post` (atau medan `Type` di Notion).
- Theme: templat + aset + direktori statik.
- Data Modules: dimuatkan melalui `content.sources[].mode: data`; hanya disuntik ke `site.modules.*`.
- Output terbina dalam: `sitemap.xml`, `rss.xml`, `search.json`, dan lain-lain.

Sumber penuh bahasa Cina: [README.md](./README.md)
