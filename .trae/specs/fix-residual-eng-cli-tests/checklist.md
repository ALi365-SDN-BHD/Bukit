# Checklist

## 分类 A: GetCollection
- [x] `GetCollection_WithNeither_ReturnsPage` — assert `""` not `"page"`

## 分类 B: Route Generation
- [x] `Execute_GeneratesContentRoutesWithCollectionRulesAndListRoutes` — passes
- [x] `DerivePages_WithMarkdownProviderOutput_PopulatesIndex` — passes

## 分类 C: CollectionWarningStage (7 tests)
- [x] `ExecuteAsync_CustomTypeWithoutCollection_NoWarning` — passes
- [x] `ExecuteAsync_TypePageWithoutCollection_EmitsWarning` — passes
- [x] `ExecuteAsync_TypePostWithCollection_EmitsConflictWarning` — passes
- [x] `ExecuteAsync_TypePostWithoutCollection_EmitsWarning` — passes
- [x] `ExecuteAsync_TypeWithNonPostPageCollection_NoWarning` — passes
- [x] `ExecuteAsync_MultipleItems_MultipleWarnings` — passes
- [x] `ExecuteAsync_HasCollection_NoWarning` — passes

## 分类 D: PageRenderDispatcher
- [x] `RenderSpecialListsAsync_HydratesBodies_WhenModeIsAuto` — passes

## 分类 E: BuildPipelinePerformance (2 tests)
- [x] `FullBuild_With10Pages_CompletesUnderThreshold_AndAllStageKeysPresent` — passes
- [x] `FullBuild_With1Page_ProducesAllExpectedOutputFiles` — passes

## 分类 F: CLI Import (4 tests)
- [x] `ContentSourceNotion_DefaultsToBuildableMarkdownWithNotionSeed` — passes
- [x] `Verify_UsesGeneratedSiteConfigAndBuilds` — passes
- [x] `Verify_ListPages_DoNotConflictWithCollectionListRoutes` — passes
- [x] `ImportThenDoctor_DoesNotWarnForSeoFieldAccessOrBaseUrlAssets` — passes

## 最终验证
- [x] Engine: 0 failures (1113/1113)
- [x] CLI: 0 failures (834/834)
- [x] Architecture: 0 failures (12/12)
- [x] All other test suites: 0 failures
