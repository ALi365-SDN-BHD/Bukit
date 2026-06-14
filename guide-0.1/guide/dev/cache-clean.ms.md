# Cache dan Pembersihan (cache-dir / .cache / clean)

## Direktori Cache
- Lalai: `<rootDir>/.cache`, tindih dengan `--cache-dir <dir>`
- Kandungan: `build-manifest*.json`, `notion/` (cache render Notion)

## Perintah Pembersihan
- `bukit clean --dir dist` 鈥?Bersihkan direktori output
- `bukit clean --dir dist --clear-cache` 鈥?Juga bersihkan cache
- `bukit clean --clear-cache` 鈥?Bersihkan cache sahaja

`build --clean` membersihkan output sebelum bina; `clean` adalah perintah bebas.

## Perlindungan Penanda Clean (v3.x+)

Sejak v3.x, `build --clean` dan `build.clean: true` memerlukan fail `.bukit-output-marker` dalam direktori output sebelum memadamkannya. Penanda ini ditulis pada setiap binaan yang berjaya:

- Direktori tanpa penanda **tidak akan dibersihkan** — ini mencegah pemadaman tidak sengaja direktori bukan Bukit.
- Bukit juga menolak membersihkan akar projek, direktori home, akar sistem fail, atau direktori `.git`.

Jika clean ditolak:
- Jika direktori dicipta oleh Bukit: jalankan binaan penuh dahulu (ia menulis penanda), kemudian clean.
- Jika direktori bukan output Bukit: padam secara manual atau pilih direktori output lain.

Lihat: [incremental-build.md](./incremental-build.md), [doctor.md](./doctor.ms.md)

