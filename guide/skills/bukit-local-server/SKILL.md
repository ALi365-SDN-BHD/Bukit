---
name: bukit-local-server
description: Use when running Bukit locally — `bukit dev` (build, watch, LiveReload) or `bukit preview` (serve an existing build), ports, LAN exposure, what is watched, and why links or config changes behave differently than in production.
---

# Bukit Local Servers

| | `bukit dev` | `bukit preview` |
|---|---|---|
| Purpose | Edit loop: build, serve, watch, reload | Check a finished build |
| Builds | Yes: full build, then incremental rebuilds | No; serves an existing directory |
| Default address | `localhost:35729` | `localhost:4173` |
| `--port auto` | No (integer only; `0` picks a free port) | Yes |
| `--strict-port` | Not available | Fail instead of trying the next port |
| Busy port | Tries the requested port and up to 19 following ports | Same, unless `--strict-port` |
| LiveReload | WebSocket `/__ws__`, script injected into HTML | No |
| Drafts | Only with `build.draft: true` | Whatever the build produced |

## dev

```bash
bukit dev
bukit dev --config site.yaml --port 4000
bukit dev --no-watch          # serve the build without watching
```

- Initial build is a clean build; each change triggers a rebuild after 300 ms with
  `clean=false` and incremental rendering, then a browser reload.
- A failed rebuild logs `dev.rebuild.error: <message>`, keeps serving the last good
  output, and does not reload.
- Watched: `content/`, every Markdown source `dir`, the theme and parent theme
  (layouts, assets, static), and user layouts. Ignored: output, `.cache`, `.git`,
  `node_modules`, `.bukit`, `bin`, `obj`, and dot files. `data/` (`site.data_files`) is
  not watched: restart or save a watched file after editing data files.
- `site.yaml` is read once. Restart `bukit dev` after changing config.
- Builds run in development mode: with `site.analytics.productionOnly: true`
  (default) analytics are not injected.
- `--output` overrides the output directory (relative to the project root).

## preview

```bash
bukit build
bukit preview                          # serves ./dist
bukit preview --config site.yaml       # serves the configured build.output
bukit preview --dir public --port auto
```

Directory choice: `--dir` if given; else `build.output` when `--config` or `--site` is
given; else `./dist`. `/` and extensionless paths fall back to `index.html`. Path
traversal returns 403. When analytics are production-only and preview can load the site config
(`--config`/`--site`, or a `site.yaml` found from the directory), managed analytics
blocks are stripped from responses; otherwise they are kept with a warning. A
`site.yaml` found this way that fails to load stops preview with `Preview analytics
policy error: failed to load ...` (exit 2). Files on
disk stay unchanged.

## Shared behavior

- Both serve the output at `/` and ignore `site.baseUrl`. For a site with
  `baseUrl: /repo/`, root-relative links 404 locally. For local checks build with
  `bukit build --base-url /`, then preview.
- Missing files return a plain 404; a custom `404.html` is not served.
- Never served: anything under `.bukit/`, `.bukit-build-state.json`, and
  `.bukit-output-marker`.
- A non-loopback `--host` (for example `0.0.0.0`) requires `--allow-lan` or
  `--public`; otherwise exit 2. Use them only on trusted networks.
- Read the actual URL from the startup line (preview: `Preview: http://...`, dev:
  `dev server: http://...`); a busy port moves to the next one with a
  `Port X unavailable` line. Bind failure (`Failed to listen on ...`) exits 1; an
  invalid preview `--port` or a missing directory exits 2. If dev's initial build fails,
  dev exits with the build's exit code; only later rebuild failures keep serving.
