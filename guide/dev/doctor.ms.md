# doctor (Semakan Kendiri Persekitaran dan Konfigurasi)

`bukit doctor` melakukan semakan kendiri konfigurasi, persekitaran, dan ketersambungan.

Pelaksanaan: `src/Bukit.Cli/Commands/DoctorCommand.cs`

## Pemeriksaan Dilakukan
1. Kesahan konfigurasi (penghuraian/pengesahan site.yaml)
2. Integriti direktori tema (layouts/assets/static, templat diperlukan)
3. Pengesahan sintaks templat (penghuraian Scriban bagi templat diperlukan)
4. Ketersambungan Notion (jika menggunakan pembekal Notion)
5. Pra-semakan plugin (boleh laku plugin luaran, kebenaran)
6. Kebolehtulisan direktori output
7. Pemeriksaan konflik laluan (halaman dengan URL/outputPath sama)
8. Pengesahan medan skema kandungan terhadap takrif `site.collections`
9. Pemeriksaan konfigurasi Notion (token, sambungan pangkalan data)
10. **Pemeriksaan ejaan pemboleh ubah templat** (baharu)
    - Melakukan penghuraian AST pada semua templat `.html` di bawah `layouts/`
    - Mengekstrak semua rujukan pemboleh ubah (`page.title`, `site.params.theme`, dll.)
    - Membandingkan silang dengan senarai putih medan yang diketahui
    - Mengeluarkan amaran apabila menemui pemboleh ubah tidak diketahui (tidak gagal, hanya ⚠)
    - Senarai putih ditakrifkan dalam: `src/Bukit.Rendering/Scriban/ScribanModelKnownFields.cs`
11. **Laporan medan tambahan** (baharu)
    - Memeriksa sama ada medan front matter kandungan diisytiharkan dalam skema koleksi
    - Mengeluarkan amaran apabila menemui medan yang tidak diisytiharkan
12. **Output berformat kod diagnostik**
    - Semua ralat konfigurasi menggunakan format stabil `BKT-XXXX`
    - Pelaksanaan: `src/Bukit.Shared/DiagnosticExceptionFormatter.cs`
    - Contoh: `[BKT-0601] Refusing to clean unsafe output directory`

## Penggunaan
`bukit doctor --config site.yaml` atau `bukit doctor --site blog`
