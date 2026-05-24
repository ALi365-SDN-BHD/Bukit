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

## Pemulihan Binaan

Apabila binaan terganggu (cth., proses ranap, penutupan sistem), Bukit mengesan keadaan tidak lengkap pada larian seterusnya dan membersihkan direktori output secara automatik untuk permulaan baharu.

### Cara Ia Berfungsi

1. **Penanda mula**: Pada permulaan setiap binaan, Bukit menulis `.bukit-build-state.json` dengan `status: started` ke direktori output.
2. **Penanda selesai**: Apabila binaan berjaya diselesaikan, status dikemas kini kepada `completed`.
3. **Pengesanan pemulihan**: Pada binaan seterusnya (mod bukan Clean), jika fail status menunjukkan `started`, enjin memadam direktori output secara automatik dan membina semula dari awal.

### Binaan Bersih Manual

Untuk memaksa binaan semula bersih secara eksplisit (mengabaikan sebarang keadaan sebelumnya):

```bash
bukit build --clean
```

### Ringkasan Tingkah Laku Pemulihan

| Senario | Tingkah Laku |
|---|---|
| Binaan sebelumnya selesai | Binaan incremental normal |
| Binaan sebelumnya terganggu (`--clean` tidak ditetapkan) | Auto-bersih direktori output, kemudian binaan penuh dengan log amaran |
| `--clean` ditetapkan secara eksplisit | Sentiasa bersihkan direktori output sebelum binaan |

Ini memastikan direktori output kekal konsisten walaupun selepas gangguan binaan yang tidak dijangka.

## Perbandingan dengan Binaan Penuh
