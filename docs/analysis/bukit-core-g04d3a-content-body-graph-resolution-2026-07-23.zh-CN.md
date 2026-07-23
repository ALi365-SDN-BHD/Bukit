# Bukit Core G-04D3A Content Body/Markdown 图收窄决议

日期：2026-07-23

状态：implementation complete；`group-verification-pending`

分支：`codex/g04-group1-pluginhost-content-a`

G1 `GROUP_BASE`：`10bfead3f28b8a9f82a9b5fc008a16d49e290cae`

目标版本线：`2.0`

## 1. 决议

G-04D3A 只将以下四个具体实现/helper CLR identity 从 `public` 收窄为
`internal`：

- `Bukit.Content.CompositeContentBodyStore`
- `Bukit.Content.DictionaryContentBodyStore`
- `Bukit.Content.Markdown.BasicMarkdownToHtml`
- `Bukit.Content.Markdown.MarkdownBodyStore`

四个类型、构造器、成员、namespace、assembly 和方法体全部保留。对外行为边界仍是
`IContentBodyStore`、Content provider、Markdown provider 以及 CLI 构建结果。本变更
没有引入替代 facade，也没有修改 Content schema、配置、Notion transport、媒体、SEO、
插件协议或持久化格式。

current baseline 从 14 assemblies / 501 public types / 89 candidates 变为
14 / 497 / 85。关闭的 136 项历史 candidate manifest 不修改，Git blob 必须继续为
`7b07d6890562387010b52301e9f8716e9bf10ed1`。

## 2. 兼容性与运行时漂移

这是明确的 2.0-only source/binary breaking change。未声明而直接构造四个 CLR 类型、
反射其 public metadata，或编译时引用 `BasicMarkdownToHtml` 的外部消费者需要迁移到
受支持的 provider/build 行为边界。公开证据没有确认 direct CLR consumer；private、
unindexed 或 undisclosed consumers 仍为 unknown。

运行时预期漂移为 **0**：四个 production diff 都只有 type accessibility token 变化，
没有成员或方法体变化。

## 3. 行为证据

新增 Composite 联合 characterization，固定同一次 dispatch 将
`markdown:projected-page-1` 与 custom `sourceId=source-page-1` 还原为 inner
`Id=source-page-1`，并按 `StringComparison.Ordinal` 将
`BodyKey=markdown:source-body-1` 剥为 `source-body-1`；inner 返回的
`ContentBody` 对象 identity 保持不变。

新增 Markdown characterization 固定当前 Markdig pipeline 对 `javascript:`、
`data:` 和 `vbscript:` link destination 的 passthrough 输出。该测试是**既有风险证据，
不是安全批准**。本任务不修改 Markdown pipeline 或 URL policy；若要禁止危险 scheme，
必须建立独立安全任务。`.DisableHtml()` 的 raw HTML 防线不等于 URL scheme sanitizer。

本任务不修改 async disposal。G1 组级验证必须运行既有
`Bukit.Engine.Tests.SiteEngineBodyStoreLifetimeTests` 的成功、异常、取消三个
exactly-once disposal 场景；完成前保持 `group-verification-pending`。

## 4. Friendship 源码事实纠正

Task 9 并行调查曾把 Content friendship 误写成项目文件已有配置。实际
`Bukit.Content.csproj` 没有 `InternalsVisibleTo` item；既有声明位于
`src/Bukit-Core/Bukit.Content/InternalsVisibleTo.cs`，精确为：

- `Bukit.Content.Tests`
- `Bukit.Engine`
- `Bukit.Engine.Tests`

本任务不新增、不删除、不复制这些声明。`Bukit.Engine` production friend 是既有
Notion compatibility 依赖：Engine 调用 internal `NotionCompatibilityQueries`。它不是
D3A 四个候选所需，也不得在本任务迁移或移除。

## 5. Task 11 边界

`Bukit.Content.Notion.NotionClientStats` 仍 public/exported，并保留在 current baseline。
legacy stats 与 canonical `Bukit.Notion.Transport.NotionClientStats` 的迁移涉及 transport
facade、统计语义和 lifetime，只能由 G2 Task 11 / G-04D3B 处理。

## 6. 组级待验证集合

当前只完成源码、测试与治理证据编辑，尚未运行任何测试、build、gate 或 Native AOT。
父级 Task 10 必须统一执行并记录：

- `Bukit.PluginHost.Tests`
- `Bukit.Content.Tests`
- `Bukit.Cli.Tests`
- `Bukit.Architecture.Tests`
- `Bukit.Engine.Tests.SiteEngineBodyStoreLifetimeTests` 三场景
- public API drift
- 从 G1 `GROUP_BASE` 起的一次 aggregate targeted gate
- published Native AOT Markdown smoke
- `git diff --check`
- 一次独立轻量只读复审

验证和复审全部通过前，四项只能标记
`implemented / group-verification-pending`，不得申请关闭。

## 7. 防漂移审计

允许的 production diff 仅为四个 `public` → `internal`。确认没有修改方法体、fallback、
exception、cancellation、comparer、Markdown pipeline、HTML、TOC、URL policy、file
I/O、front matter、disposal ownership、Notion stats/API、schema、配置、插件协议或
friendship。current baseline 只删除四个 D3A candidate；historical candidate manifest
不变。
