# AD-01B3 Report: BuildContext and assembly decoupling

## Scope completed

- Removed `BuildContext.Config` and the Engine.Abstractions project/compiled
  assembly dependency on `Bukit.Config`.
- Preserved `BuildContext.ResolveTemplateKind` failure behavior as
  `ConfigException` with `DiagnosticCode.ConfigInvalidValue`.
- Kept every public `PluginRegistry`, `PluginRunner`, and plugin-interface
  signature.
- Routed config-free public plugin facades through one Engine-owned,
  deterministic strict/fail/all-enabled compatibility configuration.
- Kept all Core production paths on the explicit effective `AppConfig` paths
  introduced in AD-01B2.
- Removed `SiteEngine.GetListRoutes(BuildContext, ThemeTemplateResolver?)` and
  added the approved public overload accepting routed documents, collections,
  output-path encoding, and an optional template resolver.
- Preserved Doctor's list-route graph attachment through an internal overload
  that accepts the same explicit route/config leaves and stores only the
  derived graph in `BuildContext.Data`.
- Migrated Core and test `BuildContext` construction plus configuration-aware
  plugin tests without introducing an AppConfig mirror, context-data config,
  ambient/global config source, or weak-table side channel.
- Updated the governed public API baseline for only the approved 2.0 CLR
  changes.

No Labs or external plugin implementation, schema, plugin protocol, asset URL,
output ownership, path/security helper, CI/release/gate, or protected
backup/reference file was modified.

## RED evidence

Initial architecture/public-shape command:

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  --filter FullyQualifiedName~Ad01ConfigDecouplingTests \
  --no-restore --nologo
```

Expected RED: exit 1; 3 failed and 2 passed. The failures proved the old state:

- Engine.Abstractions still had a `Bukit.Config` project reference;
- `BuildContext.Config` was still public;
- the public SiteEngine BuildContext overload still existed and the new
  explicit overload did not.

During self-review, a temporary implementation changed the missing-template
resolver exception type. A focused compatibility test was added before the
correction:

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj \
  --filter FullyQualifiedName~BuildContext_MissingTemplateResolver_PreservesConfigDiagnostic \
  --no-restore --nologo
```

Expected RED: exit 1; the test expected `ConfigException` but observed
`InvalidOperationException`. The production method was then restored to its
original exception type, diagnostic code, and message semantics.

## GREEN evidence

Fresh focused project runs:

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj \
  --no-restore --nologo
```

Result: exit 0; 61 passed, 0 failed, 0 skipped.

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --no-restore --nologo
```

Result: exit 0; 1620 passed, 0 failed, 0 skipped.

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj \
  --no-restore --nologo --verbosity quiet
```

Result: exit 0; 618 passed, 0 failed, 0 skipped.

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  --no-restore --nologo --verbosity quiet
```

Result: exit 0; 264 passed, 0 failed, 0 skipped.

The first complete Architecture run found one stale AD-01B2 fixture:
`G04D9EBuiltInPluginGraphTests` still invoked `BuiltInPluginSource` through a
parameterless constructor. The fixture now passes an explicit `AppConfig`,
matching the existing production constructor, and the full rerun above is
green.

Public API owner checks:

```sh
env -u NOTION_TOKEN bash scripts/checks/public-api-drift-self-test.sh
env -u NOTION_TOKEN bash scripts/checks/public-api-drift.sh check Release
```

Result: both commands exited 0.

The required single `post-change-focused.sh` invocation is run after this report
is materialized so the report itself is included in the verified path set. Its
result is intentionally recorded in the task handoff rather than by modifying
this file after that one allowed gate run.

## Exact public API delta

The candidate was generated with:

```sh
env -u NOTION_TOKEN TMPDIR=/private/tmp \
  bash scripts/checks/public-api-drift.sh snapshot \
  /private/tmp/bukit-ad01b3-public-api.json Release
```

The candidate retained exactly 14 assembly mappings and 443 governed public
types. The comparison contained exactly these changes:

1. Removed
   `BuildContext.Config : Bukit.Config.AppConfig`.
2. Removed
   `SiteEngine.GetListRoutes(BuildContext, ThemeTemplateResolver?)`.
3. Added
   `SiteEngine.GetListRoutes(IReadOnlyList<RoutedContentDocument>,
   IReadOnlyDictionary<string, CollectionConfig>?, string,
   ThemeTemplateResolver?)`.

After the three-line semantic update, the governed baseline and generated
candidate were byte-for-byte identical. No type, assembly mapping,
classification, compatibility, migration-horizon, or unrelated member drift
was accepted.

The first snapshot attempt was environment-blocked because the isolated
worktree lacked the PublicApiDrift tool assets; restoring that existing tool
project resolved it. A subsequent `/tmp` output was rejected by the tool's
macOS canonical temporary-path guard, so the successful candidate used
`/private/tmp`. Neither event was treated as verification success.

## Changed paths

Production and governed-contract changes:

- `docs/governance/bukit-core-public-api-baseline.v1.json`
- `src/Bukit-Core/Bukit.Engine.Abstractions/Bukit.Engine.Abstractions.csproj`
- `src/Bukit-Core/Bukit.Engine.Abstractions/Plugins/BuildContext.cs`
- `src/Bukit-Core/Bukit.Engine/Plugins/PluginRegistry.cs`
- `src/Bukit-Core/Bukit.Engine/Plugins/PluginRunner.cs`
- `src/Bukit-Core/Bukit.Engine/SiteEngine.cs`
- `src/Bukit-Core/Bukit.Engine/SeoAlternatesService.cs`
- `src/Bukit-Core/Bukit.Engine/VariantRouteStage.cs`
- `src/Bukit-Core/Bukit.Cli/Commands/DoctorCommand.cs`

Tests:

- `tests/Bukit.Architecture.Tests/Ad01ConfigDecouplingTests.cs`
- `tests/Bukit.Architecture.Tests/AnalyticsPluginBoundaryTests.cs`
- `tests/Bukit.Architecture.Tests/G04D9EBuiltInPluginGraphTests.cs`
- `tests/Bukit.Engine.Abstractions.Tests/PluginModelTests.cs`
- the 27 affected explicit-configuration fixtures under
  `tests/Bukit.Engine.Tests/`

## Self-review

- Engine.Abstractions has no `Bukit.Config` project or compiled assembly
  reference; Engine and CLI retain their explicit Config dependencies.
- `BuildContext` has no Config member and stores no effective configuration in
  `Data`.
- All production plugin execution, registry construction, built-in behavior,
  Analytics state, Taxonomy behavior, Variant stages, SEO alternates, and
  Doctor paths use explicit effective configuration.
- Config-free public plugin facades use only the named deterministic
  compatibility configuration; they do not infer configuration from context
  state.
- Static built-in registration remains direct construction and
  reflection-free in production.
- Built-in count/order/name/version/source and aggregate-only ownership remain
  unchanged.
- Doctor still attaches the calculated list-route graph before collecting
  plugin template requirements; the pagination regression is green.
- `ResolveTemplateKind` retains its original `ConfigException`,
  `ConfigInvalidValue`, and message behavior.
- No AppConfig clone DTO or hidden configuration transport was introduced.
- No aggregate targeted, `ci-fast`, full, release, `test-all`, `smoke-all`, or
  Native AOT command was run.

## Commit

- Intended message: `refactor(core): decouple build context from config`
- Base commit before AD-01B3: `73d14e34`
- The final commit hash is reported in the handoff because a file inside the
  commit cannot embed that commit's stable hash.

## Concerns

- This is the approved 2.0 CLR migration. Consumers compiling directly against
  `BuildContext.Config` or the removed SiteEngine overload must migrate to
  explicit inputs.
- Native AOT publish proof was not run because it is outside this task's
  authorized verification boundary.
