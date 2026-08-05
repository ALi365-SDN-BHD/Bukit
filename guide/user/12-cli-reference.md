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
| `seo insights` | Join local SEO observations with the route map and write an offline insights report. | `--dir`, `--routes`, `--observations`, `--rules`, `--out`, `--strict-join` |
| `seo question-insights` | Join a local question target map with search question observations and write an offline coverage report. | `--dir`, `--routes`, `--targets`, `--observations`, `--rules`, `--out`, `--strict-join` |
| `seo generative-insights` | Join local generative answer observations with the route map and write an offline citation report. | `--dir`, `--routes`, `--observations`, `--rules`, `--out`, `--strict-join` |
| `seo authority-insights` | Join local external authority observations with the route map and write an offline citation evidence report. | `--dir`, `--routes`, `--observations`, `--rules`, `--out`, `--strict-join` |
| `geo` | Parent command for GEO reports. | `--dir` |
| `geo audit` | Validate `.bukit/geo-report.json`. | `--dir` |
| `publish` | Parent command for publish audit reports. | `--dir`, `--report`, `--strict`, `--external` |
| `publish audit` | Validate `.bukit/publish-audit-report.json`. | `--dir`, `--report`, `--strict`, `--external` |
| `publish diff` | Compare publish audit reports. | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `deploy` | Build if needed and deploy to GitHub Pages. | `--config`, `--site`, `--dry-run`, `--skip-build`, `--base-url`, `--site-url`, `--output`, `--branch`, `--message`, `--ci`, `--force` |

`bukit clean --dir <path>` only removes an empty directory or a Bukit output
directory containing `.bukit-output-marker`. It refuses the project root,
user home, filesystem root, `.git` or a `.git` descendant, paths outside the
current project, targets reached through symlink/reparse-point segments, and
non-empty unmarked directories. A refusal returns exit code 2 and preserves the
target. Config-based clean and build recovery use the same cleaner.

## Common Examples

```bash
bukit config check --config site.yaml
bukit doctor --config site.yaml
bukit build --config site.yaml --clean --metrics .bukit/metrics.json
bukit dev --config site.yaml --port 35729
bukit preview --dir dist --port 4173
bukit seo audit --dir dist --strict
bukit seo insights --dir dist --observations incoming/gsc.json,incoming/ga4.json --rules seo-insights-rules.json
bukit seo question-insights --dir dist --targets observations/question-targets.json --observations observations/gsc-questions.json --rules seo-insights-rules.json
bukit seo generative-insights --dir dist --observations observations/generative-runs.json --rules seo-insights-rules.json
bukit seo authority-insights --dir dist --observations observations/external-authority.json --rules seo-insights-rules.json
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

`seo insights` refines the generic table: it returns `0` after writing the
report when joins are complete or gaps are allowed, `1` only when
`--strict-join` finds unmatched/ambiguous rows (after writing the report), and
`2` for input, local-path, schema, or read/write failures. See
[21 SEO Insights](21-seo-insights.md) for required options, defaults, and the
offline collector boundary.

`seo question-insights` uses the same exit-code refinement for its two-stage
join of question targets and search question observations. See
[22 SEO Question Insights](22-seo-question-insights.md) for required options,
defaults, and the privacy boundary.

`seo generative-insights` uses the same exit-code refinement for its join of
generative cited URLs with the route map. See
[23 Generative Citation Insights](23-generative-citation-insights.md) for
required options, defaults, and the privacy boundary.

`seo authority-insights` uses the same exit-code refinement for its join of
external cited URLs with the route map. See
[24 External Authority Insights](24-external-authority-insights.md) for
required options, defaults, and the privacy boundary.

## Dynamic Plugin Commands

If a project config exposes plugin commands through plugin manifests, `Program`
can compose those commands after Core command resolution. They are project
capabilities, not static Core commands.
