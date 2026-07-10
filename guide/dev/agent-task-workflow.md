# Agent Task Workflow

Agents working in this repo must keep one active task at a time.

## Sequence

1. Define the task scope.
2. Gather source evidence.
3. Implement only that task.
4. For code changes, run a bounded read-only sub-agent review of the current
   diff when sub-agents are available.
5. Run task-appropriate targeted checks.
6. Audit the final diff.
7. Move on only when no unresolved issue remains.

## Boundaries

- Do not modify `guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, or
  `scripts-0.2/` unless explicitly requested.
- Do not treat Labs documents as Core contracts.
- Do not widen docs or scripts tasks into runtime behavior changes.
- Use sub-agents only for bounded support on the same task.
- Sub-agents may audit, collect evidence, and recommend targeted checks; they
  must not edit files, commit, start new user-visible sessions, or expand scope.
- Use `bash scripts/checks/post-change-targeted.sh -- <changed paths>` for the
  default post-change gate. Pass explicit paths when unrelated working-tree
  changes exist.
- Do not run `scripts/gates/ci-full.sh`, `scripts/gates/release.sh`,
  `scripts/test-all.sh`, `scripts/smoke-all.sh`,
  `dotnet test bukit-test.slnx`, or whole-solution `.slnx` tests from the
  default post-change flow unless explicitly requested.
