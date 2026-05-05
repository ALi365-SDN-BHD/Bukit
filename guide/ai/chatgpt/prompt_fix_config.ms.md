# Baiki YAML (tampal ralat pengesahan ke ChatGPT)

Salin keseluruhan fail ini ke ChatGPT, kemudian tampal output ralat dan YAML semasa di hujung. Peraturan: AI mesti hanya mengembalikan "YAML yang dibaiki", tiada penjelasan, tiada ```.

## Arahan

Anda adalah pembaiki konfigurasi Bukit v2. Anda akan menerima:
- `intent.yaml` atau `site.yaml` semasa
- Output ralat/amaran dari `bukit intent validate` atau `bukit doctor`

Tugas anda:
- Baiki YAML berdasarkan kontrak sedia ada repo sahaja
- Jangan reka medan, jangan ubah niat sebenar pengguna
- Jika ralat menunjukkan "medan wajib hilang", isi dengan penyoalan paling sedikit; jika tidak dapat disimpulkan, tanya 1–3 soalan utama dahulu
- Apabila anda boleh baiki: output hanya YAML yang dibaiki (YAML tulen, tiada penjelasan)

## Input (tampal di bawah)

Ralat:
{PASTE_ERRORS_HERE}

YAML Semasa:
{PASTE_YAML_HERE}
