# CLI Parameter Reference

This document is for maintainers and keeps command metadata aligned with implementation (`src/Bukit.Cli/Cli/BukitCliSpecs.cs` and `src/Bukit.Cli/Cli/BukitCliDescriptors.cs`).

## Supported top-level commands (current)

| Command | Purpose | Implementation |
|---|---|---|
| `build` | Generate static site output | `src/Bukit.Cli/Commands/BuildCommand.cs` |
| `config` | Config validation/schema generation | `src/Bukit.Cli/Commands/ConfigCommand.cs` |
| `clean` | Remove build output and `.cache/.bukit` | `src/Bukit.Cli/Commands/CleanCommand.cs` |
| `completion` | Generate shell completion scripts | `src/Bukit.Cli/Commands/CompletionCommand.cs` |
| `deploy` | Build and deploy to GitHub Pages | `src/Bukit.Cli/Commands/DeployCommand.cs` |
| `doctor` | Diagnostics for config, theme and templates | `src/Bukit.Cli/Commands/DoctorCommand.cs` |
| `geo` | GEO quality gate (`.bukit/geo-report.json`) | `src/Bukit.Cli/Commands/GeoCommand.cs` |
| `preview` | Serve built output as static preview server | `src/Bukit.Cli/Commands/PreviewCommand.cs` |
| `dev` | HMR development server with live reload | `src/Bukit.Cli/Commands/DevCommand.cs` |
| `publish` | Publish quality gate (`.bukit/publish-audit-report.json`) | `src/Bukit.Cli/Commands/PublishCommand.cs` |
| `seo` | SEO quality gate (`.bukit/seo-report.json`) | `src/Bukit.Cli/Commands/SeoCommand.cs` |
| `version` | Print version + runtime | `src/Bukit.Cli/Commands/VersionCommand.cs` |

Subcommands:
- `config check`
- `config schema`
- `seo audit`
- `seo diff`
- `geo audit`
- `publish audit`
- `publish diff`

## Override order (runtime)

For command options that map to config fields, overrides apply in this order:

1. CLI flags (`--output`, `--base-url`, `--site-url`, `--clean`, etc.)
2. Config file values
3. Runtime defaults

## Shared command options

| Flag | Effect |
|---|---|
| `--config <path>` | Path to entry config (default `site.yaml`) |
| `--site <name>` | Resolve from `sites/<name>.yaml` |
| `--ci` | CI logging mode; currently used by build/deploy |

## build

Implementation: `BuildCommand.RunAsync`.

```bash
bukit build --config site.yaml --clean --site-url https://example.com --draft
```

Options:
- `--output <dir>`
- `--base-url <path>`
- `--site-url <url>`
- `--clean` / `--no-clean`
- `--draft`
- `--incremental` / `--no-incremental`
- `--cache-dir <dir>`
- `--metrics <path>`
- `--jobs <n>`
- `--log-format text|json`

## config

### `config check`

```bash
bukit config check --config site.yaml --site demo --site-url https://example.com
```

### `config schema`

```bash
bukit config schema --output site.schema.json
```

If `--output` is omitted, schema is printed to stdout.

## doctor

```bash
bukit doctor --config site.yaml
```

Loads and validates:
- `site.yaml` schema
- theme manifest and required templates
- template syntax + variable usage + include references
- template capabilities completeness
- markdown checks (front matter, syntax, empty bodies)
- hardcoded URL checks and config/plugin wiring
- optional Notion connection checks when Notion source is configured

## preview

```bash
bukit preview --dir dist --port auto
```

Options:
- `--dir <path>` (default: `dist`)
- `--host <host>` (default: `localhost`)
- `--port <port|auto>` (default: `4173`, `auto` picks free port)
- `--strict-port` (fail on conflict)

## dev

```bash
bukit dev --config site.yaml --port 35729
```

Starts a development preview server that runs an initial build, serves the output directory, watches source/theme/static files, rebuilds incrementally, and reloads connected browsers over WebSocket.

Options:
- `--config <path>` (default: `site.yaml`)
- `--site <name>`
- `--host <host>` (default: `localhost`)
- `--port <port>` (default: `35729`, auto-increments when occupied)
- `--output <dir>` (overrides `build.output`)
- `--no-watch` (serve only, no file watching or live reload)

## clean

```bash
bukit clean --config site.yaml
```

Deletes:
- configured output dir or `--dir` (default `dist`)
- `.cache/`
- `.bukit/`

## deploy

```bash
bukit deploy --config site.yaml --dry-run
```

Important options:
- `--dry-run`
- `--skip-build`
- `--force`
- `--base-url`
- `--site-url`
- `--output`
- `--branch`
- `--message`
- `--ci`

Deploys only after invoking `build` unless `--skip-build` is set.

## seo / geo / publish

All three commands are report readers for outputs produced by the build:

- `seo audit [--dir dist] [--report file] [--strict] [--external]`
- `seo diff --baseline <old> --current <new> [--max-new-issues n] [--fail-on-route-removed] ...`
- `geo audit [--dir dist]`
- `publish audit [--dir dist] [--report file] [--strict] [--external]`
- `publish diff --baseline <old> --current <new> [--max-new-issues n] [--fail-on-indexable-drop] ...`

See [dev/seo.md](./seo.md) / [dev/geo.md](./geo.md) / [dev/publish-deploy.md](./publish-deploy.md) for the corresponding behavioral rationale.

## completion

```bash
bukit completion bash
bukit completion zsh
bukit completion fish
```

Argument contract: `bukit completion <shell>`.

## version

```bash
bukit version
```

Output:
- `bukit <version>`
- `runtime: native-aot`
