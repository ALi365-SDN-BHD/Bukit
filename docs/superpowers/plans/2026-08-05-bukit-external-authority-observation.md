# Bukit External Authority Observation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立跨官方来源、新闻、行业组织、代码仓库和社区讨论的通用外部引用观测合同，并明确把 Reddit adapter 留在 Core 之外。

**Architecture:** 外部采集器输出 `external-authority-observation.v1`，记录来源页面、来源类别、question/topic/entity keys、引用 URL、上下文哈希和生命周期状态。新的离线 `bukit seo authority-insights` 只把被引用的本站 URL 连接到 route map，按来源类别/provider 汇总；不计算权威分、不抓取内容、不自动发布。

**Tech Stack:** .NET 10, Native AOT JSON source generation, strict local readers, existing SEO route matcher, external-process plugin boundary, xUnit.

## Global Constraints

- WP2-B 必须已完成并集成。
- 本包不创建 Reddit、新闻、GitHub 或其他 provider adapter，只创建通用 Core handoff 和报告。
- sourceType 固定为 `official|regulator|research|news|association|repository|forum|other`。
- provider 是非空字符串；Core 不把 provider 名称映射为可信等级。
- 每行必须有 sourceUrl、sourceType、observedAt、status、contextHash、citedUrls；question/topic/entity keys 可选但至少一个必须存在。
- status 固定 `active|deleted|unavailable`；deleted/unavailable 行保留历史证据但不计入当前 active citation 数。
- contextHash 固定 `context:sha256:<64hex>`；不得保存正文、用户名、用户 ID、评论正文或私信。
- 可选 identity key 分别匹配 `question:sha256:<64hex>`、`topic:sha256:<64hex>`、`entity:sha256:<64hex>`。
- sourceUrl 可以是任意绝对 HTTP(S) URL；citedUrls 只接受绝对 HTTP(S) URL。
- Core 不判断事实真实性、原创性、权威性或社区共识，不生成“authority score”。
- 不增加网络权限、环境变量、OAuth、API key 或调度代码。
- Reddit 后续 adapter 必须是外部只读插件；自动发帖、评论、投票、私信和账号操作永远不在本计划范围。

---

## Verification Closure Command

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed docs/schemas/external-authority-observation.v1.schema.json \
  --changed docs/schemas/external-authority-report.v1.schema.json \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoAuthorityInsights/SeoAuthorityInsightsModels.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoAuthorityInsights/ExternalAuthorityObservationReader.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoAuthorityInsights/ExternalAuthorityReportWriter.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoAuthorityInsightsCommand.cs \
  --changed src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoCommand.cs \
  --changed tests/Bukit.Cli.Tests/ExternalAuthorityObservationReaderTests.cs \
  --changed tests/Bukit.Cli.Tests/ExternalAuthorityReportWriterTests.cs \
  --changed tests/Bukit.Cli.Tests/SeoAuthorityInsightsCommandTests.cs \
  --changed tests/Bukit.Cli.Tests/CliContractTests.cs \
  --changed guide/user/24-external-authority-insights.md \
  --changed guide/user/12-cli-reference.md \
  --changed guide/user/README.md \
  --changed tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
```

Expected: `unmappedFiles: []`; CLI and Architecture commands run serially.

### Task 1: Define external observation and report schemas

**Files:**
- Create: `docs/schemas/external-authority-observation.v1.schema.json`
- Create: `docs/schemas/external-authority-report.v1.schema.json`
- Modify: `tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`

**Interfaces:**
- Produces: two strict Draft 2020-12 contracts.

- [ ] **Step 1: Generate full package closure**

Include all schema, CLI, tests and guide paths. Stop on any unmapped file.

- [ ] **Step 2: Add schema RED tests**

Require exact `$id`, fixed schema version, strict source/status enums, hash/key regexes, absolute HTTP(S) URL patterns, at-least-one identity key, row cap `100000`, and report join-quality fields.

- [ ] **Step 3: Create the observation shape**

```json
{
  "schema": "https://bukit.dev/schemas/external-authority-observation.v1.json",
  "schemaVersion": "1.0",
  "provider": "approved-provider",
  "collectedAt": "2026-08-05T00:00:00Z",
  "collectionMethod": "api",
  "rows": [
    {
      "sourceUrl": "https://source.example/discussion/1",
      "sourceType": "forum",
      "observedAt": "2026-08-05T00:00:00Z",
      "status": "active",
      "questionKey": "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "topicKey": null,
      "entityKey": null,
      "contextHash": "context:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
      "citedUrls": ["https://example.com/guide/"]
    }
  ]
}
```

Allowed collection methods: `api|export|manual`. Null identity fields are permitted, but schema `anyOf` requires one non-null key.

- [ ] **Step 4: Define report semantics**

Report requires source counts by provider/type/status, active cited route counts, source records, unmatched/ambiguous cited URLs and join quality. It contains no score, rank, sentiment or recommendation field.

### Task 2: Implement the strict local reader

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoAuthorityInsights/SeoAuthorityInsightsModels.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoAuthorityInsights/ExternalAuthorityObservationReader.cs`
- Create: `tests/Bukit.Cli.Tests/ExternalAuthorityObservationReaderTests.cs`

**Interfaces:**
- Produces: source-generated dataset/row records and local reader.

- [ ] **Step 1: Write RED tests**

Cover remote/oversize/malformed input, duplicate and unknown fields, invalid
source/status, missing all identity keys, invalid URLs/hashes, duplicate cited
URLs, deleted row retention, `observedAt > collectedAt` rejection, and row cap.

- [ ] **Step 2: Run CLI tests and confirm RED**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

- [ ] **Step 3: Implement strict parse and semantic validation**

Reject unknown and duplicate fields before deserialization. Require local file
paths and 50 MiB maximum. Reject a row whose `observedAt` is later than the
dataset `collectedAt`. Preserve provider/source strings exactly after trimming;
do not infer platform, language, authority or sentiment from URLs.

- [ ] **Step 4: Run CLI tests to GREEN**

### Task 3: Join cited site URLs and preserve source lifecycle

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoAuthorityInsights/ExternalAuthorityReportWriter.cs`
- Create: `tests/Bukit.Cli.Tests/ExternalAuthorityReportWriterTests.cs`

**Interfaces:**
- Consumes: existing route matcher, rule profile and authority datasets.
- Produces: `ExternalAuthorityReport` and `.bukit/external-authority-report.json`.

- [ ] **Step 1: Write RED aggregation tests**

Prove active/deleted/unavailable counts, matched/unmatched/ambiguous cited URL preservation, multiple sources citing one route, one source citing multiple routes, no double count for duplicate URLs, byte-stable ordering and separate provider/type totals.

- [ ] **Step 2: Implement lifecycle-safe aggregation**

Keep every source record in evidence output. Only `status == active` contributes to current citation totals. Preserve deleted/unavailable records with their last observedAt so consumers can explain declines without erasing history.

- [ ] **Step 3: Run CLI tests to GREEN**

### Task 4: Add offline command, guide and plugin boundary

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoAuthorityInsightsCommand.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/SeoCommand.cs`
- Create: `tests/Bukit.Cli.Tests/SeoAuthorityInsightsCommandTests.cs`
- Modify: `tests/Bukit.Cli.Tests/CliContractTests.cs`
- Create: `guide/user/24-external-authority-insights.md`
- Modify: `guide/user/12-cli-reference.md`
- Modify: `guide/user/README.md`
- Modify: `tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`

**Interfaces:**
- Produces: `bukit seo authority-insights --route-map dist/.bukit/seo-route-map.json --rules observations/seo-insights-rules.json --observations observations/external-authority.json --out dist/.bukit/external-authority-report.json`.

- [ ] **Step 1: Add CLI RED tests**

Assert help, repeatable local observations, rules-based allowed site hosts, default output, atomic write, strict join exit `1`, invalid input exit `2`, and unchanged existing SEO commands.

- [ ] **Step 2: Implement composition-only command**

Reuse existing readers/matchers/output safety. Do not add a generic HTTP client or plugin invocation from Core.

- [ ] **Step 3: Document the Reddit decision gate**

Guide states that a future `Bukit.Plugin.RedditObserve` may be proposed only after:

```text
1. approved API use case and credentials boundary;
2. measured incremental value over GSC/GA4/generative observations;
3. fixed subreddit/query scope, rate and retention policy;
4. deletion/unavailable synchronization;
5. read-only commands only;
6. output validates against external-authority-observation.v1.
```

The guide explicitly forbids automated posting, commenting, voting, messaging and account creation in this workflow.

- [ ] **Step 4: Run specialty tests serially**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

- [ ] **Step 5: Review and commit WP2-C**

Review raw-content exclusion, lifecycle accounting, absence of scoring/network code, and explicit plugin boundary. Commit:

```bash
git commit -m "feat(seo): report external citation evidence"
```
