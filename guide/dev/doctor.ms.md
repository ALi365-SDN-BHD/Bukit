# doctor (Semakan Kendiri Persekitaran dan Konfigurasi)

`bukit doctor` melakukan semakan kendiri konfigurasi, persekitaran, dan ketersambungan.

Pelaksanaan: `src/Bukit.Cli/Commands/DoctorCommand.cs`

## Pemeriksaan Dilakukan
1. Kesahan konfigurasi (penghuraian/pengesahan site.yaml)
2. Integriti direktori tema (layouts/assets/static, templat diperlukan)
3. Ketersambungan Notion (jika menggunakan pembekal Notion)
4. Pra-semakan plugin (boleh laku plugin luaran, kebenaran)
5. Kebolehtulisan direktori output

## Penggunaan
`bukit doctor --config site.yaml` atau `bukit doctor --site blog`
