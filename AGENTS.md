# Repository Working Rules

## Scope

This file defines non-code operating rules for future changes in this repository.

### 1) Backup-only areas (do not modify by default)

- `guide-0.1/` and `guide-0.2/` are backup/reference documentation only.
- `scripts-0.1/` and `scripts-0.2/` are backup/reference scripts only.
- Backup/reference directories must not be used as official documentation,
  active quality gates, CI script sources, release script sources, or runtime
  behavior references.

Default behavior:
- Do not make fixes, updates, or refactors inside these directories unless explicitly requested.
- If a task touches quality gates, CI, release, official documentation, or
  runtime behavior, use mainline paths and avoid `guide-0.1/`, `scripts-0.1/`,
  `guide-0.2/`, and `scripts-0.2/` as targets or executable sources.
- If useful material exists only in a backup/reference directory, port it into
  `guide/` or `scripts/` and verify it there instead of linking to or executing
  the backup copy.

### 2) Mainline change preference

- Files under `guide/` and `scripts/` are the official documentation and script
  surfaces for repository work.
- Keep backup directories aligned for historical consistency only, with no active rule or behavior changes.

### 3) Agent task execution discipline

- Sub agents are allowed, but only as support for the current single task.
- Do not advance to a second task until the current task is fully implemented, verified, and audited.
- If a task is split into subtasks, treat them as one active task until the full verification chain is complete.

Required execution order for each task:

1. Define the current task scope and keep execution limited to that scope.
2. Use sub agents only for bounded support work such as read-only exploration, evidence collection, or isolated implementation assistance for the same task.
3. Complete the implementation for the current task.
4. Run task-appropriate tests.
5. Perform a code audit of the final diff and impacted surfaces.
6. Move to the next task only after the audit finds no unresolved issues.

Validation boundary:

- Development tasks require a repository gate by default.
- Rule-definition and rule-modification tasks do not require a repository gate.
- Use the task-appropriate gate for normal development work. If the task
  directly changes CI, release, verification scripts, or another gate-owned
  surface, use the gate that owns that surface.

Failure rule:

- If tests, a required gate, or code audit fail, stop task progression, fix
  the current task, and rerun the same verification chain before starting any
  new task.
