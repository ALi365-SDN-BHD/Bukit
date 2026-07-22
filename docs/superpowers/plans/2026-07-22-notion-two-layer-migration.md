# Bukit Notion Two-Layer Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move reusable Notion protocol, transport, read/write, conversion, and rendering behavior into `Bukit.Notion`, and move Bukit-specific content-source behavior into `Bukit.Content.Notion`, without changing 1.x consumer behavior.

**Architecture:** `Bukit.Notion` is BCL-only and owns Notion HTTP/protocol behavior. `Bukit.Content.Notion` depends on `Bukit.Notion`, `Bukit.Engine.Abstractions`, `Bukit.Config`, and `Bukit.Shared`, but not `Bukit.Content` or `Bukit.Engine`; the existing `Bukit.Content.Notion.NotionContentProvider` remains a 1.x facade implementing `IContentProvider`. Config mapping and composition remain in Engine, command presentation remains in CLI, and import orchestration remains in Importing.

**Tech Stack:** .NET 10, System.Net.Http, System.Text.Json/JsonDocument/Utf8JsonWriter, xUnit, Native AOT-compatible code.

## Global Constraints

- Do not change config schema, plugin protocol, Notion API version, public command output, asset URLs, content projection, relation-link shape, cache keys, or cache paths.
- `Bukit.Notion` must not reference any Bukit project and must not read environment variables.
- Non-idempotent POST/PATCH/DELETE requests must not be automatically replayed by a generic retry loop.
- Request authorization and `Notion-Version` headers must be set per request; never mutate shared `HttpClient.DefaultRequestHeaders` with a token.
- An injected `HttpClient` is never disposed by `Bukit.Notion`; a client constructed internally is disposed exactly once.
- Notion exceptions must not include response bodies, Authorization values, or unredacted secret-bearing URLs.
- Cancellation must propagate unchanged through transport, adapter, body rendering, and import paths.
- Throttle state is per `NotionClient` instance, never process-global.
- Continue using `JsonDocument` and `Utf8JsonWriter`; do not introduce reflection-based JSON serialization.
- Preserve 1.x source and binary compatibility through thin facades or type forwarders. Any residual old-assembly dependency must be documented for removal in 2.0.
- Each task runs its focused tests and `bash scripts/checks/post-change-focused.sh -- <changed paths>`. Run aggregate `post-change-targeted.sh` exactly once after N-07.

---

### Task N-01: Establish `Bukit.Notion` and remove heavy conversion logic from Shared

**Files:**
- Create: `src/Bukit-Core/Bukit.Notion/Bukit.Notion.csproj`
- Create: `tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj`
- Move implementation into: `src/Bukit-Core/Bukit.Notion/Conversion/`
- Modify: `src/Bukit-Core/Bukit.Shared/Bukit.Shared.csproj`
- Modify: `src/Bukit-Core/Bukit.Shared/Notion/*.cs`
- Modify: `bukit-core.slnx`
- Test: `tests/Bukit.Notion.Tests/*`
- Test: `tests/Bukit.Shared.Tests/*`

**Interfaces:**
- Produces: `Bukit.Notion.Conversion.HtmlToNotionBlockConverter.Convert(string)` and `ToBlocksJson(string)`.
- Preserves: `Bukit.Shared.Notion.HtmlToNotionBlockConverter`, `NotionBlock` records, and `NotionApiUrls` for 1.x consumers.

- [ ] Add a failing architecture test proving `Bukit.Notion.csproj` has no `ProjectReference` and no package dependency.
- [ ] Add failing converter compatibility tests comparing legacy and new entry points for headings, lists, rich text, images, callouts, toggles, tables, code blocks, empty containers, malformed attributes, and whitespace boundaries.
- [ ] Add failing regression tests for `<pre><code>`, empty container token advancement, exact attribute matching, and an attribute ending with `=`.
- [ ] Create the project and move the converter/tokenizer/block/writer implementation without changing JSON shape.
- [ ] Keep only compatibility facades/forwarders in Shared and prove legacy assembly-qualified type resolution.
- [ ] Run `Bukit.Notion.Tests`, `Bukit.Shared.Tests`, architecture tests, and the N-01 focused gate.

### Task N-02: Introduce the unified transport without unsafe write replay

**Files:**
- Create: `src/Bukit-Core/Bukit.Notion/Transport/NotionClientOptions.cs`
- Create: `src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs`
- Create: `src/Bukit-Core/Bukit.Notion/Transport/NotionApiException.cs`
- Create: `src/Bukit-Core/Bukit.Notion/Transport/NotionRequestSemantics.cs`
- Modify: `src/Bukit-Core/Bukit.Content/Notion/NotionApiClient.cs`
- Test: `tests/Bukit.Notion.Tests/NotionClientTests.cs`
- Test: `tests/Bukit.Content.Tests/NotionApiClientExtendedTests.cs`

**Interfaces:**
- Produces: `NotionClient.SendAsync(HttpRequestMessage, NotionRequestSemantics, CancellationToken)`.
- `NotionRequestSemantics` values are `IdempotentRead` and `NonReplayableWrite`.
- Existing `NotionApiClient.GetAsync/PostAsync` remains a compatibility facade.

- [ ] Add RED tests for 429 read retry, Retry-After, max-RPS scheduling, per-instance throttle isolation, cancellation, response disposal, and safe error messages.
- [ ] Add RED tests proving a 429 write response results in exactly one handler invocation.
- [ ] Add RED tests proving two clients sharing one `HttpClient` send different bearer tokens without cross-contamination.
- [ ] Add RED ownership tests proving injected clients remain usable after `NotionClient.Dispose`, while internally-owned handlers are disposed once.
- [ ] Implement the BCL-only transport with per-request headers and per-instance throttle state.
- [ ] Adapt the legacy Content client and translate `NotionApiException` to the existing `ContentException` messages.
- [ ] Run Notion, Content, architecture tests, and the N-02 focused gate.

### Task N-03: Move Doctor health and schema I/O onto `Bukit.Notion`

**Files:**
- Create: `src/Bukit-Core/Bukit.Notion/Diagnostics/NotionHealthClient.cs`
- Create: `src/Bukit-Core/Bukit.Notion/Diagnostics/NotionDatabaseSchema.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/DoctorNotionChecker.cs`
- Test: `tests/Bukit.Cli.Tests/DoctorCommandTests.cs`
- Test: `tests/Bukit.Notion.Tests/NotionHealthClientTests.cs`

**Interfaces:**
- Produces structured `NotionHealthResult` and `NotionDatabaseSchema` values with no console dependency.
- CLI remains responsible for exact symbols, wording, property-map comparison, and exit behavior.

- [ ] Add RED request-contract tests for `/users/me` and database schema requests.
- [ ] Add RED CLI output snapshots for reachable, HTTP failure, transport failure, missing field, and type mismatch cases.
- [ ] Implement diagnostics APIs using `NotionClient` idempotent-read semantics.
- [ ] Replace all Doctor-owned Notion `HttpClient` construction while preserving text and exit codes.
- [ ] Run Notion, CLI, architecture tests, and the N-03 focused gate.

### Task N-04: Move Importing write operations onto explicit Notion commands

**Files:**
- Create: `src/Bukit-Core/Bukit.Notion/Write/NotionWriteClient.cs`
- Create: `src/Bukit-Core/Bukit.Notion/Write/NotionWriteResults.cs`
- Modify: `src/Bukit-Plugins/Bukit.Importing/Bukit.Importing.csproj`
- Modify: `src/Bukit-Plugins/Bukit.Importing/ImportNotionSchemaValidator.cs`
- Modify: `src/Bukit-Plugins/Bukit.Importing/ImportNotionSeedPusher.cs`
- Modify: `src/Bukit-Plugins/Bukit.Importing/ImportNotionPushWorkflow.cs`
- Test: `tests/Bukit.Importing.Tests/ImportNotionPushWorkflowTests.cs`
- Test: `tests/Bukit.Notion.Tests/NotionWriteClientTests.cs`

**Interfaces:**
- Produces explicit methods for query-existing, inspect-schema, create-page, update-page, append-children, list-children, and archive-block.
- Importing owns seed-to-request payload mapping and workflow/report policy.

- [ ] Add RED wire-contract tests asserting exact method, URL, version, content type, and payload for every operation.
- [ ] Add RED tests proving create/update/append/archive are not retried after 429 or transport failure.
- [ ] Add RED tests proving cancellation produces no later write and response error bodies are not surfaced by library exceptions.
- [ ] Implement write commands on non-replayable transport semantics.
- [ ] Migrate Importing without adding Content or Engine references through `Bukit.Notion`.
- [ ] Update exact plugin-boundary allowlists and prove the new dependency is the only added edge.
- [ ] Run Notion, Importing, Plugin.Import, architecture tests, and the N-04 focused gate.

### Task N-05: Move pure Notion block and rich-text rendering

**Files:**
- Move implementation from: `src/Bukit-Core/Bukit.Content/Notion/BlockRenderers/`
- Move implementation from: `NotionBlocksRenderer.cs`, `NotionRenderContext.cs`, `NotionRichTextRenderer.cs`, `NotionColorPalette.cs`
- Create corresponding files under: `src/Bukit-Core/Bukit.Notion/Rendering/`
- Modify: `src/Bukit-Plugins/Bukit.WechatSyncing/Bukit.WechatSyncing.csproj`
- Remove duplicate: `src/Bukit-Plugins/Bukit.WechatSyncing/NotionColorPalette.cs`
- Test: migrate pure renderer tests to `tests/Bukit.Notion.Tests/Rendering/`

**Interfaces:**
- Rendering accepts `NotionClient` and Notion JSON only; it never exposes `ContentField`, `ContentException`, or `Bukit.Shared.ILogger`.
- Content retains 1.x facades where public types cannot move compatibly.

- [ ] Add RED parity tests comparing complete legacy/new HTML for every supported block and nested pagination case.
- [ ] Add RED escaping, media URL, color, cancellation, and pagination request-contract tests.
- [ ] Implement rendering with `NotionApiException` and BCL escaping helpers.
- [ ] Replace the Wechat palette duplicate with the canonical Notion palette without introducing Content/Engine dependencies.
- [ ] Prove public legacy renderer entry points still compile and return identical output.
- [ ] Run Notion, Content, WechatSyncing, architecture tests, and the N-05 focused gate.

### Task N-06: Establish the Bukit content-source adapter assembly

**Files:**
- Create: `src/Bukit-Core/Bukit.Content.Notion/Bukit.Content.Notion.csproj`
- Create: `tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj`
- Move implementation from: `src/Bukit-Core/Bukit.Content/Notion/` excluding 1.x facade files
- Modify: `src/Bukit-Core/Bukit.Content/Notion/NotionContentProvider.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Bukit.Engine.csproj`
- Modify: `src/Bukit-Core/Bukit.Engine/ContentProviderFactory.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/DefaultNotionPageFetcher.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/TaxonomyTermsInjector.cs`
- Modify: `bukit-core.slnx`

**Interfaces:**
- Produces: `NotionContentSource.LoadRawAsync(CancellationToken)` returning `RawContentLoadResult`.
- Existing `NotionContentProvider : IContentProvider` delegates to `NotionContentSource` and preserves constructor/API behavior.
- Adapter may reference Config, Engine.Abstractions, Shared, and Notion; it must not reference Content, Engine, CLI, Rendering, Routing, Theme, or plugin assemblies.

- [ ] Move end-to-end provider fixtures into adapter tests and establish baseline snapshots for every `RawContentDocument`, field type/value, body HTML, relation link, taxonomy term, log event, cache path/key, and failure message.
- [ ] Add RED architecture tests for the exact adapter dependency set and absence of cycles.
- [ ] Implement the adapter and thin Content facade without moving `IContentProvider` or changing public signatures.
- [ ] Replace Engine low-level Notion JSON operations with adapter/query services while preserving behavior.
- [ ] Run adapter, Content, Engine, CLI, architecture tests, and the N-06 focused gate.

### Task N-07: Complete compatibility governance and aggregate verification

**Files:**
- Modify: `docs/governance/bukit-core-public-api-baseline.v1.json` or the current baseline owner selected by governance scripts
- Modify: `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
- Create: `docs/analysis/bukit-notion-two-layer-migration-2026-07-22.zh-CN.md`
- Modify: architecture/coverage/AOT owner files required for the two new projects

**Interfaces:**
- 1.x compatibility surfaces remain available.
- 2.0 ledger records removal of Shared/Content facades and final namespace ownership under `Bukit.Notion` and `Bukit.Content.Notion`.

- [ ] Add compile-time consumer fixtures against old namespaces and binary-forwarding/reflection tests against old assembly-qualified names.
- [ ] Add a repository guard rejecting production `Notion-Version`, `api.notion.com/v1`, and raw Notion Authorization construction outside `Bukit.Notion` compatibility fixtures.
- [ ] Add AOT-safe serialization guard proving no reflection serializer entered the new projects.
- [ ] Update coverage project discovery and thresholds with direct owner self-tests.
- [ ] Run all directly affected project tests and architecture tests.
- [ ] Run one aggregate `bash scripts/checks/post-change-targeted.sh --base b8bc7059fa9f1040d71e12cac1697c8cecac741a -- <all changed paths>`.
- [ ] Perform a read-only audit of the aggregate diff against every global constraint and document any residual 2.0-only dependency.

## Self-Review Result

- Every listed safety risk is assigned to a RED test or architecture guard.
- The design avoids a `Bukit.Content` ↔ `Bukit.Content.Notion` cycle by keeping `IContentProvider` implementation as a thin 1.x facade.
- Exact 1.x binary preservation may require a facade instead of a type forwarder when a moved public constructor mentions Config or Content types; the plan prohibits direct breaking moves.
- Notion API/version upgrades, config-schema changes, plugin-protocol changes, parser replacement, and unrelated path/security utilities are explicitly outside scope.
