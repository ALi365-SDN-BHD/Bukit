# Bukit Core SEO Observability Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 Bukit-Core 建立离线、版本化、可审计的 SEO 观测基础设施，使外部插件能够把 GSC/GA4 页面数据稳定连接到 Bukit 路由，并生成带连接质量和可配置 P0/P1/P2 候选诊断的报告。

**Architecture:** `bukit build` 只生成确定性的 `.bukit/seo-route-map.json`，不访问外部网络。外部采集器或插件把 GSC/GA4 数据转换成 `seo-observation.v1` 文件；新的离线 `bukit seo insights` 子命令负责严格验证、URL 归一化、路由连接、覆盖率统计和规则评估，生成 `.bukit/seo-insights-report.json`。Google OAuth、凭据、API 分页、缓存、调度和通知保持在 Core 之外。

**Tech Stack:** .NET 10, Native AOT, `System.Text.Json` source generation, SHA-256, JSON Schema Draft 2020-12, xUnit, Bukit CLI binding, Bukit verification-closure workflow.

## Global Constraints

- `bukit build`、`bukit seo audit` 和现有 `seo-report.v1` 的行为及退出码保持不变。
- Core 不包含 Google OAuth、服务账号、令牌存储、GSC/GA4 HTTP 客户端、后台服务、数据库、定时器或通知代码。
- `bukit seo insights` 只读取本地文件；同一组输入必须生成字段顺序和路由顺序稳定的报告。
- 新增 JSON 必须使用 `System.Text.Json` source generation；禁止依赖运行时反射序列化，保持 Native AOT 兼容。
- 原始 CMS/Notion 标识不得出现在 `routeKey`、`contentKey`、日志、CLI 输出或公开文档示例中。`routeKey` 是当前 URL 观测身份，格式为 `route:sha256:<64hex>`；可选 `contentKey` 是跨改址内容连续性身份，格式为 `content:sha256:<64hex>`。
- 任何输入 JSON 中的未知字段、错误 schema、负数指标、越界比率、结束日期早于开始日期、`clicks > impressions` 或 `engagedSessions > sessions` 都必须失败关闭，退出码为 `2`。
- URL 归一化只移除规则文件明确列出的跟踪参数；禁止删除全部查询参数。
- 跨主机 URL 只有在主机位于 `siteHost` 或规则文件的 `hostAliases` 中时才可连接。
- 一条观测只能产生 `matched`、`unmatched` 或 `ambiguous` 三种连接结果之一；禁止静默丢弃。
- 候选诊断必须使用 `hypothesis` 和 `suggestedAction`，不得将相关性描述为已证明根因，不得自动修改页面。
- 规则阈值由 `seo-insights-rules.v1.json` 提供；Core 不内置电商转化率、排名 8–15、四秒首屏等业务常数。
- 默认输出目录为 `dist/.bukit/`；SEO 观测文件不进入公开发布目录或 `agent-manifest.json`。
- 外部观测失败不得阻止正常 `bukit build`；只有显式执行 `bukit seo insights` 才验证这些输入。
- 保留 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/`，不得读取它们作为当前实现依据或修改它们。
- 实施前必须重新读取根 `AGENTS.md`，生成每项任务的 verification closure，并在 `unmappedFiles` 非空时停止。
- 实施使用一个写者；通过 `/tmp/codex-reports/bukit-seo-observability-writer.json` 管理 `writing`、`testing`、`review_wait` 和 `done|blocked` 状态。
- 所有 closure 中的 `dotnet-serial` 命令串行执行；不得运行 `scripts/test-all.sh`、`scripts/smoke-all.sh`、whole-solution、full、release、`ci-fast` 或未被 closure 返回的门禁。
- 每项任务只做一次 specialty review；Critical/Important 发现才重进实现和复审，Minor 记录但不扩展范围。
- 每项任务一个独立提交；不得暂存或提交已有的 `docs/analysis/bukit-full-deep-audit-2026-07-31.zh-CN.md`。
- 执行阶段先使用 `superpowers:using-git-worktrees` 创建隔离 worktree；计划文档所在当前 `main` 工作区及其已有未跟踪文件不得成为实现写入目标。

## Public Contracts

### `seo-route-map.v1`

```json
{
  "schema": "https://bukit.dev/schemas/seo-route-map.v1.json",
  "schemaVersion": "1.0",
  "generatedAt": "2026-08-03T00:00:00Z",
  "siteUrl": "",
  "baseUrl": "/",
  "routes": [
    {
      "routeKey": "route:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "contentKey": "content:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
      "route": "/insights/example/",
      "canonical": "/insights/example/",
      "language": "zh-CN",
      "contentType": "article",
      "collection": "insights",
      "indexable": true,
      "publishedAt": "2026-08-01T00:00:00Z",
      "updatedAt": "2026-08-02T00:00:00Z"
    }
  ]
}
```

### `seo-observation.v1`

```json
{
  "schema": "https://bukit.dev/schemas/seo-observation.v1.json",
  "schemaVersion": "1.0",
  "provider": "google-search-console",
  "scope": "google-organic",
  "collectedAt": "2026-08-03T00:00:00Z",
  "window": {
    "startDate": "2026-07-01",
    "endDate": "2026-07-28",
    "timeZone": "Asia/Kuala_Lumpur"
  },
  "rows": [
    {
      "url": "https://example.com/insights/example/?utm_source=newsletter",
      "impressions": 1000,
      "clicks": 20,
      "averagePosition": 9.4
    }
  ]
}
```

GA4 输入使用同一个 schema，`provider` 为 `google-analytics-4`，行指标为 `sessions`、`engagedSessions` 和 `keyEvents`。CTR、engagement rate 和 key-event rate 由 Core 计算，不接受输入文件直接声明。

### `seo-insights-rules.v1`

```json
{
  "schema": "https://bukit.dev/schemas/seo-insights-rules.v1.json",
  "schemaVersion": "1.0",
  "siteHost": "example.com",
  "hostAliases": ["www.example.com"],
  "ignoredQueryParameters": [
    "utm_source",
    "utm_medium",
    "utm_campaign",
    "utm_term",
    "utm_content",
    "gclid",
    "fbclid"
  ],
  "thresholds": {
    "minimumSearchImpressions": 100,
    "maximumLowImpressions": 20,
    "minimumAnalyticsSessions": 20,
    "lowCtr": 0.02,
    "lowEngagementRate": 0.4,
    "highEngagementRate": 0.65,
    "opportunityPositionMinimum": 8.0,
    "opportunityPositionMaximum": 15.0
  },
  "priorities": {
    "snippetMismatch": "P1",
    "landingQuality": "P0",
    "discoverability": "P1",
    "positionOpportunity": "P2"
  }
}
```

数值只是文档示例；Core 只有在用户提供规则文件时才评估，不把示例当成默认值。

### `seo-insights-report.v1`

报告顶层固定包含 `schema`、`schemaVersion`、`generatedAt`、`window`、`sources`、`joinQuality`、`routes`、`unmatched` 和 `ambiguous`。`joinQuality` 同时提供总体和每个 provider 的 `sourceRows`、`matchedRows`、`unmatchedRows`、`ambiguousRows`；每一行都被计数一次。

---

### Task 0: Close the workflow-policy gap before Core implementation

**Files:**
- Modify: `scripts/checks/codex-workflow-self-test.sh`
- Modify: `scripts/checks/codex-workflow-policy.v1.json`

**Interfaces:**
- Consumes: the repository requirement that every planned path maps to an exact specialty test.
- Produces: a narrow `seo-observability-contract` path rule covering the four new schemas and active user-guide files; all later closure calls must report `unmappedFiles: []`.

- [ ] **Step 1: Initialize the single-writer state**

```bash
python3 scripts/checks/codex-workflow.py queue init \
  --state /tmp/codex-reports/bukit-seo-observability-writer.json
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/codex-reports/bukit-seo-observability-writer.json \
  --task seo-observability-task-0
```

Expected: Task 0 owns the writer state in `writing`.

- [ ] **Step 2: Generate the owner closure**

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed scripts/checks/codex-workflow-self-test.sh \
  --changed scripts/checks/codex-workflow-policy.v1.json
```

Expected specialty test: `bash scripts/checks/codex-workflow-self-test.sh`; `unmappedFiles` is empty.

- [ ] **Step 3: Add a failing self-test for the new path rule**

Add a fixture invocation that supplies these four representative paths:

```text
docs/schemas/seo-route-map.v1.schema.json
docs/schemas/seo-observation.v1.schema.json
docs/schemas/seo-insights-rules.v1.schema.json
guide/user/21-seo-insights.md
```

Assert that the closure output contains:

```json
"unmappedFiles": []
```

and contains:

```json
"dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj"
```

- [ ] **Step 4: Run the self-test and confirm RED**

```bash
bash scripts/checks/codex-workflow-self-test.sh
```

Expected: FAIL only because the four SEO observability documentation paths are not mapped.

- [ ] **Step 5: Add the narrow policy rule**

Add this object to `pathRules`:

```json
{
  "contractConsumerGlobs": [
    "tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs"
  ],
  "id": "seo-observability-contract",
  "matches": [
    "docs/schemas/seo-route-map.v1.schema.json",
    "docs/schemas/seo-observation.v1.schema.json",
    "docs/schemas/seo-insights-rules.v1.schema.json",
    "docs/schemas/seo-insights-report.v1.schema.json",
    "guide/user/10-built-in-outputs.md",
    "guide/user/12-cli-reference.md",
    "guide/user/21-seo-insights.md",
    "guide/user/README.md"
  ],
  "publicContract": true,
  "resource": "dotnet-serial",
  "specialtyTests": [
    "dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj"
  ]
}
```

- [ ] **Step 6: Run the owner self-test and confirm GREEN**

```bash
bash scripts/checks/codex-workflow-self-test.sh
```

Expected: exit `0`.

- [ ] **Step 7: Record evidence and perform the one specialty review**

Record the exact command under `/tmp/codex-reports/seo-observability-task-0-workflow.json`, transition the queue to `review_wait`, and review only the two changed workflow files. Critical/Important count must be zero before completion.

- [ ] **Step 8: Commit Task 0**

```bash
git add scripts/checks/codex-workflow-self-test.sh \
  scripts/checks/codex-workflow-policy.v1.json
git diff --cached --name-only
git commit -m "test(workflow): map seo observability contracts"
```

Expected staged paths: exactly the two files above. Transition Task 0 to `done`.

---

### Task 1: Emit the SEO route and content identity map

**Files:**
- Create: `src/Bukit-Core/Bukit.Engine/SeoObservability/SeoRouteMapModels.cs`
- Create: `src/Bukit-Core/Bukit.Engine/SeoObservability/SeoObservationIdentity.cs`
- Create: `src/Bukit-Core/Bukit.Engine/SeoObservability/SeoRouteMapBuilder.cs`
- Create: `src/Bukit-Core/Bukit.Engine/SeoObservability/SeoRouteMapWriter.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/MachineReadabilityTrustAuditBuilder.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/MachineReadabilityTrustAuditBuilder.Core.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/SeoAuditReportWriter.cs`
- Create: `docs/schemas/seo-route-map.v1.schema.json`
- Create: `tests/Bukit.Engine.Tests/SeoObservationIdentityTests.cs`
- Create: `tests/Bukit.Engine.Tests/SeoRouteMapWriterTests.cs`
- Modify: `tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`

**Interfaces:**
- Consumes: `SeoIndexEntry`, `SeoModel`, optional `ContentRecord`, and the build snapshot timestamp already used by `SeoAuditReport`.
- Produces: `SeoRouteMap`, `SeoRouteMapEntry`, `SeoObservationIdentity.CreateRouteKey(string route, string canonical)`, `SeoObservationIdentity.CreateContentKey(ContentRecord? record, string language)`, and `.bukit/seo-route-map.json`.

- [ ] **Step 1: Acquire Task 1 and generate its closure**

Acquire `seo-observability-task-1`, then run `closure` with every Task 1 path listed above. Expected: `unmappedFiles: []`; exact specialty tests are Engine and Architecture test projects, both classified `dotnet-serial` and therefore run serially.

```bash
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/codex-reports/bukit-seo-observability-writer.json \
  --task seo-observability-task-1
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed src/Bukit-Core/Bukit.Engine/SeoObservability/SeoRouteMapModels.cs \
  --changed src/Bukit-Core/Bukit.Engine/SeoObservability/SeoObservationIdentity.cs \
  --changed src/Bukit-Core/Bukit.Engine/SeoObservability/SeoRouteMapBuilder.cs \
  --changed src/Bukit-Core/Bukit.Engine/SeoObservability/SeoRouteMapWriter.cs \
  --changed src/Bukit-Core/Bukit.Engine/MachineReadabilityTrustAuditBuilder.cs \
  --changed src/Bukit-Core/Bukit.Engine/MachineReadabilityTrustAuditBuilder.Core.cs \
  --changed src/Bukit-Core/Bukit.Engine/SeoAuditReportWriter.cs \
  --changed docs/schemas/seo-route-map.v1.schema.json \
  --changed tests/Bukit.Engine.Tests/SeoObservationIdentityTests.cs \
  --changed tests/Bukit.Engine.Tests/SeoRouteMapWriterTests.cs \
  --changed tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
```

- [ ] **Step 2: Write failing observation-identity tests**

The tests must prove:

```csharp
[Fact]
public void CreateRouteKey_ChangesAcrossRouteChanges()
{
    var first = SeoObservationIdentity.CreateRouteKey("/old/", "/old/");
    var second = SeoObservationIdentity.CreateRouteKey("/new/", "/new/");

    Assert.NotEqual(first, second);
    Assert.Matches("^route:sha256:[0-9a-f]{64}$", first);
}

[Fact]
public void CreateContentKey_IsStableAcrossRouteChangesAndDistinguishesLanguages()
{
    var record = TestRecord(id: "internal-id", language: "zh-CN");
    var first = SeoObservationIdentity.CreateContentKey(record, "zh-CN");
    var second = SeoObservationIdentity.CreateContentKey(record, "zh-CN");

    Assert.Equal(first, second);
    Assert.Matches("^content:sha256:[0-9a-f]{64}$", first);
    Assert.DoesNotContain("internal-id", first, StringComparison.Ordinal);
    Assert.NotEqual(
        first,
        SeoObservationIdentity.CreateContentKey(record, "en"));
}
```

Also prove `CreateContentKey(null, language)` returns `null`, and that route identity uses both route and canonical deterministically.

- [ ] **Step 3: Run Engine tests and confirm RED**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
```

Expected: compilation failure because `SeoObservationIdentity` does not exist.

- [ ] **Step 4: Implement the dual identity primitive**

Use this exact identity material:

```csharp
var routeMaterial = $"route\0{route}\0{canonical}";
var contentMaterial = $"content\0{record.Identity.ContentType}\0{record.Identity.Id}\0{language}";
```

`CreateRouteKey` hashes `routeMaterial` and always returns the `route:` form. `CreateContentKey` returns `null` without a record; otherwise it hashes `contentMaterial` and returns the `content:` form. Reject blank route or canonical values. Do not log or serialize either material.

- [ ] **Step 5: Define the route-map models and source-generation context**

```csharp
internal sealed record SeoRouteMap(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    string SiteUrl,
    string BaseUrl,
    IReadOnlyList<SeoRouteMapEntry> Routes);

internal sealed record SeoRouteMapEntry(
    string RouteKey,
    string? ContentKey,
    string Route,
    string Canonical,
    string? Language,
    string? ContentType,
    string? Collection,
    bool Indexable,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? UpdatedAt);
```

Add a `JsonSerializerContext` with camelCase and indented output for `SeoRouteMap`.

- [ ] **Step 6: Write failing builder/writer tests**

Cover all of these conditions:

- routes sort by `canonical`, then `routeKey`, using ordinal comparison;
- route keys change when the route/canonical pair changes;
- duplicate normalized canonical entries are all preserved in deterministic order so the offline matcher can report ambiguity;
- Notion/internal source IDs do not appear in serialized JSON;
- the file path is exactly `.bukit/seo-route-map.json`;
- the schema is exactly `https://bukit.dev/schemas/seo-route-map.v1.json`;
- `collection`, published and updated timestamps come from `ContentRecord` when present;
- derived/static routes without a record remain representable with nullable content fields.

- [ ] **Step 7: Build and write the route map**

`SeoRouteMapBuilder` must create one entry inside the existing SEO route loop, where `record`, `entry`, and `model` are already resolved. Extend `MachineReadabilityTrustAuditResult` with `SeoRouteMap RouteMap`. Reuse `seoReport.GeneratedAt` as the route-map `generatedAt`, then make `SeoAuditReportWriter.WriteReport` call:

```csharp
SeoRouteMapWriter.Write(outputDir, result.RouteMap);
```

Write through `FileWriter.WriteUtf8` under `BuildReporter.ReportDirectoryName`; do not add it to the public projection registry.

- [ ] **Step 8: Add the strict JSON Schema**

`docs/schemas/seo-route-map.v1.schema.json` must use Draft 2020-12, `additionalProperties: false`, and the exact `$id`. `siteUrl` accepts either an empty string or an absolute HTTP(S) URL; `canonical` accepts either an absolute HTTP(S) URL or a leading-slash relative path. Require the leading-slash route pattern and `^route:sha256:[0-9a-f]{64}$`; allow nullable/optional `contentKey` only when it matches `^content:sha256:[0-9a-f]{64}$`. Do not impose canonical uniqueness: duplicate canonical entries remain valid ambiguity evidence.

- [ ] **Step 9: Run Task 1 specialty tests serially**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

Expected: both exit `0`; `.bukit/seo-route-map.json` tests pass and existing `seo-report.v1` tests remain unchanged.

- [ ] **Step 10: Record, review and commit Task 1**

Record both GREEN commands separately, conduct one Engine/schema specialty review, then commit only Task 1 files:

```bash
git add \
  src/Bukit-Core/Bukit.Engine/SeoObservability/SeoRouteMapModels.cs \
  src/Bukit-Core/Bukit.Engine/SeoObservability/SeoObservationIdentity.cs \
  src/Bukit-Core/Bukit.Engine/SeoObservability/SeoRouteMapBuilder.cs \
  src/Bukit-Core/Bukit.Engine/SeoObservability/SeoRouteMapWriter.cs \
  src/Bukit-Core/Bukit.Engine/MachineReadabilityTrustAuditBuilder.cs \
  src/Bukit-Core/Bukit.Engine/MachineReadabilityTrustAuditBuilder.Core.cs \
  src/Bukit-Core/Bukit.Engine/SeoAuditReportWriter.cs \
  docs/schemas/seo-route-map.v1.schema.json \
  tests/Bukit.Engine.Tests/SeoObservationIdentityTests.cs \
  tests/Bukit.Engine.Tests/SeoRouteMapWriterTests.cs \
  tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
git diff --cached --name-only
git commit -m "feat(seo): emit stable route observation map"
```

---

### Task 2: Normalize observed URLs and match routes without changing routing semantics

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoObservationUrlNormalizer.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoObservationRouteMatcher.cs`
- Create: `tests/Bukit.Cli.Tests/SeoObservationUrlNormalizerTests.cs`
- Create: `tests/Bukit.Cli.Tests/SeoObservationRouteMatcherTests.cs`

**Interfaces:**
- Consumes: `SeoRouteMap`, `siteHost`, `hostAliases`, and a case-insensitive set of ignored query-parameter names.
- Produces: `SeoObservationUrlNormalizer.Normalize(string value, SeoObservationUrlOptions options)` with both normalized full URL and a host-independent path/query `MatchKey`, plus `SeoObservationRouteMatcher.Match(string observedUrl)` returning `Matched`, `Unmatched`, or `Ambiguous` and complete candidate route entries.

- [ ] **Step 1: Acquire Task 2 and generate its closure**

Expected exact specialty command: `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj`; resource classification: `dotnet-serial`; `unmappedFiles: []`.

```bash
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/codex-reports/bukit-seo-observability-writer.json \
  --task seo-observability-task-2
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoObservationUrlNormalizer.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoObservationRouteMatcher.cs \
  --changed tests/Bukit.Cli.Tests/SeoObservationUrlNormalizerTests.cs \
  --changed tests/Bukit.Cli.Tests/SeoObservationRouteMatcherTests.cs
```

- [ ] **Step 2: Write URL-normalization tests**

Include exact cases:

```csharp
[Theory]
[InlineData(
    "HTTPS://EXAMPLE.COM:443/a/?utm_source=x&b=2#part",
    "https://example.com/a/?b=2")]
[InlineData(
    "https://example.com/%E9%A9%AC%E6%9D%A5%E8%A5%BF%E4%BA%9A",
    "https://example.com/%E9%A9%AC%E6%9D%A5%E8%A5%BF%E4%BA%9A/")]
public void Normalize_ProducesCanonicalObservationKey(string input, string expected)
```

Also prove:

- non-tracking query parameters are preserved and sorted by name/value;
- parameter names are compared case-insensitively;
- fragments are removed;
- default ports are removed;
- credentials, `ftp:`, malformed URLs and unapproved hosts are rejected;
- an explicit `www.example.com` alias is accepted, but an undeclared host is not;
- an extension path such as `/feed.xml` does not gain a trailing slash;
- existing `RoutePathBuilder.NormalizeUrl()` tests remain untouched.

- [ ] **Step 3: Run CLI tests and confirm RED**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

Expected: missing normalizer/matcher types.

- [ ] **Step 4: Implement normalization as a separate SEO Insights component**

Define:

```csharp
internal sealed record SeoObservationUrlOptions(
    string SiteHost,
    IReadOnlySet<string> HostAliases,
    IReadOnlySet<string> IgnoredQueryParameters);

internal sealed record SeoObservationUrlNormalizationResult(
    bool Success,
    string? NormalizedUrl,
    string? MatchKey,
    string? ErrorCode);
```

Use `Uri`/`UriBuilder`; normalize scheme/IDN host/default port, remove fragments, filter only configured query names, sort retained query pairs, and apply a trailing slash only to extensionless paths. `MatchKey` is the normalized path plus retained query and therefore lets a relative route-map canonical match an absolute observed URL. Validate absolute observed hosts against `siteHost`/`hostAliases`; route-map canonical values may be relative. Error codes are fixed values: `invalid_url`, `unsupported_scheme`, `credentials_not_allowed`, `host_not_allowed`.

- [ ] **Step 5: Write matcher tests**

Prove:

- exact canonical match returns one route entry;
- tracking-parameter and declared-host-alias variants match the same key;
- no candidate returns `Unmatched`;
- duplicate normalized canonical values return `Ambiguous` with sorted candidate route entries, including each route key and optional content key;
- ambiguous results never select the first candidate;
- matcher construction does not mutate route-map entries.

- [ ] **Step 6: Implement the immutable route index**

```csharp
internal enum SeoObservationMatchKind { Matched, Unmatched, Ambiguous }

internal sealed record SeoObservationRouteCandidate(
    string RouteKey,
    string? ContentKey,
    string Route,
    string Canonical);

internal sealed record SeoObservationRouteMatch(
    SeoObservationMatchKind Kind,
    string ObservedUrl,
    string? NormalizedUrl,
    string? RouteKey,
    string? ContentKey,
    IReadOnlyList<SeoObservationRouteCandidate> Candidates,
    string? ErrorCode);
```

Build an ordinal dictionary from host-independent canonical `MatchKey` to sorted immutable candidates. Duplicate canonical values are valid and produce `Ambiguous`; only `bukit seo insights --strict-join` converts ambiguity into exit `1`. Normalization errors become `Unmatched` with `ErrorCode`; they do not throw unless the route-map schema itself is invalid.

- [ ] **Step 7: Run, record, review and commit Task 2**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
git add \
  src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoObservationUrlNormalizer.cs \
  src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoObservationRouteMatcher.cs \
  tests/Bukit.Cli.Tests/SeoObservationUrlNormalizerTests.cs \
  tests/Bukit.Cli.Tests/SeoObservationRouteMatcherTests.cs
git diff --cached --name-only
git commit -m "feat(seo): normalize and match observation URLs"
```

Expected: CLI specialty project passes; specialty review has zero Critical/Important findings.

---

### Task 3: Validate observation datasets and report join quality

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsModels.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoObservationDatasetReader.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsReportWriter.cs`
- Create: `docs/schemas/seo-observation.v1.schema.json`
- Create: `docs/schemas/seo-insights-report.v1.schema.json`
- Create: `tests/Bukit.Cli.Tests/SeoObservationDatasetReaderTests.cs`
- Create: `tests/Bukit.Cli.Tests/SeoInsightsReportWriterTests.cs`
- Modify: `tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`

**Interfaces:**
- Consumes: `seo-route-map.v1`, one or more `seo-observation.v1` datasets, and `SeoObservationRouteMatcher`.
- Produces: validated observation models, route-aggregated metrics, provider-level and overall join-quality counts, unmatched/ambiguous evidence, and `seo-insights-report.v1` without priority findings yet.

- [ ] **Step 1: Acquire Task 3 and generate its closure**

Expected specialty projects: CLI and Architecture, serialized as `dotnet-serial`; `unmappedFiles: []`.

```bash
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/codex-reports/bukit-seo-observability-writer.json \
  --task seo-observability-task-3
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsModels.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoObservationDatasetReader.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsReportWriter.cs \
  --changed docs/schemas/seo-observation.v1.schema.json \
  --changed docs/schemas/seo-insights-report.v1.schema.json \
  --changed tests/Bukit.Cli.Tests/SeoObservationDatasetReaderTests.cs \
  --changed tests/Bukit.Cli.Tests/SeoInsightsReportWriterTests.cs \
  --changed tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
```

- [ ] **Step 2: Define source-generated models**

Use explicit types:

```csharp
internal sealed record SeoObservationWindow(
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZone);

internal sealed record SeoObservationRow(
    string Url,
    long? Impressions,
    long? Clicks,
    double? AveragePosition,
    long? Sessions,
    long? EngagedSessions,
    long? KeyEvents);

internal sealed record SeoObservationDataset(
    string Schema,
    string SchemaVersion,
    string Provider,
    string Scope,
    DateTimeOffset CollectedAt,
    SeoObservationWindow Window,
    IReadOnlyList<SeoObservationRow> Rows);
```

Add source-generation entries for every input/output/nested type. Provider values are exactly `google-search-console` and `google-analytics-4`; scope is exactly `google-organic` for v1.

- [ ] **Step 3: Write strict-reader tests**

Test valid GSC and GA4 files, then reject each condition independently:

- wrong schema or schema version;
- unknown property;
- file larger than 50 MiB;
- more than 100,000 rows per dataset;
- negative metrics or non-finite doubles;
- `clicks > impressions`;
- `engagedSessions > sessions`;
- GSC row missing impressions/clicks/position;
- GA4 row missing sessions/engagedSessions/keyEvents;
- provider-specific foreign metrics;
- end date before start date;
- blank timezone, provider, scope or URL.

- [ ] **Step 4: Run CLI tests and confirm RED**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

- [ ] **Step 5: Implement strict deserialization and semantic validation**

Use `JsonDocument` first to enforce allowed properties at every object level, then deserialize with the generated context. Return `InvalidDataException` with stable codes such as `observation.schema_invalid`, `observation.metric_invalid`, and `observation.window_invalid`; exception messages must not include the complete input JSON.

- [ ] **Step 6: Write join-quality aggregation tests**

Create a fixture with:

```text
GSC: 4 rows -> matched 2, unmatched 1, ambiguous 1
GA4: 3 rows -> matched 2, unmatched 1, ambiguous 0
Overall: 7 rows -> matched 4, unmatched 2, ambiguous 1
```

Assert every row is counted once. For multiple matched rows on one route:

- sum impressions, clicks, sessions, engaged sessions and key events;
- compute CTR as `clicks / impressions`;
- compute engagement rate as `engagedSessions / sessions`;
- compute key-event rate as `keyEvents / sessions`;
- compute average position weighted by impressions;
- return `null`, not `NaN` or infinity, when a denominator is zero.

- [ ] **Step 7: Implement report assembly and deterministic writing**

Sort route results by canonical and route key; sort unmatched rows by provider/normalized URL/original URL and ambiguous rows by the same keys. `generatedAt` is the maximum `collectedAt` from input datasets. Reject mismatched observation windows for v1. Task 4 adds findings and priority ordering without changing the underlying route ordering.

- [ ] **Step 8: Add strict observation and report schemas**

Both schemas use Draft 2020-12, `additionalProperties: false`, explicit required fields, provider-specific `oneOf` row definitions, non-negative constraints, maximum `1` for CTR and engagement rate, non-negative unbounded `keyEventRate` because GA4 key events may exceed sessions, and route-key patterns. Add architecture tests that read the schema files and assert `$id`, required fields, provider enum and join-quality fields.

- [ ] **Step 9: Run, record, review and commit Task 3**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
git add \
  src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsModels.cs \
  src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoObservationDatasetReader.cs \
  src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsReportWriter.cs \
  docs/schemas/seo-observation.v1.schema.json \
  docs/schemas/seo-insights-report.v1.schema.json \
  tests/Bukit.Cli.Tests/SeoObservationDatasetReaderTests.cs \
  tests/Bukit.Cli.Tests/SeoInsightsReportWriterTests.cs \
  tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
git diff --cached --name-only
git commit -m "feat(seo): join observations with quality evidence"
```

---

### Task 4: Evaluate configurable priority candidates without claiming root cause

**Files:**
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsModels.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsReportWriter.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsRuleProfileReader.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsRuleEvaluator.cs`
- Create: `docs/schemas/seo-insights-rules.v1.schema.json`
- Modify: `docs/schemas/seo-insights-report.v1.schema.json`
- Create: `tests/Bukit.Cli.Tests/SeoInsightsRuleProfileReaderTests.cs`
- Create: `tests/Bukit.Cli.Tests/SeoInsightsRuleEvaluatorTests.cs`
- Modify: `tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`

**Interfaces:**
- Consumes: aggregated route metrics and a validated `seo-insights-rules.v1` profile.
- Produces: zero or more `SeoInsightsFinding` records per route, each with fixed code, configured priority, evidence, hypothesis and suggested action.

- [ ] **Step 1: Acquire Task 4 and generate its closure**

Expected specialty projects: CLI and Architecture, serial; `unmappedFiles: []`.

```bash
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/codex-reports/bukit-seo-observability-writer.json \
  --task seo-observability-task-4
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsModels.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsReportWriter.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsRuleProfileReader.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsRuleEvaluator.cs \
  --changed docs/schemas/seo-insights-rules.v1.schema.json \
  --changed docs/schemas/seo-insights-report.v1.schema.json \
  --changed tests/Bukit.Cli.Tests/SeoInsightsRuleProfileReaderTests.cs \
  --changed tests/Bukit.Cli.Tests/SeoInsightsRuleEvaluatorTests.cs \
  --changed tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
```

- [ ] **Step 2: Define and validate the rule profile**

```csharp
internal sealed record SeoInsightsThresholds(
    long MinimumSearchImpressions,
    long MaximumLowImpressions,
    long MinimumAnalyticsSessions,
    double LowCtr,
    double LowEngagementRate,
    double HighEngagementRate,
    double OpportunityPositionMinimum,
    double OpportunityPositionMaximum);

internal sealed record SeoInsightsPriorities(
    string SnippetMismatch,
    string LandingQuality,
    string Discoverability,
    string PositionOpportunity);
```

Validate priority values against `P0|P1|P2`; ratios are `[0,1]`; all counts are non-negative; minimum position is positive and not greater than maximum; site host and aliases are DNS hosts without scheme, port, path, credentials, query or fragment; ignored parameter names match `^[A-Za-z0-9_.-]+$`.

- [ ] **Step 3: Write rule tests before implementation**

Cover exactly four v1 findings:

```text
seo.insights.snippet_mismatch
  impressions >= minimumSearchImpressions
  ctr < lowCtr
  sessions >= minimumAnalyticsSessions
  engagementRate >= highEngagementRate

seo.insights.landing_quality
  sessions >= minimumAnalyticsSessions
  engagementRate < lowEngagementRate

seo.insights.discoverability
  impressions <= maximumLowImpressions
  sessions >= minimumAnalyticsSessions
  engagementRate >= highEngagementRate

seo.insights.position_opportunity
  impressions >= minimumSearchImpressions
  averagePosition within the inclusive configured interval
```

Assert boundary inclusivity, missing-metric suppression, stable priority ordering `P0`, `P1`, `P2`, and no duplicate code for one route.

- [ ] **Step 4: Run CLI tests and confirm RED**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

- [ ] **Step 5: Implement evidence-backed findings**

```csharp
internal sealed record SeoInsightsEvidence(
    string Metric,
    double Actual,
    string Operator,
    double Threshold);

internal sealed record SeoInsightsFinding(
    string Code,
    string Priority,
    string RouteKey,
    IReadOnlyList<SeoInsightsEvidence> Evidence,
    string Hypothesis,
    string SuggestedAction);
```

Use fixed, cautious English messages. Example for snippet mismatch:

```text
hypothesis: Search presentation may not align with the intent of impressions reaching this route.
suggestedAction: Review the title and description against the observed queries before changing content.
```

Never emit “caused by”, “proved”, or an automatic-edit instruction.

- [ ] **Step 6: Add the rule schema, report finding contract and architecture assertions**

Use the contract shown above. The rule schema must require the user to provide every threshold and priority; no default rule profile is silently synthesized. Extend each route object in `seo-insights-report.v1.schema.json` with required `findings`, whose nested code/priority/routeKey/evidence/hypothesis/suggestedAction fields match the source-generated runtime model. Keep the report's existing eight top-level fields unchanged.

- [ ] **Step 7: Run, record, review and commit Task 4**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
git add \
  src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsModels.cs \
  src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsReportWriter.cs \
  src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsRuleProfileReader.cs \
  src/Bukit-Core/Bukit.Cli/Commands/SeoInsights/SeoInsightsRuleEvaluator.cs \
  docs/schemas/seo-insights-rules.v1.schema.json \
  docs/schemas/seo-insights-report.v1.schema.json \
  tests/Bukit.Cli.Tests/SeoInsightsRuleProfileReaderTests.cs \
  tests/Bukit.Cli.Tests/SeoInsightsRuleEvaluatorTests.cs \
  tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
git diff --cached --name-only
git commit -m "feat(seo): evaluate configurable insight priorities"
```

---

### Task 5: Expose the offline `bukit seo insights` command

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/SeoInsightsCommand.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/SeoCommand.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/CompletionCommand.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Program.cs`
- Modify: `src/Bukit-Core/Bukit.Cli.Shared/Cli/Rendering/CliHelpRenderer.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs`
- Create: `tests/Bukit.Cli.Tests/SeoInsightsCommandTests.cs`
- Modify: `tests/Bukit.Cli.Tests/CliContractTests.cs`
- Modify: `tests/Bukit.Cli.Tests/CompletionCommandTests.cs`
- Modify: `tests/Bukit.Cli.Tests/HelpPrinterTests.cs`

**Interfaces:**
- Consumes: route-map path, comma-separated observation-file paths, rules path, output path and strict-join flag.
- Produces: `bukit seo insights` CLI behavior and `.bukit/seo-insights-report.json`.

- [ ] **Step 1: Acquire Task 5 and generate its closure**

Expected exact specialty command: CLI project, classified `dotnet-serial`; `unmappedFiles: []`.

```bash
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/codex-reports/bukit-seo-observability-writer.json \
  --task seo-observability-task-5
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoInsightsCommand.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/SeoCommand.cs \
  --changed src/Bukit-Core/Bukit.Cli/Commands/CompletionCommand.cs \
  --changed src/Bukit-Core/Bukit.Cli/Program.cs \
  --changed src/Bukit-Core/Bukit.Cli.Shared/Cli/Rendering/CliHelpRenderer.cs \
  --changed src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs \
  --changed tests/Bukit.Cli.Tests/SeoInsightsCommandTests.cs \
  --changed tests/Bukit.Cli.Tests/CliContractTests.cs \
  --changed tests/Bukit.Cli.Tests/CompletionCommandTests.cs \
  --changed tests/Bukit.Cli.Tests/HelpPrinterTests.cs
```

- [ ] **Step 2: Add failing CLI contract tests**

Define this public command:

```text
bukit seo insights \
  --dir dist \
  --routes dist/.bukit/seo-route-map.json \
  --observations gsc.json,ga4.json \
  --rules seo-insights-rules.json \
  --out dist/.bukit/seo-insights-report.json \
  --strict-join
```

Options:

```text
--dir             default dist
--routes          default <dir>/.bukit/seo-route-map.json
--observations    required comma-separated local JSON paths
--rules           required local JSON path
--out             default <dir>/.bukit/seo-insights-report.json
--strict-join     exit 1 when unmatchedRows or ambiguousRows is non-zero
```

Exit codes:

```text
0 valid report written; join gaps allowed when strict mode is absent
1 valid report written but strict join failed
2 usage, missing file, invalid contract, invalid metric, invalid rule or write failure
```

- [ ] **Step 3: Run CLI tests and confirm RED**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

- [ ] **Step 4: Implement orchestration only**

`SeoInsightsCommand` must:

1. resolve paths without accessing the network;
2. require 1–10 observation files;
3. load route map and rules strictly;
4. load each observation dataset strictly;
5. require one common window;
6. normalize and match every row;
7. aggregate metrics and join quality;
8. evaluate findings;
9. write the report atomically;
10. print only counts, output path and exit classification.

CLI output example:

```text
SEO insights: sourceRows=7 matched=4 unmatched=2 ambiguous=1 findings=3
SEO insights report: /absolute/path/dist/.bukit/seo-insights-report.json
```

Do not print property IDs, observation row URLs, environment values or file contents.

- [ ] **Step 5: Add descriptor/help/completion coverage**

Add `insights` under the existing `seo` command without changing `audit` or `diff`. Assert registry resolution, real deepest-leaf help text, required/default option diagnostics, and Bash/Zsh/Fish completion output. Completion generation and help rendering must derive from the live registry rather than hardcoded Task 5 strings.

- [ ] **Step 6: Run, record, review and commit Task 5**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
git add \
  src/Bukit-Core/Bukit.Cli/Commands/SeoInsightsCommand.cs \
  src/Bukit-Core/Bukit.Cli/Commands/SeoCommand.cs \
  src/Bukit-Core/Bukit.Cli/Commands/CompletionCommand.cs \
  src/Bukit-Core/Bukit.Cli/Program.cs \
  src/Bukit-Core/Bukit.Cli.Shared/Cli/Rendering/CliHelpRenderer.cs \
  src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs \
  tests/Bukit.Cli.Tests/SeoInsightsCommandTests.cs \
  tests/Bukit.Cli.Tests/CliContractTests.cs \
  tests/Bukit.Cli.Tests/CompletionCommandTests.cs \
  tests/Bukit.Cli.Tests/HelpPrinterTests.cs
git diff --cached --name-only
git commit -m "feat(cli): add offline seo insights command"
```

---

### Task 6: Document the contracts, operating boundary and plugin handoff

**Files:**
- Create: `guide/user/21-seo-insights.md`
- Modify: `guide/user/README.md`
- Modify: `guide/user/10-built-in-outputs.md`
- Modify: `guide/user/12-cli-reference.md`
- Modify: `tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`

**Interfaces:**
- Consumes: the four schemas and final CLI contract.
- Produces: active user documentation that explains the offline Core boundary, provides valid examples and prevents `seo insights` from being mistaken for a Google connector or ranking guarantee.

- [ ] **Step 1: Acquire Task 6 and generate its closure**

Expected exact specialty command: Architecture project, classified `dotnet-serial`; `unmappedFiles: []`.

```bash
python3 scripts/checks/codex-workflow.py queue acquire \
  --state /tmp/codex-reports/bukit-seo-observability-writer.json \
  --task seo-observability-task-6
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed guide/user/21-seo-insights.md \
  --changed guide/user/README.md \
  --changed guide/user/10-built-in-outputs.md \
  --changed guide/user/12-cli-reference.md \
  --changed tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
```

- [ ] **Step 2: Write failing documentation-contract assertions**

Assert the active guide contains all of these exact concepts:

```text
seo-route-map.v1
seo-observation.v1
seo-insights-rules.v1
seo-insights-report.v1
bukit seo insights
offline
does not authenticate to Google
does not prove causation
unmatched
ambiguous
```

Also assert the README index links `21-seo-insights.md`, built-in outputs lists `.bukit/seo-route-map.json`, and CLI reference lists all six insights options.

- [ ] **Step 3: Run Architecture tests and confirm RED**

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

- [ ] **Step 4: Write the active guide**

The guide must include:

- purpose and non-goals;
- the architecture flow `build -> route map -> external collector/plugin -> observations -> insights`;
- one valid GSC dataset, one valid GA4 dataset and one complete rules file;
- CLI command and exit-code table;
- URL normalization behavior and tracking-parameter warning;
- join-quality interpretation;
- four candidate diagnostics with cautious meaning;
- privacy boundary: no credentials or raw CMS IDs in outputs/logs;
- explicit statement that Search Console API can omit low-volume rows and the report cannot prove ranking or causation;
- plugin handoff: collectors request explicit network/environment permissions and write only the local observation contract.

- [ ] **Step 5: Run, record, review and commit Task 6**

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
git add \
  guide/user/21-seo-insights.md \
  guide/user/README.md \
  guide/user/10-built-in-outputs.md \
  guide/user/12-cli-reference.md \
  tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs
git diff --cached --name-only
git commit -m "docs(seo): document observability workflow"
```

---

### Task 7: Perform delta-only final review and completion evidence

**Files:**
- Review only: all files committed by Tasks 0–6.
- Evidence only: `/tmp/codex-reports/bukit-seo-observability-*`.

**Interfaces:**
- Consumes: task commits, closure outputs, GREEN cache records and the current findings file.
- Produces: one delta-only unified review, metrics report and a truthful completion classification.

- [ ] **Step 1: Confirm repository scope**

```bash
git status --short
git log --oneline -7
```

Expected: only the pre-existing untracked audit file may remain outside the task commits; no generated `dist`, credential, observation fixture or report is staged.

- [ ] **Step 2: Build review scope**

Run `python3 scripts/checks/codex-workflow.py review-scope` with each Task 0–6 evidence file, the final findings JSON, and every changed path. Review only cross-task intersections, uncovered changed files, invalidated evidence, serialized/public contracts and open Critical/Important findings.

- [ ] **Step 3: Reuse valid GREEN evidence**

Use `cache check` before any repeat. Do not rerun unchanged Engine, CLI or Architecture projects when HEAD, closure, command, environment state and SDK still match the recorded evidence.

- [ ] **Step 4: Run the final unified review**

Focus on:

- route-key stability and privacy;
- JSON schema/model agreement;
- Native AOT source-generation completeness;
- URL normalization ambiguity and query preservation;
- every observation row accounted exactly once;
- provider/window validation;
- rule boundary conditions and cautious wording;
- old `seo audit`/`seo diff` compatibility;
- Core/plugin/network boundary;
- CLI exit-code truthfulness.

Critical and Important findings must be zero. Minor findings remain recorded and do not trigger a duplicate review.

- [ ] **Step 5: Report workflow metrics**

```bash
python3 scripts/checks/codex-workflow.py metrics report \
  --state /tmp/codex-reports/bukit-seo-observability-metrics.json
```

Report queue delay, cache hits/misses, reruns, conflicts and task completion states. Do not include raw commands, URLs, environment values or secrets in metric labels.

- [ ] **Step 6: Classify completion**

Use:

```text
success  = all four schemas, route-map emission, offline join, quality evidence,
           configurable findings, CLI/docs and required specialty evidence pass
partial  = implementation is usable but a named specialty proof or documented
           contract remains incomplete
blocked  = required proof cannot run or closure remains unmapped; do not claim GREEN
```

Do not run a full/release gate unless the user separately authorizes it.

## Acceptance Matrix

| Requirement | Acceptance evidence |
|---|---|
| Dual identity | `routeKey` changes with the current route/canonical pair; optional `contentKey` stays stable for the same content ID/language after a route change; raw ID is absent from both |
| Canonical route contract | Every SEO route is preserved in `seo-route-map.v1`; duplicate canonical values remain explicit and are reported as ambiguous instead of breaking `bukit build` |
| Offline boundary | Network-disabled `bukit seo insights` succeeds with local fixtures; build never reads observations |
| URL correctness | Tracking parameters removed only by explicit rules; retained query values remain sorted and encoded |
| Match truth | Every row is matched, unmatched or ambiguous exactly once; ambiguity never selects a winner |
| Metric integrity | Sums, weighted position and derived ratios match fixture arithmetic; zero denominators produce null |
| Input safety | Unknown fields, invalid metrics, provider mismatch and mismatched windows exit 2 |
| Priority configuration | All thresholds and P0/P1/P2 values come from the rules file |
| Diagnostic restraint | Findings contain hypothesis/evidence/action and never claim proven causation |
| Native AOT | All new JSON types are in generated serializer contexts; CLI specialty tests pass |
| Backward compatibility | Existing `seo audit`, `seo diff`, `seo-report.v1` and route normalization tests pass unchanged |
| Documentation | Active guide and schemas agree with CLI and architecture tests pass |
| Governance | All closure results have no unmapped files; only exact specialty commands run |

## Explicit Follow-On Work Outside This Plan

These are separate consumer projects and require separate approval/plans:

- a GSC collector implementing API pagination, quota, property selection and row-loss disclosure;
- a GA4 Data API collector producing `google-organic` landing-page observations;
- secret provisioning and Google Cloud authorization;
- scheduled weekly execution, historical storage, dashboarding and notifications;
- longitudinal comparison of two observation windows;
- non-Google search or AI citation monitoring;
- automatic page edits, deployment or publication.
