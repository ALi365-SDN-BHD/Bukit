# Bukit Generative Citation Observation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 记录固定问题集在生成式回答中的品牌提及和站点引用，多运行聚合后生成可复现、非因果的离线报告。

**Architecture:** 外部采集器写 `generative-answer-observation.v1`，每行只保存 question key、prompt variant、run index、布尔信号、引用 URL/位置和 answer hash，不保存答案正文。`bukit seo generative-insights` 复用 route map/URL normalizer，把本站引用连接到 route keys，输出独立 `generative-citation-report.v1`。

**Tech Stack:** .NET 10, Native AOT JSON source generation, strict JSON readers, existing SEO route matcher, xUnit.

## Global Constraints

- WP2-A 必须已完成并集成。
- Core 不调用任何生成式服务，不持有账号、cookie、OAuth、API key 或浏览器状态。
- engine 是非空 provider 标识，不在 v1 硬编码 ChatGPT/Gemini/Perplexity 枚举。
- observation 顶层固定一个 engine、promptSetVersion、locale、collectedAt 和 collectionMethod。
- `promptVariant` 是 `0..9999`，`runIndex` 是 `0..9999`；同一 dataset 内 `(questionKey,promptVariant,runIndex)` 必须唯一。
- `answerHash` 固定 `answer:sha256:<64hex>`；不得保存原始答案、完整 prompt 或用户标识。
- `brandMentioned`、`siteCited` 必须与 citedUrls 一致：`siteCited=false` 时不得出现允许主机内 URL；`siteCited=true` 时至少有一个允许主机 URL。
- citationPosition 是一基整数或 null；只有实际引用时可非空。
- 报告输出 mention/citation 次数与比率，始终同时输出分母；不计算“排名”或置信度。
- 不将一次运行描述为稳定结果，不把前后变化描述为内容修改导致。
- 默认输出 `dist/.bukit/generative-citation-report.json`，不公开发布。

---

## Verification Closure Command

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed docs/schemas/generative-answer-observation.v1.schema.json \
  --changed docs/schemas/generative-citation-report.v1.schema.json \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoGenerativeInsights/SeoGenerativeInsightsModels.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoGenerativeInsights/GenerativeAnswerObservationReader.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoGenerativeInsights/GenerativeAnswerObservationValidator.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoGenerativeInsights/GenerativeCitationReportWriter.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoGenerativeInsightsCommand.cs \
  --changed src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoCommand.cs \
  --changed tests/Bukit.Cli.Tests/GenerativeAnswerObservationReaderTests.cs \
  --changed tests/Bukit.Cli.Tests/GenerativeCitationReportWriterTests.cs \
  --changed tests/Bukit.Cli.Tests/SeoGenerativeInsightsCommandTests.cs \
  --changed tests/Bukit.Cli.Tests/CliContractTests.cs \
  --changed guide/user/23-generative-citation-insights.md \
  --changed guide/user/12-cli-reference.md \
  --changed guide/user/README.md \
  --changed tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
```

Expected: `unmappedFiles: []`; CLI and Architecture commands run serially.

### Task 1: Define observation and report contracts

**Files:**
- Create: `docs/schemas/generative-answer-observation.v1.schema.json`
- Create: `docs/schemas/generative-citation-report.v1.schema.json`
- Modify: `tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`

**Interfaces:**
- Produces: strict Draft 2020-12 schemas.

- [ ] **Step 1: Generate full package closure**

Include schemas, CLI source/tests and guide. Expected: no unmapped files.

- [ ] **Step 2: Add schema RED tests**

Lock exact `$id`, required fields, no additional properties, key/hash regexes, numeric bounds, `maxItems: 100000`, and report join-quality structure.

- [ ] **Step 3: Create the observation shape**

```json
{
  "schema": "https://bukit.dev/schemas/generative-answer-observation.v1.json",
  "schemaVersion": "1.0",
  "engine": "provider-model-channel",
  "promptSetVersion": "2026-08-05.1",
  "locale": "zh-CN",
  "collectedAt": "2026-08-05T00:00:00Z",
  "collectionMethod": "api",
  "rows": [
    {
      "questionKey": "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "promptVariant": 0,
      "runIndex": 0,
      "brandMentioned": true,
      "siteCited": true,
      "citedUrls": ["https://example.com/page/"],
      "citationPosition": 1,
      "answerHash": "answer:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"
    }
  ]
}
```

Allowed collection methods: `api|browser-export|manual`; the value documents evidence provenance, not quality.

- [ ] **Step 4: Create report requirements**

Top level includes sources, overall run counts, by-engine counts, by-question counts, matched route citations, unmatched/ambiguous cited URLs and `joinQuality`. Stable order: engine, questionKey, routeKey, cited URL.

### Task 2: Implement strict reader and semantic validation

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoGenerativeInsights/SeoGenerativeInsightsModels.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoGenerativeInsights/GenerativeAnswerObservationReader.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoGenerativeInsights/GenerativeAnswerObservationValidator.cs`
- Create: `tests/Bukit.Cli.Tests/GenerativeAnswerObservationReaderTests.cs`

**Interfaces:**
- Produces: source-generated dataset/row records, local reader and `GenerativeAnswerObservationValidator.Validate(GenerativeAnswerObservationDataset, SeoObservationUrlOptions)`.

- [ ] **Step 1: Write security and semantic RED tests**

Cover the same local-file corpus as existing observation readers plus duplicate run identity, empty engine/version/locale, invalid hashes, contradictory siteCited/citedUrls, citationPosition without citation, duplicate cited URL and oversize rows.

- [ ] **Step 2: Run CLI tests and confirm RED**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

- [ ] **Step 3: Implement reader with allowed-host options**

The reader validates syntax independent of site host. Implement `GenerativeAnswerObservationValidator` as the separate semantic step: it receives the existing `SeoObservationUrlOptions`, verifies that `siteCited` agrees with the presence of at least one allowed-host cited URL, and classifies all cited URLs. Third-party HTTP(S) URLs remain valid external evidence and must not fail merely because they cannot map to a Bukit route.

- [ ] **Step 4: Run CLI tests to GREEN**

### Task 3: Assemble deterministic multi-run citation evidence

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoGenerativeInsights/GenerativeCitationReportWriter.cs`
- Create: `tests/Bukit.Cli.Tests/GenerativeCitationReportWriterTests.cs`

**Interfaces:**
- Consumes: route matcher and one or more generative datasets.
- Produces: `GenerativeCitationReport` and `.bukit/generative-citation-report.json`.

- [ ] **Step 1: Write RED aggregation tests**

Prove multiple engines, prompt variants and runs; mention/citation numerators and denominators; repeated citation within one run counted once per route; unmatched/ambiguous preservation; deterministic bytes; contradictory prompt-set versions preserved as separate sources rather than merged.

- [ ] **Step 2: Implement descriptive aggregation**

For every group output `runs`, `brandMentions`, `brandMentionRate`,
`siteCitations`, `siteCitationRate`. Ratio is null when runs is zero. First
classify cited URLs by the allowed host set: allowed-host URLs enter route
matching and join-quality counts; other HTTP(S) URLs enter a separate
`externalCitedUrls` evidence array and never count as unmatched Bukit routes.

- [ ] **Step 3: Run CLI tests to GREEN**

### Task 4: Add offline command and guide

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoGenerativeInsightsCommand.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/SeoCommand.cs`
- Create: `tests/Bukit.Cli.Tests/SeoGenerativeInsightsCommandTests.cs`
- Modify: `tests/Bukit.Cli.Tests/CliContractTests.cs`
- Create: `guide/user/23-generative-citation-insights.md`
- Modify: `guide/user/12-cli-reference.md`
- Modify: `guide/user/README.md`
- Modify: `tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`

**Interfaces:**
- Produces: `bukit seo generative-insights --route-map dist/.bukit/seo-route-map.json --rules observations/seo-insights-rules.json --observations observations/generative-runs.json --out dist/.bukit/generative-citation-report.json`.

- [ ] **Step 1: Add CLI RED tests**

Assert help, repeatable observations, allowed hosts from the existing rules contract, local-only I/O, default output, atomic write, exit `0` normal, exit `1` strict join, exit `2` invalid input.

- [ ] **Step 2: Implement by composition only**

Reuse `SeoRouteMapReader`, `SeoInsightsRuleProfileReader`, `SeoObservationRouteMatcher` and output staging. Do not add provider SDKs or browser automation.

- [ ] **Step 3: Document repeatability boundary**

Guide requires fixed question set, versioned prompt set, multiple phrasings, repeated runs, engine/model context where collector can provide it, and explicit statement that observed changes do not prove causation.

- [ ] **Step 4: Run tests serially and commit**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
git commit -m "feat(seo): report generative citation observations"
```

Review must focus on raw-answer exclusion, ratio denominators, host matching and unchanged existing commands.
