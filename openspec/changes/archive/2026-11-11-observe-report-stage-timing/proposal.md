# Observe report stage timing

## Why

丝路文化站点本地完整构建约 21 分钟，其中「审查和报告」阶段约 5 分钟，期间不写任何页面，也没有任何分项计时。报告阶段在单语言与多语言两条路径上的步骤与顺序不同：

- **变体范围**（两条路径相同，`VariantBuildPipeline`）：`variantTotal` 停止计时之后，依次执行内容投影写入（`DefaultContentProjectionWriter`）、媒体输出校验（启用媒体本地化时，`ContentMediaOutput.ValidateOutputs` 会重新读取并扫描每个已渲染的 HTML 文件）、记录本次输出清单（`PublicOutputLifecycle.Record`）、`VariantReportStage` 中的 `SeoAuditReportWriter`（SEO、发布审查与 GEO 报告）与 analytics 报告。多语言时各变体并行执行。
- **单语言汇总**（`SiteEngine.cs:179-206`）：`MetricsWriter` → `PublicOutputLifecycle.CollectAndClean` → `BuildOutputInventory.Create` → `BuildReporter.CreateSecurityReportData` → `BuildReporter.WriteIfEnabledAsync`（先总是写 `security-report.json`，再运行 `BuildReportWriterPlan` 中最多 8 个写入器）→ `BuildReporter.EnforceSecurityGate`。
- **多语言汇总**（`I18nWorktreeBuild.FinalizeMultiLanguageOutputsAsync`，`I18nWorktreeBuild.cs:254-317`）：`CollectAndClean` 在 `MetricsWriter` **之前**；另有 `I18nOutputMerger.GenerateRootOutputs`（根投影与站点地图）与 `SeoAuditReportWriter.WriteMerged`；之后同样是清单、安全数据、报告写入与安全门禁。

按 `docs/plans/2026-11-10-build-time-incremental-remediation-plan.zh-CN.md`，渲染修复之后报告阶段将成为最大的一块，且增量构建跳不过它。没有分项数据就无法决定优化方向。

## What Changes

- 每一步输出一条结构化信息日志：`event=build.report.timing scope=<variant|finalize> step=<名称> duration_ms=<毫秒>`，按执行顺序输出；每个范围最后输出 `step=total`，其值为该范围的**墙钟时间**（从该范围第一步开始到最后一步结束）。
- 步骤清单（按路径）：
  - 变体范围：`projections`、`media-validate`（仅启用媒体本地化时）、`output-record`、`seo-audit`、`analytics-report`；多语言时（变体有默认语言）每行带 `language=<代码>`，每个变体各自输出 `total`。除 `media-validate` 外每步每次都会执行（analytics 关闭时也会删除旧报告文件），因此总是输出。实施后在 1000 篇合成站点上发现，未计时的媒体输出校验使各步之和比 `total` 少约 23 秒，因此补入这两步。
  - 单语言汇总：`metrics-write`（仅指定 `--metrics` 时）、`collect-and-clean`、`output-inventory`、`security-data`、`security-report`、每个运行的写入器 `writer:<类型名>`、`security-gate`。
  - 多语言汇总：`collect-and-clean`、`root-outputs`、`seo-audit-merged`、`metrics-write`（仅指定 `--metrics` 时）、`output-inventory`、`security-data`、`security-report`、`writer:<类型名>`、`security-gate`（不带 `language=`）。
- 只为实际执行的步骤输出：功能关闭、调用直接返回的步骤（如 SEO 审查或 analytics 关闭）不输出；`security-report` 总是写出，总是输出。
- 某步抛出异常时异常原样传播，该步与该范围的 `total` 不输出。
- 只加计时与日志，不改变任何报告内容、写入顺序、并发方式、异常传播、警告与错误计数或 `metrics.json` 结构。日志级别为 info，不新增配置项。
- 开发文档写明这些日志的含义；中英马 CHANGELOG 同步。

## Impact

- 受影响代码：`Bukit.Engine/VariantBuildPipeline.cs`、`VariantReportStage.cs`、`BuildReportPipeline.cs`、`SiteEngine.cs`、`BuildReporter.cs`、`I18nWorktreeBuild.cs`（`FinalizeMultiLanguageOutputsAsync`）；新增内部 `ReportStageTimer.cs`。`I18nOutputMerger` 只被调用，不修改。
- 受影响测试：新增单语言与多语言日志断言测试；现有报告内容测试保持通过。
- 规格：`library-logging` 新增一项要求（报告阶段计时日志）。选择该能力而非 `core-report-contracts`，因为这些日志不改变任何报告结构。
- 公共契约：无公共 API 变化；报告文件与 `metrics.json` 不变；构建日志多出若干 info 行。
- 与其他变更的关系：与 `fix-render-shared-model-cost` 同一发布，一次现场构建同时得到渲染与报告两部分的新数据。
- 用户批准范围：按计划实施；提交与发布另行确认。

## Non-Goals

- 不优化任何报告写入器（另立变更，依据本变更的数据）。
- 不把报告计时写入 `metrics.json`（它在多数报告步骤之前写出，改顺序会改变现有契约）。
- 不为构建后插件单独加计时：它们在 `variantTotal` 之内，`metrics.json` 已有按插件的耗时。
- 不改变 `--ci` 下的安全门禁与审查行为。
