# Script P1-P3 Hardening Design

Languages: English | [简体中文](2026-07-14-script-p1-p3-hardening-design.zh-CN.md)

## Status

Approved design baseline: Route A, complete capability repair.

## Objective

Close every P1-P3 finding from the 2026-07-14 full script audit without
preserving false-green behavior. A script may succeed only when it has direct
evidence for the contract it claims to validate.

The implementation must remain inside the active repository surfaces:

- `scripts/`
- `guide/skills/scripts/`
- `.github/workflows/release.yaml`
- focused Architecture tests and active developer documentation
- the three audited helper scripts under `.trae/skills/`

The backup/reference trees `guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, and
`scripts-0.2/` must not be changed or used as executable sources. No Bukit Core
runtime code under `src/Bukit-Core/` is in scope.

## Finding Closure Matrix

| ID | Priority | Current defect | Required end state |
|---|---|---|---|
| F1 | P1 | Security regression accepts zero matched tests | Every declared selector is represented by an executed, passing TRX result |
| F2 | P1 | Release metadata accepts duplicate, extra, or stale assets | Disk files, checksums, JSON metadata, and expected RIDs form one exact set |
| F3 | P1 | Release smoke accepts an empty directory and is absent before upload | Each final archive is extracted and its packaged CLI completes the Core smoke before upload |
| F4 | P1 | Two scanners convert search-tool failures into success | No-match is success; scanner failure is a distinct non-zero error covered by portability tests |
| F5 | P1 | Brainstorm stop helper trusts arbitrary `/tmp` paths and PIDs | Stop validates session path, owner, PID, process command, and session token before signalling or deletion |
| F6 | P2 | `build-repro.sh` and `native-aot.sh` are successful no-ops | Both perform real work and fail when their claimed build contract is not proven |
| F7 | P2 | Core CLI contract scans backup workflows | Only active scripts and `.github/workflows/` are searched |
| F8 | P2 | Native AOT packaging can retain stale publish files and interpolate PowerShell paths | Publish output is clean and guarded; archive paths cross the PowerShell boundary as data |
| F9 | P3 | Active size policy ignores Python automation | Active Shell and Python automation, including audited helpers, share the script-size policy |
| F10 | P3 | Brainstorm start helper can loop on missing option values | Strict mode and a total argument parser reject missing or malformed values immediately |
| F11 | P3 | Polluter finder splits filenames and hides failed tests | NUL-safe enumeration preserves paths and failed tests make the result inconclusive or failed, never clean |

## Architecture

The repair uses four bounded contract layers instead of one large gate:

1. **Evidence producers** run the real operation: `dotnet test`, Native AOT
   publish, archive construction, archive extraction, and CLI smoke commands.
2. **Focused validators** parse structured evidence such as TRX and release
   metadata. Parsing that is awkward or unsafe in Bash belongs in small Python
   helpers.
3. **Entrypoint self-tests** inject empty, duplicate, stale, malformed, and
   tool-failure cases. They must first fail against the old behavior and then
   pass after the implementation is changed.
4. **Workflow contracts** prove ordering and wiring structurally. Release jobs
   must smoke the archive between packaging and artifact upload.

Existing public script paths remain stable. The implementation may add focused
helpers, but it must not introduce a second release-manifest schema or a broad
replacement framework.

## F1: Security Regression Evidence

`scripts/security/security-regression.sh` keeps one `dotnet test` invocation per
test project, but each invocation writes a uniquely named TRX file into a fresh
temporary result directory. The selector list for a project is the single
source for both the VSTest filter expression and result validation.

A focused Python validator reads the TRX and proves all of the following:

- exactly one expected TRX result exists for the project;
- `total` is greater than zero;
- all discovered tests were executed and passed;
- no test is failed, skipped, or not executed;
- every declared selector matches at least one executed result by fully
  qualified class or method name.

The `dotnet test` exit code remains authoritative for test failures. TRX
validation closes the separate VSTest behavior where an empty filter can exit
zero. Temporary results are removed on exit. `BUKIT_SECURITY_SKIP_RESTORE=1`
continues to mean only `--no-restore`; it never bypasses evidence validation.

The self-test uses a fake `dotnet` executable to generate valid, zero-test,
missing-selector, missing-TRX, and failing-result fixtures without running the
real suite.

## F2: Exact Release Asset Contract

The Shell entrypoints remain:

- `scripts/release/prepare-release-assets.sh`
- `scripts/release/verify-release-assets.sh`

A focused Python helper owns canonical asset-name validation, metadata parsing,
hashing, and exact-set comparison. Preparation occurs in a fresh sibling
staging directory. Only after every input and generated metadata file validates
does the helper replace the requested output directory. The output path is
canonicalized and rejected when it is a filesystem root, the repository root,
`.` or `..`, or crosses a symlinked parent boundary.

Preparation rejects:

- missing inputs, non-regular files, or symlinks;
- duplicate source paths or duplicate basenames;
- names containing separators, `.`/`..`, control characters, or reserved
  metadata names;
- archive names that do not match `bukit-<version>-<known-rid>` with the
  platform-correct extension.

Verification treats these four representations as a bijection:

1. expected RID-derived archive names;
2. regular top-level archive files on disk;
3. `release-manifest.json` and `checksums.json` asset objects;
4. strict `checksums.txt` records.

All names must be unique. JSON objects have exact required keys and types;
SHA-256 values are lowercase 64-character hex strings; byte sizes are
non-negative integers; checksum lines have exactly one digest and one safe
basename. Nested paths, symlinks, unlisted files, extra checksum lines, and
duplicate expected RIDs fail.

The release workflow always passes the selected RID set to verification, not
only for a published release. `all` expands to the three supported RIDs.

## F3: Smoke the Final Archive

`scripts/smoke/release-artifacts.sh` changes from a directory-existence probe
into an artifact execution gate. Its exact interface is
`release-artifacts.sh <archive-or-publish-dir> <rid>`. It accepts either an
unpacked publish directory or the final `.tar.gz`/`.zip` archive for local
compatibility, while the release workflow always passes the final archive.

For an archive, a focused extractor validates member paths before extraction:

- no absolute path;
- no `..` traversal;
- no member escaping the scratch root after normalization;
- no archive type or filename inconsistent with the RID.

The gate locates exactly one expected executable (`bukit` or `bukit.exe`),
copies `tests/fixtures/basic-markdown-site` into an isolated scratch directory,
and invokes `scripts/smoke/core.sh` with explicit `BUKIT_BIN`, config, and output
paths. Success therefore proves `config check`, clean build, and publish audit
using the packaged binary. Empty archives, missing executables, duplicate
executables, extraction errors, or any CLI failure are blockers.

`package-native-aot.sh` writes both `archive` and `publish_dir` outputs for
diagnostics. Each platform package job adds a named smoke step immediately
after packaging and before `actions/upload-artifact`. Architecture tests parse
the workflow YAML and assert this ordering and archive-output data flow.

The stale example in `scripts/smoke/core.sh` is corrected to the tracked basic
fixture.

## F6 and F8: Real Native AOT and Reproducibility

### Package hygiene

`scripts/build/package-native-aot.sh` canonicalizes the output root, rejects a
symlinked publish parent, and recreates only the derived
`<output-root>/publish/<rid>` directory after proving it is a strict descendant
of the output root. Existing archives for the same version and RID are removed
before writing. A failed publish or archive operation cannot be mistaken for a
fresh output.

For Windows, the destination path is passed through an environment variable or
positional PowerShell argument. It is never interpolated into PowerShell source
text. ZIP creation includes the complete publish directory and fails if the
result is missing or empty. The apostrophe-path regression is covered with a
fake PowerShell executable.

### Native AOT compatibility entrypoint

`scripts/build/native-aot.sh` becomes a strict compatibility entrypoint for
the canonical package script. Its exact interface is
`native-aot.sh <version> <rid> <output-root> [configuration]`; configuration
defaults to `Release`, while the other three values are mandatory. It prints
the delegated command context and returns the package script's status and
archive path. Missing arguments or an unsupported RID exit with usage status 2.

### Deterministic clean-twice proof

`scripts/build/build-repro.sh` has the exact interface
`build-repro.sh <version> <rid> [configuration]`, with configuration defaulting
to `Release`. It performs two isolated Native AOT package runs for the same
version, current commit, RID, configuration, and deterministic build
properties. It compares the expanded publish trees, not timestamp-bearing
archive container bytes. The comparison covers the exact relative file set,
file type, size, and SHA-256 digest for every regular file. Symlinks and special
files fail the proof.

On mismatch, the script prints missing, extra, and changed relative paths and
returns non-zero. It does not downgrade a toolchain or Native AOT
non-determinism into a warning. Its temporary roots are always cleaned.

Self-tests use fake `dotnet`, archive, and PowerShell commands to prove stale
publish files are removed, hostile output paths are rejected, apostrophes stay
data, and identical/different clean builds are classified correctly. Final
verification also performs the real reproducibility command for the current
host RID.

## F4, F7, and F9: Active Scanner Contracts

`scripts/checks/active-workflow-boundary.sh` and
`guide/skills/scripts/validate-skills-strict.sh` use the repository's standard
grep status pattern:

- status 0: matches found, evaluate as a contract violation;
- status 1: no matches, continue successfully;
- status greater than 1: print a specific `text search failed` error and return
  the tool's non-zero status.

The skills check no longer requires ripgrep. Both scripts are added to
`ci-fast-portability-self-test.sh` for the no-ripgrep and injected-grep-failure
cases.

`scripts/checks/core-cli-contract.sh` searches only `scripts/` and
`.github/workflows/`. The backup workflow tree is neither searched nor added to
an exclusion list that could conceal future scope drift.

`scripts/checks/docs/size-policy.sh` enumerates `.sh` and `.py` automation from
`scripts/`, `guide/skills/scripts/`, and the audited `.trae/skills` helper
surface. All use the existing 200-line script limit. Documentation retains the
existing 1000-line limit. The gate must report every violation in one run.

## F5 and F10: Brainstorm Server Lifecycle Safety

`start-server.sh` gains `set -euo pipefail`, a `require_value` parser helper,
and immediate rejection of missing values, unknown flags, conflicting
foreground/background flags, newline-containing paths, and empty host values.
Session state is created with a restrictive umask.

Every started server records separate, non-sourceable state files for:

- numeric PID;
- current numeric UID;
- canonical `server.cjs` path;
- a safe per-session token also present in the Node process argument list.

Foreground mode uses `exec` so the recorded PID is the Node process. Background
mode invokes the same absolute server path and token. Startup failure removes
only the fresh session it created.

`stop-server.sh` canonicalizes the supplied directory and validates all state
fields before any signal. It compares the PID owner from `ps`, the expected
server path, and the session token against the live process command. Missing,
malformed, stale, reused, foreign, or mismatched state returns an error without
calling `kill` or deleting files.

Recursive deletion is allowed only for a canonical direct child of `/tmp`
whose basename matches the exact generated `brainstorm-<pid>-<time>-<random>`
grammar. Persistent `.superpowers/brainstorm/` sessions are retained after a
valid stop. SIGTERM is attempted first; SIGKILL is allowed only after the same
validated process remains alive through the grace period.

A fast auxiliary self-test covers missing option values, flag conflicts,
malformed state, arbitrary `/tmp` paths, PID identity mismatch, valid stop, and
ephemeral-versus-persistent cleanup without starting the real server.

## F11: Reliable Polluter Search

`find-polluter.sh` uses NUL-delimited `find` output and a Bash array, preserving
spaces, tabs, glob characters, and newlines in test filenames. A pre-existing
pollution target and a pattern matching zero tests both fail before test
execution.

Each test runs as `npm test -- <exact-path>` with output captured to a temporary
log. Classification order is:

1. if pollution appears, report the exact test and its command status, then
   return the polluter-found status;
2. if no pollution appears but the test command failed, record the failure and
   continue looking for a polluter;
3. if no polluter is found but any command failed, return an inconclusive
   non-zero result and list failed tests;
4. only an all-green, pollution-free run prints `No polluter found` and exits
   zero.

The auxiliary self-test includes a filename containing spaces, a polluting
test, a failing non-polluting test, zero matches, and a completely clean run.

## Error Handling and Output Rules

- Usage and malformed caller input return 2.
- Contract violations, failed operations, unsafe paths, and mismatched evidence
  return 1 unless an underlying tool has a more specific non-zero status worth
  preserving.
- Tool errors must be named as tool errors; they must not be formatted as a
  normal contract mismatch.
- All temporary directories use a Bukit-specific prefix and are removed by a
  trap.
- Diagnostic output names the project, RID, archive, selector, or relative file
  that failed. Secrets and arbitrary file contents are not printed.
- No `|| true` may surround an evidence-producing or validating command. A
  best-effort cleanup may ignore an error only after the primary status is
  preserved.

## Test and Delivery Strategy

Implementation is one parent task with ordered, independently verified
subtasks:

1. Security TRX evidence and its self-test.
2. Exact release preparation/verification and its self-test.
3. Clean AOT packaging, safe PowerShell transport, real Native AOT and
   reproducibility entrypoints, and their self-tests.
4. Final-archive smoke plus release-workflow and Architecture contracts.
5. Scanner failure classification, active scope, and Python size policy.
6. Brainstorm lifecycle safety and its auxiliary self-test.
7. Polluter classification and its auxiliary self-test.
8. Active documentation synchronization and aggregate audit.

For each subtask, the red test or injected failure must be observed before the
production change. After it turns green, run:

```bash
bash scripts/checks/post-change-targeted.sh -- <that-subtask-paths>
```

Additional owning checks are required where applicable:

- real `scripts/security/security-regression.sh Release` for F1;
- release asset, package, smoke, portability, and auxiliary self-tests;
- targeted `Bukit.Architecture.Tests` for release workflow changes;
- one real host-RID `build-repro.sh` invocation for F6;
- `bash -n` for every changed Shell script and Python compilation for every
  changed Python helper;
- `git diff --check` and an explicit backup-tree scope check.

CI/release/gate edits are high risk. Because no sub-agent was explicitly
requested, the main thread performs the required immediate bounded read-only
audit after each such subtask and one consolidated parent-task audit at the
end. The audit maps every F1-F11 requirement to direct current-state evidence
and checks for unrelated changes.

The task does not run `ci-full`, `scripts/gates/release.sh`, `test-all`,
`smoke-all`, or whole-solution `.slnx` tests unless the user separately requests
that broader proof.

## Success Criteria

The parent task is complete only when:

- all F1-F11 negative cases fail for the intended reason;
- all positive self-tests and owning targeted gates pass;
- the real security regression contains non-zero executed evidence for every
  selector;
- a real host Native AOT build is reproducible across two clean roots;
- workflow structure proves each uploaded archive was smoked first;
- no active scanner depends on ripgrep or hides scanner failure;
- no audited helper can kill an unverified process, delete an arbitrary
  temporary directory, split a test path, or report clean after test failure;
- no backup/reference file is modified;
- the final diff contains only files needed to close F1-F11.
