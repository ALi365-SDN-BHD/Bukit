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

Runtime changes need targeted `dotnet test` runs for the affected project. For
Core-wide verification, run:

```bash
bash scripts/gates/ci-full.sh Release
```

`ci-full` runs the fast contract gate and `scripts/checks/core-tests.sh`, which
executes Core runtime test projects explicitly. When `examples/silkroad_biz23`
is absent, the Core gate filters only the fixture-backed Silkroad example test;
if that fixture exists, the test runs normally. Architecture/governance tests are
not folded into this gate because they also validate repository workflow files
and coverage scripts. The top-level `scripts/test-all.sh` runs
`dotnet test bukit-test.slnx` and is broader than the Core gate.

Smoke validation is fixture-based. `scripts/smoke/core.sh` requires `BUKIT_BIN`
and `BUKIT_SMOKE_ROOT`; it fails fast when those are not set so a placeholder
cannot be mistaken for a passed smoke.

Do not run release-level artifact validation for a docs task unless the task
changes release assets or publishing scripts.
