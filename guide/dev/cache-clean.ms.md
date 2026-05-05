# Cache dan Pembersihan (cache-dir / .cache / clean)

## Direktori Cache
- Lalai: `<rootDir>/.cache`, tindih dengan `--cache-dir <dir>`
- Kandungan: `build-manifest*.json`, `notion/` (cache render Notion)

## Perintah Pembersihan
- `bukit clean --dir dist` — Bersihkan direktori output
- `bukit clean --dir dist --clear-cache` — Juga bersihkan cache
- `bukit clean --clear-cache` — Bersihkan cache sahaja

`build --clean` membersihkan output sebelum bina; `clean` adalah perintah bebas.

Lihat: [incremental-build.md](./incremental-build.md), [doctor.md](./doctor.md)
