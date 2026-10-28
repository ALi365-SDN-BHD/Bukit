# Bounded SCSS missing-log diagnosis

On 2026-10-29, after KD-003 option A completed, the user requested continuing the approved residual-risk plan. Execute task 3 only: collect causal evidence for the historical missing `sass.log`; do not claim that a later passing run fixes the historical failure or authorizes stage 7.

## Approved design and smallest scope

Modify only the original `BuildAsync_ScssEnabled_PreservesSourceAndCompilesOnceAcrossLanguages` test and its two plan/status documents, plus this separate OpenSpec. Reuse its fake Unix Sass, logger and fixtures. Add separate probe and compile traces, preserving `sass.log` as compile-only. Save traces and redacted debug/warning/error messages, source/CSS status and observable timing before fixture cleanup on success or failure. Use a test-only evidence destination, consistent with existing evidence sinks; record environment SET/EMPTY/UNSET states and fixed fixture tool identity, never raw PATH or environment values.

A local failure-retention self-check deliberately throws without starting a process/build, verifies copied sentinel traces and failure/redaction/missing-CSS metadata, then removes only its own synthetic inputs. The same capture path surrounds the sole real build and all original assertions; a capture failure preserves the original failure as well. Do not add shared test infrastructure, a production hook, dependency or configuration framework. `ExternalToolProcessRunnerTests` already tests captured process exit/stdout/stderr and needs no change.

## Verification and boundaries

Run exactly one planned Release specialty with `coverage.runsettings`, XPlat Code Coverage and filter `ExternalToolProcessRunnerTests|SiteEngineIntegrationTests.BuildAsync_ScssEnabled_PreservesSourceAndCompilesOnceAcrossLanguages`, plus `git diff --check`. Preserve all source SCSS, compile-once and three-language CSS assertions and the production 3-second probe / 5-second compile timeouts. Production files remain read-only. Accepted tests are followed by one specialty and one generated-scope delta-only final review. No full Engine/whole-solution/release gate, stability loop, cross-platform claim, commit, push or merge.

The shell trace records self-reported branch entry/exit, not the wrapper process result. Successful probe/compile process exit/stderr/individual elapsed time and scheduling latency are not exposed by the current compiler interface and must remain explicitly unobserved. If this bounded run does not reproduce the failure, record "root cause still unknown"; further production observation or a repair requires a new scope and authorization. Evidence root: `/Users/ali/.local/state/bukit-verification/scss-causal-diagnosis-z6Jmp9`.
