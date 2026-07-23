# Bukit Core G-04D9 Engine 公共面总资格审计

> 日期：2026-07-23
>
> 任务：G-04 Group 4 / Task 32
>
> 源码基线：`2.0@6f10269c515f328628955f706075d70cc3a21977`
>
> 状态：eligibility-audit-complete / implementation-paused-on-pre-existing-p1

## 1. 审计范围

本报告只审计 current public API baseline 中 56 个
`Bukit.Engine / Build engine / implementation-public / 2.0-candidate`。
它们严格分配到八个 cluster：

| Cluster | 数量 |
|---|---:|
| D9A build orchestration | 7 |
| D9B content validation/stage contracts | 9 |
| D9C filesystem/output utilities | 9 |
| D9D feed/SEO/sitemap generators | 8 |
| D9E built-in plugins | 13 |
| D9F Notion fetch integration | 2 |
| D9G plugin source/capability | 3 |
| D9H list/template capability helpers | 5 |
| **合计** | **56** |

审计覆盖 declaration、public/protected signature、Core 跨程序集调用、继承与 interface
实现、reflection/serialization/source generation、Native AOT、owner tests、活动文档和
closed consumer declaration。Task 32 只建立资格图，不修改 production、tests、baseline
或历史 manifest。

Labs 和外部插件不属于修复范围。仓内只读搜索未发现它们直接引用这 56 个 CLR identity；
该阴性结果不能排除 private、未索引或未自愿声明的消费者。

## 2. 总结论

| 终态建议 | 数量 | 含义 |
|---|---:|---|
| eligible internalize | 41 | 只在批准 cluster 内原子收窄；保持 runtime behavior 和 stable parent API |
| retained public | 15 | 被既有 `1.x-do-not-narrow` public signature、protected extension、stable nested type 或 public facade 传播固定 |
| **合计** | **56** | 每项唯一归属，无遗漏和重复 |

如果 Task 33～40 完全按本报告实施，条件终值是：

```text
14 assemblies / 443 public types / 0 candidates
```

这是资格审计投影，不是 Task 32 已完成的 baseline 变化。Task 42 必须以编译程序集重新
生成并验证实际结果。历史 136-entry manifest 不随 current type 收窄或 retained
重分类而改写。

## 3. 共享消费者证据

### 3.1 当前仓内传播

- 56 项的 production 调用均位于 `Bukit.Engine`；
- Core 其他 13 个程序集没有发现直接名称引用；
- `Bukit.Engine.Tests` 通过既有 friend access 测试内部编排；
- Core CLI 已是既有 `InternalsVisibleTo("bukit")` 消费者，本任务不新增 friend；
- Architecture tests 可以通过 compiled metadata 的 exact full name 检查 internal
  终态，不需要新增 friend。

“仅 Engine 内部调用”不是单独删除授权。以下 retained 决议来自 public metadata
传播，而不是来自仓内引用数。

### 3.2 Closed declaration window

历史 manifest 中有 57 个 Engine entries：

- 本报告的 current 56 项；
- G-04C 已删除的 `Bukit.Engine.RouteInventoryInspectEntry`。

57 项的历史搜索状态均为：

```text
declarationStatus = consumer-declaration-pending
externalEvidence.searchStatus = no-public-match-found
privateConsumerStatus = unknown-until-voluntary-declaration
proposedAction = review-only
```

manifest 本身保持 `closed / 136 / 136`，Git blob 为
`7b07d6890562387010b52301e9f8716e9bf10ed1`。本报告不把公开搜索阴性结果改写成
“不存在消费者”。

## 4. D9A：build orchestration（7）

| Candidate | 传播事实 | 资格结论 |
|---|---|---|
| `BuildOptions` | public `SiteEngine.BuildAsync(IContentProvider, BuildOptions, ...)` 参数 | retained public |
| `BuildPipeline` | 仅由 `SiteEngine` 创建；与 context 互相组成完整内部图 | eligible internalize with context |
| `BuildPipelineContext` | 仅作为 `BuildPipeline` executor/method 参数 | eligible internalize with pipeline |
| `BuildVariantSummary` | public stable `BuildResult.Variants` 元素、constructor 与 deconstruction 类型 | retained public |
| `ContentPipelineResult` | public stable `ContentPipeline.ExecuteAsync(...)` 返回类型 | retained public |
| `RoutePipeline` | Engine variant orchestration内部创建；只返回同 cluster result | eligible internalize with result |
| `RoutePipelineResult` | 只由 candidate `RoutePipeline` 返回并在 Engine 内消费 | eligible internalize with pipeline |

D9A 结论：4 eligible、3 retained。不得顺带收窄 `SiteEngine`、`BuildResult` 或
`ContentPipeline`；不得把 report/variant shape 换成新 DTO。

## 5. D9B：content validation/stage contracts（9）

| Candidate | 传播事实 | 资格结论 |
|---|---|---|
| `ContentCollectionContractValidator` | static implementation helper；活动开发文档描述运行阶段，不构成 CLR SDK | eligible internalize |
| `ContentSchemaValidator` | static validation helper；Engine stage 内消费 | eligible internalize |
| `ContentValidationIssue` | public stable `ContentModelSchemaProjection.ValidateDocuments(...)` 和 retained `ContentPipelineResult`/stage output 传播 | retained public |
| `IContentProviderFactory` | public stable `ContentPipeline` constructor 参数 | retained public |
| `ITemplateRenderer` | public candidate base 的 interface；多个 tests 实现，且 base 文档明确描述 renderer replacement seam | retained public pending extension migration |
| `ContentStageInput` | public candidate stage interface 的参数，同时由 stable `ContentPipeline` constructor 传播该 interface | retained public |
| `ContentStageOutput` | public candidate stage interface 的返回，同时包含 retained issue | retained public |
| `IContentStage` | public stable `ContentPipeline(IReadOnlyList<IContentStage>, ...)` 参数 | retained public |
| `TemplateRendererBase` | public abstract base，包含 protected constructor、state、abstract hooks 与 virtual layout hook；明确形成扩展语义 | retained public pending extension migration |

D9B 结论：2 eligible、7 retained。`ContentPipeline` 的 explicit-stage constructor 是
`1.x-do-not-narrow` parent member；Task 34 不得通过把该 constructor 改成 internal 来
制造 stage graph 的收窄资格。renderer interface/base 具有真实 protected extension
语义；没有公开搜索命中不等于不存在 subclass，替代 seam、迁移说明或 obsolete window
均未建立，因此本批保留。

## 6. D9C：filesystem/output utilities（9）

| Candidate | 原子图 | 资格结论 |
|---|---|---|
| `DirectoryCopy` | 与 options/path policy 共同使用 | eligible internalize |
| `DirectoryCopyOptions` | 只被 candidate copy API 暴露 | eligible internalize |
| `FileWriter` | Engine output writer helper | eligible internalize |
| `Incremental.HashUtil` | incremental/render dependency hashing helper | eligible internalize |
| `IOutputFileSystem` | Engine output seam；implementation 为 candidate safe filesystem | eligible internalize |
| `IOutputPathPolicy` | 只被本 cluster copy/writer/safe filesystem 公开成员传播 | eligible internalize |
| `OutputPathSecurityException` | 运行时 exact type 只由 resolver 抛出，不在 stable public signature | eligible internalize |
| `SafeOutputFileSystem` | candidate interfaces 的 Engine implementation | eligible internalize |
| `SafePathResolver` | candidate path policy implementation | eligible internalize |

D9C 结论：9 eligible、0 retained。Task 35 只能改变 accessibility，必须保持 F-01、
F-03、F-04 形成的 destructive guard、symlink/reparse、destination identity、
collision-before-write、既有 direct-write/preflight、hash 和 diagnostic code 下界。
当前 `FileWriter`/`SafeOutputFileSystem` 不是 temp+rename filesystem-atomic writer，
关闭台账不得虚构 atomic replacement 保证。

## 7. D9D：feed/SEO/sitemap generators（8）

| Candidate | 传播事实 | 资格结论 |
|---|---|---|
| `AtomFeedGenerator` | Engine feed projection helper；接收 stable `RssGenerator.Post` | eligible internalize |
| `JsonFeedGenerator` | Engine feed projection helper；接收 stable `RssGenerator.Post` | eligible internalize；见第 12 节止损项 |
| `RssGenerator` | containing type 内含 public stable `RssGenerator.Post / 1.x-do-not-narrow` | retained public |
| `SitemapGenerator` | 只与两个 candidate nested records 形成完整生成图 | eligible internalize with nested types |
| `SitemapGenerator.Alternate` | 只在 candidate parent/member graph 中传播 | eligible internalize with parent |
| `SitemapGenerator.UrlEntry` | 只在 candidate parent/member graph 中传播 | eligible internalize with parent |
| `SeoAlternatesService` | Engine variant SEO helper | eligible internalize |
| `SeoInjectionPolicy` | Engine HTML transform policy helper | eligible internalize |

D9D 结论：7 eligible、1 retained。不能通过 internalize containing
`RssGenerator` 让已保护的 nested `Post` 失去有效 public 可达性；也不能把 `Post`
迁移为新 top-level DTO。

## 8. D9E：built-in plugins（13）

13 个候选均实现 stable `Bukit.Engine.Abstractions` plugin interfaces，但 type
construction 和生命周期由 Engine 静态路径拥有，未发现 Core 外 CLR 构造、反射或
dynamic assembly registration：

| Registry-owned（9） | Aggregate-only（4） |
|---|---|
| `AliasPlugin` | `FeedPlugin` |
| `ArchivePlugin` | `LlmsTxtPlugin` |
| `DataFilesPlugin` | `SearchIndexPlugin` |
| `ImageProcessingPlugin` | `SitemapPlugin` |
| `MenuPlugin` |  |
| `PagesIndexPlugin` |  |
| `PaginationPlugin` |  |
| `RelatedContentPlugin` |  |
| `TaxonomyPlugin` |  |

全部 13 项均 eligible internalize，但不能把它们错误描述成“全部由 registry 注册”：

- `BuiltInPluginSource` 实际注册一个非候选 `AnalyticsPlugin` 和上述 9 个候选；
- Feed、LLMs、search index、sitemap 通过 aggregate projection 路径直接调用；
- Task 37 必须锁定 9+4 归属、registration/order/name/version、hook/capability、
  report 和 output ownership；
- 不引入 reflection、动态程序集或 external process plugin source。

## 9. D9F：Notion fetch integration（2）

| Candidate | 传播事实 | 资格结论 |
|---|---|---|
| `INotionPageFetcher` | 只由 candidate `PagesIndexPlugin` constructor 与 internal default adapter 使用 | eligible internalize after PagesIndexPlugin |
| `NotionFetchedPage` | interface 返回与 PagesIndex projection 使用；与 interface 原子绑定 | eligible internalize with interface |

D9F 结论：2 eligible。Task 38 必须在 D9E 已收窄 `PagesIndexPlugin` 后执行；不得新增
第二套 Notion client，分页、取消、缓存和 PagesIndex 输出保持。

## 10. D9G：plugin source/capability（3）

| Candidate | 传播事实 | 资格结论 |
|---|---|---|
| `BuiltInPluginSource` | `PluginRegistry` 内部静态构造 | eligible internalize |
| `IPluginSource` | 只由 built-in source/registry implementation graph 使用 | eligible internalize with source |
| `PluginCapability` | Engine 对 stable string capabilities 的内部词汇 helper | eligible internalize |

public stable `PluginRegistry.GetAllPlugins(...)` 返回
`IBukitPlugin/string` tuple，不传播 `IPluginSource`。三个候选均可原子收窄；字符串
`emit-outputs`、`derive-pages` 和 CG-019 静态注册边界不变。

## 11. D9H：list/template capability helpers（5）

| Candidate | 传播事实 | 资格结论 |
|---|---|---|
| `SpecialListRouteBuilder` | Engine page dispatcher/list graph helper | eligible internalize |
| `TemplateCapabilitiesResolver.ListPageContentResolution` | public stable parent `ResolveListPageContent(...)` 返回类型 | retained public |
| `TemplateCapabilitiesResolver.TemplateCapabilityFlags` | public stable parent `GetCapabilities(...)` 返回类型，并包含 field declarations | retained public |
| `TemplateCapabilitiesResolver.TemplateFieldDeclaration` | retained flags 的 public `Fields` 元素类型 | retained public |
| `TemplateVariableWarning` | public stable `ScribanTemplateLinter.Lint*` 返回元素类型 | retained public |

D9H 结论：1 eligible、4 retained。修改 parent public members 才能收窄四个 companion，
但 parent 已是 `1.x-do-not-narrow` 且不在 Task 40 候选范围；强行收窄会造成
inconsistent accessibility 或越界 API 迁移。

## 12. 新发现的范围外安全止损项

只读调用链审计确认一个在 G4 基线已经存在、与 CLR accessibility 无关的 Core output
路径 P1：

```text
site.feed.path / collection.output.feedPath
  -> FeedPlugin or PublishAggregateProjectionWriters
  -> JsonFeedGenerator.Generate(..., feedFileName, ...)
  -> Path.Combine(outputDir, feedFileName)
  -> File.Create(path)
```

`JsonFeedGenerator` 没有像 Atom 的 `FileWriter.WriteUtf8(...)` 或 RSS collection path 的
normalizer 一样在写入前经过 safe relative path policy。复核确认 config schema 只要求
string，strict field validator 只检查字段名，`SiteDefaultsApplier`、`ConfigApplier`、
`I18nValidator` 和 `ConfigValidator` 均不拒绝该值。全局 JSON feed 可通过 `..`、rooted
path 或 output 内既有 symlink 写出 output root。

最小复现：

```yaml
site:
  url: https://example.com
  feed:
    formats: [json]
    path: ../escaped-feed
```

当 output 为 `<project>/dist` 时，目标变为
`<project>/escaped-feed/feed.json`，`File.Create` 会创建或截断该文件。Atom 路径会由
`SafePathResolver` 拒绝同类输入，因此不是统一上游策略已经覆盖的误报。

本问题：

- 不是 Task 32 或任何 G-04 visibility change 引入；
- 不授权在 G-04D9 顺带修改 config schema、feed URL、path policy 或 writer；
- 不影响 `JsonFeedGenerator` 的纯 accessibility 资格判断；
- 建议严重度为 **Important / P1**；
- 不阻断 D9D 的纯 visibility 资格结论；
- 阻断 Task 42 按总计划给出无条件 Critical/Important/Minor `0/0/0`。

Task 36 若继续实施，必须保持 JSON/XML/HTML bytes 和 URL 行为不变，并把该项登记为
独立 Core correctness/security follow-up。按照总计划严格关闭口径，建议先暂停 G4
production changes，另立受控 Core 修复任务；完成独立回归和复审后，从新的 `2.0`
基线恢复 G4。G-04 内不得顺带修改 schema、feed URL、全局路径工具或 writer 行为。

## 13. Task 33～40 实施顺序

严格顺序：

1. D9A：先保留三个 public companion，再原子收窄 build/context 与 route/result；
2. D9B：固定 stable parent、stage 和 renderer extension signatures，只收窄两个 validator；
3. D9C：九项 filesystem/output cluster 一次形成可编译图；
4. D9D：保留 `RssGenerator`，只收窄另外七项；
5. D9E：13 个 built-in types 收窄，并区分 9 registry + 4 aggregate-only；
6. D9F：在 `PagesIndexPlugin` 已 internal 后收窄 fetch interface/record；
7. D9G：source/interface/capability 原子收窄；
8. D9H：只收窄 route builder，四个 template companion 重分类保留；
9. Task 41：56/56 终态汇总；
10. Task 42：唯一 G4 完整测试、aggregate、AOT、轻量复审和整个 G-04 最终只读复审。

每个 cluster 先建立 architecture/behavior assertions，但 Task 33～41 不运行正式测试、
focused gate、aggregate、AOT 或单任务复审。

## 14. 停止条件

任一 cluster 若发现下列新事实，应停止对应 internalization 并转为 retained/migration：

- 新外部 CLR consumer、subclass 或 interface implementation；
- stable public/protected signature、serializer/source generator 或 AOT root 传播；
- 必须改配置、模板、report、plugin protocol、feed/schema 或安全行为才能编译；
- 必须新增 production friend、reflection fallback 或动态 plugin loading；
- 测试只能通过删除行为断言、改变输出 golden 或放宽安全策略。

保留 public 是合法终态。D9 的目标是消除责任不清晰，而不是把 56 强制变成零 public
types。
