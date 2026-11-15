# CLI Contract

The static Core command contract lives in `BukitCliSpecs.cs`; dispatch lives in
`BukitCliDescriptors.cs`. `Program.cs` first resolves static Core commands, then
loads dynamic plugin descriptors only when the command is not a Core command.

## Command Table

| Command | Parameters |
|---|---|
| `build` | `--config`, `--site`, `--output`, `--base-url`, `--site-url`, `--clean`, `--no-clean`, `--draft`, `--ci`, `--incremental`, `--no-incremental`, `--cache-dir`, `--metrics`, `--jobs`, `--log-format`, `--notion-snapshot`, `--notion-offline`, `--notion-pin`, `--secrets-stdin` |
| `doctor` | `--config`, `--site`, `--site-url`, `--notion-snapshot`, `--notion-offline`, `--notion-pin`, `--secrets-stdin` |
| `config` | `--config`, `--site`, `--site-url`, `--output` |
| `config check` | `--config`, `--site`, `--site-url`, `--notion-snapshot`, `--notion-offline`, `--notion-pin`, `--secrets-stdin` |
| `config schema` | `--output` |
| `preview` | `--dir`, `--host`, `--port`, `--strict-port`, `--config`, `--site`, `--allow-lan`, `--public` |
| `dev` | `--config`, `--site`, `--host`, `--port`, `--output`, `--no-watch`, `--allow-lan`, `--public`, `--secrets-stdin` |
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
| `deploy` | `--config`, `--site`, `--dry-run`, `--skip-build`, `--base-url`, `--site-url`, `--output`, `--branch`, `--repo`, `--token-env`, `--secrets-stdin`, `--message`, `--ci`, `--force` |

## Error Handling

`ConfigException` and `ContentException` return exit code 2; `RenderException`
returns 3; unexpected exceptions return 1. `--log-format json` is treated as a
global error-rendering option and remains a build option only for `build`.

## Secrets on stdin

`Program.cs` strips the global `--secrets-stdin` flag before command resolution
and, for `build`, `dev`, `doctor`, `config check`, and `deploy`, loads one JSON
object (max 64 KiB) from stdin into `Bukit.Shared.SecretStore` before plugin
discovery or config loading. Every token read goes through `SecretStore.Get`
(stdin value first, then the environment); `EnvironmentHelper.GetNotionToken()`
and the deploy `--token-env` lookup both use it. Stdin secrets are never written
to an environment: the multilingual worker gets them over its stdin with
`--secrets-stdin`, and `ConsoleLogger`, top-level CLI errors, and
`PluginSecretMasker` redact their values. Invalid input returns exit 2 with
`secrets-stdin-invalid`. Plugin commands do not accept the flag. A token read
from stdin is handed by the deploy provider to git's credential helper over a
private local endpoint; it is not placed in the `git` child's environment or
command line. A token read from the environment remains in Bukit's environment
and is inherited by git children, as before.

## BukitJalil consumer contract

BukitJalil 0.1.0-beta.4 requires Bukit `MIN_BUKIT_VERSION = 2.2.3`. It reads these
surfaces directly; the tests below are the contract owners. Changing a row is a
consumer change, not an internal refactor: update BukitJalil's
`packages/server/test/bukit-behavior.test.ts` and `MIN_BUKIT_VERSION` in the same
release. Versions are compatible within the same minor line by default; crossing a
minor line is BukitJalil's decision.

| Surface | Contract test |
|---|---|
| `bukit version` prints `bukit <major>.<minor>.<patch>` | `ProgramEntryPointTests.Main_Version_PrintsVersionAndRuntime` |
| Build exit codes 0/2/3/1 and the cli-error document | `ProgramEntryPointTests` (`Main_BuildConfigError_WritesBktCode`, `Main_BuildSnapshotError_WritesBktCodeAndExitTwo`, `Main_BuildAssetCollision_StaysExitOneAndCliError`); `BukitJalilContractTests.Build_ParseFailure_ReturnsExitCodeThree` (3); `CliErrorRendererTests.RenderJson_Diagnostics_UsesDefaultSchemaAndExitCode` (document) |
| `--base-url` sets the template's `site.base_url` (site.yaml key `site.baseUrl`); named-theme `static/` stays at the output root | `BukitJalilContractTests.Build_BaseUrl_NamedThemeNormalizesAndKeepsStaticAtRoot` |
| `--secrets-stdin` JSON, exit 2, no echo; deploy reads `--token-env` from it first | `SecretsStdinCliTests`; `GitHubPagesDeployProviderTests.DeployAsync_CustomTokenEnv_ResolvedFromSecretsStdinBeforeEnvironment` |
| Offline snapshot 0B10/0B11; build 2 / check 1 / doctor 1 | `ProgramEntryPointTests.Main_BuildSnapshotError_WritesBktCodeAndExitTwo` (build 2); `NotionOfflineSnapshotCommandTests.ChangedDatabaseId_FailsConfigCheckDoctorAndBuildOffline` (check/doctor 1); `NotionOfflineSnapshotCommandTests.OptionMisuse_IsAnArgumentError` (doctor 1) |
| Notion `BKT-0Bxx` codes and line-start output, including 0B18/0B19 | `NotionOfflineSnapshotCommandTests.Pin_MissingSourceOrInvalidFile_FailsWithItsCode`; `DiagnosticCodeFormatterTests` |
| `event=content.source.loaded source=<name> count=<n>` in JSON `msg` | `BukitJalilContractTests.Build_JsonLog_EmitsContentSourceLoadedLinePerSource` |
| `.bukit/seo-report.json` and `.bukit/geo-report.json` consumer fields | `BukitJalilContractTests.Build_SeoAndGeoReportsMatchConsumerSchema` |
| `publish audit --dir` and `.bukit/publish-audit-report.json` `documents`/`issues` | `BukitJalilContractTests.PublishAudit_RealBuild_ReportMatchesDeployConsumerShape` |
| `config schema --output` writes parseable JSON | `BukitJalilContractTests.ConfigSchema_OutputIsJsonAtTopLevel` |
| Deploy token via stdin; git children see no token; credential broker | `GitCredentialBrokerIntegrationTests`; `GitCredentialBrokerWindowsIntegrationTests` |

The cli-error document is the last `{` ... `}` block on stderr, one line per
brace, with a schema starting `https://bukit.dev/schemas/cli-error`. JSON log
`level` values are `Info`/`Warn`/`Error` (and `Debug` at `logging.level: debug`);
consumers compare them case-insensitively. The **content.source.loaded** log line is
written at the default `logging.level: info` (or lower); `--ci` raises the level to
`warn` and suppresses it, so BukitJalil does not pass `--ci` to `build`.

## Dynamic Commands

`PluginCliLoader` reads project plugin config, validates manifests, checks
platform entries and SHA-256 values, performs handshake and manifest calls, then
composes command descriptors. Dynamic commands must not conflict with Core
command names or aliases.

## Build transactions

Single-language builds and direct Engine, `dev`, and `deploy` builds run in process without a Git or clean-checkout prerequisite. Explicit CLI builds with two or more languages use Git Worktrees and separate workers. The Engine transaction stages output, manifests, writable Notion caches, media downloads, and metrics JSON/HTML. Worktree builds coordinate output, normal cache, custom media and metrics after all workers succeed. Existing output and caches survive rendering, aggregation, report, gate, and pre-commit cancellation failures. `--no-clean` preserves files not owned by Bukit; clean and migration still enforce output safety and marker checks on the formal directory.

Builds fail promptly when their output, cache, or metrics resources overlap an active build, including parent/child directories and filesystem aliases. Independent resources remain concurrent. A highest missing parent is also a directory write resource and is staged as a whole; otherwise distinct leaf targets sharing that new parent therefore conflict until it has been committed. Readonly Notion caches are read without creating directories; an overlapping writer is rejected. A readonly subtree inside a staged parent must remain unchanged. Notion `off` adds no Notion cache operations. Media configuration retains its existing relative-path validation; custom download directories must not overlap template, assets, or static source trees, because those inputs cannot be safely overlaid by this transaction. Cache symlinks are rejected before use; passive output symlinks are preserved, but directly generated aggregate/report paths must not traverse them.

Writable targets use sibling staging and backup paths. Exchanges are coordinated, not a multi-directory atomic rename: a reader may briefly observe an absent directory. Exchange failure attempts reverse restoration; if restoration fails, the error identifies preserved backups for manual recovery. After process interruption, the next build attempts to recover the recorded transaction. It preserves unknown pending files and stops if a target changed or a backup is missing. Successful commit with backup-cleanup failure remains a successful build with a warning. Do not remove a preserved backup until the formal output and recovery state have been checked. Power-loss recovery and rollback of custom provider/plugin external side effects are not guaranteed.

`dev` serves the formal output and reloads only after commit. Its watcher excludes managed write resources and the reserved transaction namespace, including externally watched paths. Deploy's internal build uses the same Engine transaction; a failed build never reaches the deployment provider. Upload snapshot locking and deployment atomicity are separate concerns.
