# AD-01B2 Report: Built-in configuration binding

## Scope completed

- Made `BuiltInPluginSource` accept the effective `AppConfig` reference and
  construct the locked ten-plugin set in its existing order:
  Analytics, DataFiles, PagesIndex, Taxonomy, Pagination, Archive,
  RelatedContent, Alias, Menu, ImageProcessing.
- Added an internal `PluginRegistry.GetAllPlugins(BuildContext, AppConfig)`
  path. The existing public context-only facade remains as the B2 compatibility
  bridge.
- Made the existing context-local registry cache sensitive to `AppConfig`
  reference identity:
  - same context and same reference reuses instances;
  - same context and another reference rebuilds and replaces the cache;
  - comparison uses `ReferenceEquals`;
  - cache key, context-data lock, duplicate filtering, order, source label, and
    build-count behavior remain unchanged.
- Bound all registry-owned built-ins to the supplied effective configuration.
  Their hook methods no longer read `BuildContext.Config`.
- Bound the aggregate-only Feed, LlmsTxt, SearchIndex, and Sitemap adapters to
  explicit configuration without adding them to `BuiltInPluginSource`.
  Aggregate publish projection ownership remains unchanged.
- Added explicit configuration paths through `PluginRunner`, production variant
  stages, `PluginPipeline`, CLI Doctor discovery/template analysis,
  `AnalyticsBuildState`, `TaxonomyTermsInjector`, and the internal SiteEngine
  list-route helper.
- Preserved `BuildContext.Config`, the Engine.Abstractions-to-Config project
  reference, all public PluginRegistry/PluginRunner/plugin-interface signatures,
  and the public `SiteEngine.GetListRoutes(BuildContext, ...)` overload for B3.
- Did not modify Labs, official/external plugin source, schemas, plugin
  protocols, asset URLs, output ownership, path/security helpers, gates, or
  backup/reference directories.

## RED evidence

Command:

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter FullyQualifiedName~PluginRegistryTests \
  --no-restore --nologo
```

Expected RED: exit 1. The new registry tests failed compilation with `CS1729`
and `CS1501` because `BuiltInPluginSource(AppConfig)` and
`PluginRegistry.GetAllPlugins(BuildContext, AppConfig)` did not exist. No B2
production file had been edited at that point.

## GREEN evidence

Registry cache, identity, order, version, and source tests:

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter FullyQualifiedName~PluginRegistryTests \
  --no-restore --nologo
```

Result: exit 0; 13 passed, 0 failed, 0 skipped.

Representative Engine behavior regression:

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter '<PluginRegistry/Runner, Analytics, Taxonomy, Pagination, Archive,
  Feed/Search/Sitemap/Llms, media, menu/related/pages, SiteEngine and variant
  test filters>' --no-restore --nologo
```

Result: exit 0; 206 passed, 0 failed, 0 skipped.

Explicit effective-config mismatch regressions:

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter 'FullyQualifiedName~BuildListRoutes_WithExplicitConfig_UsesEffectiveConfigInsteadOfContextBridge|FullyQualifiedName~GetOrCreate_UsesExplicitEffectiveConfigInsteadOfContextBridge' \
  --no-restore --nologo
```

Result: exit 0; 2 passed, 0 failed, 0 skipped.

Focused CLI Doctor/template analysis:

```sh
env -u NOTION_TOKEN dotnet test \
  tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj \
  --filter 'FullyQualifiedName~DoctorCommandTests|FullyQualifiedName~DoctorTemplateAnalyzerTests' \
  --no-restore --nologo
```

Result: exit 0; 25 passed, 0 failed, 0 skipped.

Required focused post-change gate:

```sh
changed_paths=("${(@f)$(git diff --name-only HEAD)}")
env -u NOTION_TOKEN bash scripts/checks/post-change-focused.sh -- "${changed_paths[@]}"
```

Result: exit 0. Diff whitespace passed; Release owner checks passed:

- `Bukit.Cli.Tests`: 618 passed, 0 failed, 0 skipped.
- `Bukit.Engine.Tests`: 1618 passed, 0 failed, 0 skipped.

The first shell attempt used Bash-only `mapfile` under zsh, so path collection
failed and the script reported `No changed paths detected.` That no-op was not
treated as proof. The zsh-compatible replacement command above is the required
successful focused gate.

## Changed paths

Production changes are limited to:

- `src/Bukit-Core/Bukit.Engine/Plugins/PluginRegistry.cs`
- `src/Bukit-Core/Bukit.Engine/Plugins/PluginRunner.cs`
- the ten registry-owned built-ins and four aggregate-only adapter files under
  `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/`
- taxonomy binding helpers under the same directory
- `AnalyticsBuildState`, `TaxonomyTermsInjector`, `SeoAlternatesService`,
  `SiteEngine`, `PluginPipeline`, and affected variant stages
- CLI Doctor discovery/template-analysis callers

Test changes are limited to `tests/Bukit.Engine.Tests/` and mechanically pass
the context's effective configuration into internal built-in constructors,
plus the new registry, Analytics, and SiteEngine regressions.

## Self-review

- Static scan finds no `context.Config` read in any built-in plugin,
  `AnalyticsBuildState`, or `TaxonomyTermsInjector`.
- Remaining `BuildContext.Config` reads in the touched configuration path are
  confined to the deliberately retained PluginRegistry, PluginRunner, and
  public SiteEngine compatibility facades. Removing them belongs to B3.
- `SiteEngine.BuildCoreAsync` reads `BuildPipelineContext.Config`, not
  `BuildContext.Config`; it is unrelated to the dependency being removed.
- `BuiltInPluginSource` still registers exactly ten plugins with unchanged
  names, versions, order, and `built-in` source.
- Feed, LlmsTxt, SearchIndex, and Sitemap remain absent from the registry and
  remain aggregate-projection owned.
- No AppConfig mirror, context-data configuration payload, ambient/global
  holder, conditional weak table, or other hidden configuration channel was
  introduced.
- Existing output generation algorithms and static aggregate writers were not
  moved or rewritten; only their configuration source changed.
- `git diff --check` passed.
- No aggregate targeted, `ci-fast`, full, release, `test-all`, or `smoke-all`
  command was run.

## Commit

- Intended message: `refactor(engine): bind plugin configuration explicitly`
- Base commit before AD-01B2: `5a7a30ad`
- The final commit hash is reported in the AD-01B2 handoff because a file inside
  that same commit cannot embed its own stable hash.

## Concerns

- The B2 compatibility facades still read `BuildContext.Config` by design.
  AD-01B3 must remove those bridges only after all Core callers use the explicit
  paths and must then remove the Abstractions project reference.
- Native AOT was not run; it is outside this subtask's authorized verification
  boundary.
