---
name: bukit-dev
description: Use when starting or troubleshooting Bukit LiveReload development preview, file watching, incremental rebuilds, WebSocket reload messages, LAN exposure, or `dev` server behavior.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "tests/Bukit.Cli.Tests/DevCommandTests.cs"
source_anchors:
  - "src/Bukit.Cli/Commands/DevCommand.cs"
  - "src/Bukit.Cli/Commands/Dev/DevFileWatcher.cs"
  - "src/Bukit.Cli/Commands/Dev/DevWebSocketHub.cs"
  - "src/Bukit.Cli/Commands/Dev/DevRequestHandler.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit LiveReload Development Server

`dev` starts a development preview server with file watching, incremental rebuilds, WebSocket reload messages, and full browser refresh.

It is not module-level replacement inside a frontend bundler.

## Usage

```bash
bukit dev
bukit dev --port 3000
bukit dev --host 0.0.0.0 --allow-lan
bukit dev --no-watch
bukit dev --output public
```

## Options

| Option | Default | Meaning |
|---|---|---|
| `--config` | `site.yaml` | Config path |
| `--site` | none | Multi-site name |
| `--host` | `localhost` | Listen host |
| `--port` | `35729` | Listen port |
| `--output` | `build.output` | Output override |
| `--no-watch` | false | Serve without watching |
| `--allow-lan` | false | Permit non-loopback hosts |
| `--public` | false | Alias for `--allow-lan` |

## Behavior

1. Resolve and load config.
2. Run an initial clean incremental build.
3. Serve output over HTTP.
4. Watch content and active theme inputs.
5. Exclude output, cache, `.git`, `node_modules`, `.bukit`, `bin`, and `obj`.
6. Rebuild incrementally on changes.
7. Broadcast reload over `/__ws__`.

## Troubleshooting

| Symptom | Check |
|---|---|
| Server refuses LAN host | Add `--allow-lan` only on trusted networks |
| Browser does not refresh | Check WebSocket path `/__ws__`, Host/Origin headers, and browser console |
| File change ignored | Confirm the file is in content, active theme, layouts, assets, or static paths |
| Slow rebuild | Compare first full build with later incremental rebuilds |
