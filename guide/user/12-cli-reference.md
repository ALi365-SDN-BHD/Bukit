# CLI Reference

Bukit Core 1.0 command metadata is defined in `src/Bukit.Cli/Cli/BukitCliSpecs.cs`.

| Command | Purpose |
|---|---|
| `build` | Build static output |
| `doctor` | Diagnose config, templates, routes, reports, and providers |
| `config` | Parent for config diagnostics |
| `config check` | Validate configuration without building |
| `config schema` | Generate the `site.yaml` JSON Schema |
| `preview` | Serve existing output locally |
| `dev` | Development server with file watching and browser reload |
| `clean` | Clean output and cache directories |
| `version` | Print version information |
| `completion` | Generate shell completion scripts |
| `seo` | SEO quality gate parent |
| `seo audit` | Validate `.bukit/seo-report.json` |
| `seo diff` | Compare SEO reports |
| `geo` | GEO quality gate parent |
| `geo audit` | Validate `.bukit/geo-report.json` and GEO outputs |
| `publish` | Publish-readiness gate parent |
| `publish audit` | Validate publish readiness |
| `publish diff` | Compare publish audit reports |
| `deploy` | Build and deploy to GitHub Pages |

## Common Commands

```bash
bukit version
bukit config check
bukit doctor
bukit build
bukit dev
bukit preview --dir dist
bukit seo audit --dir dist
bukit geo audit --dir dist
bukit publish audit --dir dist
bukit deploy --dry-run
```

## Build Options

Common options: `--config`, `--site`, `--output`, `--base-url`, `--site-url`, `--clean`, `--no-clean`, `--draft`, `--ci`, `--incremental`, `--no-incremental`, `--cache-dir`, `--metrics`, `--jobs`, `--log-format`.

## Preview and Dev

`preview` serves an existing directory. `dev` builds, watches files, and reloads connected browsers.

```bash
bukit preview --dir dist --port 4173
bukit dev --port 5173
bukit dev --allow-lan
```

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | Runtime, build, validation, or quality-gate failure |
| 2 | Command usage or argument failure |
