# Bukit Core G-04D1B Block Renderer Facade 原子移除设计

日期：2026-07-23

状态：`design-approved / written-spec-awaiting-review / implementation-not-started`

基线：`2.0@136b6ba127ee7edb6a136cf3a70449110ff47d87`

独立任务分支：`codex/g04d1b-block-renderer-facade-removal`

## 1. 目标与决策

G-04D1B 只处理 `Bukit.Content.Notion.BlockRenderers` 中 23 个无独立业务职责的
public facade。它们逐一将调用转发到 `Bukit.Notion.Rendering.BlockRenderers` 的同名
canonical renderer，形成重复公共面、重复测试归属和错误的程序集所有权暗示。

本任务采用已经批准的方案 A：**在一个独立任务中原子删除全部 23 个 facade，并把
其直接行为测试迁移到 canonical 所有者 `Bukit.Notion.Tests`；对混合测试文件按方法
拆分，完整保留 G-04D1C 的 legacy extension-graph 覆盖。**

成功标准不是文件行数或 public 类型数量本身，而是同时满足：

- 编译后的 `Bukit.Content` 不再导出这 23 个完整类型名；
- 对应 canonical renderer 继续由 `Bukit.Notion` 导出，渲染语义和安全行为不变；
- 现有测试覆盖不因迁移而删除、弱化或改写成只验证“能够编译”；
- 五个 G-04D1C 类型及其 legacy 行为测试保持原状；
- public API drift 在 baseline 更新前只报告这 23 项 deliberate breaking removal；
- 当前 baseline 从 537 types / 133 candidates 精确收敛为
  514 types / 110 candidates；
- 已关闭的 136 项候选 manifest 保持字节不变。

任何额外生产行为、公共契约或候选类型变化均为阻断性 scope drift。

## 2. 精确移除集合

唯一生产源码删除目标是
`src/Bukit-Core/Bukit.Content/Notion/BlockRenderers/BlockRendererFacades.cs` 中的内部
转发 helper 和以下 23 个 public sealed 类型：

1. `AudioBlockRenderer`
2. `BookmarkBlockRenderer`
3. `CalloutBlockRenderer`
4. `ChildEntityBlockRenderer`
5. `CodeBlockRenderer`
6. `ColumnBlockRenderer`
7. `ColumnListBlockRenderer`
8. `DividerBlockRenderer`
9. `EmbedBlockRenderer`
10. `EquationBlockRenderer`
11. `FileBlockRenderer`
12. `ImageBlockRenderer`
13. `LinkPreviewBlockRenderer`
14. `LinkToPageBlockRenderer`
15. `NoOpBlockRenderer`
16. `PdfBlockRenderer`
17. `RichTextContainerRenderer`
18. `SyncedBlockRenderer`
19. `TableBlockRenderer`
20. `TableOfContentsBlockRenderer`
21. `ToDoBlockRenderer`
22. `ToggleBlockRenderer`
23. `VideoBlockRenderer`

以上 legacy 类型的命名空间均为
`Bukit.Content.Notion.BlockRenderers`；替代类型均位于
`Bukit.Notion.Rendering.BlockRenderers`，名称、构造参数和渲染成员已在 G-04D1
资格审计中逐项验证。

同文件末尾的 internal `NotionBlockHelpers` 不属于 public removal 集合。实施时不得因
删除整文件而误删它；若 facade 与 helper 仍共处一文件，应把 helper 以纯机械方式
保留到同命名空间下的独立源码文件，不改变成员、可见性或逻辑。

## 3. 方案比较

### 3.1 方案 A：原子删除并精确拆分测试（采用）

一次删除全部 23 个同构 facade，四个纯 renderer 测试文件整体迁移，两个混合文件
按测试职责拆分。优点是 public ownership 一次收敛、不会留下任意半迁移组合，并能
保住 D1C 的 legacy extension-graph 契约；代价是测试迁移需要逐方法核对。

### 3.2 方案 B：六个测试文件全部迁移（拒绝）

机械操作更简单，但会把 `NotionBlocksRenderer`、legacy registry/context/client 和
`NotionBlockHelpers` 的测试一起移出 `Bukit.Content.Tests`。这会提前改变 G-04D1C
测试归属，掩盖 legacy extension graph 是否仍受保护，属于超限修复。

### 3.3 方案 C：保留 facade 并增加 `Obsolete`（拒绝）

该方案可提供额外迁移期，但会继续维持重复公共面和错误 ownership。G-04B3 声明窗口
及 G-04D1 资格审计没有发现需要该兼容层的直接 CLR 消费证据，而当前任务位于明确的
2.0 breaking-change 开发线，因此不新增 staged shim。

若实施期间出现真实 CLR 消费者证据，必须停止删除并另立 deprecation 设计，不能在
本任务中临时混入 `Obsolete`、type-forwarding 或新 facade。

## 4. 架构与所有权边界

删除后，block renderer 的唯一生产实现和公共入口归属 `Bukit.Notion`：

```text
Bukit.Notion.Rendering.BlockRenderers.*
        ↑ canonical production ownership
Bukit.Notion.Tests
        ↑ direct renderer behavior and safety coverage

Bukit.Content.Notion extension graph
        ↑ legacy compatibility still covered by Bukit.Content.Tests
        ↑ deferred to G-04D1C
```

不得为迁移测试给 `Bukit.Content.Tests` 新增访问 `Bukit.Notion` internals 的权限。
`Bukit.Notion` 已向 `Bukit.Notion.Tests` 提供 `InternalsVisibleTo`，需要 context 的
canonical renderer 测试应在该 owner test assembly 内使用 test-only helper 构造
canonical `NotionClient`、`NotionBlocksRenderer` 和 internal `NotionRenderContext`。
这避免为了测试迁移扩大生产可见性。

以下 G-04D1C 类型不得删除、改为 internal、改签名或迁移治理分类：

- `Bukit.Content.Notion.INotionBlockRenderer`；
- `Bukit.Content.Notion.NotionBlockTransformer`；
- `Bukit.Content.Notion.NotionBlockRendererRegistry`；
- `Bukit.Content.Notion.NotionRenderContext`；
- `Bukit.Content.Notion.NotionBlocksRenderer`。

## 5. 测试迁移设计

### 5.1 整文件迁移

以下四个文件只验证 D1B renderer 或已经 canonical 化的 D1A helper，可整体迁移到
`tests/Bukit.Notion.Tests/` 并改用 canonical 命名空间：

- `BlockRendererExtendedTests.cs`；
- `BlockRendererColorEncodingTests.cs`；
- `BlockRendererUrlSafetyTests.cs`；
- `NotionBlockRenderersTests.cs`。

迁移应保留测试名称、输入、断言和安全边界。允许为避免类型名冲突调整 namespace、
using 或 test-only helper 调用；不得减少 case、合并不同安全断言或降低精确度。

### 5.2 混合文件精确拆分

`BlockRendererMediaAndContainerTests.cs` 中，所有直接实例化这 23 个 renderer 的行为
测试迁移到 `Bukit.Notion.Tests`；`NotionBlockHelpers` 的 legacy internal bridge 覆盖
留在 `Bukit.Content.Tests`。静态 palette/rich-text 覆盖若已指向 canonical owner，可
保留，但不得以移动 D1B 测试为由删除。

`NotionBlockRendererEdgeCasesTests.cs` 中，直接 renderer 行为测试迁移到 canonical
owner；以下 D1C legacy coverage 必须继续留在 `Bukit.Content.Tests`：

- `NotionBlocksRenderer_Registry_ReturnsRegistry`；
- `NotionBlocksRenderer_NullType_BlockSkipped`；
- `NotionBlocksRenderer_HasMoreNoCursor_StopsPagination`。

该文件中的 D1A rich-text 测试可以继续使用 canonical alias 留在 Content 测试项目，
不作为 D1B 迁移的附带清理对象。

### 5.3 消费者 fixture 与导出清单

`LegacyNotionConsumerFixture.cs` 只移除 legacy `ImageBlockRenderer` 和
`TableBlockRenderer` 的 fixture 引用；其余 legacy D1C 消费路径保持不变。

`NotionBoundaryTests.cs` 的 legacy export 集合只移除精确 23 个完整名称，同时继续
断言五个 D1C 类型存在。新建 G-04D1B 架构守卫，直接从编译程序集枚举 23 个完整名称：
删除前必须 RED，删除后全部 GREEN，并确认 23 个 canonical replacement 仍存在。

## 6. TDD 与公共 API 治理顺序

实施必须按以下证据链执行：

1. 新增编译程序集 guard，断言 23 个 legacy 类型均不存在；在未改生产代码时运行并
   取得只因 23 个类型仍存在而失败的 RED。
2. 在不删除测试语义的前提下迁移四个纯文件、拆分两个混合文件，并保留 D1C 覆盖。
3. 保留 internal `NotionBlockHelpers`，删除 facade helper 和全部 23 个 public facade。
4. 运行 Architecture、Content、Notion 三个受影响测试项目并取得 GREEN。
5. 在更新 baseline 前运行真实 public API drift，要求只有 23 个目标类型及其从属
   members 的 breaking removal；出现其他 identity 立即停止。
6. 用现行 snapshot 工具生成临时 baseline，与从当前 baseline 精确删除 23 项所得的
   expected JSON 做规范化语义 diff；只有无其他差异时才替换活动 baseline。
7. 更新后要求活动 baseline 为 14 assemblies / 514 types / 110 candidates，drift
   self-test 与真实 check 均通过。

不得手工重写与目标无关的 baseline 节点，也不得用放宽 validator、排序规则或
allowlist 的方式使 drift 通过。

## 7. 历史记录与兼容性

`docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` 是声明窗口关闭时的
136 项历史 cohort，必须与任务基线字节相同。它继续保留 23 个 facade 的历史 identity
和搜索结果，不代表删除后的当前程序集导出。

活动治理文档和架构测试必须区分三个时间点：

- G-04C 完成后的 539 / 135；
- G-04D1A 完成后的 537 / 133；
- G-04D1B 完成后的当前 514 / 110。

不得把历史陈述改写成“当时就是 514 / 110”，也不得将 G-04D1B 解释为其余 110 项
获得批量删除授权。新增独立 G-04D1B 关闭台账，记录精确类型、canonical replacements、
兼容性影响、测试证据和复审结果。

本任务是明确的 2.0 source/binary breaking change。外部源码若直接引用 legacy facade，
迁移方式是把 namespace 从 `Bukit.Content.Notion.BlockRenderers` 改为
`Bukit.Notion.Rendering.BlockRenderers`；构造参数和 `RenderAsync` 调用保持对应关系。
CLI、配置、主题、进程插件和站点构建消费者不直接引用 CLR facade 时不受影响。

## 8. 验证与复审

开始实施前的有效基线为：Architecture 109/109、Content 670/670、Notion 86/86，均为
Release、零失败。

实施至少验证：

- `Bukit.Architecture.Tests`、`Bukit.Content.Tests`、`Bukit.Notion.Tests`；
- Core、Labs 和官方插件 Release 编译；
- public API drift self-test、baseline 更新前的精确 breaking diagnostic、更新后的真实
  drift check；
- 当前主机 `osx-arm64` Native AOT publish/package smoke；
- 每个实现子任务按变更路径运行 `post-change-focused.sh`；
- 父任务只运行一次 aggregate `post-change-targeted.sh --base
  136b6ba127ee7edb6a136cf3a70449110ff47d87 -- <all changed paths>`；
- 独立只读 task review 和最终 aggregate diff review。

不运行 full、release、`test-all`、`smoke-all` 或 whole-solution tests。环境、权限或
基础设施失败只按实际阻塞记录，不授权修改 NuGet、TLS、CI、发布或无关代码。

## 9. 停止条件与回滚

以下任一条件出现时立即停止：

- 发现新的直接 CLR、反射、继承、签名、序列化或 source-generation 消费证据；
- canonical renderer 的构造或渲染语义不能覆盖 legacy facade；
- 测试迁移需要扩大生产可见性或修改 D1C API；
- public API drift 出现目标集合之外的变化；
- 关闭 manifest 发生任何字节变化；
- Core、Labs、官方插件或 AOT 出现无法归因并受控解决的回归；
- 独立复审存在未关闭的 Critical 或 Important finding。

回滚必须恢复 facade 源码、测试归属、活动 baseline 和 G-04D1B 台账／活动文档变更；
不得回滚 G-04C、G-04D1A 或 2.0 版本线，也不得改写 136 项历史 manifest。

## 10. 明确非目标

- 不处理 G-04D1C 五个 extension-graph 类型；
- 不处理 `NotionClientStats` 或其他 Content/Notion 类型；
- 不修改 schema、插件协议、配置、持久化格式或 build report；
- 不修改 asset URL、输出路径、全局路径工具、HTTP 或 TLS 策略；
- 不改变 block renderer 的 HTML、安全过滤、分页、取消或异常语义；
- 不新增 public facade、type forwarding、`Obsolete` shim 或 compatibility package；
- 不更新或重跑 136 项 GitHub 搜索，除非出现新消费者证据；
- 不修改版本号、push、发布或合并到 1.x `main`；
- 不把 23 项原子删除扩展成其余候选的批量授权。
