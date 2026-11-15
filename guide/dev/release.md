# Release

## Authorization Boundary

Bukit Core is currently enterprise internal-first. Regular public binary
releases are paused. The procedures below remain active for internal artifacts,
internal deployment validation, and an exceptionally approved public release.

Passing CI, Native AOT, reproducibility, smoke, coverage, security, checksum,
or release-asset verification proves technical state only. It does not
authorize publication. A public release requires explicit management approval.

### GitHub Enforcement

The `publish-release` job targets the `public-release` GitHub Environment and
accepts only `refs/heads/main`. Repository administrators must configure that
Environment with:

- at least one explicitly authorized management reviewer;
- prevent self-review enabled;
- administrator bypass disabled;
- a deployment branch policy allowing only `main`.

The workflow contract is not operationally complete until those external
settings are present. Internal artifact runs keep `publish=false` and do not
enter the protected Environment.

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

## Fast And Full Source Gates

`scripts/gates/ci-fast.sh` validates docs, config-docs contracts, skills, README
links, and Core CLI script boundaries. It also runs verification-script
self-tests, `dotnet format`, the code-analysis debt ratchet, public API drift
checks, and YAML static-context regeneration checks. The public API check
builds Core; the YAML check builds the Theme project in temporary artifact
directories. It requires the .NET toolchain and restored dependencies, can
write build intermediates, and is not a docs-only or read-only check. It does
not perform Native AOT packaging or the full Core test suite.

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
`Release`, and accepts `linux-x64`, `linux-arm64`, `osx-arm64`, `osx-x64`, or
`win-x64`. It prints the non-empty archive path on stdout after recreating the
selected RID publish directory. Wrong arity, an invalid version, or an
unsupported RID exits 2; unsafe output state, empty output, or other packaging
failure exits nonzero.

Prove that two clean Native AOT publishes have identical relative paths,
entry types, sizes, and SHA-256 values with:

```bash
bash scripts/build/build-repro.sh <version> <rid> [configuration]
```

This entrypoint requires two or three arguments and also defaults to `Release`.
It packages twice with one commit and source timestamp, compares both publish
trees, and exits 0 only when they are identical. Wrong arity or an invalid
delegated version/RID exits 2; a tree mismatch or build failure is nonzero.
Set `BUKIT_REPRO_ROUNDS` to an integer from 2 to 10 to publish that many times
and compare every later tree with the first; unset or empty means 2, and any
other value exits 2. Set `BUKIT_REPRO_REFERENCE` to an already packaged publish
directory to compare every fresh publish with that tree instead; a relative
path resolves from the caller's directory, and a missing directory or an empty
value exits 2. The reference must come from the same commit and version. The
source revision is compiled into `bukit`, so `GITHUB_SHA` must also be the same
in both runs: either unset in both, or exported with the same value.

Every packaging job in the release workflow (`package-linux`,
`package-linux-arm64`, `package-macos`, `package-macos-intel`, `package-windows`)
packages first, then compares the packaged tree with at least two more clean
publishes, so the uploaded binary is one of the images compared in that job. The
Intel macOS job uses three more clean publishes to cover the known linker race on
a new runner image. Each job builds on a pinned runner: `package-linux` on
`ubuntu-24.04`, `package-linux-arm64` on `ubuntu-24.04-arm`, and
`package-macos-intel` on `macos-15-intel`. The two Linux jobs share the Ubuntu
24.04 glibc baseline instead of following `ubuntu-latest`.

The injected contract checks do not replace the real host-RID reproducibility
run:

```bash
bash scripts/build/native-aot-self-test.sh
bash scripts/build/build-repro-self-test.sh
```

### Windows path mapping and link timestamp

On `win-*` two things made every publish differ, measured on `windows-latest`
with ILCompiler 10.0.15:

- **`PathMap` did not match.** Git Bash reports the checkout as `/d/a/...`
  while the compiler sees `D:\a\...`. A `PathMap` written in the first form
  maps nothing, so the random build root was compiled into every managed
  assembly, and from there into the ILC object and about a third of the
  Windows executable. The packaging script now converts the source and build roots
  with `cygpath -w` on a Windows host (Git Bash, MSYS2 or Cygwin, by `uname`),
  not merely when a `cygpath` is on `PATH`. Published Windows binaries no longer
  embed runner paths.
- **The linker stamped the link time.** With the paths fixed, three bytes still
  differed: the PE `TimeDateStamp` and its copies. The script passes `/Brepro`
  through a `LinkerArg` item. ILCompiler reads `ExtraLinkerArg` only on Unix, so
  the macOS item name does nothing here.

Linux needs no hook: ILCompiler already passes `--build-id=sha1`, and four
clean `linux-x64` publishes were identical.

### macOS linker determinism

For `osx-*` RIDs the packaging script passes `-objc_stubs_small` to the linker
through ILCompiler's `ExtraLinkerArg` item. Apple's linker otherwise assigns
the GOT slot used by its default `objc_msgSend` stubs nondeterministically, so
identical inputs link to one of two images. They differ by three bytes in
`__TEXT`; `LC_UUID` is a content hash and the ad-hoc signature covers both, so
the visible difference is about 83 bytes in `bukit` only.

- ILC output is already byte-identical between clean builds. Do not add
  `-no_uuid` or `IlcSingleThreaded=true`: they hide part of the difference or
  slow the build without removing it, and `-no_uuid` drops the `LC_UUID` that
  crash symbolication needs.
- `-reproducible` and `-ld_classic` do not remove the race either.
- The race shows up in roughly half of the links, so one passing
  `build-repro.sh` run is weak evidence. After changing the SDK, Xcode, or the
  linker arguments, run it with `BUKIT_REPRO_ROUNDS=3` or more.
- This was measured with the Command Line Tools linker `ld-1167.5` and
  ILCompiler 10.0.14. A different Xcode needs the same check again.

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
linux-x64   -> linux-x64
linux-arm64 -> linux-arm64
osx-arm64   -> osx-arm64
osx-x64     -> osx-x64
win-x64     -> win-x64
all         -> linux-x64 linux-arm64 osx-arm64 osx-x64 win-x64
```

Run the asset and final-archive injected checks directly when changing these
surfaces:

```bash
bash scripts/release/release-assets-self-test.sh
bash scripts/smoke/release-artifacts-self-test.sh
```

## Workflow execution and failure evidence

After input validation, fast gate and Architecture contracts run independently.
`Fast contracts` requires both to succeed before Core tests, coverage and security.
The complete non-instrumented Core tests and selector-verified security tests remain
required. Each test project restores its own dependencies; no broad test-solution
restore precedes those runs. Coverage project artifacts and security TRX files are
uploaded even after failure; test failures still block packaging.

Public requests check `v<version>` against the build SHA before expensive work and
again after protected Environment approval, immediately before publication. Missing
tags are permitted; lightweight and annotated tags must resolve to the build SHA.
Network or malformed responses fail closed. New tags explicitly target the workflow commit SHA.
The check is read-only and does not replace management approval or prevent a
concurrent external tag change between verification and publication.

`BUKIT_SECURITY_RESULTS=TestResults/security` preserves each run in a fresh
subdirectory, including failed TRX files. Without it, temporary results are cleaned
on exit. Existing evidence is never deleted by this option.
