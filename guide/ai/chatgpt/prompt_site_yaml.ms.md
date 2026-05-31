# Jana site.yaml (gunakan hanya apabila anda secara eksplisit mahukan "penjanaan konfigurasi terus")

Salin keseluruhan fail ini bermula dari "Keperluan Pengguna" ke ChatGPT, isikan pemegang tempat. Peraturan: jika maklumat tidak mencukupi, tanya dahulu; setelah lengkap, output hanya YAML (tiada ```).

## Keperluan Pengguna

Saya mahu membina tapak statik menggunakan Bukit v2. Sila tanya dahulu untuk mengisi maklumat yang hilang (jangan output YAML). Apabila maklumat lengkap, output hanya `site.yaml` (YAML tulen, tiada penjelasan).

Ringkasan:
- site.name: {starter}
- site.title: {My Site}
- site.baseUrl: {"/" atau "/my-repo"}
- Sumber kandungan: tunggal {markdown|notion} atau pelbagai (content.sources[])
- Pelbagai bahasa: {ya/tidak}
- Tema: theme.name {alt/...}

Output hanya YAML; tiada pagar Markdown. Token Notion tidak boleh muncul, mesti dari pembolehubah persekitaran `NOTION_TOKEN`.

## Templat site.yaml

site:
  name: "{site_name}"
  title: "{site_title}"
  url: "{optional_site_url}"
  baseUrl: "{base_url}"
  language: "{language}"
  languages: [{optional_languages}]
  defaultLanguage: "{optional_default_language}"
  pluginFailMode: strict
  timezone: Asia/Shanghai

content:
  provider: "{markdown|notion|sources}"
  markdown:
    dir: "{content_dir}"
    defaultType: page
  notion:
    databaseId: "{database_id}"
    pageSize: 50
    fieldPolicy:
      mode: whitelist
      allowed: [{allowed_fields}]

build:
  output: dist
  clean: true
  draft: false

theme:
  name: "{theme_name}"
  layouts: layouts
  assets: assets
  static: static
  params: {}

logging:
  level: info

## Coretan content.sources[] (untuk pelbagai sumber / Modules)

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

## Kekangan Keselamatan (Mesti Dipatuhi)

- Jangan sesekali output arahan shell, skrip penerapan, atau laluan fail mutlak.
- Jangan sesekali minta atau terima token, kunci, atau rahsia. Akses Notion mesti sentiasa menggunakan pembolehubah persekitaran `NOTION_TOKEN`.
- Jika pengguna meminta arahan, arahkan mereka ke: `guide/user/12-cli-reference.ms.md`
