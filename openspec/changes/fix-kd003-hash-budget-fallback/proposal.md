# KD-003: disable variant incremental reuse when template hashing exceeds budget

The user confirmed option A after task 1 (KD-004) completed. This is a separate Core incremental-correctness task, not authorization for SCSS, stage 7, broad gates, commits or changes to existing completed repairs.

## Approved behavior

Keep the directory hash defaults (10,000 files / 100 MiB content). Never return a normal reusable digest when some file content was omitted. Signal that condition internally; the variant manifest planner handles only this budget signal, warns, and uses its existing nonincremental branch so the build continues with full rendering. Do not catch ordinary I/O, permission or unrelated invalid-operation errors as budget fallback. Keep public/serialized manifest shapes, ownership and non-render state unchanged.

Reuse `DirectoryHashCache` without changing it: the existing ConcurrentDictionary does not cache factory exceptions. Use the existing `IncrementalEnabled=false`, empty entries/hash and null concurrent entries rather than introduce a result framework, option or public test hook. Preserve complete under-budget digest bytes/determinism and existing symlink discovery rules.

## Scope and proof

Only HashUtil, VariantManifestPlanner, four existing test classes, two plan status documents, troubleshooting guidance and this OpenSpec may change. Observe new-policy behavioral RED before production changes. Retain original KD-003 same-length inputs/history, but replace the old 'different digest above budget' expectation with 'no incomplete reusable digest'. Compare real Scriban page and list output after same-length, same-mtime template edits against nonincremental output, using tiny budgets across child/parent/user layouts. Prove ordinary reuse and recovery still work and operational errors are not swallowed.

Run only the six full classes listed in the approved plan (HashUtilTests, DirectoryHashCacheTests, LibraryDiagnosticOutputTests, RenderPipelineTests, IncrementalBuildEngineTests, IncrementalBuildEngineAsyncTests), plus `git diff --check`, one specialty review and one generated-scope delta-only final review. Preserve all current worktree edits. Evidence root: `/Users/ali/.local/state/bukit-verification/kd003-budget-fallback-SkfsyV`.
