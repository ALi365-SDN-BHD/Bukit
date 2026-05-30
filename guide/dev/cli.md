# CLI Parameter Reference

This document is for maintainers, explaining CLI commands, parameters, override relationships, and common usage.

Implementation references: `src/Bukit.Cli/Cli/BukitCliSpecs.cs`, `src/Bukit.Cli/Cli/Parsing/CliParser.cs`

## Command Overview

| Command | Purpose |
|---|---|
| `create <dir>` | Create a new site project (equivalent to `init`) |
| `init <dir>` | Initialize site project scaffold |
| `build` | Generate static site |
| `dev` | HMR dev server (file watch + incremental build + live reload) |
| `preview` | Local preview of output directory |
| `clean` | Clean output and cache |
| `config check` | Validate configuration without building |
| `config schema` | Generate site.yaml JSON Schema |
| `doctor` | Environment and configuration diagnostics |
| `plugin` | Plugin-related commands |
| `theme` | Theme-related commands |
| `template` | Template-related commands |
| `intent` | AI Intent related commands |
| `deploy` | Build and deploy to GitHub Pages |
| `seo` | SEO audit and regression detection |
| `geo` | GEO (Generative Engine Optimization) audit |
| `clone` | Extract data from target website to generate theme and content |
| `webhook` | Webhook trigger |
| `data` | Data module inspection and export |
| `completion` | Generate shell auto-completion script |
| `lint` | Check config and Markdown content |
| `visual` | Generate Playwright visual regression test scripts |
| `docs` | Documentation consistency check |
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

## dev

Implementation: `src/Bukit.Cli/Commands/Dev/` (DevServerHost, DevWebSocketHub, DevFileWatcher, DevRequestHandler, DevPathGuard)

```
bukit dev [--config <path>] [--site <name>] [--host <host>] [--port <port>] [--output <dir>] [--no-watch]
```

Starts an HMR development server that watches content/themes/layouts/assets/static for changes, triggers incremental rebuilds, and pushes live reload via WebSocket. Default port is 35729. Use `--no-watch` for static serving without file monitoring.

## preview

Implementation: `src/Bukit.Cli/Commands/PreviewCommand.cs`

| Parameter | Default | Description |
|---|---|---|
| `--dir <path>` | `dist` | Directory to preview |
| `--host <host>` | `localhost` | Listen address |
| `--port <port\|auto>` | `4173` | `auto` auto-selects free port |
| `--strict-port` | false | Fail on port conflict (default retries incrementally) |

## Other Commands

For detailed parameter information on the following commands, refer to the corresponding `*Command.cs` implementation files and the [bukit-cli-reference skill](../../src/skills/bukit-cli-reference/SKILL.md):

- `config check` / `config schema`: `src/Bukit.Cli/Commands/ConfigCommand.cs`
- `doctor`: `src/Bukit.Cli/Commands/DoctorCommand.cs`
- `clean`: `src/Bukit.Cli/Commands/CleanCommand.cs`
- `theme`: `src/Bukit.Cli/Commands/ThemeCommand.cs`
- `template`: `src/Bukit.Cli/Commands/TemplateCommand.cs`
- `deploy`: `src/Bukit.Cli/Commands/DeployCommand.cs`
- `seo`: `src/Bukit.Cli/Commands/SeoCommand.cs`
- `geo`: `src/Bukit.Cli/Commands/GeoCommand.cs`
- `clone`: `src/Bukit.Cli/Commands/Clone/` (CloneInputLoader, CloneAssetDownloader, CloneContentWriter, CloneFidelityRunner, CloneThemeGenerator, CloneVerifier)
- `plugin`: `src/Bukit.Cli/Commands/PluginCommand.cs`
- `intent`: `src/Bukit.Cli/Commands/IntentCommand.cs`
- `webhook`: `src/Bukit.Cli/Commands/WebhookCommand.cs`
- `data`: `src/Bukit.Cli/Commands/DataCommand.cs`
- `completion`: `src/Bukit.Cli/Commands/CompletionCommand.cs`
- `lint`: `src/Bukit.Cli/Commands/LintCommand.cs`
- `visual`: `src/Bukit.Cli/Commands/VisualCommand.cs`
- `docs`: `src/Bukit.Cli/Commands/DocsCheck/DocsCheckCommand.cs`

See also:
- init/create scaffolding output and directory structure: [init/create](./init-create.md)
- doctor checks and common failures: [doctor](./doctor.md)
- clean and cache directory semantics: [cache-clean](./cache-clean.md)
- theme development and parameters: [theme](./theme.md)
- intent CLI and rootDir inference: [intent-cli](./intent-cli.md)
- webhook security constraints and environment variables: [webhook](./webhook.md)
