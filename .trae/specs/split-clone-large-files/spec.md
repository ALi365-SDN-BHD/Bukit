# Split Clone Large Files Spec

## Why

`bukit-deep-audit-report-2026-05-29.md` 的 P2-1 将 `CloneCommand.cs`（555 行）标记为高严重度 god class，混合了"选项解析、文件加载、资产下载、图标写入、fidelity 模式、site.yaml 生成"等多个不内聚的职责。延伸盘点 `src/Bukit.Cli/Commands/Clone*.cs` 下其他临近 600 行阈值的大文件，确认整个 clone 子系统都存在职责膨胀风险：

| 文件 | 行数 | 风险 |
|------|------|------|
| CloneContentWriter.cs | 591 | 接近 600 阈值；混合 IO/Markdown/CSS/资产/manifest 生成 |
| CloneThemeGenerator.cs | 581 | 接近 600 阈值；layout/header/footer/section/yaml 混合 |
| CloneFidelityGenerator.cs | 566 | HTML 解析、common-block 提取、模板构建、资产拷贝混合 |
| CloneCommand.cs | 555 | **P2-1 god class**；编排 + 多个职责 |
| CloneVerifier.cs | 526 | 验证 + 报告 + 截图比较混合 |
| CloneModels.cs | 481 | 24 个 record 集中在一个文件 |

本 spec 系统性拆分上述 6 个大文件，将职责按单一职责原则提取到独立类型，每个文件目标 < 400 行（远离 600 阈值），同时保持外部 API（`CloneCommand.RunAsync`、`CloneThemeGenerator.WriteTo`、`CloneContentWriter.WriteTo` 等公共入口）完全不变。

## What Changes

### 1. CloneCommand.cs（555 → ≤ 250 行） — P2-1 核心

提取出 3 个独立处理器到 `src/Bukit.Cli/Commands/Clone/` 子目录：

- **CloneCommandOptions.cs**（新增，≈ 80 行）：包含 `CloneCommandOptions` record（封装所有 CLI 选项）和静态 `Parse(ArgReader)` / `Parse(CliBoundCommand)` + `ParseVisualThreshold` 方法。`CloneCommand.RunAsync(ArgReader)` 和 `RunAsync(CliBoundCommand)` 改为先调用 `CloneCommandOptions.Parse(...)`。
- **CloneInputLoader.cs**（新增，≈ 150 行）：包含 `LoadTokensAsync`、`LoadLayoutAsync`、`LoadBehaviorsAsync`、`LoadIconsAsync`、`LoadAssetsAsync`、`LoadPageAsync`、`LoadSectionsAsync` 共 7 个加载方法，统一返回 `(T value, int errorCode)` 模式，将 `RunCoreAsync` 中重复的 "检查路径 → 读 JSON → 反序列化 → 错误信息" 模式消除。
- **CloneAssetDownloader.cs**（新增，≈ 80 行）：将 `DownloadAssetsAsync` 提取为独立类，复用 `SsrfGuard`。同时承载 `WriteIcons` + `SanitizeFileName` + `CountBehaviors` 3 个写出/统计辅助方法。
- **CloneFidelityRunner.cs**（新增，≈ 100 行）：将 `RunFidelityAsync` + `WriteFidelitySiteYaml` + `TransferAssetsToStatic` 三个 fidelity-only 方法提取为独立类。`CloneCommand.RunCoreAsync` 通过 `CloneFidelityRunner.RunAsync(...)` 调用。

保留在 `CloneCommand.cs`：`RunAsync(ArgReader)`、`RunAsync(CliBoundCommand)`、`RunAsync(CliBoundCommand, ArgReader)`、`RunCoreAsync`（瘦化后只剩主编排，约 100 行）。

### 2. CloneContentWriter.cs（591 → ≤ 320 行）

- **CloneSectionDataWriter.cs**（新增，≈ 200 行）：提取 `GenerateSectionData`、`BuildSectionBody`、`GenerateStructuredIndex`、`AppendResponsiveCss`、`NormalizeSections`、`NormalizedSection` record、`PartialFor` 共 7 个 section-data 生成方法。
- **CloneContentCssWriter.cs**（新增，≈ 80 行）：提取 `GenerateCloneCss` + `IsSafeCssName` + `CssNameRegex`，专责 content-level 自定义 CSS 生成。
- **CloneContentAssetHelpers.cs**（新增，≈ 100 行）：提取 `BuildAssetMap`、`AssetFileName`、`LocalAssetPath`、`AssetSubdir`、`SectionDataKey`、`SectionSpecFileName`、`RewriteUrls` 重载、`RewriteUrl`、`NormalizeType`、`GenerateAssetManifest` 共 10 个资产/URL 辅助方法。
- 保留：`WriteTo`、`GenerateIndexContent`、`GenerateThemeYaml`、`CloneContentWriteResult` record、`Html`/`HtmlAttr`/`WriteFile`/`SanitizeSlug`/`SanitizeFileName`/`CommonPartials` 等通用辅助。

### 3. CloneThemeGenerator.cs（581 → ≤ 280 行）

- **CloneLayoutGenerator.cs**（新增，≈ 150 行）：提取 `GenerateBaseLayout`、`GenerateHeader`、`GenerateFooter`、`GenerateNavLinks` 共 4 个 HTML layout 生成方法。
- **CloneIndexPageGenerator.cs**（新增，≈ 200 行）：提取 `GenerateIndex`、`GenerateStaticSection`、`GenerateResponsiveCss`、`GenerateStateSection` 共 4 个 index 页面生成方法。
- 保留：`WriteTo`、`GenerateThemeYaml`、`SanitizeFileName`、`WriteFile` 等编排和通用辅助。

### 4. CloneFidelityGenerator.cs（566 → ≤ 300 行）

- **CloneFidelityHtmlParser.cs**（新增，≈ 180 行）：提取 `FidelityPage` 内嵌类（含 `ExtractBetween`、`StripBodyTags`、`SplitBodyIntoTopAndBottom`、`FindMainTagIndex`、`FindClosingTag`、`GetTagName`、`ExtractAssetPaths`、`AssetRegex`）整体外移为独立类。
- **CloneFidelityCommonBlocks.cs**（新增，≈ 120 行）：提取 `CommonBlocks` record、`ExtractCommonBlocks`、`FindLongestCommonPrefixLines`、`FindLongestCommonSuffixLines`、`FindClosingTagInString`、`FidelityPage_GetTagName`、`NormalizeBlock`、`CountIndent` 共 8 个 common-block 算法方法。
- 保留：`FidelityResult` record、`Generate`、`BuildLayout`、`BuildPageTemplate`、`BuildIndexTemplate`、`BuildListTemplate`、`WritePartial`、`CopyAssets`、`CopyStaticFiles`、`SanitizeTemplateName`。

### 5. CloneVerifier.cs（526 → ≤ 300 行）

- **CloneScreenshotComparer.cs**（新增，≈ 220 行）：提取 `ScreenshotComparison` record、`MissingScreenshotPair` record、`AffectedSection` record、`VisualVerifyResult` record、`CompareScreenshotFiles`、`FindMissingScreenshotPairs`、`ComparePngScreenshots`、`FindAffectedSections`、`AppendAffectedSections`、`ResolveSectionBounds`、`ExtractViewportName`、`RangesOverlap`、`SectionLabel` 共 13 项。
- 保留：`VerifyCloneAsync`、`WriteVerifyReport`、`WriteVerifyJsonReport`、`WriteBehaviorVerifyScript`、`CountFiles`、`CountThemeAssetFiles`、`ResolveConfigPathForCommand`。

### 6. CloneModels.cs（481 → ≤ 200 行）

按职责拆分为 4 个文件（保留 namespace `Bukit.Cli.Commands` 不变，所有 record 仍 `public`）：

- **CloneInputModels.cs**（新增，≈ 200 行）：`CloneTokens`、`SpacingScale`、`ResponsiveBreakpoints`、`CloneLayoutInfo`、`ClonePageInfo`、`ClonePageSeo`、`CloneSectionsDocument`、`CloneSectionInfo`、`CloneSectionButton`、`CloneSectionItem`、`CloneSectionAsset`、`CloneComponentInfo`、`CloneInteractionInfo`、`CloneBox`、`CloneViewportCapture`。
- **CloneNavModels.cs**（新增，≈ 80 行）：`NavLinkInfo`、`FooterLinkInfo`、`SectionInfo`、`SectionState`、`SectionResponsiveInfo`、`CloneViewportSectionInfo`。
- **CloneOutputModels.cs**（新增，≈ 80 行）：`CloneIcon`、`CloneAsset`、`CloneGenerationSummary`、`CloneBehaviors`。
- 保留 `CloneModels.cs`：仅保留 `IsSafeThemeName` 静态辅助方法和 partial regex（< 50 行）。

## Impact

- **Affected specs**：质量门禁中 `scripts/check-oversized.sh` 的检查范围（无文件加入 baseline，目标是所有 clone 文件远离 600 阈值）。
- **Affected code**：
  - `src/Bukit.Cli/Commands/Clone*.cs`（6 个原始文件 + 约 15 个新增类文件）
  - `src/Bukit.Cli/Commands/CloneCommand.cs`（行为不变，仅调用新提取的类）
  - `src/Bukit.Cli/Commands/CloneVerifier.cs`（行为不变，分担给新比较器）
  - `tests/Bukit.Cli.Tests/CloneCommandTests.cs`（仅验证调用方式不变，无需修改）
- **API 兼容**：所有 `public` 入口（`CloneCommand.RunAsync` / `CloneThemeGenerator.WriteTo` / `CloneContentWriter.WriteTo` / 所有 model record）签名不变。新增类型默认为 `internal`，model 拆分后所有 record 仍位于 `Bukit.Cli.Commands` 命名空间。
- **测试**：现有 `CloneCommandTests.cs` 等测试不需修改。新增类的单元测试可在后续 spec 中增量补充（本 spec 通过现有测试覆盖回归即可）。
- **零行为变更**：所有切分均为纯重构（move method/extract class），无逻辑、无 IO 顺序、无错误码语义改变。

## ADDED Requirements

### Requirement: Clone subsystem cohesion

The system SHALL split `Clone*.cs` files in `src/Bukit.Cli/Commands/` so that no clone-related file exceeds 400 logical lines, while preserving all public API signatures and external behavior.

#### Scenario: Build and tests pass after split

- **WHEN** developer runs `dotnet build bukit.slnx -c Release` and `dotnet test bukit.slnx -c Release`
- **THEN** build emits 0 warnings 0 errors and `Bukit.Cli.Tests` passes with the same count as before the split

#### Scenario: Public API preserved

- **WHEN** external caller invokes `CloneCommand.RunAsync(CliBoundCommand)` or `CloneCommand.RunAsync(ArgReader)` or `CloneThemeGenerator.WriteTo(...)` or `CloneContentWriter.WriteTo(...)`
- **THEN** the call compiles and executes with identical exit codes, file outputs, and console output as before

#### Scenario: Single responsibility per new file

- **WHEN** reviewer reads any newly extracted class (`CloneInputLoader`, `CloneAssetDownloader`, `CloneFidelityRunner`, `CloneSectionDataWriter`, `CloneContentCssWriter`, `CloneContentAssetHelpers`, `CloneLayoutGenerator`, `CloneIndexPageGenerator`, `CloneFidelityHtmlParser`, `CloneFidelityCommonBlocks`, `CloneScreenshotComparer`, `CloneCommandOptions`, `CloneInputModels`, `CloneNavModels`, `CloneOutputModels`)
- **THEN** each file's name describes a single responsibility and contains only methods/types matching that responsibility

### Requirement: Model files grouped by purpose

The system SHALL group clone-related record types into 4 files (`CloneInputModels`, `CloneNavModels`, `CloneOutputModels`, `CloneModels`) by their role in the clone pipeline, all sharing the existing `Bukit.Cli.Commands` namespace.

#### Scenario: Namespace unchanged

- **WHEN** existing code references `CloneTokens`, `CloneSectionInfo`, `NavLinkInfo`, `CloneIcon`, etc.
- **THEN** no `using` directive change is required because all model records remain in `Bukit.Cli.Commands` namespace

## MODIFIED Requirements

None — this is a pure refactor.

## REMOVED Requirements

None.
