---
name: bukit-cli-reference
description: Use when using bukit CLI — agent needs to execute Bukit commands (build, deploy, init, preview, clean, doctor, plugin, theme, intent, webhook, version), detect whether the Bukit CLI tool is installed, install or upgrade bukit, or interpret bukit build output and exit codes

status: stable
since: "v3.0.0"
verified_by:
  - "src/Bukit.Cli/Cli/BukitCliSpecs.cs"
source_anchors:
  - "src/Bukit.Cli/Cli/BukitCliSpecs.cs"
guide_chapters:
  - "guide/user/12-cli-reference.md"
  - "guide/user/16-parameter-cheatsheet.md"
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
bukit 1.x.x
runtime: jit   (or runtime: native-aot)
```

On Windows, if the `.exe` is not in PATH, use `.\bukit.exe` or `./bukit.exe`. In PowerShell, use `&` to invoke:

```powershell
& .\bukit.exe version
```

**Important note**: All commands except `version` will output the version number to stderr before execution (e.g., `bukit 1.x.x`). This is normal behavior, not an error.

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
| `build` | Build static site | `--config` `--output` `--base-url` `--draft` `--ci` `--incremental` / `--no-incremental` `--jobs` `--metrics` `--log-format` |
| `preview` | Static preview of dist/ | `--dir` `--host` `--port` `--strict-port` `--config` `--site` |
| `clean` | Clean output and cache directories | `--config` `--site` `--dir` |
| `config check` | Validate site.yaml without building | `--config` `--site` `--site-url` |
| `config schema` | Generate site.yaml JSON Schema | `--output` |
| `doctor` | Diagnose config and templates | `--config` `--site` `--site-url` |
| `completion` | Generate shell auto-completion script | `<shell>` (bash|zsh|fish) |
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
| `--clean` / `--no-clean` | Force enable/disable pre-build clean. When clean is enabled, Bukit verifies the output directory contains a `.bukit-output-marker` file (written on every successful build) before deleting it. Directories without this marker are not cleaned — this prevents accidental deletion of non-Bukit directories. |
| `--draft` | Include content marked as draft |
| `--ci` | CI mode (log level auto-set to warn) |
| `--incremental` / `--no-incremental` | Enable/disable incremental build |
| `--cache-dir` | Override cache directory |
| `--metrics` | Output JSON build metrics to specified file |
| `--jobs` | Parallel rendering concurrency (positive integer) |
| `--log-format` | Log format: `text` (default) or `json` |

**Working directory requirement:** Must be run from the site root containing `site.yaml`.

**Exit code:** 0 = success

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
| `--config` | `site.yaml` | Config file path |
| `--site` | — | Multi-site name |

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

### config check

Validate configuration without building the site.

```
bukit config check [--config <path>] [--site <name>] [--site-url <url>]
```

Checks:
1. Resolves config path (`site.yaml`, `--config`, or `sites/<name>.yaml`)
2. Loads YAML into the typed config model
3. Applies `--site-url` override when provided
4. Runs `ConfigValidator.Validate`

Does not load content, render templates, contact Notion, or run plugin hooks.

### config schema

Generate JSON Schema for editor tooling.

```
bukit config schema [--output <path>]
```

Without `--output`, writes the schema to stdout.

### doctor

Diagnose site configuration and template health.

```
bukit doctor [--config <path>] [--site <name>] [--site-url <url>]
```

Checks:
1. Config loading and validation
2. Collections configuration readiness (prompts migration if missing)
3. Template file existence and parsing (all `.html` files under layouts)
4. Template capabilities manifest validation
5. **Template completeness report**: compares `bukit.templates.yaml` declarations vs actual files (missing/stale)
6. **Template chain analysis**: extracts `{% layout %}` inheritance chains and `{{ include }}` dependency references
7. **Template variable spell check**: scans all Scriban templates for unknown variable references using AST analysis, cross-referenced against a known field whitelist for `page`/`site`/`pages`/`p`/`item`
8. **Unused parameter warnings**: `theme.params` declared in site.yaml but not referenced in any template
9. **Extra fields report**: raw content fields not declared in `content.modelSchema` canonical mappings, custom fields, field scopes, entity mappings, or relation mappings
10. Assets and Static directory existence
11. Build manifest JSON format
12. Plugin discovery count
13. Notion database reachability (if Notion content source configured)
14. List page content mode heuristic fallback warnings
15. Route inventory validation (URL/outputPath conflict detection)

All config errors are formatted with diagnostic codes in `BKT-XXXX` format:
```
✖ Config error
[BKT-0601] Refusing to clean unsafe output directory: /path/to/dir.
```

Template variable spell check output:
```
--- Template variable spell check ---
⚠ pages/index.html: Unknown variable 'site.settings' — did you mean 'site.params'?
✔ No unknown template variables detected

### completion

Generate shell auto-completion scripts for bash, zsh, or fish.

```
bukit completion <shell>
```

| Argument | Description |
|------|------|
| `<shell>` | Target shell: `bash`, `zsh`, or `fish` |

Writes the completion script to stdout. To install:
- **bash**: `bukit completion bash > /etc/bash_completion.d/bukit` or source it in `.bashrc`
- **zsh**: `bukit completion zsh > "${fpath[1]}/_bukit"`
- **fish**: `bukit completion fish > ~/.config/fish/completions/bukit.fish`

## Exit Codes

| Exit Code | Meaning | Exception Types |
|--------|------|------|
| 0 | Success | — |
| 1 | Unexpected error | Unhandled `Exception` (runtime failures, bugs) |
| 2 | Configuration or content error | `ConfigException`, `CommandArgumentException`, `ContentException` |
| 3 | Render error | `RenderException` |

> **v1.0.7+**: `ConfigPathResolver` path traversal (`--site ../../../etc/passwd`) now exits with code 2 (`ConfigException` + `BKT-0004`), consistent with other path traversal guards.

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

7. Dev server: bukit dev → HMR with live reload during development

8. Deploy (optional): bukit deploy → refer user to bukit-deploy skill and guide/user/13-deploy-github-pages.md
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
| `Missing templates` (doctor) | A required or referenced template file is missing | Check `theme.yaml templates`, `required: true`, collection templates, content route templates, and plugin template requirements |
| `Route inventory error` (doctor) | Route URL or outputPath conflicts detected | Fix conflicting slugs, URLs, or permalink patterns |
| `Route conflict on url` / `Route conflict on outputPath` (build) | Multiple content items generate identical URLs or output paths | Ensure unique slugs/outputPaths or adjust routing |

## Environment Variables

| Variable | Purpose | Related Commands |
|------|------|---------|
| `NOTION_TOKEN` | Notion API key | build, doctor |
| `BUKIT_WEBHOOK_TOKEN` | Webhook authentication token | webhook |
| `BUKIT_GITHUB_REPO` | GitHub repo name (owner/repo) | webhook |
| `BUKIT_GITHUB_TOKEN` | GitHub PAT | webhook |
| `GITHUB_TOKEN` | GitHub PAT (fallback) | webhook, deploy |
| `BUKIT_<SECTION>__<FIELD>` | Generic scalar config override, e.g. `BUKIT_SITE__URL` | config check, build, doctor |
| `BUKIT_AUTO_SUMMARY` | Auto summary toggle (internal) | build |
| `BUKIT_AUTO_SUMMARY_MAXLEN` | Auto summary max length (internal) | build |

## Breaking Changes (v2.8 / v3.0)

| Change | Old | New | Migration |
|------|------|------|------|
| Plugin toggle key | `site.plugins.rss` | `site.plugins.feed` | Rename `rss` → `feed` in site.yaml |
| Feed generation plugin | `RssPlugin` | `FeedPlugin` | Plugin now supports RSS + Atom + JSON Feed via `site.feed.formats`, default formats include RSS when `site.feed.formats` is unspecified. |
| Plugin count | 9 built-in | 13 built-in | New plugins include DataFilesPlugin, RelatedContentPlugin, AliasPlugin, MenuPlugin, ImageProcessingPlugin |
| Search index | `search.json` only | + `searchWeight`, `searchExclude` front matter, built-in search UI | Add `site.search` config for UI theme/placeholder |
