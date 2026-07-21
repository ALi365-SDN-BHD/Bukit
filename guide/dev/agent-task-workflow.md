# Agent Task Workflow

The root `AGENTS.md` contains Bukit-specific red lines. General task methods are
owned by the applicable Superpowers skills and are not duplicated here.

## Superpowers ownership

Use Superpowers for analysis and planning, test-driven development, systematic
debugging, worktree isolation, optional sub-agent dispatch, code review, and
completion verification. An approved implementation plan should be executed
without repeating its design phase. Ordinary work does not require a sub-agent;
the main thread may implement, verify, and review it.

## Bukit verification model

### 1. Focused affected checks

After each code subtask, pass only that subtask's paths:

```bash
bash scripts/checks/post-change-focused.sh -- <changed paths>
```

Focused verification checks whitespace, changed shell syntax, registered owner
self-tests, and mapped affected test projects. It never runs `ci-fast`, a full
or release gate, or a whole-solution test. Use `--dry-run` to inspect its exact
commands and `--base <sha>` when the relevant diff is already committed.

### 2. High-risk stable checkpoint

After a stable security, authorization, concurrency, consistency, persistence,
public-contract, or gate-logic change passes focused checks, perform one bounded
read-only review. Superpowers decides whether the main thread or a sub-agent is
the useful reviewer. Later aggregate review does not repeat the implementation
review; it checks only integration and closure.

### 3. Aggregate parent gate

At parent-task completion, run one aggregate gate from the parent's starting
SHA with every path owned by the task:

```bash
bash scripts/checks/post-change-targeted.sh \
  --base <parent-task-base-sha> -- <all parent-task changed paths>
```

The aggregate gate runs focused verification for the complete diff and then
invokes `ci-fast` exactly once. It never upgrades itself to full, release, or
whole-solution verification.

After the gate, perform one aggregate interaction review for cross-subtask
regressions, omitted contract updates, unrelated changes, and closure of prior
high-risk findings.

## Owner gates and failures

Changes to CI, release, gate, or verification files require their direct owner
test or self-test during focused verification. If the actual owner gate is a
full or release gate, obtain explicit user authorization; do not substitute a
different command and claim equivalent proof.

Classify a failure before changing code. Environment, permission, tool, and
infrastructure failures must be reported with the exact command and evidence;
they do not authorize changes outside the active scope.

The protected reference areas and website/Core isolation boundary are defined
only in the root `AGENTS.md`.
