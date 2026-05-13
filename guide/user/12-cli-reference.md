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
| `doctor` | Environment/config self-check (first step in troubleshooting) |
| `clean` | Clean output directory and cache |
| `theme` | List/switch themes |
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

Specify template (default `minimal`):

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site --template minimal
```

The scaffold includes `themes/starter/`, a content-site starter theme with reusable partials, responsive CSS, and optional pagination/search/taxonomy templates.

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

## doctor: Self-Check & Troubleshooting (First Step)

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config site.yaml
```

Run doctor first when you encounter these issues:

- Missing Notion token
- Path does not exist (content/theme/build output)
- Config field errors, type mismatches

Troubleshooting checklist: [14 Troubleshooting](./14-troubleshooting.md).

## clean: Clean Output & Cache

```bash
dotnet run --project src/Bukit.Cli -c Release -- clean --dir dist
```

Recommended to run in these situations:

- Switched theme directory structure
- Made significant changes to routing rules/output modes
- Suspect incremental cache is causing "looks like it didn't update"

## theme: List & Switch Themes

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme list --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- theme use alt --config site.yaml
```

Theme usage: [08 Themes & Templates](./08-themes-templates.md).

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

## version: Check Version

```bash
dotnet run --project src/Bukit.Cli -c Release -- version
```

Outputs the current CLI version number.
