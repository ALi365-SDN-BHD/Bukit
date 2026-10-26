# Unify library logging

## Why

Twenty-three Core library diagnostic sites bypass the caller's `ILogger`, losing
the selected format, filtering, and build diagnostic counts.

## Approved change

- Route those sites through the existing caller-owned logger; preserve the twenty
  existing warning texts and accept `[warn] ` on security and both termination warnings.
- Add only `ConfigLoader.Load(string, ILogger)`, `BuildManifest.Load(string, ILogger)`,
  and `SystemProcessRunner(ILogger)` publicly, retaining every old entry point.
- Parse command configuration once, replay startup diagnostics once in order, and
  explicitly carry the global format through command and plugin startup.
- Preserve build counting intervals, report schema, public page bytes, process
  behavior, safety validation, fallback behavior, and cache algorithms.
- Add a semantic architecture guard and focused owner regressions.

## Boundaries

Base: `e20deee3e65bd0fca53d9bb0e4d68fe8c0ca95b7`, after the independently
accepted Theme/Labs commits. No Theme/Section work, logging framework, structured
fields API, dependencies in runtime projects, historical fixture replay, full gate,
automatic commit, push, or merge. Report counts and their derived hashes may change.
