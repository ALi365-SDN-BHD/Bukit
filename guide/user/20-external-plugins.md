# Writing External Plugins

External plugins let you extend Bukit using **any language** (Node.js, Python, Go, etc.) via a simple stdin/stdout JSON protocol. They run as subprocesses — fully compatible with Bukit's Native AOT builds.

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
```

| Field | Description |
|-------|-------------|
| `runtime` | Always `"process"` |
| `entry` | Path to your plugin executable (resolved relative to project root) |
| `hooks` | Which hooks to participate in: `after-build`, `derive-pages` |
| `capabilities` | Required: `emit-outputs` (for after-build), `derive-pages` (for derive-pages) |
| `timeoutMs` | Max time before the plugin is killed (default 5000) |
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

## Security

- **Stdout/stderr limits**: 1MB default (configurable via `maxStdoutBytes`/`maxStderrBytes`)
- **Timeout**: Plugin is killed if it exceeds `timeoutMs`
- **Environment isolation**: Only `BUKIT_*` variables and `AllowEnvironment` whitelist are passed
- **Output path validation**: Plugins cannot write outside the output directory

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
