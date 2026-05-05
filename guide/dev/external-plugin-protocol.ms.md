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

## Sokongan WASM
- `runtime: wasm`, `wasmProfile: wasi-preview1`
- `wasmFsMode`: `none|output-only`, `wasmAllowNetwork` hanya membenarkan `false`
- Kata kunci ralat: `[plugin-timeout]`, `[plugin-exit]`, `[plugin-protocol]`
