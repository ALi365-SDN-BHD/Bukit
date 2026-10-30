# SCSS Process Observability Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fill the SCSS process-attempt observation gap without claiming to repair the unexplained historical missing-log failure.

**Architecture:** Add one private observation boundary in `ScssCompiler`, calling the unchanged `ExternalToolProcessRunner`. Existing `ILogger.Debug` receives correlated start/end events; candidate names and phases are fixed, returned exit codes and elapsed call-boundary milliseconds are real. User approved full stderr suppression: record only empty/redacted/unavailable and returned diagnostic-string UTF-16 character count; no content, hashes, environment values, arguments or paths.

**Tech Stack:** C#/.NET 10, existing ILogger/Stopwatch/Guid, xUnit, original coverlet settings and codex-workflow helpers. Existing isolated worktree; no commits or dependencies.

---

## Authorization and scope

On 2026-10-29 the user explicitly chose SCSS production-observation follow-up, then approved full stderr masking. This is a separate visible Core observation task, not reopening completed stage-6 audit repairs. Do not change timeouts (probe 3s, compile 5s), candidate order, compilation/output checks, cancellation, old warning/Info payloads, runner/cleanup logic, public API/schema, CLI policy or CI. The existing compile-failure warning still carries its old stderr detail; the new masking guarantee applies to the new Debug events only. Additional production repair still needs its own authorization.

Evidence root R: `/Users/ali/.local/state/bukit-verification/scss-process-observation-Ycjxhu`. Ten allowed paths are frozen/mapped in R/snapshot.json and closure.json; no unmapped paths. Main agent is the sole writer. Preserve all 73 existing changed files at their current task-start bytes except the explicitly scoped edits.

## Task 1: Behavioral observation RED

Files: `tests/Bukit.Engine.Tests/ExternalToolProcessRunnerTests.cs`, the original SCSS method only in `SiteEngineIntegrationTests.cs`; three OpenSpec files under `openspec/changes/archive/2026-10-30-observe-scss-process-attempts/`; this plan, two existing plan/status docs and `guide/user/14-troubleshooting.md`.

1. Add nine real fake-tool cases to the existing runner-test class: success, nonzero-probe fallback, both probes rejected, compile failure, exit-zero missing output, probe/compile timeout and probe/compile cancellation. Record actual shell branches independently. Cancellation follows a real branch-entry marker, not a scheduling guess; cancel and await the operation before assertions/cleanup.
2. Retain old CSS/source/outcome behavior and assert paired start/end attempt IDs, fixed phase/candidate, actual exits or unavailable, nonnegative elapsed, original timeout parameters, fully suppressed stderr and exact returned diagnostic-character counts. An additional logger-sink-failure control ensures supplemental Debug writes cannot change a successful tool result. Never treat a controlled injected timeout as reproduction of the historical defect.
3. Preserve the original real multilingual fixture, negative retention self-check and all source/compile-once/CSS assertions. Require two returned process-end records in its retained Debug snapshot; update its explicit observation gaps without inventing physical executable or OS-scheduling measurements.
4. Run the exact RED specialty below against unchanged `ScssCompiler`. Read logs/TRX; expected failures must be missing observation events after old behavior controls, not compilation/fixture/environment errors. No production code before that RED.

## Task 2: Minimum private observation boundary

File: `src/Bukit-Core/Bukit.Engine.Assets/ScssCompiler.cs` only. Read-only consumers: `AssetSourceWorkspace`, `SiteEngine`, `I18nWorktreeBuild`, existing runner, ILogger/build diagnostic consumers.

Replace only the two runner calls with one private `RunSassProcessAsync(phase, tool, startInfo, timeout, logger, cancellationToken)`. It emits:

```text
event=scss.tool.start attempt=<Guid-N> phase=probe|compile tool=sass|dart-sass timeout_ms=3000|5000
event=scss.tool.end attempt=<same> phase=<same> tool=<same> outcome=exit|timeout|cancelled|error exit_code=<returned-int>|unavailable elapsed_ms=<integer> stderr=empty|redacted|unavailable stderr_chars=<returned-string-length>|unavailable error=none|<exception-type>
```

Start the stopwatch immediately around the unchanged runner invocation. Keep its returned result untouched; on an exception classify its type then `throw;`, and emit the end record in `finally`. Supplemental start/end Debug writes are best-effort, so a failing diagnostic sink does not replace tool results/cancellation; existing other logging behavior stays unchanged. No exception messages, stderr text, stdout, args, environment or file paths enter these new events. Returned nonzero exits remain `outcome=exit`; exit 0 does not prove CSS exists. A thrown attempt has no exposed result: do not label its stderr empty or fabricate an exit code. Elapsed includes wrapper/group setup, scheduling and cleanup, not isolated Sass CPU time or proof of a timeout cause.

Run the exact GREEN specialty after the minimal implementation; inspect its own TRX/coverage and actual retained process observations. Record/cache matching HEAD, closure bytes, command, environment states and SDK; don't refresh unchanged fixtures with extra runs.

## Task 3: Scoped review and honest closeout

Update the user troubleshooting guide for non-CI `logging.level: debug` (CI CLI retains its existing Warn minimum), field meanings, full masking and remaining gaps. Backfill this task only in the two plan records and its OpenSpec; retain earlier diagnosis as historical. Run `git diff --check`, protect outside bytes, accept one specialty and one generated-scope delta-only final review, release writer and report metrics. Only Critical/Important findings trigger a scoped repair/re-review. Keep the worktree and all uncommitted work; no merge, push, archive or broad gate.

## Exact specialty commands

RED (GREEN is identical except results directory R/green):

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --settings coverage.runsettings --collect "XPlat Code Coverage" --filter "FullyQualifiedName~ExternalToolProcessRunnerTests|FullyQualifiedName~LibraryDiagnosticOutputTests|FullyQualifiedName~SiteEngineIntegrationTests.BuildAsync_ScssEnabled_PreservesSourceAndCompilesOnceAcrossLanguages" --logger "trx;LogFileName=tests.trx" --results-directory /Users/ali/.local/state/bukit-verification/scss-process-observation-Ycjxhu/red
git diff --check
```

No full Engine, whole solution, stability loop, Linux/Windows execution, full/release gates or stage 7. A passing observation feature is not a historical root-cause fix.

## Execution evidence

- `R/red/tests.trx`: 10 expected missing-event failures / 93 passed / 0 Skip, actual exit 1, ScssCompiler exact task-start bytes. All legacy-outcome and actual multilingual CSS/trace assertions preceded the failed observation assertions. This is observation-feature RED, not historical missing-log reproduction.
- `R/green/tests.trx`: 104 passed / 0 failed / 0 Skip, actual exit 0. One Windows-only NOT_APPLICABLE on macOS is not Windows proof. Counts are 29 runner-test results (including ten new cases), 74 exact diagnostic-payload controls and one real multilingual test. The additional failing-Debug-sink control accounts for the one-case increase after RED.
- Real retained Debug capture: probe and compile actual returned exit code 0, call-boundary elapsed 163 ms / 26 ms; total build/assertions 521 ms, source unchanged and all three CSS exact. Controlled timeout/cancellation cases are not reproduction of the historical fault. `R/green-analysis.json` and `R/captures/green/` retain provenance and remaining observation limits; coverage attachments belong to this exact command, not global coverage proof.
- Runtime GREEN cached and checked HIT with matching HEAD/closure bytes/exact command/environment-state/SDK. Specialty review `R/reviews/specialty-v1.md` and single generated-scope delta-only final review `R/reviews/final-delta-v1.md` each accepted with Critical 0 / Important 0 / Minor 0; no repair or rerun. Final static/protection/cache, writer release and metrics results are recorded in `R/completion.json`; no fixture refresh or broader gate.
- **SUCCESS for this observation-feature scope only.** Historical missing-log failure remains unreproduced and unexplained. Raw stderr is deliberately suppressed; thrown-process results, physical executable resolution and OS scheduling remain unobserved. Existing legacy warning privacy is unchanged. Stop here unless another bounded investigation/repair is explicitly authorized; no automatic stage 7.
