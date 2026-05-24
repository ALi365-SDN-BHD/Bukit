# Bukit Taxonomy 分类系统深度分析报告

> 日期：2026-05-24
> 范围：Bukit 项目 taxonomy 模块全链路分析

---

## 一、实现架构总览

Bukit 的 taxonomy 是一个**多维度内容分类框架**，支持 `tags`、`categories` 以及任意自定义分类维度（通过 `kinds` 配置）。核心调用链路如下：

```
site.yaml (taxonomy 配置节)
       │
       ▼
ConfigLoader.cs              ConfigValidator.cs
(反序列化 TaxonomyConfig)     (校验所有 taxonomy 字段)
       │
       ▼
TaxonomyTermsInjector.cs (338行)
├── 从 mode:data 数据源注入 ensure terms
└── 从 Notion 数据库 schema 提取 select/multi_select/status options
       │
       ▼
TaxonomyPlugin.cs (1194行) — 核心引擎
├── BuildIndexCore()     → 按 key 聚合所有内容项的 taxonomy 数据
├── CreateKind()         → 生成索引页 + term页（含分页）
├── CreateIndexPage()    → /tags/、/categories/ 索引页
├── CreateTermPage()     → /tags/slug/ 等 term 详情页
├── SetTaxonomyData()    → context.Data["taxonomy"]
├── AfterBuild()         → 输出 taxonomy.json
└── ComparePages()       → 排序（置顶优先 > pinOrder > publishAt > title）
       │
       ▼
SeoAlternatesService.cs  → 生成 taxonomy 路由 URL 给 sitemap/SEO
```

### 关键数据模型

| 类型 | 位置 | 说明 |
|------|------|------|
| `TaxonomyConfig` | `AppConfig.cs:L280-L295` | 全局配置：模板、outputMode、pageSize、置顶等 |
| `TaxonomyTemplatesConfig` | `AppConfig.cs:L297-L301` | tags/categories 分层模板配置 |
| `TaxonomyKindConfig` | `AppConfig.cs:L303-L313` | 单个 kind 配置：key、title、模板路径 |
| `TaxonomyKindTemplateConfig` | `AppConfig.cs:L315-L320` | 模板路径配置 |
| `TaxonomyTerm` | `TaxonomyPlugin.cs:L1180-L1191` | term 运行时模型：DisplayName, Slug, Pages |
| `TaxonomyPage` | `TaxonomyPlugin.cs:L1193` | term 下的条目：Title, Url, PublishAt, Summary, Extra, IsPinned |

### 模板变量注入

**term 页可用变量：**

| 变量路径 | 类型 | 说明 |
|----------|------|------|
| `page.fields.items.value` | list | 当前 term 下的文章列表 |
| `page.fields.taxonomy.value` | object | `{ kind, term, slug, count }` |
| `page.fields.pagination.value` | object | `{ page, page_size, total, total_pages, has_prev, has_next }` |

**索引页可用变量：**

| 变量路径 | 类型 | 说明 |
|----------|------|------|
| `page.fields.terms.value` | list | 所有 term 列表 `[{ title, slug, url, count }]` |

**全局变量：**

| 变量路径 | 类型 | 说明 |
|----------|------|------|
| `site.data.taxonomy` | object | 完整 taxonomy 数据结构 |

### 产出物

| 文件 | 说明 |
|------|------|
| `<output>/tags/index.html` | 标签索引页 |
| `<output>/tags/<slug>/index.html` | 标签详情页（含分页） |
| `<output>/categories/index.html` | 分类索引页 |
| `<output>/categories/<slug>/index.html` | 分类详情页（含分页） |
| `<output>/taxonomy.json` | 结构化 JSON 数据 |

---

## 二、当前实现的优点

1. **多维度分类**：支持 tags、categories + 任意自定义 kinds，灵活性强
2. **模板优先级链**：kinds 模板 > templates.tags/categories > 全局 > convention > fallback，设计精巧
3. **多数据源支持**：通过 `PinFieldBySource`/`PinOrderFieldBySource` 实现 sourceKey 级别的置顶差异化
4. **Notion 深度集成**：
   - 自动提升 relation 为 taxonomy term
   - 从 Notion schema 注入 ensure terms（select/multi_select/status options）
5. **灵活的 OutputMode**：`both` / `pages` / `data` / `fields_only` 四种模式
6. **SEO 集成**：自动将 taxonomy 路由纳入 sitemap/alternates
7. **结构化 JSON 输出**：`taxonomy.json` 可用于前端 JS 搜索/过滤
8. **内置分页**：term 页支持 `/kind/slug/page/n/` 分页路由
9. **置顶排序**：支持 pinField、pinOrderField + 多数据源映射

---

## 三、存在的问题与改进空间

### 3.1 架构层面

| 问题 | 严重程度 | 说明 |
|------|----------|------|
| TaxonomyPlugin.cs 过于臃肿（1194行） | 🔴 高 | 单文件承担索引构建、页面生成、JSON 序列化、模板解析、排序、slug 生成、HTML 转义等 8+ 职责 |
| Slugify 实现重复 | 🟡 中 | `TaxonomyPlugin.Slugify()` 和 `TaxonomyTermsInjector.SlugifyTerm()` 代码几乎一致 |
| TaxonomyTerm 模型过于简陋 | 🟡 中 | 仅有 DisplayName + Slug，缺乏描述、图片、权重、层级等能力 |
| 无层次化分类支持 | 🟡 中 | Categories 完全扁平，不支持父子层级 |

### 3.2 功能缺失

| 缺失特性 | 影响范围 | 对标产品 |
|----------|----------|----------|
| Term 元数据（描述、图片、排序权重） | 无法为每个 term 定制显示 | Hugo, WordPress, Ghost |
| 层次化 taxonomy | 不可实现"科技 > 前端 > React"类层级导航 | Hugo, WordPress |
| RSS/Atom feeds for terms | 用户无法订阅特定分类的文章 | Hugo, Jekyll |
| Term 别名/重定向 | slug 变更后旧 URL 404 | Hugo (aliases), WordPress |
| Term 可见性控制 | 无法隐藏内部使用的 tag | Ghost (internal tags) |
| 非拉丁字符 slug | 中文 tag 变成空白 slug | Hugo (transliteration) |
| Term 排序权重 | term 在索引页只能按字母序排列 | Hugo (weight), WordPress (menu_order) |

---

## 四、与其他主流产品对比

| 特性 | Bukit | Hugo | Jekyll | Eleventy | Ghost | WordPress |
|------|:-----:|:----:|:------:|:--------:|:-----:|:---------:|
| **内置 tags/categories** | ✅ | ✅ | ✅ | ❌（手动） | ✅ | ✅ |
| **自定义 kinds** | ✅ | ✅ | ✅ | ✅（灵活） | ❌ | ✅ |
| **分页** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **层次化分类** | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ |
| **Term 元数据** | ❌ | ✅ | ⚠️ | ⚠️ | ✅ | ✅ |
| **Term RSS** | ❌ | ✅ | ⚠️ | ❌ | ✅ | ✅ |
| **Term 排序权重** | ❌ | ✅ | ❌ | ❌ | ❌ | ✅ |
| **Term 别名/重定向** | ❌ | ✅ | ⚠️ | ❌ | ❌ | ❌ |
| **非拉丁 slug** | ❌ | ✅ | ❌ | ❌ | ❌ | ⚠️ |
| **结构化 JSON 输出** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Notion 集成** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **置顶/排序** | ✅ | ⚠️ | ❌ | ❌ | ✅ | ✅ |
| **SSG 原生** | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |

### 对标分析结论

**Bukit 的独特优势**：
- 结构化 JSON 输出（`taxonomy.json`）是 Hugo 等竞品不具备的，对 headless/Jamstack 场景非常有价值
- Notion 深度集成也是差异化亮点

**最应追赶的方向**（按价值/成本排序）：

1. **Term 元数据**（高价值、低成本）— 让每个 term 拥有 `_index.md` 或 metadata 文件
2. **Term 排序权重**（高价值、低成本）— 在 term 模型中加 `weight` 字段
3. **层次化分类**（高价值、中等成本）— categories 支持 `parent` 字段
4. **RSS feeds for terms**（中等价值、低成本）— AfterBuild 阶段额外输出 RSS XML
5. **Slug 国际化**（中等价值、低成本）— 引入 transliteration 库

---

## 五、重构方案 → 已全部实施完成 ✅

> 实施日期：2026-05-24
> 最终测试：Shared 67/67, Engine 793/793, Content 451/451 — 全部通过

### 5.1 总体评估

> **需要重构，但应渐进式进行** — 已完成。

当前架构的**核心逻辑是正确的**，已通过职责分离和模型增强完成了优化。

### 5.2 已完成阶段

#### ✅ Phase 1：消除技术债

**1.1 提取 `Slugify` 到共享工具类** — 已完成

新增 `src/Bukit.Shared/SlugHelper.cs`（82行），合并三份重复实现：
- `TaxonomyPlugin.Slugify()` → `SlugHelper.Slugify()`
- `TaxonomyTermsInjector.SlugifyTerm()` → `SlugHelper.Slugify()`
- `SeoAlternatesService.SlugifySegment()` → `SlugHelper.Slugify()`

**1.2 拆分 `TaxonomyPlugin.cs`（1194 → 7 文件）** — 已完成

```
TaxonomyPlugin.cs (保留，220行)              — 插件入口 + 编排逻辑
TaxonomyIndexBuilder.cs (新建，271行)        — BuildIndexCore, GetOrBuildIndex, MergeEnsureTerms
TaxonomyPageCreator.cs (新建，302行)         — CreateKind, CreateIndexPage, CreateTermPage
TaxonomyDataWriter.cs (新建，260行)          — AfterBuild, BuildKindData, WriteKind, JSON helpers
TaxonomyTemplateResolver.cs (新建，52行)      — ResolveTemplates, EnsureTemplateExists, FirstNonEmpty
TaxonomySortHelper.cs (新建，114行)          — ComparePages, ComparePinOrder, TryGetPinned, ParseBoolLike
```

#### ✅ Phase 2：功能增强

**2.1 丰富 TaxonomyTerm 运行时模型** — 已完成

```csharp
public sealed class TaxonomyTerm
{
    public string DisplayName { get; }
    public string Slug { get; }
    public string? Description { get; init; }        // 新增
    public string? Image { get; init; }               // 新增
    public int Weight { get; init; }                  // 新增（排序权重，0=默认）
    public bool IsVisible { get; init; } = true;      // 新增（可见性控制）
    public string? ParentSlug { get; init; }          // 新增（层级父级）
    public IReadOnlyList<string>? Aliases { get; init; } // 新增（别名）
    public List<TaxonomyPage> Pages { get; init; }
}
```

**新模板变量（term 页）：**
| `page.fields.taxonomy.value.description` | term 描述 |
| `page.fields.taxonomy.value.image` | term 封面图 |
| `page.fields.taxonomy.value.weight` | term 权重 |
| `page.fields.taxonomy.value.parent` | 父 term slug |
| `page.fields.taxonomy.value.children` | 子 term slug 列表 |
| `page.fields.taxonomy.value.ancestors` | 祖先 term slug 列表（面包屑） |
| `page.fields.taxonomy.value.aliases` | 别名列表 |

**2.2 层次化分类** — 已完成

新增 `TaxonomyHierarchyBuilder.cs`（81行），根据 `ParentSlug` 计算 children 和 ancestors。

```yaml
# site.yaml 配置增强
taxonomy:
  kinds:
    - key: categories
      kind: categories
      hierarchical: true  # 新增配置字段
```

**2.3 Term 元数据加载** — 已完成

新增 `TaxonomyMetadataLoader.cs`（232行），支持两种加载源：
1. **data 数据源**：`content/data/<kind>.yaml` → `taxonomy_ensure_terms`
2. **_index.md 约定**（仿 Hugo）：`content/_taxonomy/<kind>/<slug>/_index.md`

#### ✅ Phase 3：高级特性

**3.1 RSS feeds for terms** — 已完成

新增 `TaxonomyFeedWriter.cs`（116行），为每个 term 生成独立 RSS 2.0 feed：
```
<output>/<kind>/<slug>/feed.xml
```

**3.2 Slug transliteration** — 已完成

`SlugHelper` 支持 Unicode NFD 分解（é→e, ñ→n 等）和常见拉丁字符映射（ß→ss, æ→ae, œ→oe, ø→o）。

**3.3 Term 别名重定向** — 已完成

新增 `TaxonomyRedirectWriter.cs`（69行），为 `Aliases` 生成 HTML meta refresh redirect 页面：
```
<output>/<kind>/<alias>/index.html → redirect to /<kind>/<slug>/
```

---

## 六、测试覆盖

### 最终测试结果（2026-05-24）

| 测试套件 | 通过 | 总计 |
|----------|------|------|
| Bukit.Shared.Tests | 67 | 67 |
| Bukit.Engine.Tests | 793 | 793 |
| Bukit.Content.Tests | 451 | 451 |

### 新增测试文件

| 文件 | 测试数 | 说明 |
|------|--------|------|
| `SlugHelperTests.cs` | 22 | slug 生成 + transliteration 全覆盖 |
| `TaxonomyHierarchyBuilderTests.cs` | 3 | 层级关系计算 |
| `TaxonomyMetadataLoaderTests.cs` | 6 | 元数据加载 + 前导标记解析 |
| `TaxonomyFeedWriterTests.cs` | 3 | RSS feed 生成 |
| `TaxonomyRedirectWriterTests.cs` | 4 | 别名重定向 |

---

## 七、新增文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `src/Bukit.Shared/SlugHelper.cs` | 77 | 共享 slug 生成 + 拉丁字符 transliteration |
| `src/Bukit.Engine/.../TaxonomyIndexBuilder.cs` | 271 | 索引构建 + ensure terms 合并 + pin 解析 |
| `src/Bukit.Engine/.../TaxonomyPageCreator.cs` | 302 | 页面生成（索引页 + term 页） |
| `src/Bukit.Engine/.../TaxonomyDataWriter.cs` | 260 | JSON 输出 + 结构化数据构建 |
| `src/Bukit.Engine/.../TaxonomyTemplateResolver.cs` | 52 | 模板优先级链解析 |
| `src/Bukit.Engine/.../TaxonomySortHelper.cs` | 114 | 排序 + 置顶 + 布尔解析 |
| `src/Bukit.Engine/.../TaxonomyHierarchyBuilder.cs` | 81 | 层次化关系计算 |
| `src/Bukit.Engine/.../TaxonomyMetadataLoader.cs` | 232 | Term 元数据加载 |
| `src/Bukit.Engine/.../TaxonomyFeedWriter.cs` | 116 | Per-term RSS 2.0 feed |
| `src/Bukit.Engine/.../TaxonomyRedirectWriter.cs` | 69 | 别名 HTML redirect |
