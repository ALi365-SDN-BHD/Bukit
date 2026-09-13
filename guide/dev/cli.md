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
| `preview` | `--dir`, `--host`, `--port`, `--strict-port`, `--config`, `--site`, `--allow-lan`, `--public` |
| `dev` | `--config`, `--site`, `--host`, `--port`, `--output`, `--no-watch`, `--allow-lan`, `--public` |
| `clean` | `--dir`, `--config`, `--site` |
| `version` | none |
| `completion` | `<shell>` |
| `seo` | `--dir`, `--report`, `--strict`, `--external` |
| `seo audit` | `--dir`, `--report`, `--strict`, `--external` |
| `seo diff` | `--baseline`, `--current`, `--max-new-errors`, `--max-new-warnings`, `--max-new-issues`, `--fail-on-new-code`, `--fail-on-route-removed`, `--fail-on-indexable-drop` |
| `seo insights` | `--dir`, `--routes`, `--observations`, `--rules`, `--out`, `--strict-join` |
| `seo question-insights` | `--dir`, `--routes` (`--route-map` alias), `--targets`, `--observations`, `--rules`, `--out`, `--strict-join` |
| `seo generative-insights` | `--dir`, `--routes` (`--route-map` alias), `--observations`, `--rules`, `--out`, `--strict-join` |
| `seo authority-insights` | `--dir`, `--routes` (`--route-map` alias), `--observations`, `--rules`, `--out`, `--strict-join` |
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

## Build transactions

Single-language and multilingual builds run in the current process without a Git or clean-checkout prerequisite. The public Engine entry point stages output, manifests, writable Notion caches, media downloads, and metrics JSON/HTML before publishing them together. Existing output and caches survive rendering, aggregation, report, gate, and pre-commit cancellation failures. `--no-clean` preserves files not owned by Bukit; clean and migration still enforce output safety and marker checks on the formal directory.

Builds fail promptly when their output, cache, or metrics resources overlap an active build, including parent/child directories and filesystem aliases. Independent resources remain concurrent. A highest missing parent is also a directory write resource and is staged as a whole; otherwise distinct leaf targets sharing that new parent therefore conflict until it has been committed. Readonly Notion caches are read without creating directories; an overlapping writer is rejected. A readonly subtree inside a staged parent must remain unchanged. Notion `off` adds no Notion cache operations. Media configuration retains its existing relative-path validation; custom download directories must not overlap template, assets, or static source trees, because those inputs cannot be safely overlaid by this transaction. Cache symlinks are rejected before use; passive output symlinks are preserved, but directly generated aggregate/report paths must not traverse them.

Each writable target uses sibling `.bukit-txn-<id>-stage` and backup paths. Exchanges are coordinated, not a multi-directory atomic rename: a reader may briefly observe an absent directory. Exchange failure attempts reverse restoration; if restoration fails, the error identifies preserved backups for manual recovery. Successful commit with backup-cleanup failure remains a successful build with a warning. Do not remove a preserved backup until the formal output and recovery state have been checked. Only resources owned by the current transaction are automatically cleaned. No crash/power-loss recovery or rollback of custom provider/plugin external side effects is promised.

`dev` serves the formal output and reloads only after commit. Its watcher excludes managed write resources and the reserved transaction namespace, including externally watched paths. Deploy's internal build uses the same Engine transaction; a failed build never reaches the deployment provider. Upload snapshot locking and deployment atomicity are separate concerns.
