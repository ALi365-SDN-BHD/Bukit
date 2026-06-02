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
| `init` | Initialize site scaffolding | `<target-dir>` `--provider`(markdown/notion) `--template`(minimal\|blog\|docs\|landing\|portfolio) |
| `create` | Alias for `init` | Same as above |
| `build` | Build static site | `--config` `--output` `--base-url` `--draft` `--ci` `--incremental` / `--no-incremental` `--jobs` `--metrics` `--log-format` `--allow-external-plugins` |
| `dev` | HMR dev server (watch + live reload) | `--config` `--site` `--host` `--port` `--output` `--no-watch` |
| `preview` | Static preview of dist/ | `--dir` `--host` `--port` `--strict-port` `--config` `--site` |
| `clean` | Clean output and cache directories | `--config` `--site` `--dir` |
| `config check` | Validate site.yaml without building | `--config` `--site` `--site-url` |
| `config schema` | Generate site.yaml JSON Schema | `--output` |
| `doctor` | Diagnose config and templates | `--config` `--site` `--site-url` |
| `plugin list` | List registered plugins (13 built-in) | `--config` `--site` |
| `theme list` | List available themes with metadata (version, description, tags) | `--config` `--site` |
| `theme create` | Create a theme from starter or an existing theme | `<name>` `--from` `--brand` `--primary-color` `--accent-color` `--use` `--force` `--config` `--site` |
| `theme use` | Switch current theme | `<name>` `--config` `--site` |
| `theme info` | Show full theme information (name, version, author, params, template files) | `<name>` `--config` `--site` |
| `theme params` | List customizable theme parameters (from theme.yaml) | `[name]` `--config` `--site` |
| `theme wizard` | Interactive Q&A theme creation + 5 presets | `<name>` `--preset`(blog\|docs\|landing\|minimal\|portfolio) `--use` `--force` `--config` `--site` |
| `theme pack` | Package theme as `<name>-<version>.tar.gz` | `[name]` `--output` `--config` `--site` |
| `theme install` | Install theme from local file, URL, or registry | `<path\|url>` `--registry <name>` `--registry-url` `--force` `--config` `--site` |
| `theme search` | Query community theme registry | `[query]` `--refresh` `--registry-url <url>` `--config` `--site` |
| `theme preview` | Display detailed theme anatomy | `[name]` `--config` `--site` |
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
| `webhook` | Webhook server (Notion trigger → GitHub repository_dispatch) — note: `start` arg in help is not CLI-registered | `--host` `--port` `--path` `--repo` `--event` |
| `clone` (beta) | Generate Bukit theme and content from target website | `--tokens` `--theme` `--layout` `--page` `--sections` `--behaviors` `--icons` `--assets` `--brand` `--use` `--force` `--verify` `--visual-threshold` `--fail-on-visual-diff` `--fidelity` `--config` `--site` |
| `import html-demo` | Convert an HTML demo directory into a buildable Bukit theme/site draft | `<demo-dir>` `--theme` `--force` `--verify` `--content-source`(notion\|json\|yaml) `--site-path` `--strict` `--push-notion` `--notion-database-id` `--notion-database-map` `--create-missing-notion-databases` |
| `import seed` | Convert generated JSON/YAML seed into local Markdown content | `<seed-dir>` `--output` `--force` |
| `notion push` | Push generated seed records to one or more Notion databases; use `--dry-run` for a reviewable local plan | `--input` `--database-id` `--database-map` `--create-missing-databases` `--parent-page-id` `--dry-run` `--report` `--token-env` |
| `geo audit` | GEO audit on dist output | `--dir` |
| `seo audit` | Audit SEO health from build report | `--dir` `--report` `--strict` `--external` |
| `seo diff` | Compare SEO reports for regression budgets | `--baseline` `--current` `--max-new-errors N` `--max-new-warnings N` `--max-new-issues N` `--fail-on-new-code c1,c2` `--fail-on-route-removed` `--fail-on-indexable-drop` |
| `data inspect` | List all data modules (Markdown/Notion content + data sources) | `--config` `--site` `--module` |
| `data dump` | Export data module content as JSON | `--config` `--site` `--format` |
| `completion` | Generate shell auto-completion script | `<shell>` (bash\|zsh\|fish) |
| `lint` | Check config and Markdown content | `--config` `--site` |
| `visual generate` | Generate Playwright visual test script | `--config` `--dir` `--site-url` `--out` |
| `docs check` (beta) | Check documentation consistency (README/guide/skills) | `--cli` `--config-fields` `--file-refs` `--examples` `--skills` |
| `version` | Output version number | No parameters |
| `route inspect` | List all routes with optional JSON output and collection filtering | `--json` `--collection` `--config` `--site` |
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
| `--allow-external-plugins` | Allow loading external protocol plugins (overrides `site.externalPluginPolicy`) |

**Working directory requirement:** Must be run from the site root containing `site.yaml`.

**Exit code:** 0 = success

### init / create

Initialize a new Bukit site in the current directory.

```
bukit init <target-dir> [--provider markdown|notion] [--template minimal|blog|docs|landing|portfolio]
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
    assets/og-default.gif
    static/
  .gitignore
  README.md
```

`--provider notion` generates a site.yaml pre-configured for Notion content source; `--provider markdown` (default) generates Markdown content source config. The generated `themes/starter/` is a content-site starter theme with responsive CSS, reusable partials, and optional pagination/search/taxonomy templates. `--template minimal` keeps the default starter scaffold; `blog`, `docs`, `landing`, and `portfolio` reuse the theme wizard presets so the initial project has a site-type-specific visual direction and matching starter content.

Generated `site.yaml` includes `site.url: https://example.com` as a placeholder for absolute canonical, sitemap, RSS, and schema URLs, plus `site.seo.defaultImage: /assets/og-default.gif` for share previews. Replace the URL with the production URL before publishing, or override it with `--site-url`.

Template-specific Markdown scaffolds:
- `minimal`: `content/hello-world.md`, page default type
- `blog`: `content/posts/welcome.md` and `content/pages/about.md`, homepage data modules, post default type, dated blog routes, pagination, RSS/archive output
- `docs`: `content/docs/getting-started.md` and `content/docs/configuration.md`, homepage data modules, doc default type, `/docs/{slug}/` routes
- `landing`: `content/pages/overview.md` and `content/pages/contact.md`, feature and CTA homepage modules, page default type, flat page routes
- `portfolio`: `content/work/sample-project.md` and `content/pages/about.md`, homepage data modules, work default type, `/work/{slug}/` routes

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

### dev

Start HMR development server with file watching, incremental rebuild, and WebSocket live reload.

```
bukit dev [--config <path>] [--site <name>] [--host <host>] [--port <port>] [--output <dir>] [--no-watch]
```

| Parameter | Default | Description |
|------|--------|------|
| `--config` | `site.yaml` | Config file path |
| `--site` | — | Multi-site name |
| `--host` | `localhost` | Listen address |
| `--port` | `35729` | Listen port (auto-increment if occupied) |
| `--output` | `dist` | Output directory override |
| `--no-watch` | false | Disable file watching (serve only, no live reload) |

**How it works:**
1. Full initial build (Clean + Incremental)
2. Start HTTP server + WebSocket endpoint (`/__ws__`)
3. Watch content/, themes/, layouts/, assets/, static/ for changes
4. On change → 300ms debounce → incremental rebuild → WebSocket broadcast "reload" to all browsers

**Live reload:** Every HTML response is injected with a `<script>` tag that connects to the WebSocket endpoint for automatic browser refresh on rebuild.

**vs preview:** `bukit dev` is designed for active development with automatic rebuild and live reload. `bukit preview` only serves a pre-built `dist/` directory without file watching.

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
9. **Extra fields report**: front matter fields in content files not declared in collection schemas
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

### plugin list

List all registered plugins and their status under the current configuration.

```
bukit plugin list [--config <path>] [--site <name>]
```

Output format:
```
PluginName@1.0.0 [BuiltIn] enabled=true (derive-pages, after-build)
PluginName@1.0.0 [external-protocol] enabled=true (derive-pages)
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

### theme preview

Display detailed theme anatomy including sections, components, design tokens, and layout templates.

```
bukit theme preview [<name>]
```

| Parameter | Default | Description |
|---|---|---|
| `<name>` | Active theme | Theme name to preview |

**Output includes:**
- Basic metadata from `theme.yaml` (name, version, description, homepage, thumbnail, tags)
- Sections: registered page sections with descriptions and plugin associations (from `ThemeManifestV2`)
- Components: reusable components with declared props
- Design tokens: group counts (colors/font/radius/spacing/layout) with color sample preview
- Layout templates: all `.scriban`/`.html`/`.sbn` files under `layouts/`
- File stats: asset and static file counts

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

Exit codes: 0 = success, 1 = report or directory not found, 1 = invalid report JSON.

### data

Inspect and export data modules loaded by Bukit.

```
bukit data inspect [--config <path>] [--site <name>] [--module <name>]
bukit data dump [--config <path>] [--site <name>] [--format json]
```

| Subcommand | Description |
|------|------|
| `inspect` | Lists all data modules (content sources + data files) with row counts, column names, and sample values. Use `--module` to focus on a single module. |
| `dump` | Exports data module content as structured output (JSON format). |

`data inspect` shows a summary of every data module available to templates via `site.modules` and `site.data`. `data dump` writes the full content to stdout for inspection or piping.

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

### lint

Check configuration and Markdown content for issues without building.

```
bukit lint [--config <path>] [--site <name>]
```

| Parameter | Default | Description |
|------|--------|------|
| `--config` | `site.yaml` | Config file path |
| `--site` | — | Multi-site name |

Runs config validation and content checks (front matter consistency, required fields, schema violations) without triggering a full build. Useful as a pre-commit or CI lint gate.

### visual

Generate Playwright visual regression test scripts.

```
bukit visual generate [--config <path>] [--dir <dir>] [--site-url <url>] [--out <path>]
```

| Parameter | Default | Description |
|------|--------|------|
| `--config` | `site.yaml` | Config file path |
| `--dir` | `dist` | Output directory to scan for HTML pages |
| `--site-url` | `http://localhost:4173` | Base URL for test navigation |
| `--out` | `visual-tests.spec.js` | Output test script path |

Discovers all `.html` files in the output directory and generates a Playwright test script with `toHaveScreenshot()` assertions for each page. Typical workflow:

1. `bukit build`
2. `bukit visual generate --dir dist`
3. `npm init -y && npm install @playwright/test`
4. `npx playwright test visual-tests.spec.js --reporter=list`


### clone (beta)

Generate Bukit theme and content by extracting data from a target website.
Requires design token JSON files (typically produced by Browser MCP extraction).

```
bukit clone --tokens <file> --theme <name> [options]
```

| Parameter | Default | Description |
|------|--------|------|
| `--tokens` | - | Design tokens JSON file (required) |
| `--theme` | - | Target theme name (required) |
| `--layout` | - | Page layout JSON file |
| `--page` | - | Page metadata JSON file |
| `--sections` | - | Page sections JSON file |
| `--behaviors` | - | Interaction behavior JSON file |
| `--icons` | - | SVG icons JSON file |
| `--assets` | - | Static assets JSON file (auto-downloads images) |
| `--brand` | - | Brand name for navbar and footer |
| `--use` | false | Switch to this theme after creation |
| `--force` | false | Overwrite existing theme |
| `--verify` | false | Run doctor/build verification after generation |
| `--visual-threshold` | - | Visual screenshot diff threshold (0-1) |
| `--fail-on-visual-diff` | false | Fail when screenshot diff exceeds threshold |
| `--fidelity` | - | Fidelity mode: directly migrate HTML directory as templates |
| `--config` | site.yaml | Config file path |
| `--site` | - | Multi-site name |

Two modes:
- Standard mode: Requires --tokens. Flow: design tokens to theme generation to optional verification.
- Fidelity mode (--fidelity): Directly migrates a directory of HTML files as Scriban templates.

See bukit-clone skill for the full Browser MCP extraction workflow.

### route inspect

List all routes with optional JSON output and collection filtering.

```
bukit route inspect [--json] [--collection <name>] [--config <path>] [--site <name>]
```

| Parameter | Default | Description |
|------|--------|------|
| `--json` | false | Output in JSON format |
| `--collection` | - | Filter by collection name |
| `--config` | site.yaml | Config file path |
| `--site` | - | Multi-site name |

### docs check (beta)

Check consistency between README, user guide, skills documentation, and source code.

```
bukit docs check [--cli] [--config-fields] [--file-refs] [--examples] [--skills]
```

| Parameter | Default | Description |
|------|--------|------|
| `--cli` | false | Check CLI command coverage in docs |
| `--config-fields` | false | Check site.yaml field references |
| `--file-refs` | false | Check file path reference validity |
| `--examples` | false | Check README example parseability |
| `--skills` | false | Check Skill-CLI consistency |

Without any flags, all checks are performed. Use individual flags to limit scope.

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
| `BUKIT_<SECTION>__<FIELD>` | Generic scalar config override, e.g. `BUKIT_SITE__URL` | config check, build, doctor |
| `BUKIT_AUTO_SUMMARY` | Auto summary toggle (internal) | build |
| `BUKIT_AUTO_SUMMARY_MAXLEN` | Auto summary max length (internal) | build |

## Breaking Changes (v2.8 / v3.0)

| Change | Old | New | Migration |
|------|------|------|------|
| Plugin toggle key | `site.plugins.rss` | `site.plugins.feed` | Rename `rss` → `feed` in site.yaml |
| Feed generation plugin | `RssPlugin` | `FeedPlugin` | Plugin now supports RSS + Atom + JSON Feed via `site.feed.formats`. Backward compatible: RSS still generated by default. |
| Plugin count | 9 built-in | 13 built-in | New plugins include DataFilesPlugin, RelatedContentPlugin, AliasPlugin, MenuPlugin, ImageProcessingPlugin |
| Search index | `search.json` only | + `searchWeight`, `searchExclude` front matter, built-in search UI | Add `site.search` config for UI theme/placeholder |
