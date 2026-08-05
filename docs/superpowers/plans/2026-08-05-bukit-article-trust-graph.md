# Bukit Article Trust Graph Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将文章页面、引用和明确派生来源连接到同一个 Article-family JSON-LD 节点，同时保持现有 citation 输入兼容。

**Architecture:** 所有 Article/BlogPosting/NewsArticle 输出 `mainEntityOfPage`。现有 citation 全部进入 Article 的 `citation`；新增可选 `relation: based-on` 时，同一条引用额外进入 `isBasedOn`。现有独立 `WebPage.mentions` 在本包保留，避免无版本的序列化删除。

**Tech Stack:** .NET 10, Bukit Rendering models, JSON-LD, Schema.org semantics, xUnit, documentation contract tests.

## Global Constraints

- WP0 必须已完成并集成；不得在本包再次修改 publisher 规则。
- `GeoCitationModel.Relation` 是可选增量属性，默认 `citation`；允许值仅 `citation`、`based-on`。
- 所有有效引用进入 `citation`；只有 `based-on` 额外进入 `isBasedOn`。
- 不从标题、URL、`original_source` 或“转载”字样推断 `based-on`。
- `mainEntityOfPage` 固定为 `{ "@type": "WebPage", "@id": canonical }`。
- 继续输出现有独立 `WebPage.mentions`；移除或改为 `@graph` 需要未来独立版本计划。
- 无引用时仍输出 `mainEntityOfPage`，但不输出空数组。
- 无效 relation 按 `site.seo.diagnostics` 现有 warn/strict 行为报告；不得静默回退。

---

## Verification Closure Command

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed src/Bukit-Core/Bukit.Rendering/Models.cs \
  --changed src/Bukit-Core/Bukit.Engine/SeoGeoMetaParser.cs \
  --changed src/Bukit-Core/Bukit.Engine/SeoDiagnostics.cs \
  --changed src/Bukit-Core/Bukit.Engine/SeoJsonLdBuilder.cs \
  --changed src/Bukit-Core/Bukit.Engine/SeoSchemaValidator.cs \
  --changed tests/Bukit.Engine.Tests/GeoSeoModelBuilderTests.cs \
  --changed tests/Bukit.Engine.Tests/GeoDiagnosticsTests.cs \
  --changed tests/Bukit.Engine.Tests/SeoSchemaValidatorCoverageTests.cs \
  --changed guide/user/17-geo.md \
  --changed docs/seo.md \
  --changed tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
```

Expected: `unmappedFiles: []`; Engine and Architecture commands are
`dotnet-serial` and run in the order returned by `classify`.

### Task 1: Add the additive citation relation contract

**Files:**
- Modify: `src/Bukit-Core/Bukit.Rendering/Models.cs:85-89`
- Modify: `src/Bukit-Core/Bukit.Engine/SeoGeoMetaParser.cs:151-173`
- Modify: `src/Bukit-Core/Bukit.Engine/SeoDiagnostics.cs`
- Modify: `tests/Bukit.Engine.Tests/GeoSeoModelBuilderTests.cs`
- Modify: `tests/Bukit.Engine.Tests/GeoDiagnosticsTests.cs`

**Interfaces:**
- Produces: `GeoCitationModel.Relation`, defaulting to `citation`.
- Consumes: content-level `geo.citations[].relation`.

- [ ] **Step 1: Generate closure for every Task 1 path**

Expected: Engine and Rendering consumers are present; `unmappedFiles: []`; commands are serialized if both projects appear.

- [ ] **Step 2: Write parser RED tests**

Use this exact input shape:

```csharp
["geo"] = new Dictionary<string, object>
{
    ["citations"] = new List<object>
    {
        new Dictionary<string, object>
        {
            ["title"] = "Primary report",
            ["url"] = "https://source.example/report",
            ["relation"] = "based-on"
        }
    }
};
```

Assert `Relation == "based-on"`; omit the field in a second row and assert `Relation == "citation"`.

- [ ] **Step 3: Add invalid-relation diagnostics RED test**

Construct `GeoCitationModel { Title = "Ref", Url = "https://example.com", Relation = "copied-from" }` and assert the existing logger contains `geo.citation_relation_invalid` in warn mode and throws the existing config exception in strict mode.

- [ ] **Step 4: Run Engine tests and confirm RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

- [ ] **Step 5: Add the property and parser default**

```csharp
public sealed record GeoCitationModel
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string Relation { get; init; } = "citation";
}
```

Parser trims and lowercases relation using invariant rules; blank or omitted becomes `citation`. It preserves an unknown nonblank value so diagnostics can fail closed rather than silently rewriting it.

- [ ] **Step 6: Run Engine tests to GREEN**

Run the closure-returned Engine command. Expected: exit `0`.

### Task 2: Attach page and provenance relationships to Article JSON-LD

**Files:**
- Modify: `src/Bukit-Core/Bukit.Engine/SeoJsonLdBuilder.cs:189-258,588-607`
- Modify: `src/Bukit-Core/Bukit.Engine/SeoSchemaValidator.cs`
- Modify: `tests/Bukit.Engine.Tests/GeoSeoModelBuilderTests.cs`
- Modify: `tests/Bukit.Engine.Tests/SeoSchemaValidatorCoverageTests.cs`

**Interfaces:**
- Consumes: `canonical`, `IReadOnlyList<GeoCitationModel>`.
- Produces: Article `mainEntityOfPage`, `citation`, optional `isBasedOn`.

- [ ] **Step 1: Write JSON-LD RED tests for all Article-family types**

For `Article`, `BlogPosting`, and `NewsArticle`, assert:

```json
"mainEntityOfPage": {
  "@type": "WebPage",
  "@id": "https://example.com/news/item/"
}
```

Add one default citation and one `based-on` citation. Assert both appear in `citation`; only the second appears in `isBasedOn`; the existing standalone WebPage still contains both under `mentions`.

- [ ] **Step 2: Add audit RED tests**

Assert Article-family nodes warn with `<prefix>_main_entity_of_page_missing` when absent. For any present `citation`/`isBasedOn` entry, require an object with `@type: WebPage`, nonblank `name`, and absolute HTTP(S) `url`; malformed entries produce warning codes, not errors.

- [ ] **Step 3: Run Engine tests and confirm RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

- [ ] **Step 4: Build reusable citation nodes once**

Inside `BuildArticleJsonLd`, create deterministic arrays ordered by input order:

```csharp
var citationNodes = geo.Citations?
    .Select(BuildCitationNode)
    .ToArray();

article["mainEntityOfPage"] = new Dictionary<string, object?>
{
    ["@type"] = "WebPage",
    ["@id"] = canonical
};
```

Set `article["citation"]` when the array is nonempty. Filter `Relation == "based-on"` into a second array and set `article["isBasedOn"]` only when nonempty. `BuildCitationsJsonLd` must call the same `BuildCitationNode` helper so fields cannot drift.

- [ ] **Step 5: Run Engine tests to GREEN**

Run the exact closure Engine command. Expected: exit `0`; serialized order is stable across repeated builds.

### Task 3: Document and lock the public contract

**Files:**
- Modify: `guide/user/17-geo.md`
- Modify: `docs/seo.md`
- Modify: `tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`

**Interfaces:**
- Produces: user-visible `geo.citations[].relation` documentation and no-ranking boundary.

- [ ] **Step 1: Add a failing documentation contract test**

Assert the active guide contains `relation: citation`, `relation: based-on`, `mainEntityOfPage`, and the statement that `based-on` must be explicit and does not prove authority or ranking.

- [ ] **Step 2: Run Architecture tests and confirm RED**

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

- [ ] **Step 3: Update active documentation only**

Add one valid YAML example and one JSON-LD result example. State that existing citations without relation retain `citation` semantics and standalone `mentions` remains for compatibility.

- [ ] **Step 4: Run specialty tests serially**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

- [ ] **Step 5: Review and commit WP1-A**

Review only closure files, focusing on public `GeoCitationModel`, JSON-LD duplication and invalid relation behavior. Then stage only those files and commit:

```bash
git commit -m "feat(seo): connect article provenance graph"
```
