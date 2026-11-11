# Track render dependencies per route

## Why

增量构建开启时，`RenderDependencyHasher.Compute` 为整个变体算出**一个**渲染依赖哈希，所有页面、列表和特殊列表共用。`SiteModelDataContributor` 把 `site.modules`、`site.data`（含 `PagesIndexPlugin` 写入的 `pages_by_id`、`TaxonomyPlugin` 写入的分类数据）、`site.data_index`、`site.related_pages`、`site.data_files` 逐节点写入这个哈希。这些数据随每篇文章变化，所以修改任意一篇文章都会让全站重渲染（计划 `docs/plans/2026-11-10-build-time-incremental-remediation-plan.zh-CN.md` 问题 5；`docs/plans/2026-11-08-incremental-per-page-render-dependencies.md`）。

另一方面，根变量 `pages`（全部页面列表）和区块 `source` 读取的页面集合目前根本不进入任何哈希：引用它们的页面在其他文章变化时会被错误地跳过（用户指南已列为已知局限）。

## What Changes

- **拆分全站哈希**：渲染依赖分为「基础部分」（除共享数据集合以外的全部贡献者：站点身份与模式、分析、SEO、主题与模板模型、图片处理、集合与字段范围、分类配置、路由元数据配置、插件启用与选项、SEO 备用链接）与若干「数据集合」：
  - `site.data.<键>`（公开键，排除路由元数据来源）
  - `site.modules.<键>`、`site.data_index.<键>`
  - `site.related_pages`、`site.data_files`
  - `pages`：页面模型的全部页面列表，以及区块 `source` 解析时读取的页面集合
  每个集合的指纹在一次变体渲染中最多计算一次。
- **记录每个路由读取的集合**：渲染一个路由时，记录本次渲染实际使用的全部模板文件（主模板、布局链、`include`、组件、区块模板）。对每个模板做保守的静态分析（基于现有 `ScribanSymbolAnalyzer`）：
  - 引用 `site.data.X`、`site.modules.X`、`site.data_index.X` → 对应集合；
  - 引用 `site.related_pages`、`site.data_files`、根 `pages` → 对应集合；
  - 整体使用 `site`、`site.data`、`site.modules`、`site.data_index`（作为值传递、遍历、下标为表达式等无法确定键的用法）→ 该范围内的全部集合；`site` 整体 → 全部集合；
  - 解析失败、无法识别的全局名（`with`、`import` 引入的名字也算）、调用会在 C# 中读取页面或站点数据的函数（带 `source` 的区块 → `pages`；区块插件 → 全部集合）→ 按上述规则保守扩大。
  - 模板在渲染加载时记录，动态模板名按实际使用的文件计入。
  路由的读取集合 = 这些模板引用集合的并集。读取集合由本次渲染的输入决定，输入不变时控制流不变，读取集合也不变；所以用上次记录的集合判断本次能否跳过是可靠的。
- **按路由计算依赖哈希**：路由依赖哈希 = H(基础哈希, 按键排序的「集合键 + 集合指纹」，仅含该路由上次读取的集合)，再叠加现有的路由元数据与面包屑折叠。渲染后用本次记录的读取集合重新计算并写入清单。
- **清单**：`BuildManifestEntry` 新增 internal 成员 `RenderReads`（读取集合键列表，`*` 表示全部）与 `RenderBaseHash`，序列化为可选字段 `renderReads`、`renderBaseHash`。旧清单没有这两个字段时按「读取全部集合」处理（fail closed）。公共 API 不变。
- **渲染原因细分**（问题 6 中确认的拆分）：基础哈希变化 → `render_dependency_changed`（全站输入变化）；基础哈希不变、读取的某个集合变化 → 新原因 `data_dependency_changed`。判定顺序：`render_dependency_changed` 在前。
- **保守回退**：读取记录缺失、模板分析失败、渲染器不是 Scriban（自定义 `ITemplateRenderer`）时，按读取全部集合处理。
- 用户与开发文档、中英马 CHANGELOG 同步；用户指南删去「page lists 不触发重渲染」这条已知局限，改为说明新的失效规则。

## Impact

- 受影响代码：`Bukit.Engine/Incremental/RenderDependencyHasher.cs`、`SiteModelDataContributor.cs`、`BuildManifest.cs`、`Bukit.Engine/VariantRenderStage.cs`、`PageRenderDispatcher.cs`、`SpecialListRenderer.cs`、`RenderPipeline.cs`；`Bukit.Rendering/Scriban` 中记录所用模板与静态分析的部分（`ScribanTemplateRenderer`、`TemplateContextBuilder`、组件与区块函数、`ScribanSymbolAnalyzer` 的调用方）。
- 受影响测试：增量与渲染依赖相关测试；新增读取记录、静态分析与端到端测试。
- 公共契约：无公共 API 变化；`build-manifest.json` 增加两个可选字段（内部缓存文件，旧文件可读）；`incremental-manifest.json` 的 `renderReasons` 增加取值 `data_dependency_changed`（模式不限定原因名）。
- 兼容性：升级后第一次 `--no-clean` 构建全量渲染（引擎身份已变，且旧清单没有读取记录）。引用 `pages` 或带 `source` 区块的页面今后会在任意页面变化时重渲染，这是修正，不是回归。
- 预期效果：修改一篇文章时，只重渲染该文章、读取了相关集合的页面（通常是首页、列表、分类、标签与引用 `pages_by_id` 的页面）；文章模板不读取共享集合的站点，其余文章页跳过。
- 与其他变更的关系：建立在 `harden-incremental-invalidation`（引擎身份、`metadata_changed`）之上。
- 用户批准范围：用户 2026-11-10 选择在本会话实施阶段 6；提交与发布需单独确认。

## Non-Goals

- 不细到集合内的条目（例如只依赖 `pages_by_id` 中的某几页）。
- 不追踪模板对页面自身字段的读取（已由内容、元数据哈希覆盖）。
- 不改变基础部分各贡献者的内容。
