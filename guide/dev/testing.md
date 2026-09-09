# Testing

## Verification closure

Before a subtask is dispatched, generate its complete affected closure:

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed <path>
```

The result contains changed files, direct source consumers, public or
serialized-contract consumers, exact specialty test commands, and unmapped
files. Resolve unmapped files explicitly; do not infer an aggregate matrix.

## GREEN evidence cache

Record a passing specialty command under `/tmp/codex-reports/` with:

```bash
python3 scripts/checks/codex-workflow.py cache record \
  --record /tmp/codex-reports/<task>.json \
  --base HEAD \
  --command "<exact specialty command>" \
  --path <closure-file> \
  --result passed --exit-code 0 --duration-ms <milliseconds>
```

Run the corresponding `cache check` before repeating a previously GREEN
command. Reuse exit `0` only when HEAD, every closure file's content, the exact
command, relevant environment state, and SDK/toolchain version are unchanged.
Exit `1` means the evidence is stale; exit `2` means the record or invocation is
invalid. Environment values are never persisted.

The workflow tool and policy have a direct self-test:

```bash
bash scripts/checks/codex-workflow-self-test.sh
```

## Resource classification

Before scheduling the closure's exact specialty commands, classify its paths
and commands:

```bash
python3 scripts/checks/codex-workflow.py classify \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --path <closure-file> \
  --test-command "<exact specialty command>"
```

Execute the returned batches in order. Disjoint `static-parallel` commands may
run concurrently. `dotnet-serial` commands run one at a time.
`fixture-exclusive` work runs alone because it may contend for Bukit locks,
plugin locks, build manifests, caches, or fixture output. The closure command
entries carry the same resource labels.

## Format contract

Use the repository-owned wrapper locally and in CI so both environments execute
the same restored-solution contract:

```bash
bash scripts/checks/dotnet-format.sh
```

The wrapper runs `dotnet format bukit-core.slnx --verify-no-changes
--no-restore`. Its direct wiring and command contract are covered by:

```bash
bash scripts/checks/dotnet-format-self-test.sh
```

## Code analysis debt ratchet

The code-analysis gate inventories all SDK style and analyzer diagnostics at
`info` severity, but it does not promote the historical inventory to build
errors. It compares per-diagnostic counts and rejects only a new diagnostic ID
or an increase above the committed baseline:

```bash
bash scripts/checks/code-analysis-ratchet.sh check
```

After an intentional analyzer-wave or policy change, write a candidate baseline
to a new path and review its diagnostic-by-diagnostic delta before replacing the
committed baseline:

```bash
bash scripts/checks/code-analysis-ratchet.sh snapshot OUTPUT
```

The snapshot command refuses to overwrite an existing path. The comparator,
formatter-status handling, baseline shape, owner routing, and `ci-fast` wiring
are covered by:

```bash
bash scripts/checks/code-analysis-ratchet-self-test.sh
```

## Final review scope

Use `review-scope` once after all specialty work finishes. Its result limits the
final unified review to cross-task intersections, invalidated or missing
evidence, uncovered changed files, public-contract focus, and open
Critical/Important findings. Unchanged specialty proof is not rerun, and Minor
findings do not broaden the scope.

No `post-change-*`, `ci-fast`, full/release, whole-solution, historical fixture,
or unnamed gate is routine. Run only the exact specialty tests and final gate
explicitly required by the task contract.

## Direct owner proof paths

For repository agent-governance and active development-documentation changes:

```bash
bash scripts/checks/agent-governance-contract.sh
```

For `guide/skills/` content changes, use the Skills pack's own validators:

```bash
bash scripts/checks/skills-schema.sh
bash guide/skills/scripts/validate-skills-strict.sh
```

For CI-fast wiring and active workflow boundaries:

```bash
bash scripts/checks/ci-fast-portability-self-test.sh
bash scripts/checks/active-workflow-boundary-self-test.sh
bash scripts/checks/active-workflow-boundary.sh
```

For security and release-artifact script behavior:

```bash
bash scripts/security/security-regression-self-test.sh
bash scripts/smoke/release-artifacts-self-test.sh
bash scripts/release/release-assets-self-test.sh
bash scripts/build/native-aot-self-test.sh
bash scripts/build/build-repro-self-test.sh
bash scripts/build/normalize-yaml-static-context-self-test.sh
```

Run a real full/release owner gate only with explicit user authorization. If
authorization is absent, report the remaining verification boundary. Unknown
gate or verification paths require a registered direct owner self-test.

## Explicit broad gates

The following are never part of focused or aggregate post-change verification:

```bash
bash scripts/gates/ci-full.sh Release
bash scripts/gates/release.sh Release
bash scripts/test-all.sh
bash scripts/smoke-all.sh
dotnet test bukit-test.slnx
```

Use them only when the user explicitly requests the corresponding broad proof.

## Coverage and artifact checks

Core coverage is a separate explicit gate:

```bash
bash scripts/checks/coverage-baseline-schema.sh
bash scripts/checks/coverage.sh Release
```

In CI and release workflows, `coverage-plan` validates policy and builds a
per-project matrix from the unchanged 13 Core test projects. Each
`coverage-projects` invocation runs that project's complete tests once and collects
coverage and TRX together. `coverage-summary` rejects missing project results,
enforces 84% overall and 70% per-project coverage, and uploads the evidence.
The single-project runner validates actual executed tests and reports skipped
cases; its direct owner test is:

```bash
bash scripts/checks/coverage-run-one-self-test.sh
```

Ordinary CI no longer repeats those full project tests through `ci-full.sh`.
The `Core tests` check aggregates required coverage and platform results; failure,
cancellation, missing evidence, or an unexpected skipped prerequisite cannot pass
it. PRs, the existing push branches and manual `core` run fast contracts, full Core
coverage, three platform specialties and the aggregate. Manual `coverage` runs
fast plus coverage; manual `fast` runs fast only. The release workflow and local
`ci-full.sh` keep their existing structure and separate authorization boundaries.

The non-instrumented platform specialties select these complete test classes:

| Runner / actual target | Required classes |
|---|---|
| `ubuntu-24.04` / Linux x64 | PluginHost: `SystemProcessRunnerTests`, `PluginPathValidatorTests`; Engine: `ExternalToolProcessRunnerTests` |
| `macos-15` / macOS arm64 | The Linux process classes; Engine: `SafeOutputFileSystemTests`, `DirectoryCopyFollowSymlinksTests`; Shared: `PlatformPathHelperTests`, `PathUtilsTests`, `PlatformSafeSourceFileOpenerTests`; CLI: `PreviewCommandTests`, `PreviewCommandExtendedTests`, `DevCommandTests` |
| `windows-2025` / Windows x64 | The same full class selection as macOS, exercising the Windows branches |

Process behavior and OS-specific path, link, preview and dev-server behavior need
this separate evidence without coverage instrumentation. `platform-tests` checks
the actual OS, .NET host process architecture and SDK against the matrix and
`global.json`; a runner label alone is insufficient. `TestResults/platform`
contains `platform.json`, `dotnet-info.txt` and per-project TRX files. Coverage
TRX files are beside each project's coverage output under
`TestResults/coverage/projects`.

Explicit POSIX-only or opposite-platform cases emit `BUKIT_NOT_APPLICABLE:` in
TRX test output. The platform report accepts only the declared method/OS pairs,
lists them separately and requires actual applicable execution in every selected
class. A required path/link capability failure or unexpected skipped test is a
verification gap, not a pass. The local macOS run does not establish hosted Linux,
Windows or macOS acceptance; actual target runs remain separately required.

Smoke one supported release archive or publish directory with:

```bash
bash scripts/smoke/release-artifacts.sh <archive-or-publish-dir> <rid>
```

Supported RIDs are `linux-x64`, `osx-arm64`, and `win-x64`. These checks are not
automatic substitutes for focused or aggregate validation.

## Failure reporting

Classify failures as scoped regressions, pre-existing failures, environment
restrictions, or infrastructure noise. Modify code only when evidence connects
the failure to the active diff. Report unresolved environment or infrastructure
failures precisely; do not change unrelated code to make a gate pass.
