# CLI Contract

The static Core command contract lives in `BukitCliSpecs.cs`; dispatch lives in
`BukitCliDescriptors.cs`. `Program.cs` first resolves static Core commands, then
loads dynamic plugin descriptors only when the command is not a Core command.

## Command Table

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

## Error Handling

`ConfigException` and `ContentException` return exit code 2; `RenderException`
returns 3; unexpected exceptions return 1. `--log-format json` is treated as a
global error-rendering option and remains a build option only for `build`.

## Dynamic Commands

`PluginCliLoader` reads project plugin config, validates manifests, checks
platform entries and SHA-256 values, performs handshake and manifest calls, then
composes command descriptors. Dynamic commands must not conflict with Core
command names or aliases.
