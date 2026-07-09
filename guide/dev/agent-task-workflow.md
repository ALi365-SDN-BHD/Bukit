# Agent Task Workflow

Agents working in this repo must keep one active task at a time.

## Sequence

1. Define the task scope.
2. Gather source evidence.
3. Implement only that task.
4. Run task-appropriate checks.
5. Audit the final diff.
6. Move on only when no unresolved issue remains.

## Boundaries

- Do not modify `guide-0.1/` or `scripts-0.1/` unless explicitly requested.
- Do not treat Labs documents as Core contracts.
- Do not widen docs or scripts tasks into runtime behavior changes.
- Use sub-agents only for bounded support on the same task.
