# Testing

For docs and scripts work, start with the focused fast checks:

```bash
bash scripts/checks/docs-consistency.sh
bash scripts/checks/config-docs-contract.sh
bash scripts/checks/cli-docs-sync.sh
bash scripts/checks/skills-schema.sh
bash guide/skills/scripts/validate-skills-strict.sh
bash scripts/checks/readme-sync.sh
bash scripts/checks/core-cli-contract.sh
```

Then run the thin repository gate:

```bash
bash scripts/gates/ci-fast.sh Release
```

## Strict Script Proof Paths

Run the injected security regression before the real security gate:

```bash
bash scripts/security/security-regression-self-test.sh
bash scripts/security/security-regression.sh Release
```

The self-test proves that zero tests, a missing selector result, a missing TRX,
a failed result, and a malformed selector are rejected. The real gate writes a
separate TRX for each security test project and exits successfully only when
every configured selector has an executed, passing result and all counters are
nonzero and clean. The optional configuration argument defaults to `Release`.

Smoke either a final release archive or an existing publish directory with
exactly one supported RID:

```bash
bash scripts/smoke/release-artifacts.sh <archive-or-publish-dir> <rid>
bash scripts/smoke/release-artifacts-self-test.sh
```

Supported RIDs are `linux-x64`, `osx-arm64`, and `win-x64`. The smoke entrypoint
requires exactly two arguments, safely extracts an archive when necessary,
requires exactly one packaged CLI, and runs `config check`, `build --clean`,
and `publish audit` with that CLI. Wrong arity exits 2; an unsupported RID,
missing or unsafe artifact, invalid CLI set, or failed smoke exits nonzero.

The two auxiliary behavior self-tests are also direct proof paths and are part
of `ci-fast`:

```bash
bash scripts/checks/brainstorm-server-self-test.sh
bash scripts/checks/find-polluter-self-test.sh
```

The active workflow boundary also has a direct self-test:

```bash
bash scripts/checks/active-workflow-boundary-self-test.sh
bash scripts/checks/active-workflow-boundary.sh
```

It proves that backup/reference paths are rejected from runtime source,
official guide content, active scripts, and CI workflows while allowing only
the narrow policy declarations that describe the boundary itself.

The brainstorm test requires access to process identity inspection. The
polluter test proves clean `0`, confirmed polluter `1`, and inconclusive/error
`2` classification while preserving paths containing spaces or newlines.

## Post-change Targeted Verification

After each ordinary small code subtask, run the local targeted gate without
creating a sub-agent audit by default:

```bash
bash scripts/checks/post-change-targeted.sh -- <changed paths>
```

If no paths are provided, the script reads changed files from the working tree
relative to `HEAD`. When unrelated local changes exist, pass the current task
paths explicitly. Use `--dry-run` to inspect the exact commands before running
them.

Run the targeted gate before committing the subtask. Its default `--base HEAD`
then includes the working-tree diff in `git diff --check`. If the subtask was
already committed, use its exact starting SHA:

```bash
bash scripts/checks/post-change-targeted.sh \
  --base <subtask-base-sha> -- <changed paths>
```

For final verification of a multi-subtask parent task, use the parent task's
starting SHA and all paths changed by that parent task:

```bash
bash scripts/checks/post-change-targeted.sh \
  --base <parent-task-base-sha> -- <all parent-task changed paths>
```

The targeted gate runs `git diff --check`, `bash -n` for changed shell scripts,
the thin `ci-fast` gate, and only the affected test projects it can map from
changed source or test paths. If a runtime source path cannot be mapped to a
targeted test project, the script fails and asks for an explicit test target
instead of falling back to a full gate.

If a targeted check fails, stop task progression and classify the result as a
scoped regression, pre-existing failure, environment restriction, or
infrastructure noise. Change the active task only when evidence connects the
failure to its diff. Rerun environment-sensitive checks in an appropriate
permitted environment when safe; if verification remains blocked, report the
exact command and limitation. Do not modify unrelated code merely to make a
gate pass.

## Task classification and review plan

Before implementation, record the parent objective, ordered code subtasks,
each subtask's risk and targeted gate, immediate high-risk review requirements,
and whether a final consolidated audit is required.

An ordinary small task has one bounded objective and one primary ownership
surface, introduces no cross-module contract change, and is fully covered by a
focused targeted gate. Treat the parent as large when it has multiple code
subtasks, changes contracts across modules, combines multiple high-risk
surfaces, makes broad mechanical changes across projects, or the user
explicitly designates it large. Commit count and a single-subtask label do not
reduce the classification.

For a website-business task, where the deliverable is a named or specific
site's content, configuration, theme, or deployment result, Core is a read-only
dependency. Do not change Core source, Core-defining tests or fixtures, public
API/config/protocol baselines, Core contract documentation, or CI/release/gate
logic that changes Core behavior. Reproduce under `/tmp` or the downstream
workspace and report the proposed Core repair for a separately confirmed task;
do not add a Core regression test or copy a downstream fixture into this
repository during the website task.

## Rule-change verification

Rule-definition and rule-modification tasks do not require runtime, full, or
release gates, but they must run:

```bash
git diff --check -- <changed-governance-paths>
bash scripts/checks/docs-consistency.sh
```

When `guide/skills/` or a nested `AGENTS.md` changes, also run:

```bash
bash scripts/checks/skills-schema.sh
bash guide/skills/scripts/validate-skills-strict.sh
```

`docs-consistency.sh` includes the semantic contract that keeps the root
`AGENTS.md`, the agent workflow, this testing guide, and applicable nested
`AGENTS.md` rules aligned.

The root `AGENTS.md` applies repository-wide. Nested `AGENTS.md` files apply
only to their directory and descendants and may supplement or tighten, but not
silently weaken, root-level strict prohibitions.

## Consolidated Post-change Audit

When a large parent task contains multiple code subtasks, each subtask must pass
its targeted gate before the next one begins. After all subtasks pass, run one
bounded read-only audit that:

- checks each subtask against its declared scope and targeted-gate evidence;
- checks the aggregate parent-task diff for cross-subtask regressions,
  omissions, and unrelated changes.

A standalone ordinary small task does not require a sub-agent audit unless the
user explicitly requests one. Audit a high-risk subtask immediately when it
changes security or authorization, concurrency or consistency, persistence
formats or migrations, public APIs or plugin/config contracts,
CI/release/gate logic, or when targeted checks cannot cover its key behavior.

If an audit finds an issue, fix the affected scope, rerun its targeted gate,
and repeat only the necessary audit. An audit must not widen verification into
a full or release gate.

The post-change flow must not run `scripts/gates/ci-full.sh`,
`scripts/gates/release.sh`, `scripts/test-all.sh`, `scripts/smoke-all.sh`,
`dotnet test bukit-test.slnx`, or whole-solution `.slnx` tests unless that
broader proof is explicitly requested.

Runtime changes need targeted `dotnet test` runs for the affected project. Only
when the user explicitly requests Core-wide verification, run:

```bash
bash scripts/gates/ci-full.sh Release
```

`ci-full` runs the fast contract gate and `scripts/checks/core-tests.sh`, which
executes every Core runtime test project explicitly without fixture-dependent
test-name exclusions.

Core coverage is an explicit gate with short, visible steps:

```bash
bash scripts/checks/coverage-baseline-schema.sh
bash scripts/checks/coverage.sh Release
```

The coverage entrypoint delegates to small scripts under `scripts/checks/coverage/`.
It runs each Core test project separately, writes project coverage under
`TestResults/coverage/projects/`, and writes the final Core-only summary to
`TestResults/coverage/coverage-summary.txt`.

In CI and release workflows, `coverage-plan` validates policy and behavior
contracts, then generates a per-project matrix from the same project list used
by `core-tests.sh`. The `coverage-summary` job downloads those isolated results,
enforces the Core thresholds, and uploads the summary together with
`docs/coverage-baselines.json`. `Fast contracts` also runs Architecture tests so
workflow and coverage contracts cannot drift without failing CI.

Architecture/governance tests are not folded into `ci-full` because they also
validate repository workflow files and coverage script contracts. The top-level
`scripts/test-all.sh` runs
`dotnet test bukit-test.slnx` and is broader than the Core gate.

Smoke validation is fixture-based. `scripts/smoke/core.sh` requires `BUKIT_BIN`
and `BUKIT_SMOKE_ROOT`; it fails fast when those are not set so a placeholder
cannot be mistaken for a passed smoke.

Do not run release-level artifact validation for a docs task unless the task
changes release assets or publishing scripts.
