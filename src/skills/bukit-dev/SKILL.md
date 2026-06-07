---
name: bukit-dev
description: Use when using bukit to start the HMR development server, wanting hot-reload during development, needing file watching with automatic rebuild, or troubleshooting dev server issues

status: stable
since: "v3.0.0"
verified_by:
  - "src/Bukit.Cli/Commands/DevCommand.cs"
source_anchors:
  - "src/Bukit.Cli/Commands/DevCommand.cs"
guide_chapters:
  - "guide/user/12-cli-reference.md"
---

# Bukit HMR Development Server

## Overview

`bukit dev` starts a development server with Hot Module Replacement (HMR): it watches for file changes, incrementally rebuilds affected pages, and pushes live reload notifications to connected browsers via WebSocket.

**REQUIRED SUB-SKILL:** CLI commands reference bukit-cli-reference. Config reference bukit-config.

## Multilingual Triggers

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "开发服务器"、"HMR"、"热重载"、"文件监控"、"自动刷新"、"bukit dev"、"实时预览" |
| English | "dev server", "HMR", "hot reload", "live reload", "watch mode", "auto refresh", "bukit dev" |
| Bahasa Melayu | "pelayan pembangun", "HMR", "muat semula panas", "muat semula langsung", "mod pantau", "segar automatik", "bukit dev" |

## Usage

### Basic

```bash
bukit dev
```

Starts at `http://localhost:35729/`, watching content/, themes/, layouts/, assets/, static/ for changes.

### Custom Port and Host

```bash
bukit dev --port 3000 --host 0.0.0.0
```

### Disable File Watching (static server only)

```bash
bukit dev --no-watch
```

### Custom Config

```bash
bukit dev --config site.yaml
bukit dev --site myblog
```

### Custom Output Directory

```bash
bukit dev --output ./public
```

## How It Works

```
bukit dev
  ├─ 1. Full initial build (Clean + Incremental)
  ├─ 2. Start HTTP server (HttpListener)
  │     └─ HTML: injects livereload WebSocket <script> before </head>
  ├─ 3. WebSocket endpoint (/__ws__)
  │     └─ Browser connects → waits for "reload" broadcast
  └─ 4. File watchers (FileSystemWatcher)
        ├─ Watches: content/ + themes/ + layouts/ + assets/ + static/
        ├─ Excludes: .cache/ + dist/ + dot-prefixed files
        ├─ Debounce: 300ms (batches rapid changes)
        └─ On change → incremental rebuild → WebSocket broadcast "reload"
```

## Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--config` | string | — | Config file path |
| `--site` | string | — | Multi-site name |
| `--host` | string | localhost | Listen address |
| `--port` | int | 35729 | Listen port (auto-increment if occupied) |
| `--output` | string | dist | Output directory override |
| `--no-watch` | flag | false | Disable file watching (serve only) |

## Live Reload Injection

The dev server automatically injects a WebSocket-based live reload script before `</head>` in all HTML responses:

```javascript
// Autoinjected into every HTML page served:
var s = new WebSocket('ws://localhost:35729/__ws__');
s.onmessage = function(e) {
  if (e.data === 'reload') location.reload();
};
```

When a file change triggers a rebuild, all connected browsers receive a "reload" message and refresh automatically.

## Watched Directories

| Directory | Reason |
|-----------|--------|
| `content/` | Markdown/Notion content changes |
| `themes/<name>/` | Theme template changes |
| `themes/<parent>/` | Parent theme changes (if theme.extends is set) |
| `layouts/` | Standalone layout template changes |
| `assets/` | CSS/JS/SCSS changes |
| `static/` | Static file changes |

Excluded: `.cache/`, `dist/`, dot-prefixed files (`.DS_Store`, etc.).

## Common Issues

### Port already in use

```
bukit dev --port 3000
```
If port 3000 is busy, bukit auto-increments to 3001, 3002, etc.

### Changes not detected

- Check that the changed file is in a watched directory (content/, themes/, layouts/, assets/, static/)
- Ensure the file is not ignored (dot-prefixed files, .cache/, dist/ are excluded)
- For theme inheritance, ensure parent theme directories are accessible

### Hot reload not working

- Verify WebSocket connection in browser DevTools (`/__ws__`)
- Check that no firewall/proxy blocks WebSocket connections
- Ensure the page served contains the injected live-reload `<script>` tag

### Build slow during dev

Use incremental builds and check the per-stage timing in the output log:
```
event=content.stage stage=ContentLoad duration_ms=234
event=content.stage stage=ImageLocalize duration_ms=156
event=content.stage stage=DraftFilter duration_ms=1
event=content.stage stage=ContentGraphValidate duration_ms=3
event=content.stage stage=CollectionWarning duration_ms=12
```

Each content pipeline stage logs its name and duration. Long stages (especially `ContentLoad` and `ImageLocalize`) indicate where build time is spent.

### Diagnostic codes in logs

Build errors and `bukit doctor` output use stable `BKT-XXXX` diagnostic codes. See the Config Skill for the full code reference.

### Template variable warnings

Run `bukit doctor` after writing new templates to detect typos in Scriban variable names. The doctor's spell check section reports unknown variables like `site.settings` (when you meant `site.params`).

### Livereload not working

1. Check browser console for WebSocket connection errors
2. Verify `bukit dev` is running (not `bukit preview`)
3. Check firewall settings if accessing from another device

### Slow rebuilds

Incremental build is enabled by default. First build is full, subsequent builds only re-render changed pages. Use `--no-watch` for static-only serving.

### Dev Server Architecture (P2-2)

The dev command has been decomposed into a modular `Dev/` subdirectory under `Bukit.Cli/Commands/`:

| Component | Responsibility |
|---|---|
| `DevServerHost` | HTTP server lifecycle and middleware |
| `DevWebSocketHub` | WebSocket connections for HMR live reload |
| `DevFileWatcher` | File system change detection with debouncing |
| `DevRequestHandler` | HTTP request routing and static file serving |
| `DevPathGuard` | Path traversal protection for dev server file access |

This separation makes it easier to diagnose dev server issues: check the specific component based on the symptom (WebSocket connection errors → DevWebSocketHub, file changes not detected → DevFileWatcher, 404 errors → DevRequestHandler).
