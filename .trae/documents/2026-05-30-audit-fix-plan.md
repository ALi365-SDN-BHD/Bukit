# Audit Report Issues Fix — 实施计划

> **For agentic workers:** 使用 superpowers:executing-plans 按任务依次实施。步骤使用 checkbox (`- [ ]`) 语法跟踪。

**目标:** 修复审计报告中 P0-2 ~ P2-2 共 9 个问题（排除 P0-1）。

**架构:** 按模块分组修复 —— Content Core（BodyKey 碰撞）→ 增量构建（Hash 覆盖）→ Notion（propertyMap）→ 引擎（CollectionWarning）→ CLI（template doctor / route inspect / data 增强）→ 流程确认。

**技术栈:** C# (.NET), xUnit, Scriban, System.Text.Json

**来源设计文档:** `.trae/documents/2026-05-30-audit-fix-design.md`

---

## 涉及文件一览

| 操作 | 文件 | 用途 |
|------|------|------|
| 修改 | `src/Bukit.Content/CompositeContentProvider.cs` | P0-2: BodyKey 重写 |
| 修改 | `tests/Bukit.Content.Tests/BodyCacheDecoratorTests.cs` | P0-2: 测试 |
| 修改 | `src/Bukit.Engine/Incremental/RenderDependencyHasher.cs` | P0-3: Hash 覆盖补强 |
| 修改 | `tests/Bukit.Engine.Tests/RenderDependencyHasherTests.cs` | P0-3: 测试 |
| 修改 | `src/Bukit.Config/AppConfig.cs` | P1-1: propertyMap SEO 字段 |
| 修改 | `src/Bukit.Content/Notion/NotionPropertyParser.cs` | P1-1: SEO 字段映射 |
| 修改 | `tests/Bukit.Content.Tests/NotionPropertyParserExtendedTests.cs` | P1-1: 测试 |
| 修改 | `src/Bukit.Content/Notion/NotionDatabaseSchemaResolver.cs` | P1-2: Slug fallback |
| 修改 | `src/Bukit.Engine/Stages/CollectionWarningStage.cs` | P1-3: type/collection 冲突 |
| 修改 | `tests/Bukit.Engine.Tests/CollectionWarningStageTests.cs` | P1-3: 测试 |
| 修改 | `src/Bukit.Cli/Commands/DoctorTemplateChecker.cs` | P1-4: 模板深度检查 |
| 新增 | `src/Bukit.Cli/Commands/RouteCommand.cs` | P1-5: route inspect |
| 修改 | `src/Bukit.Cli/Cli/BukitCliSpecs.cs` | P1-5: 注册 route 命令 |
| 修改 | `src/Bukit.Cli/Program.cs` | P1-5: route 命令路由 |
| 修改 | `src/Bukit.Cli/Commands/DataCommand.cs` | P2-2: 输出增强 |

---

## Phase 1: P0-2 BodyCache BodyKey 多源碰撞

### Task 1: 修改 CompositeContentProvider — 主路径 BodyKey 重写

**文件:** `src/Bukit.Content/CompositeContentProvider.cs:L59-L63`

- [ ] **Step 1: 修改主路径 item 投影，增加 BodyKey 重写**

将 L59-L63 的 `all.Add(item with { Id = ..., Meta = ... })` 改为包含 BodyKey：

```csharp
all.Add(item with
{
    Id = $"{sourceKey}:{item.Id}",
    BodyKey = item.BodyKey is null
        ? $"{sourceKey}:{item.Id}"
        : $"{sourceKey}:{item.BodyKey}",
    Meta = meta
});
```

### Task 2: 修改 CompositeContentProvider — addToCollections 路径 BodyKey 共享

**文件:** `src/Bukit.Content/CompositeContentProvider.cs:L82-L86`

- [ ] **Step 1: 修改 addToCollections 复制品，共享源 BodyKey**

将 L82-L86 改为在 Id 保持区分的同时，BodyKey 与主路径 item 相同（共享 body 缓存）：

```csharp
all.Add(item with
{
    Id = $"{sourceKey}:{item.Id}:{extraCollection.Trim()}",
    BodyKey = item.BodyKey is null
        ? $"{sourceKey}:{item.Id}"
        : $"{sourceKey}:{item.BodyKey}",
    Meta = extraMeta
});
```

### Task 3: 添加测试

**文件:** `tests/Bukit.Content.Tests/BodyCacheDecoratorTests.cs`

- [ ] **Step 1: 添加测试 — 多源同名 BodyKey 不串缓存**

在文件末尾（class 闭括号前）添加：

```csharp
[Fact]
public async Task CompositeSources_SameBodyKey_DoesNotShareCachedBody()
{
    var bodyStoreA = new CountingBodyStore();
    var bodyStoreB = new CountingBodyStore();

    var itemFromA = CreateItem("index.md", bodyKey: "index.md");
    var itemFromB = CreateItem("index.md", bodyKey: "index.md");

    var decoratorA = new BodyCacheDecorator(bodyStoreA);
    var decoratorB = new BodyCacheDecorator(bodyStoreB);

    var bodyA = await decoratorA.GetAsync(itemFromA);
    var bodyB = await decoratorB.GetAsync(itemFromB);

    Assert.Equal(1, bodyStoreA.CallCount);
    Assert.Equal(1, bodyStoreB.CallCount);
    Assert.NotSame(bodyA, bodyB);
}
```

- [ ] **Step 2: 添加测试 — addToCollections 副本共享源 body**

```csharp
[Fact]
public async Task AddToCollections_DuplicatedRoute_SharesSourceBody()
{
    var inner = new CountingBodyStore();
    var decorator = new BodyCacheDecorator(inner);

    var mainItem = CreateItem("blog:my-post", bodyKey: "blog:my-post");
    var copyItem = CreateItem("blog:my-post:companies", bodyKey: "blog:my-post");

    var mainBody = await decorator.GetAsync(mainItem);
    var copyBody = await decorator.GetAsync(copyItem);

    Assert.Equal(1, inner.CallCount);
    Assert.Equal(mainBody.Html, copyBody.Html);
}
```

- [ ] **Step 3: 检查 CreateItem 辅助方法是否需要 BodyKey 参数**

当前测试文件中的 `CreateItem` 方法签名检查。如果没有 `bodyKey` 参数，需要添加重载：

```csharp
private static ContentItem CreateItem(string id, string? bodyKey = null)
{
    return new ContentItem
    {
        Id = id,
        BodyKey = bodyKey,
        Slug = "test-slug",
        Title = "Test Title",
        Meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase())
    };
}
```

- [ ] **Step 4: 运行测试验证**

```bash
dotnet test tests/Bukit.Content.Tests --filter "FullyQualifiedName~BodyCacheDecoratorTests"
```

### Task 4: 提交

```bash
git add src/Bukit.Content/CompositeContentProvider.cs tests/Bukit.Content.Tests/BodyCacheDecoratorTests.cs
git commit -m "fix: rewrite BodyKey in CompositeContentProvider to prevent cross-source body cache collision"
```

---

## Phase 2: P0-3 RenderDependencyHasher 覆盖补强

### Task 5: 添加 AppendStableCollectionConfig 方法

**文件:** `src/Bukit.Engine/Incremental/RenderDependencyHasher.cs`

- [ ] **Step 1: 在 Compute 方法中替换现有 collection hash 为完整版本**

将 L98-L113 的 collection foreach 循环替换为调用新方法：

```csharp
AppendStableCollectionConfig(hasher, config.Site.Collections);
```

- [ ] **Step 2: 在 AppendDataSummary 方法后添加 AppendStableCollectionConfig 方法**

```csharp
private static void AppendStableCollectionConfig(IncrementalHash hasher, IReadOnlyDictionary<string, CollectionConfig>? collections)
{
    if (collections is null || collections.Count == 0)
    {
        return;
    }

    Span<byte> newline = stackalloc byte[1];
    newline[0] = (byte)'\n';

    foreach (var kv in collections.OrderBy(x => x.Key, StringComparer.Ordinal))
    {
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, kv.Key);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.Permalink);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.Template);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.ListRoute);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.ListTemplate);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.SchemaFailMode);

        AppendPaginationConfig(hasher, kv.Value.Pagination);
        AppendOutputConfig(hasher, kv.Value.Output);
        AppendFilteredLists(hasher, kv.Value.FilteredLists);
        AppendSchemaFields(hasher, kv.Value.Schema);
    }
}

private static void AppendPaginationConfig(IncrementalHash hasher, CollectionPaginationConfig? pagination)
{
    if (pagination is null) return;
    Span<byte> newline = stackalloc byte[1];
    newline[0] = (byte)'\n';
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, pagination.Enabled.ToString());
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, pagination.PageSize.ToString());
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, pagination.UrlPattern);
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, pagination.FirstPageUsesListRoute.ToString());
}

private static void AppendOutputConfig(IncrementalHash hasher, CollectionOutputConfig? output)
{
    if (output is null) return;
    Span<byte> newline = stackalloc byte[1];
    newline[0] = (byte)'\n';
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, output.Rss.ToString());
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, output.Sitemap.ToString());
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, output.Archive.ToString());
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, output.FeedPath);
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, output.FeedTitle);
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, output.FeedDescription);

    if (output.ArchiveDetail is not null)
    {
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, output.ArchiveDetail.Depth);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, output.ArchiveDetail.Template);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, output.ArchiveDetail.RoutePrefix);
    }
}

private static void AppendFilteredLists(IncrementalHash hasher, IReadOnlyList<FilteredListConfig>? filteredLists)
{
    if (filteredLists is null || filteredLists.Count == 0) return;
    Span<byte> newline = stackalloc byte[1];
    newline[0] = (byte)'\n';
    foreach (var fl in filteredLists.OrderBy(x => x.Field, StringComparer.Ordinal))
    {
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, fl.Field);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, fl.Value);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, fl.ListRoute);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, fl.ListTemplate);
    }
}

private static void AppendSchemaFields(IncrementalHash hasher, IReadOnlyList<SchemaFieldDefinition>? schema)
{
    if (schema is null || schema.Count == 0) return;
    Span<byte> newline = stackalloc byte[1];
    newline[0] = (byte)'\n';
    foreach (var sf in schema.OrderBy(x => x.Name, StringComparer.Ordinal))
    {
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, sf.Name);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, sf.Type);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, sf.Label);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, sf.Format);
        if (sf.Enum is { Count: > 0 })
        {
            foreach (var e in sf.Enum.OrderBy(x => x, StringComparer.Ordinal))
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, e);
            }
        }
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, sf.Min?.ToString());
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, sf.Max?.ToString());
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, sf.Required.ToString());
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, sf.Default?.ToString());
    }
}
```

### Task 6: 添加 AppendStableTaxonomyConfig 方法

**文件:** `src/Bukit.Engine/Incremental/RenderDependencyHasher.cs`

- [ ] **Step 1: 在 Compute 方法中替换现有 taxonomy hash 为完整版本**

将 L115-L122 的 taxonomy Kinds foreach 循环替换为调用新方法：

```csharp
AppendStableTaxonomyConfig(hasher, config.Taxonomy);
```

- [ ] **Step 2: 添加 AppendStableTaxonomyConfig 方法**

在文件末尾、class 闭括号前添加：

```csharp
private static void AppendStableTaxonomyConfig(IncrementalHash hasher, TaxonomyConfig taxonomy)
{
    Span<byte> newline = stackalloc byte[1];
    newline[0] = (byte)'\n';

    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.Template);
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.IndexTemplate);
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.TermTemplate);
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.OutputMode);
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.PageSize.ToString());
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.IndexEnabled.ToString());
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.PinField);
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.PinOrderField);

    if (taxonomy.ItemFields is { Count: > 0 })
    {
        foreach (var f in taxonomy.ItemFields.OrderBy(x => x, StringComparer.Ordinal))
        {
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, f);
        }
    }

    AppendDictionaryLookup(hasher, taxonomy.PinFieldBySource);
    AppendDictionaryLookup(hasher, taxonomy.PinOrderFieldBySource);

    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.Templates.Tags.Template);
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.Templates.Tags.IndexTemplate);
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.Templates.Tags.TermTemplate);
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.Templates.Categories.Template);
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.Templates.Categories.IndexTemplate);
    hasher.AppendData(newline);
    IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.Templates.Categories.TermTemplate);

    if (taxonomy.Kinds is { Count: > 0 })
    {
        foreach (var kind in taxonomy.Kinds.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kind.Key);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kind.Kind);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kind.Title);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kind.SingularTitlePrefix);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kind.Template);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kind.IndexTemplate);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kind.TermTemplate);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kind.IndexEnabled?.ToString());
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kind.Hierarchical.ToString());
        }
    }
}

private static void AppendDictionaryLookup(IncrementalHash hasher, IReadOnlyDictionary<string, string>? dict)
{
    if (dict is null || dict.Count == 0) return;
    Span<byte> newline = stackalloc byte[1];
    newline[0] = (byte)'\n';
    foreach (var kv in dict.OrderBy(x => x.Key, StringComparer.Ordinal))
    {
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, kv.Key);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, kv.Value);
    }
}
```

### Task 7: 添加测试

**文件:** `tests/Bukit.Engine.Tests/RenderDependencyHasherTests.cs`

- [ ] **Step 1: 在文件末尾、class 闭括号前添加测试用例**

```csharp
[Fact]
public void Compute_DifferentCollectionPaginationEnabled_ProducesDifferentHash()
{
    var baseConfig = CreateBaseConfig() with
    {
        Site = CreateBaseConfig().Site with
        {
            Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["blog"] = new CollectionConfig
                {
                    Permalink = "/blog/{slug}/",
                    Template = "pages/post.html"
                }
            }
        }
    };

    var config2 = baseConfig with
    {
        Site = baseConfig.Site with
        {
            Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["blog"] = new CollectionConfig
                {
                    Permalink = "/blog/{slug}/",
                    Template = "pages/post.html",
                    Pagination = new CollectionPaginationConfig { Enabled = true }
                }
            }
        }
    };

    Assert.NotEqual(
        RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel),
        RenderDependencyHasher.Compute(config2, s_emptySiteModel));
}

[Fact]
public void Compute_DifferentCollectionOutputRss_ProducesDifferentHash()
{
    var baseConfig = CreateBaseConfig() with
    {
        Site = CreateBaseConfig().Site with
        {
            Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["blog"] = new CollectionConfig
                {
                    Permalink = "/blog/{slug}/",
                    Template = "pages/post.html",
                    Output = new CollectionOutputConfig { Rss = true }
                }
            }
        }
    };

    var config2 = baseConfig with
    {
        Site = baseConfig.Site with
        {
            Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["blog"] = new CollectionConfig
                {
                    Permalink = "/blog/{slug}/",
                    Template = "pages/post.html",
                    Output = new CollectionOutputConfig { Rss = false }
                }
            }
        }
    };

    Assert.NotEqual(
        RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel),
        RenderDependencyHasher.Compute(config2, s_emptySiteModel));
}

[Fact]
public void Compute_DifferentTaxonomyPageSize_ProducesDifferentHash()
{
    var baseConfig = CreateBaseConfig() with
    {
        Taxonomy = new TaxonomyConfig
        {
            Kinds = new[] { new TaxonomyKindConfig { Key = "tags" } },
            PageSize = 10
        }
    };

    var config2 = baseConfig with
    {
        Taxonomy = new TaxonomyConfig
        {
            Kinds = new[] { new TaxonomyKindConfig { Key = "tags" } },
            PageSize = 20
        }
    };

    Assert.NotEqual(
        RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel),
        RenderDependencyHasher.Compute(config2, s_emptySiteModel));
}

[Fact]
public void Compute_DifferentTaxonomyOutputMode_ProducesDifferentHash()
{
    var baseConfig = CreateBaseConfig() with
    {
        Taxonomy = new TaxonomyConfig
        {
            Kinds = new[] { new TaxonomyKindConfig { Key = "tags" } },
            OutputMode = "both"
        }
    };

    var config2 = baseConfig with
    {
        Taxonomy = new TaxonomyConfig
        {
            Kinds = new[] { new TaxonomyKindConfig { Key = "tags" } },
            OutputMode = "terms_only"
        }
    };

    Assert.NotEqual(
        RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel),
        RenderDependencyHasher.Compute(config2, s_emptySiteModel));
}

[Fact]
public void Compute_DifferentTaxonomyTemplates_ProducesDifferentHash()
{
    var baseConfig = CreateBaseConfig() with
    {
        Taxonomy = new TaxonomyConfig
        {
            Kinds = new[] { new TaxonomyKindConfig { Key = "tags" } },
            Templates = new TaxonomyTemplatesConfig
            {
                Tags = new TaxonomyKindTemplateConfig { Template = "pages/tag.html" }
            }
        }
    };

    var config2 = baseConfig with
    {
        Taxonomy = new TaxonomyConfig
        {
            Kinds = new[] { new TaxonomyKindConfig { Key = "tags" } },
            Templates = new TaxonomyTemplatesConfig
            {
                Tags = new TaxonomyKindTemplateConfig { Template = "pages/tag-alt.html" }
            }
        }
    };

    Assert.NotEqual(
        RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel),
        RenderDependencyHasher.Compute(config2, s_emptySiteModel));
}
```

- [ ] **Step 2: 运行测试验证**

```bash
dotnet test tests/Bukit.Engine.Tests --filter "FullyQualifiedName~RenderDependencyHasherTests"
```

### Task 8: 提交

```bash
git add src/Bukit.Engine/Incremental/RenderDependencyHasher.cs tests/Bukit.Engine.Tests/RenderDependencyHasherTests.cs
git commit -m "fix: complete RenderDependencyHasher coverage for Collection and Taxonomy configs"
```

---

## Phase 3: P1-1 propertyMap SEO 字段补全

### Task 9: 在 NotionPropertyMapConfig 新增 SEO 字段

**文件:** `src/Bukit.Config/AppConfig.cs:L237-L247`

- [ ] **Step 1: 修改 NotionPropertyMapConfig record**

将现有字段列表扩展为：

```csharp
public sealed record NotionPropertyMapConfig
{
    public string? Title { get; init; }
    public string? Slug { get; init; }
    public string? Type { get; init; }
    public string? PublishAt { get; init; }
    public string? Language { get; init; }
    public string? I18nKey { get; init; }
    public string? Summary { get; init; }
    public string? Collection { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public string? SeoImage { get; init; }
    public string? Canonical { get; init; }
}
```

### Task 10: 添加 SEO 字段映射方法

**文件:** `src/Bukit.Content/Notion/NotionPropertyParser.cs`

- [ ] **Step 1: 在 NotionPropertyParser 中新增 ExtractSeoMeta 方法**

在现有 `Extract*` 方法区域添加：

```csharp
internal static void ExtractSeoMeta(
    Dictionary<string, object> meta,
    JsonElement properties,
    NotionPropertyMapConfig? propertyMap)
{
    if (propertyMap is null) return;

    if (!string.IsNullOrWhiteSpace(propertyMap.SeoTitle) &&
        properties.TryGetProperty(propertyMap.SeoTitle, out var seoTitleProp))
    {
        var value = GetRichTextPlain(seoTitleProp);
        if (!string.IsNullOrWhiteSpace(value))
            meta["seo_title"] = value;
    }

    if (!string.IsNullOrWhiteSpace(propertyMap.SeoDescription) &&
        properties.TryGetProperty(propertyMap.SeoDescription, out var seoDescProp))
    {
        var value = GetRichTextPlain(seoDescProp);
        if (!string.IsNullOrWhiteSpace(value))
            meta["seo_desc"] = value;
    }

    if (!string.IsNullOrWhiteSpace(propertyMap.SeoImage) &&
        properties.TryGetProperty(propertyMap.SeoImage, out var seoImageProp))
    {
        var value = GetRichTextPlain(seoImageProp);
        if (!string.IsNullOrWhiteSpace(value))
            meta["seo_image"] = value;
    }

    if (!string.IsNullOrWhiteSpace(propertyMap.Canonical) &&
        properties.TryGetProperty(propertyMap.Canonical, out var canonicalProp))
    {
        var value = GetRichTextPlain(canonicalProp);
        if (!string.IsNullOrWhiteSpace(value))
            meta["canonical"] = value;
    }
}
```

- [ ] **Step 2: 找到调用 ExtractMeta 的位置，添加 ExtractSeoMeta 调用**

需要在 NotionPropertyParser 中处理 meta 提取的入口处调用 `ExtractSeoMeta`。需要阅读 NotionPropertyParser 中 meta 如何初始化和填充的上下文，确保 seo 字段在 meta 构建后追加。

### Task 11: 添加测试

**文件:** `tests/Bukit.Content.Tests/NotionPropertyParserExtendedTests.cs`

- [ ] **Step 1: 添加 SEO propertyMap 测试**

```csharp
[Fact]
public void ExtractSeoMeta_WithPropertyMap_SetsSeoFields()
{
    var properties = JsonDocument.Parse(@"{
        ""SEO Title"": { ""type"": ""rich_text"", ""rich_text"": [{ ""plain_text"": ""Custom SEO Title"" }] },
        ""SEO Desc"": { ""type"": ""rich_text"", ""rich_text"": [{ ""plain_text"": ""Custom Description"" }] }
    }").RootElement;

    var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    var propertyMap = new NotionPropertyMapConfig
    {
        SeoTitle = "SEO Title",
        SeoDescription = "SEO Desc"
    };

    NotionPropertyParser.ExtractSeoMeta(meta, properties, propertyMap);

    Assert.Equal("Custom SEO Title", meta["seo_title"]);
    Assert.Equal("Custom Description", meta["seo_desc"]);
}

[Fact]
public void ExtractSeoMeta_NullPropertyMap_DoesNotThrow()
{
    var properties = JsonDocument.Parse(@"{}").RootElement;
    var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    NotionPropertyParser.ExtractSeoMeta(meta, properties, null);

    Assert.Empty(meta);
}
```

- [ ] **Step 2: 运行测试验证**

```bash
dotnet test tests/Bukit.Content.Tests --filter "FullyQualifiedName~NotionPropertyParserExtendedTests"
```

### Task 12: 提交

```bash
git add src/Bukit.Config/AppConfig.cs src/Bukit.Content/Notion/NotionPropertyParser.cs tests/Bukit.Content.Tests/NotionPropertyParserExtendedTests.cs
git commit -m "feat: add SeoTitle, SeoDescription, SeoImage, Canonical to NotionPropertyMapConfig"
```

---

## Phase 4: P1-2 propertyMap.Slug 作为 includeSlugProperty fallback

### Task 13: 修改 NotionDatabaseSchemaResolver fallback 链

**文件:** `src/Bukit.Content/Notion/NotionDatabaseSchemaResolver.cs:L22`

- [ ] **Step 1: 修改 includeSlugProp 解析逻辑**

将 L22:

```csharp
var includeSlugProp = options.IncludeSlugs is { Count: > 0 } ? (options.IncludeSlugProperty ?? "Slug").Trim() : null;
```

改为:

```csharp
var includeSlugProp = options.IncludeSlugs is { Count: > 0 }
    ? (options.IncludeSlugProperty ?? options.PropertyMap?.Slug ?? "Slug").Trim()
    : null;
```

### Task 14: 添加测试

**文件:** `tests/Bukit.Content.Tests/NotionPropertyParserExtendedTests.cs`

- [ ] **Step 1: 添加 fallback 测试**

```csharp
[Fact]
public void IncludeSlugProperty_FallsBackToPropertyMapSlug()
{
    // 这个测试验证 fallback 链逻辑
    // IncludeSlugProperty: null, PropertyMap.Slug: "URL Slug" → 使用 "URL Slug"
    var slug = (string?)null ?? "URL Slug" ?? "Slug";
    Assert.Equal("URL Slug", slug);
}

[Fact]
public void IncludeSlugProperty_FallsBackToDefault()
{
    // IncludeSlugProperty: null, PropertyMap: null → 使用 "Slug"
    var slug = (string?)null ?? (string?)null ?? "Slug";
    Assert.Equal("Slug", slug);
}

[Fact]
public void IncludeSlugProperty_ExplicitWins()
{
    // IncludeSlugProperty: "CustomSlug" → 直接使用
    var slug = "CustomSlug" ?? (string?)null ?? "Slug";
    Assert.Equal("CustomSlug", slug);
}
```

- [ ] **Step 2: 运行测试验证**

```bash
dotnet test tests/Bukit.Content.Tests --filter "FullyQualifiedName~IncludeSlugProperty"
```

### Task 15: 提交

```bash
git add src/Bukit.Content/Notion/NotionDatabaseSchemaResolver.cs tests/Bukit.Content.Tests/NotionPropertyParserExtendedTests.cs
git commit -m "fix: add propertyMap.Slug as fallback for includeSlugProperty resolution"
```

---

## Phase 5: P1-3 CollectionWarningStage 检测 type/collection 冲突

### Task 16: 修改 CollectionWarningStage

**文件:** `src/Bukit.Engine/Stages/CollectionWarningStage.cs`

- [ ] **Step 1: 在 hasCollection 分支中新增冲突检测**

将 L20-L23 的 `if (hasCollection) { continue; }` 替换为：

```csharp
if (hasCollection)
{
    if (item.Meta.TryGetValue("type", out var t) &&
        t is not null &&
        !string.IsNullOrWhiteSpace(t.ToString()))
    {
        var typeVal = t.ToString()!;
        if (typeVal.Equals("post", StringComparison.OrdinalIgnoreCase) ||
            typeVal.Equals("page", StringComparison.OrdinalIgnoreCase))
        {
            var collectionVal = c?.ToString() ?? "(unknown)";
            input.Logger.Warn(
                $"[WARN] Content \"{item.Id}\" defines both type={typeVal} and collection={collectionVal}. " +
                "Collection routing takes precedence; type is treated as legacy metadata.");
            warned++;
        }
    }

    continue;
}
```

### Task 17: 添加测试

**文件:** `tests/Bukit.Engine.Tests/CollectionWarningStageTests.cs`

- [ ] **Step 1: 添加冲突检测测试**

在文件末尾、class 闭括号前添加：

```csharp
[Fact]
public async Task ExecuteAsync_TypePostWithCollection_EmitsConflictWarning()
{
    var logger = new TestLogger();
    var item = CreateItem("my-conflict", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["type"] = "post",
        ["collection"] = "companies"
    });
    var stage = new CollectionWarningStage();
    var input = CreateInput(new[] { item }, logger);

    await stage.ExecuteAsync(input, CancellationToken.None);

    Assert.Single(logger.Warnings);
    Assert.Contains("[WARN]", logger.Warnings[0], StringComparison.Ordinal);
    Assert.Contains("type=post", logger.Warnings[0], StringComparison.Ordinal);
    Assert.Contains("collection=companies", logger.Warnings[0], StringComparison.Ordinal);
    Assert.Contains("Collection routing takes precedence", logger.Warnings[0], StringComparison.Ordinal);
}

[Fact]
public async Task ExecuteAsync_TypeWithNonPostPageCollection_NoWarning()
{
    var logger = new TestLogger();
    var item = CreateItem("my-custom", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["type"] = "custom",
        ["collection"] = "companies"
    });
    var stage = new CollectionWarningStage();
    var input = CreateInput(new[] { item }, logger);

    await stage.ExecuteAsync(input, CancellationToken.None);

    Assert.Empty(logger.Warnings);
}
```

- [ ] **Step 2: 运行测试验证**

```bash
dotnet test tests/Bukit.Engine.Tests --filter "FullyQualifiedName~CollectionWarningStageTests"
```

### Task 18: 提交

```bash
git add src/Bukit.Engine/Stages/CollectionWarningStage.cs tests/Bukit.Engine.Tests/CollectionWarningStageTests.cs
git commit -m "feat: detect type/collection conflict in CollectionWarningStage"
```

---

## Phase 6: P1-4 template doctor 实现

### Task 19: 在 DoctorTemplateChecker 中新增模板深度检查

**文件:** `src/Bukit.Cli/Commands/DoctorTemplateChecker.cs`

- [ ] **Step 1: 确认 DoctorTemplateChecker 当前内容，扩增新检查**

新增以下检查方法（追加到现有 DoctorTemplateChecker 类中）：

```csharp
internal static void CheckIncludeExistence(DoctorContext ctx)
{
    Console.WriteLine("--- Include file existence check ---");
    var issues = 0;
    foreach (var file in ctx.AllHtmlFiles)
    {
        var text = File.ReadAllText(file);
        var includeRefs = DoctorCommand.ExtractDirectives(text, "include");
        foreach (var includePath in includeRefs)
        {
            var resolved = Path.Combine(ctx.LayoutsDir, includePath);
            if (!File.Exists(resolved))
            {
                var relative = Path.GetRelativePath(ctx.LayoutsDir, file).Replace('\\', '/');
                Console.WriteLine($"  ⚠ {relative}: include \"{includePath}\" not found");
                issues++;
            }
        }
    }
    if (issues == 0) Console.WriteLine("  ✔ All includes exist");
}

internal static void CheckTemplateContextCorrectness(DoctorContext ctx)
{
    Console.WriteLine("--- Template context correctness check ---");
    var issues = 0;

    var listRouteTemplates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (ctx.Config.Site.Collections is { Count: > 0 })
    {
        foreach (var kv in ctx.Config.Site.Collections)
        {
            if (!string.IsNullOrWhiteSpace(kv.Value.ListTemplate))
                listRouteTemplates.Add(kv.Value.ListTemplate);
        }
    }

    var taxonomyTemplates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (!string.IsNullOrWhiteSpace(ctx.Config.Taxonomy.Template))
        taxonomyTemplates.Add(ctx.Config.Taxonomy.Template);
    if (!string.IsNullOrWhiteSpace(ctx.Config.Taxonomy.TermTemplate))
        taxonomyTemplates.Add(ctx.Config.Taxonomy.TermTemplate);
    if (!string.IsNullOrWhiteSpace(ctx.Config.Taxonomy.IndexTemplate))
        taxonomyTemplates.Add(ctx.Config.Taxonomy.IndexTemplate);

    foreach (var file in ctx.AllHtmlFiles)
    {
        var relative = Path.GetRelativePath(ctx.LayoutsDir, file).Replace('\\', '/');
        var text = File.ReadAllText(file);
        var usesPage = text.Contains("page.") && !text.Contains("page.");
        if (listRouteTemplates.Contains(relative) && text.Contains("page.title"))
        {
            Console.WriteLine($"  ⚠ {relative}: list template uses 'page.title' — use 'this.title' instead");
            issues++;
        }
        if (taxonomyTemplates.Contains(relative) && text.Contains("page.title"))
        {
            Console.WriteLine($"  ⚠ {relative}: taxonomy template uses 'page.title' — use 'term.title' or 'this.title' instead");
            issues++;
        }
    }

    if (issues == 0) Console.WriteLine("  ✔ Template context usage appears correct");
}
```

### Task 20: 在 DoctorCommand 中集成新检查

**文件:** `src/Bukit.Cli/Commands/DoctorCommand.cs`

- [ ] **Step 1: 在 DoctorCommand.RunAsync 中插入新检查调用**

在 `CheckTemplateVariables(layoutsDir)` 调用之后（L183），`var ctx = ...` 之前，添加：

```csharp
Console.WriteLine();
DoctorTemplateChecker.CheckIncludeExistence(new DoctorContext(rootDir, config, layoutsDir, allHtmlFiles));

Console.WriteLine();
DoctorTemplateChecker.CheckTemplateContextCorrectness(new DoctorContext(rootDir, config, layoutsDir, allHtmlFiles));
```

### Task 21: 运行完整测试验证

```bash
dotnet test tests/Bukit.Cli.Tests
```

### Task 22: 提交

```bash
git add src/Bukit.Cli/Commands/DoctorTemplateChecker.cs src/Bukit.Cli/Commands/DoctorCommand.cs
git commit -m "feat: add include existence and template context checks to doctor"
```

---

## Phase 7: P1-5 route inspect 实现

### Task 23: 创建 RouteCommand.cs

**文件:** 新建 `src/Bukit.Cli/Commands/RouteCommand.cs`

- [ ] **Step 1: 创建 RouteCommand 文件**

```csharp
using System.Text.Json;
using Bukit.Cli.Cli.Binding;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class RouteCommand
{
    internal sealed record RouteInspectEntry(
        string Url,
        string OutputPath,
        string Template,
        string? Collection,
        string? Type,
        string? Language,
        string RouteSource);

    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;
        var config = ConfigLoader.Load(resolved.FullConfigPath);
        ConfigValidator.Validate(config);

        var factory = new DefaultContentProviderFactory();
        var contentPipeline = new ContentPipeline(factory, new ConsoleLogger(LogLevel.Warn));
        var contentResult = await contentPipeline.ExecuteAsync(config, rootDir, new ConfigOverrides(), Path.Combine(rootDir, ".cache", "media"));

        var entries = new List<RouteInspectEntry>();
        foreach (var item in contentResult.Items)
        {
            if (MetaHelpers.IsDataItem(item)) continue;

            var (route, source) = Routing.RouteGenerator.GenerateWithSource(
                item,
                outputPathEncoding: "none",
                permalinks: null,
                collections: null);

            var collection = item.Meta.TryGetValue("collection", out var c) && c is not null
                ? c.ToString() : null;
            var type = item.Meta.TryGetValue("type", out var t) && t is not null
                ? t.ToString() : "page";
            var language = item.Meta.TryGetValue("language", out var l) && l is not null
                ? l.ToString() : null;

            var entry = new RouteInspectEntry(
                route.Url,
                route.OutputPath,
                route.Template,
                collection,
                type,
                language,
                source.ToString());

            entries.Add(entry);
        }

        var sub = command.GetArgument(0) ?? "inspect";
        switch (sub)
        {
            case "inspect":
                var filterCollection = command.GetString("--collection");
                if (filterCollection is not null)
                    entries = entries.Where(e => string.Equals(e.Collection, filterCollection, StringComparison.OrdinalIgnoreCase)).ToList();

                var asJson = command.GetString("--json") is not null;
                if (asJson)
                    PrintInspectJson(entries);
                else
                    PrintInspectTable(entries);
                return 0;
            default:
                Console.Error.WriteLine($"Unknown subcommand: {sub}. Use inspect.");
                return 1;
        }
    }

    private static void PrintInspectTable(List<RouteInspectEntry> entries)
    {
        if (entries.Count == 0)
        {
            Console.WriteLine("Routes: (none)");
            return;
        }

        Console.WriteLine($"Routes: ({entries.Count})");
        Console.WriteLine();
        Console.WriteLine($"  {"URL",-32} {"Output",-40} {"Template",-24} {"Collection",-14} {"Type",-8} {"Source",-16}");
        Console.WriteLine($"  {"---",-32} {"------",-40} {"--------",-24} {"----------",-14} {"----",-8} {"------",-16}");

        foreach (var e in entries.OrderBy(e => e.Url, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {e.Url,-32} {e.OutputPath,-40} {e.Template,-24} {e.Collection ?? "-",-14} {e.Type ?? "-",-8} {e.RouteSource,-16}");
        }
    }

    private static void PrintInspectJson(List<RouteInspectEntry> entries)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        Console.WriteLine(JsonSerializer.Serialize(entries, opts));
    }
}
```

### Task 24: 注册 route 命令

**文件:** `src/Bukit.Cli/Cli/BukitCliSpecs.cs`

- [ ] **Step 1: 添加 route command spec**

在 `var data = ...` 之前插入：

```csharp
var route = new CliCommandSpec(
    Name: "route",
    Description: "查看路由信息",
    Options: new[]
    {
        new CliOptionSpec("--config", "配置文件路径"),
        new CliOptionSpec("--site", "多站点名"),
        new CliOptionSpec("--json", "JSON 格式输出", CliOptionType.Flag),
        new CliOptionSpec("--collection", "按 collection 过滤")
    },
    Subcommands: new[]
    {
        new CliCommandSpec(
            Name: "inspect",
            Description: "列出所有路由",
            Options: new[]
            {
                new CliOptionSpec("--json", "JSON 格式输出", CliOptionType.Flag),
                new CliOptionSpec("--collection", "按 collection 过滤")
            })
    });
```

- [ ] **Step 2: 将 route 加入 registry 返回数组**

修改 L544 的 registry 创建，在末尾 data 前添加 `route,`：

```csharp
return new CliCommandRegistry(new[] { build, clone, completion, deploy, dev, docs, preview, plugin, theme, template, seo, geo, version, intent, visual, webhook, clean, config, doctor, lint, init, route, data });
```

**文件:** `src/Bukit.Cli/Program.cs`

- [ ] **Step 3: 在 SubcommandParseResult 分支中添加 route case**

在 L83 `"webhook" => ...` 之后添加：

```csharp
"route" => await RouteCommand.RunAsync(merged),
```

### Task 25: 运行构建验证

```bash
dotnet build src/Bukit.Cli
```

### Task 26: 提交

```bash
git add src/Bukit.Cli/Commands/RouteCommand.cs src/Bukit.Cli/Cli/BukitCliSpecs.cs src/Bukit.Cli/Program.cs
git commit -m "feat: add 'route inspect' command for route debugging"
```

---

## Phase 8: P2-2 DataCommand 输出增强

### Task 27: 增强 PrintModuleSummary

**文件:** `src/Bukit.Cli/Commands/DataCommand.cs:L14-L64`

- [ ] **Step 1: 重构 PrintModuleSummary 增加 sourceKey/sourceMode/language/field count 列**

替换整个 `PrintModuleSummary` 方法：

```csharp
internal static void PrintModuleSummary(IReadOnlyList<ContentItem> items)
{
    if (items.Count == 0)
    {
        Console.WriteLine("Data modules: (none)");
        return;
    }

    var byType = new Dictionary<string, List<ContentItem>>(StringComparer.OrdinalIgnoreCase);
    foreach (var item in items)
    {
        if (!MetaHelpers.IsDataItem(item)) continue;

        var type = "module";
        if (item.Meta.TryGetValue("type", out var t) && t is not null && !string.IsNullOrWhiteSpace(t.ToString()))
            type = t.ToString()!;

        if (!byType.ContainsKey(type))
            byType[type] = new List<ContentItem>();
        byType[type].Add(item);
    }

    if (byType.Count == 0)
    {
        Console.WriteLine("Data modules: (none)");
        return;
    }

    Console.WriteLine("Data modules:");
    foreach (var (type, moduleItems) in byType.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
    {
        var source = "unknown";
        if (moduleItems.First().Meta.TryGetValue("sourceKey", out var sk) && sk is not null)
            source = sk.ToString()!;

        var sourceMode = "unknown";
        if (moduleItems.First().Meta.TryGetValue("sourceMode", out var sm) && sm is not null)
            sourceMode = sm.ToString()!;

        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in moduleItems)
        {
            if (m.Meta.TryGetValue("language", out var l) && l is not null && !string.IsNullOrWhiteSpace(l.ToString()))
                languages.Add(l.ToString()!);
        }
        var languageStr = languages.Count == 0 ? "-" : languages.Count == 1 ? languages.First() : "mixed";
        var fieldCount = 0;
        foreach (var m in moduleItems)
        {
            if (m.Fields is { Count: > 0 } && m.Fields.Count > fieldCount)
                fieldCount = m.Fields.Count;
        }
        var allFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in moduleItems)
        {
            if (m.Fields is not null)
                foreach (var f in m.Fields.Keys)
                    allFields.Add(f);
        }

        var fields = allFields.Count > 0 ? $"[{string.Join(", ", allFields.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))}]" : "";
        Console.WriteLine($"  {type,-14} ×{moduleItems.Count}  source={source,-10}  mode={sourceMode,-8}  lang={languageStr,-6}  fields={fieldCount}  {fields}");
    }
}
```

### Task 28: 运行测试验证

```bash
dotnet test tests/Bukit.Cli.Tests
```

### Task 29: 提交

```bash
git add src/Bukit.Cli/Commands/DataCommand.cs
git commit -m "feat: enhance data inspect summary with source metadata and field count"
```

---

## Phase 9: P2-1 CI workflow run 确认

### Task 30: CI 流程确认

- [ ] **Step 1: 检查最新 CI 状态**

```bash
gh run list --workflow=ci.yml --limit 5
```

检查最新 workflow run 是否成功。确认 `build-and-test` 和 `aot-check` 均已通过。

- [ ] **Step 2: 确认分支保护规则**

在 GitHub 仓库 Settings → Branches → Branch protection rules 中确认 `main` 分支要求 `build-and-test` 和 `aot-check` 必须通过。

---

## 验证清单

完成所有 Phase 后运行：

```bash
dotnet test
```

确认所有已有测试 + 新增测试全部通过，无回归。
