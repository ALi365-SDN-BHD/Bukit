# 14 Penyelesaian Masalah: Doctor Dahulu, Diagnosis Mengikut Gejala

Apabila anda menghadapi masalah, jangan meneka dahulu. Ikuti urutan ini untuk diagnosis:

1. `doctor` (semakan kendiri konfigurasi/persekitaran)
2. `build --clean` (hapuskan kesan cache tokokan)
3. Bandingkan dengan `examples/starter/` (cari "garis asas yang berfungsi")

Dokumen penyelesaian masalah berorientasikan pembangun: [guide/dev/doctor](../dev/doctor.ms.md), [guide/dev/cache-clean](../dev/cache-clean.ms.md).

## Rujukan Perintah Pantas

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c Release -- clean --dir dist
dotnet run --project src/Bukit.Cli -c Release -- preview --dir dist --port auto
```

## Gejala 1: doctor Gagal Serta-merta (Pengesahan Konfigurasi)

### A) Token Notion hilang

Gejala: Meminta bahawa `NOTION_TOKEN` hilang atau konfigurasi berkaitan Notion tidak tersedia.

Pembaikan:

- Setempat: tetapkan pembolehubah persekitaran `NOTION_TOKEN`
- CI: suntik melalui GitHub Actions Secrets (lihat: [13 Terap GitHub Pages](./13-deploy-github-pages.ms.md))

### B) Laluan tidak wujud (content/theme/build output)

Gejala: Meminta bahawa direktori tidak wujud (contohnya, `content`, `layouts`, `assets`).

Senarai semak pembaikan:

- Sahkan direktori benar-benar wujud
- Sahkan anda memahami "asas laluan relatif" (relatif kepada direktori yang mengandungi `site.yaml`), lihat: [03 Struktur Projek](./03-project-structure.ms.md)
- Jika anda menggunakan `--config path/to/site.yaml`, pastikan direktori yang sepadan juga berada di bawah direktori konfigurasi tersebut

### C) Jenis medan salah (Struktur YAML tidak sepadan)

Kesilapan tipikal:

- Menulis senarai sebagai rentetan (contohnya, `languages: zh-CN` dan bukannya `languages: [zh-CN]`)
- Kesilapan inden menyebabkan salah jajaran struktur

Pembaikan:

- Bandingkan dahulu dengan `examples/starter/site.yaml`, `examples/starter/site.i18n.yaml`
- Kemudian betulkan mengikut [04 Konfigurasi YAML Tapak](./04-site-yaml-config.ms.md)

### D) Konflik laluan dikesan

Gejala: `doctor` atau `build` gagal dengan `Route conflict on url` atau `Route conflict on outputPath`.

Senarai semak pembaikan:
- Dua halaman kandungan mempunyai slug yang sama → namakan semula slug atau gunakan laluan koleksi berbeza
- Dua halaman kandungan mempunyai `route.outputPath` yang sama → pastikan keunikan
- URL halaman kandungan bertembung dengan halaman terbitan (pagination/arkib/taksonomi) → tukar `deriveConflictPolicy` ke `warn` atau `last-wins`, atau laraskan URL yang bertembung

Jalankan `bukit doctor` dahulu untuk mengesan konflik tanpa binaan penuh.

## Gejala 2: build Berjaya, tetapi Halaman Hilang / URL Salah

### A) Perubahan slug/type menyebabkan perubahan laluan

Gejala: Anda fikir halaman berada di `/pages/about/`, tetapi ia sebenarnya dikeluarkan ke tempat lain.

Pembaikan:

- Sahkan `type` dan `slug` kandungan
- Jangan gunakan medan tindihan `route/url/outputPath/template` secara sambil lewa (melainkan anda benar-benar tahu laluan output)

### B) Penapisan pelbagai bahasa mengecualikan kandungan

Gejala: Selepas mendayakan `languages` di laman, kandungan tertentu "hilang" dalam bahasa tertentu.

Pembaikan:

- Tambah `language` pada setiap kandungan
- Periksa bahawa nilai bahasa adalah betul-betul konsisten (`en-US` jangan ditulis sebagai `en`)

Lihat: [11 Pelbagai Bahasa & SEO](./11-i18n-seo.ms.md).

## Gejala 3: 404 Selepas Penyahgunaan (Pratonton Setempat Berfungsi dengan Baik)

### A) baseUrl salah konfigurasi (paling lazim untuk repositori projek)

Gejala:

- Halaman utama boleh dibuka, tetapi CSS/imej 404
- Atau pautan dalaman laman 404 selepas diklik

Pembaikan:

- Repositori projek mesti menetapkan `baseUrl: /<repo>`
- Semasa bina, disyorkan untuk menindih melalui CLI: `--base-url /<repo> --site-url https://<owner>.github.io/<repo>`

Lihat: [13 Terap GitHub Pages](./13-deploy-github-pages.ms.md).

### B) Direktori muat naik salah

Gejala: Penyahgunaan GitHub Pages berjaya, tetapi kandungan kosong.

Pembaikan:

- Sahkan `path` `upload-pages-artifact` aliran kerja menunjuk ke direktori output sebenar (contohnya, `_site`)

## Gejala 4: Port Pratonton Diduduki atau Tidak Boleh Dibuka

Pembaikan:

- Gunakan `--port auto` untuk auto-pilih port
- Atau tukar ke port lain: `--port 4174`
- Jika anda memerlukan port tetap tetapi ia diduduki, hentikan proses yang menduduki port tersebut dahulu

## Gejala 5: Kandungan/Templat Diubah, tetapi Output Tidak Berubah

Utamakan "kaedah penghapusan":

1. `build --clean` (pastikan direktori output dibersihkan)
2. Lumpuhkan tokokan buat sementara: `--no-incremental`
3. Bersihkan direktori cache: direktori yang ditunjuk oleh `--cache-dir` (lalai `.cache`) atau jalankan `clean`

Jika anda benar-benar bergantung pada binaan tokokan untuk kelajuan, disyorkan untuk menjalankan laman dahulu, kemudian dayakan tokokan secara beransur-ansur.

## Gejala 6: Modules (data) Tidak Berkesan

Gejala:

- `site.modules.*` kosong
- Halaman utama tidak memaparkan modul banner/faq dll.

Senarai semak diagnosis:

- Dalam sources, adakah modules ditetapkan kepada `mode: data`?
- Adakah data modul mengandungi `type` (menentukan kunci pengelompokan)?
- Adakah templat tema membaca `site.modules` (bandingkan dengan tema contoh)?

Lihat: [09 Modul Data Berstruktur](./09-modules-data.ms.md).

## Simptom 7: Clean Enggan Memadam Direktori Output

Simptom:

- `build --clean` gagal dengan "output directory clean refused"
- Direktori output tidak dipadamkan

Punca: Bukit kini memerlukan fail `.bukit-output-marker` dalam direktori output sebelum membersihkan. Ini menghalang pemadaman tidak sengaja direktori bukan Bukit (contohnya, akar projek, direktori home, `.git`).

Pembaikan:

- Jika direktori dicipta oleh Bukit: jalankan binaan penuh dahulu (ia menulis marker), kemudian clean.
- Jika direktori bukan output Bukit: padam secara manual atau pilih direktori output lain.
- Jika anda menetapkan `build.output` ke direktori bukan Bukit sedia ada: tukar `build.output` ke direktori khusus.

## Simptom 8: Had stdout/stderr Plugin Melebihi

Simptom:

- Binaan gagal dengan "stdout limit exceeded" atau "stderr limit exceeded"
- Proses plugin luaran dibunuh semasa binaan

Punca: Plugin luaran menghasilkan lebih banyak output daripada had `maxStdoutBytes` atau `maxStderrBytes` yang dikonfigurasi.

Pembaikan:

- Tingkatkan had dalam `site.externalPlugins.<name>.maxStdoutBytes` / `maxStderrBytes`.
- Atau padam medan konfigurasi untuk membenarkan output tanpa had.
- Siasat mengapa plugin menghasilkan output berlebihan — ia mungkin menunjukkan pepijat.

## Simptom 9: Ketidakpadanan Commit Kunci Tema

Simptom:

- Binaan gagal dengan "Theme lock mismatch for ... locked commit ..., current commit ..."
- Tema jauh yang sebelum ini berfungsi kini gagal

Punca: Tema jauh (`theme.source`) sebelum ini dibina dan dikunci ke commit Git tertentu. Tema yang dicache kini mempunyai commit berbeza daripada yang direkodkan dalam `bukit-theme.lock.json`.

Pembaikan:

- Padam direktori cache setempat tema dan fail kunci, kemudian bina semula untuk mengklon semula.
- Atau padam hanya fail kunci untuk memaksa pengesahan semula.
- Jika anda sengaja mengemas kini tema, fail kunci perlu dijana semula.

