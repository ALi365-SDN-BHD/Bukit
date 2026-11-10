# Convert shared template models once per render pass

## Why

丝路文化站点（silkroutecc 主题，约 4650 页）在 Bukit 2.0.12 下，有缓存时 CI 构建仍需约 29 分钟。本地用 `--metrics` 实测（干净输出目录、现有缓存）：

- 渲染阶段 `variantTotal` 为 900 秒（15 分钟）；其中可计量的各项（`bodyLoad` 0.09 秒、`metadataHash` 4.6 秒、`taxonomySetup` 0.7 秒、analytics 插件 4.9 秒等）合计不到 15 秒。
- `PageRenderDispatcher` 中 `pageRender`、`listBuild`、`staticRender` 的耗时被写死为 0（第 209、258、289 行），剩下约 890 秒没有任何指标。生产路径的 `listBuild` 一直为 0（`SpecialListRenderer` 里的真实计时只在测试路径 `RenderSpecialListsAsync` 中被调用）。
- 渲染期间 CPU 只用到约 2 个核，进程内存约 870MB，调用栈采样大量出现 `memset`、`mprotect`；设置服务器 GC 环境变量后耗时几乎不变（1254 秒对约 1260 秒）。该环境变量在 AOT 二进制上是否生效尚未验证（计划 V4），因此不据此判断瓶颈与回收策略无关。
- 合成站点（3533 篇、silkroutecc 主题与同一配置）上用 `dotnet-trace` 对渲染线程采样 60 秒：`pages` 转换占 63.7%，`site` 转换占 33.3%，模板执行 0.8%，HTML 变换与写文件各 0.2%（证据见 `/tmp/claude/codex-reports/stage0a-evidence.md`；合成数据，非真实 Notion 字段）。

原因在模型转换：

1. `PageRenderDispatcher` 为整个渲染批次构建一次全站页面索引 `pageIndex`（第 72-97 行），内容页与静态页都设 `Pages = pageIndex`（第 199、279 行），且同一批次使用同一个 `siteModel` 实例。
2. `ScribanRootModelMapper.ToScriptObject(PageModel)` 每渲染一页都调用 `ScribanListModelMapper.ToPageInfoScriptArray(model.Pages)`，把索引中每一条重新转换成 `ScriptObject`，并逐层展开 `fields`、`content_model`、`entities`、`provenance`、`trust`、`representation_urls`、`seo`。模板是否使用 `pages` 都会执行。
3. `site` 对象（含 `params`、`modules`、`data`、`data_index`、`data_files`、`related_pages`）也每页重新转换一次。`site.data` 含 `PagesIndexPlugin` 写入的 `pages_by_id`（每个已路由页面一条，带自定义字段），所以 `site` 的转换同样随页数增长，是与 `pages` 并列的主要成本。列表页（`ListPageModel`）同样每页转换一次 `site`，因此列表页也受益。
4. 列表页的 `pages` 与 `items` 指向同一份数据时各转换一遍：生产路径 `SpecialListRenderer.CreateListPageModel` 设 `Pages = pageInfos, Items = pageInfos`（同一非空实例），`Items` 为空时映射也回退到 `Pages`。

因此一次构建的对象分配约为「页数 × 索引条数 × 每条子对象数」，随页数平方增长。silkroutecc 的文章模板及其局部模板不使用 `pages`/`pages_by_id`（V6），这部分工作完全浪费。

增量构建不能替代这项修复：`--no-clean` 只跳过未变页面，每个需要重渲染的页面仍要付出与全站规模成正比的转换成本；两轮 `--no-clean` 实测也未跳过任何页面（原因另行排查）。

## What Changes

- **共享模型只转换一次**：每个渲染器实例内，按引用相等（`ReferenceEquals`，不用 record 的值相等）缓存最近一次的 `pages` 来源与 `SiteModel` 及其转换结果。键与值作为一个整体原子发布（不会出现 A 实例配 B 的转换结果）；并发下用加锁的单次初始化保证每个来源最多转换一次（`Lazy<T>` 在 AOT 裁剪分析下需要额外注解，故不用）。来源变化时整体替换，不累积旧批次。根对象（`page`、`seo`、`content` 等每页不同的值）仍每页新建。
- **冻结共享值**：用递归遍历器（带已访问集合，`content_model` 与 `content_record` 是同一实例）把共享的 `ScriptObject`、`ScriptArray` 及全部嵌套值设为 `IsReadOnly`。Scriban 7.2.5 的 `IsReadOnly` 不会自动向下传播。现状下所有成员已用 `readOnly: true` 设置，给已有成员赋值本来就报错；冻结后**新增**拦截两类写入：给任何共享对象添加新键（Scriban 抛 `InvalidOperationException("The object is readonly")`），以及给共享数组按下标赋值。实施时发现 Scriban 7.2.5 的模板下标赋值走非泛型 `IList` 索引器，会绕过 `ScriptArray.IsReadOnly`；因此新增内部 `BukitTemplateContext`（重写 `GetListAccessorImpl`），在页面、组件、区块三处模板上下文中对只读数组的下标赋值抛出带模板位置的 `The array is readonly`。这样共享值不会在页面之间泄漏，也不会出现并发写入。
- **保持现有语义**：
  - 模板看到的 `pages`、`site` 键、值、顺序与现在完全相同；不新增键、不按下标改数组的模板，渲染输出逐字节不变。
  - 派生列表页（`ScribanDerivedListAliasProjector`）只改每页根对象上的别名，行为不变。
  - 列表模型 `ListPageModel` 只共享 `site`；`pages`/`items` 是每个列表各自的数据，仍按列表转换，但 `ReferenceEquals(model.Items ?? model.Pages, model.Pages)` 时只转换一次，两个名字共用同一个只读数组。
  - 公共接口 `ITemplateRenderer` 签名不变。
- **补齐渲染计时**（全部由 `PageRenderDispatcher` 记录）：
  - `pageRender`、`listBuild`、`staticRender`：每页从读完正文之后（不与 `bodyLoad`、`metadataHash` 重叠）到写出文件的实际耗时。
  - 细分：`renderCall`（`RenderPage`/`RenderList` 调用，含模型转换与模板执行）、`htmlTransform`（HTML 变换）、`renderWrite`（写文件）。内容页、列表页、静态页都计入。
  - `renderModelBind`：模型转换耗时，通过内部 Scriban 适配器（`ScribanTemplateRendererAdapter`）实现的内部接口取得，是 `renderCall` 的子集；外部 `ITemplateRenderer` 实现不提供时不输出该项。
  - `renderDispatch`：整个渲染调度的墙钟时间（在 `DispatchAsync` 内从开始到全部页面写完，含页面索引构建），用于与 `variantTotal` 对照；上面各项是各工作线程的累计时间，可能大于墙钟时间。
  - 精度：各项按 `Stopwatch` 刻度累计，在快照时一次换算为毫秒，避免逐页截断成 0；`metrics.json` 仍以毫秒输出，结构不变，只新增指标名。
- 开发文档与中英马 CHANGELOG 同步。

## Impact

- 受影响代码：`Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs`（批次内缓存、转换计时、结束渲染批次；在同步绑定调用期间以线程内作用域激活缓存，受 G-04D6B 治理的 `ScribanModelBinder` 两个单参数重载与渲染器中两处 `ScribanModelBinder.ToScriptObject(model)` 保持不变）、`ScribanRootModelMapper.cs`，新增内部 `SharedModelCache.cs`、`ScriptValueFreezer.cs`（递归冻结，覆盖 `ScribanDynamicValueMapper` 生成的嵌套对象/数组，映射器本身不改）、`BukitTemplateContext.cs`（数组下标只读），以及 `TemplateContextBuilder.cs`、`ComponentFunctions.cs`、`SectionRenderHelper.cs` 改用该上下文；`Bukit.Engine/ScribanTemplateRendererAdapter.cs`（内部 `IRenderPassCompletion`）、`PageRenderDispatcher.cs`（全部渲染计时与 `renderDispatch`）、`BuildStageMetrics.cs`（刻度累计）、`VariantBuildPipeline.cs`（渲染前各步骤墙钟计时）。
- 受影响测试：`ScribanModelBinderTests`（契约不变，新增共享与只读断言）、渲染输出相关测试、指标相关测试。
- 规格：`static-rendering` 新增三项要求（共享模型转换、列表来源去重、渲染阶段计时）。
- 公共契约：无公共 API 变化（基线 `docs/governance/bukit-core-public-api-baseline.v1.json` 不变）；`metrics.json` 结构不变，`pageRender`、`listBuild`、`staticRender` 从 0 变为实际值，并多出 `renderCall`、`htmlTransform`、`renderWrite`、`renderModelBind`、`renderDispatch`，以及变体渲染前的 `variantSetup`、`mediaResolve`、`renderPlan`、`assetPrepare`、`seoPrepare`。
- 兼容性：模板若给 `site`、`pages[i]` 或其嵌套对象**添加新键**，或给其中的数组**按下标赋值**，升级后会报 `The object is readonly`（错误信息不带模板行号）。仓库内主题与夹具、silkroutecc 主题均需搜索确认没有此类写法；CHANGELOG 写明。`ScribanTemplateRenderer` 是公共类型：调用方若复用同一个 `SiteModel` 实例却修改其背后的字典，现在会得到缓存的旧值，文档写明。
- 内存：渲染调度结束时（含异常退出）清空缓存，不让 `pages`、`site`（含 `pages_by_id`）保留到报告阶段。
- 预期收益：silkroutecc 渲染阶段应从约 15 分钟大幅下降；具体数值以实施后的同条件 `--metrics` 对比为准，不预先承诺。
- 与其他变更的关系：与 `fix-media-signed-url-identity`、`fix-notion-body-cache-contract`、`harden-incremental-invalidation` 相互独立；按 `docs/plans/2026-11-10-build-time-incremental-remediation-plan.zh-CN.md` 最先实施，并与 `observe-report-stage-timing` 同一发布。
- 用户批准范围：按计划实施；提交与发布另行确认。

## Non-Goals

- **按需（惰性）转换与读取记录**：在模板首次访问 `pages` 时才转换并记录「本页依赖 `pages`」，可进一步让增量只重渲染真正依赖页面索引的页面（Gatsby 式读取跟踪）。这涉及 Scriban 自定义对象与清单字段，另立变更。
- 不改变 `pageIndex` 的内容（例如去掉不常用字段），不改变 `ListPageModel` 的转换方式。
- 不排查 `--no-clean` 未跳过页面的原因（`render_dependency_changed`、`content_changed`），另行处理。
- 不调整渲染并发度与 GC 配置。
- 不修复 `ScribanTemplateRendererAdapter` 第二个构造函数丢弃 `contributors` 的既有问题（另行报告）。
