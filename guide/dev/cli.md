# CLI Parameter Reference

This document is for maintainers, explaining CLI commands, parameters, override relationships, and common usage.

Implementation references: `src/Bukit.Cli/Cli/BukitCliSpecs.cs`, `src/Bukit.Cli/Cli/Parsing/CliParser.cs`

## Command Overview

| Command | Purpose |
|---|---|
| `create <dir>` | Create a new site project (equivalent to `init`) |
| `init <dir>` | Initialize site project scaffold |
| `build` | Generate static site |
| `preview` | Local preview of output directory |
| `clean` | Clean output and cache |
| `doctor` | Environment and configuration diagnostics |
| `plugin` | Plugin-related commands |
| `theme` | Theme-related commands |
| `intent` | AI Intent related commands |
| `webhook` | Webhook trigger |
| `version` | Version info |

Note: When executing most commands, the CLI outputs a `bukit <version>` line first.

## Key Override Relationships

Build-related override order (highest to lowest):
1. CLI parameters (e.g. `--output/--base-url/--clean/--draft/--site-url`)
2. `site.yaml`
3. Code defaults (see `Bukit.Config` defaults and `ConfigLoader`)

## Common Build Parameters (shared by build/doctor etc.)

| Parameter | Purpose | Overrides |
|---|---|---|
| `--config <path>` | Specify config file path | Becomes config rootDir and relative path base |
| `--site <name>` | Multi-site reads `sites/<name>.yaml` | rootDir remains current directory |
| `--output <dir>` | Override output directory | Overrides `build.output` |
| `--base-url <path>` | Override baseUrl | Overrides `site.baseUrl` |
| `--site-url <url>` | Override site absolute URL | Overrides `site.url` (for sitemap/rss) |
| `--clean` | Clean before build | Overrides `build.clean=true` |
| `--no-clean` | Disable clean before build | Overrides `build.clean=false` |
| `--draft` | Render drafts | Overrides `build.draft=true` |
| `--ci` | CI mode | Affects log level policies |
| `--incremental` | Enable incremental build | Overrides incremental switch |
| `--no-incremental` | Disable incremental build | Overrides incremental switch |
| `--cache-dir <dir>` | Override cache directory | Default `<rootDir>/.cache` |
| `--jobs <n>` | Parallel rendering concurrency | Positive integer; default CPU core count |
| `--metrics <path>` | Output build metrics JSON | Relative path resolved against rootDir |
| `--log-format <text|json>` | Control log output format | Default `text` |

## build

Implementation: `src/Bukit.Cli/Commands/BuildCommand.cs`

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean
dotnet run --project src/Bukit.Cli -c Release -- build --site blog --clean
```
