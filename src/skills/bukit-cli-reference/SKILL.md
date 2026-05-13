---
name: bukit-cli-reference
description: Use when using bukit CLI — agent needs to execute Bukit commands (build, init, preview, clean, doctor, plugin, theme, intent, webhook, version), detect whether the Bukit CLI tool is installed, install or upgrade bukit, or interpret bukit build output and exit codes
---

# Bukit CLI Command Reference

## Overview

Bukit is a .NET single-file executable CLI tool. Agents execute `bukit` commands through their native shell to initialize sites, build, preview, and more. This skill is the single source of truth for all CLI operations — other Bukit skills reference this skill for command execution guidance and do not duplicate command instructions.

## Multilingual Triggers / Pencetus Berbilang Bahasa

| Language | Trigger Phrases |
|----------|----------------|
| 中文 | "执行 bukit 命令"、"运行 bukit build"、"bukit CLI"、"安装 bukit" |
| English | "run bukit command", "execute bukit build", "install bukit CLI", "bukit preview" |
| Bahasa Melayu | "jalankan arahan bukit", "laksana bukit build", "pasang bukit CLI", "bukit preview" |

## CLI Detection

**Check if CLI is available:**

```
bukit version
```

Sample output:
```
bukit 2.x.x
runtime: jit   (or runtime: native-aot)
```

On Windows, if the `.exe` is not in PATH, use `.\bukit.exe` or `./bukit.exe`. In PowerShell, use `&` to invoke:

```powershell
& .\bukit.exe version
```

**Important note**: All commands except `version` will output the version number to stderr before execution (e.g., `bukit 2.x.x`). This is normal behavior, not an error.

## Installation Guide

Bukit distributes platform binaries via GitHub Releases — it is NOT published as a NuGet dotnet tool.

| Method | Command | Use Case |
|------|------|---------|
| Download binary | Download the matching platform file from [GitHub Releases](https://github.com/ALi365-SDN-BHD/Bukit/releases) | Recommended, no .NET SDK required |
| Build from source | `dotnet publish src/Bukit.Cli -c Release` | Developers / bleeding edge |

After downloading, place the binary in a PATH directory or the project root.

## Command Quick Reference

| Command | Purpose | Key Parameters |
|------|------|---------|
| `init` | Initialize site scaffolding | `<target-dir>` `--provider`(markdown/notion) `--template`(minimal) |
| `create` | Alias for `init` | Same as above |
| `build` | Build static site | `--config` `--output` `--base-url` `--draft` `--ci` `--incremental` / `--no-incremental` `--jobs` `--metrics` `--log-format` |
| `preview` | Local preview of dist | `--dir` `--host` `--port` `--strict-port` |
| `clean` | Clean output and cache directories | `--config` `--site` `--dir` |
| `doctor` | Diagnose config and templates | `--config` `--site` `--site-url` |
| `plugin list` | List registered plugins | `--config` `--site` |
| `theme list` | List available themes in themes/ | `--config` `--site` |
| `theme create` | Create a theme from starter or an existing theme | `<name>` `--from` `--brand` `--primary-color` `--accent-color` `--use` `--force` `--config` `--site` |
| `theme use` | Switch current theme | `<name>` `--config` `--site` |
| `intent init` | Interactive intent file creation | `--out` |
| `intent validate` | Validate intent file | `<intent.yaml>` `--root-dir` `--out` |
| `intent apply` | Apply intent to generate site.yaml | `<intent.yaml>` `--out` |
| `webhook` | Start Notion→GitHub webhook service | `--repo` `--host` `--port` `--path` `--event` |
| `version` | Output version number | No parameters |

## Key Command Details

### build

Build the site, rendering content sources and templates into static HTML files.

```
bukit build [--config <path>] [--output <dir>] [--base-url <url>] [--draft] [--ci] [--incremental|--no-incremental] [--jobs <n>] [--metrics <path>] [--log-format text|json]
```

| Parameter | Description |
|------|------|
| `--config` | Path to site.yaml, defaults to current directory `site.yaml` |
| `--site` | Multi-site mode: specify `sites/<name>.yaml` |
| `--output` | Override output directory |
| `--base-url` | Override site baseUrl |
| `--site-url` | Override site URL (used for sitemap/RSS absolute links) |
| `--clean` / `--no-clean` | Force enable/disable pre-build clean |
| `--draft` | Include content marked as draft |
| `--ci` | CI mode (log level auto-set to warn) |
| `--incremental` / `--no-incremental` | Enable/disable incremental build |
| `--cache-dir` | Override cache directory |
| `--metrics` | Output JSON build metrics to specified file |
| `--jobs` | Parallel rendering concurrency (positive integer) |
| `--log-format` | Log format: `text` (default) or `json` |

**Working directory requirement:** Must be run from the site root containing `site.yaml`.

**Exit code:** 0 = success

### init / create

Initialize a new Bukit site in the current directory.

```
bukit init <target-dir> [--provider markdown|notion] [--template minimal]
```

Generated directory structure:
```
<target-dir>/
  site.yaml
  content/
    hello-world.md
  themes/starter/
    layouts/layouts/base.html
    layouts/pages/{page,post,index,list,pagination,search,taxonomy-index,taxonomy-term}.html
    layouts/partials/{header,footer,list-card,pagination-nav}.html
    layouts/bukit.templates.yaml
    assets/style.css
    static/
  .gitignore
  README.md
```

`--provider notion` generates a site.yaml pre-configured for Notion content source; `--provider markdown` (default) generates Markdown content source config. The generated `themes/starter/` is a content-site starter theme with responsive CSS, reusable partials, and optional pagination/search/taxonomy templates.

### preview

Start a local HTTP file server to preview build output.

```
bukit preview [--dir <dir>] [--host <host>] [--port <port>] [--strict-port]
```

| Parameter | Default | Description |
|------|--------|------|
| `--dir` | `dist` | Directory to preview |
| `--host` | `localhost` | Listen address |
| `--port` | `4173` | Listen port (`auto` = auto-select free port) |
| `--strict-port` | false | Error immediately on port conflict, no auto-switch |

**Port selection logic:**
- Default port 4173 → try 4174 if busy, up to 20 attempts
- `auto` mode: system assigns a free port
- `--strict-port` mode: error on port conflict

**MIME type support:** HTML, CSS, JS, JSON, XML, SVG, PNG, JPG, GIF, TXT

### clean

Clean output and cache directories.

```
bukit clean [--config <path>] [--site <name>] [--dir <dir>]
```

Deletes:
- Output directory (default `dist`, read from site.yaml)
- `.cache/` directory (incremental build manifests, etc.)
- `.bukit/` directory

### doctor

Diagnose site configuration and template health.

```
bukit doctor [--config <path>] [--site <name>] [--site-url <url>]
```

Checks:
1. Config loading and validation
2. Collections configuration readiness (prompts migration if missing)
3. Template file existence (base.html, page.html, post.html, index.html, list.html)
4. Template syntax parsing
5. Template capabilities manifest validation
6. Assets and Static directory existence
7. Build manifest JSON format
8. Plugin discovery count
9. Notion database reachability (if Notion content source configured)
10. List page content mode heuristic fallback warnings

### plugin list

List all registered plugins and their status under the current configuration.

```
bukit plugin list [--config <path>] [--site <name>]
```

Output format:
```
PluginName@1.0.0 [BuiltIn] enabled=true (derive-pages, after-build)
PluginName@1.0.0 [ExternalAssembly] enabled=false (after-build)
```

### theme

```
bukit theme list [--config <path>] [--site <name>]
bukit theme create <name> [--from starter|<existing-theme>] [--brand <text>] [--primary-color <hex>] [--accent-color <hex>] [--use] [--force] [--config <path>] [--site <name>]
bukit theme use <name> [--config <path>] [--site <name>]
```

`theme list` lists all valid theme names in the `themes/` directory.
`theme create` creates `themes/<name>/`; by default it uses the built-in starter, and `--from` copies an existing local theme.
`theme use` modifies `theme.name` in site.yaml to the specified theme name.

### intent

Intent-driven configuration: generate site.yaml through interactive Q&A or an intent file.

```
bukit intent init [--out <intent.yaml>]    # Interactive intent creation
bukit intent validate <intent.yaml>        # Validate intent file
bukit intent apply <intent.yaml> [--out <path>]  # Apply intent to generate site.yaml
```

## Exit Codes

| Exit Code | Meaning |
|--------|------|
| 0 | Success |
| 1 | Runtime error (config error, template error, Notion connection failure, etc.) |
| 2 | Parameter error (unknown command, invalid parameter, missing required parameter) |

## Cross-Platform Execution Notes

| Scenario | Guidance |
|------|------|
| Windows | May need `.\bukit.exe` or `./bukit.exe`. In PowerShell, use `& .\bukit.exe <cmd>` |
| Linux/macOS | `./bukit`, may need `chmod +x bukit` first. Place in `/usr/local/bin/` for global access |
| Working directory | Always run from the site root (directory containing `site.yaml`) |
| Output encoding | Non-English Windows environments may have encoding issues |
| First build | `build` creates `dist/` directory; first build is always full (no incremental skip) |
| stderr version output | All commands except `version` output the version number to stderr — not an error |

## Typical Agent Workflow

User says "help me build a Bukit blog":

```
1. Detect CLI: bukit version
   → CLI unavailable → guide installation
   → CLI available → continue

2. Initialize: bukit init ./my-blog --provider markdown

3. Load bukit-config skill → modify site.yaml as needed

4. Load bukit-theme skill → adjust theme as needed

5. Load bukit-templating skill → write templates as needed

6. Build: bukit build

7. Preview (optional): bukit preview
```

## Common Errors

| Symptom | Cause | Fix |
|---------|------|------|
| `Unknown command: xxx` | Command name typo | Check command name; run `bukit` or `bukit help` for full list |
| `init requires a target directory` | No target directory specified | `bukit init ./my-site` |
| `Directory not found: dist` | Not built before preview or output cleaned | Run `bukit build` first |
| `Failed to listen on ... (port conflict)` | Port occupied and strict-port mode | Change port `--port 8080` or use `--port auto` |
| Config loading failed | site.yaml missing or YAML syntax error | Check path, ensure valid YAML syntax |
| Notion connection failed (401) | NOTION_TOKEN not set or invalid | Set env var `NOTION_TOKEN` |
| Notion connection failed (404) | Wrong databaseId | Check content.notion.databaseId in site.yaml |
| `Config error` (doctor) | site.collections not configured | Add collections config per doctor prompt |
| `Missing templates` (doctor) | Template files missing | Ensure 5 required template files exist under themes/<name>/layouts/ |

## Environment Variables

| Variable | Purpose | Related Commands |
|------|------|---------|
| `NOTION_TOKEN` | Notion API key | build, doctor |
| `BUKIT_WEBHOOK_TOKEN` | Webhook authentication token | webhook |
| `BUKIT_GITHUB_REPO` | GitHub repo name (owner/repo) | webhook |
| `BUKIT_GITHUB_TOKEN` | GitHub PAT | webhook |
| `GITHUB_TOKEN` | GitHub PAT (fallback) | webhook |
| `BUKIT_AUTO_SUMMARY` | Auto summary toggle (internal) | build |
| `BUKIT_AUTO_SUMMARY_MAXLEN` | Auto summary max length (internal) | build |
