# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。

## 1. 计时

- [x] 变体范围：`projections`、`media-validate`（仅启用媒体本地化时）、`output-record`、`seo-audit`、`analytics-report` 各输出一条 `event=build.report.timing scope=variant step=… duration_ms=…`，最后输出 `step=total`（该范围墙钟时间）；多语言时带 `language=`。 证据：`ReportStageTimingTests.SingleLanguageBuild_LogsVariantAndFinalizeStepsInOrder`；1000 篇合成站点（启用媒体本地化）实测 `projections 62637`、`media-validate 23284`、`output-record 1`、`seo-audit 8315`、`analytics-report 10`、`total 94258`。
- [x] 单语言汇总（`SiteEngine`）：`metrics-write`（仅指定 `--metrics` 时）、`collect-and-clean`、`output-inventory`、`security-data`、`security-report`、每个运行的写入器 `writer:<类型名>`、`security-gate`，最后 `scope=finalize step=total`。 证据：同上单语言测试。
- [x] 多语言汇总（`I18nWorktreeBuild.FinalizeMultiLanguageOutputsAsync`）：`collect-and-clean`、`root-outputs`、`seo-audit-merged`、`metrics-write`（仅指定 `--metrics` 时）、`output-inventory`、`security-data`、`security-report`、`writer:<类型名>`、`security-gate`，最后 `scope=finalize step=total`。 证据：`MultiLanguageBuild_LogsEachVariantWithLanguageAndOneFinalizeScope`。
- [x] 功能关闭而直接返回的步骤不输出；`security-report` 总是输出。 证据：`ReportsDisabled_LogsOnlyWritersThatRun`、`MetricsRequested_LogsMetricsWriteInPathOrder`（未指定 `--metrics` 时不输出 `metrics-write`，专项审查 I1 修复）；`media-validate` 仅在媒体本地化启用时输出。
- [x] 某步抛异常时原样传播，该步与该范围 `total` 不输出。 证据：`FailingStep_PropagatesWithoutLoggingStepOrTotal`。
- [x] 计时不改变报告内容、写入顺序、并发方式、异常传播与警告/错误计数。 证据：200 篇合成站点新旧版对比，报告只有 `generatedAt`/`startedAt`/`endedAt`/`durationMs` 不同；引擎测试 3255 个全部通过。

## 2. 测试

- [x] 单语言构建夹具：日志按顺序包含上述全部已执行步骤与两个 `total`，`duration_ms` 为非负整数；每个范围各步之和与 `total` 相差小于 10%（短于 500 毫秒的范围允许 50 毫秒）。 证据：单语言测试中 `AssertStepsAddUpToTotal`。
- [x] 多语言构建夹具（两种语言）：每种语言各有一组带 `language=` 的变体行与 `total`；汇总行不带 `language=`，包含 `root-outputs` 与 `seo-audit-merged`，顺序与上文一致。 证据：多语言测试。
- [x] 报告关闭（`build.report.enabled: false`）时汇总只输出 `security-report` 与实际运行的写入器。 证据：`ReportsDisabled_LogsOnlyWritersThatRun`。
- [x] 现有报告内容测试保持通过；警告与错误计数不变。 证据：`Bukit.Engine.Tests` 3255/3255。

## 3. 文档

- [x] 开发文档：报告阶段计时日志说明（字段、范围、两条路径的步骤清单）。 证据：`guide/dev/observability.md`「Metrics」。
- [x] 中英马 CHANGELOG。 证据：三份 CHANGELOG `[Unreleased]`。

## 4. 验证

- [x] `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包并解决未映射文件。 证据：`/tmp/claude/codex-reports/stage1-closure.json`，`unmappedFiles` 为空。
- [x] `Bukit.Engine` 报告相关专项测试，格式检查，`openspec validate observe-report-stage-timing --strict`。 证据：同上闭包结果；`ReportStageTimingTests` 7/7。
- [x] 现场构建：记录各步骤耗时并按占比排序，写回汇总文档。 证据：silkroutecc（3547 条内容、4651 页，非 CI 模式）变体范围 total 323579 毫秒：`projections` 212899（65.8%）、`seo-audit` 61220（18.9%）、`media-validate` 49446（15.3%）、`output-record` 8、`analytics-report` 3；汇总范围 total 6423 毫秒：`security-data` 4299（66.9%）、`writer:AssetsReportWriter` 754、`writer:ReleaseBundleChecksumsReportWriter` 671、`output-inventory` 327、其余均小于 250。已写回汇总文档。
