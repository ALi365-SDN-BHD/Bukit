---
name: bukit-cli-reference
description: Use when using bukit CLI — agent needs to execute Bukit commands (build, deploy, init, preview, clean, doctor, plugin, theme, intent, webhook, version), detect whether the Bukit CLI tool is installed, install or upgrade bukit, or interpret bukit build output and exit codes
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
| `theme list` | List available themes with metadata (version, description, tags) | `--config` `--site` |
| `theme create` | Create a theme from starter or an existing theme | `<name>` `--from` `--brand` `--primary-color` `--accent-color` `--use` `--force` `--config` `--site` |
| `theme use` | Switch current theme | `<name>` `--config` `--site` |
| `theme info` | Show full theme information (name, version, author, params, template files) | `<name>` `--config` `--site` |
| `theme params` | List customizable theme parameters (from theme.yaml) | `[name]` `--config` `--site` |
| `theme wizard` | Interactive Q&A theme creation + 5 presets | `<name>` `--preset`(blog\|docs\|landing\|minimal\|portfolio) `--use` `--force` `--config` `--site` |
| `theme pack` | Package theme as `<name>-<version>.tar.gz` | `[name]` `--config` `--site` |
| `theme install` | Install theme from local file, URL, or registry | `<path\|url>` `--registry <name>` `--force` `--config` `--site` |
| `theme search` | Query community theme registry | `[query]` `--refresh` `--registry-url <url>` `--config` `--site` |
| `template create` | Interactive template file creation | `<path>` `--force` `--config` `--site` |
| `template list` | List all templates in active theme | `--config` `--site` |
| `template show` | Print template content | `<path>` `--config` `--site` |
| `template validate` | Validate Scriban syntax of all templates | `--config` `--site` |
| `template snippets` | Browse template/CSS snippet library | `[name]` `--config` `--site` |
| `template hints` | Show available template variables reference | `--config` `--site` |
| `template sync` | Auto-generate bukit.templates.yaml from templates | `--force` `--config` `--site` |
| `intent init` | Interactive intent file creation | `--out` |
| `intent validate` | Validate intent file | `<intent.yaml>` `--root-dir` `--out` |
| `intent apply` | Apply intent to generate site.yaml | `<intent.yaml>` `--out` |
| `deploy` | Build and deploy to GitHub Pages | `--config` `--site` `--output` `--base-url` `--site-url` `--branch` `--message` `--ci` `--dry-run` `--skip-build` |
| `webhook` | Webhook server (Notion trigger → build + push) | `--host` `--port` `--path` `--token` `--repo` `--event` |
| `geo audit` | GEO audit on dist output | `--dir` `--config` |
| `seo` | SEO audit and regression detection | `audit` `--dir` `--strict` `--external`; `diff` `--baseline` `--current` `--max-new-*` `--fail-on-*` |
| `geo` | GEO audit for AI search engines | `audit` `--dir` |
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
3. Template file existence and parsing (all `.html` files under layouts)
4. Template capabilities manifest validation
5. **Template completeness report**: compares `bukit.templates.yaml` declarations vs actual files (missing/stale)
6. **Template chain analysis**: extracts `{% layout %}` inheritance chains and `{{ include }}` dependency references
7. **Unused parameter warnings**: `theme.params` declared in site.yaml but not referenced in any template
8. Assets and Static directory existence
9. Build manifest JSON format
10. Plugin discovery count
11. Notion database reachability (if Notion content source configured)
12. List page content mode heuristic fallback warnings
13. Route inventory validation (URL/outputPath conflict detection)

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
bukit theme info <name> [--config <path>] [--site <name>]
bukit theme params [name] [--config <path>] [--site <name>]
bukit theme wizard <name> [--preset blog|docs|landing|minimal|portfolio] [--use] [--force] [--config <path>] [--site <name>]
bukit theme pack [name] [--output <path>] [--config <path>] [--site <name>]
bukit theme install <path|url> [--registry <name>] [--registry-url <url>] [--force] [--config <path>] [--site <name>]
bukit theme search [query] [--refresh] [--registry-url <url>] [--config <path>] [--site <name>]
```

`theme list` displays themes with metadata from `theme.yaml` (version, description, tags, param count).
`theme create` creates `themes/<name>/`; by default it uses the built-in starter, and `--from` copies an existing local theme.
`theme use` modifies `theme.name` in site.yaml to the specified theme name.
`theme info` shows full theme details including parameter definitions and template file list.
`theme params` lists customizable parameters declared in `theme.yaml`.
`theme wizard` runs an interactive Q&A to create a custom theme. `--preset` applies one of 5 pre-defined designs (blog/docs/landing/minimal/portfolio) as defaults; without `--preset`, an interactive preset picker appears.
`theme pack` packages a theme into `<name>-<version>.tar.gz` for distribution. `<name>` defaults to the active theme.
`theme install` installs a theme from a local `.tar.gz`, HTTP URL, or `--registry <name>` (community theme registry with SHA256 verification).
`theme search` queries the community theme index (cached locally for 24h). `--refresh` forces a fresh fetch.

### template

```
bukit template create <path> [--force] [--config <path>] [--site <name>]
bukit template list [--config <path>] [--site <name>]
bukit template show <path> [--config <path>] [--site <name>]
bukit template validate [--config <path>] [--site <name>]
bukit template snippets [name]
bukit template hints
bukit template sync [--force] [--config <path>] [--site <name>]
```

`template create` interactively creates a new Scriban template file (single page / list page / partial) in the active theme's layouts directory.
`template list` lists all `.html` template files grouped by subdirectory with file sizes.
`template show` prints the content of a specific template.
`template validate` parses all templates with Scriban and reports syntax errors.
`template snippets` browses the built-in snippet library (8 Scriban + 9 CSS). `snippets <name>` shows a specific snippet.
`template hints` outputs a reference table of all available template variables (site/page/pages/scriban functions/layout directives).
`template sync` scans all template files and auto-generates/updates `layouts/bukit.templates.yaml` capability declarations.

### intent

Intent-driven configuration: generate site.yaml through interactive Q&A or an intent file.

```
bukit intent init [--out <intent.yaml>]    # Interactive intent creation
bukit intent validate <intent.yaml>        # Validate intent file
bukit intent apply <intent.yaml> [--out <path>]  # Apply intent to generate site.yaml
```

### seo

Audit and regression-detect traditional SEO health. Reads `seo-report.json` from the output directory.

```
bukit seo audit [--dir <dir>] [--report <path>] [--strict] [--external]
bukit seo diff --baseline <old> --current <new> [--max-new-errors N] [--max-new-warnings N] [--max-new-issues N] [--fail-on-new-code c1,c2] [--fail-on-route-removed] [--fail-on-indexable-drop]
```

**Subcommand: audit**

| Option | Default | Description |
|------|--------|------|
| `--dir` | `dist` | Output directory containing `seo-report.json` |
| `--report` | `<dir>/seo-report.json` | Explicit report path |
| `--strict` | off | Treat warnings as errors (exit code 1) |
| `--external` | off | Live HTTP validation of canonical URLs, links, and images (HEAD first, fallback to GET) |

Exit codes: 0 = pass, 1 = errors found (or warnings with `--strict`), 2 = report missing/invalid.

**Subcommand: diff**

| Option | Description |
|------|------|
| `--baseline <path>` | Previously accepted report |
| `--current <path>` | New report to validate |
| `--max-new-errors N` | Fail if new errors exceed N |
| `--max-new-warnings N` | Fail if new warnings exceed N |
| `--max-new-issues N` | Fail if total new issues exceed N |
| `--fail-on-new-code c1,c2` | Fail if specific codes appear (comma-separated) |
| `--fail-on-route-removed` | Fail if any route disappeared |
| `--fail-on-indexable-drop` | Fail if any route changed from indexable to non-indexable |

Exit codes: 0 = diff passed all budgets, 1 = budget exceeded, 2 = report missing/invalid.

### geo

Audit GEO (Generative Engine Optimization) readiness for AI-driven search engines.

```
bukit geo audit [--dir <dir>]
```

| Parameter | Default | Description |
|------|--------|------|
| `audit` | — | Subcommand (required) |
| `--dir` | `dist` | Output directory to audit |

Reads `seo-report.json` from the output directory and reports llms.txt/llms-full.txt status, GEO-enhanced routes, schema types, GEO Score, and geo.* diagnostic issues. Requires a full `bukit build` first.

Exit codes: 0 = success, 2 = directory or report not found, 2 = invalid report JSON.

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
| `Missing templates` (doctor) | Template files missing | Ensure 5 required template files exist under themes/<name>/layouts/ |
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
| `BUKIT_AUTO_SUMMARY` | Auto summary toggle (internal) | build |
| `BUKIT_AUTO_SUMMARY_MAXLEN` | Auto summary max length (internal) | build |
