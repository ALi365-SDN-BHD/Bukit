# 12 CLI Reference

This table is derived from `BukitCliSpecs.cs`.

## Commands

| Command | Purpose | Parameters |
|---|---|---|
| `build` | Build the site. | `--config`, `--site`, `--output`, `--base-url`, `--site-url`, `--clean`, `--no-clean`, `--draft`, `--ci`, `--incremental`, `--no-incremental`, `--cache-dir`, `--metrics`, `--jobs`, `--log-format` |
| `doctor` | Diagnose config, content, theme, templates, and providers. | `--config`, `--site`, `--site-url` |
| `config` | Parent command for config checks. | `--config`, `--site`, `--site-url`, `--output` |
| `config check` | Validate config without building pages. | `--config`, `--site`, `--site-url` |
| `config schema` | Emit JSON Schema for `site.yaml`. | `--output` |
| `preview` | Serve an existing output directory. | `--dir`, `--host`, `--port`, `--strict-port`, `--config`, `--site` |
| `dev` | Build, watch files, serve output, and trigger LiveReload. | `--config`, `--site`, `--host`, `--port`, `--output`, `--no-watch`, `--allow-lan`, `--public` |
| `clean` | Remove output and cache directories. | `--dir`, `--config`, `--site` |
| `version` | Print version. | none |
| `completion` | Print shell completion. | `<shell>` |
| `seo` | Parent command for SEO reports. | `--dir`, `--report`, `--strict`, `--external` |
| `seo audit` | Validate `.bukit/seo-report.json`. | `--dir`, `--report`, `--strict`, `--external` |
| `seo diff` | Compare SEO reports. | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `geo` | Parent command for GEO reports. | `--dir` |
| `geo audit` | Validate `.bukit/geo-report.json`. | `--dir` |
| `publish` | Parent command for publish audit reports. | `--dir`, `--report`, `--strict`, `--external` |
| `publish audit` | Validate `.bukit/publish-audit-report.json`. | `--dir`, `--report`, `--strict`, `--external` |
| `publish diff` | Compare publish audit reports. | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `deploy` | Build if needed and deploy to GitHub Pages. | `--config`, `--site`, `--dry-run`, `--skip-build`, `--base-url`, `--site-url`, `--output`, `--branch`, `--message`, `--ci`, `--force` |

`bukit clean --dir <path>` only removes an empty directory or a Bukit output
directory containing `.bukit-output-marker`. It refuses the project root,
`.git`, paths outside the current directory, and non-empty unmarked directories.

## Common Examples

```bash
bukit config check --config site.yaml
bukit doctor --config site.yaml
bukit build --config site.yaml --clean --metrics .bukit/metrics.json
bukit dev --config site.yaml --port 35729
bukit preview --dir dist --port 4173
bukit seo audit --dir dist --strict
bukit publish audit --dir dist --strict
bukit deploy --dry-run
```

## Exit Codes

| Code | Meaning |
|---:|---|
| 0 | Success. |
| 1 | General runtime failure. |
| 2 | Command argument, config, or content setup error. |
| 3 | Render error. |

## Dynamic Plugin Commands

If a project config exposes plugin commands through plugin manifests, `Program`
can compose those commands after Core command resolution. They are project
capabilities, not static Core commands.
