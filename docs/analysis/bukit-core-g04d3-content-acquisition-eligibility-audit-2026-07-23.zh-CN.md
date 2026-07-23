# Bukit Core G-04D3 Content acquisition 资格审计

日期：2026-07-23

状态：Task 9 资格审计完成；`group-verification-pending`

分支：`codex/g04-group1-pluginhost-content-a`

并行调查基线：`15f0bd352f3c3b1c9a94e24d8e2885d5b2f428a0`

资格结论复核 HEAD：`c0195b92c32d46a2700fff603bc0f67bf5be469f`

G1 `GROUP_BASE`：`10bfead3f28b8a9f82a9b5fc008a16d49e290cae`

目标版本线：`2.0`

## 1. 执行结论

G-04D3 的五个候选不能作为一个批次处理。当前源码和消费者证据支持把它们拆成两个
变更原因不同的原子图：

1. **G-04D3A Body/Markdown graph（Task 10）**
   - `Bukit.Content.CompositeContentBodyStore`
   - `Bukit.Content.DictionaryContentBodyStore`
   - `Bukit.Content.Markdown.BasicMarkdownToHtml`
   - `Bukit.Content.Markdown.MarkdownBodyStore`
2. **G-04D3B Notion transport facade（Task 11）**
   - `Bukit.Content.Notion.NotionClientStats`

四个 Body/Markdown 类型均具备在 Task 10 **仅收窄为 `internal`** 的资格：

- production 构造和调用都留在 `Bukit.Content`；
- retained public boundary 只传播 `IContentBodyStore` 或 provider/build 行为，不传播四个
  concrete/helper CLR identity；
- 类型均无继承扩展点、protected member、反射绑定、序列化契约或 source-generator
  注册；
- 仓库内跨程序集直接构造仅出现在测试中；Task 10 复核确认 test friendship 由
  `InternalsVisibleTo.cs` 提供，而不是项目文件；
- internalize 不需要 facade、不需要新增 production `InternalsVisibleTo`，也不需要改变
  Markdown、content body、配置或持久化语义。

`NotionClientStats` **不得进入 Task 10**。它是 legacy Content facade 中的重复统计 DTO，
canonical owner `Bukit.Notion.Transport.NotionClientStats` 已存在；但迁移会涉及 legacy
consumer fixture、Notion boundary、统计语义和 transport lifetime，必须留给 Task 11
独立完成 canonical facade migration。

本报告只作 eligibility 决策，没有修改访问级别、baseline、测试或运行行为，也没有运行
任何测试、门禁或 Native AOT。Task 10 尚未完成，因此不得把四项写成
`internalized/closed`；当前统一状态是：

> `eligible-for-task10-internalization / group-verification-pending`

## 2. 审计边界与禁止漂移

### 2.1 本任务包含

- 五项候选的 current public/protected surface；
- 仓库内生产、测试和活动文档消费者；
- 构造传播和 retained public signature 传播；
- 可替代行为边界；
- inheritance、reflection、serialization、source generation 和 Native AOT 静态可达性；
- 外部消费者证据及其限制；
- Task 10 必须补齐的最小行为和架构断言。

### 2.2 本任务不包含

- 不修改任何 C# 源码、访问级别或 public API baseline；
- 不修改关闭的 136 项历史 candidate manifest；
- 不修改 Content schema、配置、媒体、SEO、asset URL、插件协议、路径工具或报告格式；
- 不改变 content body fallback、exception、cancellation、case comparison、file I/O、
  Markdown pipeline 或 HTML safety 行为；
- 不处理 Notion retry、rate limit、API、transport 或 HttpClient ownership；
- 不运行单项目测试、focused/targeted gate、public API drift、Native AOT、full/release
  gate 或复审；
- 不修改 1.x `main`。

Task 10 只获得“四个类型由 `public` 变为 `internal`”的资格，不获得顺带重构、删除
实现、统一 fallback 规则或修复 Markdown 安全语义的授权。

## 3. 当前事实基线

Task 9 资格调查时 public API baseline 为 14 / 501 / 89。Task 10 实施四项收窄后，当前
public API baseline 为：

- 14 个 Core assemblies；
- 497 个 public types；
- 85 个 `2.0-candidate`；
- 四个 Body/Markdown 候选已从 current baseline 移除；
- `NotionClientStats` 仍 public，并继续留在 current baseline 直到 Task 11。

四个 Body/Markdown 类型与 stats 类型的职责边界如下：

```text
ContentProviderFactory / providers
  └─ RawContentLoadResult.BodyStore : IContentBodyStore
       ├─ CompositeContentBodyStore
       ├─ MarkdownBodyStore
       └─ other internal/concrete stores

MarkdownFolderProvider / MarkdownTextHelper
  └─ BasicMarkdownToHtml

NotionApiClient.GetStats() [internal legacy facade]
  └─ Bukit.Content.Notion.NotionClientStats [duplicate]

Bukit.Notion.Transport.NotionClient.GetStats() [canonical]
  └─ Bukit.Notion.Transport.NotionClientStats
```

因此，对外稳定的 Content 行为边界是 provider、`RawContentLoadResult` 和
`IContentBodyStore`；四个具体实现不是必须保留的 CLR SDK identity。stats 则是一个已有
canonical owner 的跨程序集 facade migration 问题，不能用“同样改成 internal”代替。

## 4. 逐类型资格矩阵

| 候选 | 仓库内 production 消费 | 构造/签名传播 | 行为替代边界 | reflection / serialization / AOT | 终态 |
|---|---|---|---|---|---|
| `CompositeContentBodyStore` | `CompositeContentProvider` 同程序集构造 | 只以 `IContentBodyStore` 返回，不进入 retained public signature | `IContentBodyStore` + composite provider 行为 | 无 identity binding；Markdown CLI 路径静态可达 | Task 10 internalize |
| `DictionaryContentBodyStore` | 无 production 构造 | Engine 跨程序集直接构造仅在 tests；既有 source-level IVT | `IContentBodyStore` test/helper seam | 无 runtime binding；无 production AOT root | Task 10 internalize |
| `BasicMarkdownToHtml` | `MarkdownFolderProvider`、`MarkdownTextHelper` 同程序集静态调用 | helper identity 不传播 | Markdown provider/CLI 的 rendered HTML 与 TOC 行为 | 无 identity binding；Markdown CLI 路径静态可达 | Task 10 internalize |
| `MarkdownBodyStore` | `MarkdownFolderProvider` 同程序集构造 | 只以 `IContentBodyStore` 返回 | `IContentBodyStore` + Markdown provider 行为 | 无 identity binding；Markdown CLI 路径静态可达 | Task 10 internalize |
| `NotionClientStats` | legacy internal `GetStats()` 构造；canonical owner 已存在 | 不在 retained public member 返回值中；测试刻意冻结旧 identity | canonical `Bukit.Notion.Transport.NotionClientStats` | 无 runtime serializer/AOT root；有 test/governance roots | Task 11 canonical facade migration |

以下各节给出完整证据。

## 5. `CompositeContentBodyStore`

### 5.1 当前公共面

```csharp
public sealed class CompositeContentBodyStore : IContentBodyStore
{
    public CompositeContentBodyStore(
        IReadOnlyDictionary<string, IContentBodyStore> stores);

    public Task<ContentBody> GetAsync(
        ContentDocument document,
        CancellationToken cancellationToken = default);
}
```

类型是 `sealed`，没有 protected member。

### 5.2 消费者与构造传播

production 唯一直接构造位于同程序集
`src/Bukit-Core/Bukit.Content/CompositeContentProvider.cs`：

```text
ContentProviderFactory.Create
  -> CompositeContentProvider.LoadRawAsync
     -> new CompositeContentBodyStore(stores)
     -> RawContentLoadResult.BodyStore : IContentBodyStore
```

跨程序集获得的是 `IContentBodyStore`，不是 `CompositeContentBodyStore`。没有 retained
public/protected constructor、property、field、return type 或 generic constraint 传播该
concrete identity。

测试中 `Bukit.Content.Tests` 直接构造它；这是 owner test，不是 production API
依赖。`Bukit.Content` 已有对 `Bukit.Content.Tests` 的精确
`InternalsVisibleTo`，收窄不需要新增 friend。

活动文档 `guide/dev/content.md` 说明多源构建使用该实现，描述的是内部实现事实，没有
提供 CLR 构造教程或独立 SDK 承诺。internalize 后多源行为仍由 provider 保持。

### 5.3 必须保持的行为

当前调用顺序为：

1. cancellation；
2. non-empty inline HTML 直接返回，不 dispatch；
3. 从 `document.Id` 的第一个 `:` 前缀解析 source key；
4. 按 `_stores` 自身 comparer 查找 inner store；
5. 若 custom field `sourceId` 非空，恢复 inner document `Id`；
6. 仅当 `BodyKey` 以精确 `Ordinal` 的 `sourceKey:` 开头时去掉前缀；
7. 把 document 交给 inner store。

Task 10 不能调整 `IsNullOrEmpty`、source key comparer、`Ordinal` BodyKey 前缀规则、
exception message 或 cancellation 时机。

行为替代不是新 facade，而是已有的 `IContentBodyStore` 和
`CompositeContentProvider`。外部使用者应依赖 provider/build 行为，而不是 concrete
store identity。

### 5.4 Reflection、serialization 与 AOT

- 未发现 `Activator`、`Assembly.GetType`、DI registration、serializer metadata 或
  source-generated context 以该 full name 绑定 runtime；
- sealed 且无 protected surface，不存在外部继承扩展点；
- basic Markdown build 经 `ContentProviderFactory` 创建 composite provider，因此发布
  CLI 会静态触达此实现；
- access modifier 收窄不会改变静态调用图，但仍必须由 Task 10 的 published Native AOT
  smoke 证明，当前不得声称已通过。

### 5.5 资格结论

**Task 10 internalize。** 只允许 `public sealed class` → `internal sealed class`；
不删除、不建立 public facade、不新增 production friendship。

## 6. `DictionaryContentBodyStore`

### 6.1 当前公共面

```csharp
public sealed class DictionaryContentBodyStore : IContentBodyStore
{
    public DictionaryContentBodyStore(
        IReadOnlyDictionary<string, ContentBody> bodies);

    public Task<ContentBody> GetAsync(
        ContentDocument document,
        CancellationToken cancellationToken = default);
}
```

类型是 `sealed`，没有 protected member。

### 6.2 消费者与构造传播

除类型自身外，`src/` 没有 production 构造。直接消费者为：

- `Bukit.Content.Tests` 的 store 行为测试；
- `Bukit.Engine.Tests` 的 SearchIndex、PageRender 和 I18n 测试。

`SiteEngineIntegrationTests` 另有一个同名 nested private test type，不能误判为
`Bukit.Content.DictionaryContentBodyStore` 消费者。

`Bukit.Content` 的 `InternalsVisibleTo.cs` 已精确 friend：

- `Bukit.Content.Tests`
- `Bukit.Engine`
- `Bukit.Engine.Tests`

Task 9 并行调查把这些 friend 误写成项目文件已有配置；实际
`Bukit.Content.csproj` 没有任何 `InternalsVisibleTo` item。Task 10 以源码事实为准，
不复制、不移动也不扩张这三项。`Bukit.Engine` 是既有 production friend，用于 Engine
调用 internal `NotionCompatibilityQueries`，属于 Notion compatibility 跨程序集债务，
不是四个 Body/Markdown 候选的需求，也不在本任务调整。两个 test consumers 在
internalize 后继续依赖既有精确 friendship。

### 6.3 必须保持的行为

顺序为：

1. cancellation；
2. non-empty inline HTML；
3. `BodyKey` dictionary lookup；
4. missing 时抛出包含 document id 的 `InvalidOperationException`；
5. 命中时返回 dictionary 中同一个 `ContentBody` 实例。

Task 10 必须保留 `Assert.Same` identity 语义、dictionary comparer 由调用方决定的规则、
`IsNullOrEmpty` 和 exception。不能因为它主要是 test/helper seam 而删除实现或换成
复制对象。

行为替代边界是 `IContentBodyStore`。它不是一个需要新增 public replacement 的产品
facade。

### 6.4 Reflection、serialization 与 AOT

- 无 runtime reflection、serializer 或 source-generator binding；
- 无 production 构造路径，因此没有必须伪造的 published runtime AOT scenario；
- 编译和 tests 仍必须证明 internal type 在精确 IVT 下可用；
- 不得把“没有 production AOT root”错误写成 dead code；它仍是有效 owner/test utility。

### 6.5 资格结论

**Task 10 internalize。** 只改变 type accessibility，保留 constructor、member 和行为；
不删除，不公开新 helper。

## 7. `BasicMarkdownToHtml`

### 7.1 当前公共面

```csharp
public static class BasicMarkdownToHtml
{
    public static string Convert(string markdown);
    public static IReadOnlyList<TableOfContentsEntry>
        ExtractTableOfContents(string markdown);
}
```

静态类没有 inheritance/protected surface。

### 7.2 消费者与构造传播

全部 production 调用留在 `Bukit.Content`：

```text
MarkdownFolderProvider.LoadRawAsync
  -> BasicMarkdownToHtml.ExtractTableOfContents

MarkdownBodyStore.GetAsync
  -> MarkdownFolderProvider.RenderHtmlFromFileAsync
     -> MarkdownTextHelper.RenderHtmlFromFileAsync
        -> BasicMarkdownToHtml.Convert

MarkdownTextHelper.ExtractSummaryFromMarkdown
  -> BasicMarkdownToHtml.Convert
```

retained public provider 和 body-store interface 都不在签名中传播该 helper identity。
测试直接调用只是 owner behavior coverage。

### 7.3 必须保持的行为

当前 Markdig pipeline 包含：

- pipe tables；
- task lists；
- emphasis extras；
- auto links；
- GitHub auto identifiers；
- emoji/smiley；
- footnotes；
- `.DisableHtml()`。

此外还包括 standalone Markdown image 归一化、standalone `<img>` paragraph 去除、TOC
fenced-code heading 排除和重复 heading id 后缀规则。

Task 10 只能改变 type accessibility，不能：

- 改 Markdig extensions 或顺序；
- 改 raw HTML escaping；
- 改 image normalization；
- 改 heading slug/TOC；
- 把 access-level task 扩张成 URL scheme sanitizer。

对外实际契约是 Markdown provider/CLI 生成的 HTML 和 TOC，不是 helper CLR identity。

### 7.4 Reflection、serialization 与 AOT

- 未发现以候选 full name 进行 runtime reflection；
- 没有 serializer attribute、Json context、DI/reflection factory 或 source generator root；
- `RegexOptions.Compiled` 是 helper 内部实现，不是 public CLR identity 的反射契约；
- published Markdown build 会静态触达 `Convert` 和 `ExtractTableOfContents`；
- internalize 理论上不改变 Native AOT reachability，但 Task 10 仍必须实际验证发布产物。

### 7.5 资格结论

**Task 10 internalize。** 只允许 `public static class` → `internal static class`。HTML 和
TOC 行为必须由现有测试加最小安全 characterization 守住。

## 8. `MarkdownBodyStore`

### 8.1 当前公共面

```csharp
public sealed class MarkdownBodyStore : IContentBodyStore
{
    public MarkdownBodyStore();

    public Task<ContentBody> GetAsync(
        ContentDocument document,
        CancellationToken cancellationToken = default);
}
```

类型是 `sealed`，没有 protected member。

### 8.2 消费者与构造传播

production 唯一直接构造位于同程序集 `MarkdownFolderProvider.LoadRawAsync`，随后只以
`RawContentLoadResult.BodyStore : IContentBodyStore` 返回。测试直接构造位于
`Bukit.Content.Tests`，已有精确 IVT。

`guide/dev/content.md` 和 `guide/user/05-markdown-content.md` 用类型名解释 lazy body
实现，没有提供 CLR SDK 构造契约。internalize 后 lazy rendering 行为不变。

### 8.3 必须保持的行为

顺序为：

1. cancellation；
2. non-whitespace inline HTML；
3. nonblank `BodyKey`；
4. async file read、front-matter removal 和 Markdown conversion；
5. 返回新的 `ContentBody`。

Task 10 不得修改 `IsNullOrWhiteSpace`、文件异常、cancellation、front matter 或渲染
pipeline。需要特别保留一个现状差异：Composite/Dictionary 对 inline HTML 使用
`IsNullOrEmpty`，Markdown 使用 `IsNullOrWhiteSpace`。access-level 任务不能顺手统一。

行为替代边界仍是 `IContentBodyStore` 和 `MarkdownFolderProvider`。

### 8.4 Reflection、serialization 与 AOT

- 无 runtime reflection、serialization 或 source-generator binding；
- 无外部继承扩展点；
- published Markdown build 静态构造并触达它；
- Task 10 必须通过真实 published CLI Markdown fixture 证明 internalize 后可达性不变。

### 8.5 资格结论

**Task 10 internalize。** 只改变 type accessibility，不迁移 file I/O，不引入 facade。

## 9. `NotionClientStats`

### 9.1 当前公共面和 canonical duplicate

legacy：

```csharp
public sealed record Bukit.Content.Notion.NotionClientStats(
    long RequestCount,
    long ThrottleWaitCount,
    long ThrottleWaitTotalMs);
```

canonical：

```csharp
public sealed record Bukit.Notion.Transport.NotionClientStats(
    long RequestCount,
    long ThrottleWaitCount,
    long ThrottleWaitTotalMs);
```

两者字段同形，但 assembly/namespace identity 不同。

### 9.2 消费者与传播

```text
Bukit.Content.Notion.NotionApiClient.GetStats() [internal]
  -> _client.GetStats() [canonical transport stats]
  -> new legacy Content.Notion.NotionClientStats(...)
```

legacy `GetStats()` 是 internal；retained public member 没有返回 legacy stats。
`Bukit.Content.Notion.NotionContentClient` 已直接使用 canonical stats。运行时消费者只读
三个计数并用于日志。

仓库内还有两类刻意的 compatibility roots：

- `tests/Bukit.Content.Tests/LegacyNotionConsumerFixture.cs` 的 `typeof(...)`；
- `tests/Bukit.Architecture.Tests/NotionBoundaryTests.cs` 的 exact full-name 守卫。

它们证明旧 identity 目前仍受治理，不是产品 runtime reflection root，也不能在 Task 10
被静默删除。

### 9.3 行为替代、reflection/serialization 与 AOT

canonical transport record 是明确替代 owner。Task 11 可考虑删除 duplicate legacy
record，并让 internal `NotionApiClient.GetStats()` 直接返回 canonical stats，但必须同时
验证：

- `RequestCount`、`ThrottleWaitCount`、`ThrottleWaitTotalMs` 含义不变；
- retry/throttle 行为不变；
- record value/equality 预期不意外扩张；
- owned/injected HttpClient 的 lifetime/disposal 不变；
- legacy fixture、boundary 和 baseline 精确迁移。

没有发现 legacy stats 的 JSON attribute、serializer context、reflection factory、
source-generator 或 product AOT registration。test `typeof` 和 full-name string 是
governance roots，不是运行时序列化证据。

### 9.4 资格结论

**严格留给 Task 11 G-04D3B canonical facade migration。** Task 10 必须用 architecture
assertion 证明 legacy `NotionClientStats` 仍 public/exported，防止跨图误删。

若 Task 11 前出现 direct CLR consumer、private declaration 或 serializer/reflection
root，应停止删除，改为 retained 或独立 obsolete window；不得回头把它混入 D3A。

## 10. 外部消费者证据与限制

### 10.1 已有认证历史证据

2026-07-22 关闭的认证搜索 manifest 对五项 full name 均记录
`no-public-match-found`：

- `CompositeContentBodyStore`：full/simple 0/0；
- `DictionaryContentBodyStore`：0/0；
- `BasicMarkdownToHtml`：full 0；simple-name 结果经审阅为 lexical false positives；
- `MarkdownBodyStore`：0/0；
- `NotionClientStats`：0/0。

历史 manifest 的 136 entries 是声明窗口关闭时的不可变 cohort。Task 10/11 都只能更新
current baseline 和新 ledger，不能重写历史证据。

### 10.2 2026-07-23 刷新限制

- GitHub Issue #60 为 closed/completed，已有声明没有提供五项 direct CLR 使用；
- 当前环境没有 `gh`；
- unauthenticated GitHub Code Search API 返回认证要求，未形成新的
  governance-grade authenticated search snapshot；
- 普通网页 exact search 没有发现可信匹配，但不能替代 authenticated GitHub Code
  Search；
- private、unindexed、undisclosed consumers 始终不可观察。

因此本报告只能得出：

> 当前没有新的已确认 direct CLR consumer 证据；私人消费者状态仍为 unknown。

不得把它改写成“没有消费者”。四项 internalize 和未来 stats migration 都是 2.0-only
source/binary breaking decision；资格成立不消除该兼容性事实。

## 11. 测试盘点与 Task 10 必须补齐的证据

### 11.1 已有 fallback 和 identity 覆盖

现有 Content tests 已覆盖：

- Dictionary：inline precedence、BodyKey 命中、null/empty/missing、cancellation、命中
  返回同一个 `ContentBody`；
- Composite：inline 不 dispatch、source prefix dispatch、missing source/prefix、
  sourceId 基础路径；
- Markdown：inline、null/blank BodyKey、missing file、cancellation；
- Basic Markdown：raw HTML escaping、image attribute escaping、code、table、task list、
  autolink、emoji、footnote；
- decorators：部分 async disposal forwarding/ownership；
- basic Markdown release fixture：CLI config/build/audit 主路径。

这些测试必须作为 Task 10 组级回归运行，不能通过删除 direct-construction tests 来绕过
internal accessibility。

### 11.2 缺口一：`sourceId` + `BodyKey` 联合重写

当前 sourceId 测试把 `BodyKey` 直接设为 `actual-id`，没有证明真实 composite key 的
`sourceKey:` 前缀被正确剥离，也没有同时断言 inner document identity。

Task 10 必须添加 recording inner store characterization：

```text
input:
  Id       = "md:projected-id"
  sourceId = "actual-id"
  BodyKey  = "md:actual-body-key"

inner receives:
  Id       = "actual-id"
  BodyKey  = "actual-body-key"
  remaining document fields unchanged

result:
  exact same ContentBody instance returned by inner store
```

这是 Task 10 必须补齐的高价值行为断言。不得借此改变 prefix case comparison 或重写其他
字段。

### 11.3 缺口二：危险 URL scheme

`.DisableHtml()` 已证明 raw HTML 不直接通过，但现有 tests 没有固定 Markdown link/image
对以下 scheme 的当前处理：

- `javascript:`
- `data:text/html`
- `vbscript:`

Task 10 必须添加**当前行为 characterization**，并明确安全契约究竟只承诺 raw HTML
disabled，还是还承诺 URL protocol sanitization。

若 characterization 暴露未满足的安全预期，Task 10 必须停止该安全修复并建立独立安全
任务；不得在 access modifier commit 中顺带修改 Markdig pipeline、URL policy 或输出
HTML。internalize 可以继续与否，应由独立风险判断和 Task 10 ledger 明确记录。

### 11.4 缺口三：async disposal owner test

已有：

- `BodyCacheDecoratorTests.DisposeAsync_ForwardsToInnerStoreExactlyOnce`；
- `LocalizedContentBodyStoreTests` 的 owned localizer/inner/same-instance disposal；
- `Bukit.Engine.Tests.SiteEngineBodyStoreLifetimeTests` 的成功、异常、取消路径最终 body
  store exactly-once disposal。

但 master plan Task 10 明列的四个 group test projects 不含 `Bukit.Engine.Tests`。仅运行
这些命令不足以声称 async disposal 已验证。

Task 10 必须二选一并在 ledger 给出确证：

1. 运行 owner test `Bukit.Engine.Tests.SiteEngineBodyStoreLifetimeTests` 的 focused
   test；或
2. 证明本组唯一 aggregate targeted gate 实际选择并通过该 owner test。

`CompositeContentBodyStore` 当前不实现 `IDisposable/IAsyncDisposable`，Task 10 不得新增
child cascade-disposal 行为。未来若 child store ownership 需要变化，必须另立 lifetime
任务。

### 11.5 架构断言

Task 10 新增的 `G04D3AContentBodyGraphTests` 至少应断言：

1. 四个 exact full names 不再 export；
2. 四个类型仍能从 `Bukit.Content` resolve，且 `IsNotPublic`；
3. `Bukit.Content.Notion.NotionClientStats` 仍 public/exported；
4. current baseline 只移除四项，其他类型/member 不漂移；
5. 136-entry historical manifest 内容和 Git blob 不变；
6. 既有 source-level friendship 精确保持
   `Bukit.Content.Tests`、`Bukit.Engine`、`Bukit.Engine.Tests`，且没有新增
   production friendship；
7. public API 总数和 candidate 总数按最终 G1 aggregate 的真实 PluginHost delta 计算，
   不提前硬编码本报告调查时的中间数。

## 12. Task 10 最小实施合同

### 12.1 唯一允许的 production diff

```text
CompositeContentBodyStore : public -> internal
DictionaryContentBodyStore: public -> internal
BasicMarkdownToHtml        : public -> internal
MarkdownBodyStore          : public -> internal
```

除此之外，不允许：

- 删除类型或移动 namespace/assembly；
- 改 constructor/member signature；
- 改 fallback、exception、cancellation、case comparison、body identity；
- 改 Markdig pipeline、HTML、TOC、URL scheme policy；
- 改 file I/O、front matter 或 disposal ownership；
- 改 stats、Notion transport、schema、配置、插件协议、媒体或 SEO；
- 新增 public facade 或 production IVT。

### 12.2 必须更新

- current public API baseline；
- Task 10 architecture tests；
- 上述三个缺口的最小行为/owner test 证据；
- G-04D3A resolution ledger；
- G1 aggregate 组级验证状态。

活动指南可以继续描述 provider 内部使用的 store 实现；若 Task 10 为避免把 internal CLR
identity 误读为公共 SDK 而调整措辞，只能做事实性文字澄清，不能改变用户配置或行为
承诺。

### 12.3 组级验证

Task 10 负责一次性验证 G1 当前整合终态，包括：

- `Bukit.PluginHost.Tests`
- `Bukit.Content.Tests`
- `Bukit.Cli.Tests`
- `Bukit.Architecture.Tests`
- async disposal owner test 的明确证据
- public API drift
- G1 `GROUP_BASE..HEAD` 唯一 aggregate targeted gate
- Native AOT build 和 published artifact smoke
- `git diff --check`
- 一次轻量只读复审

本报告没有运行上述任何命令，全部状态仍为
`group-verification-pending`。

## 13. 风险、兼容性与停止条件

| 风险 | 当前判断 | 控制 |
|---|---|---|
| direct CLR consumer source/binary break | 公开证据无确认命中；private unknown | 2.0-only；保留历史 manifest；明确 breaking |
| public signature propagation | 四项未传播；stats 单独处理 | architecture exact-name/signature tests |
| test consumer 编译失败 | 已有精确 source-level test IVT | 不复制到 csproj；不新增 production friend；组级完整编译 |
| Markdown 行为漂移 | access change 本身不改行为 | 全套 owner tests + 危险 scheme characterization |
| body identity/fallback 漂移 | sourceId + BodyKey 联合证据不足 | Task 10 必须补 recording-store test |
| async disposal 误报通过 | Engine owner test 不在四个明列项目中 | 明确运行或证明 targeted gate 选择 |
| Native AOT trimming | Markdown 主路径静态可达 | published CLI Markdown fixture |
| stats 被跨图误删 | duplicate 看似简单但 owner/lifetime 不同 | Task 10 明确断言仍 public；Task 11 独立迁移 |

出现以下任一新证据时，停止对应收窄并重新资格审计：

- 新 direct CLR 或 private consumer declaration；
- public/protected signature propagation；
- reflection、serialization、source generation 或 AOT identity root；
- 需要新增 production friendship；
- 需要修改 schema、配置、协议或运行时行为才能 internalize；
- Task 10 characterization 暴露必须先处置的安全或 lifetime 回归。

停止后可选择 retained-by-design、obsolete window 或独立 facade migration；不能通过扩大
Task 10 来绕过停止条件。

## 14. 正式决策台账

| # | 类型 | Task 9 决策 | 下一任务 | 当前关闭状态 |
|---:|---|---|---|---|
| 1 | `Bukit.Content.CompositeContentBodyStore` | eligible for internalization | Task 10 / G-04D3A | `group-verification-pending` |
| 2 | `Bukit.Content.DictionaryContentBodyStore` | eligible for internalization | Task 10 / G-04D3A | `group-verification-pending` |
| 3 | `Bukit.Content.Markdown.BasicMarkdownToHtml` | eligible for internalization | Task 10 / G-04D3A | `group-verification-pending` |
| 4 | `Bukit.Content.Markdown.MarkdownBodyStore` | eligible for internalization | Task 10 / G-04D3A | `group-verification-pending` |
| 5 | `Bukit.Content.Notion.NotionClientStats` | separate canonical facade migration | Task 11 / G-04D3B | not in G1 implementation |

Task 9 至此完成资格分类。它没有授权把任一类型标记为已修复或已关闭；四个 D3A 类型只有
在 Task 10 完成最小源码修改、证据补齐、G1 组级完整验证和轻量复审后才能申请关闭。
`NotionClientStats` 只有在 Task 11 及 G2 组级验证完成后才能申请关闭。
