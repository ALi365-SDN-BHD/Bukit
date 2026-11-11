# Design: Harden incremental invalidation for engine and plugin changes

## Context

增量判断有三类输入：每页的 metadata/content/route 哈希、模板目录哈希，以及全局或按路由的渲染依赖哈希（`RenderDependencyContributorPlan`）。本变更只补两个缺失的输入：引擎身份和内置插件选项。按页面图片依赖、`pages` 依赖和 Notion 正文缓存，分别见 proposal 的 Non-Goals 与 `fix-notion-body-cache-contract`。

## Decisions

### D1. 引擎身份并入模板哈希

- 从 `Bukit.Engine` 程序集读取 `AssemblyInformationalVersionAttribute`（`2.0.12+<sha>`），作为 `ComputeCompositeTemplateHash` 的一个组成部分。AOT 下可用，`ImageOptimizer` 已用同样方式读取。
- 为什么放在模板哈希：页面分支（`PageRenderDispatcher` 比较 `existing.TemplateHash`）、列表分支（比较 `le.TemplateHash`，且列表内容哈希包含模板哈希）和特殊列表（`SpecialListRenderer`）都已经逐条比较它；`TemplateHash` 也已经随 `I18nBuildManifestSnapshot` 往返。一处改动即可覆盖全部渲染路径，生产路径（`RenderPipeline` → `DispatchAsync`）和测试路径都生效。
- 备选 A：清单新增 `engineIdentity` 字段，加「引擎已变」标志传入渲染流程，原因记为 `engine_changed`。被否决：需要公共 API 变更（`BuildManifest` 是公共类型）、i18n 快照改动、多处传参，而且与 `in-process-build-transactions`「manifest 结构保持现状」的表述冲突，收益只是报告措辞。
- 备选 B：只用 `2.0.12` 版本号。被否决，因为同版本号下的代码修改无法识别。
- 已知局限：没有 git 信息的构建只有版本号；未提交的本地修改不改变 SHA。两种情况都需要 `--clean`。
- 模板目录哈希超出限制时增量本来就会关闭（`DirectoryHashLimitExceededException`），不受影响。

### D2. 插件选项

- 复用 `RenderDependencyHashWriter.AppendLabeledCanonicalValue` 写入 `plugin.options`，与 `theme.params` 一致，不另写 JSON 规范化。`ConfigYamlHelpers.ToObject` 产生的值类型（字符串、布尔、`List<object>`、不区分大小写的 `Dictionary<string, object>`）均已支持。
- `analytics` 插件仍由 `AnalyticsContributor` 负责，不重复写入。
- 代价：任何选项变化（包括不影响 HTML 的密钥轮换）都会全站重渲染。按插件声明「哪些选项影响 HTML」可以减少重渲染，但需要插件元数据，不在本变更内。

## Risks

- 报告中引擎升级显示为 `template_changed`，可能让人误以为主题被改过；用户指南说明。
- 开发时频繁提交会导致每次全量渲染；开发时本来多用 `--clean`，影响有限。

## Verification

- 单元测试：不同 InformationalVersion 产生不同模板哈希；插件选项变化改变渲染依赖哈希、键顺序不影响哈希。
- 端到端：同一 fixture 连续两次 `--no-clean` 构建，第二次全部命中；模拟不同引擎身份后页面、列表、特殊列表全部重渲染；修改一个插件选项后全部以 `render_dependency_changed` 重渲染。
