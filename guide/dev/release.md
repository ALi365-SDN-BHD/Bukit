# Release

Release work is broader than the fast docs gate. It should be explicit and
artifact-driven.

## Release Surfaces

- CLI command help and static command registry.
- Config schema and strict validation.
- Native AOT publishing.
- Release artifact packaging.
- Core coverage summary from `scripts/checks/coverage.sh`.
- Smoke tests against packaged binaries.
- SEO, GEO, publish, security, and route report schemas.
- README and guide links.

## Thin Gates

The current `scripts/gates/ci-fast.sh` is intentionally thin. It validates docs,
config-docs contracts, skills, README links, and Core CLI script boundaries
without running expensive release work.

`scripts/gates/ci-full.sh` is the Core source gate: it runs `ci-fast` and the
explicit Core test project list in `scripts/checks/core-tests.sh`. Release
artifact checks must still be invoked explicitly during release tasks.

Core coverage is separate from `ci-full` so it can print visible per-project
progress. Run:

```bash
bash scripts/checks/coverage-baseline-schema.sh
bash scripts/checks/coverage.sh Release
```

The active CI and release workflows split this into `coverage-plan`, parallel
per-project coverage jobs, and `coverage-summary`. Packaging depends on the
summary job, and the final `core-coverage` artifact contains both
`TestResults/coverage/` and `docs/coverage-baselines.json`.
