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
        - derive-pages
      options:
        mode: demo
```

### Capabilities (Sandbox Enforcement)

The `capabilities` field declares which hooks the plugin is authorized to execute. Two capabilities are defined:

| Capability | Required For Hook | Description |
|---|---|---|
| `derive-pages` | `derive-pages` | Generate new pages |
| `emit-outputs` | `after-build` | Write files to output directory |

**Enforcement rules:**
- **Not declared** (`capabilities: null` or absent): All hooks allowed (backward compatible)
- **Declared but incomplete**: Build fails with `[BKT-0701]` — the engine checks each hook against declared capabilities at runtime
- Config validation rejects invalid capability names (`ConfigException`)

```yaml
# Error example — "after-build" requires "emit-outputs" capability:
site:
  externalPlugins:
    bad:
      hooks: [after-build]
      capabilities: [derive-pages]  # Missing: emit-outputs → BKT-0701
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
- **SSRF Protection (P1-6)**: External plugin entries are validated via `SsrfGuard.SsrfSafeConnectAsync` when accessing network resources. The host rejects connections to loopback (127.0.0.0/8, ::1), RFC1918 private networks (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16), and link-local addresses (169.254.0.0/16).

## Process Environment Isolation

The plugin process runs in a controlled environment with specific rules:

- **Default Runtime Allowlist**: `ProcessPluginInvoker` preserves these variables by default:
  - POSIX: `PATH`, `HOME`, `USER`, `SHELL`, `TMPDIR`
  - Windows: `USERPROFILE`, `SystemRoot`, `WINDIR`, `COMSPEC`, `PATHEXT`
  - Cross-platform: `TEMP`, `TMP`
  - .NET: `DOTNET_ROOT`, `DOTNET_ROOT_X64`, `DOTNET_ROOT_X86`, `DOTNET_CLI_HOME`
- **Security Guarantee**: Sensitive variables (`NOTION_TOKEN`, `OPENAI_API_KEY`, `GITHUB_TOKEN`, `DATABASE_URL`, `AWS_SECRET_ACCESS_KEY`, `CLOUDFLARE_API_TOKEN`) are never inherited unless explicitly listed in `allowEnvironment`.
- **AllowEnvironment**: Users can explicitly whitelist additional variables:

  ```yaml
  site:
    externalPlugins:
      sample:
        runtime: process
        entry: plugins/plugin.exe
        hooks: [after-build]
        allowEnvironment:
          - MY_CUSTOM_VAR
  ```

- **Deterministic .NET CLI Settings**: The invoker always sets `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`, `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1` in the plugin subprocess.
- **BUKIT_* Context Variables**: Always injected:

  | Variable | Description |
  |---|---|
  | `BUKIT_PLUGIN_NAME` | Plugin name (from `site.externalPlugins` key) |
  | `BUKIT_PLUGIN_HOOK` | Current hook: `derive-pages` or `after-build` |
  | `BUKIT_PROJECT_ROOT` | Absolute path to the site project root |
  | `BUKIT_OUTPUT_DIR` | Absolute path to the build output directory |

- **Implementation**: `ProcessPluginInvoker.cs` — `ApplyEnvironment`, `CopyAllowedEnvironment`, `DefaultRuntimeEnvironmentAllowlist`

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

## Routed Page Payload (vNext)

When `site.externalProtocolIncludeRoutedPages: true`, routed page entries now include both `fields` and canonical `content`.

```json
{
  "id": "post-1",
  "url": "/blog/post-1/",
  "outputPath": "blog/post-1/index.html",
  "fields": {
    "tags": { "type": "multi_select", "value": ["news"] }
  },
  "content": {
    "id": "post-1",
    "slug": "post-1",
    "canonicalUrlKey": "post-1",
    "type": "post",
    "collection": "post",
    "status": "published",
    "title": "Post 1",
    "summary": "Short summary",
    "language": "en",
    "translations": [],
    "author": "Author",
    "organization": "Bukit",
    "publishedAt": "2026-06-06T00:00:00+00:00",
    "updatedAt": "2026-06-06T00:00:00+00:00",
    "source": "markdown",
    "originalSource": "https://example.com/source",
    "citations": [],
    "references": [],
    "syncStatus": "synced",
    "reviewStatus": "published",
    "credibilityScore": null,
    "qualityFlags": [],
    "entities": [
      { "type": "company", "name": "Bukit", "description": null, "id": null, "url": null, "sameAs": null }
    ],
    "relations": [
      { "type": "mentions", "target": "Bukit", "targetType": "company", "targetId": null }
    ],
    "media": [
      { "kind": "image", "url": "/img/cover.jpg", "alt": "Cover", "caption": null, "description": null, "license": null }
    ]
  }
}
```

Compatibility notes:

- `fields` is the dynamic custom field surface.
- `content` is the canonical semantic content model and should be preferred for publishing, audit, feed, search, and agent workflows.
- `meta` is not emitted in vNext protocol payloads.

## WASM Support
- `runtime: wasm`, `wasmProfile: wasi-preview1`
- `wasmFsMode`: `none|output-only`
- `wasmAllowNetwork` only allows `false`
- Error keywords: `[plugin-timeout]`, `[plugin-exit]`, `[plugin-protocol]`, `[plugin-policy]`, `[plugin-init]`, `[plugin-runtime]`
