# 后期优化：增量构建按页面追踪渲染依赖

- 状态：**待排期**（后期优化项，当前不实施）
- 记录日期：2026-11-08
- 来源：silkroute.cc 迁移全量构建（3536 篇 Notion 文章）
- 类别：Core 设计层面变更（`Bukit.Engine/Incremental`）

## 1. 当前决定

保持方案 A：`RenderDependencyHashWriter` 的节点上限由 100,000 提高到固定的 10,000,000（`DefaultMaxNodes`），循环检测与深度上限不变。已在分支 `feat/media-localization-controls` 实现并有回归测试（5000 页数据在旧上限下失败、新上限下通过）。

该修改只解决大站构建失败，不改变增量构建的失效粒度。

## 2. 问题

增量构建开启时，`RenderDependencyHasher.Compute` 为整个站点计算**一个**依赖哈希，所有页面共用它判断能否复用上次输出。该哈希逐节点编码整个 `site.data`，其中包含由内容派生、随文章数线性增长的数据：

- `site.data.pages_by_id`（`PagesIndexPlugin`）：每页标题、URL、摘要、字段等；
- `site.data.taxonomy`（`TaxonomyPlugin`）：每个分类、标签下的文章列表。

后果：

1. 任意一篇文章的标题、摘要、分类、标签变化，都会改变全站哈希，**所有页面重新渲染**；对内容型大站，增量构建在内容有改动时基本失效。
2. 每次构建都要完整遍历上述派生数据，哈希开销随内容总量增长。
3. 节点数随站点规模增长，曾导致 `Render dependency value graph exceeds the 100000 node budget.`（方案 A 已缓解）。

## 3. 目标方案：按页面追踪依赖

只有确实读取 `site.data.pages_by_id` 或 taxonomy 的页面（首页、列表页、分类页、标签页等）才依赖文章指纹；文章页只依赖它自身的内容与共享的配置、主题。

预期效果：修改一篇文章，只重渲染这篇文章及引用它的列表页、分类页、标签页，而不是全站。

## 4. 需要解决的问题

1. **模板读取了哪些数据**：需要知道每个模板（含 layout、include、partial、shortcode、component）访问了 `site.*` 的哪些成员。可选做法：
   - 渲染时记录访问（在 Scriban 上下文对 `site.data` 等对象做访问追踪），首次构建生成依赖清单，写入 build manifest；
   - 或静态分析模板，保守地标记引用。
   运行时追踪更准确，但要处理条件分支：某次渲染没走到的分支，下次可能走到。
2. **依赖的粒度**：页面依赖"整个 pages_by_id"还是"其中某些条目"。第一步可按数据集合粒度（依赖 / 不依赖），再考虑细到条目。
3. **派生数据指纹**：对 `pages_by_id`、taxonomy 用紧凑指纹（每页 id、url、最后编辑时间或正文指纹）代替逐字段编码，并保证指纹覆盖模板可见的所有字段，避免漏报导致旧内容残留。
4. **保守回退**：依赖信息缺失、模板或插件版本变化、追踪失败时，回退为当前的全站哈希行为（fail closed）。
5. **与 route metadata 的组合**：`RenderDependencyHasher.ComputeForRoute` 已有按路由附加元数据的机制，新方案应在其基础上扩展，而不是另起一套。

## 5. 开始前的度量

先用现有的 `renderDependencyHash` 计时指标，以及"一次内容修改后重渲染的页面数"，在 silkroute.cc 规模（约 3500 篇）上建立基线，用来评估收益。

## 6. 实施方式

按 AGENTS.md 流程，先新建 openspec 变更（建议名称 `add-per-page-render-dependencies`），写明 proposal、spec 与 tasks，再实施。测试至少覆盖：

- 修改一篇文章后，只有该文章页及依赖它的列表、分类、标签页被重渲染；
- 模板条件分支切换后，依赖能被正确识别；
- 依赖信息缺失或版本变化时回退为全站重渲染；
- 与现有增量、i18n 多语言构建、route metadata 行为兼容。
