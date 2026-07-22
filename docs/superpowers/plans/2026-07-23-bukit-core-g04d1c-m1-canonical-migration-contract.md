# G-04D1C-M1 Canonical Migration Contract Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Establish an executable, source-oriented migration contract from the five retained `Bukit.Content.Notion` extension-graph types to `Bukit.Notion.Rendering`, without removing or narrowing any legacy CLR type.

**Architecture:** The canonical `Bukit.Notion` assembly remains the BCL-only owner of transport and rendering. M1 adds consumer-facing characterization fixtures around that owner, aligns only the canonical registry's null-argument failure timing with the legacy registry, and records the intentional client/options/exception/write-semantics differences. Legacy adapters remain unchanged until a separately authorized atomic M2 removal.

**Tech Stack:** C# 14, .NET 10, xUnit, `HttpMessageHandler` test doubles, repository public-API drift tooling, Markdown governance documentation.

## Global Constraints

- Keep these five types public and unchanged in M1: `Bukit.Content.Notion.INotionBlockRenderer`, `Bukit.Content.Notion.NotionBlockTransformer`, `Bukit.Content.Notion.NotionBlockRendererRegistry`, `Bukit.Content.Notion.NotionRenderContext`, and `Bukit.Content.Notion.NotionBlocksRenderer`.
- Keep the governed public API baseline at 14 assemblies, 514 types, and 110 `2.0-candidate` types. Do not regenerate or edit `docs/governance/bukit-core-public-api-baseline.v1.json`.
- Keep `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` byte-identical; its Git blob must remain `7b07d6890562387010b52301e9f8716e9bf10ed1`.
- Do not modify the legacy extension-graph implementation or repair its shared-registry split-brain behavior in M1. Canonical fixtures must prove that the canonical registry does not reproduce that behavior.
- Do not modify `NotionApiClient`, `NotionProviderOptions`, `NotionClientStats`, `NotionClient`, `NotionClientOptions`, `NotionRequestSemantics`, transport/retry behavior, schema, plugin protocol, CLI, config, asset URL, path utilities, build reports, CI, release, or gate scripts.
- `src/Bukit-Core/Bukit.Notion/Bukit.Notion.csproj` must remain free of `ProjectReference` and `PackageReference` entries. Do not introduce a `Bukit.Content` or `Bukit.Shared` dependency into canonical production code.
- The only production behavior change allowed is immediate `ArgumentNullException` validation for a null renderer passed to canonical `Register` and a null transformer passed to canonical `SetCustomTransformer`.
- Explicitly document that renderer instances do not own clients, consumers dispose canonical `NotionClient`, injected `HttpClient` remains caller-owned, internally-created `HttpClient` remains client-owned, and database query reads use `IdempotentRead` while writes use `NonReplayableWrite`.
- Do not run full/release gates, `scripts/test-all.sh`, `scripts/smoke-all.sh`, or whole-solution tests. Run `post-change-focused.sh` after each task and one aggregate `post-change-targeted.sh` at parent completion.

---

### Task 1: Canonical registry and extension-graph contract

**Files:**
- Modify: `src/Bukit-Core/Bukit.Notion/Rendering/NotionBlockRendererRegistry.cs`
- Create: `tests/Bukit.Notion.Tests/CanonicalExtensionGraphMigrationContractTests.cs`

**Step 1: Write the failing null-validation tests**

Add tests that call canonical `Register("paragraph", null!)` and `SetCustomTransformer("paragraph", null!)`. Assert immediate `ArgumentNullException` with parameter names `renderer` and `transformer`. Run only those tests and record RED: current canonical code accepts the null value.

**Step 2: Add the canonical consumer fixtures**

In the same focused fixture, exercise the public canonical interface/delegate/registry/renderer/context graph:

- a custom `INotionBlockRenderer` receives the exact source `JsonElement`, exact renderer-owned `NotionClient`, and exact caller cancellation token;
- the callback uses `NotionRenderContext.RenderChildrenAsync` to render paginated nested children and proves token propagation;
- custom renderer override, duplicate replacement, transformer override, transformer-null fallback, removal, and unknown block behavior are asserted through public entry points;
- two `NotionBlocksRenderer` instances share one canonical registry but use distinct clients; callbacks invoked through renderer A and B receive client A and B respectively.

Use real `HttpClient` instances backed by deterministic `HttpMessageHandler` fakes. Do not add a `Bukit.Content` project reference to `Bukit.Notion.Tests`.

**Step 3: Implement the narrow production change**

Add `ArgumentNullException.ThrowIfNull(renderer)` before the canonical renderer dictionary assignment and `ArgumentNullException.ThrowIfNull(transformer)` before the canonical transformer dictionary assignment. Do not change method signatures, fluent return values, lookup order, fallback, duplicate, or removal behavior.

**Step 4: Verify GREEN**

Run:

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --nologo --verbosity minimal --tl:off --filter FullyQualifiedName~CanonicalExtensionGraphMigrationContractTests
bash scripts/checks/post-change-focused.sh -- src/Bukit-Core/Bukit.Notion/Rendering/NotionBlockRendererRegistry.cs tests/Bukit.Notion.Tests/CanonicalExtensionGraphMigrationContractTests.cs
```

Expected: all new tests and focused owner checks pass with no warnings introduced by this task.

**Step 5: Commit**

```bash
git add src/Bukit-Core/Bukit.Notion/Rendering/NotionBlockRendererRegistry.cs tests/Bukit.Notion.Tests/CanonicalExtensionGraphMigrationContractTests.cs
git commit -m "test(notion): establish extension graph migration contract"
```

---

### Task 2: Client, exception, request-semantics, and ownership migration fixtures

**Files:**
- Create: `tests/Bukit.Notion.Tests/CanonicalClientMigrationContractTests.cs`
- Create: `tests/Bukit.Content.Tests/LegacyNotionExtensionMigrationContractTests.cs`

**Step 1: Add compile-time source consumer fixtures**

Compile one small legacy custom renderer/transformer fixture against `Bukit.Content.Notion` and one canonical fixture against `Bukit.Notion.Rendering`. Assert the context client identities are respectively `NotionApiClient` and `NotionClient`. Keep the two source consumers in their owning test projects; do not add cross-owner project references.

**Step 2: Add the paired exception contract**

Using deterministic handlers, prove the documented migration matrix:

- missing `results`: legacy public renderer exposes `ContentException` with inner `NotionRenderingException`; canonical exposes `NotionRenderingException` directly;
- non-success HTTP, terminal 429, invalid JSON, and transport failure: legacy exposes `ContentException` with inner `NotionApiException`; canonical exposes `NotionApiException` directly and retains the expected `NotionApiErrorKind`;
- a custom renderer or transformer exception propagates without translation;
- caller cancellation propagates as `OperationCanceledException` with the original token.

Do not change production exception types or add a content dependency to canonical code.

**Step 3: Add options and write-semantics migration proof**

Use public canonical APIs to prove the migration mapping and request behavior:

- token and explicit API version are emitted as request headers;
- request delay, retry count, max RPS, and the 30-second default timeout remain representable in `NotionClientOptions` without adding a mapping API;
- a database-query POST sent as `IdempotentRead` retries a deterministic 429 then succeeds;
- a write request sent as `NonReplayableWrite` does not replay after 429;
- migration examples compile with explicit `HttpRequestMessage` plus `NotionRequestSemantics`, not a guessed automatic facade.

**Step 4: Add ownership/disposal proof**

Prove that disposing/rendering through `NotionBlocksRenderer` does not create ownership of the client, that disposing a canonical client with injected `HttpClient` leaves that `HttpClient` usable, and that an internally owned client disposes its handler exactly once. Reuse existing test-support patterns, but keep this fixture consumer-oriented and avoid changing production disposal behavior.

**Step 5: Verify**

Run:

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --nologo --verbosity minimal --tl:off --filter FullyQualifiedName~CanonicalClientMigrationContractTests
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --nologo --verbosity minimal --tl:off --filter FullyQualifiedName~LegacyNotionExtensionMigrationContractTests
bash scripts/checks/post-change-focused.sh -- tests/Bukit.Notion.Tests/CanonicalClientMigrationContractTests.cs tests/Bukit.Content.Tests/LegacyNotionExtensionMigrationContractTests.cs
```

Expected: the new canonical and legacy migration fixtures pass; no production file changes occur in this task.

**Step 6: Commit**

```bash
git add tests/Bukit.Notion.Tests/CanonicalClientMigrationContractTests.cs tests/Bukit.Content.Tests/LegacyNotionExtensionMigrationContractTests.cs
git commit -m "test(notion): codify client migration semantics"
```

---

### Task 3: M1 public-surface guard and migration guide

**Files:**
- Create: `tests/Bukit.Architecture.Tests/G04D1CM1MigrationContractTests.cs`
- Create: `docs/analysis/bukit-core-g04d1c-m1-canonical-migration-contract-2026-07-23.zh-CN.md`

**Step 1: Write the failing governance/guide test**

Add an architecture test that initially fails because the M1 guide does not exist. The test must also prove:

- all five legacy types resolve publicly from `Bukit.Content` under their original full names;
- their canonical replacements resolve publicly from `Bukit.Notion`;
- the baseline still contains 14 assemblies, 514 types, and 110 `2.0-candidate` entries, including all five legacy entries;
- the closed 136-entry candidate manifest still has Git blob `7b07d6890562387010b52301e9f8716e9bf10ed1`;
- canonical `Bukit.Notion.csproj` has no project or package references;
- the guide records the exact M1/M2 boundary and all required migration contracts.

Run the new test and record RED from the missing guide, not from weakening any existing guard.

**Step 2: Write the canonical migration guide/ledger**

Create the dated Chinese report with:

- scope, immutable boundaries, baseline/test evidence, and M1 status;
- exact old/new source snippets for interface, transformer, registry, context, renderer construction, and namespace/assembly identity;
- explicit `NotionProviderOptions` to `NotionClientOptions` mapping for token, fixed API version, request delay, retries, max RPS, and 30-second timeout; explain that database/content projection options do not map to transport;
- the complete old/new exception matrix and catch examples;
- explicit database-query `IdempotentRead` and write `NonReplayableWrite` examples;
- callback identity, nested child rendering, shared-registry behavior, cancellation, and ownership/disposal rules;
- source break, binary break, no type-forwarding path, private-consumer uncertainty, and the new-evidence fallback rule;
- state that M1 retains all five legacy types and does not authorize M2; M2 requires separate deliberate public API approval.

Do not claim the focused, aggregate, or independent review passed until each has actually completed. Use provisional evidence wording that the controller can update after validation.

**Step 3: Verify the task**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --nologo --verbosity minimal --tl:off --filter FullyQualifiedName~G04D1CM1MigrationContractTests
bash scripts/checks/post-change-focused.sh -- tests/Bukit.Architecture.Tests/G04D1CM1MigrationContractTests.cs docs/analysis/bukit-core-g04d1c-m1-canonical-migration-contract-2026-07-23.zh-CN.md
```

Expected: the M1 guard and documentation checks pass; baseline and candidate manifest remain unchanged.

**Step 4: Commit**

```bash
git add tests/Bukit.Architecture.Tests/G04D1CM1MigrationContractTests.cs docs/analysis/bukit-core-g04d1c-m1-canonical-migration-contract-2026-07-23.zh-CN.md
git commit -m "docs(notion): publish canonical migration contract"
```

---

## Parent Completion Verification

After all three task reviews are clean:

1. Run the four relevant Release test projects once and record exact counts:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --nologo --verbosity minimal --tl:off
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --nologo --verbosity minimal --tl:off
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --nologo --verbosity minimal --tl:off
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj -c Release --nologo --verbosity minimal --tl:off
```

2. Run public API drift self-test and the real Release check. Confirm 514/110 and the immutable candidate-manifest blob.
3. Run exactly one parent aggregate gate from base `a0bd2f3f36ae623f47b06b259bc2ffc36890ea08`, passing every changed tracked path after that base.
4. Update only the M1 guide's verification section with the actual outcomes. If that update changes the aggregate diff after the single aggregate run, run only the documentation-focused check for the final evidence edit; do not falsely claim a second aggregate.
5. Dispatch one independent whole-branch read-only review. Acceptance is `0 Critical / 0 Important / 0 Minor`; fixes require focused revalidation and re-review.
6. Run `git diff --check` and confirm no forbidden path, public baseline, candidate manifest, project reference, or legacy implementation drift.

Any repeat of the existing unrelated `brainstorm-server-self-test` failure must be recorded as an exact independent gate blocker. Do not repair, suppress, or classify it as an M1 regression without evidence.
