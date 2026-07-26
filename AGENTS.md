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
- Run only the complete specialty tests explicitly required by the current task and affected file set. Do not add unscheduled audits, `post-change-*`, aggregate matrices, historical fixtures, or unnamed gates; only the specialty and final reviews defined below are routine.
- Before dispatch, the controller must define the affected verification closure: changed files, direct consumers, public or serialized-contract consumers, and the exact specialty test command.
- Cache GREEN evidence by `HEAD`, the related working-tree content or diff hash, verification-closure file set, exact test command, relevant environment-variable state, and SDK/toolchain version. Reuse it only while every fingerprint input is unchanged; do not rerun an unchanged fixture merely to refresh evidence.
- Serialize tests or builds that contend for Bukit locks, plugin locks, build manifests, caches, or fixture output. Pure static contracts with disjoint inputs may run in parallel.
- At parent-task completion, run only the final review and gate explicitly required by the user or task contract. Do not infer `post-change-targeted`, `ci-fast`, or a release gate.
- Changes to CI, release, gate, or verification files require their direct owner test/self-test. A full/release owner gate still requires explicit user authorization.

## High-speed agent workflow

### Single writer and fixed slots

- At any moment, only one implementation agent may modify repository files. The controller coordinates and verifies but does not make concurrent edits.
- Use up to four fixed slots: slot 1 controller; slot 2 current implementation; slot 3 read-only source-consumer search or test inventory; slot 4 read-only documentation preparation.
- Slots 3 and 4 must remain read-only, work on mutually exclusive investigations, and never edit prompts, tests, lock files, manifests, or implementation files while slot 2 is writing.
- Do not keep a standing audit agent. Temporarily replace slot 3 or 4 only when a scheduled specialty or final review is due.
- File mutation, Git commits, Bukit fixture builds, plugin-lock resolution, build-manifest generation, and Notion writes or publication are serial operations. Parallelize only source investigation, consumer search, test inventory, documentation drafts, and read-only external-state checks.

### Review frequency

- For an ordinary implementation task, perform one specialty review after implementation and specialty tests finish.
- Re-enter implementation and scoped re-review only for Critical or Important findings. Record Minor findings without blocking the current task.
- Stop the review when it has no Critical or Important findings. Do not dispatch duplicate reviews for the same diff.
- After all implementation tasks finish, perform one final unified review. Do not repeat historical audits already backed by unchanged evidence.

### Agent reporting and long-running work

- Before dispatch, the controller must assign a unique report-file path. Read-only agents must write reports outside the repository, preferably under `/tmp/codex-reports/`; this evidence write does not grant repository write authority or count as the implementation writer. The agent must write its detailed evidence there before returning.
- Each agent's final response must contain only:
  - `STATUS: DONE | BLOCKED`
  - `COMMIT: <sha or none>`
  - `TESTS: <commands and pass/fail summary>`
  - `FINDINGS: Critical <n> / Important <n> / Minor <n>`
  - `CONCERNS: <at most three items>`
  - `REPORT: <path>`
- Put full investigation notes, diffs, and logs in the report file; do not repeat large history or raw output in the controller conversation.
- Configure long-running tool calls to yield within 60 seconds and report progress between waits. After the same error occurs twice, stop blind retries and switch to targeted diagnosis.
- If an agent provides no progress for 90 seconds, the controller must check whether it is stalled, waiting for input, or holding a contested resource.

## Failure boundary

- Environment, permission, tool, or infrastructure failures do not authorize unrelated code changes.
- If required proof remains unavailable, report the exact blocker and do not claim the gate passed.
