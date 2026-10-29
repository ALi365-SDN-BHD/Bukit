# KD-004 taxonomy redirect writer boundary

User authorization: execute task 1 of `docs/plans/2026-10-28-core-residual-risk-next-steps.md` after “按计划 执行”. This batch repairs KD-004 only. Stop before the KD-003 policy decision; SCSS and stage 7 are not included.

## Decision

Validate every redirect alias and final index path before any write in one `TaxonomyRedirectWriter.WriteRedirects` call. Reject rooted/drive-qualified paths, cross-platform unsafe separators and traversal segments. Reuse `FileWriter.GetSafeFullPath` and its existing containment/symlink policy, translating path-security or invalid-path errors into `ConfigException` / `BuildOutputUnsafe`. Keep normal aliases, safe hierarchy, self/blank skips, URL/HTML content, UTF-8 encoding and generated-path tracking unchanged.

Use a local pending-write list, not a transaction framework. Input rejection must preserve existing bytes and `generatedPaths` and create no directory/file. This guarantee does not claim that the containing `TaxonomyPlugin.AfterBuild` has not already written other outputs, nor that arbitrary I/O failures or concurrent filesystem mutation are transactional.

## Scope and verification

Only the writer, its existing test class, two plan status documents and this independent OpenSpec may change. Do not add aliases ingestion, modify shared path helpers, dependencies, public contracts or CI. Current ordinary production alias reachability is unproven; this is a confirmed internal writer boundary repair, not a claim of remote exploitation.

First observe behavioral RED, then apply the smallest repair and run the complete TaxonomyRedirectWriterTests, TaxonomyEnsureTermsTests and TaxonomyPageCreatorTests, plus `git diff --check`. One specialty review and one single generated-scope delta final review; preserve all pre-existing work, no commit/push/merge or broad gates. Closure, baseline, commands, queue, logs/TRX, cache and metrics are under `/Users/ali/.local/state/bukit-verification/kd004-redirect-boundary-bh6Eqt`.
