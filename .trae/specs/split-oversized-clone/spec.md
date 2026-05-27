# Split 3 Clone Oversized Files Spec

## Why

8 files remain in the oversized baseline. This spec targets the 3 Clone-related files: `CloneContentWriter.cs` (~712), `CloneThemeGenerator.cs` (~1206), `CloneCommand.cs` (~1179).

## What Changes

### 1. CloneContentWriter.cs → Extract helper classes

- **CloneYamlWriter.cs**: `EnsureSourcesConfig`, `EnsureMarkdownSource`, `GetOrCreateMapping`, `GetOrCreateSequence`, `GetScalar`, `YamlScalar`, `AppendBlockScalar` — YAML writing utilities
- Keep in CloneContentWriter: `WriteTo`, all content generation methods (`GenerateIndexContent`, `GenerateSectionData`, etc.), asset/URL helpers, `CloneContentWriteResult` record, `NormalizedSection` record

### 2. CloneThemeGenerator.cs → Extract style and JS generators

- **CloneStyleSheetGenerator.cs**: `GenerateStyleCss` (the big ~314-line CSS generator) and its helpers `C`, `AddVar`, `Esc`
- **CloneBehaviorGenerator.cs**: `GenerateBehaviorCss`, `GenerateBehaviorsJs`, `CountBehaviors`
- Keep in CloneThemeGenerator: `WriteTo`, `GenerateBaseLayout`, `GenerateIndex`, `GenerateHeader`, `GenerateFooter`, `GenerateThemeYaml`, `SanitizeFileName`, `CloneGenerationSummary`, partial constants, nav footer helpers

### 3. CloneCommand.cs → Extract verify and PNG reader

- **CloneVerifier.cs**: `VerifyCloneAsync`, `WriteVerifyReport`, `WriteBehaviorVerifyScript`, `WriteVerifyJsonReport`, `AppendAffectedSections`, `FindAffectedSections`, `CompareScreenshotFiles`, `FindMissingScreenshotPairs`, `ComparePngScreenshots`, `ResolveSectionBounds`, `ExtractViewportName`, `RangesOverlap`, `SectionLabel`, `CountFiles`, `CountThemeAssetFiles`, all verify-related records
- **PngImageReader.cs**: `PngImage` record with `Read`, `Unfilter`, `Paeth`
- Keep in CloneCommand: `RunAsync` overloads, `RunCoreAsync`, `RunFidelityAsync`, `LoadPageAsync`, `LoadSectionsAsync`, `DownloadAssetsAsync`, `WriteIcons`, `SanitizeFileName`, `CountBehaviors`, `ParseVisualThreshold`, `WriteFidelitySiteYaml`, `TransferAssetsToStatic`, `ResolveConfigPathForCommand`

## Impact

- Affected specs: Quality Gate (oversized baseline)
- Affected code: `src/Bukit.Cli/Commands/`
- All extracted classes are `internal`
- No public API changes

## Baseline Removal

After each file is split below 600 lines, remove its entry from `scripts/.oversized-baseline.txt`.
