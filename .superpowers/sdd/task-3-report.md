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

## Reviewer Important fix: explicit plugin execution session

The first B3 implementation removed `BuildContext.Config`, but a recursive
review found three configuration-bearing object paths still reachable through
`BuildContext.Data`:

1. `__plugin_registry_cache` retained both `AppConfig` and the ten
   configuration-bound built-in plugin instances.
2. `__analytics_build_state` retained `AnalyticsBuildState._sourceConfig`.
3. `__taxonomy_index_cache` retained `TaxonomyConfig`.

A complete `Data` writer scan also found that `MenuPlugin` placed
`MenuConfig` records directly in `Data["menus"]`. That path had to be fixed to
make the reviewer invariant true for normal builds with non-empty menus.

### Review-fix RED evidence

Before production edits, the new session tests were run with:

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter FullyQualifiedName~PluginExecutionSessionTests \
  --no-restore --nologo --verbosity quiet
```

Result: expected exit 1. Compilation reported four `CS0103` errors because
`PluginExecutionSession` did not yet exist. The test contract was therefore
red for the missing explicit execution-state boundary rather than for an
unrelated environment failure.

### Review-fix implementation

- Added an Engine-internal, per-variant `PluginExecutionSession`.
- The session owns the effective plugin policy, one materialized set of ten
  registry registrations, and one `AnalyticsBuildState`.
- The same session is passed explicitly through variant derive, HTML
  transform, after-build, and reporting. It is never stored in
  `BuildContext.Data`.
- Public config-free `PluginRegistry` and `PluginRunner` signatures are
  unchanged and create an isolated deterministic compatibility session.
- Removed the registry cache and Analytics state from `BuildContext.Data`;
  removed `PluginCacheEntry.Config`, `_sourceConfig`, `Attach`, and
  `GetOrCreate`.
- Moved the Taxonomy index cache into each `TaxonomyPlugin` instance and
  explicitly reused it across template requirements, derive, data projection,
  and after-build. A new plugin/session receives an isolated cache.
- Projected menu data to plain dictionaries/lists with the same menu names,
  item order, and `identifier`, `name`, `url`, `weight`, and `children`
  fields. `menus.json` continues to use the existing writer and ordering.
- Doctor binds separate real-config sessions for its two different
  `BuildContext` instances. SEO alternates retain their narrow direct
  `TaxonomyPlugin(config)` path and do not construct the other nine plugins.
- The registry remains exactly ten ordered built-ins and excludes the four
  aggregate-only publish projections: feed, llms-txt, search-index, and
  sitemap.

### Review-fix GREEN evidence

Fresh focused runs after the fix:

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --no-restore --nologo --verbosity quiet
```

Result: exit 0; 1626 passed, 0 failed, 0 skipped.

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

The first run reported one stale source-text assertion for the old
`BuiltInPluginSource(config)` construction. After updating that assertion to
the explicit shared Analytics state constructor, the rerun passed: exit 0;
264 passed, 0 failed, 0 skipped.

```sh
env -u NOTION_TOKEN bash \
  scripts/checks/public-api-drift.sh check Release
```

Result: exit 0; build succeeded with 0 warnings and 0 errors. The governed
baseline remains 14 assemblies and 443 types, with no additional API delta.

Runtime regression coverage now proves:

- the same session reuses the same ten plugin instances;
- different sessions isolate registrations and Analytics state;
- production effective policy does not fall back to compatibility defaults;
- the Analytics transform updates the session-owned state and does not attach
  it to `Data`;
- Taxonomy indices are reused inside one session and rebuilt across sessions;
- a normal derive/transform/after-build path with nested menus leaves no
  object from the `Bukit.Config` assembly reachable from any `Data` value;
- the three removed hidden keys are absent.

Production source scans have no remaining references to the three removed
cache keys/entries and no direct assignment of config objects into
`BuildContext.Data`.

The required single review-fix `post-change-focused.sh` invocation is run only
after this report is updated so the report itself is in the path set. Its
result and the independent read-only re-review are reported in the final
handoff.

## Proof-test review fix: complete recursive graph traversal

The independent re-review found that the recursive no-Config proof had two
blind spots:

- the `IDictionary` branch pushed visible entries and then returned early,
  without inspecting instance fields;
- one `GetFields` call on the runtime type did not include private fields
  declared by base types.

No production defect or production-code change was required. A malicious test
dictionary now exposes only ordinary entries while a private field on its base
class retains an `AppConfig`. A separate self-referencing dictionary locks the
cycle guard.

RED command:

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter \
  FullyQualifiedName~PluginExecutionSessionTests.ConfigGraphCheck_DetectsConfigInDictionaryBasePrivateField \
  --no-restore --nologo --verbosity quiet
```

Result: expected exit 1; the old helper returned without detecting the hidden
configuration, so the test's required exception was absent.

The helper now:

- scans dictionary entries and then continues to instance fields;
- uses `BindingFlags.DeclaredOnly` while walking every type in the inheritance
  chain;
- retains reference-identity `visited` tracking before expanding collections
  or fields, so cyclic graphs terminate.

GREEN command:

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter FullyQualifiedName~PluginExecutionSessionTests \
  --no-restore --nologo --verbosity quiet
```

Result: exit 0; 7 passed, 0 failed, 0 skipped.

The one allowed proof-test `post-change-focused.sh` run is executed after this
report update against only this report and
`PluginExecutionSessionTests.cs`. Its result is reported in the handoff.
