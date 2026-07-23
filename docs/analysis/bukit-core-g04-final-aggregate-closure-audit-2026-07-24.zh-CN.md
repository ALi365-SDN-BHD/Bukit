# Bukit Core G-04 公共面治理最终关闭审计

> 日期：2026-07-24
>
> 范围：Bukit Core；Labs 与外部插件不属于修复范围
>
> G4 基线：`2.0@729088dbc2faf1bf7a20fe670e96a09b7568e7ba`
>
> 状态：closed / group-verification-complete / aggregate-review-complete

## 1. 执行结论

G-04 已把 AD-04 从“public 含义不清”转化为可执行治理：

- historical cohort 的 136/136 个 CLR identity 均有终态；
- 97 项不再 exported，其中 92 项为 internalized/removed、5 项为 migrated；
- 39 项 retained public；superseded、blocked 的 identity 终态均为 0；
- current baseline 为 14 assemblies / 443 public types / 0 candidates；
- 443 项全部具有 owner、classification、compatibility、migration horizon 和
  public/protected member inventory；
- historical consumer manifest 仍为 136 项，Git blob 仍为
  `7b07d6890562387010b52301e9f8716e9bf10ed1`。

`internalized/removed` 是 CLR identity 终态合并标签：可能是 access 收窄，也可能是经
批准删除旧 facade/carrier。它不表示 persisted/wire/config contract 被删除。
`retained-public` 表示当前跨程序集实现、稳定 shape、wire/serialization 或 protected
extension seam 需要该类型保持 exported；它不自动构成通用第三方 CLR SDK 承诺。

AD-04 判定为 **closed**。依据是 443 项已全部分类、drift gate 已落地、136 个候选已
全部决策并具备迁移/验证链，而不是单凭 public 类型数量下降。

## 2. 决策链索引

| 阶段 | 核心产物/提交锚点 | 结论 |
|---|---|---|
| G-04A | `2005d148`～`1b54aebb` | 纠正 build-report 与 Theme 语义分类 |
| G-04B1/B2/B3 | `fe2af9ea`～`46c9005e` | 建立 136 项 manifest、认证搜索和声明窗口关闭规则 |
| G-04C | `aab05364`、`3f77a738` | 单类型 deliberate removal 试点完成 |
| G-04D1 | `bbec35aa`、`5c08c950`、`3822933a`、`21072f4f` | Notion legacy renderer/facade/extension graph 原子移除 |
| G-04D2A/B | `2272156f`、`757fb149`、`27492e20`、`519eb995` | secret masker 与 error-code contract 迁移/收窄 |
| G-04D2D～D3 | `c54671fa`～`b054b471` | PluginHost 与 Content Group 1 完整关闭 |
| G-04D3/D4/D5 | `4f81eba1`～`7faed66a` | Content、Shared、CLI Shared Group 2 完整关闭 |
| G-04D6/D7/D8 | `4635fc9d`～`6f10269c` | Rendering、Routing、Theme Group 3 完整关闭 |
| 独立 P1 | `547b1728`～`729088db` | JSON Feed output path 漏洞在 G4 前独立关闭 |
| G4 入场 | `a853cc0b`、`921f2f85`、`2439a8d6` | Theme 汇总、Engine eligibility 与 P1 后恢复点 |
| G-04D9A～H | `df9edfc6`～`409aa4b7` | Engine 56 项：41 internalized、15 retained |
| Task 41/42 | `3b7742ca`、`e368a72d`、`2679cd6e`、`0152d9c1` | 决策汇总、验证缺口、最终台账与复审修正 |

完整计划与各 owner 的逐项理由见
[master plan](../superpowers/plans/2026-07-23-bukit-core-g04-remaining-public-surface-governance-master-plan.md)
及本目录下 G-04A～D9 台账。三份先行组级证明分别见
[G1](bukit-core-g04-group1-verification-ledger-2026-07-23.zh-CN.md)、
[G2](bukit-core-g04-group2-verification-ledger-2026-07-23.zh-CN.md)、
[G3](bukit-core-g04-group3-verification-ledger-2026-07-23.zh-CN.md)。

## 3. 136 项终态

### 3.1 Assembly 汇总

| Historical assembly | internalized/removed | migrated | retained-public | 合计 |
|---|---:|---:|---:|---:|
| Bukit.Cli.Shared | 4 | 0 | 1 | 5 |
| Bukit.Content | 34 | 1 | 0 | 35 |
| Bukit.Engine | 42 | 0 | 15 | 57 |
| Bukit.PluginHost | 8 | 0 | 8 | 16 |
| Bukit.Rendering | 2 | 0 | 0 | 2 |
| Bukit.Routing | 0 | 1 | 0 | 1 |
| Bukit.Shared | 1 | 3 | 13 | 17 |
| Bukit.Theme | 1 | 0 | 2 | 3 |
| **合计** | **92** | **5** | **39** | **136** |

### 3.2 逐项投影

投影规则是 exact CLR full-name membership：历史 identity 若存在于 current compiled
baseline 则为 `retained-public`；不存在时，再按已批准任务决议区分
`internalized/removed` 与 `migrated`。不存在按 simple name 猜测、namespace 前缀匹配
或文本搜索替代。

| Assembly | Historical CLR identity | 终态 |
|---|---|---|
| `Bukit.Cli.Shared` | `Bukit.Cli.Shared.Cli.Binding.CliBoundCommandFactory` | internalized/removed |
| `Bukit.Cli.Shared` | `Bukit.Cli.Shared.Cli.Parsing.CliParseResult` | retained-public |
| `Bukit.Cli.Shared` | `Bukit.Cli.Shared.Cli.Parsing.SimpleParseResult` | internalized/removed |
| `Bukit.Cli.Shared` | `Bukit.Cli.Shared.Cli.Parsing.SubcommandParseResult` | internalized/removed |
| `Bukit.Cli.Shared` | `Bukit.Cli.Shared.Cli.Rendering.CliErrorRenderer+CliErrorPayload` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.CompositeContentBodyStore` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.DictionaryContentBodyStore` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Markdown.BasicMarkdownToHtml` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Markdown.MarkdownBodyStore` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.AudioBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.BookmarkBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.CalloutBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.ChildEntityBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.CodeBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.ColumnBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.ColumnListBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.DividerBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.EmbedBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.EquationBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.FileBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.ImageBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.LinkPreviewBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.LinkToPageBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.NoOpBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.PdfBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.RichTextContainerRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.SyncedBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.TableBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.TableOfContentsBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.ToDoBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.ToggleBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.BlockRenderers.VideoBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.INotionBlockRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.NotionBlockRendererRegistry` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.NotionBlockTransformer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.NotionBlocksRenderer` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.NotionClientStats` | migrated |
| `Bukit.Content` | `Bukit.Content.Notion.NotionColorPalette` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.NotionRenderContext` | internalized/removed |
| `Bukit.Content` | `Bukit.Content.Notion.NotionRichTextRenderer` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.AtomFeedGenerator` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.BuildOptions` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.BuildPipeline` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.BuildPipelineContext` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.BuildVariantSummary` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.ContentCollectionContractValidator` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.ContentPipelineResult` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.ContentSchemaValidator` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.ContentValidationIssue` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.DirectoryCopy` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.DirectoryCopyOptions` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.FileWriter` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.IContentProviderFactory` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.ITemplateRenderer` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.Incremental.HashUtil` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.JsonFeedGenerator` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Output.IOutputFileSystem` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Output.IOutputPathPolicy` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Output.OutputPathSecurityException` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Output.SafeOutputFileSystem` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Output.SafePathResolver` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.AliasPlugin` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.ArchivePlugin` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.DataFilesPlugin` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.FeedPlugin` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.INotionPageFetcher` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.ImageProcessingPlugin` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.LlmsTxtPlugin` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.MenuPlugin` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.NotionFetchedPage` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.PagesIndexPlugin` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.PaginationPlugin` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.RelatedContentPlugin` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.SearchIndexPlugin` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.SitemapPlugin` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltIn.TaxonomyPlugin` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.BuiltInPluginSource` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.IPluginSource` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Plugins.PluginCapability` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.RouteInventoryInspectEntry` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.RoutePipeline` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.RoutePipelineResult` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.RssGenerator` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.SeoAlternatesService` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.SeoInjectionPolicy` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.SitemapGenerator` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.SitemapGenerator+Alternate` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.SitemapGenerator+UrlEntry` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.SpecialListRouteBuilder` | internalized/removed |
| `Bukit.Engine` | `Bukit.Engine.Stages.ContentStageInput` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.Stages.ContentStageOutput` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.Stages.IContentStage` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.TemplateCapabilitiesResolver+ListPageContentResolution` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.TemplateCapabilitiesResolver+TemplateCapabilityFlags` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.TemplateCapabilitiesResolver+TemplateFieldDeclaration` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.TemplateRendererBase` | retained-public |
| `Bukit.Engine` | `Bukit.Engine.TemplateVariableWarning` | retained-public |
| `Bukit.PluginHost` | `Bukit.PluginHost.IPluginProcessInvoker` | retained-public |
| `Bukit.PluginHost` | `Bukit.PluginHost.IPluginRequestIdFactory` | retained-public |
| `Bukit.PluginHost` | `Bukit.PluginHost.IProcessRunner` | retained-public |
| `Bukit.PluginHost` | `Bukit.PluginHost.PluginExecutionReport` | internalized/removed |
| `Bukit.PluginHost` | `Bukit.PluginHost.PluginExecutionReporter` | internalized/removed |
| `Bukit.PluginHost` | `Bukit.PluginHost.PluginExecutionResponseSummary` | internalized/removed |
| `Bukit.PluginHost` | `Bukit.PluginHost.PluginFileSystemPermissionEvaluator` | internalized/removed |
| `Bukit.PluginHost` | `Bukit.PluginHost.PluginHostErrorCodes` | internalized/removed |
| `Bukit.PluginHost` | `Bukit.PluginHost.PluginPermissionPathNormalizer` | internalized/removed |
| `Bukit.PluginHost` | `Bukit.PluginHost.PluginProcessRequest` | retained-public |
| `Bukit.PluginHost` | `Bukit.PluginHost.PluginProcessResult` | retained-public |
| `Bukit.PluginHost` | `Bukit.PluginHost.PluginRuntimeOnlyContext` | internalized/removed |
| `Bukit.PluginHost` | `Bukit.PluginHost.PluginSecretMasker` | internalized/removed |
| `Bukit.PluginHost` | `Bukit.PluginHost.ProcessOutputStream` | retained-public |
| `Bukit.PluginHost` | `Bukit.PluginHost.ProcessRunRequest` | retained-public |
| `Bukit.PluginHost` | `Bukit.PluginHost.ProcessRunResult` | retained-public |
| `Bukit.Rendering` | `Bukit.Rendering.Scriban.FileTemplateLoader` | internalized/removed |
| `Bukit.Rendering` | `Bukit.Rendering.Scriban.ScribanModelBinder` | internalized/removed |
| `Bukit.Routing` | `Bukit.Routing.RouteGenerator+RouteGenerationResult` | migrated |
| `Bukit.Shared` | `Bukit.Shared.Notion.BulletedListItemBlock` | retained-public |
| `Bukit.Shared` | `Bukit.Shared.Notion.CalloutBlock` | retained-public |
| `Bukit.Shared` | `Bukit.Shared.Notion.CodeBlock` | retained-public |
| `Bukit.Shared` | `Bukit.Shared.Notion.Heading1Block` | retained-public |
| `Bukit.Shared` | `Bukit.Shared.Notion.Heading2Block` | retained-public |
| `Bukit.Shared` | `Bukit.Shared.Notion.Heading3Block` | retained-public |
| `Bukit.Shared` | `Bukit.Shared.Notion.HtmlTokenizer` | migrated |
| `Bukit.Shared` | `Bukit.Shared.Notion.HtmlTokenizer+HtmlToken` | migrated |
| `Bukit.Shared` | `Bukit.Shared.Notion.HtmlTokenizer+HtmlTokenType` | migrated |
| `Bukit.Shared` | `Bukit.Shared.Notion.ImageBlock` | retained-public |
| `Bukit.Shared` | `Bukit.Shared.Notion.NotionBlock` | retained-public |
| `Bukit.Shared` | `Bukit.Shared.Notion.NumberedListItemBlock` | retained-public |
| `Bukit.Shared` | `Bukit.Shared.Notion.ParagraphBlock` | retained-public |
| `Bukit.Shared` | `Bukit.Shared.Notion.QuoteBlock` | retained-public |
| `Bukit.Shared` | `Bukit.Shared.Notion.RichTextSegment` | retained-public |
| `Bukit.Shared` | `Bukit.Shared.Notion.ToggleBlock` | retained-public |
| `Bukit.Shared` | `Bukit.Shared.ValueCoercion` | internalized/removed |
| `Bukit.Theme` | `Bukit.Theme.SchemaValidationError` | retained-public |
| `Bukit.Theme` | `Bukit.Theme.SchemaValidationException` | internalized/removed |
| `Bukit.Theme` | `Bukit.Theme.ThemeDoctorCommand+DoctorResult` | retained-public |

## 4. Current baseline

### 4.1 Assembly

| Assembly | Public types |
|---|---:|
| Bukit.Cli.Shared | 16 |
| Bukit.Config | 64 |
| Bukit.Content | 16 |
| Bukit.Content.Notion | 2 |
| Bukit.Engine | 38 |
| Bukit.Engine.Abstractions | 50 |
| Bukit.Notion | 62 |
| Bukit.Plugin.Abstractions | 30 |
| Bukit.PluginHost | 32 |
| Bukit.Rendering | 20 |
| Bukit.Routing | 5 |
| Bukit.Shared | 35 |
| Bukit.Theme | 33 |
| bukit | 40 |
| **合计** | **443** |

### 4.2 Classification / compatibility

Current baseline 还按 20 个语义 owner 完整归属：

| Owner | 数量 |
|---|---:|
| Build engine | 38 |
| CLI contract infrastructure | 16 |
| Canonical model and in-process engine contracts | 50 |
| Configuration | 64 |
| Content acquisition | 16 |
| Core CLI | 40 |
| External plugin host | 32 |
| External plugin protocol | 30 |
| Notion API endpoint contract | 1 |
| Notion block contract | 13 |
| Notion content adapter contract | 2 |
| Notion conversion contract | 5 |
| Notion diagnostics contract | 4 |
| Notion rendering contract | 31 |
| Notion transport contract | 6 |
| Notion write contract | 2 |
| Rendering and theme model | 20 |
| Routing | 5 |
| Shared foundation | 35 |
| Theme runtime | 33 |
| **合计** | **443** |

| Classification | 数量 |
|---|---:|
| aot-serialization-surface | 3 |
| cross-assembly-implementation | 275 |
| implementation-public | 40 |
| persisted-internal-format | 6 |
| plugin-wire-contract | 23 |
| serialized-contract | 96 |

| Compatibility | 数量 |
|---|---:|
| 1.x-do-not-narrow | 278 |
| 1.x-migration-safe | 6 |
| 1.x-shape-stable | 119 |
| not-a-clr-contract | 40 |

两组分别均为 443；`2.0-candidate` 为 0。40 个 `not-a-clr-contract` 全部来自 CLI
executable assembly，不应被误写成 SDK。

## 5. Breaking CLR 变化与迁移

所有 deliberate breaking CLR 变化均发生在 `2.0`，且有任务级批准和迁移说明：

- G-04C 删除 `RouteInventoryInspectEntry` 旧 inspect entry；
- D1A/D1B/D1C 删除 legacy Notion static/rendering/extension facade，并以 canonical
  client/renderer contract fixture 固定迁移路径；
- D3B 把 legacy `Bukit.Content.Notion.NotionClientStats` 迁移到 canonical transport
  identity；
- D4A 把三个 Shared `HtmlTokenizer` identity 迁移到 canonical Notion conversion
  owner；
- D7 把 nested `RouteGenerationResult` 迁移为命名 tuple，CLI/Engine consumer 与
  baseline 同步；
- 上述 deliberate removal/migration 共 36 项，其余 61 个 absent identity 为经
  owner graph 证明的 internalization，未删除
  wire、schema 或持久化 shape。

private、未索引或未自愿声明的外部 CLR consumer 仍属于未知信息；这项限制已写入历史
manifest，不被“搜索无结果”伪装为“不存在”。

## 6. Task 42 验证证据

独立 worktree 初始缺少 `project.assets.json`。最初无输出且退出 0 的 `dotnet test
--no-restore` 不计为测试证据；完成 Core 与九个测试项目 restore 后重新执行：

| 项目 | Passed / Failed / Skipped |
|---|---:|
| Bukit.Engine.Tests | 1597 / 0 / 0 |
| Bukit.Engine.Abstractions.Tests | 60 / 0 / 0 |
| Bukit.Content.Tests | 464 / 0 / 0 |
| Bukit.Content.Notion.Tests | 6 / 0 / 0 |
| Bukit.Rendering.Tests | 169 / 0 / 0 |
| Bukit.Routing.Tests | 27 / 0 / 0 |
| Bukit.Theme.Tests | 74 / 0 / 0 |
| Bukit.Cli.Tests | 618 / 0 / 0 |
| Bukit.Architecture.Tests | 259 / 0 / 0 |
| **合计** | **3274 / 0 / 0** |

测试首次发现两个验证缺口，均只修改 Architecture tests：

1. 既有 Analytics boundary test 仍编译期引用已 internalize 的
   `BuiltInPluginSource`/`PluginCapability`，改为通过稳定 `PluginRegistry`
   assembly anchor 反射验证；
2. D9E 文档断言把数字英文拼写差异误判为缺失，改为同时接受 `9/nine` 和
   `4/four`，仍锁定 ownership 数量与术语。

修正后 Architecture 259/259、对应 focused owner gate 和
`dotnet format ... --verify-no-changes --no-restore` 均通过。

`public-api-drift.sh check Release` exit 0，编译导出面与 baseline 一致。

## 7. Native AOT 与发布产物

Core-only Darwin arm64 证明：

- `package-native-aot.sh 2.0.0-g04g4 osx-arm64 ... Release` exit 0；
- archive：
  `/private/tmp/bukit-g04-g4-aot/bukit-2.0.0-g04g4-osx-arm64.tar.gz`；
- 归档非空，published CLI 可执行；
- `release-artifacts.sh` exit 0；
- basic Markdown fixture config/build 成功；
- publish audit：`routes=2 errors=0 warnings=22`。

这覆盖 static built-in registration、feed/sitemap/SEO serialization roots、Core CLI
编排与真实 published binary。没有修改或验证 Labs/外部插件业务实现。

## 8. 不变量与越界检查

- historical manifest：`declarationState=closed`、136/136、blob 未变；
- 配置 schema、plugin protocol、asset URL、HTTP/TLS、persisted format 未改；
- D9C 只改变 filesystem/output 类型可见性，F-01/F-03/F-04 安全算法未改；
- D9D 保留 external SEO image no-fetch 与两个 `external_unverified` code；
- D9E/D9G 保留静态 AOT registration 与 CG-019，不引入 CLR plugin SDK；
- JSON Feed P1 是 G4 基线前独立修复，不被重复计入 visibility diff；
- 没有新增 production `InternalsVisibleTo`；D2E 只向
  `Bukit.PluginHost.Tests` 与 `Bukit.Cli.Tests` 新增两个精确 test-only friends；
- full/release/whole-solution gates 未运行。

## 9. Closing proof

G4 唯一 aggregate 在 `2679cd6e` 执行：

```text
post-change-targeted.sh
  --base 729088dbc2faf1bf7a20fe670e96a09b7568e7ba
  -- 78 paths
```

命令级移除宿主 `NOTION_TOKEN`，exit 0。实际通过 Engine 1597/1597、
Architecture 259/259、docs consistency、`dotnet format`、code-analysis ratchet、
public API drift/self-test、portability、brainstorm server self-test、CLI/config/docs
contracts 与 YAML static context deterministic drift。

aggregate 后只发生独立复审要求的治理文档修正：

- 恢复 D3B 历史时点 `84`，不把 current `0` 回写到旧授权边界；
- 把 136 项终态校准为 92 internalized/removed、5 migrated、39 retained；
- 补齐 20 个 owner、遗漏提交锚点、test-only IVT 与 D2B2 task-scoped 措辞。

这些 post-aggregate 文档修正分别通过 focused diff check 与完整
`docs-consistency.sh`；按“G4 aggregate 只执行一次”约束没有重跑 aggregate。

独立复审结果：

| 复审 | 范围 | Critical / Important / Minor |
|---|---|---:|
| G4 轻量只读复审 | `729088db..0152d9c1` | 0 / 0 / 0 |
| G-04 全决策链只读审计 | G-04A～Task 42，含修正后工作树 | 0 / 0 / 0 |
| 最终台账证据一致性复核 | 136 投影、baseline、提交/测试/AOT 证据 | 0 / 0 / 0 |

最终判定：

- G1～G4 均为 `group-verification-complete`；
- G-04 historical cohort 136/136 正式关闭；
- current baseline 无未解释 drift 或悬空 candidate；
- AD-04 正式关闭；
- 不需要整体重构；后续新公共面变化继续由 baseline/drift gate 和单独 2.0
  migration task 治理。
