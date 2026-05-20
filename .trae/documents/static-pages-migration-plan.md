# 静态页面迁移：Bukit 增强 filtered list + staticTemplate 支持

> 日期：2026-05-20\
> 问题：5 个静态 HTML 页面无法使用 Scriban 模板，无法引用 `site.data.*`、partias，修改需同步 5 处

***

## 问题分析

### 当前状态

| 页面                 | 路径                     | 实现方式                                           | 核心需求                                     |
| ------------------ | ---------------------- | ---------------------------------------------- | ---------------------------------------- |
| china-companies    | `/china-companies/`    | `static/china-companies/index.html`（纯 HTML）    | 展示 `page` 集合中 `Type == "已进驻中国企业"` 的内容列表  |
| malaysia-companies | `/malaysia-companies/` | `static/malaysia-companies/index.html`（纯 HTML） | 展示 `page` 集合中 `Type == "马来西亚本地企业"` 的内容列表 |
| about              | `/about/`              | `static/about/index.html`（纯 HTML）              | 纯介绍页面，无内容聚合                              |
| contact            | `/contact/`            | `static/contact/index.html`（纯 HTML）            | 纯联系方式页面                                  |
| join               | `/join/`               | `static/join/index.html`（纯 HTML）               | 纯表单页面                                    |

### 架构瓶颈

1. **`static/`** **目录是纯文件复制**：[SiteEngine.cs#L175](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/SiteEngine.cs#L175) 调用 `DirectoryCopy.Sync(ctx.StaticDir, outputDir)`，不经过 Scriban 渲染。

2. **每个 collection 只能有一条 listRoute**：[BuildSpecialListDefinitions](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/PageRenderDispatcher.cs#L483-L511) 中，每个 collection 只产生一个 `SpecialListDefinition`，通过 `GetByCollection(key)` 获取全部内容项。

3. **无字段值过滤能力**：[CollectionRouteIndex.GetByCollection](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/CollectionRouteIndex.cs#L50-L59) 按 collection key 分组，但无按 `item.Fields["Type"]` 值进一步筛选的能力。

***

## 解决方案：双路径

### 路径 A：Filtered Lists（解决 china-companies / malaysia-companies）

为 `CollectionConfig` 增加 `filteredLists` 配置，允许基于内容项的 Fields 字段值进一步拆分为子列表页。

#### 1. 配置模型变更

**文件：`AppConfig.cs`**

```csharp
public sealed record CollectionConfig
{
    // ... existing fields ...
    public IReadOnlyList<FilteredListConfig>? FilteredLists { get; init; }
}

public sealed record FilteredListConfig
{
    public required string Field { get; init; }          // 过滤字段名，如 "Type"
    public required string Value { get; init; }           // 过滤值，如 "已进驻中国企业"
    public required string ListRoute { get; init; }       // 如 "/china-companies/"
    public string? ListTemplate { get; init; }            // 可选，默认复用 collection.ListTemplate
    public bool? PaginationEnabled { get; init; }          // 可选，默认复用 collection.Pagination.Enabled
    public int? PageSize { get; init; }                   // 可选，默认复用 collection.Pagination.PageSize
}
```

#### 2. YAML 配置示例（丝路商讯项目）

```yaml
site:
  collections:
    page:
      permalink: /companies/{slug}/
      template: pages/company_detail.html
      listRoute: /companies/
      listTemplate: pages/company_overview.html
      pagination:
        enabled: true
        pageSize: 12
      filteredLists:
        - field: Type
          value: "已进驻中国企业"
          listRoute: /china-companies/
          listTemplate: pages/company_list.html
          paginationEnabled: true
          pageSize: 9
        - field: Type
          value: "马来西亚本地企业"
          listRoute: /malaysia-companies/
          listTemplate: pages/company_list.html
          paginationEnabled: true
          pageSize: 9
```

#### 3. 配置加载

**文件：`ConfigLoader.cs`**

新增 `ReadFilteredLists(YamlMappingNode)` 方法，从 `collectionNode` 中读取 `filteredLists` 键。

#### 4. 渲染集成

**文件：`PageRenderDispatcher.cs`** **—** **`BuildSpecialListDefinitions`**

在现有 `foreach (var (key, collection) in collections)` 循环中，对每个 collection 遍历 `FilteredLists`，创建额外的 `SpecialListDefinition`：

```csharp
if (collection.FilteredLists is { Count: > 0 })
{
    var allItems = index.GetByCollection(key);
    foreach (var filter in collection.FilteredLists)
    {
        var filtered = allItems
            .Where(x => TryGetFieldValue(x.Item.Fields, filter.Field) == filter.Value)
            .ToList();
        
        var url = RoutePathBuilder.NormalizeListRoute(filter.ListRoute);
        var template = string.IsNullOrWhiteSpace(filter.ListTemplate) 
            ? collection.ListTemplate ?? "pages/list.html"
            : filter.ListTemplate.Trim();
        
        list.Add(new SpecialListDefinition(
            new RouteInfo(url, ..., template), filtered, ...));
    }
}
```

关键：`TryGetFieldValue` 需要访问 `ContentField` — 已有的 `ContentField` 类型有 `.Value` 属性（`object`），可以用 `ToString()` 比较。

**文件：`SiteEngine.cs`** **—** **`BuildListRoutesCore`**

同样为每个 `FilteredList` 生成列表路由（供 SEO、sitemap 等使用）。

#### 5. 分页支持

**文件：`PaginationPlugin.cs`**

如果 `paginationEnabled: true`，需要为每个 filtered list 生成分页。这需要 PaginationPlugin 知道 filtered lists 的存在。一种简洁的方式是让 `SpecialListDefinition` 携带 `pagination` 配置，PaginationPlugin 读取该配置决定是否分页。

更简单的做法：filtered list 的 items 数量如果超过 `pageSize`，PageRenderDispatcher 在渲染时通过 `include "partials/pagination.html"` 由 Scriban 侧处理（`pagination` 变量由引擎在 `ListPageModel` 中注入，但当前分页逻辑由 PaginationPlugin 独立生成页面）。因此需要协调。

**推荐方案**：filtered lists 不单独生成分页 URL 页面，而是在模板中通过 `{{ for page in pages | array.slice (pagination.current_page-1)*pageSize, pageSize }}` 实现前端分页（Scriban 支持 array.slice）。这样可以避免分页插件层面的重构。后续版本再考虑后端分页。

***

### 路径 B：Static Template 渲染（解决 about / contact / join）

利用已有的 `theme.staticTemplate` 配置（已在 #3 修复中添加了配置模型），让 `static/` 目录下的 `.html` 文件通过 Scriban 渲染。

#### 1. 实现方式

**文件：`SiteEngine.cs`** **—** **`BuildVariantAsync`**

当前代码（第 173-175 行）：

```csharp
if (Directory.Exists(ctx.StaticDir))
{
    DirectoryCopy.Sync(ctx.StaticDir, outputDir);
}
```

改为：

```csharp
if (Directory.Exists(ctx.StaticDir))
{
    var staticTemplate = config.Theme.StaticTemplate;
    if (!string.IsNullOrWhiteSpace(staticTemplate))
    {
        RenderStaticFiles(ctx.StaticDir, outputDir, renderer, siteModel, staticTemplate, baseUrl);
    }
    else
    {
        DirectoryCopy.Sync(ctx.StaticDir, outputDir);
    }
}
```

`RenderStaticFiles` 遍历 `static/` 下的 `.html` 文件，对每个文件：

1. 读取 HTML 内容作为 `page.content`
2. 从文件路径推断 `page.url` 和 `page.title`
3. 通过 `renderer.RenderPage(staticTemplate, pageModel)` 渲染
4. 写入输出目录

#### 2. YAML 配置示例

```yaml
theme:
  name: silkroad
  layouts: layouts
  assets: assets
  static: static
  staticTemplate: pages/page.html    # 使用通用页面模板渲染 static/ 下的 HTML
```

当 `staticTemplate` 不为空时，static 文件的主体内容会作为 `{{ page.content }}` 注入到模板中，同时享有 header/footer/cta partials 和 `site.data.*` 的能力。

#### 3. 用户迁移步骤

1. 在 `site.yaml` 中设置 `theme.staticTemplate: pages/page_simple.html`
2. 从 5 个静态 HTML 文件中剥离 header/footer/cta 的内联代码，只保留 `<main>` 内的内容
3. 删除 `static/` 下的文件（如果不想保留独立纯 HTML）

***

### 路径对比

| 维度                | Filtered Lists（路径 A）                | Static Template（路径 B）          |
| ----------------- | ----------------------------------- | ------------------------------ |
| 适用场景              | 需要展示内容聚合列表的页面                       | 纯内容页面（about/contact/join）      |
| 解决页面              | china-companies, malaysia-companies | about, contact, join           |
| 复杂度               | 中（需改 5+ 文件）                         | 低（需改 1-2 文件）                   |
| 是否需 Pagination 适配 | 是（可先前端分页）                           | 否                              |
| 依赖                | 无                                   | 已有 `theme.staticTemplate` 配置模型 |

***

## 实施步骤

### 第一步：Filtered Lists 数据模型

| #   | 文件                   | 操作                                                                      |
| --- | -------------------- | ----------------------------------------------------------------------- |
| 1.1 | `AppConfig.cs`       | 新增 `FilteredListConfig` record，`CollectionConfig` 新增 `FilteredLists` 字段 |
| 1.2 | `ConfigLoader.cs`    | 新增 `ReadFilteredLists()` 方法，在 `ReadCollections()` 中调用                   |
| 1.3 | `ConfigValidator.cs` | 新增 `ValidateFilteredLists()` 校验方法                                       |

### 第二步：Filtered Lists 路由生成

| #   | 文件                                                      | 操作                                            |
| --- | ------------------------------------------------------- | --------------------------------------------- |
| 2.1 | `SiteEngine.cs — BuildListRoutesCore`                   | 为每个 FilteredList 生成列表路由                       |
| 2.2 | `PageRenderDispatcher.cs — BuildSpecialListDefinitions` | 为每个 FilteredList 创建过滤后的 SpecialListDefinition |

### 第三步：Filtered Lists 分页（前端侧）

| #   | 文件                                            | 操作                                                       |
| --- | --------------------------------------------- | -------------------------------------------------------- |
| 3.1 | `PageRenderDispatcher.cs`                     | 在 ListPageModel 中添加 `FilterField`/`FilterValue` 信息，供模板使用 |
| 3.2 | Starter theme `list.html` / `pagination.html` | （可选）提供前端分页示例                                             |

### 第四步：Static Template 渲染

| #   | 文件                                  | 操作                                            |
| --- | ----------------------------------- | --------------------------------------------- |
| 4.1 | `SiteEngine.cs — BuildVariantAsync` | 当 `staticTemplate` 非空时，渲染 static HTML 文件而非纯复制 |
| 4.2 | `SiteEngine.cs`                     | 新增 `RenderStaticFiles()` 私有方法                 |

### 第五步：测试

| #   | 操作                                   |
| --- | ------------------------------------ |
| 5.1 | 单元测试：FilteredList 配置加载/校验            |
| 5.2 | 单元测试：FilteredList 内容过滤               |
| 5.3 | 单元测试：StaticTemplate 渲染               |
| 5.4 | 集成测试：端到端 markdown 项目带 filtered lists |

### 第六步：丝路商讯项目迁移

| #   | 操作                                                |
| --- | ------------------------------------------------- |
| 6.1 | 在 `site.yaml` 添加 `filteredLists` 配置               |
| 6.2 | 设置 `theme.staticTemplate: pages/page_simple.html` |
| 6.3 | 简化 5 个 static HTML，移除 header/footer/cta 内联代码      |
| 6.4 | 验证构建输出路由表正确                                       |

***

## 涉及文件汇总

| 文件                                     | 变更类型      | 说明                                          |
| -------------------------------------- | --------- | ------------------------------------------- |
| `Bukit.Config/AppConfig.cs`            | 新增 record | `FilteredListConfig`                        |
| `Bukit.Config/ConfigLoader.cs`         | 新增方法      | `ReadFilteredLists()`                       |
| `Bukit.Config/ConfigValidator.cs`      | 新增方法      | `ValidateFilteredLists()`                   |
| `Bukit.Engine/SiteEngine.cs`           | 修改方法      | `BuildListRoutesCore` + `RenderStaticFiles` |
| `Bukit.Engine/PageRenderDispatcher.cs` | 修改方法      | `BuildSpecialListDefinitions`               |
| `Bukit.Engine/BuildVariantResult.cs`   | 可能扩展      | 如需携带 filtered route 信息                      |

***

## 向后兼容性

* 无 `filteredLists` 配置时，行为完全不变 ✅

* 无 `theme.staticTemplate` 配置时，static 目录行为完全不变 ✅

* 所有新配置字段均为可选 (`?`) ✅

* 现有测试全部通过 ✅

