# SCSS diagnostic evidence

## Requirement: Distinguish probe, compile and output observation

The existing fake Sass SHALL trace `--version` and compile branches separately using a fixed, non-sensitive fixture-tool marker. `sass.log` SHALL remain compile-only. Its original one-compile, unchanged-source and exact three-language CSS assertions MUST remain strict. Production timeout and process/compilation semantics SHALL not change.

## Requirement: Preserve evidence before fixture cleanup

The test SHALL retain available traces, redacted logger debug/warning/error messages, expected/actual CSS state and observable elapsed time outside the fixture before cleanup, including on an exception or assertion failure. Environment evidence SHALL contain only SET/EMPTY/UNSET states and match booleans, not raw PATH, values or credentials. The same capture path SHALL have an assert-based failure self-check that proves copying/redaction and missing-output recording without another process or build. Capture errors SHALL not silently discard an existing original failure.

## Requirement: Bound conclusions and verification

Execute the approved coverage-enabled specialty once, not a stability loop or full Engine run. Stub branch-exit markers SHALL not be misrepresented as observed wrapper-process exit results. Missing process stderr/individual elapsed time/scheduling observations SHALL be identified explicitly. A non-reproduction SHALL leave the historical root cause unknown; further production instrumentation or repair requires separate authorization. Do not reopen stage 6, enter stage 7 or claim cross-platform success.
