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

## Environment Isolation

The plugin process runs in a clean environment — host environment variables are **not** inherited. Only these Bukit-injected variables are exposed:

| Variable | Description |
|---|---|
| `BUKIT_PLUGIN_NAME` | Plugin name (from `site.externalPlugins` key) |
| `BUKIT_PLUGIN_HOOK` | Current hook: `derive-pages` or `after-build` |
| `BUKIT_PROJECT_ROOT` | Absolute path to the site project root |
| `BUKIT_OUTPUT_DIR` | Absolute path to the build output directory |

To expose additional host environment variables, use `allowEnvironment`:

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

## Output Limits

To cap runaway plugins that produce excessive stdout/stderr, set byte limits:

```yaml
site:
  externalPlugins:
    sample:
      maxStdoutBytes: 1048576   # 1 MB
      maxStderrBytes: 262144    # 256 KB
```

When either limit is exceeded, Bukit kills the plugin process and fails the build with a clear error message. Default (unset) is unlimited.

## Plugin Output Manifest

Every file written by external plugins is tracked in the build manifest. During incremental builds, outputs from the previous build that are no longer produced are **automatically deleted** (stale output cleanup).

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
