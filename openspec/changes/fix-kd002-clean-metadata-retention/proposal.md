# Preserve project metadata during CLI clean (KD-002)

User authorization (2026-10-28): independently repair KD-002 only, preserving all existing uncommitted work. KD-003/004, SCSS, shared Engine behavior, dependencies, CI/release gates and commits are excluded.

## Decision

Project-root `.bukit` mixes user-owned plugin configuration/locks with generated metadata; it is not a disposable cache. Stop deleting that directory during ordinary clean. Reject it and its descendants as explicit clean output targets before deletion; retain the existing shared output safety checks and normal output/`.cache` cleanup. Do not introduce selective metadata cleanup, new options or a cleanup framework.

## Execution and evidence

Use the existing worktree and one writer. First activate and strengthen the KD-002 regression across default, `--dir`, `--config` and `--site` selection, assert byte-preserved plugin/lock/unknown files, and test real CLI dispatch and protected explicit targets. Observe RED before production changes, then make the minimum shared CleanCommand repair and observe GREEN. Run the complete CleanCommandTests, ProgramEntryPointTests and CliContractTests only, plus `git diff --check`; retain existing safety regressions. Perform one specialty review and one delta-only final review, then backfill this task and the known-defect record.

Affected closure, classification, exact commands, baseline copies and durable evidence root: `/Users/ali/.local/state/bukit-verification/kd002-clean-preserve-s2CkH1`. `tasks.md` is the implementation checklist. Existing `Directory.Build.props` changes are user work and must remain byte-identical. No automatic commit, push, merge, whole-solution or release gate.
