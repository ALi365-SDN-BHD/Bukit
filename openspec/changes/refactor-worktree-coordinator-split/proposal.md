# Split the multilingual Worktree build coordinator

## Why

Merging `codex/i18n-worktree-isolation` into `main` left
`src/Bukit-Core/Bukit.Cli/Commands/WorktreeBuildCoordinator.cs` at 957 lines, above
the repository's 600-line ceiling. The file mixed three unrelated responsibilities:
build orchestration, Git repository/Worktree plumbing, and staging-path/transaction
handling. Every new Worktree feature had to be added to an already oversized file.

## Changes

- Keep orchestration in `WorktreeBuildCoordinator.cs`: worker entry point, the
  multilingual build pipeline, worker process launch, self-invocation, `WorkerPlan`.
- Extract Git plumbing into `WorktreeBuildRepository.cs`: repository validation,
  tracked/ignored input resolution, symlink escape checks, detached Worktree
  add/remove/list, stale-run recovery, and the `git` process helpers.
- Extract staging, manifest and path handling into `WorktreeBuildPaths.cs`:
  staging-tree preparation, variant manifest seeding, manifest output-root rewrite,
  path containment helpers, metrics path resolution, the per-checkout build lock,
  permission hardening, and best-effort directory deletion.
- Extract the output transaction into `WorktreeBuildTransaction.cs`: two-phase
  directory/file install, ordered rollback, and backup cleanup.
- Move `WorktreeBuildException` next to the coordinator; it stays in
  `Bukit.Cli.Commands`.
- Update the four internal references in `WorktreeBuildCoordinatorTests` to the new
  owning types.

## Compatibility and boundaries

Pure extraction: no behavior, message, exception code, file layout, or public
contract changes. All moved members keep their accessibility (`internal`) and
signatures. No new project references, packages, gates, or CI changes. No
`partial` split was used, because the extracted groups are independent
responsibilities rather than fragments of one type.

Verification is limited to the affected specialties: `bukit-core.slnx` and
`bukit-test.slnx` build with `-warnaserror`, `WorktreeBuildCoordinatorTests` and
`I18nWorktreeProtocolTests` pass, and `dotnet format --verify-no-changes` reports no
changes for the touched files.
