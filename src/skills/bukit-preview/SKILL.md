---
name: bukit-preview
description: Use when using bukit to preview a built site locally, starting the local preview server, troubleshooting preview port conflicts, configuring preview host/port, or understanding preview behavior with analytics disabling and MIME type handling
---

# Bukit Local Preview Server

## Overview

Bukit's `preview` command starts a lightweight HTTP server that serves the built output directory (`dist/` by default) for local testing before deployment. It handles MIME type detection, gzip support, and automatically injects a CSS snippet to disable analytics tracking in preview mode.

**REQUIRED SUB-SKILL:** CLI commands reference bukit-cli-reference.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "本地预览"、"启动预览服务器"、"bukit preview"、"预览端口被占用" |
| English | "local preview", "start preview server", "bukit preview", "preview port conflict" |
| Bahasa Melayu | "pralihat setempat", "mulakan pelayan pralihat", "bukit preview", "konflik port pralihat" |

## Usage

### Basic

```bash
bukit preview
```

Starts a server at `http://localhost:4173/` serving the `dist/` directory.

### Custom Directory and Port

```bash
bukit preview --dir ./output --port 8080
```

### Strict Port Mode

```bash
bukit preview --port 4173 --strict-port
```

When `--strict-port` is set, the server exits with an error if the port is already in use. Without it, the server falls back to an available port and prints the actual URL.

### Custom Host

```bash
bukit preview --host 0.0.0.0 --port 4173
```

Use `0.0.0.0` to make the preview accessible from other devices on the network.

## Command Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--dir <path>` | string | `dist` | Directory to serve |
| `--host <addr>` | string | `localhost` | Host address to bind |
| `--port <port>` | int | `4173` | Port to listen on (use `auto` to assign a free port) |
| `--strict-port` | flag | — | Exit if port is unavailable instead of falling back |

## Preview Behavior

### Analytics Disabling

By default, the preview server automatically disables analytics tracking:

- Injects a CSS rule (`[data-ga4-disable]`) into HTML responses to hide analytics elements
- Strips `ga-disable-*` window properties to prevent tracking
- This only affects the preview — deployed sites are unaffected

### MIME Type Detection

The server maps file extensions to correct `Content-Type` headers:

| Extension | Content-Type |
|------|------|
| `.html`, `.htm` | `text/html; charset=utf-8` |
| `.css` | `text/css; charset=utf-8` |
| `.js`, `.mjs` | `text/javascript; charset=utf-8` |
| `.json` | `application/json; charset=utf-8` |
| `.xml` | `application/xml; charset=utf-8` |
| `.svg` | `image/svg+xml` |
| `.png` | `image/png` |
| `.jpg`, `.jpeg` | `image/jpeg` |
| `.gif` | `image/gif` |
| `.webp` | `image/webp` |
| `.ico` | `image/x-icon` |
| `.woff2` | `font/woff2` |
| `.txt` | `text/plain; charset=utf-8` |

### Gzip Support

The preview server supports gzip compression for text-based content:

- Checks for `.gz` variants of requested files
- Sets `Content-Encoding: gzip` when serving pre-compressed files
- Handles: `.html`, `.css`, `.js`, `.json`, `.xml`, `.svg`, `.txt`

### Directory Index

Requesting a directory path (e.g., `/blog/`) serves `index.html` from that directory if it exists.

## Common Issues

| Issue | Cause | Fix |
|------|------|------|
| `Port already in use` | Another process on the same port | Use a different port: `--port 8080` |
| `Directory not found` | Output directory doesn't exist | Run `bukit build` first |
| CSS/JS not loading | Incorrect `baseUrl` in site.yaml | Check `site.baseUrl` matches preview context |
| Analytics still appear | Analytics tracking script is inline | Preview disables GA4 window properties; inline scripts may bypass |
| 404 on all routes | Wrong `--dir` path | Verify the directory contains built HTML files |

## Quick Tips

- **Port auto-assignment**: `--port auto` assigns a free port automatically
- **Before deployment**: Always preview with `bukit preview` to catch broken links and missing assets
- **Stop server**: Press `Ctrl+C`
- **Multiple sites**: Run multiple preview instances on different ports for side-by-side comparison
