# Repository Red Lines

## Scope and precedence

- This file applies repository-wide. Nested `AGENTS.md` files apply only below their directory and may tighten, but never weaken, these red lines.
- Higher-priority platform instructions and explicit user instructions take precedence.

## Protected reference areas

- `guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, and `scripts-0.2/` are backup/reference-only and must not drive official docs, runtime, CI, release, or gates.
- Do not modify or recreate those areas unless the user explicitly requests historical-snapshot maintenance; port useful material into `guide/` or `scripts/` instead.

## Website/Core isolation

- During a named website business task, do not modify Core-owned surfaces: `src/Bukit-Core/`, Core-defining tests or fixtures, public contracts, or CI/release/gate logic that changes Core behavior.
- Report Core defects with evidence and a proposed repair. Implement them only after explicit confirmation in a separate user-visible Core task.

## Verification boundaries

- Do not run full/release gates, `scripts/test-all.sh`, `scripts/smoke-all.sh`, or whole-solution tests without explicit user authorization.
- After each code subtask, run only focused affected checks with `bash scripts/checks/post-change-focused.sh -- <changed paths>`; do not repeat aggregate targeted or `ci-fast` per subtask.
- At parent-task completion, run `bash scripts/checks/post-change-targeted.sh --base <parent-base> -- <all changed paths>` exactly once for the aggregate diff.
- Changes to CI, release, gate, or verification files require their direct owner test/self-test. A full/release owner gate still requires explicit user authorization.

## Failure boundary

- Environment, permission, tool, or infrastructure failures do not authorize unrelated code changes.
- If required proof remains unavailable, report the exact blocker and do not claim the gate passed.
