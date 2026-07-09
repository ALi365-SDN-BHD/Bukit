---
name: bukit-cli-reference
description: Stable Bukit Core CLI command reference generated from BukitCliSpecs.cs.
---

# Bukit CLI Reference

Use only these static Core commands:

| Command | Parameters |
|---|---|
| `build` | `--config`, `--site`, `--output`, `--base-url`, `--site-url`, `--clean`, `--no-clean`, `--draft`, `--ci`, `--incremental`, `--no-incremental`, `--cache-dir`, `--metrics`, `--jobs`, `--log-format` |
| `doctor` | `--config`, `--site`, `--site-url` |
| `config` | `--config`, `--site`, `--site-url`, `--output` |
| `config check` | `--config`, `--site`, `--site-url` |
| `config schema` | `--output` |
| `preview` | `--dir`, `--host`, `--port`, `--strict-port`, `--config`, `--site` |
| `dev` | `--config`, `--site`, `--host`, `--port`, `--output`, `--no-watch`, `--allow-lan`, `--public` |
| `clean` | `--dir`, `--config`, `--site` |
| `version` | none |
| `completion` | `<shell>` |
| `seo` | `--dir`, `--report`, `--strict`, `--external` |
| `seo audit` | `--dir`, `--report`, `--strict`, `--external` |
| `seo diff` | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `geo` | `--dir` |
| `geo audit` | `--dir` |
| `publish` | `--dir`, `--report`, `--strict`, `--external` |
| `publish audit` | `--dir`, `--report`, `--strict`, `--external` |
| `publish diff` | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `deploy` | `--config`, `--site`, `--dry-run`, `--skip-build`, `--base-url`, `--site-url`, `--output`, `--branch`, `--message`, `--ci`, `--force` |

Default loop:

```bash
bukit config check
bukit doctor
bukit build --clean
bukit publish audit --dir dist
```
