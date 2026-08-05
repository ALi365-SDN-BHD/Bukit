# Bukit LLMS Curation Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为每个内容页面增加向后兼容的 llms 策展规则，使站点能够显式包含、排除、降为 Optional 或稳定排序页面，而不改变 robots/sitemap/search 资格。

**Architecture:** 内容级合同放在既有 `geo` 元数据下的 `geo.llms`，由独立 parser 产生 `LlmsCurationPolicy`。llms 投影先执行不可覆盖的 indexability 边界，再处理 visibility/tier/priority；`llms.txt` 生成策展目录，`llms-full.txt` 使用同一 exclude 决策但保留全文用途。

**Tech Stack:** .NET 10, Bukit built-in llms plugin, content field maps, deterministic projection ordering, xUnit, active GEO guide.

## Global Constraints

- WP1-A 必须已完成并集成。
- 内容合同固定为 `geo.llms.visibility|tier|priority`；不得新增站点路径正则、标签特例或分页字符串判断。
- `visibility`: `auto|include|exclude`，默认 `auto`。
- `tier`: `primary|optional`，默认 `primary`。
- `priority`: `-100..100` 的整数，默认 `0`；只影响 Bukit 内部排序。
- 非 indexable 页面永远排除；`include` 不能覆盖 `noindex`。
- `exclude` 同时排除 `llms.txt` 和 `llms-full.txt`。
- `include` 绕过 `llmsTxtMaxArticles` 的 auto 数量限制；auto 项填充到限制。显式 include 数量可以使最终组超过限制，这是用户明确策展的结果。
- `llmsTxtMaxArticles: 0` 仍表示 auto 项无限制。
- `optional` 页面进入唯一 `## Optional` 区；现有 `llmsTxtOptionalLinks` 追加在同一区并按配置顺序保留。
- 不修改 sitemap、search、RSS、robots 或 `SeoIndexEntry.Indexable`。
- 未知字段/枚举/越界 priority 在现有 warn 模式报告，在 strict 模式失败关闭。

---

## Verification Closure Command

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed src/Bukit-Core/Bukit.Engine/LlmsCurationPolicy.cs \
  --changed src/Bukit-Core/Bukit.Engine/SeoDiagnostics.cs \
  --changed src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs \
  --changed src/Bukit-Core/Bukit.Engine/MachineReadabilityTrustAuditBuilder.Core.cs \
  --changed tests/Bukit.Engine.Tests/LlmsCurationPolicyTests.cs \
  --changed tests/Bukit.Engine.Tests/GeoDiagnosticsTests.cs \
  --changed tests/Bukit.Engine.Tests/LlmsTxtPluginTests.cs \
  --changed tests/Bukit.Engine.Tests/SeoAuditReportWriterTests.cs \
  --changed guide/user/17-geo.md \
  --changed tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
```

Expected: `unmappedFiles: []`; Engine and Architecture commands run serially.

### Task 1: Parse and validate page-level curation metadata

**Files:**
- Create: `src/Bukit-Core/Bukit.Engine/LlmsCurationPolicy.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/SeoDiagnostics.cs`
- Create: `tests/Bukit.Engine.Tests/LlmsCurationPolicyTests.cs`
- Modify: `tests/Bukit.Engine.Tests/GeoDiagnosticsTests.cs`

**Interfaces:**
- Produces: `LlmsVisibility`, `LlmsTier`, `LlmsCurationPolicy`, `LlmsCurationParseResult`, `LlmsCurationPolicyParser.Parse(ContentDocument)`.
- Consumes: nested `geo.llms` content fields.

- [ ] **Step 1: Generate closure**

Pass all four Task 1 paths plus the later plugin/tests/docs paths to closure before any edit. Expected: `unmappedFiles: []`.

- [ ] **Step 2: Write parser RED tests**

Cover omitted metadata, each enum, priority boundaries `-100` and `100`, unknown field, unknown enum, non-integer priority and values `-101`/`101`.

Use the exact result types:

```csharp
internal enum LlmsVisibility { Auto, Include, Exclude }
internal enum LlmsTier { Primary, Optional }
internal sealed record LlmsCurationPolicy(
    LlmsVisibility Visibility,
    LlmsTier Tier,
    int Priority)
{
    internal static readonly LlmsCurationPolicy Default =
        new(LlmsVisibility.Auto, LlmsTier.Primary, 0);
}

internal sealed record LlmsCurationParseResult(
    bool Valid,
    LlmsCurationPolicy Policy,
    IReadOnlyList<string> ErrorCodes);
```

- [ ] **Step 3: Run Engine tests and confirm RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

- [ ] **Step 4: Implement the parser without changing `SeoModel`**

Read the nested maps directly from `ContentDocument.CustomFields`. Return a
valid `Default` result when `geo` or `llms` is absent. For invalid fields,
return `Valid: false`, `Policy: Default`, and all fixed error codes;
`SeoDiagnostics` applies the configured warn/strict behavior, while the llms
selector treats every invalid result as excluded. Invalid metadata therefore
cannot silently become an auto-included page.

- [ ] **Step 5: Add fixed diagnostics**

Use these codes:

```text
geo.llms_visibility_invalid
geo.llms_tier_invalid
geo.llms_priority_invalid
geo.llms_field_unknown
```

Run Engine tests to GREEN.

### Task 2: Apply curation to compact and full llms projections

**Files:**
- Modify: `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs`
- Modify: `tests/Bukit.Engine.Tests/LlmsTxtPluginTests.cs`

**Interfaces:**
- Consumes: `LlmsCurationPolicy` per routed document and existing `SeoIndexEntry.Indexable`.
- Produces: deterministic primary/optional selections shared by sync and async full writers.

- [ ] **Step 1: Write selection RED tests**

Cover all of these independently:

- non-indexable + include remains excluded;
- explicit exclude disappears from both files;
- explicit include survives a per-collection max of `1`;
- auto fills remaining max slots after explicit includes;
- priority sorts descending, then publish date descending, then canonical URL ordinal;
- optional pages appear only in the single Optional section of compact output;
- full output includes primary and optional, but not exclude;
- omitted metadata produces byte-for-byte current ordering for an unchanged fixture;
- repeated generation produces identical bytes.
- invalid curation metadata is absent from both files in warn mode and causes the existing strict diagnostics failure in strict mode.

- [ ] **Step 2: Run Engine tests and confirm RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

- [ ] **Step 3: Introduce one selection record and one selector**

```csharp
internal sealed record LlmsCandidate(
    ContentDocument Document,
    ContentRecord Record,
    SeoIndexEntry Entry,
    SeoModel? Model,
    LlmsCurationPolicy Policy);

internal sealed record LlmsSelection(
    IReadOnlyList<LlmsCandidate> Primary,
    IReadOnlyList<LlmsCandidate> Optional,
    IReadOnlyList<LlmsCandidate> Excluded);
```

Create one pure selector used by `WriteLlmsTxt` and both full-writer paths. Do not duplicate visibility logic in three loops.

- [ ] **Step 4: Enforce precedence explicitly**

```text
entry.Indexable == false -> excluded
parseResult.Valid == false -> excluded
visibility == exclude     -> excluded
tier == optional          -> optional
otherwise                 -> primary
```

Within each collection, select all explicit include candidates first; then select auto candidates up to `llmsTxtMaxArticles` (`0` means all auto candidates). Apply stable ordering after selection.

- [ ] **Step 5: Preserve existing optional links**

Write at most one `## Optional` heading. Append page candidates first using stable selection order, then configured external optional links in their configured order. Do not deduplicate different titles solely because URLs match.

- [ ] **Step 6: Run Engine tests to GREEN**

Run the exact closure command. Expected: exit `0`.

### Task 3: Audit leakage and document the contract

**Files:**
- Modify: `src/Bukit-Core/Bukit.Engine/MachineReadabilityTrustAuditBuilder.Core.cs`
- Modify: `tests/Bukit.Engine.Tests/SeoAuditReportWriterTests.cs`
- Modify: `guide/user/17-geo.md`
- Modify: `tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`

**Interfaces:**
- Produces: `publish.llms_excluded_route_present` and `geo.llms_include_nonindexable` warnings; active documentation.

- [ ] **Step 1: Write audit and documentation RED tests**

Assert an explicitly excluded route found in either llms file produces `publish.llms_excluded_route_present`. Assert include+noindex produces `geo.llms_include_nonindexable` and remains absent. Architecture tests require all enum values, numeric bounds, precedence, and no-ranking language.

- [ ] **Step 2: Implement audit from declared policy plus actual output**

Use exact canonical containment rules already used by llms audits. Do not infer exclusion from path or collection name.

- [ ] **Step 3: Run tests serially**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

- [ ] **Step 4: Review and commit WP1-B**

Review indexability precedence, three writer paths, cap semantics and output determinism. Commit only closure files:

```bash
git commit -m "feat(geo): add page-level llms curation"
```
