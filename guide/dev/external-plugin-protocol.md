# External Plugin Protocol v1/v2

`external-protocol` is Bukit's AOT-compatible dynamic extension solution. It does not replace built-in or generated plugins but provides dynamically installable plugin capabilities without external DLL reflection loading.

## Applicable Scenarios
- Dynamic extensions in AOT mode
- Independently published plugins without recompiling the main program
- Future compatibility with `process` and `wasm` hosts

## Configuration Example

```yaml
site:
  externalProtocolIncludeRoutedPages: false
  externalPlugins:
    sample:
      runtime: process
      entry: plugins/sample-plugin.exe
      hooks:
        - after-build
        - derive-pages
      enabled: true
      timeoutMs: 5000
      wasmProfile: wasi-preview1
      maxMemoryMb: 64
      capabilities:
        - emit-outputs
      options:
        mode: demo
```

## Request Structure (stdin JSON)

```json
{
  "schemaVersion": "1",
  "hook": "after-build",
  "plugin": { "name": "sample", "version": "protocol-v1" },
  "site": { "baseUrl": "/", "language": "zh-CN", "title": "Test" },
  "config": { "pluginOptions": { "mode": "demo" } },
  "afterBuild": { "outputDir": "dist", "routedPages": [] }
}
```

## Response Structure (stdout JSON)

```json
{
  "ok": true,
  "logs": [{ "level": "info", "message": "ok" }],
  "outputs": [{ "path": "plugin-output.json", "contentType": "application/json", "text": "{\"ok\":true}" }]
}
```

Or on failure: `{ "ok": false, "error": { "code": "PLUGIN_ERROR", "message": "..." } }`

## Security Boundaries
- `outputs.path` must be relative to output directory (no absolute paths, no `..`)
- Host is solely responsible for actual file writes

## Protocol Negotiation (v2)
- After-build first sends `hook=handshake`
- Successful negotiation returns `negotiatedSchemaVersion=2`
- Failed negotiation falls back to v1
- Handshake results are cached within the same BuildContext

## WASM Support
- `runtime: wasm`, `wasmProfile: wasi-preview1`
- `wasmFsMode`: `none|output-only`
- `wasmAllowNetwork` only allows `false`
- Error keywords: `[plugin-timeout]`, `[plugin-exit]`, `[plugin-protocol]`, `[plugin-policy]`, `[plugin-init]`, `[plugin-runtime]`
