# Implementation checklist

- [x] Confirm the ceiling breach: `WorktreeBuildCoordinator.cs` at 957 lines after
  the `codex/i18n-worktree-isolation` merge.
- [x] Identify three cohesive groups (orchestration, Git/Worktree plumbing,
  staging/paths/transaction) and record the target layout.
- [x] Extract `WorktreeBuildTransaction` (atomic commit, rollback, backup cleanup).
- [x] Extract `WorktreeBuildPaths` (staging, manifest roots, containment, metrics,
  lock, permissions, best-effort deletion).
- [x] Extract `WorktreeBuildRepository` (validation, input collection, Worktree
  lifecycle, stale-run recovery, `git` helpers).
- [x] Reduce `WorktreeBuildCoordinator` to orchestration and re-point call sites.
- [x] Repoint the four internal test references; change no assertions.
- [x] Verify all four files are at or below 600 lines (395 / 285 / 205 / 97).
- [x] Build `bukit-core.slnx` and `bukit-test.slnx` with `-c Release -warnaserror`:
  0 errors, 0 warnings.
- [x] Run the affected specialties: `WorktreeBuildCoordinatorTests` 22 passed /
  0 failed; `I18nWorktreeProtocolTests` 5 passed / 0 failed.
- [x] `dotnet format bukit-core.slnx --verify-no-changes --include <four files>`:
  no changes.

## Not included

No behavior change, no new public API, no new dependency, no gate/CI change, no
commit or push, and no full/release/solution-wide gate.
