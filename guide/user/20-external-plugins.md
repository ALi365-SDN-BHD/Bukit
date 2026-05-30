# Writing External Plugins

## Plugin Classification & Security Levels

Bukit categorizes plugins into four types based on their runtime model and trust boundaries:

| Plugin Type | Positioning | Security Level | Notes |
|-------------|-------------|---------------|-------|
| Built-in Plugin | Engine internal capabilities | High | Runs in-process, full trust |
| Process Plugin | Local trusted extension | Low | Full host process privileges, no sandbox. Disabled in CI by default. Use `--allow-external-plugins` to enable. |
| Future WASM Plugin | Distributable community plugins | Medium-High | Sandboxed, resource-limited |
| Section Plugin | Theme component-level | Medium | Theme-scoped capabilities |

**Process plugins** are the currently supported external plugin runtime. The following sections document their security model, configuration, and usage in detail.

## Security & Trust Model

**External plugins run as subprocesses with full host process privileges.** This means:

- They can read any file on the host filesystem, not just the project directory.
- They can access the network and make arbitrary outbound connections.
- They can execute arbitrary subprocesses and system commands.
- There is **no sandbox** or container isolation between your plugin and the host.

**Because of this, you must:**
- Only install plugins from **trusted sources** (authors you know and trust, or official Bukit plugin registries).
- Review plugin source code before adding it to your project.
- Never use plugins from untrusted third parties in production environments.

**Additional safety measures:**
- **CI environments disable external plugins by default.** To enable them in CI, pass `--allow-external-plugins` on the command line.
- **Control plugin loading with `externalPluginPolicy`:** Set `site.externalPluginPolicy` to `deny` (block all), `warn` (load with warning, default), or `allow` (load silently). Invalid values throw `ConfigException` with `BKT-0002`.
- **Plugin entry paths must be within the project directory.** Absolute paths like `/usr/bin/some-tool` are rejected unless the plugin explicitly sets `allowAbsoluteEntry: true` in its configuration.
- **Stdout/stderr output is capped** at 1 MB by default (configurable via `maxStdoutBytes` / `maxStderrBytes`).
- **Timeout protection:** Plugins are automatically killed if they exceed `timeoutMs`.
- **Environment isolation with runtime allowlist:** By default, the plugin process receives only a minimal set of runtime variables (`PATH`, `HOME`, `USER`, `SHELL`, `TMPDIR`, `TEMP`, `TMP`, and `.NET` runtime variables like `DOTNET_ROOT`). Sensitive host variables (`NOTION_TOKEN`, `OPENAI_API_KEY`, `GITHUB_TOKEN`, `DATABASE_URL`, etc.) are **never** inherited unless explicitly whitelisted.
- **`AllowEnvironment` whitelist:** To pass custom variables, list them in `allowEnvironment`. Example: `allowEnvironment: [MY_API_KEY, NODE_ENV]`
- **Deterministic .NET settings:** `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`, and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1` are always set in the plugin subprocess.
- **`BUKIT_*` context variables:** `BUKIT_PLUGIN_NAME`, `BUKIT_PLUGIN_HOOK`, `BUKIT_PROJECT_ROOT`, and `BUKIT_OUTPUT_DIR` are always available in the plugin subprocess.
- **Output path validation:** Plugins cannot write outside the configured output directory.

## How It Works

```
Bukit Engine                    Your Plugin (subprocess)
     |                               |
     |--- JSON request → stdin ----→ |  (reads request)
     |                               |  (processes)
     |←-- JSON response ← stdout --- |  (writes response)
     |                               |
```

Bukit invokes your plugin's entry point, sends a JSON request via stdin, and reads the JSON response from stdout.

## Configuration

Add an `externalPlugins` section to your `site.yaml`:

```yaml
site:
  externalPlugins:
    my-plugin:
      runtime: process          # only "process" is supported
      entry: plugins/my-plugin.js
      hooks: [derive-pages]     # or [after-build], or both
      capabilities: [derive-pages]
      timeoutMs: 5000
      allowAbsoluteEntry: false # set true only if entry is an absolute path
```

| Field | Description |
|-------|-------------|
| `runtime` | Always `"process"` |
| `entry` | Path to your plugin executable (resolved relative to project root) |
| `hooks` | Which hooks to participate in: `after-build`, `derive-pages` |
| `capabilities` | Required: `emit-outputs` (for after-build), `derive-pages` (for derive-pages) |
| `timeoutMs` | Max time before the plugin is killed (default 5000) |
| `allowAbsoluteEntry` | Allow absolute paths for `entry` (default `false`). Only needed when the plugin binary is outside the project. |
| `options` | Optional: custom key-value pairs passed to your plugin |

## Protocol Overview

### Request Format

Bukit sends a JSON object:

```json
{
  "schemaVersion": "2",
  "hook": "derive-pages",
  "plugin": { "name": "my-plugin", "version": "0.1.0" },
  "site": { "baseUrl": "/", "language": "en", "title": "My Site" },
  "projectRoot": "/path/to/project",
  "outputDir": "/path/to/project/dist",
  "derivePages": {
    "routedPages": [
      { "id": "...", "title": "...", "slug": "...", "url": "/...", "collection": "..." }
    ]
  },
  "config": { "options": {} }
}
```

### Response Format

Your plugin must write a JSON object to stdout:

```json
{
  "ok": true,
  "derivedPages": [
    {
      "id": "my-page",
      "title": "My Generated Page",
      "slug": "my-page",
      "url": "/my-page/",
      "outputPath": "my-page/index.html",
      "contentHtml": "<p>Generated content</p>"
    }
  ],
  "logs": [{ "level": "info", "message": "Generated 1 page" }]
}
```

## Hook: derive-pages

Use derive-pages to generate additional pages based on existing routes. Your plugin receives all routed pages and can return new ones.

### After-build Hook

```json
// Request
{
  "hook": "after-build",
  "afterBuild": {
    "outputDir": "/project/dist",
    "routedPages": [...]
  }
}

// Response
{
  "ok": true,
  "outputs": [
    { "path": "plugin-data.json", "contentType": "application/json", "text": "..." }
  ]
}
```

## Complete Example: Node.js Derive-Pages Plugin

```javascript
#!/usr/bin/env node
const { stdin, stdout } = require("process");

let raw = "";
stdin.setEncoding("utf-8");
stdin.on("data", chunk => raw += chunk);
stdin.on("end", () => {
    const req = JSON.parse(raw);

    if (req.hook === "handshake") {
        stdout.write(JSON.stringify({
            ok: true,
            supportedHooks: ["derive-pages"],
            negotiatedSchemaVersion: "2"
        }));
        return;
    }

    if (req.hook === "derive-pages") {
        const count = req.derivePages.routedPages.length;
        stdout.write(JSON.stringify({
            ok: true,
            derivedPages: [{
                id: "hello",
                title: `Hello (${count} pages)`,
                slug: "hello",
                url: "/hello/",
                outputPath: "hello/index.html",
                contentHtml: `<p>Generated from ${count} routable pages.</p>`,
                publishAt: new Date().toISOString()
            }],
            logs: [{ level: "info", message: `Derived hello from ${count} pages` }]
        }));
    }
});
```

See `examples/plugin-site/plugins/hello-derive.js` for the full working example.

## Troubleshooting

| Problem | Check |
|---------|-------|
| Plugin not found | Ensure `entry` path is correct relative to project root |
| No output | Check stderr for errors. Bukit logs it when `logging.level: debug` |
| Timeout | Increase `timeoutMs` or simplify plugin logic |
| Permission denied | Make sure the plugin file is executable (`chmod +x`) |
| Invalid JSON response | Test manually: `echo '{"hook":"handshake"}' | node plugin.js` |
