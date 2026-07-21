# Agent Task Workflow

Agents working in this repo must keep one active parent task at a time. A parent
task may contain multiple small, sequential subtasks.

## Rule applicability and precedence

The root `AGENTS.md` applies repository-wide. A nested `AGENTS.md` applies only
to its directory and descendants and may supplement or tighten the root rules;
it does not silently weaken a root-level strict prohibition. Follow the most
specific compatible repository rule. If applicable rules conflict and cannot
all be satisfied, preserve the stricter non-destructive boundary and request
direction. Higher-priority platform instructions and explicit user instructions
take precedence over repository rules.

## Task declaration and classification

Before implementation, state:

- the parent-task objective and ordered code subtasks;
- each subtask's risk and targeted gate;
- whether any subtask needs an immediate high-risk review;
- whether the parent needs a final consolidated audit.

An ordinary small task has one bounded objective and one primary ownership
surface, introduces no cross-module contract change, is covered by a focused
targeted gate, and has not been designated large by the user.

Treat a parent task as large when it has multiple code subtasks, changes
contracts across multiple modules, combines multiple high-risk surfaces, makes
broad mechanical changes across projects, or is explicitly designated large by
the user. Classification follows scope, ownership, and risk; a single commit or
single-subtask label does not make complex work small.

## Website-business/Core boundary

A website-business task delivers content, configuration, theme, or deployment
results for a named or specific downstream site. Classify it by the requested
deliverable, not by which repository or file is open. Generic Bukit capability
or repository-owned example work is not automatically a website-business task.

Website-business tasks must not implement Core-owned changes. This includes
Core source, tests or fixtures that define Core behavior, public
API/config/protocol baselines, Core contract documentation, and CI, release, or
gate logic that changes Core behavior. Use read-only inspection and disposable
reproduction under `/tmp` or the downstream workspace; do not add Core tests or
copy downstream fixtures into this repository.

Report a discovered Core defect with evidence, impact, likely ownership,
proposed repair, and targeted verification. Implement it only after explicit
confirmation in a separate user-visible task. Plugin, theme, or example work
within the active scope does not authorize a Core-owned change.

## Lifecycle exits

- The user may explicitly cancel, replace, pause, or request an interim handoff
  for the active parent task. Stop or redirect as requested without forcing the
  previous task to completion.
- Report the exact implementation, verification, and working-tree state at an
  interim handoff. Do not discard or commit unfinished work unless requested.
- If safe in-scope checks are exhausted and the task remains blocked, report the
  evidence and pause. A blocker does not authorize a different parent task
  without explicit user redirection.

## Sequence

1. Define the task scope.
2. Gather source evidence.
3. Implement one small subtask.
4. Run task-appropriate targeted checks for that subtask.
5. If a targeted check fails, stop and classify the failure before changing
   code. Continue only after a scoped regression is fixed and verified or an
   environment/infrastructure failure is successfully rerun.
6. At the end of a large parent task, run one bounded read-only review that
   audits every subtask and the aggregate parent-task diff.
7. Resolve review findings, rerun affected targeted checks, and repeat only the
   necessary audit scope.
8. Move to another parent task only when no required gate or audit issue
   remains.

## Failure handling

- Classify failures as scoped regressions, pre-existing failures, environment
  restrictions, or infrastructure noise before deciding what to modify.
- Modify the current task only when evidence connects the failure to its diff.
- Rerun environment-sensitive checks in an appropriate permitted environment
  when safe. If verification remains blocked, report the exact command,
  failure, and limitation instead of changing unrelated code.
- Report unrelated defects separately; do not absorb them into the active
  parent task merely to make a gate pass.

## Rule-change verification

Rule-definition and rule-modification tasks do not require runtime, full, or
release gates. They still require the minimum governance verification:

```bash
git diff --check -- <changed-governance-paths>
bash scripts/checks/docs-consistency.sh
```

When `guide/skills/` or a nested `AGENTS.md` changes, also run:

```bash
bash scripts/checks/skills-schema.sh
bash guide/skills/scripts/validate-skills-strict.sh
```

Keep the root `AGENTS.md`, this workflow, `guide/dev/testing.md`, and applicable
nested `AGENTS.md` rules semantically aligned. The docs-consistency gate checks
this governance contract.

## Boundaries

- Do not create, synchronize, or modify `guide-0.1/`, `guide-0.2/`,
  `scripts-0.1/`, or `scripts-0.2/` by default; their absence is valid. Touch
  them only when the user explicitly requests historical-snapshot maintenance,
  and never connect them to an active documentation, gate, CI/release, or
  runtime surface.
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
- Implementation sub-agents may edit only explicitly assigned, non-overlapping
  paths. They must not edit the same files in parallel, commit, merge, start new
  user-visible sessions, or expand the parent-task scope.
- Review sub-agents are strictly read-only. They may audit, collect evidence,
  and recommend targeted checks, but must not edit files, commit, merge, start
  new user-visible sessions, or expand scope. The main agent owns the final
  diff, verification, and completion decision.
- Use `bash scripts/checks/post-change-targeted.sh -- <changed paths>` for the
  gate after each code subtask. Pass that subtask's explicit paths when
  unrelated working-tree changes exist.
- Run the gate before creating the subtask commit. If the subtask is already
  committed, pass `--base <subtask-base-sha>` together with its changed paths.
- For final parent-task verification, pass `--base <parent-task-base-sha>` and
  all paths changed by the parent task.
- Do not run `scripts/gates/ci-full.sh`, `scripts/gates/release.sh`,
  `scripts/test-all.sh`, `scripts/smoke-all.sh`,
  `dotnet test bukit-test.slnx`, or whole-solution `.slnx` tests from the
  default post-change flow unless explicitly requested.
