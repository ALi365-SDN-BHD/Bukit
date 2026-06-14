# CLI

The Core CLI surface is defined in `src/Bukit.Cli/Cli/BukitCliSpecs.cs` and
wired to handlers in `src/Bukit.Cli/Cli/BukitCliDescriptors.cs`.

Do not document a command in the Core guide unless it exists in that registry.

## Command Registry

| Command | Purpose | Key options or arguments |
|---|---|---|
| `build` | Build static output | `--config`, `--site`, `--output`, `--base-url`, `--site-url`, `--clean`, `--no-clean`, `--draft`, `--ci`, `--incremental`, `--no-incremental`, `--cache-dir`, `--metrics`, `--jobs`, `--log-format` |
| `doctor` | Diagnose config and templates | `--config`, `--site`, `--site-url` |
| `config` | Config diagnostics parent | `--config`, `--site`, `--site-url`, `--output` |
| `config check` | Validate config without building | `--config`, `--site`, `--site-url` |
| `config schema` | Generate JSON Schema | `--output` |
| `preview` | Serve existing output | `--dir`, `--host`, `--port`, `--strict-port`, `--config`, `--site` |
| `dev` | LiveReload development server | `--config`, `--site`, `--host`, `--port`, `--output`, `--no-watch`, `--allow-lan`, `--public` |
| `clean` | Clean output and cache directories | `--dir`, `--config`, `--site` |
| `version` | Print version information | none |
| `completion` | Generate shell completion | `<shell>` |
| `seo` | SEO quality gate parent | `--dir`, `--report`, `--strict`, `--external` |
| `seo audit` | Validate `.bukit/seo-report.json` | `--dir`, `--report`, `--strict`, `--external` |
| `seo diff` | Compare SEO reports | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `geo` | GEO quality gate parent | `--dir` |
| `geo audit` | Validate `.bukit/geo-report.json` | `--dir` |
| `publish` | Publish-readiness gate parent | `--dir`, `--report`, `--strict`, `--external` |
| `publish audit` | Validate `.bukit/publish-audit-report.json` | `--dir`, `--report`, `--strict`, `--external` |
| `publish diff` | Compare publish audit reports | same diff options as `seo diff` |
| `deploy` | Build and deploy to GitHub Pages | `--config`, `--site`, `--dry-run`, `--skip-build`, `--base-url`, `--site-url`, `--output`, `--branch`, `--message`, `--ci`, `--force` |

## Normal Verification Chain

```bash
bukit config check
bukit doctor
bukit build
bukit seo audit --dir dist
bukit geo audit --dir dist
bukit publish audit --dir dist
```

## Exit Codes

| Code | Meaning |
|---|---|
| `0` | Success |
| `1` | Runtime, build, config, provider, or quality-gate failure |
| `2` | Usage, argument, command, missing directory, or invalid option failure |

## Maintainer Rules

- Update `BukitCliSpecs.cs`, `BukitCliDescriptors.cs`, tests, skills, and guide
  docs together when command shape changes.
- `dev` must be described as LiveReload or browser reload.
- A command blocked by `CoreBoundaryTests` is not a Core command.

