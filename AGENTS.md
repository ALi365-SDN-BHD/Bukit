# Repository Working Rules

## Scope

This file defines non-code operating rules for future changes in this repository.

### Applicability and precedence

- This root `AGENTS.md` applies to the entire repository.
- A nested `AGENTS.md` applies only to its directory and descendants. It may
  supplement or tighten these root rules, but it must not silently weaken a
  root-level strict prohibition.
- When multiple repository rules apply, follow the most specific compatible
  rule. If applicable rules cannot be satisfied together, preserve the stricter
  non-destructive boundary and request direction before proceeding.
- Higher-priority platform instructions and explicit user instructions take
  precedence over repository `AGENTS.md` rules when they conflict.

### 1) Backup-only areas (do not modify by default)

- If present, `guide-0.1/` and `guide-0.2/` are backup/reference documentation
  only, and `scripts-0.1/` and `scripts-0.2/` are backup/reference scripts only.
- Backup/reference directories must not be used as official documentation,
  active quality gates, CI script sources, release script sources, or runtime
  behavior references.

Default behavior:
- Do not create, synchronize, fix, update, or refactor these directories by
  default. Their absence is valid and does not require reconstruction.
- Create or modify a backup/reference directory only when the user explicitly
  requests maintenance of a historical snapshot.
- If a task touches quality gates, CI, release, official documentation, or
  runtime behavior, use mainline paths and avoid `guide-0.1/`, `scripts-0.1/`,
  `guide-0.2/`, and `scripts-0.2/` as targets or executable sources.
- If useful material exists only in a backup/reference directory, port it into
  `guide/` or `scripts/` and verify it there instead of linking to or executing
  the backup copy.

### 2) Mainline change preference

- Files under `guide/` and `scripts/` are the official documentation and script
  surfaces for repository work.
- Do not keep backup directories aligned with mainline by default. When explicit
  historical-snapshot maintenance is requested, keep the snapshot disconnected
  from official docs, active gates, CI/release scripts, and runtime behavior.

### 3) Agent task execution discipline

- Sub agents are allowed, but only as support for the current single task.
- Do not advance to a second parent task until the current parent task is fully
  implemented, verified, and, when required below, audited.
- The user may explicitly cancel, replace, pause, or request an interim handoff
  for the active parent task. Honor that direction without forcing the previous
  task to completion, and report its exact implementation and verification
  state without discarding or committing work unless requested.
- If the active task is blocked after safe in-scope checks are exhausted,
  report the blocker and pause. A blocker does not authorize a different parent
  task without explicit user redirection.
- If a parent task is split into small subtasks, treat them as one active task.
  Complete each subtask's targeted gate before starting the next subtask.

Task declaration and classification:

- Before implementation, state the parent-task objective, ordered code
  subtasks, each subtask's risk, its targeted gate, whether it requires an
  immediate high-risk review, and whether the parent requires a final
  consolidated audit.
- An ordinary small task has one bounded objective and one primary ownership
  surface, introduces no cross-module contract change, is covered by a focused
  targeted gate, and has not been designated large by the user.
- Treat a parent task as large when it contains multiple code subtasks, changes
  contracts across multiple modules, combines multiple high-risk surfaces,
  makes broad mechanical changes across projects, or is explicitly designated
  large by the user.
- Task size is determined by scope, ownership, and risk, not by the number of
  commits or by packaging complex work as one subtask.

Required execution order for each task:

1. Define the current task scope and keep execution limited to that scope.
2. Use sub agents only for bounded support work such as read-only exploration, evidence collection, or isolated implementation assistance for the same task.
3. Complete one small subtask at a time.
4. After each subtask, run task-appropriate targeted tests. Do not create a
   sub-agent review for an ordinary small subtask.
5. After all subtasks in a parent task are complete, perform the consolidated
   audit required below when the parent task qualifies for one.
6. Move to the next parent task only after required gates pass and any required
   audit has no unresolved issues.

Post-change verification and audit for code changes:

- After adding or modifying code logic in an ordinary small subtask, run the
  targeted gate but do not create a sub-agent review by default.
- When a parent task is classified as large, create one bounded read-only
  sub-agent review after all
  subtasks have passed their targeted gates. The review must audit each
  subtask against its scope and evidence, then audit the aggregate parent-task
  diff for cross-subtask regressions, omissions, and unrelated changes.
- A standalone ordinary small task does not require a sub-agent audit unless
  the user explicitly requests one.
- Perform an immediate bounded read-only review for a high-risk subtask when it
  changes security or authorization behavior, concurrency or consistency,
  persistence formats or migrations, public APIs or plugin/config contracts,
  CI/release/gate logic, or when targeted verification cannot cover the key
  behavior.
- An implementation sub-agent may modify only the explicitly assigned,
  non-overlapping paths for its bounded subtask. Implementation sub-agents must
  not modify the same files in parallel, create commits, merge changes, start a
  new user-visible session, or expand the parent-task scope.
- A review sub-agent is strictly read-only. It may audit the diff, collect
  evidence, and recommend targeted checks, but it must not modify files, create
  commits, merge changes, start a new user-visible session, or expand scope.
  The main agent remains responsible for the final diff, verification, and
  completion decisions.
- Use `bash scripts/checks/post-change-targeted.sh -- <changed paths>` as the
  default gate after each code subtask. When the working tree has unrelated
  changes, pass that subtask's paths explicitly instead of relying on automatic
  diff detection.
- Run the targeted gate before creating the subtask commit so its default
  `--base HEAD` checks the working-tree diff. If the subtask is already
  committed, run
  `bash scripts/checks/post-change-targeted.sh --base <subtask-base-sha> -- <changed paths>`.
- For final parent-task verification, use the parent task's starting SHA as
  `--base` and pass all parent-task changed paths so committed changes remain
  inside the verification diff.
- The default post-change flow must not run full or release gates. Do not run
  `scripts/gates/ci-full.sh`, `scripts/gates/release.sh`,
  `scripts/test-all.sh`, `scripts/smoke-all.sh`,
  `dotnet test bukit-test.slnx`, or whole-solution `.slnx` tests unless the
  user explicitly requests that broader proof.
- If a required consolidated or high-risk audit cannot use a sub-agent, state
  that limitation and perform the same scoped audit in the main thread.

Validation boundary:

- Development tasks require a task-appropriate repository gate by default.
- Rule-definition and rule-modification tasks do not require runtime, full, or
  release gates, but they must pass the minimum governance verification:
  - `git diff --check -- <changed governance paths>`;
  - `bash scripts/checks/docs-consistency.sh`;
  - when `guide/skills/` or a nested `AGENTS.md` changes,
    `bash scripts/checks/skills-schema.sh` and
    `bash guide/skills/scripts/validate-skills-strict.sh`.
- Keep `AGENTS.md`, `guide/dev/agent-task-workflow.md`,
  `guide/dev/testing.md`, and applicable nested `AGENTS.md` rules semantically
  aligned. The docs-consistency gate owns this governance contract.
- Use the task-appropriate gate for normal development work. If the task
  directly changes CI, release, verification scripts, or another gate-owned
  surface, use the gate that owns that surface.

Failure rule:

- If a test or required gate fails, stop before the next subtask and classify
  the failure as a scoped regression, a pre-existing failure, an environment
  restriction, or infrastructure noise. Modify the current task only when
  evidence connects the failure to its changes.
- For an environment or infrastructure failure, rerun the same check in an
  appropriate permitted environment when safe. If it still cannot be verified,
  report the exact blocker and evidence; do not modify unrelated modules merely
  to make the gate pass.
- If a required audit finds an issue in the current scope, fix the affected
  scope and rerun its targeted gate before repeating only the necessary audit.
  Report unrelated findings separately without folding them into the task.

### 4) Website business boundary (strict prohibition)

- A website-business task is one whose deliverable is the content,
  configuration, theme, or deployment result of a named or specific downstream
  site. Classification follows the requested deliverable, not the repository or
  file currently being edited. Generic Bukit capabilities and repository-owned
  examples are not website-business tasks merely because a site can use them.
- During a website-business task, do not implement any Core-owned change.
  Core-owned surfaces include `src/Bukit-Core/`, tests or fixtures that define
  Core behavior, public API/config/protocol baselines, Core contract
  documentation, and CI, release, or gate logic that changes Core behavior.
- Read-only Core inspection and disposable reproduction under `/tmp` or the
  downstream site workspace are allowed. Do not add Core regression tests or
  copy downstream fixtures into this repository as part of the website task.
- If the website task reveals a Bukit Core defect or missing capability, do not
  fix it as part of the website task. Report the evidence, affected behavior,
  likely Core ownership, proposed fix, and targeted verification approach.
- Plugin, theme, or example changes remain governed by the active task scope;
  they do not authorize a Core-owned change.
- A Bukit Core fix may proceed only after the user explicitly confirms it, and
  it must be implemented in a separate user-visible task/session dedicated to
  the Core change. Do not continue the Core fix in the current website task.
