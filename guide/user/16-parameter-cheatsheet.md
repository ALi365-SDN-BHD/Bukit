# Parameter Cheatsheet

## Command Options

| Command | Common options |
|---|---|
| `build` | `--config`, `--site`, `--output`, `--base-url`, `--site-url`, `--draft`, `--ci`, `--incremental`, `--no-incremental`, `--metrics`, `--jobs`, `--log-format` |
| `doctor` | `--config`, `--site`, `--site-url` |
| `config check` | `--config`, `--site`, `--site-url` |
| `config schema` | `--output` |
| `preview` | `--dir`, `--host`, `--port`, `--strict-port`, `--config`, `--site` |
| `dev` | `--config`, `--site`, `--host`, `--port`, `--output`, `--no-watch`, `--allow-lan`, `--public` |
| `clean` | `--dir`, `--config`, `--site` |
| `seo audit` | `--dir`, `--report`, `--strict`, `--external` |
| `seo diff` | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `geo audit` | `--dir` |
| `publish audit` | `--dir`, `--report`, `--strict`, `--external` |
| `deploy` | `--config`, `--site`, `--dry-run`, `--skip-build`, `--base-url`, `--site-url`, `--output`, `--branch`, `--message`, `--ci`, `--force` |

## Config Value Sets

| Field | Values |
|---|---|
| `content.sources[].type` | `markdown`, `notion` |
| `content.sources[].mode` | `content`, `data` |
| `build.listPageContentMode` | `auto`, `always`, `never` |
| `build.schemaFailMode` | `off`, `warn`, `strict` |
| `build.fingerprintMode` | `size-time`, `sha256` |
| `build.report.securityFailMode` | `auto`, `off`, `warn`, `strict` |
| `site.pluginFailMode` | `strict`, `warn` |
| `site.deriveConflictPolicy` | `fail`, `warn`, `last-wins` |
| `site.seo.renderMode` | `theme`, `inject`, `off` |
| `site.seo.diagnostics` | `off`, `warn`, `strict` |
| `site.seo.geo.aiBotMode` | `allow`, `block`, `selective` |
| `deploy.provider` | `github-pages` |
