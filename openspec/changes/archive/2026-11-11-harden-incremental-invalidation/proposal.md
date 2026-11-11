# Harden incremental invalidation for engine and plugin changes

## Why

目前站点构建都用 `--clean`，渲染增量从不命中，所以两处失效漏洞没有暴露。下一步计划在 CI 缓存 `dist/` 并改用 `--no-clean`，届时它们会直接产出过期页面：

1. **引擎版本不参与失效**：模板哈希只有常量 `scriban-renderer-v1`（`VariantManifestPlanner.ComputeCompositeTemplateHash`），清单不记录 Bukit 版本。升级 Bukit（渲染、SEO、HTML 变换的修复）后，内容未变的页面仍被判为 `unchanged`。
2. **内置插件只记名字和开关**：`NonAnalyticsPluginEnablementContributor` 只写 `plugin.name`、`plugin.enabled`，不含 `Options`。改了会影响 HTML 的插件选项后，未变内容的页面被跳过。

## What Changes

- **引擎身份并入模板哈希**：在 `ComputeCompositeTemplateHash` 的组成部分中加入 `Bukit.Engine` 程序集的 InformationalVersion（形如 `2.0.12+<sha>`，AOT 下可读取，`ImageOptimizer` 已有先例），与 `scriban-renderer-v1` 并列。
  - 每个页面、列表和特殊列表条目本来就逐条比较 `TemplateHash`，列表内容哈希也包含它，所以引擎变化后全部重新渲染，不需要新增清单字段，也不需要在渲染流程里传递新标志。
  - `TemplateHash` 已经随 i18n worktree 清单快照往返，i18n 构建无需改动。
  - 清单格式与公共 API 不变。
  - 代价：报告里页面的原因显示为 `template_changed`、列表为 `list_render`，而不是专门的「引擎已变」。用户指南写明「升级 Bukit 后出现 `template_changed` 属正常」。
- **插件选项**：每个启用的非 analytics 内置插件，其 `Options` 用现有 `RenderDependencyHashWriter.AppendLabeledCanonicalValue` 写入渲染依赖哈希。YAML 读出的选项值只有字符串、布尔、列表和不区分大小写的字典（数字保持为字符串），该方法均已支持，字典按序数排序，`theme.params` 即用此法。
- **元数据变化单列原因**（计划问题 6，用户 2026-11-10 确认拆分）：`PageRenderDispatcher` 中因元数据哈希（页面记录与自定义字段）变化而重渲染的页面，原因从 `content_changed` 改为 `metadata_changed`；`content_changed` 只表示正文变化。判定顺序不变。`bodyFingerprint` 字段是正文身份，不再计入元数据哈希（仍计入内容哈希；不含正文的列表哈希仍随它变化），否则 Markdown 改正文会被误报为 `metadata_changed`。Notion 的 `last_edited_time` 是页面字段，任何编辑都会改变它，因此 Notion 页面的编辑仍显示为 `metadata_changed`。报告中的原因取值因此变化，读取 `content_changed` 的脚本需同时读取 `metadata_changed`。
- 用户与开发文档、中英马 CHANGELOG 同步。

## Impact

- 受影响代码：`Bukit.Engine/VariantManifestPlanner.cs`（模板哈希组成）、`Bukit.Engine/Incremental/NonAnalyticsPluginEnablementContributor.cs`、`Bukit.Engine/PageRenderDispatcher.cs`（原因名）、`Bukit.Engine/Incremental/IncrementalBuildEngine.cs`（元数据哈希排除 `bodyFingerprint`），以及一个读取引擎 InformationalVersion 的小辅助方法。
- 受影响测试：模板哈希、渲染依赖哈希、增量引擎相关测试。
- 公共契约：无公共 API 变化（基线 `docs/governance/bukit-core-public-api-baseline.v1.json` 不变）；`build-manifest.json` 格式不变；`incremental-manifest.v1.schema.json` 的 `renderReasons` 不限定原因名称，新增 `metadata_changed` 也无需改契约。
- 兼容性：
  - 升级后第一次 `--no-clean` 构建全量渲染。
  - 开发构建每个提交都会全量渲染一次。
  - 没有 git 信息的构建（如源码包）只有版本号；未提交的本地修改也不改变 SHA。这两种情况需要 `--clean`，文档写明。
  - 修改任何内置插件选项都会让全站重新渲染，包括不影响 HTML 的选项（如 indexnow、wechat 的密钥轮换）。
- 与其他变更的关系：与 `fix-media-signed-url-identity`、`fix-notion-body-cache-contract` 相互独立，实施顺序不限。
- 跨仓影响：SRBiz-bukit 要真正跳过未变页面，还需在站点仓库缓存 `dist/` 并改用 `--no-clean`，那属于站点任务，不在本变更内。
- 用户批准范围：本提案只批准起草；实施、提交、发布需单独确认。

## Non-Goals

- **专门的 `engine_changed` 渲染原因**：需要新增清单字段、公共 API 变更、渲染流程传参和 i18n 快照改动，收益只是报告措辞，不纳入。
- **外部插件锁定身份**：外部插件由 `Bukit.Cli` 通过 `Bukit.PluginHost` 加载，只注册 CLI 命令；`Bukit.Engine` 不引用 PluginHost，构建与渲染路径也不调用外部插件。目前没有外部插件影响渲染输出，不纳入。
- **按页面的图片依赖**：现在开启 `theme.images` 时，全站图片指纹会让任意图片变化触发全站重渲染。但只看页面自己的正文和字段不够：模板会输出其他文档的封面（如 SRBiz 的 `article-card.html` 用于首页、列表和标签页）。正确做法是在渲染时记录每个路由实际输出的本地图片及宽度，存进清单条目，下次只重读这些文件的宽度。这需要单独的设计和变更，在此之前保留全局指纹（结果偏保守，但正确）。
- **`pages` 页面索引依赖**：SRBiz 的 `related-entity.html`（被文章页和公司页引用）和 `index.html` 都遍历 `pages`；而且 `pages` 在列表模型里还表示列表项，简单扫描几乎总会判定「有引用」。按模板引用来决定、整体哈希页面索引的方案，对 SRBiz 等于每次任意文章变化都全站重渲染，没有收益。可行方向是复用 `ScribanSymbolAnalyzer`，只对模板实际访问的成员路径（如 `url`、`title`、`fields.collection`、`content_model.slug`）求哈希。另立变更。在此之前，引用 `pages` 的主题在 `--no-clean` 下可能显示其他文章的过期标题或链接，文档写明需要定期 `--clean`。
- 不引入 SQLite、LiteDB 或其他嵌入式数据库；缓存继续使用 JSON 文件。
- 不回收孤儿缓存文件；不改变清单与输出目录绝对路径的绑定方式。
- 不修改站点仓库的 CI 工作流。
