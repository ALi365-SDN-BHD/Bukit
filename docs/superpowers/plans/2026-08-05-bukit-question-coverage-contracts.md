# Bukit Question Coverage Contracts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不破坏 URL 级 SEO Insights 的前提下，分别记录“站点计划回答的问题”和“搜索平台实际观察到的问题”，生成可审计的问题—页面覆盖报告。

**Architecture:** 新的 `seo-question-target-map.v1` 保存声明的 question/topic/目标 route keys，`search-question-observation.v1` 保存外部采集的 query 级指标。新的离线 `bukit seo question-insights` 复用现有 route map reader、URL normalizer 和匹配边界，输出独立 `seo-question-insights-report.v1`；原 `seo insights` 及其三个 v1 合同不变。

**Tech Stack:** .NET 10, Native AOT, `System.Text.Json` source generation, SHA-256 identity strings, JSON Schema Draft 2020-12, Bukit CLI spec/binder, xUnit.

## Global Constraints

- WP1 全部工作包必须已完成并集成。
- 不修改 `seo-observation.v1`、`seo-insights-report.v1` 或原 `bukit seo insights` 输出。
- question key 固定格式 `question:sha256:<64 lowercase hex>`；topic key 固定 `topic:sha256:<64 lowercase hex>`。
- Core 不接收或输出原始搜索 query；问题文本的规范化和 key 生成属于授权外部规划器/采集器。
- target map 是声明数据，observation 是观测数据；不得把 `coveredRouteKeys` 写入 observation。
- target map 只引用现有 `routeKey`；不存在的 route key 保留为 unmatched，不静默丢弃。
- target map 必须由读取当前 route map 的外部规划器生成；路由或 canonical 改变后旧 key 通过 unmatched 证据提示重新生成，不在 Core 猜测迁移。
- GSC observation 仍可能不完整；报告必须保存 collection method 和窗口，不宣称零数据等于零需求。
- v1 provider 固定 `google-search-console`，scope 固定 `google-organic`。
- `device` 允许 `desktop|mobile|tablet|unknown`；locale 使用非空 BCP-47-like 字符串但不在 Core 推断。
- 所有数值非负，`clicks <= impressions`，averagePosition 为有限非负数。
- 默认输出 `dist/.bukit/seo-question-insights-report.json`，不进入公开 projection 或 agent manifest。

---

## Verification Closure Command

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed docs/schemas/seo-question-target-map.v1.schema.json \
  --changed docs/schemas/search-question-observation.v1.schema.json \
  --changed docs/schemas/seo-question-insights-report.v1.schema.json \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoRouteMapReader.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoInsightsCommand.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoQuestionInsights/SeoQuestionInsightsModels.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoQuestionInsights/SeoQuestionTargetMapReader.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoQuestionInsights/SearchQuestionObservationReader.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoQuestionInsights/SeoQuestionInsightsAssembler.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoQuestionInsights/SeoQuestionInsightsReportWriter.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoQuestionInsightsCommand.cs \
  --changed src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoCommand.cs \
  --changed tests/Bukit.Cli.Tests/SeoQuestionTargetMapReaderTests.cs \
  --changed tests/Bukit.Cli.Tests/SearchQuestionObservationReaderTests.cs \
  --changed tests/Bukit.Cli.Tests/SeoInsightsCommandTests.cs \
  --changed tests/Bukit.Cli.Tests/SeoQuestionInsightsReportWriterTests.cs \
  --changed tests/Bukit.Cli.Tests/SeoQuestionInsightsCommandTests.cs \
  --changed tests/Bukit.Cli.Tests/CliContractTests.cs \
  --changed guide/user/22-seo-question-insights.md \
  --changed guide/user/12-cli-reference.md \
  --changed guide/user/README.md \
  --changed tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
```

Expected: `unmappedFiles: []`; CLI and Architecture commands run serially.

### Task 1: Define strict target and observation schemas

**Files:**
- Create: `docs/schemas/seo-question-target-map.v1.schema.json`
- Create: `docs/schemas/search-question-observation.v1.schema.json`
- Create: `docs/schemas/seo-question-insights-report.v1.schema.json`
- Modify: `tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`

**Interfaces:**
- Produces: three Draft 2020-12 contracts.

- [ ] **Step 1: Generate the complete CLI/Architecture closure**

Include all schemas, source files, tests and guides from every task. Expected: `unmappedFiles: []`; CLI and Architecture commands are `dotnet-serial`.

- [ ] **Step 2: Add schema RED tests**

Assert exact `$id`, `schemaVersion: 1.0`, `additionalProperties: false`, key regexes, required sets, row caps of `100000`, and fixed provider/scope. Assert report `unmatchedTargets` requires `questionKey`, `routeKey`, `errorCode` with fixed `route_key_not_found`.

- [ ] **Step 3: Create the target-map contract**

Use this exact shape:

```json
{
  "schema": "https://bukit.dev/schemas/seo-question-target-map.v1.json",
  "schemaVersion": "1.0",
  "generatedAt": "2026-08-05T00:00:00Z",
  "questions": [
    {
      "questionKey": "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "topicKey": "topic:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
      "intent": "informational",
      "locale": "zh-CN",
      "priority": "P1",
      "coveredRouteKeys": ["route:sha256:fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210"]
    }
  ]
}
```

Allowed intents: `informational|navigational|commercial|transactional|other`; priorities: `P0|P1|P2`.

- [ ] **Step 4: Create the search observation contract**

Each row requires `questionKey`, `topicKey`, `url`, `locale`, `device`, `impressions`, `clicks`, `averagePosition`. Dataset requires `collectionMethod` with enum `api|export|manual`; it reuses the same `window` shape as URL observations.

- [ ] **Step 5: Create the report schema**

Top level requires `schema`, `schemaVersion`, `generatedAt`, `window`, `sources`, `joinQuality`, `questions`, `unmatchedTargets`, `unmatchedObservations`, `ambiguousObservations`. `joinQuality` separately counts target rows and observation rows so neither population is hidden.

### Task 2: Implement strict local readers and models

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoRouteMapReader.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/SeoInsightsCommand.cs`
- Modify: `tests/Bukit.Cli.Tests/SeoInsightsCommandTests.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoQuestionInsights/SeoQuestionInsightsModels.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoQuestionInsights/SeoQuestionTargetMapReader.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoQuestionInsights/SearchQuestionObservationReader.cs`
- Create: `tests/Bukit.Cli.Tests/SeoQuestionTargetMapReaderTests.cs`
- Create: `tests/Bukit.Cli.Tests/SearchQuestionObservationReaderTests.cs`

**Interfaces:**
- Produces: shared `SeoRouteMapReader.Read(string)`, source-generated question models/readers with `MaximumFileBytes = 50 MiB`, `MaximumRows = 100_000`.
- Consumes: local paths only.

- [ ] **Step 1: Extract the existing route-map reader with characterization tests**

Move the private route-map parsing/validation logic from `SeoInsightsCommand`
into `SeoRouteMapReader.Read(string)`. First add characterization coverage to
the existing `SeoInsightsCommandTests` proving valid, invalid, duplicate-key,
remote-path and oversized behavior is unchanged. The existing command must
delegate to the shared reader before question-specific readers are added.

- [ ] **Step 2: Write question-reader RED tests**

Copy the proven security corpus from `SeoObservationDatasetReaderTests`: remote URI, missing file, oversize, malformed JSON, duplicate field, unknown field, wrong schema, wrong provider, reversed dates, nonfinite/negative metrics, clicks greater than impressions and row overflow.

- [ ] **Step 3: Run CLI tests and confirm RED**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

- [ ] **Step 4: Define source-generated records**

```csharp
internal sealed record SeoQuestionTarget(
    string QuestionKey,
    string TopicKey,
    string Intent,
    string Locale,
    string Priority,
    IReadOnlyList<string> CoveredRouteKeys);

internal sealed record SearchQuestionObservationRow(
    string QuestionKey,
    string TopicKey,
    string Url,
    string Locale,
    string Device,
    long Impressions,
    long Clicks,
    double AveragePosition);
```

Readers must reject remote paths before opening, then reject unknown/duplicate fields before source-generation deserialization.

- [ ] **Step 5: Run CLI tests to GREEN**

### Task 3: Join questions, targets and observed URLs deterministically

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoQuestionInsights/SeoQuestionInsightsAssembler.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoQuestionInsights/SeoQuestionInsightsReportWriter.cs`
- Create: `tests/Bukit.Cli.Tests/SeoQuestionInsightsReportWriterTests.cs`

**Interfaces:**
- Consumes: existing `SeoRouteMap`, `SeoInsightsRuleProfileReader`, `SeoObservationRouteMatcher`, target map and one or more search-question datasets sharing the same window.
- Produces: `SeoQuestionInsightsReport` and `.bukit/seo-question-insights-report.json`.

- [ ] **Step 1: Write join RED tests**

Prove matched target/observation aggregation, missing target route key, unmatched/ambiguous URL preservation, provider/window mismatch rejection, duplicate datasets counted rather than silently deduplicated, byte-stable ordering, and correct target/observation join-quality totals.

- [ ] **Step 2: Implement two-stage join**

First index route map by exact `routeKey` for declared targets. Then use existing URL matcher for observation URLs. Aggregate only when both questionKey and a unique route match; preserve all failures in their respective arrays.

- [ ] **Step 3: Compute descriptive metrics only**

For each question/route pair calculate CTR as `clicks / impressions` when impressions > 0 and weighted average position by impressions. Do not emit coverage-quality grades or root-cause findings in v1.

- [ ] **Step 4: Run CLI tests to GREEN**

### Task 4: Add the offline CLI and active guide

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoQuestionInsightsCommand.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/SeoCommand.cs`
- Create: `tests/Bukit.Cli.Tests/SeoQuestionInsightsCommandTests.cs`
- Modify: `tests/Bukit.Cli.Tests/CliContractTests.cs`
- Create: `guide/user/22-seo-question-insights.md`
- Modify: `guide/user/12-cli-reference.md`
- Modify: `guide/user/README.md`
- Modify: `tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`

**Interfaces:**
- Produces: `bukit seo question-insights --route-map dist/.bukit/seo-route-map.json --rules observations/seo-insights-rules.json --targets observations/question-targets.json --observations observations/gsc-questions.json --out dist/.bukit/seo-question-insights-report.json`.

- [ ] **Step 1: Add CLI RED tests**

Assert help, required options, repeatable observations, allowed hosts and URL normalization from the existing rules contract, local-only paths, default output, exit `0` success, exit `1` under explicit strict-join with unmatched/ambiguous evidence, and exit `2` for invalid input. Ensure existing `seo audit|diff|insights` specs are unchanged.

- [ ] **Step 2: Implement command by composing readers/assembler/writer**

Do not add HTTP, auth, pagination, scheduler or automatic edit code. Write through a staged file and atomic move following existing `SeoInsightsCommand` output safety.

- [ ] **Step 3: Document privacy and incompleteness**

Guide must state that Core never receives raw queries, GSC top-row behavior can omit low-volume queries, and the report prioritizes human review rather than proving demand or causation.

- [ ] **Step 4: Run specialty tests serially**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

- [ ] **Step 5: Review and commit WP2-A**

Review schema/readers, key privacy, join accounting, unchanged URL report and Native AOT source generation. Commit:

```bash
git commit -m "feat(seo): add question coverage observations"
```
