# Core Stage 6 Audit Repairs Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Repair the authorized GEO diagnostic and public-text projection counterexamples, and correct the related evidence without expanding Core policy.

**Architecture:** Preserve incomplete FAQ/HowTo items in the existing models until diagnostics; emit only complete JSON-LD items. Keep the pinned DOM parser, script guard and malformed-anchor policy while isolating inert template scope and normalizing literal raw-text containers. Strengthen existing fixture/plugin tests rather than introduce another harness or production policy.

**Tech Stack:** .NET 10, xUnit, Markdig, repository-pinned AngleSharp 1.5.2, coverlet/Cobertura, existing codex-workflow evidence helpers.

---

The user approved execution in the existing isolated worktree. No commit or new worktree is authorized. `openspec/changes/archive/2026-10-30-complete-core-stage6-validation/tasks.md` remains the sole checkbox ledger. Evidence root: `/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-repairs-APNcZD` (R). All 21 allowed paths and their consumers are mapped in R/closure.json, R/runtime-closure.json and R/docs-closure.json; no unmapped path may be added without controller approval.

## 1. GEO parser boundary (I-01)

Files: `src/Bukit-Core/Bukit.Engine/SeoGeoMetaParser.cs`, `SeoJsonLdBuilder.cs`; connected `SeoDiagnostics.cs`/`SeoModelBuilder.cs` only if necessary. Tests: `tests/Bukit.Engine.Tests/GeoDiagnosticsTests.cs`, `GeoSeoModelBuilderTests.cs`.

1. Read all parser/model/JSON-LD/diagnostic callers, including `CanonicalContentGraphBuilder`'s schema/date/citation use.
2. Add raw maps → `ContentDocument` → `BuildForContent` → `AnalyzeIndex` regressions for each missing required field, non-map entries, mixed/all-invalid lists, and warn/strict/off; retain missing/empty controls.
3. Run `geo-red` below and inspect assertion failures before production edits.
4. Retain incomplete entries as existing models with empty required strings; filter only emission, omit empty specialized nodes and preserve valid step positions. No public model declarations change.
5. Run `geo-green`; cache exact successful evidence, preserving the RED log.

## 2. Public projection context (I-02/I-03/M-01/M-04)

Files: `src/Bukit-Core/Bukit.Engine/HtmlTextProjection.cs`, `MarkdownBodyProjection.cs` only if needed. Tests: `tests/Bukit.Engine.Tests/MarkdownBodyProjectionTests.cs`, `SearchIndexBuilderTests.cs`, `SiteEngineBodyProjectionTests.cs`.

1. Trace search, document Markdown and both llms-full callers. Inspect pinned parser source if callback context is required.
2. Add exact outer-anchor/template, noscript literal-link and raw-text word-boundary regressions plus nested/unclosed/foreign/self-closing controls. Independently exercise all three real-site outputs with the existing helper; inspect actual Markdig `LinkInline` targets. Remove only the inherited redundant brace block.
3. Run `projection-red`; preserve failures before production edits.
4. Apply the smallest context-correct shared repair. Keep script escape guard, AngleSharp version, unsafe-URI checks, entity/Unicode behavior and innermost-explicitly-closed malformed-anchor rule.
5. Run `projection-green` and cache its exact successful evidence.

## 3. Evidence-only plugin corrections (M-02/M-03)

Files: `tests/Bukit.Engine.Tests/RelatedContentPluginTests.cs`, `ArchivePluginTests.cs`.

1. Give the disabled related-content case two matching documents and explicit configuration; assert actual `BuildDataKeys.RelatedPages` absence/emptiness for disabled and single-item cases.
2. Rename/correct the archive test to prove its actual blank-collection boundary; point the matrix no-output column to the existing empty-content test. Do not change either production plugin or invent a missing-date rule.

## 4. Complete affected specialty, then honest closeout

Files: this plan, `docs/plans/bukit-core-automated-test-plan-2026-10-24.zh-CN.md`, `docs/testing/core-stage6-behavior-matrix.md`, `guide/user/17-geo.md`, and proposal/design/tasks/spec under `openspec/changes/archive/2026-10-30-complete-core-stage6-validation/`.

1. Reopen existing tasks 3/4/5 and mark audit-repair-in-progress before implementation, retaining old evidence as history.
2. Once runtime/tests stabilize, run `engine` exactly once unless a diagnosed real failure requires a justified rerun. Engine commands are fixture-exclusive: no repository edits during execution.
3. Inspect raw TRX outcomes and its own Cobertura attachment; merge unique Engine source-path+line coverage and require ≥70%. Keep inherited KD-003/004 skips notPassed.
4. Cache successful exact commands using runtime-closure paths, HEAD, SDK and only environment names from R/cache-environment-names.json. Freeze runtime hashes after the full successful run; do not repeat stale focused fixtures just to refresh cache.
5. Update actual semantics, method-reference counts and results. Earlier CLI 100/Host 26/Console 21/Architecture 48 remain historical unchanged-area evidence, not current-closure HITs. Mark tasks 3/4 only with proof; leave 5/unconditional COMPLETE pending controller-owned specialty and delta-only final review.
6. Run `static`, record report/delta/input-preservation receipts and real phase metrics under R, then retain writer task in `review_wait`. No subagent or duplicate review dispatch.

## Exact scheduled commands

`geo-red`:
```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~GeoSeoModelBuilderTests|FullyQualifiedName~GeoDiagnosticsTests|FullyQualifiedName~SeoDiagnosticsTests|FullyQualifiedName~SeoPublisherJsonLdTests" --logger "trx;LogFileName=tests.trx" --results-directory /private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-repairs-APNcZD/geo-red
```

`geo-green`:
```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~GeoSeoModelBuilderTests|FullyQualifiedName~GeoDiagnosticsTests|FullyQualifiedName~SeoDiagnosticsTests|FullyQualifiedName~SeoPublisherJsonLdTests" --logger "trx;LogFileName=tests.trx" --results-directory /private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-repairs-APNcZD/geo-green
```

`projection-red`:
```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~SearchIndexBuilderTests|FullyQualifiedName~MarkdownBodyProjectionTests|FullyQualifiedName~BodyProjection_|FullyQualifiedName~LlmsProjectionIntegrationTests" --logger "trx;LogFileName=tests.trx" --results-directory /private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-repairs-APNcZD/projection-red
```

`projection-green`:
```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~SearchIndexBuilderTests|FullyQualifiedName~MarkdownBodyProjectionTests|FullyQualifiedName~BodyProjection_|FullyQualifiedName~LlmsProjectionIntegrationTests" --logger "trx;LogFileName=tests.trx" --results-directory /private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-repairs-APNcZD/projection-green
```

`engine`:
```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --settings coverage.runsettings --collect "XPlat Code Coverage" --logger "trx;LogFileName=engine.trx" --results-directory /private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-repairs-APNcZD/engine
```

`static`:
```sh
git diff --check
```
