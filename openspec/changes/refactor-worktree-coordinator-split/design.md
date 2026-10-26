# Design: Worktree build coordinator split

## Resulting layout

All files stay in `src/Bukit-Core/Bukit.Cli/Commands/` with namespace
`Bukit.Cli.Commands`, matching the directory rule.

| File | Lines | Responsibility |
| --- | --- | --- |
| `WorktreeBuildCoordinator.cs` | 395 | Worker entry (`RunWorkerAsync`), build pipeline (`RunAsync`), worker process launch, self-invocation, `WorkerPlan`, `WorktreeBuildException` |
| `WorktreeBuildRepository.cs` | 285 | `GitRepository`, repository/input validation, Worktree lifecycle, stale-run recovery, `git` execution |
| `WorktreeBuildPaths.cs` | 205 | Staging trees, manifest roots, path containment, metrics paths, build lock, permissions, `MetricsPaths` |
| `WorktreeBuildTransaction.cs` | 97 | Atomic commit, rollback, backup cleanup |

## Dependency direction

`WorktreeBuildCoordinator` → `WorktreeBuildRepository` / `WorktreeBuildPaths` /
`WorktreeBuildTransaction`. The extracted types never reference the coordinator, so
the graph stays acyclic and each type can be reasoned about alone. Path helpers are
shared by repository and coordinator, hence `WorktreeBuildPaths` is the lowest layer.

## Alternatives considered

- **`partial` class files.** Would have kept test call sites unchanged, but a
  `partial` split hides three responsibilities behind one type name and does not
  satisfy the single-responsibility rule. Rejected.
- **New `I*` interface plus DI.** The coordinator is a static entry point invoked
  from `BuildCommand`; the extracted members are pure filesystem/Git helpers with no
  state and no substitution need in this change. Introducing interfaces here would
  add indirection without a second implementation. Rejected as over-engineering.
- **Leaving the file and only moving transaction helpers.** Would land at ~860
  lines, still above the ceiling. Rejected.

## Preserved semantics

- Method bodies were moved verbatim; only the declaring type changed.
- Call sites now name the owning type explicitly (`WorktreeBuildRepository.`,
  `WorktreeBuildPaths.`, `WorktreeBuildTransaction.`), so the control flow and
  execution order of the build pipeline are unchanged.
- `WorktreeBuildException` codes and messages are untouched, so diagnostics stay
  byte-identical.
- Test references for `PrepareStaging`, `AcquireRunLock`, `CommitDirectories` and
  `CleanupBackup` were repointed; assertions were not modified.
