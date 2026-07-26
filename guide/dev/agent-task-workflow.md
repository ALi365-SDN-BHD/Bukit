# Agent Task Workflow

The root `AGENTS.md` contains Bukit-specific red lines. General task methods are
owned by the applicable Superpowers skills and are not duplicated here.

## Superpowers ownership

Use Superpowers for analysis and planning, test-driven development, systematic
debugging, worktree isolation, optional sub-agent dispatch, code review, and
completion verification. An approved implementation plan should be executed
without repeating its design phase. Ordinary work does not require a sub-agent;
the main thread may implement, verify, and review it.

## Bukit high-speed verification model

### 1. Generate the verification closure

Before implementation, generate the affected closure from the task's actual
changed-file set:

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed <path>
```

The JSON result names changed files, direct source consumers, public or
serialized-contract consumers, exact specialty test commands, and unmapped
files. The controller must resolve every unmapped file before dispatch and must
not silently add aggregate gates.

### 2. Record and reuse GREEN evidence

After an exact specialty command passes, record its proof:

```bash
python3 scripts/checks/codex-workflow.py cache record \
  --record /tmp/codex-reports/<task>.json \
  --base HEAD \
  --command "<exact specialty command>" \
  --path <closure-file> \
  --env <relevant-variable-name> \
  --result passed --exit-code 0 --duration-ms <milliseconds>
```

Use `cache check` with the same inputs before rerunning that command. A hit is
valid only while the resolved HEAD, closure file content, exact command,
environment state, and SDK/toolchain version are unchanged. Records contain
only `set`, `empty`, or `unset` environment state, never values.

### 3. Specialty review

After implementation and the complete specialty test, perform one bounded
specialty review. Re-enter implementation and review only for Critical or
Important findings. Record Minor findings without blocking and stop when no
Critical or Important finding remains.

### 4. Delta-only final review

At parent-task completion, build `review-scope` from each task's evidence
summary and the current finding list. The final unified review is limited to:

- cross-task file intersections;
- invalidated specialty evidence;
- changed files not covered by reusable evidence;
- public or serialized-contract changes;
- still-open Critical or Important findings.

Unchanged GREEN specialty evidence is consumed, not rerun. Minor findings and
historical audits do not expand the final review scope. Run a final gate only
when the user or task contract explicitly names it.

## Single-writer queue

For a plan with multiple implementation tasks, initialize one state file
outside the repository and acquire it before a task writes:

```bash
python3 scripts/checks/codex-workflow.py queue init \
  --state /tmp/codex-reports/<parent>-writer.json
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/codex-reports/<parent>-writer.json \
  --task <task-id>
```

Move the active task through `writing`, `testing`, and `review_wait` with
`queue transition`. A direct transition from `writing` to `done` is invalid.
Only `done` or `blocked` releases the queue for another implementation task;
a blocked task may reacquire when the queue is free. Queue writes use an atomic
operation lock, and `queue status` emits the current schema-versioned state.

## Speed metrics

Record one bounded event after each implementation, test, review, or idle
phase:

```bash
python3 scripts/checks/codex-workflow.py metrics add \
  --state /tmp/codex-reports/<parent>-metrics.json \
  --task <task-id> \
  --phase <implementation|test|review|idle> \
  --duration-ms <milliseconds> \
  --cache-status <hit|miss|none> \
  --status <completed|blocked>
```

Use `--rerun` and `--conflict` when applicable. `--command-label` accepts only
a short identifier such as `config-tests`; never pass the raw command, an
environment value, a credential-bearing URL, or another secret. At parent
completion, `metrics report` summarizes phase and task duration, cache hit
rate, duplicate command labels, reruns, conflicts, and status counts.

## Owner gates and failures

Changes to CI, release, gate, or verification files require their direct owner
test or self-test. If the actual owner gate is a
full or release gate, obtain explicit user authorization; do not substitute a
different command and claim equivalent proof.

Classify a failure before changing code. Environment, permission, tool, and
infrastructure failures must be reported with the exact command and evidence;
they do not authorize changes outside the active scope.

The protected reference areas and website/Core isolation boundary are defined
only in the root `AGENTS.md`.
