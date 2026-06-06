# 12 CLI Reference: Most Common Commands & Parameters (User Edition)

This page provides a "sufficient, easy to copy, minimal pitfalls" CLI cheat sheet for regular users. For a more complete maintainer version, see: [guide/dev/cli](../dev/cli.md).

Notes:
- You can use `bukit build --help`, `bukit preview --help`, `bukit theme --help` to view command-specific parameters
- Parameter names and defaults follow the CLI built-in help

## Command Overview (You'll Probably Only Use These)

| Command | When You Use It |
|---|---|
| `create <dir>` | Create a new site project (scaffold); also use `init` alias |
| `build` | Generate static site (output to dist/) |
| `preview` | Local preview of output directory |
| `config check` | Validate site.yaml without building |
| `config schema` | Generate site.yaml JSON Schema |
| `doctor` | Environment/config self-check (first step in troubleshooting) |
| `clean` | Clean output directory and cache |
| `theme` | Create, list, switch, explore, share, and install themes |
| `template` | Create, list, show, validate, sync, and browse template files |
| `clone` | Clone any website's visual design into a Bukit theme |
| `import` | Import HTML demos or seed files into Bukit theme/content drafts |
| `notion` | Generate Notion seed push plans and validate push prerequisites |
| `seo` | SEO audit and diff (validate seo-report.json) |
| `publish` | Machine-readability and trust audit (validate publish-audit-report.json) |
| `visual` | Generate Playwright visual test scripts |
| `webhook` | Notion change triggers GitHub Actions (optional) |
| `intent` | AI Intent related (optional) |
| `version` | Output version number |

Note:
- When executing most commands, the CLI will first output a line `bukit <version>` (for confirming the current running version; `help/version` are exceptions)

## Common Parameters (shared by build/doctor etc.)

| Parameter | Purpose | Typical Usage |
|---|---|---|
| `--config <path>` | Specify config file path | `--config site.yaml` / `--config examples/starter/site.yaml` |
| `--site <name>` | Multi-site reads `sites/<name>.yaml` | `--site blog` |
| `--output <dir>` | Override output directory | `--output dist` |
| `--base-url <path>` | Override baseUrl | `--base-url /my-repo` |
| `--site-url <url>` | Override site absolute URL | `--site-url https://user.github.io/my-repo` |
| `--clean` / `--no-clean` | Clean output directory before build | `--clean` |
| `--draft` | Render draft content | `--draft` |
| `--incremental` / `--no-incremental` | Incremental build toggle | `--no-incremental` (for troubleshooting) |
| `--cache-dir <dir>` | Cache directory | `--cache-dir .cache` |
| `--jobs <n>` | Parallel rendering concurrency (positive integer; default CPU core count) | `--jobs 8` |
| `--metrics <path>` | Output build metrics JSON | `--metrics metrics.json` |
| `--log-format <text|json>` | Log format | `--log-format json` (CI recommended) |
| `--ci` | CI mode (log level defaults to WARN) | `--ci` (GH Actions recommended) |

## create / init: Create a Site

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site
```

`init` is an equivalent alias for `create`:

```bash
dotnet run --project src/Bukit.Cli -c Release -- init my-site
```

Notion mode (scaffold generates corresponding config):

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site --provider notion
```

Specify template (default `minimal`; available: `minimal`, `blog`, `docs`, `landing`, `portfolio`):

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site --template blog
```

The scaffold includes `themes/starter/`, a content-site starter theme with reusable partials, responsive CSS, and optional pagination/search/taxonomy templates. Non-minimal templates reuse the same presets as `bukit theme wizard --preset ...`, so the first generated project already has a site-type-specific visual direction and matching starter content:

- `blog`: first post plus About page, dated blog URLs, pagination, RSS/archive output, and a blog-focused homepage
- `docs`: Getting Started and Configuration docs under `/docs/`, plus a docs-focused homepage
- `landing`: Overview and Contact pages with flat URLs, plus feature and call-to-action homepage modules
- `portfolio`: Sample work item under `/work/` plus About page, with a selected-work homepage

Generated `site.yaml` includes `site.url: https://example.com` as a placeholder for absolute SEO URLs and `site.seo.defaultImage: /assets/og-default.gif` for share previews. Replace the URL with your real production URL before publishing; you can also override it per build with `--site-url`.

## build: Build the Site (Most Common)

In the site directory:

```bash
dotnet run --project ../src/Bukit.Cli -c Release -- build --clean --site-url https://example.com
```

### GitHub Pages Sub-Path (baseUrl) Example

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean --base-url /my-repo --site-url https://user.github.io/my-repo
```

### Output metrics & structured logs (CI recommended)

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean --metrics metrics.json --log-format json
```

## preview: Local Preview of Output Directory

```bash
dotnet run --project src/Bukit.Cli -c Release -- preview --dir dist --port auto
```

Common parameters:

- `--dir <path>`: Preview directory (default `dist`)
- `--host <host>`: Default `localhost`
- `--port <port|auto>`: `auto` for auto port selection
- `--strict-port`: Strict port mode (error instead of auto-switching when port is occupied)

## config check: Validate Configuration Only

```bash
dotnet run --project src/Bukit.Cli -c Release -- config check --config site.yaml
```

Use this before a build when you only need to verify `site.yaml`. It loads the resolved config, applies `--site-url` if provided, runs config validation, and exits without loading content, rendering templates, or contacting Notion.

Common parameters:

- `--config <path>`: Config file path
- `--site <name>`: Multi-site config under `sites/<name>.yaml`
- `--site-url <url>`: Override `site.url` for validation

## config schema: Generate Configuration Schema

```bash
dotnet run --project src/Bukit.Cli -c Release -- config schema --output site.schema.json
```

Generates a JSON Schema for editor tooling such as VSCode/YAML LSP. Omit `--output` to print the schema to stdout.

## doctor: Self-Check & Troubleshooting (First Step)

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config site.yaml
```

Run doctor first when you encounter these issues:

- Missing Notion token
- Path does not exist (content/theme/build output)
- Config field errors, type mismatches
- **Template variable typos** — silently empty variables caught by spell check
- **Route conflicts** — detected and displayed with `[BKT-0201]` diagnostic codes

All config errors now display stable diagnostic codes:
```
✖ Config error
[BKT-0601] Refusing to clean unsafe output directory: /Users/xxx.

--- Template variable spell check ---
⚠ pages/index.html: Unknown variable 'site.settings' — did you mean 'site.params'?
✔ No unknown template variables detected
```

Troubleshooting checklist: [14 Troubleshooting](./14-troubleshooting.md).

## import: HTML Demo and Seed Import

```bash
# Convert an HTML demo into a buildable theme/site draft
bukit import html-demo ./demo --theme silkroadbiz --force --verify

# Convert generated JSON/YAML seed back into local markdown content
bukit import seed sites/silkroadbiz/data --output sites/silkroadbiz/content --force
```

`import html-demo` writes a theme under `themes/<theme>/`, a site config under `sites/<theme>/site.yaml`, markdown drafts under `sites/<theme>/content/`, seed review files, and `import-report.md`. When `--content-source notion` is used, the generated `notion-seed/` directory also includes a default `notion-database-map.yaml` template for multi-database push setup. `--build-source markdown` is the default and keeps builds local; use `--build-source notion` only with `--content-source notion` when the generated site should build directly from Notion and skip local markdown drafts. With a multi-database map, the generated `site.yaml` uses `content.sources[]` for `pages/posts/companies/services` as content sources and `navigation` as a data source injected into `site.modules.navigation`. `--verify` runs `doctor` and `build` against the generated site config.

`import seed` reads `pages/navigation/posts/companies/services` from `.json`, `.yaml`, or `.yml` files and writes markdown under the matching content/data folders. It is intended as a local build adapter; it does not write to Notion.

For the full workflow, review checklist, and Notion push decision tree, see [21 Import HTML Demo](./21-import-html-demo.md).

Common parameters:

- `--theme <name>`: required for `html-demo`
- `--content-source <notion|json|yaml>`: seed output type; markdown build drafts are still generated by default
- `--build-source <markdown|notion>`: build provider for the generated site, default `markdown`
- `--site-path <dir>`: target site directory, default `sites/<theme>`
- `--strict`: promote import warnings to blocking errors
- `--output <dir>`: target content directory for `import seed`

## notion: Seed Push Planning

```bash
bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --database-id <notion-database-id> \
  --dry-run

# Multi-database push using an explicit map
bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --database-map sites/silkroadbiz/notion-databases.yaml \
  --mode upsert

# Multi-database push with automatic database creation
bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --create-missing-databases \
  --parent-page-id <notion-parent-page-id> \
  --mode upsert

# Validate one target database before pushing
bukit notion validate-schema \
  --database-id <notion-database-id> \
  --report notion-schema-report.json
```

`notion push` produces a local push plan report from `notion-seed/*.json` so records can be reviewed before any external side effect. With `--database-id`, all supported seed records go into one Notion database. With `--database-map`, each seed file can target its own database; when `--database-map` is omitted, `notion push` also auto-loads `notion-seed/notion-database-map.yaml` if that file exists in the input directory. Without a map or single database ID, Bukit can derive one database target per collection, but it only creates missing databases when `--create-missing-databases --parent-page-id <id>` is explicit. Missing database IDs without that flag are a hard error. The default push scope is `pages/navigation/posts/companies/services`; generated `sections/faqs/media/components` seed files are review-only until collection-specific Notion schemas are added. Without `--dry-run`, the command validates that the configured token environment variable exists (`NOTION_TOKEN` by default, override with `--token-env`), validates each target database schema by default, and writes a report for the push.

Common parameters:

- `--input <dir>`: seed directory
- `--database-id <id>`: legacy single target Notion database ID
- `--database-map <file>`: YAML map from collection/seed file to Notion database
- `--create-missing-databases`: create missing mapped/default databases before pushing
- `--parent-page-id <id>`: Notion page under which missing databases are created
- `--generated-database-map <file>`: output path for the map containing created database IDs
- `--no-validate-schema`: skip push-time Notion database schema validation
- `--dry-run`: generate plan only
- `--report <file>`: output plan/report path
- `--token-env <name>`: token environment variable, default `NOTION_TOKEN`

`notion validate-schema` checks one Notion database for the properties expected by Bukit seed push. It requires `NOTION_TOKEN` by default and accepts `--token-env <name>` when you store the token in another environment variable.

## clean: Clean Output & Cache

```bash
dotnet run --project src/Bukit.Cli -c Release -- clean --dir dist
```

Recommended to run in these situations:

- Switched theme directory structure
- Made significant changes to routing rules/output modes
- Suspect incremental cache is causing "looks like it didn't update"

## theme: Theme Creation, Discovery, Sharing

```bash
# List all themes (shows version, description, tags)
bukit theme list --config site.yaml

# Create from starter
bukit theme create custom --config site.yaml --brand "My Site" --primary-color "#0b5fff" --use

# Interactive wizard (Q&A with preset selection)
bukit theme wizard my-blog

# Quick creation with preset
bukit theme wizard my-blog --preset blog

# View theme details
bukit theme info starter --config site.yaml

# List theme parameters
bukit theme params --config site.yaml

# Switch active theme
bukit theme use alt --config site.yaml
```

`theme create` creates `themes/<name>/` from the built-in starter by default. Use `--from <existing-theme>` to copy an existing theme, `--force` to overwrite, and `--use` to write `theme.name` back to the selected config.

`theme wizard` runs an interactive Q&A. Use `--preset` (blog/docs/landing/minimal/portfolio) for quick default-based creation.

### Theme Distribution

```bash
# Pack theme for sharing
bukit theme pack my-blog          # → my-blog-1.0.0.tar.gz

# Install from local file
bukit theme install ./my-blog-1.0.0.tar.gz

# Install from URL
bukit theme install https://github.com/user/theme/releases/download/v1.0/theme.tar.gz

# Search community theme registry
bukit theme search               # list all
bukit theme search blog          # filter by name/tags

# Install from registry
bukit theme install --registry blog-clean
```

## template: Template-level Management

```bash
# List all templates in active theme
bukit template list --config site.yaml

# View template content
bukit template show pages/index.html --config site.yaml

# Validate all templates' Scriban syntax
bukit template validate --config site.yaml

# Interactive template creation
bukit template create pages/gallery.html --config site.yaml

# Browse snippet library
bukit template snippets
bukit template snippets post-card

# Show all available template variables
bukit template hints

# Auto-generate bukit.templates.yaml
bukit template sync --config site.yaml
```

### theme preview

Display detailed theme information including sections, components, design tokens, and layout templates.

```
bukit theme preview [<name>]
```

| Parameter | Default | Description |
|---|---|---|
| `<name>` | Active theme | Theme name to preview |

**Output includes:**
- Basic metadata: name, version, description, homepage, thumbnail, tags
- Sections: count, descriptions, plugin associations
- Components: count and declared props
- Design tokens: group counts (colors/font/radius/spacing/layout) with color samples
- Layout templates: all `.scriban`/`.html`/`.sbn` files under `layouts/`
- File stats: asset and static file counts

Example output:
```
Theme preview: my-blog
Version:      1.0.0
Description:  A clean blog theme with dark mode support
Tags:         blog, minimal, dark-mode

Sections (4):
  hero                      Hero section with CTA
  features                  Feature grid section
  recent-posts              Recent posts list
  footer-cta                Footer call-to-action [plugin: sample-plugin]

Components (2):
  PostCard                  props: [title, url, date]
  TagBadge                  props: [tag]

Design tokens: colors (12), font (8), radius (4), spacing (10)
  Color samples:
    primary: #0b5fff
    accent: #0f7b6c
    bg: #fbfaf8
    text: #202124
    ... and 8 more

Layout templates (8):
  layouts/base.html
  pages/index.html
  pages/list.html
  pages/page.html
  pages/post.html
  partials/footer.html
  partials/header.html
  partials/list-card.html

Assets: 3 files  |  Static: 1 files
Local path:   /project/themes/my-blog
```

For detailed theme and template usage, see: [08 Themes & Templates](./08-themes-templates.md).

## webhook: Notion Changes Trigger GitHub Actions (Optional)

```bash
dotnet run --project src/Bukit.Cli -c Release -- webhook --repo owner/repo --port 8787 --path /webhook/notion --event bukit_notion
```

Available parameters:

- `--host <host>`: Listen address (default `localhost`)
- `--port <port>`: Listen port (default `8787`)
- `--path <path>`: HTTP path (default `/webhook/notion`)
- `--repo <owner/repo>`: Target repository
- `--event <type>`: repository_dispatch event type

It requires environment variables:

- `BUKIT_WEBHOOK_TOKEN` (inbound request header `X-Sitegen-Token`)
- `BUKIT_GITHUB_TOKEN` (or `GITHUB_TOKEN`)

Security and deployment details: [guide/dev/webhook](../dev/webhook.md).

## seo: Validate SEO Report Quality

```bash
# Audit current seo-report.json
bukit seo audit --dir dist --config site.yaml

# Audit with strict mode (warnings are failures too)
bukit seo audit --dir dist --strict

# Audit with external link/image verification
bukit seo audit --dir dist --external

# Compare two reports (regression check)
bukit seo diff --dir dist --config site.yaml

# Diff with budget control
bukit seo diff --max-new-errors 3 --max-new-warnings 5
bukit seo diff --fail-on-route-removed
bukit seo diff --fail-on-indexable-drop
```

`seo audit` validates `seo-report.json` (generated by `build`) — checks schema structure, counts errors/warnings, optionally verifies external links. `seo diff` compares against a previous report to detect regressions.

## publish: Validate Machine Readability & Trust

```bash
# Audit current publish-audit-report.json
bukit publish audit --dir dist

# Treat warnings as failures
bukit publish audit --dir dist --strict

# Compare publish audit reports
bukit publish diff --baseline previous/.bukit/publish-audit-report.json --current dist/.bukit/publish-audit-report.json
```

`publish audit` validates `.bukit/publish-audit-report.json`, the primary machine-readable report for semantic HTML, trust/provenance metadata, representation coverage, and aggregate output consistency. `seo audit` remains available for the compatibility SEO report.

## visual: Generate Visual Test Scripts

```bash
bukit visual generate [--config site.yaml] [--dir dist] [--site-url http://localhost:4173] [--out visual-tests.spec.js]
```

**Options:**

| Option | Purpose | Default |
|---|---|---|
| `--config` | Config file path | `site.yaml` |
| `--dir` | Output directory containing built HTML | `dist` |
| `--site-url` | Base URL for test page navigation | `http://localhost:4173` |
| `--out` | Output script file name | `visual-tests.spec.js` |

Generates a Playwright test script that takes full-page screenshots of every HTML page in the output directory and compares them against visual baselines.

**Usage flow:**
1. `bukit build`
2. `bukit visual generate --dir dist`
3. `npx playwright test visual-tests.spec.js --update-snapshots` (first run)
4. `npx playwright test visual-tests.spec.js` (subsequent runs)

Also see: `VisualFeedbackPlugin` (after-build plugin for AI-powered screenshot analysis with 5-dimension visual scoring).

## version: Check Version

```bash
dotnet run --project src/Bukit.Cli -c Release -- version
```

Outputs the current CLI version number.
