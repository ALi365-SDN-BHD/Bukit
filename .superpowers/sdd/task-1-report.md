# AD-01B1 Report: Plugin execution policy extraction

## Scope completed

- Added an Engine-owned internal `PluginExecutionPolicy` next to `PluginRunner`.
- Normalized only:
  - warn-versus-strict plugin failure handling;
  - derive conflict policy using the existing null/default, trim, and lowercase semantics;
  - plugin enablement into a case-insensitive name-to-enabled lookup.
- Preserved every existing public `PluginRunner` signature.
- Existing entry points continue to derive the policy from `context.Config.Site`
  and delegate to internal policy-aware overloads.
- Kept `BuildContext.Config` and the existing Config references in place.
- Did not migrate built-in plugin configuration or modify Labs, official/external
  plugins, schema, YAML, protocols, assets, output ownership, security, or gates.

## RED evidence

Command:

```sh
env -u NOTION_TOKEN dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter FullyQualifiedName~PluginExecutionPolicyTests \
  --no-restore --nologo
```

Result: expected RED, exit 1. The new focused test file failed to compile with
five `CS0103` errors because `PluginExecutionPolicy` did not exist yet. No
production file had been edited at that point.

## GREEN commands and results

New policy tests:

```sh
env -u NOTION_TOKEN dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter FullyQualifiedName~PluginExecutionPolicyTests \
  --no-restore --nologo
```

Result: exit 0; 16 passed, 0 failed, 0 skipped.

Focused policy and PluginRunner regression tests:

```sh
env -u NOTION_TOKEN dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  --filter 'FullyQualifiedName~PluginExecutionPolicyTests|FullyQualifiedName~PluginRunnerTests' \
  --no-restore --nologo
```

Result: exit 0; 31 passed, 0 failed, 0 skipped.

Required focused post-change check:

```sh
env -u NOTION_TOKEN bash scripts/checks/post-change-focused.sh -- \
  src/Bukit-Core/Bukit.Engine/Plugins/PluginExecutionPolicy.cs \
  src/Bukit-Core/Bukit.Engine/Plugins/PluginRunner.cs \
  tests/Bukit.Engine.Tests/PluginExecutionPolicyTests.cs
```

Result: exit 0. Diff/untracked whitespace checks passed and the Release owner
check ran `Bukit.Engine.Tests`: 1613 passed, 0 failed, 0 skipped.

## Changed files

- `src/Bukit-Core/Bukit.Engine/Plugins/PluginExecutionPolicy.cs`
  - Adds the internal normalized execution policy.
- `src/Bukit-Core/Bukit.Engine/Plugins/PluginRunner.cs`
  - Derives the policy at existing entry points and routes execution through
    internal policy-aware overloads.
- `tests/Bukit.Engine.Tests/PluginExecutionPolicyTests.cs`
  - Covers strict/fail/all-enabled defaults, warn mapping, derive normalization,
    case-insensitive enabled/disabled lookup, and unknown/blank/null names.
- `.superpowers/sdd/task-1-report.md`
  - Records implementation and verification evidence.

## Self-review

- `PluginRunner` no longer reads `PluginFailMode`, `DeriveConflictPolicy`, or
  `Plugins` directly.
- The policy stores no `AppConfig`, options objects, global/sidecar state, or
  unnormalized configuration graph.
- Plugin ordering remains `Order -> Name -> Version`; the ordering code was not
  changed.
- Sync/async hook selection, conflict application, execution records, logging,
  and failure rethrow/warn behavior remain in their existing control flow.
- Unknown and blank plugin names remain enabled. Configured lookup is now
  explicitly case-insensitive even when the supplied dictionary comparer is not.
- No B2/B3, Labs, plugin project, backup/reference, CI, release, gate, schema, or
  protocol files were modified.
- No aggregate targeted, `ci-fast`, full, release, `test-all`, or `smoke-all`
  command was run.

## Commit

- Intended message: `refactor(engine): extract plugin execution policy`
- Base commit before AD-01B1: `b14edc7d`
- The final commit hash is reported in the AD-01B1 handoff. It cannot be embedded
  literally in a file contained by that same commit because changing the
  embedded hash changes the commit hash.

## Concerns

- None.
