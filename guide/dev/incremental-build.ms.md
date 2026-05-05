# Binaan Tokokan (manifest / cache-dir / sebab langkau-render)

Pelaksanaan: `src/Bukit.Engine/Incremental/*`, `src/Bukit.Engine/PageRenderDispatcher.cs`

## Suis dan Direktori
- Didayakan secara lalai
- CLI: `--incremental`/`--no-incremental`, `--cache-dir <dir>` (lalai `<rootDir>/.cache`)

## Fail Manifest
Laluan lalai: `<cacheDir>/build-manifest.json` (bahasa tunggal), `<cacheDir>/build-manifest.<lang>.json` (pelbagai bahasa).

## Syarat Langkau Render
Untuk melangkau rendering, semua mesti sepadan:
1. Tokokan didayakan
2. Manifest mempunyai entri untuk halaman
3. Fail output wujud
4. TemplateHash, ContentHash, RouteHash semua sepadan

Halaman utama/senarai menggunakan `ListContentHash` khusus.

## sebabRender (dalam output `--metrics`)
- `new_page`, `output_missing`, `template_changed`, `content_changed`, `route_changed`, `full_render`
- `unchanged`, `list_render`, `list_unchanged`
