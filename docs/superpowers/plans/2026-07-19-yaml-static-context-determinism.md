# YamlDotNet AOT Static Context Determinism Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve Bukit's public YAML static-context and Native AOT behavior while making two clean builds of the same source, version, commit, and SDK byte-identical.

**Architecture:** Keep YamlDotNet's upstream generator only as an explicit regeneration tool. Commit its normalized output as the default compilation input, replacing random accessor GUID suffixes with SHA-256-derived suffixes from the full generated identifier base. A drift gate regenerates, normalizes, and compares the source; release packaging keeps clean isolated artifacts and the existing strict publish-tree comparison.

**Tech Stack:** .NET 10, C#, YamlDotNet static contexts, Python 3, Bash, Native AOT.

## Global Constraints

- Preserve the public `Bukit.Theme.ThemeManifestYamlStaticContext` type, constructor, overrides, and known-type behavior.
- Preserve Native AOT static registration and current YAML/theme manifest runtime semantics.
- Do not change YAML configuration, theme manifest schema, plugin protocol, asset URLs, routing, rendering, or persisted formats.
- Do not obtain a green result by reusing `obj`, ignoring PDB/binary differences, weakening `compare-publish-trees.py`, or patching compiled binaries.
- Keep `Vecc.YamlDotNet.Analyzers.StaticGenerator` version centrally governed; it may run only in explicit regeneration mode.
- Do not modify backup/reference directories.

---

### Task 1: Deterministic generated-identifier normalizer

**Files:**
- Create: `scripts/build/normalize-yaml-static-context.py`
- Create: `scripts/build/normalize-yaml-static-context-self-test.sh`

**Interfaces:**
- Consumes: one UTF-8 or UTF-8-BOM `YamlDotNetAutoGraph.g.cs` file.
- Produces: `normalize-yaml-static-context.py INPUT OUTPUT`, with deterministic LF output and a provenance header.

- [x] Write a self-test whose fixture contains repeated identifiers of the form `<base>_<32 lowercase hex>`.
- [x] Assert two random GUID variants normalize to identical bytes, different bases remain distinct, and the provenance header is present.
- [x] Assert no matching identifiers and one base paired with two GUIDs fail without producing a successful result.
- [x] Run `bash scripts/build/normalize-yaml-static-context-self-test.sh` and record the expected RED caused by the missing normalizer.
- [x] Implement SHA-256(base UTF-8), first 32 lowercase hex characters, with collision and malformed-input rejection.
- [x] Rerun the self-test and `bash -n`; expect PASS.
- [x] Run `bash scripts/checks/post-change-targeted.sh --` for both paths.

### Task 2: Governed checked-in static context and drift gate

**Files:**
- Create: `src/Bukit-Core/Bukit.Theme/ThemeManifestYamlStaticContext.Generated.cs`
- Create: `scripts/build/yaml-static-context.sh`
- Modify: `src/Bukit-Core/Bukit.Theme/Bukit.Theme.csproj`
- Modify: `src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj`
- Create: `tests/Bukit.Theme.Tests/ThemeManifestYamlStaticContextTests.cs`

**Interfaces:**
- Default builds compile the checked-in deterministic generated file and do not run the Vecc analyzer.
- `bash scripts/build/yaml-static-context.sh check|update` enables the analyzer only for regeneration, excludes the checked-in file during that generation build, normalizes exactly one `YamlDotNetAutoGraph.g.cs`, and compares or updates the tracked file.

- [x] Add a static-context runtime characterization test using `StaticDeserializerBuilder` and `ThemeManifestYamlStaticContext`.
- [x] Run the existing real `build-repro.sh` RED evidence and retain the random-GUID failure record.
- [x] Add conditional generator mode to `Bukit.Theme.csproj`; remove the unused analyzer reference from `Bukit.Cli.csproj` without removing the central version.
- [x] Add the regeneration script with strict arity, exact generated-file cardinality, temporary-directory cleanup, and `check`/`update` modes.
- [x] Generate and normalize the current 16.3.0 static context into the tracked C# file.
- [x] Run the static-context test, all `Bukit.Theme.Tests`, the normalizer self-test, and `yaml-static-context.sh check`.
- [x] Run `post-change-targeted.sh` with the exact Task 2 paths.

### Task 3: Native AOT proof, documentation, and aggregate review

**Files:**
- Modify: `scripts/build/package-native-aot.sh`
- Modify: `scripts/build/native-aot-self-test.sh`
- Modify: `docs/analysis/bukit-core-post-closure-rc-qualification-2026-07-19.zh-CN.md`
- Modify: `docs/analysis/bukit-core-eight-findings-final-aggregate-closure-audit-2026-07-19.zh-CN.md`
- Modify current release/developer documentation only if the regeneration command is a user-facing maintainer contract.

**Interfaces:**
- Clean package builds use a unique artifacts root mapped to `/_/build` and delete only that invocation's temporary root.
- `build-repro.sh` continues comparing all publish-tree entries by path, type, size, and SHA-256.

- [x] Run `native-aot-self-test.sh`, `build-repro-self-test.sh`, and `yaml-static-context.sh check`.
- [x] Execute two real clean `osx-arm64` AOT publishes through `build-repro.sh`; require exit 0 without comparison exceptions.
- [x] Build one real archive, run `release-artifacts.sh`, then prepare and verify the exact release asset set.
- [x] Run relevant Theme/CLI/Architecture tests and the aggregate explicit-path `post-change-targeted.sh`.
- [x] Update the RC report from BLOCKED to PASS only if every required command above exits 0; retain the root-cause and RED evidence.
- [x] Perform one independent read-only aggregate review covering scope, generated-source governance, public/AOT compatibility, release safety, and evidence.
- [x] Run fresh `git diff --check` and final required gates before any completion claim.
