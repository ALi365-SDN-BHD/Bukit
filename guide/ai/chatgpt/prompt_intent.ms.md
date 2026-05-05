# Jana intent.yaml (laluan disyorkan)

Salin keseluruhan fail ini bermula dari "Keperluan Pengguna" ke ChatGPT, isikan pemegang tempat. Peraturan: jika maklumat tidak mencukupi, tanya dahulu; setelah lengkap, output hanya YAML (tiada ```).

## Keperluan Pengguna (ganti pemegang tempat dengan maklumat sebenar anda)

Saya mahu membina tapak statik menggunakan Bukit v2. Sila mulakan dengan bertanya 5–10 soalan untuk menjelaskan maklumat yang hilang (jangan output YAML). Selepas saya menjawab, output hanya `intent.yaml` (YAML tulen, tiada penjelasan), memastikan ia lulus `bukit intent validate`.

Ringkasan:
- Jenis tapak: {blog/docs/company/landing/others}
- Bahasa: {tunggal/pelbagai}
- Penerapan: {GitHub Pages/domain tersuai/others}
- base_url: {"/" atau "/my-repo"}
- Sumber kandungan (Intent hanya menyokong satu): {markdown/notion}
- Tema: theme.name {starter/alt/...}

Output hanya YAML; tiada pagar Markdown (```) atau penjelasan. Jangan reka medan yang tidak wujud dalam repo.

## Templat intent.yaml

site:
  name: "{site_name}"
  title: "{site_title}"
  base_url: "{base_url}"
  url: "{optional_site_url}"
  language: "{optional_single_language}"

languages:
  default: "{default_language}"
  supported: [{supported_languages}]

content:
  provider: "{markdown|notion}"
  markdown:
    dir: "{content_dir}"
  notion:
    database_id: "{database_id}"
    field_policy:
      mode: "{whitelist|all}"
      allowed: [{allowed_fields}]

theme:
  name: "{theme_name}"
  params: {}
