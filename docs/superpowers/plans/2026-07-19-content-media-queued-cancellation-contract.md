# Content Media Queued Cancellation Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 当媒体本地化任务在等待 download gate 期间被取消时，任务取得 permit 后必须先传播取消，不得进入 `IImageAssetLocalizer`，同时保证 permit 不泄漏且 F-07 下载并发上限不回归。

**Architecture:** 在现有 `ContentImageRewritePipeline.LocalizeWithGateAsync` 的副作用准入边界实施双重取消检查：`SemaphoreSlim.WaitAsync` 负责取消仍在队列中的 waiter，成功取得 permit 后的 `ThrowIfCancellationRequested` 负责处理 release/cancel 竞争中由 release 先完成的 waiter。检查必须位于 `try/finally` 内，使取消异常和 localizer 的任何完成路径都恰好释放一次 permit。

**Tech Stack:** C# 13、.NET 10、xUnit、`SemaphoreSlim`、仓库 `post-change-targeted.sh` 门禁。

## Global Constraints

- 本任务是 F-07 后续独立取消契约修复，不重新设计下载并发架构。
- 只允许修改 `src/Bukit-Core/Bukit.Content/Media/ContentImageRewritePipeline.cs` 和 `tests/Bukit.Content.Tests/ContentImageRewritePipelineTests.cs`。
- 不修改 `ImageAssetLocalizer`、`IImageAssetLocalizer`、配置 schema、插件协议、HTTP/TLS、重试、SSRF、缓存、索引、URL 去重或媒体输出规则。
- 正式契约只保证：排队项取得 permit 后若已经观察到取消，则不进入 localizer；不承诺调用 `Cancel()` 后任何线程都不再执行代码的绝对时间语义。
- 保持 `SemaphoreSlim.WaitAsync(cancellationToken)`，且只有成功取得 permit 的路径才能执行一次 `Release()`。
- 不运行 full、release、`test-all`、`smoke-all` 或整个解决方案门禁。
- 因涉及并发和取消语义，targeted gate 通过后必须执行一次限定范围的只读代码审计；当前执行环境不使用子代理时，由主执行者完成同等审计并明确记录该限制。

---

## File Map

- `src/Bukit-Core/Bukit.Content/Media/ContentImageRewritePipeline.cs`：在 download gate 成功准入后增加取消检查，不改变 gate 生命周期或调用拓扑。
- `tests/Bukit.Content.Tests/ContentImageRewritePipelineTests.cs`：保留严格的“取消排队项不进入 localizer”断言，补强测试命名与最大活动并发观测。

### Task 1: Enforce queued-download cancellation at the gate boundary

**Files:**
- Modify: `src/Bukit-Core/Bukit.Content/Media/ContentImageRewritePipeline.cs:325-339`
- Test: `tests/Bukit.Content.Tests/ContentImageRewritePipelineTests.cs:383-407`
- Test helper: `tests/Bukit.Content.Tests/ContentImageRewritePipelineTests.cs:808-835`

**Interfaces:**
- Consumes: `IImageAssetLocalizer.LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)` 和 operation-local/shared `SemaphoreSlim downloadGate`。
- Produces: `ContentImageRewritePipeline.LocalizeWithGateAsync` 的内部契约——成功取得 permit 后若 token 已取消，则抛出 `OperationCanceledException`，且不调用 localizer。
- Public API、配置模型和返回类型均不变化。

- [ ] **Step 1: Rename and strengthen the strict cancellation test**

将测试命名改为明确描述正式契约，并增加峰值活动数断言；保留严格 `CallCount == 1`：

```csharp
[Fact]
public async Task RewriteBodyHtmlAsync_CanceledQueuedDownloadDoesNotEnterLocalizerOrHang()
{
    const string html = """
                        <img src="https://img.example/first.jpg" />
                        <img src="https://img.example/queued.jpg" />
                        """;
    var cfg = new MediaConfig
    {
        MaxConcurrency = 1,
        DefaultImageUrl = "/assets/images/noneimg-news.jpg"
    };
    var localizer = new CancellationProbeLocalizer();
    var pipeline = new ContentImageRewritePipeline(cfg, localizer);
    using var cancellation = new CancellationTokenSource();

    var rewrite = pipeline.RewriteBodyHtmlAsync(html, cancellation.Token);
    await localizer.Started.WaitAsync(TimeSpan.FromSeconds(2));
    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(
        () => rewrite.WaitAsync(TimeSpan.FromSeconds(2)));
    Assert.Equal(1, localizer.CallCount);
    Assert.Equal(1, localizer.MaxActiveCount);
    Assert.Equal(0, localizer.ActiveCount);
}
```

在 `CancellationProbeLocalizer` 中增加线程安全峰值记录：

```csharp
private int _maxActiveCount;

public int MaxActiveCount => Volatile.Read(ref _maxActiveCount);

public async Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
{
    Interlocked.Increment(ref _callCount);
    var active = Interlocked.Increment(ref _activeCount);
    ObserveMaxActiveCount(active);
    _started.TrySetResult();
    try
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return sourceUrl ?? string.Empty;
    }
    finally
    {
        Interlocked.Decrement(ref _activeCount);
    }
}

private void ObserveMaxActiveCount(int candidate)
{
    var observed = Volatile.Read(ref _maxActiveCount);
    while (candidate > observed)
    {
        var prior = Interlocked.CompareExchange(ref _maxActiveCount, candidate, observed);
        if (prior == observed)
        {
            return;
        }

        observed = prior;
    }
}
```

- [ ] **Step 2: Record the pre-fix RED evidence without treating a pass as proof**

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~RewriteBodyHtmlAsync_CanceledQueuedDownloadDoesNotEnterLocalizerOrHang'
```

预期基线：在 release/cancel 竞争由 release 获胜时，失败为 `Expected: 1, Actual: 2`；由于这是调度敏感竞态，单次通过只记录为“本轮未复现”，不得据此否定已经观察到的失败和源码根因。

- [ ] **Step 3: Add the minimal gate-boundary cancellation check**

只修改 `LocalizeWithGateAsync`，确保检查位于 `try/finally` 内：

```csharp
private async Task<string> LocalizeWithGateAsync(
    string? sourceUrl,
    SemaphoreSlim downloadGate,
    CancellationToken cancellationToken)
{
    await downloadGate.WaitAsync(cancellationToken);
    try
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _localizer.LocalizeAsync(sourceUrl, cancellationToken);
    }
    finally
    {
        downloadGate.Release();
    }
}
```

不得把检查移到 `try` 之前；否则检查抛出时已取得的 permit 不会释放。

- [ ] **Step 4: Verify the strict test repeatedly**

先运行一次精确测试：

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~RewriteBodyHtmlAsync_CanceledQueuedDownloadDoesNotEnterLocalizerOrHang'
```

预期：1/1 通过，完成时间不超过测试内两秒超时，`CallCount=1`、`MaxActiveCount=1`、`ActiveCount=0`。

再重复 30 次：

```bash
for iteration in $(seq 1 30); do
  dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~RewriteBodyHtmlAsync_CanceledQueuedDownloadDoesNotEnterLocalizerOrHang' || exit 1
done
```

预期：30/30 通过；任意一次失败都必须停止，不得扩大断言范围或删除严格调用次数断言。

- [ ] **Step 5: Run related concurrency and body-store regressions**

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ContentImageRewritePipelineTests|FullyQualifiedName~LocalizedContentBodyStoreTests'
```

预期全部通过，并确认下载峰值、localizer 异常释放、共享 gate 取消恢复、URL 顺序、memoization 和字段重写均不回归。

- [ ] **Step 6: Run the repository targeted gate with explicit paths**

```bash
bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Core/Bukit.Content/Media/ContentImageRewritePipeline.cs \
  tests/Bukit.Content.Tests/ContentImageRewritePipelineTests.cs
```

预期：targeted gate 完整通过。失败时必须区分本项回归、环境阻塞和既有无关失败；本项回归必须修复并重跑，不能进入审计或提交。

- [ ] **Step 7: Perform the bounded read-only concurrency audit**

```bash
git diff --check
git diff -- \
  src/Bukit-Core/Bukit.Content/Media/ContentImageRewritePipeline.cs \
  tests/Bukit.Content.Tests/ContentImageRewritePipelineTests.cs
```

审计必须确认：

1. `WaitAsync` 失败或取消时没有执行 `Release()`；
2. `WaitAsync` 成功后的取消检查位于 `try/finally` 内；
3. gate 后取消不会调用 `_localizer.LocalizeAsync`；
4. 非取消、localizer 成功、localizer 异常三条路径仍各自恰好释放一次 permit；
5. `RewriteAsync`、`RewriteBodyHtmlAsync` 和 `LocalizedContentBodyStore` 的 gate 生命周期没有变化；
6. 没有修改允许范围以外的 tracked 文件；
7. 测试没有依赖额外 `Task.Delay` 时长来制造调度顺序；
8. 没有把契约夸大成 `Cancel()` 后的绝对零执行保证。

若执行环境不能安排独立审查者，主执行者必须明确写明限制，并按上述清单完成同等只读审计。

- [ ] **Step 8: Commit the isolated fix**

```bash
git add \
  src/Bukit-Core/Bukit.Content/Media/ContentImageRewritePipeline.cs \
  tests/Bukit.Content.Tests/ContentImageRewritePipelineTests.cs
git commit -m "fix(content): stop canceled queued media work"
```

提交前确认 `git status --short` 只包含本任务允许的两个文件；计划文档若尚未单独提交，不得混入代码修复提交。

## Completion Criteria

- 排队项在取得 permit 后发现 token 已取消时，不进入 localizer。
- 严格取消测试稳定保持 `CallCount == 1`，并证明不挂起、峰值不超限、最终无活动调用。
- 相关 Content 测试和显式路径 `post-change-targeted.sh` 全部通过。
- 只读审计确认 permit 成对、非取消路径不变、无越界修改。
- 不把 `ImageAssetLocalizer` 入口取消响应纳入本任务；如需处理，另立 P3 任务。
