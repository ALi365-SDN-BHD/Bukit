# Post-change Targeted Gate P2 Fixes

## Scope

Fix all four P2 findings from the strict audit of the post-change targeted
gate:

1. Map `src/Bukit-Plugins/Bukit.Plugin.Echo/*` to its existing targeted
   runtime test consumers.
2. Replace the default-discovery self-test's catch-all skip with a
   deterministic test that cannot hide production failures.
3. Make untracked-file whitespace verification propagate path and Git errors.
4. Run the allowed thin checks before failing for an unmapped runtime source.

The previously fixed P1 behavior must remain intact. The final reviewer's
non-blocking test-uniqueness suggestion, unrelated coverage work, CI workflow
changes, backup directories, and broad gates are outside this fix.

## Architecture

The current `post-change-targeted.sh` is at the repository's 200-line script
limit. Move two responsibilities into focused executable helpers instead of
compressing more behavior into the coordinator:

- `scripts/checks/post-change-targeted-paths.sh BASE` prints changed tracked
  and untracked paths for the current repository.
- `scripts/checks/post-change-targeted-projects.sh PATH` prints zero, one, or
  multiple targeted test project paths for one changed source or test path.

`post-change-targeted.sh` remains the coordinator. It parses arguments,
collects and de-duplicates paths and projects, rejects forbidden gate paths,
runs the thin checks, reports unmapped runtime sources, and finally runs mapped
test projects.

## Project Mapping

The project helper preserves the current mappings and adds one multi-project
mapping:

- `Bukit.Cli.Shared` -> `Bukit.Cli.Tests`.
- `Bukit.Plugin.WechatSync` and `Bukit.WechatSyncing` ->
  `Bukit.Plugin.WechatSync.Tests`.
- `Bukit.Plugin.Echo` -> `Bukit.Cli.Tests` and `Bukit.PluginHost.Tests`.
- `tests/PluginProcessProbe/*` -> `Bukit.PluginHost.Tests`.
- `tests/ThrowingPlugin/*` -> `Bukit.Engine.Tests`.
- `tests/*.Tests/*` -> that test project.
- Other Core, Plugins, and Labs modules keep the existing naming convention.

`Bukit.Architecture.Tests` is not added to the Echo mapping. The audited P2 is
the missing targeted runtime coverage, while Architecture is a broader
repository-governance suite and is not part of the default post-change runtime
mapping.

The coordinator accepts multiple lines from the helper and de-duplicates them
through its existing `add_test_project` behavior.

## Deterministic Path Discovery

When no explicit paths are passed, the coordinator invokes
`post-change-targeted-paths.sh` and fails if that helper fails. The helper owns
the two Git commands currently embedded in the coordinator:

```bash
git diff --name-only "$base_ref" --
git ls-files --others --exclude-standard
```

The self-test creates its existing untracked scratch file, calls the path
helper directly, and requires the scratch path in the output. There is no
catch-all `else` branch and no "skipped for current dirty tree" success path.
Ambient unrelated changes may add output but cannot make a production failure
look like a passing test.

## Whitespace Error Semantics

`git diff --check --no-index` uses different channels and non-obvious exit
codes:

- A clean new file returns 1 with empty stdout and stderr.
- A trailing-whitespace file returns diagnostics on stdout and a non-zero
  status.
- A missing path returns diagnostics on stderr and may also return 1.

`untracked-whitespace.sh` therefore captures stdout and stderr separately:

- Non-empty stderr is a hard error and is printed to stderr.
- Non-empty stdout is a whitespace failure and is printed to stderr.
- Empty stdout and stderr is success when Git returns 0 or 1.
- Any other exit status without diagnostics is propagated as a hard error.

The checker continues to accept exactly one path and rejects invalid argument
counts with exit 2.

## Verification Order

The coordinator keeps blocked full or release gate paths as an immediate hard
failure. For an unmapped runtime source, it performs this sequence:

1. `git diff --check` for the selected paths.
2. Focused untracked-file whitespace checks.
3. `bash -n` for changed shell scripts.
4. `bash scripts/gates/ci-fast.sh <configuration>`.
5. Report all unmapped runtime source paths and exit non-zero.

Mapped `dotnet test` commands run only when there are no unmapped runtime
sources. There is no fallback to a full gate or whole solution.

In `--dry-run` mode, an unmapped runtime source prints the four allowed thin
commands, emits the mapping error, exits non-zero, and prints no `dotnet test`
command.

## Test Strategy

Follow separate red-green cycles for each behavior:

1. Echo dry-run must select `Bukit.Cli.Tests` and `Bukit.PluginHost.Tests`.
2. Existing Core, test-helper, and ordinary `tests/*.Tests/*` mappings must
   remain green.
3. The path helper must report the self-test scratch file and propagate an
   invalid Git base failure.
4. The whitespace checker must pass a clean file, reject trailing whitespace,
   reject a missing path, and reject an invalid argument count.
5. An unmapped source dry-run must print `git diff --check` and `ci-fast`, then
   fail with the mapping error without printing `dotnet test`.
6. `bash -n`, the post-change self-test, the explicit targeted gate, and a
   bounded read-only sub-agent audit must pass.

## Constraints

- Keep every active shell script at or below 200 lines.
- Do not run `ci-full`, release, `test-all`, `smoke-all`, or whole-solution
  `.slnx` tests.
- Do not modify `guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, or `scripts-0.2/`.
- Do not change CI workflow behavior.
- Preserve unrelated staged and working-tree changes.
