# Agent Task Workflow

Agents working in this repo must keep one active parent task at a time. A parent
task may contain multiple small, sequential subtasks.

## Sequence

1. Define the task scope.
2. Gather source evidence.
3. Implement one small subtask.
4. Run task-appropriate targeted checks for that subtask.
5. Continue with the next subtask only after the current subtask's gate passes.
6. At the end of a large parent task, run one bounded read-only review that
   audits every subtask and the aggregate parent-task diff.
7. Resolve review findings, rerun affected targeted checks, and repeat only the
   necessary audit scope.
8. Move to another parent task only when no required gate or audit issue
   remains.

## Boundaries

- Do not modify `guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, or
  `scripts-0.2/` unless explicitly requested.
- Do not treat Labs documents as Core contracts.
- Do not widen docs or scripts tasks into runtime behavior changes.
- Ordinary small subtasks use targeted gates without a sub-agent audit by
  default. A standalone ordinary small task also does not require one unless
  explicitly requested.
- Use one consolidated sub-agent audit after a large parent task's subtasks
  pass their targeted gates. The audit must check each subtask against its
  scope and evidence, then check the aggregate diff for interactions,
  omissions, and unrelated changes.
- Audit a subtask immediately when it changes security or authorization,
  concurrency or consistency, persistence formats or migrations, public APIs
  or plugin/config contracts, CI/release/gate logic, or when targeted checks
  cannot cover its key behavior.
- Use sub-agents only for bounded support on the same parent task.
- Sub-agents may audit, collect evidence, and recommend targeted checks; they
  must not edit files, commit, start new user-visible sessions, or expand scope.
- Use `bash scripts/checks/post-change-targeted.sh -- <changed paths>` for the
  gate after each code subtask. Pass that subtask's explicit paths when
  unrelated working-tree changes exist.
- Do not run `scripts/gates/ci-full.sh`, `scripts/gates/release.sh`,
  `scripts/test-all.sh`, `scripts/smoke-all.sh`,
  `dotnet test bukit-test.slnx`, or whole-solution `.slnx` tests from the
  default post-change flow unless explicitly requested.
