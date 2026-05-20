# P4 高影响性能修复方案

## 背景

P4 审计发现 3 个高影响性能问题，涉及多语言构建、列表页渲染和列表正文加载三条关键路径。

---

## 问题 1：多语言构建完全串行（最高影响）

### 现状

[SiteEngine.cs:L99-L126](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/SiteEngine.cs#L99-L126)：

```csharp
var results = new List<BuildVariantResult>(capacity: languages.Count);
for (var i = 0; i < languages.Count; i++)
{
    var lang = languages[i];
    // ... 准备 variantCtx ...
    var result = await BuildVariantAsync(variantCtx, templateHashCache, cancellationToken);
    results.Add(result);
}
```

每个语言变体按顺序串行构建。5 语言站点 = 5x 构建时间。`BuildVariantAsync` 包含路由生成、模板渲染、资产复制、插件执行，是全站构建中最昂贵的部分。

### 可行性分析

| 共享资源 | 线程安全性 | 说明 |
|---|---|---|
| `templateHashCache` (DirectoryHashCache) | ✅ 安全 | 底层是 `ConcurrentDictionary<string, string>` |
| `cancellationToken` | ✅ 安全 | `CancellationToken` 设计为多线程 |
| `_logger` (ConsoleLogger) | ⚠️ **不安全** | `Console.WriteLine` 本身线程安全，但日志格式可能交叠（一行日志被另一行插入） |
| `BuildVariantResult` (record) | ✅ 安全 | 不可变 record |
| 每个变体的 `outputDir` | ✅ 安全 | 不同语言写入不同输出目录（`dist/en-US/` vs `dist/zh-CN/`） |
| `seoAlternates` | ⚠️ **不安全** | `AddVariantRouteAlternates` 可能修改 dictionary |
| `BuildContext` / `PluginRunner` | ✅ 安全 | 每个变体创建独立的 context |

### 关键风险点排查

1. **`_logger` 并发**：`ConsoleLogger` 对 `Console.WriteLine` 的调用在 CLR 层面是原子的（单次 `WriteLine` 调用不会被中断），但多行日志可能交错。方案：在并行构建阶段，为每个变体创建独立的 `ConsoleLogger` 实例，收集日志，合并后输出。

2. **`AddVariantRouteAlternates`**：该方法接收 `IReadOnlyDictionary` 但构造新 dictionary 写入。需确认不会修改共享的 `seoAlternates`。

   - [SiteEngine.cs:L763-L805](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/SiteEngine.cs#L763-L805)：传入 `ctx.SeoAlternates`（原始传入的），在 `existing.ContainsKey(key)` 时跳过，否则创建新 dictionary `result ??= new Dictionary(...)` 并写入。**这是安全的** —— 每次创建新 dictionary，不修改原始。

3. **`I18nOutputMerger.GenerateRootOutputs`** 和 `SeoAuditReportWriter.WriteMerged` 依赖所有变体结果 —— 必须串行在所有变体完成后。

4. **`PluginRunner`**：每个变体创建独立 `BuildContext`，`PluginRunner.RunDerivePagesAsync` 和 `PluginRunner.RunAfterBuildAsync` 各用各的 context，无竞争。

5. **`MetricsWriter.WriteIfRequested`**：依赖所有变体结果 —— 已在循环外，安全。

### 方案

```csharp
// 现有：串行 for 循环
var results = new List<BuildVariantResult>(capacity: languages.Count);
for (var i = 0; i < languages.Count; i++) { ... }

// 改为：Parallel.ForEachAsync，每个变体独立 logger + 收集结果
var results = new BuildVariantResult[languages.Count];
await Parallel.ForEachAsync(
    languages.Select((lang, i) => (lang, i)),
    new ParallelOptions { MaxDegreeOfParallelism = languages.Count, CancellationToken = cancellationToken },
    async (entry, ct) =>
    {
        var (lang, i) = entry;
        // 每个变体使用独立 logger，避免输出交叠
        var variantLogger = new ConsoleLogger(logLevel);
        var baseUrl = I18nOutputMerger.CombineBaseUrlWithLanguage(rootBaseUrl, lang);
        var variantConfig = effectiveConfig with { Site = effectiveConfig.Site with { Language = lang, BaseUrl = baseUrl } };
        var variantItems = I18nOutputMerger.FilterItemsByLanguage(items, lang, defaultLanguage);
        var variantOutputDir = Path.Combine(outputDir, lang);

        var variantCtx = new BuildVariantContext(
            variantConfig, rootDir, overrides, variantItems, bodyStore, variantOutputDir, baseUrl,
            layoutsDir, assetsDir, staticDir, mediaCacheDir,
            SeoAlternates: seoAlternates, RootBaseUrl: rootBaseUrl,
            ManifestSuffix: lang, DefaultLanguage: defaultLanguage);

        // DirectoryHashCache.GetOrAdd 线程安全
        results[i] = await BuildVariantAsync(variantCtx, templateHashCache, ct, variantLogger);
    });

// 合并变体结果（与原逻辑一致）
var variantResults = results.Where(r => r is not null).ToList();
```

**注意**：`BuildVariantAsync` 需要接收一个可选的 `ILogger` 参数，默认使用 `_logger`，并行时传入独立 logger。

### 影响

- 5 语言站点：构建时间从 ~5x 下降到接近 ~1x（忽略 I/O 竞争）
- 风险最低 — 每个变体写入不同目录，共享状态已验证

---

## 问题 2：列表页渲染完全串行

### 现状

[PageRenderDispatcher.cs:L224-L253](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/PageRenderDispatcher.cs#L224-L253)：

```csharp
// 增量模式
foreach (var x in specialLists)
{
    var result = await RenderSpecialListIfNeededAsync(...);
    rendered += result.RenderedCount;
    skipped += result.SkippedCount;
}

// 非增量模式
foreach (var x in specialLists)
{
    var metrics = await RenderSpecialListAlwaysAsync(...);
}
```

6 个列表页（首页 + 各集合列表）逐个串行渲染。

### 可行性分析

| 共享资源 | 线程安全性 | 说明 |
|---|---|---|
| `currentKeys` (HashSet<string>) | ❌ **不安全** | 需要换 `ConcurrentDictionary<string, byte>` 或用锁 |
| `renderReasons` | ✅ 安全 | 已是 `ConcurrentDictionary` |
| `manifest.Entries` (Dictionary) | ❌ **不安全** | 需要换 `ConcurrentDictionary`（增量模式已有 manifestEntries） |
| `writeLocks` | ✅ 可安全 | 每个特殊列表输出路径不同，各自创建 lock 或共享一个 ConcurrentDictionary |
| `stageMetrics` | ⚠️ **不安全** | `BuildStageMetricsCollector` 需要可变操作 |
| `rendered` / `skipped` 计数器 | ❌ **不安全** | 需用 `Interlocked` |

### 方案（增量模式）

```csharp
// 增量模式：并行检查 + 条件渲染
var renderedTotal = 0;
var skippedTotal = 0;
await Parallel.ForEachAsync(specialLists, parallelOptions, async (x, ct) =>
{
    var result = await RenderSpecialListIfNeededAsync(
        x.Route, x.Items, bodyStore, renderer, siteModel, outputDir, templateHash,
        manifest, renderReasons, x.IncludeContent, ct, seoBuilder, listSeoBuilder, listHtmlPostProcessor);
    Interlocked.Add(ref renderedTotal, result.RenderedCount);
    Interlocked.Add(ref skippedTotal, result.SkippedCount);
    MergeCollectors(stageMetrics, result.StageMetrics);  // 需加锁
});
```

**`RenderSpecialListIfNeededAsync` 需要的修改**：
- 写入 manifest 时使用线程安全的操作（已使用 `manifest.Entries[key] = ...`，Dictionary 赋值需要加锁或换 ConcurrentDictionary）
- `currentKeys` 改用 `ConcurrentDictionary<string, byte>`

### 方案（非增量模式）

```csharp
// 非增量模式：所有列表页并行渲染
var writeLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
await Parallel.ForEachAsync(specialLists, parallelOptions, async (x, ct) =>
{
    var metrics = await RenderSpecialListAlwaysAsync(
        x.Route, x.Items, bodyStore, renderer, siteModel, outputDir,
        writeLocks, x.IncludeContent, ct, seoBuilder, listSeoBuilder, listHtmlPostProcessor);
    lock (stageMetrics) { MergeCollectors(stageMetrics, metrics); }
});
```

`writeLocks` 是 `ConcurrentDictionary`，每个输出路径独立 lock，天然并行安全。

### 影响

- 多列表页站点（博客 + 标签 + 分类 + 集合）：列表渲染从 ~6x 下降到 ~1x

---

## 问题 3：列表正文加载完全串行

### 现状

[PageRenderDispatcher.cs:L369-L405](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/PageRenderDispatcher.cs#L369-L405)：

```csharp
private static async Task<List<PageInfo>> BuildPageInfosAsync(...)
{
    var pageInfos = new List<PageInfo>(source.Count);
    foreach (var entry in source)
    {
        string content = string.Empty;
        if (includeContent)
        {
            content = await ContentBodyResolver.GetHtmlAsync(entry.Item, bodyStore, cancellationToken);
        }
        pageInfos.Add(new PageInfo { ... });
    }
    return pageInfos;
}
```

列表页包含正文时（如博客列表显示摘要），每个条目的 body 逐条串行加载。

### 可行性分析

| 共享资源 | 线程安全性 | 说明 |
|---|---|---|
| `bodyStore` | ⚠️ 需确认 | 取决于具体实现。`DictionaryContentBodyStore` 安全（只读），`CompositeContentBodyStore` 委派，`MarkdownBodyStore` 读文件 |
| `pageInfos` (List) | ❌ **不安全** | 需要预分配数组 |
| `stageMetrics` | ⚠️ **不安全** | 需要锁 |

### 方案

```csharp
private static async Task<List<PageInfo>> BuildPageInfosAsync(
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> source,
    IContentBodyStore bodyStore,
    bool includeContent,
    CancellationToken cancellationToken,
    BuildStageMetricsCollector? stageMetrics = null,
    string bodyLoadMetricName = "listBodyLoad",
    Func<ContentItem, RouteInfo, SeoModel>? seoBuilder = null)
{
    var pageInfos = new PageInfo[source.Count];

    if (!includeContent)
    {
        for (var i = 0; i < source.Count; i++)
        {
            pageInfos[i] = new PageInfo
            {
                Title = source[i].Item.Title,
                Url = source[i].Route.Url,
                Seo = seoBuilder?.Invoke(source[i].Item, source[i].Route)
            };
        }
        return new List<PageInfo>(pageInfos);
    }

    await Parallel.ForEachAsync(
        source.Select((entry, i) => (entry, i)),
        new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellationToken },
        async (work, ct) =>
        {
            var (entry, i) = work;
            var bodyLoadStopwatch = Stopwatch.StartNew();
            var content = await ContentBodyResolver.GetHtmlAsync(entry.Item, bodyStore, ct);
            bodyLoadStopwatch.Stop();
            lock (stageMetrics)
            {
                stageMetrics?.Increment(bodyLoadMetricName);
                stageMetrics?.AddDuration(bodyLoadMetricName, bodyLoadStopwatch.ElapsedMilliseconds);
            }
            pageInfos[i] = new PageInfo
            {
                Title = entry.Item.Title,
                Url = entry.Route.Url,
                Content = content,
                Summary = entry.Item.Meta.TryGetValue("summary", out var s) ? s?.ToString() : null,
                PublishDate = entry.Item.PublishAt,
                Fields = entry.Item.Fields,
                Seo = seoBuilder?.Invoke(entry.Item, entry.Route)
            };
        });

    return new List<PageInfo>(pageInfos);
}
```

### 注意事项

- **Notion `bodyStore`**：`NotionBodyStore` 在获取 body 时发起 HTTP 请求。并发过多可能触发 Notion API 限流。Notion 实际限流为每秒 3 请求。如果正文包含 Notion 块渲染（图片、嵌入等），并发应限制为 2-3。
- **Markdown `bodyStore`**：读文件操作无并发问题。
- `ParallelOptions.MaxDegreeOfParallelism` 建议使用 `Math.Min(Environment.ProcessorCount, 4)` 作为合理默认值。

### 影响

- 含摘要的博客列表页（50+ 条目）：正文加载从 ~50 次串行 IO 降到 ~并发度（如 4 路并行），理论提速 ~12x

---

## 整体风险总览

| 风险 | 缓解措施 |
|---|---|
| Logger 非线程安全 | 每个变体独立 `ConsoleLogger` |
| `currentKeys` HashSet 并行写 | 改用 `ConcurrentDictionary<string, byte>` |
| `manifest.Entries` Dictionary 并行写 | 增量模式已用 `ConcurrentDictionary`，确认传递正确 |
| `BuildStageMetricsCollector` 并行写 | 用 `lock` 保护 MergeCollectors |
| `stageMetrics.Increment/AddDuration` 并行写 | `ConcurrentDictionary` + `Interlocked` |
| Notion API 限流（3 req/s） | 限制并行度为 2-3 |
| Scriban `Template.Parse` | 纯 CPU，无共享状态，安全 |

---

## 备选方案：Git Worktree 进程级并行

### 思路

不用 `Parallel.ForEachAsync` 在进程内并行，而是用 `git worktree` 为每个语言变体创建独立的 checkout 目录，每个目录运行独立的 `bukit build` 进程，最后合并结果。

```bash
#!/usr/bin/env bash
# 多语言并行构建 via git worktree
set -euo pipefail

config="${1:-site.yaml}"
root="$(dirname "$(realpath "$config")")"
output="${2:-dist}"
langs=("en-US" "zh-CN" "ms" "ja" "ko")

# 1. 创建 worktree
for lang in "${langs[@]}"; do
  git worktree add --detach "../build-$lang" HEAD
done

# 2. 并行构建
pids=()
for lang in "${langs[@]}"; do
  (
    cd "../build-$lang"
    ln -sf "$root/content" content
    ln -sf "$root/layouts" layouts
    ln -sf "$root/themes" themes
    ln -sf "$root/data" data
    dotnet run --project src/Bukit.Cli -c Release -- \
      build --config "$config" --output "$output/$lang" --site-url https://example.com
  ) &
  pids+=($!)
done

# 3. 等待所有完成
for pid in "${pids[@]}"; do
  wait "$pid" || echo "Build failed for pid=$pid"
done

# 4. 合并输出
dotnet run --project src/Bukit.Cli -c Release -- \
  merge-i18n --config "$config" --output "$output"

# 5. 清理 worktree
for lang in "${langs[@]}"; do
  git worktree remove "../build-$lang" --force
done
```

### 对比分析

| 维度 | 进程内并行 (Parallel.ForEachAsync) | Git Worktree 进程级并行 |
|---|---|---|
| **代码改动量** | ~3 个文件，~50 行改动 | 0 行 C# 改动，需要新增 orchestration 脚本 |
| **线程安全风险** | 需要处理 4 个共享状态点 | **零风险** — 每个进程完全隔离 |
| **启动开销** | < 1ms（线程创建） | `git worktree add` ~200-500ms + `dotnet build` ~3s 每进程 |
| **内存占用** | ~1x（共享 CLR 内存） | ~N x（N 个独立 .NET 进程，每个 ~50-100MB） |
| **磁盘占用** | ~1x | ~N x（每个 worktree checkout 完整内容） |
| **增量缓存** | 共享 `DirectoryHashCache` | 各进程独立，无缓存共享 |
| **内容共享** | 天然共享（同一进程） | 需要 symlink 或复制 `content/`、`themes/`、`data/` 到每个 worktree |
| **构建产物合并** | `I18nOutputMerger` 直接可用 | 仍需要进程内合并步骤 |
| **故障隔离** | 一个变体的异常影响所有（除非 try-catch） | **天然隔离** — 一个语言崩溃不影响其他 |
| **CI 集成** | 零额外依赖 | 需要 `git` 可用 + worktree 管理 + symlink |
| **跨平台** | 全平台一致 | macOS/Linux 用 symlink，Windows 需要 admin 权限创建 symlink |
| **可调试性** | 单进程，VS 调试器直接 attach | 多进程，需要分别 attach 或日志调试 |
| **适用问题** | 全部 3 个问题 | **仅问题 1**（多语言构建）— 问题 2/3 在单个变体内，不适用 |
| **扩展性** | 受限于单机 CPU 核心数 | 可扩展到多台机器（远程构建） |
| **维护成本** | 修改现有 C# 代码，需测试验证 | 新增 bash 脚本，需测试验证 |

### 结论：不推荐 Git Worktree 方案

**核心原因：**

1. **问题不匹配**：P4 的 3 个高性能问题中，Git Worktree 仅能解决问题 1（多语言串行）。问题 2（列表页渲染串行）和问题 3（正文加载串行）发生在单个语言变体内部，worktree 对此无能为力。这意味着即使采用 worktree，仍然需要修改 `PageRenderDispatcher` 代码。

2. **开销远大于收益**：对于典型的三语言站点，启动 3 个独立 .NET 进程的开销（各 ~3s 编译 + 内存）远超 `Parallel.ForEachAsync` 的线程开销。worktree 方式只有在语言数 > CPU 核心数的极端场景下才有意义。

3. **内容同步是阿喀琉斯之踵**：每个 worktree 需要访问同样的 `content/`、`themes/`、`layouts/`、`data/` 目录。用 symlink 可以解决，但：
   - Windows 上普通用户无法创建 symlink（需要管理员权限或开发者模式）
   - symlink 内的文件修改需要谨慎处理（增量构建的 `.cache` 可能因为 symlink 路径不同而失效）
   - 这让本来简单的构建流程变得脆弱

4. **CI 环境不友好**：GitHub Actions 等 CI 环境默认 shallow clone（`fetch-depth: 0`），`git worktree` 需要完整 git 历史才能 check out。

5. **合并步骤仍然需要进程内逻辑**：`I18nOutputMerger.GenerateRootOutputs` 最终仍需把所有变体的 `dist/<lang>/` 合并成 `dist/` 下的统一输出（sitemap.xml、search.index.json 等），这个步骤必须等所有进程完成，且需要进程内代码。

### 推荐路径

保持原方案的进程内 `Parallel.ForEachAsync` 方式，原因：
- 同时解决 3 个性能问题（而非 1 个）
- 代码改动量小（~50 行），风险可控
- 无外部依赖（不需要 git、symlink）
- 全平台一致行为
- 调试友好

---

## 实施步骤

### 步骤 1：`BuildPageInfosAsync` 并行化（最安全，最低风险）

- 修改文件：[PageRenderDispatcher.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/PageRenderDispatcher.cs)
- 将 `foreach` 改为 `Parallel.ForEachAsync`
- 预分配 `PageInfo[]` 数组替代 `List<PageInfo>.Add()`
- `stageMetrics` 操作加 `lock`
- 保留 `includeContent == false` 的快速路径

### 步骤 2：`RenderSpecialListsAsync` 并行化

- 修改文件：同上
- 增量路径：`foreach` → `Parallel.ForEachAsync`，`rendered`/`skipped` 用 `Interlocked`
- 非增量路径：`foreach` → `Parallel.ForEachAsync`，复用 `writeLocks` ConcurrentDictionary
- `currentKeys` 从 `HashSet` 改为 `ConcurrentDictionary<string, byte>`
- `MergeCollectors` 加 `lock`

### 步骤 3：多语言构建并行化

- 修改文件：[SiteEngine.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/SiteEngine.cs)
- `for` 循环 → `Parallel.ForEachAsync`
- 每个变体创建独立 `ConsoleLogger`
- `BuildVariantAsync` 添加可选的 `ILogger? logger = null` 参数
- 结果存入 `BuildVariantResult[]` 预分配数组

### 步骤 4：验证

- `dotnet build bukit.slnx -c Release` ✅
- `dotnet test bukit.slnx -c Release --no-build` ✅
- `dotnet format bukit.slnx --verify-no-changes --no-restore` ✅
- `bash scripts/smoke.sh Release` ✅（含多语言站点变体）

---

## 预期收益

| 场景 | 当前 | 优化后 |
|---|---|---|
| 单语言构建 | ~1x（无变化） | ~1x（无变化） |
| 5 语言构建 | ~5x | ~1x（并行度=5） |
| 6 列表页渲染 | ~6x 串行 | ~1x（并行度=6） |
| 50 条目博客列表（含摘要） | ~50 次串行 body 加载 | ~4x 并行加载 |
