# IDE0290 / IDE0305 Ratchet Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove only the four IDE0290 and seventeen IDE0305 diagnostics introduced by the Notion two-layer migration, restoring the code-analysis ratchet without changing runtime behavior, public API, governance policy, or the accepted historical baseline.

**Architecture:** Treat the existing code-analysis ratchet as the failing acceptance test. Apply three manually reviewed syntax-only batches: primary constructors in `Bukit.Notion`, exact-target collection expressions in Notion transport/content adapter code, and exact-target collection expressions in the `Bukit.Shared` compatibility layer. Preserve concrete collection types at every interface-typed boundary instead of relying on the potentially semantics-changing loose collection-expression conversion.

**Tech Stack:** C# 12+/net10.0, .NET SDK analyzers, xUnit, Bash/Python repository gates, Bukit public API drift tooling.

## Global Constraints

- Parent base is `3ceb096a3ae2cdff145a49798460671261968b04`.
- Work on an independent branch named `codex/ide0290-ide0305-ratchet-remediation`.
- Fix only the current Notion-migration increments: IDE0290 `42 -> 38` and IDE0305 `151 -> 134`.
- Do not modify `scripts/checks/baselines/code-analysis.v1.json`, `.editorconfig`, `Directory.Build.props`, analyzer versions, severities, or thresholds.
- Do not add suppressions, pragmas, generated-code exclusions, or `NoWarn` entries.
- Do not modify schemas, plugin protocols, configuration contracts, Notion wire shapes, HTTP/retry/TLS behavior, serialization shape, or public member signatures.
- Do not bulk-run an automatic IDE0290/IDE0305 fix across the solution.
- Preserve concrete collection types: request/header snapshots remain arrays, database option snapshots remain arrays, and legacy compatibility results remain `List<T>`.
- Do not modernize the accepted historical inventory of 38 IDE0290 and 134 IDE0305 findings.
- After each code batch, run only `post-change-focused.sh` for that batch. Run `post-change-targeted.sh` exactly once for the aggregate diff.
- Do not run full/release/smoke-all/test-all/coverage/whole-solution gates.
- A failed environment or infrastructure check is reported as blocked evidence and does not authorize unrelated changes.

## Current Evidence and Exact Scope

The live style report at the parent base contains:

| Diagnostic | Governed baseline | Current | Increment to remove |
|---|---:|---:|---:|
| IDE0290 | 38 | 42 | 4 |
| IDE0305 | 134 | 151 | 17 |

The increments map exactly to these locations:

| Batch | File | Diagnostic count |
|---|---|---:|
| Primary constructors | `src/Bukit-Core/Bukit.Notion/Rendering/BlockRenderers/ChildEntityBlockRenderer.cs` | IDE0290 x1 |
| Primary constructors | `src/Bukit-Core/Bukit.Notion/Rendering/BlockRenderers/RichTextContainerRenderer.cs` | IDE0290 x1 |
| Primary constructors | `src/Bukit-Core/Bukit.Notion/Rendering/NotionRenderingException.cs` | IDE0290 x1 |
| Primary constructors | `src/Bukit-Core/Bukit.Notion/Transport/NotionApiException.cs` | IDE0290 x1 |
| Transport/adapter collections | `src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs` | IDE0305 x3 |
| Transport/adapter collections | `src/Bukit-Core/Bukit.Content.Notion/NotionDatabaseOptionReader.cs` | IDE0305 x1 |
| Compatibility collections | `src/Bukit-Core/Bukit.Shared/Notion/HtmlTokenizer.cs` | IDE0305 x1 |
| Compatibility collections | `src/Bukit-Core/Bukit.Shared/Notion/HtmlToNotionBlockConverter.cs` | IDE0305 x1 |
| Compatibility collections | `src/Bukit-Core/Bukit.Shared/Notion/NotionBlockJsonWriter.cs` | IDE0305 x1 |
| Compatibility collections | `src/Bukit-Core/Bukit.Shared/Notion/NotionCompatibilityMapper.cs` | IDE0305 x10 |

No other IDE0290/IDE0305 location is in scope.

---

### Task 1: Establish the isolated failing baseline

**Files:**
- Read: `scripts/checks/code-analysis-ratchet.sh`
- Read: `scripts/checks/code-analysis-ratchet.py`
- Read: `scripts/checks/baselines/code-analysis.v1.json`
- Do not modify any file in this task.

**Interfaces:**
- Consumes: `bash scripts/checks/code-analysis-ratchet.sh check`.
- Produces: a recorded parent base and an exact failing diagnostic inventory.

- [ ] **Step 1: Verify clean parent state and create the branch**

Run:

```bash
git status --short --branch
git rev-parse HEAD
git switch -c codex/ide0290-ide0305-ratchet-remediation
```

Expected: no tracked or untracked changes other than this plan if it has not been committed separately; resolved parent base is `3ceb096a3ae2cdff145a49798460671261968b04`.

- [ ] **Step 2: Run the existing failing acceptance test**

Run:

```bash
bash scripts/checks/code-analysis-ratchet.sh check
```

Expected: exit 1 with IDE0290 current 42 above baseline 38 and IDE0305 current 151 above baseline 134. Any different count requires refreshing the location inventory before editing; it does not authorize changing the baseline.

- [ ] **Step 3: Prove protected governance files are initially unchanged**

Run:

```bash
git diff --exit-code -- \
  .editorconfig \
  Directory.Build.props \
  scripts/checks/baselines/code-analysis.v1.json
```

Expected: exit 0 and no output.

---

### Task 2: Remove the four IDE0290 increments with API-compatible primary constructors

**Files:**
- Modify: `src/Bukit-Core/Bukit.Notion/Rendering/BlockRenderers/ChildEntityBlockRenderer.cs:10-17`
- Modify: `src/Bukit-Core/Bukit.Notion/Rendering/BlockRenderers/RichTextContainerRenderer.cs:11-20`
- Modify: `src/Bukit-Core/Bukit.Notion/Rendering/NotionRenderingException.cs:3-8`
- Modify: `src/Bukit-Core/Bukit.Notion/Transport/NotionApiException.cs:13-35`
- Test existing: `tests/Bukit.Notion.Tests/NotionRenderingTests.cs`
- Test existing: `tests/Bukit.Notion.Tests/NotionClientTests.cs`

**Interfaces:**
- Consumes: the four existing public constructor signatures and exception property contracts.
- Produces: the same constructor metadata, defaults, fields/properties, exception messages, and rendering behavior without IDE0290 findings.

- [ ] **Step 1: Convert the two renderers without removing their explicit private state**

Replace the class headers and constructor assignments with:

```csharp
public sealed class ChildEntityBlockRenderer(string typeName) : INotionBlockRenderer
{
    private readonly string _typeName = typeName;

    // RenderAsync remains byte-for-byte unchanged.
}
```

```csharp
public sealed class RichTextContainerRenderer(string containerName, string tag) : INotionBlockRenderer
{
    private readonly string _containerName = containerName;
    private readonly string _tag = tag;

    // All rendering methods remain byte-for-byte unchanged.
}
```

Do not replace `_typeName`, `_containerName`, or `_tag` usages with captured primary-constructor parameters; retaining the named fields minimizes state-layout and readability drift.

- [ ] **Step 2: Convert the rendering exception while preserving its public constructor**

Use:

```csharp
public sealed class NotionRenderingException(string message) : Exception(message)
{
}
```

The parameter name and `Exception(message)` base call must remain unchanged.

- [ ] **Step 3: Convert the API exception and initialize each property from the same parameter**

Use:

```csharp
public sealed class NotionApiException(
    NotionApiErrorKind kind,
    string message,
    HttpStatusCode? statusCode = null,
    string? reasonPhrase = null,
    int attempts = 1,
    string? rootErrorType = null)
    : Exception(message)
{
    public NotionApiErrorKind Kind { get; } = kind;
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public string? ReasonPhrase { get; } = reasonPhrase;
    public int Attempts { get; } = attempts;
    public string? RootErrorType { get; } = rootErrorType;
}
```

Do not change constructor accessibility, parameter order, parameter names, default values, property types, or property accessibility.

- [ ] **Step 4: Run the affected project tests**

Run:

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release
```

Expected: all tests pass, including rendering exception message, transport failure classification, cancellation, retry, and disposal tests.

- [ ] **Step 5: Verify public metadata remains unchanged**

Run:

```bash
bash scripts/checks/public-api-drift.sh check Release
```

Expected: pass with no public member addition, removal, or signature change.

- [ ] **Step 6: Run focused verification for only the four constructor files**

Run:

```bash
bash scripts/checks/post-change-focused.sh --configuration Release -- \
  src/Bukit-Core/Bukit.Notion/Rendering/BlockRenderers/ChildEntityBlockRenderer.cs \
  src/Bukit-Core/Bukit.Notion/Rendering/BlockRenderers/RichTextContainerRenderer.cs \
  src/Bukit-Core/Bukit.Notion/Rendering/NotionRenderingException.cs \
  src/Bukit-Core/Bukit.Notion/Transport/NotionApiException.cs
```

Expected: pass. The aggregate ratchet is not required to pass yet because Task 3 and Task 4 still contain the IDE0305 increments.

- [ ] **Step 7: Commit the independently reviewable IDE0290 batch**

```bash
git add \
  src/Bukit-Core/Bukit.Notion/Rendering/BlockRenderers/ChildEntityBlockRenderer.cs \
  src/Bukit-Core/Bukit.Notion/Rendering/BlockRenderers/RichTextContainerRenderer.cs \
  src/Bukit-Core/Bukit.Notion/Rendering/NotionRenderingException.cs \
  src/Bukit-Core/Bukit.Notion/Transport/NotionApiException.cs
git commit -m "style(notion): satisfy primary constructor ratchet"
```

---

### Task 3: Remove four IDE0305 increments in transport and content adapter code

**Files:**
- Modify: `src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs:298-318`
- Modify: `src/Bukit-Core/Bukit.Content.Notion/NotionDatabaseOptionReader.cs:91-109`
- Test existing: `tests/Bukit.Notion.Tests/NotionClientTests.cs`
- Test existing: `tests/Bukit.Content.Notion.Tests/NotionContentSourceTests.cs`
- Test existing: `tests/Bukit.Content.Tests/NotionApiClientExtendedTests.cs`

**Interfaces:**
- Consumes: `BufferedRequest` array-backed request/header snapshot behavior and `TryReadOptions(..., out IReadOnlyList<string>)`.
- Produces: the same array-backed snapshots and option ordering without four IDE0305 findings.

- [ ] **Step 1: Replace header-value materialization with exact `string[]` collection expressions**

Within `BufferedRequest.CreateAsync`, use an explicit array target for header values and the request-header snapshot:

```csharp
var contentHeaders = request.Content?.Headers
    .Select(static header => new KeyValuePair<string, string[]>(header.Key, [.. header.Value]))
    .ToArray() ?? [];
KeyValuePair<string, string[]>[] headers =
[
    .. request.Headers.Select(static header =>
        new KeyValuePair<string, string[]>(header.Key, [.. header.Value]))
];

return new BufferedRequest(
    request.Method,
    request.RequestUri!,
    request.Version,
    request.VersionPolicy,
    headers,
    content,
    contentHeaders);
```

Keep the outer content-header `.ToArray() ?? []` because `var` already resolves it to the existing concrete array type and it is not the governed IDE0305 location. Do not pass a collection expression directly to the `IReadOnlyList<...>` record parameter; the explicit local array prevents concrete-type drift.

- [ ] **Step 2: Preserve the option result as a concrete `string[]`**

Replace the terminal `.ToArray()` with an explicitly targeted local array:

```csharp
string[] parsedOptions =
[
    .. optionElements.EnumerateArray()
        .Select(static option => NotionContentSource.GetString(option, "name")?.Trim())
        .Where(static name => !string.IsNullOrWhiteSpace(name))
        .Select(static name => name!)
];
options = parsedOptions;
return true;
```

Do not assign the collection expression directly to `IReadOnlyList<string>`; retain the previous `string[]` runtime type, ordering, duplicate handling, trimming, and null filtering.

- [ ] **Step 3: Run the mapped affected tests**

Run:

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj -c Release
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release \
  --filter FullyQualifiedName~NotionApiClientExtendedTests
```

Expected: all selected tests pass; database options remain `Alpha`, `Beta` in source order, request retries remain deterministic, and no transport behavior changes.

- [ ] **Step 4: Run focused verification for the transport/adapter files**

Run:

```bash
bash scripts/checks/post-change-focused.sh --configuration Release -- \
  src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs \
  src/Bukit-Core/Bukit.Content.Notion/NotionDatabaseOptionReader.cs
```

Expected: pass.

- [ ] **Step 5: Commit the independently reviewable transport/adapter batch**

```bash
git add \
  src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs \
  src/Bukit-Core/Bukit.Content.Notion/NotionDatabaseOptionReader.cs
git commit -m "style(notion): preserve collection types under IDE0305"
```

---

### Task 4: Remove thirteen IDE0305 increments in the legacy compatibility layer

**Files:**
- Modify: `src/Bukit-Core/Bukit.Shared/Notion/HtmlTokenizer.cs:18-27`
- Modify: `src/Bukit-Core/Bukit.Shared/Notion/HtmlToNotionBlockConverter.cs:8-11`
- Modify: `src/Bukit-Core/Bukit.Shared/Notion/NotionBlockJsonWriter.cs:5-10`
- Modify: `src/Bukit-Core/Bukit.Shared/Notion/NotionCompatibilityMapper.cs:7-45`
- Test existing: `tests/Bukit.Shared.Tests/HtmlTokenizerTests.cs`
- Test existing: `tests/Bukit.Shared.Tests/HtmlToNotionBlockConverterTests.cs`
- Test existing: `tests/Bukit.Shared.Tests/NotionBlockJsonWriterTests.cs`
- Test existing: `tests/Bukit.Shared.Tests/LegacyNotionCompatibilityTests.cs`

**Interfaces:**
- Consumes: legacy methods that return `List<T>` and record constructors whose collection parameters are `List<T>`.
- Produces: the same mutable `List<T>` results, item order, round-trip equivalence, and serialized Notion JSON without thirteen IDE0305 findings.

- [ ] **Step 1: Use target-typed list expressions in the two public legacy converters**

Use:

```csharp
public static List<HtmlToken> Tokenize(string html)
    =>
    [
        .. Bukit.Notion.Conversion.HtmlTokenizer.Tokenize(html)
            .Select(static token => new HtmlToken
            {
                Type = (HtmlTokenType)(int)token.Type,
                TagName = token.TagName,
                Attributes = token.Attributes,
                TextContent = token.TextContent
            })
    ];
```

```csharp
public static List<NotionBlock> Convert(string html)
    =>
    [
        .. Bukit.Notion.Conversion.HtmlToNotionBlockConverter.Convert(html)
            .Select(NotionCompatibilityMapper.ToLegacy)
    ];
```

The declared return type remains `List<T>`, so the concrete mutable result type is unchanged.

- [ ] **Step 2: Preserve the internal writer's concrete `List<T>` input**

Convert the expression-bodied method to a block with an explicit list target:

```csharp
internal static string SerializeBlocks(List<NotionBlock> blocks)
{
    List<Bukit.Notion.Blocks.NotionBlock> independentBlocks =
    [
        .. blocks.Select(NotionCompatibilityMapper.ToIndependent)
    ];
    return Bukit.Notion.Conversion.NotionBlockJsonWriter.SerializeBlocks(independentBlocks);
}
```

Do not pass the collection expression directly to the independent writer's `IReadOnlyList<T>` parameter; the explicit `List<T>` preserves the former `.ToList()` materialization.

- [ ] **Step 3: Replace the ten mapper `.ToList()` calls with list-targeted spread expressions**

Use the same switch cases and change only the collection arguments:

```csharp
NewBlocks.ParagraphBlock value => new ParagraphBlock([.. value.Segments.Select(ToLegacy)]),
NewBlocks.BulletedListItemBlock value => new BulletedListItemBlock([.. value.Segments.Select(ToLegacy)]),
NewBlocks.NumberedListItemBlock value => new NumberedListItemBlock([.. value.Segments.Select(ToLegacy)]),
NewBlocks.QuoteBlock value => new QuoteBlock([.. value.Segments.Select(ToLegacy)]),
NewBlocks.ToggleBlock value => new ToggleBlock(value.Heading, [.. value.Children.Select(ToLegacy)]),
```

and:

```csharp
ParagraphBlock value => new NewBlocks.ParagraphBlock([.. value.Segments.Select(ToIndependent)]),
BulletedListItemBlock value => new NewBlocks.BulletedListItemBlock([.. value.Segments.Select(ToIndependent)]),
NumberedListItemBlock value => new NewBlocks.NumberedListItemBlock([.. value.Segments.Select(ToIndependent)]),
QuoteBlock value => new NewBlocks.QuoteBlock([.. value.Segments.Select(ToIndependent)]),
ToggleBlock value => new NewBlocks.ToggleBlock(value.Heading, [.. value.Children.Select(ToIndependent)]),
```

Every target record constructor requires `List<T>`, preserving the concrete type and eager single enumeration. Do not change any non-collection switch arm or unsupported-type exception.

- [ ] **Step 4: Run compatibility-layer tests**

Run:

```bash
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c Release
```

Expected: all tests pass, including canonical/legacy exhaustive round trips, JSON equality, tokenizer behavior, and block writer shape.

- [ ] **Step 5: Run focused verification for the four compatibility files**

Run:

```bash
bash scripts/checks/post-change-focused.sh --configuration Release -- \
  src/Bukit-Core/Bukit.Shared/Notion/HtmlTokenizer.cs \
  src/Bukit-Core/Bukit.Shared/Notion/HtmlToNotionBlockConverter.cs \
  src/Bukit-Core/Bukit.Shared/Notion/NotionBlockJsonWriter.cs \
  src/Bukit-Core/Bukit.Shared/Notion/NotionCompatibilityMapper.cs
```

Expected: pass.

- [ ] **Step 6: Commit the independently reviewable compatibility batch**

```bash
git add \
  src/Bukit-Core/Bukit.Shared/Notion/HtmlTokenizer.cs \
  src/Bukit-Core/Bukit.Shared/Notion/HtmlToNotionBlockConverter.cs \
  src/Bukit-Core/Bukit.Shared/Notion/NotionBlockJsonWriter.cs \
  src/Bukit-Core/Bukit.Shared/Notion/NotionCompatibilityMapper.cs
git commit -m "style(shared): close Notion collection-expression ratchet"
```

---

### Task 5: Prove exact ratchet closure without policy or contract drift

**Files:**
- Review all ten source files changed in Tasks 2-4.
- Verify unchanged: `.editorconfig`
- Verify unchanged: `Directory.Build.props`
- Verify unchanged: `scripts/checks/baselines/code-analysis.v1.json`
- Verify unchanged: public API baseline, schemas, plugin contracts, and configuration docs.

**Interfaces:**
- Consumes: the three committed implementation batches.
- Produces: exact code-analysis closure and machine-checkable no-drift evidence.

- [ ] **Step 1: Run the real ratchet acceptance test**

Run:

```bash
bash scripts/checks/code-analysis-ratchet.sh check
```

Expected: pass. It must not report a new diagnostic ID or an increase in any non-target diagnostic.

- [ ] **Step 2: Snapshot counts outside the repository and verify the exact target totals**

Run:

```bash
bash scripts/checks/code-analysis-ratchet.sh snapshot /tmp/bukit-ide-ratchet-after.json
jq -e '.style.IDE0290 == 38 and .style.IDE0305 == 134' \
  /tmp/bukit-ide-ratchet-after.json
```

Expected: both commands pass. Do not copy the temporary snapshot into the repository.

- [ ] **Step 3: Re-run public API drift and zero-tolerance format checks**

Run:

```bash
bash scripts/checks/public-api-drift.sh check Release
bash scripts/checks/dotnet-format.sh
```

Expected: both pass.

- [ ] **Step 4: Prove governance and contract files did not change**

Run:

```bash
git diff --exit-code 3ceb096a3ae2cdff145a49798460671261968b04 -- \
  .editorconfig \
  Directory.Build.props \
  scripts/checks/baselines/code-analysis.v1.json \
  docs/governance/bukit-core-public-api-baseline.v1.json
```

Expected: exit 0 and no output.

- [ ] **Step 5: Run the aggregate targeted gate exactly once**

Run:

```bash
bash scripts/checks/post-change-targeted.sh \
  --base 3ceb096a3ae2cdff145a49798460671261968b04 \
  --configuration Release -- \
  docs/superpowers/plans/2026-07-22-ide0290-ide0305-ratchet-remediation.md \
  src/Bukit-Core/Bukit.Notion/Rendering/BlockRenderers/ChildEntityBlockRenderer.cs \
  src/Bukit-Core/Bukit.Notion/Rendering/BlockRenderers/RichTextContainerRenderer.cs \
  src/Bukit-Core/Bukit.Notion/Rendering/NotionRenderingException.cs \
  src/Bukit-Core/Bukit.Notion/Transport/NotionApiException.cs \
  src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs \
  src/Bukit-Core/Bukit.Content.Notion/NotionDatabaseOptionReader.cs \
  src/Bukit-Core/Bukit.Shared/Notion/HtmlTokenizer.cs \
  src/Bukit-Core/Bukit.Shared/Notion/HtmlToNotionBlockConverter.cs \
  src/Bukit-Core/Bukit.Shared/Notion/NotionBlockJsonWriter.cs \
  src/Bukit-Core/Bukit.Shared/Notion/NotionCompatibilityMapper.cs
```

Expected: focused affected projects and `ci-fast` pass. Do not repeat this aggregate command unless the reviewed source diff changes afterward.

---

### Task 6: Independent read-only review and closure decision

**Files:**
- Review: `git diff 3ceb096a3ae2cdff145a49798460671261968b04...HEAD`
- Do not modify files during this task.

**Interfaces:**
- Consumes: final committed diff and Task 5 evidence.
- Produces: an approve/reject review with explicit residual-risk classification.

- [ ] **Step 1: Audit scope containment**

Confirm that production edits are limited to the ten listed source files and that there are no baseline, EditorConfig, build-policy, schema, protocol, dependency, generated-file, or documentation-contract changes beyond this plan.

- [ ] **Step 2: Audit constructor semantic equivalence**

Confirm all four public constructors retain accessibility, parameter order/names/defaults, base calls, private state, property types, and exception messages. Confirm the public API drift result is clean.

- [ ] **Step 3: Audit collection semantic equivalence**

Confirm:

- request headers and content headers are still eagerly copied;
- each header value is still a new `string[]`;
- request/header ordering is unchanged;
- database options are still a `string[]` behind `IReadOnlyList<string>`;
- the legacy tokenizer/converter methods still return mutable `List<T>`;
- compatibility mapper record collections remain `List<T>`;
- enumeration count, ordering, duplicate retention, and null filtering are unchanged.

- [ ] **Step 4: Audit ratchet honesty**

Confirm IDE0290 is exactly 38 and IDE0305 exactly 134, no other diagnostic increased, and no threshold/suppression/exclusion was changed. A result below baseline is acceptable but must be explained by one of the ten intended edits; unrelated historical cleanup is rejected.

- [ ] **Step 5: Final repository checks**

Run:

```bash
git diff --check 3ceb096a3ae2cdff145a49798460671261968b04
git status --short --branch
git log --oneline 3ceb096a3ae2cdff145a49798460671261968b04..HEAD
```

Expected: whitespace check passes; the branch contains the plan plus three bounded implementation commits; no uncommitted source change remains.

- [ ] **Step 6: Closure rule**

Approve only if Tasks 1-5 evidence is complete and the independent review finds no behavior/API/policy drift. Otherwise reject closure, reopen only the owning batch, apply the minimum correction, rerun that batch's focused verification, then rerun the aggregate targeted gate once for the revised aggregate diff.

## Rollback Strategy

- Revert only the rejected batch commit; do not revert or refresh the analyzer baseline.
- If a primary-constructor conversion changes public metadata or behavior, keep that constructor in its original form and resolve IDE0290 through a separately reviewed, location-specific governance decision. Do not silently suppress it in this task.
- If a collection expression cannot preserve the prior concrete type, retain the original `.ToArray()`/`.ToList()` and open a separate rule-configuration decision. Do not accept semantic change merely to lower a style count.
- Never compensate for a failed location by cleaning an unrelated historical diagnostic; the exact Notion migration increment remains the ownership boundary.
