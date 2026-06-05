# Plan: Split Oversized .cs Files Below 600-Line Cohesion Limit

## Summary

Two files flagged by `scripts/quality-gate.sh` exceed the 600-line cohesion limit:

| File                                                    | Lines |
| ------------------------------------------------------- | ----- |
| `src/Bukit.Cli/Commands/DoctorCommand.cs`               | 645   |
| `src/Bukit.Shared/Notion/HtmlToNotionBlockConverter.cs` | 629   |

Split each into focused files following the existing extractor-class pattern already used in the doctor subsystem (DoctorManifestChecker, DoctorTemplateChecker, DoctorSchemaChecker, DoctorNotionChecker, DoctorThemeChecker, DoctorMarkdownChecker).

***

## Current State Analysis

### DoctorCommand.cs (645 lines)

Subsystem structure (already partially extracted):

```
src/Bukit.Cli/Commands/
├── DoctorCommand.cs              ← 645 lines (OVER): orchestrator + leftover utilities
├── DoctorManifestChecker.cs      ← uses DoctorCommand.ExtractDirectives
├── DoctorTemplateChecker.cs      ← uses DoctorCommand.ExtractDirectives, AppendFileOrWarn
├── DoctorSchemaChecker.cs        ← uses DoctorCommand.DoctorContext
├── DoctorMarkdownChecker.cs      ← uses DoctorCommand.DoctorContext
├── DoctorNotionChecker.cs
└── DoctorThemeChecker.cs
```

**What's left in DoctorCommand.cs:**

| Method                               | Lines | Visibility          | Used by                                      |
| ------------------------------------ | ----- | ------------------- | -------------------------------------------- |
| `RunAsync`                           | \~415 | public              | test files                                   |
| `DoctorContext` record               | \~5   | internal            | all Doctor\*Checker files                    |
| `CollectExplicitConfiguredTemplates` | \~35  | private             | RunAsync only                                |
| `CollectMissingUsedTemplates`        | \~18  | private static      | RunAsync only                                |
| `CollectPluginRequirementTemplates`  | \~12  | private static      | RunAsync only                                |
| `AnalyzeTemplateChains`              | \~28  | private static      | RunAsync only                                |
| `ExtractDirectives`                  | \~21  | **internal** static | DoctorManifestChecker, DoctorTemplateChecker |
| `CheckFollowSymlinksSafety`          | \~7   | private static      | RunAsync only                                |
| `AppendFileOrWarn`                   | \~5   | **internal** static | DoctorTemplateChecker                        |
| `CountOpenings`                      | \~12  | private static      | RunAsync only                                |
| `CheckTemplateVariables`             | \~15  | private static      | RunAsync only                                |
| `CheckOutputDirectorySafety`         | \~43  | private static      | RunAsync only                                |

**Key cross-reference:** `ExtractDirectives` and `AppendFileOrWarn` have `internal` visibility and are called from `DoctorManifestChecker` and `DoctorTemplateChecker`.

**Tests affected:**

* `tests/Bukit.Cli.Tests/DoctorCommandAppendFileOrWarnTests.cs` — calls `DoctorCommand.AppendFileOrWarn` directly

* `tests/Bukit.Cli.Tests/DoctorCommandTests.cs` — calls `DoctorCommand.RunAsync` (unchanged)

### HtmlToNotionBlockConverter.cs (629 lines)

Located at `src/Bukit.Shared/Notion/HtmlToNotionBlockConverter.cs`.

Has two distinct responsibilities:

1. **HTML → NotionBlock parsing** (\~430 lines): `Convert`, `ParseBlocks`, `CollectTextUntilClose`, `CollectRawTextUntilClose`, `CollectRichText`, `CollectFaqBlocks`, `GetAttribute`, `HasClass`, `ExtractText`, helper methods
2. **NotionBlock → JSON serialization** (\~188 lines): `SerializeBlocks`, `WriteBlock`, `WriteHeadingBlock`, `WriteRichTextBlock`, `WriteRichTextSegment`, `TruncateBlockText`, `WriteTextObject`

The public entry point `ToBlocksJson` chains both: `Convert(html)` → `SerializeBlocks(blocks)`.

**Callers** (in `src/Bukit.Cli/Commands/NotionSeedPusher.cs`):

* `HtmlToNotionBlockConverter.ToBlocksJson(record.Content)` — lines 100, 316

* Public API unchanged by split; no test files reference the converter directly.

***

## Proposed Changes

### Change 1: Extract `DoctorTemplateAnalyzer.cs` from `DoctorCommand.cs`

**New file:** `src/Bukit.Cli/Commands/DoctorTemplateAnalyzer.cs`

Move the following methods (making them `internal static`):

| Method                               | From (DoctorCommand) | To (DoctorTemplateAnalyzer) |
| ------------------------------------ | -------------------- | --------------------------- |
| `CollectExplicitConfiguredTemplates` | private → internal   | internal static             |
| `CollectMissingUsedTemplates`        | private → internal   | internal static             |
| `CollectPluginRequirementTemplates`  | private → internal   | internal static             |
| `AnalyzeTemplateChains`              | private → internal   | internal static             |
| `ExtractDirectives`                  | internal → internal  | internal static             |
| `CheckTemplateVariables`             | private → internal   | internal static             |
| `AppendFileOrWarn`                   | internal → internal  | internal static             |
| `CountOpenings`                      | private → internal   | internal static             |

**Rationale:** All of these are template-analysis utilities. Moving them to a dedicated class follows the existing `Doctor*Checker` pattern. `CountOpenings` is a small utility used by `RunAsync` — moved with the group for cohesion.

**Updated file:** `src/Bukit.Cli/Commands/DoctorCommand.cs`

Keep:

* `RunAsync` (orchestrator) — updates method calls to `DoctorTemplateAnalyzer.*`

* `DoctorContext` record (used broadly by other checkers)

* `CheckOutputDirectorySafety`

* `CheckFollowSymlinksSafety`

**Result:** DoctorCommand.cs goes from 645 → \~475 lines.

**Also update callers (3 files):**

1. `src/Bukit.Cli/Commands/DoctorManifestChecker.cs`:

   * `DoctorCommand.ExtractDirectives` → `DoctorTemplateAnalyzer.ExtractDirectives`

2. `src/Bukit.Cli/Commands/DoctorTemplateChecker.cs`:

   * `DoctorCommand.ExtractDirectives` → `DoctorTemplateAnalyzer.ExtractDirectives`

   * `DoctorCommand.AppendFileOrWarn` → `DoctorTemplateAnalyzer.AppendFileOrWarn`

3. `tests/Bukit.Cli.Tests/DoctorCommandAppendFileOrWarnTests.cs`:

   * `DoctorCommand.AppendFileOrWarn` → `DoctorTemplateAnalyzer.AppendFileOrWarn`

### Change 2: Extract `NotionBlockJsonWriter.cs` from `HtmlToNotionBlockConverter.cs`

**New file:** `src/Bukit.Shared/Notion/NotionBlockJsonWriter.cs`

Move the following methods (making them `internal static`):

| Method                 | Description                                  |
| ---------------------- | -------------------------------------------- |
| `SerializeBlocks`      | Serializes `List<NotionBlock>` → JSON string |
| `WriteBlock`           | Switch dispatcher over block types           |
| `WriteHeadingBlock`    | JSON writer for heading blocks               |
| `WriteRichTextBlock`   | JSON writer for rich-text blocks             |
| `WriteRichTextSegment` | JSON writer for a single rich-text segment   |
| `TruncateBlockText`    | Truncates text to 2000 char limit            |
| `WriteTextObject`      | JSON writer helper for plain text objects    |

**Updated file:** `src/Bukit.Shared/Notion/HtmlToNotionBlockConverter.cs`

Keep:

* `ToBlocksJson` — updated to call `NotionBlockJsonWriter.SerializeBlocks(blocks)` instead of local `SerializeBlocks`

* `Convert` (public API) + all parsing methods (`ParseBlocks`, `CollectTextUntilClose`, `CollectRawTextUntilClose`, `CollectRichText`, `CollectFaqBlocks`, `GetAttribute`, `HasClass`, `ExtractText`, `IsHeadingTag`, `CreateHeadingBlock`)

**Result:** HtmlToNotionBlockConverter.cs goes from 629 → \~440 lines. NotionBlockJsonWriter.cs ≈ 210 lines.

**Caller impact:** Zero — `ToBlocksJson` public API signature is unchanged. `NotionSeedPusher.cs` calls `HtmlToNotionBlockConverter.ToBlocksJson(...)` as before.

***

## Assumptions & Decisions

1. **Visibility:** Both new classes use `internal static` — consistent with the existing Doctor\*Checker pattern (all are `internal static`).

2. **Namespace:** Both new classes stay in their existing namespaces:

   * `Bukit.Cli.Commands` for DoctorTemplateAnalyzer

   * `Bukit.Shared.Notion` for NotionBlockJsonWriter

3. **DoctorContext:** Kept in `DoctorCommand.cs` since all other checkers reference `DoctorCommand.DoctorContext`. Moving it would create a cascading rename across 4+ files.

4. **`WriteBlock`** **approach:** Even though `WriteBlock` is a long switch statement (\~97 lines), it is a single cohesive unit (dispatch over NotionBlock subclasses). Splitting it further would harm readability without reducing coupling. It lands in NotionBlockJsonWriter alongside the other serialization methods.

5. **Not adding to baseline:** The goal is to split the files below 600 lines, not to add them to `scripts/.oversized-baseline.txt`. The baseline approach is for justified grandfathered debt only.

***

## Verification Steps

1. **Build:**

   ```bash
   dotnet build bukit.slnx -c Release
   ```

2. **Run affected tests:**

   ```bash
   dotnet test tests/Bukit.Cli.Tests --filter "FullyQualifiedName~DoctorCommandAppendFileOrWarn|FullyQualifiedName~DoctorCommandTests" -c Release --no-build
   ```

3. **Run full test suite:**

   ```bash
   dotnet test bukit.slnx -c Release --no-build
   ```

4. **Verify oversized check passes:**

   ```bash
   bash scripts/quality-gate.sh
   ```

   Should show 0 new oversized files in the ERROR output.

5. **Quick smoke:**

   ```bash
   dotnet run --project src/Bukit.Cli doctor --help
   ```

