# Bukit Core G-04D1C-M1：Canonical Notion 扩展图迁移契约

日期：2026-07-23
任务基线：`96cfee5ccce820daec21f996a7ca280bf27d1fa8`
状态：provisional（Task 3 focused verification、parent aggregate、四项目 Release test 与独立复审待 parent controller 记录）

## 1. 范围、结论与不可变边界

本指南给使用 `Bukit.Content.Notion` 五类型扩展图的源码消费者一条明确迁移路径，
目标是 canonical `Bukit.Notion.Rendering` 与 `Bukit.Notion.Transport`。它同时记录
M1 的 source、binary、client、exception、request semantics 与 lifetime contract；
它不是删除批准，也不把历史公开检索阴性结果改写成“没有消费者”。

M1 保留五个 legacy CLR 类型；M1 不授权 M2。

M2 必须另行取得 deliberate public API approval，并把五个 legacy CLR identity 作为原子批次处理。

M1 的不可变边界如下：

- 不删除、internalize、重命名或 obsolete 五个 legacy 类型，不修改其 public signature；
- 不修改 legacy implementation，不把已知 shared-registry split-brain 行为当成 canonical
  必须保留的兼容语义，也不在本任务顺带修复 legacy 实现；
- 不修改 public API baseline、闭合 manifest、transport/retry、exception、schema、plugin
  protocol、CLI、config、URL/path/report contract、CI、release 或 gate；
- 当前 baseline 保持 **14 个程序集、514 个类型、110 个 `2.0-candidate`**，并继续包含
  五个 legacy identity；
- 闭合的 **136-entry candidate manifest** 保持字节不变，Git blob 为
  `7b07d6890562387010b52301e9f8716e9bf10ed1`；
- canonical `Bukit.Notion.csproj` 保持 0 `ProjectReference` / 0 `PackageReference`，不得为
  兼容 `ContentException` 重新依赖 `Bukit.Content` 或 `Bukit.Shared`。

## 2. CLR identity 与程序集映射

迁移不是 assembly 内重命名；每项的 namespace 与 assembly identity 都改变：

| legacy assembly / CLR identity | canonical assembly / CLR identity |
|---|---|
| `Bukit.Content` / `Bukit.Content.Notion.INotionBlockRenderer` | `Bukit.Notion` / `Bukit.Notion.Rendering.INotionBlockRenderer` |
| `Bukit.Content` / `Bukit.Content.Notion.NotionBlockTransformer` | `Bukit.Notion` / `Bukit.Notion.Rendering.NotionBlockTransformer` |
| `Bukit.Content` / `Bukit.Content.Notion.NotionBlockRendererRegistry` | `Bukit.Notion` / `Bukit.Notion.Rendering.NotionBlockRendererRegistry` |
| `Bukit.Content` / `Bukit.Content.Notion.NotionRenderContext` | `Bukit.Notion` / `Bukit.Notion.Rendering.NotionRenderContext` |
| `Bukit.Content` / `Bukit.Content.Notion.NotionBlocksRenderer` | `Bukit.Notion` / `Bukit.Notion.Rendering.NotionBlocksRenderer` |

前三项的成员形状在 namespace 规范化后接近相同，但 interface/delegate 的参数仍暴露
不同的 context CLR identity。context 的 `Client` 从 `NotionApiClient` 变为
`NotionClient`，renderer constructor 也随之改变。因此这是明确的 **source break**；
已有程序集中的 type/member reference 指向旧 assembly-qualified identity，不能只替换 DLL，
所以也是 **binary break**，消费者必须更新引用并重新编译。

标准 CLR **type forwarding** 只能把同一 full name 转发到另一程序集，不能把
`Bukit.Content.Notion.*` 转发成 `Bukit.Notion.Rendering.*`。保留旧 full name 的
facade/adapter 仍会保留待收窄 public surface，因而不存在用 type forwarding 完成
这次 namespace 迁移的路径。

## 3. 可编译的 old/new 源码迁移

以下两个 compilation unit 分别只使用各自 public extension graph。它们同时覆盖
interface implementation、transformer delegate、registry、context、renderer construction
与 client lifetime。示例中的 renderer 通过 `NotionRenderContext context` 调用
`RenderChildrenAsync`；如果 callback 还直接调用 `context.Client`，必须继续按第 5–7 节
迁移 client 与异常语义。

### 3.1 Old：legacy `Bukit.Content.Notion`

```csharp
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bukit.Content.Notion;

public sealed class LegacyCustomRenderer : INotionBlockRenderer
{
    public async Task<string?> RenderAsync(
        JsonElement block,
        NotionRenderContext context,
        CancellationToken cancellationToken)
    {
        var blockId = block.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("The block id is required.");
        return await context.RenderChildrenAsync(blockId, cancellationToken);
    }
}

public static class LegacyRenderingEntryPoint
{
    public static async Task<string> RenderAsync(
        NotionProviderOptions options,
        string pageId,
        CancellationToken cancellationToken)
    {
        using var client = new NotionApiClient(options);
        var registry = NotionBlockRendererRegistry.CreateDefault()
            .Register("custom_parent", new LegacyCustomRenderer());
        NotionBlockTransformer transformer =
            static (block, context, token) => Task.FromResult<string?>(null);
        registry.SetCustomTransformer("paragraph", transformer);
        var renderer = new NotionBlocksRenderer(client, registry);
        return await renderer.RenderPageAsync(pageId, cancellationToken);
    }
}
```

这里的 `NotionRenderContext.Client` 编译期类型是
`Bukit.Content.Notion.NotionApiClient`。transformer 返回 `null` 表示继续使用 registry
中的 built-in renderer；重复 `Register` 覆盖同 block type，
`RemoveCustomTransformer` 恢复直接使用 renderer，未知 block type 返回 `null` 并被跳过。

### 3.2 New：canonical `Bukit.Notion`

```csharp
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;

public sealed class CanonicalCustomRenderer : INotionBlockRenderer
{
    public async Task<string?> RenderAsync(
        JsonElement block,
        NotionRenderContext context,
        CancellationToken cancellationToken)
    {
        var blockId = block.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("The block id is required.");
        return await context.RenderChildrenAsync(blockId, cancellationToken);
    }
}

public static class CanonicalRenderingEntryPoint
{
    public static async Task<string> RenderAsync(
        NotionClientOptions options,
        string pageId,
        CancellationToken cancellationToken)
    {
        using var client = new NotionClient(options);
        var registry = NotionBlockRendererRegistry.CreateDefault()
            .Register("custom_parent", new CanonicalCustomRenderer());
        NotionBlockTransformer transformer =
            static (block, context, token) => Task.FromResult<string?>(null);
        registry.SetCustomTransformer("paragraph", transformer);
        var renderer = new NotionBlocksRenderer(client, registry);
        return await renderer.RenderPageAsync(pageId, cancellationToken);
    }
}
```

这里的 `NotionRenderContext.Client` 编译期类型是
`Bukit.Notion.Transport.NotionClient`。canonical `Register(..., null)` 与
`SetCustomTransformer(..., null)` 在调用点分别以参数名 `renderer` 和 `transformer`
抛出 `ArgumentNullException`；override、duplicate replace、transformer-null fallback、
remove 与 unknown 行为保持上述契约。

## 4. Options 显式映射

`NotionApiClient` 的 legacy mapping 是 internal，外部 consumer 无法从已有 legacy client
提取或复用其 canonical transport。迁移时必须新建 `NotionClient`，不要假设存在
`NotionApiClient -> NotionClient` conversion。

| `NotionProviderOptions` / legacy 来源 | `NotionClientOptions` | 迁移规则 |
|---|---|---|
| `Token` | `Token` | 必须显式复制；示例不得记录真实 token |
| 固定 `NotionApiUrls.NotionVersion` | `ApiVersion` | 显式写出固定 API version，不依赖“恰好相同”的默认值 |
| `RequestDelayMs` | `RequestDelayMs` | 一对一 |
| `MaxRetries` | `MaxRetries` | 一对一；只对 `IdempotentRead` 生效 |
| `MaxRps` | `MaxRps` | 一对一；throttle state 按 client instance 隔离 |
| legacy 间接默认 | `Timeout` | 显式采用 30 秒 |

下列 migration-time helper 在同时引用 legacy 与 canonical assemblies 的迁移项目中可编译：

```csharp
using System;
using Bukit.Content.Notion;
using Bukit.Notion;
using Bukit.Notion.Transport;

public static class NotionOptionsMigration
{
    public static NotionClientOptions Map(NotionProviderOptions legacyOptions)
        => new()
        {
            Token = legacyOptions.Token,
            ApiVersion = NotionApiUrls.NotionVersion,
            Timeout = TimeSpan.FromSeconds(30),
            RequestDelayMs = legacyOptions.RequestDelayMs,
            MaxRetries = legacyOptions.MaxRetries,
            MaxRps = legacyOptions.MaxRps
        };
}
```

`DatabaseId`、`PageSize`、`MaxItems`、`RenderConcurrency`、字段白名单/筛选/排序、
`RenderContent`、slug、cache、property map 与 auto-summary 都是 database/content
projection options，**没有 transport mapping**。page/block/database id 由实际 request 或
renderer 调用参数提供；不能为了“字段齐全”把这些选项复制进 transport。

如果迁移期间同时保留 legacy content client 与 canonical renderer client，两者拥有独立的
timeout、throttle、stats 与 disposal scope。它们不是同一个 client，也不得把其中一方的
计数或生命周期解释成另一方的状态。

## 5. Context、callback、nested rendering 与 shared registry

Canonical callback contract 是：

- `INotionBlockRenderer` 与 `NotionBlockTransformer` 收到触发该回调的原
  `JsonElement`、caller 的原 `CancellationToken`，以及构造该 renderer 的**同一个**
  `NotionClient`；
- `context.RenderChildrenAsync(blockId, token)` 继续使用该 renderer/client，保留分页、
  nested rendering 与 token propagation；callback 不得换用另一个全局 client；
- transformer non-null 结果覆盖 built-in renderer；返回 `null` 才 fallback；remove 后不再
  执行 transformer；unknown block 没有 renderer 时返回 `null`；
- 两个 `NotionBlocksRenderer` 可以共享一个 canonical registry。canonical registry 不保存
  可变 client binding，renderer A 的 callback context 必须是 client A，renderer B 必须是
  client B；构造 B 不得改变 A 的 context identity。

Legacy shared registry 的单一可变 client binding 会被后构造 renderer 覆盖，可能导致
renderer A 的 nested rendering 使用 A、公开 `context.Client` 却指向 B。Canonical contract
明确**不保留**这个 split-brain 缺陷。M1 不修改 legacy implementation；若 1.x 仍需修复，
必须另立窄任务。

取消必须贯穿 page request、block loop、callback 与 nested request。caller cancellation
原样传播为 `OperationCanceledException`，并保留原 token；不能翻译为
`ContentException` 或 `NotionApiException`。

Legacy `TranslateAsync` 会包装 custom callback 直接抛出的 `NotionRenderingException` 和 `NotionApiException`；只有其他 consumer-defined exception 原样传播。Canonical renderer 不执行该翻译，这三类 callback exception 都直接传播。

## 6. 完整 old/new exception matrix

| 场景 | legacy public surface | canonical public surface | consumer 动作 |
|---|---|---|---|
| response 缺少 array `results` | `ContentException`，inner 为 `NotionRenderingException` | 直接 `NotionRenderingException` | 更新 catch type；可继续检查 message，但不可只依赖 message |
| 非成功 HTTP status | `ContentException`，inner 为 `NotionApiException(HttpStatus)` | 直接 `NotionApiException(HttpStatus)` | 检查 `Kind`/status，而不是 inner unwrap |
| terminal 429 | `ContentException`，inner 为 `NotionApiException(RateLimited)` | 直接 `NotionApiException(RateLimited)` | retry 次数由 request semantics 与 options 决定 |
| invalid JSON | `ContentException`，inner 为 `NotionApiException(InvalidJson)` | 直接 `NotionApiException(InvalidJson)` | 使用结构化 `Kind` |
| transport failure / 非 caller timeout cancellation | `ContentException`，inner 为 `NotionApiException(Transport)` | 直接 `NotionApiException(Transport)` | 使用 `Kind` 与 `RootErrorType` |
| custom callback 抛出 `NotionRenderingException` | `ContentException`，inner 为原 `NotionRenderingException` | 原 `NotionRenderingException` | legacy unwrap inner；canonical 直接 catch |
| custom callback 抛出 `NotionApiException` | `ContentException`，inner 为原 `NotionApiException` | 原 `NotionApiException` | legacy unwrap inner；canonical 直接 catch |
| custom callback 抛出其他 consumer-defined exception | 原异常，不包装 | 原异常，不包装 | 两侧按 consumer 自有类型处理 |
| caller cancellation | `OperationCanceledException`，原 token | `OperationCanceledException`，原 token | 原样重新抛出 |

### 6.1 Old catch 示例

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Bukit.Shared;

public static class LegacyExceptionBoundary
{
    public static async Task<string> RenderAsync(
        Func<Task<string>> renderAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            return await renderAsync();
        }
        catch (OperationCanceledException exception)
            when (exception.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (ContentException exception)
            when (exception.InnerException is NotionRenderingException)
        {
            throw;
        }
        catch (ContentException exception)
            when (exception.InnerException is NotionApiException)
        {
            throw;
        }
        catch (ConsumerCallbackException)
        {
            throw;
        }
    }
}

public sealed class ConsumerCallbackException(string message) : Exception(message);
```

### 6.2 New catch 示例

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;

public static class CanonicalExceptionBoundary
{
    public static async Task<string> RenderAsync(
        Func<Task<string>> renderAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            return await renderAsync();
        }
        catch (OperationCanceledException exception)
            when (exception.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (NotionRenderingException)
        {
            throw;
        }
        catch (NotionApiException)
        {
            throw;
        }
        catch (ConsumerCallbackException)
        {
            throw;
        }
    }
}

public sealed class ConsumerCallbackException(string message) : Exception(message);
```

不要为了保留旧 catch 形状在 canonical owner 重建 `ContentException` wrapper；这会反向
引入 Content dependency，并抹掉 2.0 migration contract 的结构化异常边界。

## 7. Read/write request semantics

Legacy `PostAsync(url, json, token)` 根据 URL 自动猜测 database query 与 write；canonical
没有同形 facade。consumer 必须创建 `HttpRequestMessage` 并显式选择 semantics：

```csharp
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bukit.Notion;
using Bukit.Notion.Transport;

public static class ExplicitNotionRequests
{
    public static async Task SendAsync(
        NotionClient client,
        string databaseId,
        string pageId,
        CancellationToken cancellationToken)
    {
        using var query = new HttpRequestMessage(
            HttpMethod.Post,
            NotionApiUrls.DatabaseQuery(databaseId))
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        using var queryResult = await client.SendAsync(
            query,
            NotionRequestSemantics.IdempotentRead,
            cancellationToken);

        using var write = new HttpRequestMessage(
            HttpMethod.Patch,
            NotionApiUrls.Pages(pageId))
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        using var writeResult = await client.SendAsync(
            write,
            NotionRequestSemantics.NonReplayableWrite,
            cancellationToken);
    }
}
```

Database query 是可安全重放的 read operation，即便 HTTP verb 为 POST，也用
`NotionRequestSemantics.IdempotentRead`，因此可以按 `MaxRetries` 处理 429。真正的
create/update/append/write 必须使用 `NotionRequestSemantics.NonReplayableWrite`，终端
429 后不自动重放。错误地把 write 标成 idempotent 可能造成重复写；把 query 全标成
non-replayable 则会丢失既定 read retry。M1/M2 都不得由 facade 猜测或改写此选择。

## 8. Ownership 与 disposal

共同规则是 **renderer 不拥有 client**。`NotionBlocksRenderer` 没有转移 client ownership；
renderer 完成或离开作用域不会替 consumer dispose client，consumer 仍可在 renderer 调用后
继续使用同一 client。

- `new NotionClient(options)`：internally-created `HttpClient` 由 `NotionClient` 拥有；
  consumer 必须 dispose `NotionClient`，由它恰好一次释放内部 HTTP owner；
- `new NotionClient(options, injectedHttpClient)`：injected `HttpClient` 仍由 caller 拥有；
  dispose `NotionClient` 不 dispose injected client，caller 在自己的外层 scope 释放它；
- legacy `NotionApiClient` 同样由 consumer dispose，legacy renderer 不接管它；
- 同时保留 legacy/canonical 两个 client 时分别 dispose，不能用释放其中一个代替另一个。

推荐的 canonical lifetime 已体现在第 3.2 节：`using var client = new NotionClient(options)`
的 scope 包住 renderer 的全部使用。注入 HTTP 时则使用两个清楚分离的 scope：

```csharp
using System.Net.Http;
using Bukit.Notion.Transport;

public static class InjectedHttpOwnership
{
    public static void Use(NotionClientOptions options, HttpMessageHandler handler)
    {
        using var injectedHttpClient = new HttpClient(handler);
        using (var client = new NotionClient(options, injectedHttpClient))
        {
            _ = client.GetStats();
        }

        _ = injectedHttpClient.DefaultRequestHeaders;
    }
}
```

## 9. Consumer evidence 与新证据回退规则

闭合 manifest 对五项保存的是历史 `no-public-match-found`、
`consumer-declaration-pending` 与 `unknown-until-voluntary-declaration`。它只说明固定
搜索时点没有确认公开匹配；私有、未索引、未披露、reflection/serialization/AOT 或 binary
plugin consumer 仍未知。这些历史字段不得被覆盖成“无消费者证明”。

### 新证据回退规则

在任何 M2 删除提交前，出现下列任一具体证据就停止直接删除，回到 retain/obsolete window
与迁移时限评估：

- 可识别程序集实现 legacy `INotionBlockRenderer`，或 public/protected signature 暴露
  任一 legacy identity；
- delegate、reflection、serialization、Native AOT、source generator 或 binary plugin
  绑定旧 full name/member signature；
- custom callback 使用 `context.Client.PostAsync`，但不能安全区分 database-query read 与
  non-replayable write；
- consumer 无法在 2.0 窗口内迁移 canonical client/typed exceptions；
- 同一 binary plugin 必须同时在 1.x 与 2.0 运行。

普通 CLI、配置、主题、HTML output 或 process plugin 使用不构成 CLR consumer 证据，除非
同时提供具体 assembly/type/member dependency。新证据由独立 public API 决策处理，不能
回写或改造闭合 manifest 的历史字节。

## 10. M1 verification ledger（provisional）

本节只记录已经发生的 Task 3 RED 与尚待 controller 汇总的项目，不预写 PASS：

| 项目 | 当前记录 |
|---|---|
| Task 3 RED | 定向 architecture run：5 total，4 passed；唯一 1 failed 是本指南缺失 |
| Task 3 targeted GREEN | 定向 architecture run：5 passed / 0 failed / 0 skipped |
| 五组 old/new public identity | RED run 中独立 guard 已解析；最终 M1 结论仍待 parent 汇总 |
| governed baseline | RED run 中 guard 读取 14 / 514 / 110；未修改 baseline |
| candidate manifest | RED run 中 guard 读取 136 entries 与固定 blob；未修改 manifest |
| canonical no-dependency | RED run 中 guard 读取 0 project/package references |
| Task 3 focused verification | 本指南保持 provisional，不预写结果；Task 3 实施报告记录实际运行，parent controller 后续汇总 |
| parent aggregate | 待 parent 从指定 base 且仅执行一次；本指南不声称通过 |
| 四个相关 Release test projects / public API drift | 待 parent completion verification |
| independent whole-branch review | 待 parent dispatch；本指南不声称通过 |

若 parent aggregate 重现未变更 `brainstorm-server-self-test` 的
`mv-1 left a live spawned server`，controller 必须记录精确 blocker，不能把它改写为
M1 回归、不能顺带修复或声称 aggregate 已通过。

M1 只有在适用 fixtures、public API drift、focused/parent aggregate 与独立复审都按各自
边界形成真实证据后才能由 controller 关闭。即使 M1 最终关闭，也只证明 migration
contract 已建立；它仍不自动授权 M2。
