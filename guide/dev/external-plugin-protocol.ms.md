# Protokol Plugin Luaran v1/v2

`external-protocol` adalah penyelesaian sambungan dinamik serasi AOT Bukit.

## Contoh Konfigurasi
```yaml
site:
  externalProtocolIncludeRoutedPages: false
  externalPlugins:
    sample:
      runtime: process
      entry: plugins/sample-plugin.exe
      hooks: [after-build, derive-pages]
      timeoutMs: 5000
      capabilities:
        - emit-outputs
        - derive-pages
```

### Keupayaan (Penguatkuasaan Kotak Pasir)

Medan `capabilities` mengisytiharkan hook mana yang dibenarkan untuk dilaksanakan oleh plugin. Dua keupayaan ditakrifkan:

| Keupayaan | Diperlukan Untuk Hook | Penerangan |
|---|---|---|
| `derive-pages` | `derive-pages` | Menjana halaman baharu |
| `emit-outputs` | `after-build` | Menulis fail ke direktori output |

**Peraturan penguatkuasaan:**
- **Tidak diisytiharkan** (`capabilities: null` atau tiada): Semua hook dibenarkan (serasi ke belakang)
- **Diisytiharkan tetapi tidak lengkap**: Binaan gagal dengan `[BKT-0701]` — enjin memeriksa setiap hook terhadap keupayaan yang diisytiharkan pada masa jalan
- Pengesahan konfigurasi menolak nama keupayaan tidak sah (`ConfigException`)

```yaml
# Contoh ralat — "after-build" memerlukan keupayaan "emit-outputs":
site:
  externalPlugins:
    bad:
      hooks: [after-build]
      capabilities: [derive-pages]  # Hilang: emit-outputs → BKT-0701
```

## Struktur Permintaan (stdin JSON)
```json
{
  "schemaVersion": "1",
  "hook": "after-build",
  "plugin": { "name": "sample" },
  "site": { "baseUrl": "/", "language": "zh-CN" },
  "config": { "pluginOptions": {} },
  "afterBuild": { "outputDir": "dist", "routedPages": [] }
}
```

## Struktur Respons (stdout JSON)
```json
{
  "ok": true,
  "logs": [{ "level": "info", "message": "ok" }],
  "outputs": [{ "path": "output.json", "contentType": "application/json", "text": "{}" }]
}
```

## Sempadan Keselamatan
- `outputs.path` mesti relatif kepada direktori output (tiada laluan mutlak, tiada `..`)
- Hos bertanggungjawab sepenuhnya untuk penulisan fail sebenar

## Pengasingan Persekitaran

Proses plugin berjalan dalam persekitaran bersih — pemboleh ubah persekitaran hos **tidak** diwarisi. Hanya pemboleh ubah Bukit berikut disuntik:

| Pemboleh Ubah | Penerangan |
|---|---|
| `BUKIT_PLUGIN_NAME` | Nama plugin (dari kunci `site.externalPlugins`) |
| `BUKIT_PLUGIN_HOOK` | Hook semasa: `derive-pages` atau `after-build` |
| `BUKIT_PROJECT_ROOT` | Laluan mutlak ke direktori akar projek tapak |
| `BUKIT_OUTPUT_DIR` | Laluan mutlak ke direktori output binaan |

Untuk mendedahkan pemboleh ubah persekitaran hos tambahan, gunakan `allowEnvironment`:

```yaml
site:
  externalPlugins:
    sample:
      runtime: process
      entry: plugins/plugin.exe
      hooks: [after-build]
      allowEnvironment:
        - PATH
        - HOME
```

## Had Output

Untuk mengehadkan plugin yang menghasilkan stdout/stderr berlebihan, tetapkan had bait:

```yaml
site:
  externalPlugins:
    sample:
      maxStdoutBytes: 1048576   # 1 MB
      maxStderrBytes: 262144    # 256 KB
```

Apabila had melebihi, Bukit membunuh proses plugin dan menggagalkan binaan dengan mesej ralat yang jelas. Lalai (tidak ditetapkan) adalah tanpa had.

## Manifes Output Plugin

Setiap fail yang ditulis oleh plugin luaran dikesan dalam manifes binaan. Semasa binaan tambahan, output dari binaan sebelumnya yang tidak lagi dihasilkan akan **dipadam secara automatik** (pembersihan output lapuk).

## Sokongan WASM
- `runtime: wasm`, `wasmProfile: wasi-preview1`
- `wasmFsMode`: `none|output-only`, `wasmAllowNetwork` hanya membenarkan `false`
- Kata kunci ralat: `[plugin-timeout]`, `[plugin-exit]`, `[plugin-protocol]`
