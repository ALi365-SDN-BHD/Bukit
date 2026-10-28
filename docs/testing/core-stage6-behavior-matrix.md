# 阶段 6：行为矩阵

2026-10-28。范围和顺序见 `openspec/changes/complete-core-stage6-validation/tasks.md`。

**阶段 6：本轮授权范围已完成，审计七项问题已修复并经两次规定复核接受。** 审计修复时 Engine 2881 Passed / 0 Failed / 3 继承 Skip，覆盖率 89.91% ≥70%；专项与唯一最终增量复核均为 0 Critical / 0 Important / 0 Minor。KD-002/003/004 随后单独修复，当前分支完整 Engine 为 **2934 Passed / 0 Failed / 0 Skip，Engine 行覆盖 89.92%**（§7）。§5 原计数和复核保留为历史证据，§6 是审计修复证据，§7 是当前分支证据；不把继承缺陷、其他平台、AOT 或阶段 7 稳定性算作完成。

第一步只盘点入口和 76 个测试方法引用；盘点不等于通过。第四步以前的实际证据见 `tasks.md`，第五步历史证据见本文 §5，审计修复后的当前证据与复核见 §6。已有测试复用，不因盘点重复编写；未要求失败的纯数据投影不虚构异常契约。

## 1. 实际入口

- `src/Bukit-Core/Bukit.Engine/Plugins/PluginRegistry.cs` 的 `BuiltInPluginSource.GetPlugins` 注册十项：Analytics、DataFiles、PagesIndex、Taxonomy、Pagination、Archive、RelatedContent、Alias、Menu、ImageProcessing。
- `src/Bukit-Core/Bukit.Engine/PublishProjectionContract.cs` 的 `PublishProjectionAdapters.Aggregate` 承接 Feed、Sitemap、SearchIndex、LlmsTxt 等公共产物；同名旧插件测试不能单独证明当前接线。根多语言聚合走 `RootAggregate`。

## 2. 内置插件与发布投影

下表引用格式为 `测试类.方法`。失败列包含拒绝无效输入、取消、外部工具失败或按既有契约不输出；不是所有插件都应新增抛异常分支。

| 项 | 正常行为 | 失败／不输出行为 | 边界与接线 | 未闭环 |
|---|---|---|---|---|
| Alias | `AliasPluginTests.DerivePages_AliasesAsList_GeneratesRedirectPages` | `AliasPluginTests.DerivePages_AliasWithQueryOrFragment_Throws` | `AliasPluginTests.DerivePages_AliasWithTrailingSlash_Normalized` | KD-004 别名路径安全已在独立任务修复（主计划 5.4），当前完整 Engine 通过，见 §7 |
| Archive | `ArchivePluginTests.DerivePages_CreatesYearAndMonthArchivePages` | `ArchivePluginTests.DerivePages_EmptyContent_ReturnsEmptyList` | `ArchivePluginTests.Archive_EqualPublishAt_IsInputOrderIndependent`；`ArchivePluginTests.DerivePages_ItemWithoutCollection_IsIgnored` | 空集合不输出；无 collection 的文档不进入归档，不声称缺日期拒绝，见 §6 |
| DataFiles | `DataFilesPluginTests.DerivePages_LoadsYamlDataFile` | `DataFilesPluginTests.DerivePages_MalformedSupportedFile_ThrowsWithRelativePath` | `DataFilesPluginTests.DerivePages_TotalSizeLimitAcrossLanguages_FailsClosed`；`DataFilesPluginTests.OpenVerifiedDataFile_FileReplacedByExternalSymlink_Throws` | 本轮完整专项通过，见 §5 |
| ImageProcessing | `ImageProcessingPluginTests.AfterBuild_WebpGeneratesOwnedCandidatesAndProgressivePicture` | `ImageProcessingPluginTests.AfterBuildAsync_StaleVariantRebuildFails_RemovesVariantAndProjection` | `ImageProcessingPluginTests.AfterBuildAsync_CancellationKillsChildAndRemovesPartialOutput`；`ImageProcessingPluginTests.AfterBuildAsync_HistoricalSizeUserLookalikeWithoutOwnership_IsNeverDeleted` | 进程／原生工具证据仅代表实际平台 |
| Menu | `MenuPluginTests.AfterBuild_InjectsMenusIntoDataAndWritesJson` | `MenuPluginTests.AfterBuild_NoMenus_DoesNothing` | `MenuPluginTests.AfterBuild_WritesNestedChildren`；`MenuPluginTests.AfterBuild_ProjectsMenuDataWithoutConfigObjects` | 配置拒绝由配置层承担，不新增插件异常契约 |
| PagesIndex | `PagesByIdDataPluginTests.DerivePages_PopulatesSiteDataPagesById` | `PagesByIdDataPluginTests.DerivePages_WhenCallerCancels_StopsFetcherAndDoesNotWriteCache` | `PagesByIdDataPluginTests.DerivePages_ShouldUseFirstCanonicalRecord_WhenGraphContainsDuplicateI18nIds` | 本轮完整专项通过，见 §5 |
| Pagination | `PaginationPluginDerivePagesTests.DerivePages_MoreThanPageSize_GeneratesMultiplePages` | `PaginationPluginDerivePagesTests.DerivePages_NoPaginationCollection_ReturnsEmpty` | `PaginationPluginDerivePagesTests.DerivePages_ExactlyPageSize_ReturnsEmpty`；`PaginationPluginDerivePagesTests.DerivePages_WhenListRouteGraphOwnsPagination_ReturnsEmpty` | 无效 pageSize 属配置拒绝；接线不得重复所有权 |
| RelatedContent | `RelatedContentPluginTests.DerivePages_SharedTags_CreatesRelatedLinks` | `RelatedContentPluginTests.DerivePages_NotEnabled_DoesNotWriteRelatedData` | `RelatedContentPluginTests.DerivePages_SingleItem_NoRelated`；`RelatedContentPluginTests.DerivePages_HighThreshold_ReducesMatches` | 禁用用两个匹配文档及显式索引／阈值；禁用与单文档均断言实际 RelatedPages 不写入，非仅空 derived 返回值，见 §6 |
| Taxonomy | `TaxonomyPluginDerivePagesTests.DerivePages_GeneratesTermPagesForTags` | `TaxonomyPluginDerivePagesTests.DerivePages_OutputModeData_ReturnsEmpty` | `TaxonomyPluginDerivePagesTests.DerivePages_ZhCn_LocalizesConfiguredTermTitleAndPaginationSummary` | KD-004 重定向写入边界已修复（主计划 5.4）；`TaxonomyRedirectWriterTests` 尚未接入 `platform-tests` |
| Analytics | `AnalyticsHtmlTransformTests.Transform_RendersFourProviders_InConfigurationOrderAndCorrectLocations` | `AnalyticsHtmlTransformTests.Transform_WhenProviderFails_RecordsTransformFailed` | `AnalyticsHtmlTransformTests.Transform_IsIdempotent_AndRemovesManagedBlocksForRemovedProviders`；`AnalyticsReportWriterTests.WriteIfEnabled_WhenSerializationFails_PreservesPreviousReportAndLeavesNoTempFiles` | 实际报告必须绑定 v2 |
| Feed 投影 | `I18nMergedFeedProjectionTests.BuildAsync_MergedI18nFeeds_UseSharedStableLimitWindowInOutputsAndAudit` | `RssGeneratorTests.GenerateJsonFeed_RejectsTraversalBeforeWritingOutsideOutputRoot` | `RssGeneratorTests.AtomFeed_EmptyInput_IsByteStableAndUsesUnixEpoch`；`I18nMergedFeedProjectionTests.BuiltInPluginSource_DoesNotDoubleOwnProjectionAggregateOutputs` | 本轮完整专项通过，见 §5 |
| Sitemap 投影 | `SitemapGeneratorTests.Generate_BasicSitemap_HasCorrectXmlStructure` | `SeoAuditReportWriterTests.Build_ReportsInvalidSitemapXml` | `SitemapGeneratorTests.Generate_EmptyRoutes_ProducesEmptyUrlset`；`SiteEngineIntegrationTests.BuildAsync_I18nFilteredListRoutes_EmitMutualHreflangInHtmlAndSitemap` | 本轮完整专项通过，见 §5 |
| SearchIndex 投影 | `SearchIndexBuilderTests.GenerateSingleSearchIndex_WithGraphOnlyListRoute_IncludesListRoute` | `ConfigValidatorTests.Validate_SearchMaxContentLengthNonPositive_Throws` | `I18nMergedSearchIndexTests.GenerateRootOutputs_SplitAndIndexModesKeepCappedLanguageSearchFiles` | KD-007 见下表 |
| LlmsTxt 投影 | `I18nMergedFeedProjectionTests.BuildAsync_MergedI18nGeo_GeneratesRootLlmsAndRobotsThroughProjectionInventory` | `LlmsProjectionIntegrationTests.BuildAsync_LlmsTxt_ExcludesNoindexAndExpiredRoutes` | `SeoAuditReportWriterTests.Build_ExcludedRoutePresentInLlmsOutput_ReportsLeakWarning` | KD-007 隐藏正文／链接不得进入 llms-full |

## 3. 搜索、SEO、GEO、审计

| 范围 | 已有正常／边界对照 | 已有负例或缺口 | 本轮动作 |
|---|---|---|---|
| 搜索文本、实体、长度与片段 | `SearchIndexBuilderTests.StripHtmlToText_HtmlEntities_AreDecoded`；`SearchIndexBuilderTests.SearchProjection_DoesNotSplitSurrogatePairAtContentLimit`；`SearchIndexBuilderTests.SearchProjection_MaxContentLengthDoesNotLimitGeneratedSnippet` | `SearchIndexBuilderTests.StripHtmlToText_SeparateBlocks_PreserveTextBetweenThem`；`MarkdownBodyProjectionTests.SvgCdataBeforeScript_DoesNotExposeHiddenLink` | 第四步修复并移除 KD-007 Skip；定向 133/133；保留引号、无效结束标签、脚本转义、raw-text 和 SVG 回归 |
| SEO 标题／canonical／alternates／OG | `SeoModelBuilderTests.BuildForContent_CustomPageTitleTemplate_SeparatesDocumentAndSemanticTitles`；`SeoModelBuilderTests.BuildForContent_CustomCanonicalOverrides`；`SeoModelBuilderTests.BuildForContent_WithAlternates`；`SeoModelBuilderTests.BuildForContent_ImageSource_SiteDefaultUsedAsLastResort` | `SeoAuditReportWriterTests.Build_ReportsCanonicalThatIsRelativeOrHasFragment`；`SeoDiagnosticsTests.AnalyzeIndex_StrictMode_ThrowsWhenHreflangMissingXDefault` | 复用现有用例，不把测试数量当覆盖证明 |
| JSON-LD／FAQ／HowTo／引用 | `GeoSeoModelBuilderTests.BuildForContent_WithGeoFaq_GeneratesFaqPageJsonLd`；`GeoSeoModelBuilderTests.BuildForContent_WithGeoHowTo_GeneratesHowToJsonLd`；`GeoSeoModelBuilderTests.BuildForContent_WithGeoCitations_GeneratesCitationsJsonLd` | `GeoDiagnosticsTests.AnalyzeIndex_StrictMode_ThrowsOnGeoFaqEmptyQuestion`；空条目集合省略无效节点 | 第三步 GEO/诊断/报告消费者 85/85，warn/strict/off 均验证；citation 与 mentions 并存保留 |
| 发布与机器可读性／信任 | `PublishAuditReportWriterTests.Write_WritesPublishDocumentFactsFromContentTrustGraph` | `PublishAuditReportWriterTests.Build_ReportsTrustGraphCompletenessGaps`；`RepresentationAuditRulesTests.Analyze_DeclaredJsonButFileMissing_AddsError`；`SeoAuditReportWriterTests.Build_ReportsProjectionContentMismatches` | 最后验证真实报告与公共文件的对应关系 |
| 隐私／路由清单／哈希 | `BuildReporterTests.WriteIfEnabled_WritesRoutesInStableOrder`；`BuildReporterTests.ReleaseChecksums_ExcludeOnlyRootRecoveryState_AndMatchCompletedBytes` | `BuildReporterTests.CreateSecurityReportData_FailsWhenPublicOutputContainsKnownNotionIdentifiersOrProviderMarkers`；`BuildReporterTests.WriteIfEnabled_DoesNotFollowVariantAnalyticsReportSymlinkOutsideOutputRoot` | 已修复的历史 schema gap 不重复列为 runtime 缺陷 |
| 日志并发 | `BuildReporterTests.BuildDiagnosticLogger_ConcurrentForwardersShareAtomicCounts` | 调用方 ILogger 须线程安全，不保证跨 worker 顺序 | 第三步 ConsoleLogger 21/21，含 2000 条并发完整 JSON 事件；XML 与正式文档已明确契约 |
| SCSS 多语编译 | `SiteEngineIntegrationTests.BuildAsync_ScssEnabled_PreservesSourceAndCompilesOnceAcrossLanguages` | 历史缺 sass.log 的根因未明；失败日志已补 | 最终专项观察并保留未决诊断边界，不声称历史根因已修复 |

### 3.1 审计反例与实际修复语义

| 反例 | 当前直接回归 | 行为证明边界 |
|---|---|---|
| I-01 原始 FAQ/HowTo 不完整条目先被 parser 丢弃 | `GeoDiagnosticsTests.AnalyzeIndex_RawIncompleteItems_DiagnosesBeforeFilteringJsonLd`；`GeoDiagnosticsTests.AnalyzeIndex_RawMissingOrEmptyList_RetainsMissingListPolicy` | 原始字段图经 ContentDocument、BuildForContent 到 AnalyzeIndex；72 个缺字段／空白／非 map／null、混合／全无效、warn/strict/off 用例，加 12 个缺／空列表对照。诊断保留全部条目，JSON-LD 只输出完整条目、连续 step 位置，全无效无专用节点／无 Article 回退 |
| I-02 HTML template 内锚点覆盖外层 | `MarkdownBodyProjectionTests.HtmlTemplateContents_CannotOverwriteOuterAnchor`；`MarkdownBodyProjectionTests.UnclosedHtmlTemplate_DoesNotCloseOuterAnchor`；`MarkdownBodyProjectionTests.ForeignTemplate_IsNotAnInertHtmlAnchorScope`；`MarkdownBodyProjectionTests.ExplicitlyClosedMalformedOuterAnchor_StillKeepsOnlyInnermostTarget` | 实际 HTML template DOM 作用域保存／恢复外层状态；嵌套、空、未闭合、HTML 自闭合拼写、SVG 自闭合与 foreignObject 对照；真正畸形锚点及非惰性 foreign template 保持既有最内层策略 |
| I-03 scripting-enabled noscript 字面 a 被再解释为链接 | `MarkdownBodyProjectionTests.Noscript_LiteralAnchorRemainsTextWithoutBecomingALink`；`SearchIndexBuilderTests.StripHtmlToText_Noscript_PreservesLiteralMarkupAndFollowingText` | 原字面标签／正文保留，实际 Markdig LinkInline 仅 `/visible`；不是可执行 XSS 的证明，unsafe-URI 拒绝未放宽 |
| M-01 替换 raw-text 节点无 SourceReference，搜索粘字 | `SearchIndexBuilderTests.StripHtmlToText_NormalizedRawText_PreservesWordBoundaries`；`MarkdownBodyProjectionTests.NormalizedRawText_PreservesWordBoundaries` | xmp/iframe/noembed/noframes 精确 `before example after`；不为隐式 html/head/body 加空格 |
| 三个实际公共产物 | `SiteEngineIntegrationTests.BodyProjection_ParsingContextsPreservePublicTextAndActualLinks` | template、noscript、xmp × search、文档 Markdown、llms-full 九个独立真实构建断言；Markdown／llms 检查真实 LinkInline 目标，不只查字串缺失 |

M-02/M-03 仅加强 RelatedContent/Archive 测试与引用，未改变生产插件／缺日期策略；M-04 只移除继承的测试冗余 brace block。方法引用数是清单计数，不是行为充分性或 mutation 测试证明。

## 4. 24 项报告正反例台账

`docs/testing/core-report-inventory.json` 的 24 项均已有本地 schema。14 份 Engine 构建报告由 `TestInfrastructureTests.RealSites_BuildTwice_WithExactPublicBytesAndIndependentReports` 的实际构建及变异检查承接；四份 CLI writer、CLI error、PluginHost reporter 和四项后补 schema 分别由其实际 writer／构建用例承接。

每行原件均由真实 writer／构建生成；负例从该原件克隆，只改变表内字段，并要求错误包含该字段。所有 schema 的完整路径与版本绑定以 JSON 清单为准；本地 schema URL 不代表已部署。

| 报告 id | 真实产物测试组 | 错误类型字段 |
|---|---|---|
| build-report | Engine 构建 | durationMs |
| routes | Engine 构建 | routes |
| assets | Engine 构建 | assets |
| incremental-manifest | Engine 构建 | enabled |
| security-report | Engine 构建 | checks |
| release-bundle-checksums | Engine 构建 | fileCount |
| artifact-manifest | Engine 构建 | artifactCount |
| build-manifest-digest | Engine 构建 | reportCount |
| publish-url-snapshot | Engine 构建 | routes |
| seo-report | Engine 构建 | routes |
| publish-audit-report | Engine 构建 | documents |
| seo-route-map | Engine 构建 | routes |
| geo-report | Engine 构建 | geoScore |
| analytics-report | Engine 构建，v2 | processedHtml |
| seo-insights-report | CLI 真实 writer | sources |
| seo-question-insights-report | CLI 真实 writer | sources |
| generative-citation-report | CLI 真实 writer | sources |
| external-authority-report | CLI 真实 writer | sources |
| cli-error | 真实 renderer 与 CLI stderr | exitCode；command 错误类型与未知字段另测 |
| plugin-execution-report | 真实 reporter | success；resourceLimitExceeded、networkPermissionGranted 错误类型与未知字段另测 |
| agent-manifest | 真实多语构建，根与 en/zh-CN | documents、嵌套 route、同类型错误版本 |
| metrics | 真实多语构建 | variants、嵌套 routed、同类型错误版本 |
| search-index-index | 真实多语构建 | indexes、嵌套 language、同类型错误版本 |
| theme-catalog | 真实 writer，非空及空主题 | sections、非空主题嵌套 name |

KD-011 保留 null command 省略，KD-012 声明生产 reporter 已有的可选字段；schema ID/版本不变、错误类型及未知字段仍拒绝。清单中已修复的 publicOutputPrivacy、完成后哈希、lastModified=null 和可选 reporter 字段旧 `knownGap` 已清理，历史失败证据保留在主计划。FAQ/HowTo、logger 和 KD-007 的实际决议与专项证据见前三节及 `tasks.md`。

## 5. 审计前历史最终运行证据

证据根 `R=/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-completion-8WEr6X`。

| 验证 | 当时结果（历史） | 证据（相对 R） |
|---|---|---|
| 完整 Engine 专项 | 2764 Passed / 0 Failed / 3 KnownDefect Skip | `05-engine-after/engine.trx`、`05-engine-after.log` |
| Engine 行覆盖 | 14686/16335 = **89.91% ≥70%** | 上述 TRX 唯一引用的 Cobertura 附件；`05-verification-summary.json` |
| CLI 报告与直接消费者 | 100/100，0 Skip | `05-cli/cli.trx` |
| PluginHost reporter 与调用方 | 26/26，0 Skip | `05-plugin/plugin.trx` |
| ConsoleLogger | 21/21，0 Skip | `05-shared/shared.trx` |
| Architecture 直接 owner | 48/48，0 Skip | `05-owner/owner.trx` |
| 清单 owner 自检 | 6/6；24 项/24 schema | `core-report-inventory.py --self-test` 输出 |
| 矩阵与报告逐项核对 | 76 个方法引用全部有当前 Passed 结果；24 项实际生成正反例通过 | `05-verification-summary.json`；各报告绑定的 TRX |

首轮 Engine 为 2763 Passed / 1 Failed / 3 Skip（`05-engine/engine.trx`）：旧 publisher 用例用空 FAQ，违反本轮已明确的“空集合不输出 FAQPage”。仅为该用例补真实问答并断言 mainEntity，保留原 publisher 拒绝断言；相关消费者 51/51 后重跑得到上表结果。失败采集不作为覆盖率出口。

覆盖率使用 TRX 的 runDeploymentRoot/In/附件路径，且与 collector 输出副本的 SHA-256 相同；按仓库 `coverage/summarize.py` 的路径+行号归并，仅判断 Bukit.Engine，不把所有被插桩依赖的总数当全 Core 覆盖率。HtmlTextProjection 为 114/118；四个双转义 dash 状态分支未由项目覆盖率命中，外部浏览器组合对照单列，不合并进项目覆盖率。MarkdownBodyProjection 65/65。覆盖比例不代表完整 HTML 安全证明。

SCSS 用例在完整 Engine 本次通过，源 SCSS 保留且三个语言只编译一次；历史缺 sass.log 根因仍未明，未增加盲目重试或扩大工具超时。三个 Skip 仅是 KD-003 两个、KD-004 一个，均未计入通过；本轮 KD-006/007/011/012 无 KnownDefect Skip。

历史第五步专项复核 `05-specialty-review.md`：Critical 0 / Important 0 / Minor 0。历史 delta-only 最终复核 `final-unified-review.md`：Critical 0 / Important 0 / Minor 1（继承的无行为影响测试冗余块），当时批准收口。其后独立审计发现 §3.1 反例，故该完成状态已重新打开，不能替代新修复的复核。

历史最终状态更新后检查静态文档和 974 项实现输入不变；原工作树 24 个未提交文件及 HEAD 保持原样。上述历史缓存不因本次 Engine 修复而冒称当前闭包 HIT；未提交、推送或合并。

## 6. 审计修复后的当前专项证据与已接受复核

当前证据根 `R=/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-repairs-APNcZD`；确切命令见 R/commands.json 及 `docs/plans/2026-10-28-core-stage6-audit-repairs.md`。

| 命令 key | 实际结果 | 证据（相对当前 R） |
|---|---|---|
| geo-red | 67 Passed / 72 Failed / 0 Skip，预期 RED | `geo-red.log`、`geo-red/tests.trx`、`geo-red-receipt.json` |
| geo-green | 139 Passed / 0 Failed / 0 Skip | `geo-green.log`、`geo-green/tests.trx`、`geo-green-cache.json` |
| projection-red | 157 Passed / 18 Failed / 0 Skip，RED | `projection-red.log`、`projection-red/tests.trx`；其中两个额外 foreign-template oracle 在调查后改为既有非惰性策略对照，主审计反例预期未放宽 |
| projection-green | 176 Passed / 0 Failed / 0 Skip | `projection-green.log`、`projection-green/tests.trx`、`projection-green-cache.json`；比 RED 多一个空 HTML template 对照 |
| 完整受影响 Engine 专项 | **2881 Passed / 0 Failed / 3 inherited NotExecuted** | `engine.log`、`engine/engine.trx`、`engine-receipt.json`、`engine-cache.json` |
| Engine 行覆盖 | **14702/16351 = 89.91% ≥70%** | 成功 TRX 自身引用的 Cobertura，`final-evidence.json`；只按 Engine 源路径+行号归并，不合并失败／中断采集 |

首次 Engine 后台工具调用误设 60000ms timeout，终止进程且无 TRX；日志及基础设施失败回执保存在 `engine-interrupted.*`。确认无剩余 testhost 后，未改运行输入，以无工具 timeout 重启同一确切命令获得上表成功证据；不是为刷新缓存重复 fixture。GEO 首个 wrapper 的无效队列转换也单独记录，原 RED 测试不重跑。详见 `implementation-report.md`。

当前矩阵共 **88 个唯一测试方法引用**，全部在本次完整 Engine TRX 有 Passed 结果；这是方法引用核对，不表示每个分支都被证明。新增 117 个执行用例：84 GEO、33 projection；Archive/RelatedContent 改正既有方法而非增加数量。三个未执行项仍为 KD-003 两个、KD-004 一个，不计通过。SCSS 本次完整专项通过，不声称已修复历史偶发根因。

`runtime-frozen-inputs.json` 冻结成功完整专项的 957 项运行输入；原 974 项已验证输入中，授权路径外全部不变，原 44 个已改文件中授权路径外 28 项逐字节不变；HEAD 不变。只改三个 Engine 生产 helper、六份相连测试和授权文档，无公开模型声明、schema、依赖版本或插件生产策略变化。后续仅文档状态更新不重跑 fixture；GEO focused cache 在后续测试变化后仅作历史 TDD，完整 Engine 是当前闭包证明。

§5 的 CLI 100/100、PluginHost 26/26、ConsoleLogger 21/21、Architecture 48/48 保留为未变更领域的历史结果，本次未重跑、未称为当前完整闭包 HIT。当前 R 的一次 combined spec-then-quality 专项复核 `specialty-review.md` 和一次 delta-only 最终复核 `final-unified-review.md` 均为 0 Critical / 0 Important / 0 Minor，接受全部七项修复。OpenSpec 3/4/5 均完成；最后仅回填验收状态，保持 957 项冻结运行输入不变，不为文档状态刷新重跑 fixture。完成声明仅限本轮授权的阶段 6 范围。

KD-002/003/004、全 Core 覆盖率、stage-7 连续二十次稳定性、发布与全解决方案门禁不属于本轮新增授权。独立 JSON Schema 验证器交叉检查是可选增强，不新增为硬出口。

## 7. 当前分支完整 Engine 复验（2026-10-29）

§6 的证据根位于系统临时目录且已被清理，并早于 KD-003/KD-004 的 Engine 改动。经授权对 `codex/stage6-completion` 的 `79df55f8` 重跑完整 Engine 专项及覆盖率；持久证据根 `R=/Users/ali/.local/state/bukit-verification/stage6-engine-current-EzuJkM`，确切命令与边界见主计划 §6.10。

| 验证 | 结果 | 证据（相对 R） |
|---|---|---|
| 完整 Engine 专项 | **2934 Passed / 0 Failed / 0 Skip**，退出码 0；无 KnownDefect Skip | `engine/engine.trx`、`engine.log` |
| Engine 行覆盖 | **14701/16349 = 89.92% ≥70%** | `coverage/coverage-summary.json`、`final-evidence.json` |
| 缓存 | 记录后检查 HIT | `engine-cache.json` |

一个 Windows-only NOT_APPLICABLE 用例在 macOS 记为 Passed，不算 Windows 证明。CLI、PluginHost、Shared、Architecture 专项及 Linux/Windows `platform-tests` 本次未运行。
