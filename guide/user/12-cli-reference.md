# 12 CLI Reference: Common Commands (User Edition)

This page tracks the currently supported `bukit` CLI commands in the active checkout.

For the full implementation-oriented reference, see [guide/dev/cli](./../dev/cli.md).

> Quick note: Most commands print `bukit <version>` to stderr before running. `version` is the only command that only prints version info.

## Command Overview

| Command | Purpose | Key Parameters |
|---|---|---|
| `build` | Build the static site | `--config`, `--site`, `--output`, `--base-url`, `--site-url`, `--clean`/`--no-clean`, `--draft`, `--ci`, `--incremental`/`--no-incremental`, `--cache-dir`, `--jobs`, `--metrics`, `--log-format` |
| `config check` | Validate `site.yaml` only | `--config`, `--site`, `--site-url` |
| `config schema` | Generate JSON Schema for `site.yaml` | `--output` |
| `doctor` | Configuration and template diagnostics | `--config`, `--site`, `--site-url` |
| `preview` | Preview a built output directory | `--dir`, `--host`, `--port`, `--strict-port`, `--config`, `--site` |
| `dev` | Live development preview with file watching and browser reload | `--config`, `--site`, `--host`, `--port`, `--output`, `--no-watch` |
| `clean` | Remove output/cache directories | `--config`, `--site`, `--dir` |
| `seo audit` | Validate `seo-report.json` | `--dir`, `--report`, `--strict`, `--external` |
| `seo diff` | Compare two SEO reports | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `geo audit` | Validate `geo-report.json` and llms file presence | `--dir` |
| `publish audit` | Validate `publish-audit-report.json` | `--dir`, `--report`, `--strict`, `--external` |
| `publish diff` | Compare two publish reports | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `deploy` | Build (by default) and deploy to GitHub Pages | `--config`, `--site`, `--dry-run`, `--skip-build`, `--base-url`, `--site-url`, `--output`, `--branch`, `--message`, `--ci`, `--force` |
| `completion` | Generate shell completion | `<shell>` (`bash|zsh|fish`) |
| `version` | Print installed CLI version | none |

## Runtime vs Source Build Invocation

If you installed `bukit` binary:

```bash
bukit build --clean --config site.yaml
```

If you are running from source:

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean --config site.yaml
```

## build

Build the site into static output (`dist` by default).

```bash
bukit build --config site.yaml --clean --site-url https://example.com
```

Common parameters:
- `--config <path>`: config file path (default `site.yaml`)
- `--site <name>`: use `sites/<name>.yaml`
- `--output <dir>`: override output directory
- `--base-url <path>`: override site base URL
- `--site-url <url>`: override `site.url` for absolute links
- `--clean` / `--no-clean`: enable/disable pre-build cleaning
- `--draft`: include draft content
- `--ci`: set warn-first logging for CI
- `--incremental` / `--no-incremental`: incremental toggle
- `--cache-dir <dir>`: cache directory override
- `--metrics <path>`: write build metrics JSON
- `--jobs <n>`: rendering concurrency (positive integer)
- `--log-format text|json`: output format

## config check

Validate configuration without building.

```bash
bukit config check --config site.yaml
```

Common parameters:
- `--config <path>`
- `--site <name>`
- `--site-url <url>`: temporary override for validation

## config schema

Generate `site.yaml` JSON schema.

```bash
bukit config schema --output site.schema.json
```

Omit `--output` to print schema to stdout.

## doctor

Run config/theme/template diagnostics and output warnings/errors.

```bash
bukit doctor --config site.yaml
```

Common parameters:
- `--config <path>`
- `--site <name>`
- `--site-url <url>`

## preview

Serve a built output folder over HTTP.

```bash
bukit preview --dir dist --host localhost --port auto
```

Parameters:
- `--dir <path>`: default `dist` (or inferred from config)
- `--host <host>`: default `localhost`
- `--port <port|auto>`: default `4173`, `auto` auto-selects a free port
- `--strict-port`: fail on port conflict instead of auto-incrementing
- `--config` / `--site`: if provided, output directory is inferred from config

## dev

Run an initial build, serve the output, watch files, and live-reload connected browsers.

```bash
bukit dev --config site.yaml
bukit dev --port 3000
bukit dev --no-watch
```

Parameters:
- `--config <path>` / `--site <name>`: choose the site config
- `--host <host>`: default `localhost`
- `--port <port>`: default `35729`, auto-increments if occupied
- `--output <dir>`: override output directory
- `--no-watch`: serve only, without file watching or live reload

## clean

Delete output and cache folders.

```bash
bukit clean --config site.yaml
```

Parameters:
- `--config <path>` or `--site <name>`: resolves output from `build.output`
- `--dir <path>`: explicit output directory (default `dist`)

## seo / geo / publish (audit commands)

### seo

Run SEO quality checks from `dist/.bukit/seo-report.json`:

```bash
bukit seo audit --dir dist --strict
```

`seo diff` compares two reports:

```bash
bukit seo diff --baseline old/seo-report.json --current dist/.bukit/seo-report.json
```

### geo

Run GEO report check (requires `dist/.bukit/geo-report.json`):

```bash
bukit geo audit --dir dist
```

### publish

Run publish quality checks from `dist/.bukit/publish-audit-report.json`:

```bash
bukit publish audit --dir dist
```

`publish diff` compares two reports:

```bash
bukit publish diff --baseline old/publish-audit-report.json --current dist/.bukit/publish-audit-report.json
```

## deploy

Build and deploy (default provider: GitHub Pages).

```bash
bukit deploy --config site.yaml --dry-run
```

Most important parameters:
- `--config <path>` / `--site <name>`
- `--skip-build`: skip pre-deploy build
- `--dry-run`: report-only plan
- `--force`: allow non-fast-forward push overwrite
- `--base-url`, `--site-url`, `--output`
- `--branch`: target Pages branch
- `--message`: commit message
- `--ci`: CI logging mode

## completion

Generate shell completion script:

```bash
bukit completion bash
bukit completion zsh
bukit completion fish
```

## version

```bash
bukit version
```

Outputs:

- `bukit <version>`
- `runtime: native-aot`

## If you are using an agent

The CLI contract used by agents is documented in [`src/skills/bukit-cli-reference/SKILL.md`](../../src/skills/bukit-cli-reference/SKILL.md).
