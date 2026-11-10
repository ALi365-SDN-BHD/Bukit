# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。

## 1. 基线

- [x] 在 silkroutecc 站点用同一缓存、干净输出目录跑 `bukit build --metrics`，记录 `variantTotal`、总耗时与峰值内存，作为对比基线（已有一次：`variantTotal` 900 秒）。 证据：改动前 AOT 二进制（a4b98ede）`--clean --ci --metrics`：`variantTotal` 991059 毫秒，总耗时 1348 秒，峰值内存 2.72 GB，渲染 4651 页。

## 2. 共享模型

- [x] `ScribanTemplateRenderer` 内部按引用相等（`ReferenceEquals`，不用 record 的值相等）缓存最近一次的 `pages`（`IReadOnlyList<PageInfo>`）与 `site`（`SiteModel`）及其转换结果；键与值作为一个整体原子发布；用加锁的单次初始化保证每个来源最多转换一次（`Lazy<T>` 在 AOT 下触发 IL2091，故不用）；来源变化时整体替换，不累积旧批次。 证据：`SharedModelCache.cs`；`SharedModelTests.SharedCache_ReusesConvertedValuesForTheSameInstances`、`SharedCache_ConvertsAgainForDifferentInstances_EvenWhenValuesAreEqual`。
- [x] 渲染调度结束时清空缓存（`PageRenderDispatcher`/`RenderPipeline` 通过内部接口通知），不让缓存保留到报告阶段。 证据：`PageRenderDispatcher` 在 `finally` 中调用 `IRenderPassCompletion.CompleteRenderPass`；`RenderStageTimingTests.DispatchAsync_CompletesRenderPassWhenAPageFails`、`SharedModelTests.CompleteRenderPass_ReleasesSharedValues`。
- [x] `ScribanRootModelMapper.ToScriptObject(PageModel)` 使用共享值；根对象仍每页新建；`ScribanDerivedListAliasProjector` 覆盖 `pages`/`items` 的行为不变。 证据：`ScribanModelBinderTests` 全部通过（含派生列表别名用例）。
- [x] `ScribanRootModelMapper.ToScriptObject(ListPageModel)` 共享 `site`；`ReferenceEquals(model.Items ?? model.Pages, model.Pages)` 时只转换一次，`pages` 与 `items` 共用同一只读数组。 证据：`SharedModelTests.ListPage_SameInstanceForPagesAndItems_SharesOneReadOnlyArray`。
- [x] 新增内部递归冻结工具：遍历 `ScriptObject` 的值与 `ScriptArray` 的元素，带已访问集合（`content_model` 与 `content_record` 为同一实例），全部设为 `IsReadOnly`；冻结时预先触发 `ScriptArray` 的内部成员存储，避免并发读取时的延迟分配。 证据：`ScriptValueFreezer.cs`；`SharedModelTests.SharedValues_AreNestedReadOnly`。另：模板下标赋值绕过 `IsReadOnly`，新增 `BukitTemplateContext` 拦截。
- [x] 搜索内置主题、示例主题、测试夹具与 `/Users/ali/mydev/Git/Github/sitegen/themes/silkroutecc`，确认没有模板给 `site`、`pages[i]` 或其嵌套对象添加新键，也没有给其中的数组按下标赋值；有则先报告。 证据：99 个模板文件（含 silkroutecc 17 个）中只有 2 处成员赋值，均在 `SrbizTask4` 夹具 `related-entity.html` 中写模板自建的局部对象，不涉及共享值。

## 3. 计时

- [x] `BuildStageMetricsCollector` 增加按 `Stopwatch` 刻度累计的入口，快照时一次换算为毫秒；现有毫秒入口与 `metrics.json` 结构不变。 证据：`AddElapsedTicks`；`RenderStageTimingTests.AddElapsedTicks_AccumulatesSubMillisecondDurations`。
- [x] `PageRenderDispatcher`：`pageRender`、`listBuild`、`staticRender` 记录每页从读完正文之后到写出文件的耗时（不与 `bodyLoad`、`metadataHash` 重叠）。 证据：`RenderStageTimingTests.DispatchAsync_TimesContentListAndStaticRenders`。
- [x] `PageRenderDispatcher`：内容页、列表页、静态页都记录 `renderCall`（渲染器调用）、`htmlTransform`（HTML 变换）、`renderWrite`（写文件）。 证据：同上。
- [x] `renderModelBind`：`ScribanTemplateRendererAdapter` 实现内部计时接口，由 `ScribanTemplateRenderer` 报告模型转换耗时；外部渲染器不提供时不输出该项；公共 `ITemplateRenderer` 不变。 证据：`IRenderPassCompletion`；同上测试与 `DispatchAsync_OmitsModelBindForRenderersWithoutPassCompletion`。
- [x] `renderDispatch`：包住整个渲染调度（`DispatchAsync`）的墙钟时间。 证据：同上（在 `DispatchAsync` 内计时）。
- [x] 变体渲染前没有指标的步骤记录墙钟时间：`variantSetup`、`mediaResolve`（仅启用媒体本地化时）、`renderPlan`、`assetPrepare`、`seoPrepare`，使墙钟各项（不含页内累计项）之和与 `variantTotal` 相差小于 10%（计划问题 2 验收标准）。 证据：`ReportStageTimingTests.VariantStages_AccountForVariantTotal`；现场实测补计时前差额为 `variantTotal` 的 53%–90%。

## 4. 测试

- [x] 同一批次两次 `RenderPage` 共用同一个 `pages`、`site` 转换结果（引用相同）；不同 `pageIndex`/`SiteModel` 实例得到不同结果；值相等但引用不同的 `SiteModel` 也重新转换。 证据：`SharedCache_ReusesConvertedValuesForTheSameInstances`、`SharedCache_ConvertsAgainForDifferentInstances_EvenWhenValuesAreEqual`。
- [x] 模板给 `site`、`pages[0]`、嵌套 `site.data` 对象添加新键时报只读错误，且随后渲染的页面看不到该键（此测试在改动前必须失败，证明不是空测）。 证据：`SharedWrites_RejectNewKeysAndArrayAssignments_WithoutLeaking`（改动前 5 例全部失败，改动后通过）。
- [x] 模板执行 `pages[0] = x` 或给 `site` 内嵌套数组按下标赋值时报只读错误，其他页面看到原值（同样改动前必须失败）。 证据：同上（`pages[0] = null`、`site.data.values[0] = 'changed'`）。
- [x] 给已有成员赋值仍报错（保持现状）。 证据：`ExistingMemberAssignment_StillFails`。
- [x] 多线程并发渲染多页（含读取 `pages.<不存在的成员>`）时输出与串行一致。 证据：`ParallelRenders_MatchSequentialRenders`。
- [x] `ScribanModelBinderTests` 现有契约断言保持通过；派生列表页 `pages`/`items` 别名保持不变。 证据：`Bukit.Rendering.Tests` 368/368（含 `ScribanModelBinderTests`）；`Bukit.Architecture.Tests` 422/422（含 G-04D6B）。
- [x] 列表页：`Pages` 与 `Items` 为同一非空实例时 `pages`、`items` 为同一数组；`Items` 为空时同样共用。 证据：`ListPage_SameInstanceForPagesAndItems_SharesOneReadOnlyArray`。
- [x] 渲染输出：代表性站点夹具改动前后 `dist/` 逐字节一致。 证据：200 篇合成站点新旧版 `dist/` 逐字节一致；silkroutecc 新旧版仅 Notion 签名 URL 导致的 71 个 `assets/uploads/<哈希>` 文件名不同，替换该哈希后其余 HTML/JSON 全部一致。
- [x] 指标：带列表页的夹具上，`pageRender`、`listBuild`、`renderCall`、`htmlTransform`、`renderWrite`、`renderModelBind`、`renderDispatch` 大于 0；大量亚毫秒写入不被截断为 0。 证据：`DispatchAsync_TimesContentListAndStaticRenders`、`AddElapsedTicks_AccumulatesSubMillisecondDurations`。

## 5. 文档

- [x] 开发文档：共享模型只读约束（新增键、数组下标赋值会报错）与渲染细分指标含义（各项为工作线程累计时间，`renderDispatch` 为墙钟时间）。 证据：`guide/dev/rendering-scriban.md`「Shared Models」、`guide/dev/observability.md`「Metrics」。
- [x] 中英马 CHANGELOG（含兼容性说明）。 证据：三份 CHANGELOG `[Unreleased]`。

## 6. 验证

- [x] `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包并解决未映射文件。 证据：`/tmp/claude/codex-reports/stage1-closure.json`，`unmappedFiles` 为空。
- [x] `Bukit.Rendering`、`Bukit.Engine` 渲染与指标专项测试，公共 API 漂移检查，格式检查，`openspec validate fix-render-shared-model-cost --strict`，AOT 二进制编译。 证据：闭包 12 项命令串行全部通过（Rendering 368、Engine 3258、Architecture 422、PublicApiDrift、`dotnet format whitespace` 两个项目、两项 openspec 校验、文档检查、工作流自测），日志 `/tmp/claude/codex-reports/stage1-verify/final/`；AOT 发布无 IL/AOT 警告。
- [x] silkroutecc 同条件 `--metrics` 对比：记录 `variantTotal`、`renderDispatch`、`pageRender` 合计、总耗时与峰值内存；`renderDispatch` 与 `variantTotal` 之差应能由已计量的其他阶段解释；确认改动前后输出一致。 证据：同条件 `--clean --ci --metrics`，改动前 → 改动后：总耗时 1348 → 480 秒，峰值内存 2.72 GB → 651 MB，`variantTotal` 991059 → 88107 毫秒；改动后 `renderDispatch` 6043、`pageRender` 合计 53830（工作线程累计）、`renderModelBind` 3312。补齐渲染前计时后（非 CI 模式）`variantTotal` 18227 毫秒，墙钟各项之和 18998，相差 4.2%，其中 `mediaResolve` 9107、`renderDispatch` 6077。输出一致性见上（仅 Notion 签名 URL 导致的上传文件名差异）。
