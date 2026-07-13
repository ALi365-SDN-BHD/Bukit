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

The targeted gate runs `git diff --check`, `bash -n` for changed shell scripts,
the thin `ci-fast` gate, and only the affected test projects it can map from
changed source or test paths. If a runtime source path cannot be mapped to a
targeted test project, the script fails and asks for an explicit test target
instead of falling back to a full gate.

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
