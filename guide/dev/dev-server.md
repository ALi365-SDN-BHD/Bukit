# LiveReload Development Server

`bukit dev` starts a local development server, performs an initial build,
watches source inputs, rebuilds incrementally, and refreshes connected browsers.

Source anchors:

- `src/Bukit.Cli/Commands/DevCommand.cs`
- `src/Bukit.Cli/Commands/Dev/DevFileWatcher.cs`
- `src/Bukit.Cli/Commands/Dev/DevWebSocketHub.cs`
- `src/Bukit.Cli/Commands/Dev/DevRequestHandler.cs`

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
| `--site` | none | Multi-site config name |
| `--host` | `localhost` | Listen host |
| `--port` | `35729` | Listen port |
| `--output` | `build.output` | Build output override |
| `--no-watch` | false | Serve existing initial build without watching |
| `--allow-lan` | false | Permit non-loopback host binding |
| `--public` | false | Alias for `--allow-lan` |

## Behavior

1. Resolve config.
2. Load and validate `site.yaml`.
3. Run an initial clean incremental build.
4. Serve the output directory.
5. Inject a browser reload script into HTML responses.
6. Watch content, active theme, layouts, assets, and static directories.
7. Exclude output, cache, `.git`, `node_modules`, `.bukit`, `bin`, and `obj`.
8. Rebuild incrementally on changes.
9. Broadcast reload messages through `/__ws__`.

## LAN Exposure

Binding to a non-loopback host is refused unless `--allow-lan` or `--public` is
provided. Only use it on trusted networks.

## Troubleshooting

| Symptom | Check |
|---|---|
| Browser does not refresh | WebSocket `/__ws__`, browser console, host/origin policy |
| File change ignored | watched directory set and excluded directory list |
| Server refuses host | add `--allow-lan` for trusted LAN exposure |
| Output looks stale | compare first clean build with later incremental rebuild logs |

