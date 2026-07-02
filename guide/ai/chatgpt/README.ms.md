# ChatGPT + Bukit: Prompt Pack Pembinaan Tapak Secara Perbualan

Versi bahasa: [English](./README.md) | [简体中文](./README.zh-CN.md) | Bahasa Melayu (semasa)

Direktori ini menyediakan prompt dan arahan yang boleh terus salin-tampal untuk memindahkan pintu masuk pembinaan tapak ke ChatGPT. AI menjana `intent.yaml` (disyorkan) atau `site.yaml`, manakala Bukit menjalankan validasi dan binaan secara deterministik.

## Dua Mod Penggunaan

### Mod A: Guna terus dalam perbualan ChatGPT

1. Tampal [system_instructions.md](./system_instructions.md) ke arahan sistem ChatGPT (atau mesej pertama perbualan).
2. Pilih laluan output:
   - Disyorkan: guna [prompt_intent.md](./prompt_intent.md) untuk menjana `intent.yaml`.
   - Laluan konfigurasi terus: guna [prompt_site_yaml.md](./prompt_site_yaml.md) untuk menjana `site.yaml`.
3. Jalankan gelung tempatan:

```bash
dotnet run --project src/Bukit-Core/Bukit.Cli -c Release -- intent validate intent.yaml
dotnet run --project src/Bukit-Core/Bukit.Cli -c Release -- intent apply intent.yaml --out site.yaml
dotnet run --project src/Bukit-Core/Bukit.Cli -c Release -- doctor --config site.yaml
dotnet run --project src/Bukit-Core/Bukit.Cli -c Release -- build --config site.yaml --clean --site-url https://example.com
```

4. Jika `intent validate` atau `doctor` gagal, tampal output ralat kepada ChatGPT dan guna [prompt_fix_config.md](./prompt_fix_config.md) untuk minta YAML yang telah diperbaiki sahaja.

### Mod B: Bina "Bukit Official GPT"

1. Tetapkan arahan GPT menggunakan [system_instructions.md](./system_instructions.md).
2. Tambah fail pengetahuan GPT yang disenaraikan dalam [knowledge_manifest.md](./knowledge_manifest.md).
3. Untuk penggunaan harian, utamakan output `intent.yaml`, kemudian jalankan `intent validate/apply`.

## Keselamatan dan Validasi

Konfigurasi yang dijana AI mesti divalidasi sebelum digunakan.

1. Utamakan `intent.yaml` berbanding penjanaan terus `site.yaml`.
2. Sentiasa jalankan gelung validasi:
   ```bash
   intent validate intent.yaml
   intent apply intent.yaml --out site.yaml
   doctor --config site.yaml
   build --config site.yaml --clean
   ```
3. Jika validasi gagal, tampal hanya output ralat kepada ChatGPT.
4. Jangan sekali-kali tampal kunci rahsia ke dalam ChatGPT. Sentiasa guna pemboleh ubah persekitaran:
   - `NOTION_TOKEN` untuk akses kandungan Notion
   - GitHub Secrets untuk deployment CI/CD
5. Jangan minta AI menjana token, kunci, laluan fail mutlak, atau arahan shell yang tidak disahkan.

## Keperluan Minimum Sebelum Menjalankan

- Untuk kandungan Notion: tetapkan `NOTION_TOKEN` sebagai pemboleh ubah persekitaran (jangan tampal ke dalam chat).
- Untuk sublaluan GitHub Pages: `site.baseUrl` mesti bermula dengan `/`, contohnya `/my-repo`; laluan root menggunakan `/`.

Rujukan kanonik: [README.zh-CN.md](./README.zh-CN.md)
