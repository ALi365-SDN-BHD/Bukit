# Post-change Targeted Gate P1 Fixes

## Scope

Fix only the two P1 findings from the strict audit of the post-change targeted
gate:

1. Changes under `tests/PluginProcessProbe/` and `tests/ThrowingPlugin/` must
   run the test projects that directly consume those helper projects.
2. The post-change self-test must not be able to recurse through
   `ci-fast -> self-test -> post-change-targeted -> ci-fast`.

The existing P2 findings, full gates, release gates, CI workflows, backup
directories, and unrelated staged changes are outside this fix.

## Design

### Helper-project test mapping

Extend the existing explicit path mapping in
`scripts/checks/post-change-targeted.sh`:

- `tests/PluginProcessProbe/*` maps to
  `tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj`.
- `tests/ThrowingPlugin/*` maps to
  `tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj`.

Keep the existing `tests/*.Tests/*` convention and project de-duplication.
Explicit mappings are preferred here because the two dependency edges are
stable repository contracts and parsing MSBuild XML from Bash would add more
failure modes than it removes.

### Non-recursive whitespace verification

Move untracked-file whitespace verification into a focused script under
`scripts/checks/`. The targeted gate calls this script for each untracked
path, while the self-test calls the focused script directly.

This removes the self-test's only non-dry-run call to
`post-change-targeted.sh`. The normal `ci-fast` integration remains in place,
but the self-test can no longer reach `ci-fast` recursively.

## Error Handling

- The focused whitespace check returns success when no whitespace violation is
  reported and failure when `git diff --check --no-index` reports one.
- Missing helper mappings remain a hard failure; there is no fallback to a
  whole solution or full gate.
- No environment variable or public skip flag is introduced to bypass
  `ci-fast`.

## Test Strategy

Follow a red-green sequence in
`scripts/checks/post-change-targeted-self-test.sh`:

1. Add dry-run assertions for both helper-project mappings and confirm they
   fail before the production mapping is changed.
2. Change the trailing-whitespace case to call the focused checker and confirm
   it fails while that checker is absent.
3. Implement the checker and mappings, then rerun the self-test.
4. Run `bash -n` for all changed shell scripts.
5. Run the repository's targeted post-change gate with explicit changed paths.
6. Perform a bounded read-only sub-agent review and a final main-thread diff
   audit.

No `ci-full`, release, `test-all`, `smoke-all`, whole-solution `.slnx`, or
other full gate is permitted.

## Success Criteria

- Dry-run output for `tests/PluginProcessProbe/Program.cs` includes only the
  expected `Bukit.PluginHost.Tests` targeted project.
- Dry-run output for a file under `tests/ThrowingPlugin/` includes only the
  expected `Bukit.Engine.Tests` targeted project.
- The self-test contains no actual invocation of `post-change-targeted.sh`;
  its invocations of that script are dry-run only.
- The focused whitespace check rejects an untracked file with trailing
  whitespace.
- All scoped thin verification passes without invoking a forbidden broad gate.
