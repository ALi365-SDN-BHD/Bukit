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
