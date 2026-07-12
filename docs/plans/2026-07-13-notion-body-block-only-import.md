# Notion Body-Block-Only Import Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task.

**Goal:** 让 Bukit 的 Notion 导入/推送链路只把文章正文写入 Notion 页面 Blocks，不再要求、创建或写入名为 `Content` 的 rich_text 数据库属性。

**Architecture:** `ImportSeedRecord.Content` 仍是导入过程中的内部正文载体，用于 HTML→Notion Blocks 转换；它不是 Notion 数据库属性。Schema 校验、创建数据库载荷和页面属性载荷都应移除 `Content`，而创建/更新时的 `children` 写入与 `--update-content=append|replace` 行为必须保持不变。

**Tech Stack:** .NET 10、C#、xUnit、Bukit.Plugin.Import、Bukit.Importing、Notion API 2022-06-28、System.Text.Json。

---

## Scope and constraints

- This is a **Bukit Core/importing task**, not an SRBiz theme task.
- Work in a dedicated worktree/branch, for example `codex/notion-body-block-only-import`, created from the intended Core base branch.
- Do not change SRBiz Notion data, delete the live `Content` property, or edit the SRBiz site in this task.
- Preserve public command names and options, especially `notion validate-schema`, `notion push`, `import html-demo --push-notion`, `--update-content append`, and `--update-content replace`.
- Do not rename `ImportSeedRecord.Content` in this task. It remains the in-memory HTML body used to create Notion child blocks.
- The output contract change is intentional: newly created or updated Notion pages must not contain a `Content` database property. Their body must remain in child Blocks.

## Current fault model

Today the same `record.Content` value is duplicated in two representations:

1. `NotionSchemaValidator.RequiredFields` requires `Content: rich_text`.
2. `ImportNotionSeedPusher.WriteProperties(...)` writes the value as a `Content` page property.
3. `BuildCreatePagePayload(...)` independently writes the same value as `children` after converting HTML to Notion Blocks.
4. `BuildCreateDatabasePayload(...)` creates `Content` as a rich_text database property.

The desired outcome removes (1), (2), and the `Content` entry in (4), while retaining (3) and the existing update/replace child-Block behavior.

## Files in scope

- Modify: `src/Bukit-Plugins/Bukit.Importing/ImportNotionSchemaValidator.cs`
- Modify: `src/Bukit-Plugins/Bukit.Importing/ImportNotionSeedPusher.cs`
- Modify only if confirmed necessary by focused tests: `src/Bukit-Plugins/Bukit.Importing/NotionPropertyNaming.cs`
- Modify: `tests/Bukit.Importing.Tests/ImportNotionPushWorkflowTests.cs`
- Modify: `tests/Bukit.Plugin.Import.Tests/ImportPluginInvokeCompatibilityTests.cs`
- Add or extend targeted tests in `tests/Bukit.Importing.Tests/` if existing test seams cannot assert serialized payloads precisely.

Do not modify `guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, or `scripts-0.2/`.

## Task 1: Prove the desired schema and payload contract with failing tests

**Files:**

- Modify: `tests/Bukit.Importing.Tests/ImportNotionPushWorkflowTests.cs`
- Modify: `tests/Bukit.Plugin.Import.Tests/ImportPluginInvokeCompatibilityTests.cs`

### Step 1: Add a schema-validation regression test

Add a test whose stubbed Notion database includes exactly:

```json
{
  "Title": { "type": "title" },
  "Slug": { "type": "rich_text" },
  "Type": { "type": "select" },
  "Summary": { "type": "rich_text" },
  "Language": { "type": "select" },
  "Published": { "type": "checkbox" },
  "SeoTitle": { "type": "rich_text" },
  "SeoDescription": { "type": "rich_text" }
}
```

Assert that `ImportNotionPushWorkflow.ValidateSchemaAsync(...)` returns exit code `0` and writes a successful report. The fixture must deliberately omit `Content`.

### Step 2: Run the focused test and verify RED

Run:

```bash
dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj --no-restore --filter "FullyQualifiedName~ValidateSchemaAsync"
```

Expected before implementation: the new test fails because schema validation reports missing `Content`.

### Step 3: Add serialized-payload assertions

Using the existing stub HTTP handler seam, add tests for both creation and upsert paths that assert:

- the JSON `properties` object does **not** contain `Content`;
- a non-empty `ImportSeedRecord.Content` still produces a non-empty `children` array on creation;
- `--update-content=replace` still deletes old blocks and appends converted replacement blocks;
- `--update-content=append` still appends converted blocks;
- no test infers the rendered page body from a page property.

The assertions should parse outgoing JSON using `JsonDocument`; avoid fragile string containment checks for the JSON payload itself.

### Step 4: Extend the plugin boundary fixture

In `ImportPluginInvokeCompatibilityTests`, update/add a `notion validate-schema` success fixture that omits `Content`. Assert the plugin response remains successful and report artifact handling is unchanged.

### Step 5: Run the focused tests and verify RED

Run:

```bash
dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj --no-restore --filter "FullyQualifiedName~ImportNotion"
dotnet test tests/Bukit.Plugin.Import.Tests/Bukit.Plugin.Import.Tests.csproj --no-restore --filter "FullyQualifiedName~NotionValidateSchema"
```

Expected before implementation: tests fail only because current production code still emits or requires `Content`.

## Task 2: Remove the database-property dependency while preserving body Blocks

**Files:**

- Modify: `src/Bukit-Plugins/Bukit.Importing/ImportNotionSchemaValidator.cs`
- Modify: `src/Bukit-Plugins/Bukit.Importing/ImportNotionSeedPusher.cs`
- Modify if required by tests: `src/Bukit-Plugins/Bukit.Importing/NotionPropertyNaming.cs`

### Step 1: Relax schema validation

In `NotionSchemaValidator.RequiredFields`, remove only:

```csharp
("Content", "rich_text"),
```

Keep the remaining eight core property requirements unchanged. Do not make schema validation optional wholesale and do not weaken checks for title, slug, type, summary, language, published state, or SEO fields.

### Step 2: Stop writing `Content` in page properties

In `ImportNotionSeedPusher.WriteProperties(...)`, remove only:

```csharp
WriteRichTextProperty(writer, "Content", record.Content);
```

Leave the `record.Content` checks in `BuildCreatePagePayload(...)` and the upsert `append` / `replace` paths intact, because those paths produce the actual Notion body Blocks.

### Step 3: Stop creating the property in a new database

In `BuildCreateDatabasePayload(...)`, remove only:

```csharp
WriteDatabaseProperty(writer, "Content", "rich_text");
```

This makes `--create-missing-notion-databases` create the target schema without a redundant body property.

### Step 4: Evaluate `NotionPropertyNaming.IsCore`

`NotionPropertyNaming.IsCore(...)` currently classifies `Content` as a core database property. Remove that classification only if the new/updated payload tests demonstrate it would otherwise reserve or recreate the property. Do not make a speculative change: document the observed call path in the test or code comment if it remains intentionally unchanged.

### Step 5: Run the focused tests and verify GREEN

Run the commands from Task 1 again. Expected result: all new schema and payload tests pass; body child-block tests still pass.

## Task 3: Broaden verification, preserve compatibility, and prepare handoff

**Files:**

- Modify only fixtures/tests required by verified failures.
- Do not modify SRBiz files or a live Notion database in this task.

### Step 1: Update all in-repo schema fixtures

Search the Core repository for fixtures that define a successful Notion import schema with `Content`:

```bash
rg -n '"Content": \{ "type": "rich_text" \}|\("Content", "rich_text"\)|WriteDatabaseProperty\(writer, "Content"' \
  src/Bukit-Plugins/Bukit.Importing tests/Bukit.Importing.Tests tests/Bukit.Plugin.Import.Tests
```

Update only success fixtures and expected outgoing payloads. Retain negative tests that deliberately assert an unknown property or a wrong type for another required property.

### Step 2: Run project-level targeted tests

Run:

```bash
dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj --no-restore
dotnet test tests/Bukit.Plugin.Import.Tests/Bukit.Plugin.Import.Tests.csproj --no-restore
```

Expected result: both projects pass. Do not run full solution, release, or smoke gates unless the user separately requests them.

### Step 3: Run the repository targeted change gate

Run:

```bash
bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Plugins/Bukit.Importing/ImportNotionSchemaValidator.cs \
  src/Bukit-Plugins/Bukit.Importing/ImportNotionSeedPusher.cs \
  src/Bukit-Plugins/Bukit.Importing/NotionPropertyNaming.cs \
  tests/Bukit.Importing.Tests/ImportNotionPushWorkflowTests.cs \
  tests/Bukit.Plugin.Import.Tests/ImportPluginInvokeCompatibilityTests.cs
```

If `NotionPropertyNaming.cs` is unchanged, omit it from the path list. Stop and fix any failure before proceeding.

### Step 4: Perform the required bounded Core audit

Because this changes a public import/schema contract, conduct one bounded read-only audit after all targeted tests pass. Verify:

- no production serializer emits a `Content` database property;
- all `record.Content` uses are limited to body-block conversion, local seed content, or markdown draft generation;
- `append` and `replace` block update behavior is unchanged;
- database creation and schema validation agree on the same required property set;
- no unrelated routing, theme, or SRBiz files changed.

### Step 5: Commit the isolated Core task

After the gate and audit are green, make one focused commit, for example:

```bash
git add src/Bukit-Plugins/Bukit.Importing tests/Bukit.Importing.Tests tests/Bukit.Plugin.Import.Tests
git commit -m "feat(import): store Notion bodies only as blocks"
```

## Acceptance criteria

- `notion validate-schema` succeeds for a valid database without a `Content` property.
- Created Notion databases do not define a `Content` property.
- Created and updated pages do not write a `Content` page property.
- Non-empty `ImportSeedRecord.Content` still becomes Notion page Blocks on create, append, and replace workflows.
- Existing CLI/plugin command shapes and environment permission checks remain compatible.
- Targeted Importing and Import plugin tests, targeted repository gate, and bounded contract audit pass.

## Follow-up outside this Core task

Only after this Core task is merged and independently verified:

1. In SRBiz, remove `Content` from Notion seed assumptions and update any seed fixtures that still rely on the property.
2. Run an SRBiz Notion build, `check-post-body-blocks.mjs`, list/detail checks, and an import/upsert smoke against a disposable database.
3. Delete `Content` from the live SRBiz Posts database only after the disposable-database smoke passes.
