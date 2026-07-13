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

## Native AOT and Reproducibility

Build one Native AOT package with:

```bash
bash scripts/build/native-aot.sh <version> <rid> <output-root> [configuration]
```

The entrypoint requires three or four arguments, defaults `configuration` to
`Release`, and accepts `linux-x64`, `osx-arm64`, or `win-x64`. It prints the
non-empty archive path on stdout after recreating the selected RID publish
directory. Wrong arity, an invalid version, or an unsupported RID exits 2;
unsafe output state, empty output, or other packaging failure exits nonzero.

Prove that two clean Native AOT publishes have identical relative paths,
entry types, sizes, and SHA-256 values with:

```bash
bash scripts/build/build-repro.sh <version> <rid> [configuration]
```

This entrypoint requires two or three arguments and also defaults to `Release`.
It packages twice with one commit and source timestamp, compares both publish
trees, and exits 0 only when they are identical. Wrong arity or an invalid
delegated version/RID exits 2; a tree mismatch or build failure is nonzero.

The injected contract checks do not replace the real host-RID reproducibility
run:

```bash
bash scripts/build/native-aot-self-test.sh
bash scripts/build/build-repro-self-test.sh
```

## Release Asset Order

Prepare and verify the exact release asset set with:

```bash
bash scripts/release/prepare-release-assets.sh <version> <commit> <output-dir> <archive>...
bash scripts/release/verify-release-assets.sh <version> <commit> <asset-dir> [expected-rid...]
```

Prepare requires at least one archive; verify accepts zero or more expected
RIDs. Usage errors exit 2. Invalid paths, duplicate or stale assets, schema or
checksum differences, and expected-set mismatches exit 1.

For each selected RID, the release workflow packages the binary, smokes the
final archive, and only then uploads that package artifact. The collection job
downloads those artifacts, sorts their paths, prepares `release-assets`, maps
the selected input to the exact RID order below, verifies the prepared set, and
only then uploads `release-assets/*`:

```text
linux-x64 -> linux-x64
osx-arm64 -> osx-arm64
win-x64   -> win-x64
all       -> linux-x64 osx-arm64 win-x64
```

Run the asset and final-archive injected checks directly when changing these
surfaces:

```bash
bash scripts/release/release-assets-self-test.sh
bash scripts/smoke/release-artifacts-self-test.sh
```
