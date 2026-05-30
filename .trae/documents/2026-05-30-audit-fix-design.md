# Audit Report Fix Design

> 来源：`.trae/documents/bukit-audit-report-2026-05-30-chatgpt-02.md`
> 状态：设计完成，待用户评审
> 排除：P0-1（CI 覆盖率阈值）暂不做处理

---

## 一、范围概览

| 优先级 | 编号 | 问题 | 分类 |
|--------|------|------|------|
| P0 | P0-2 | BodyCache BodyKey 多源碰撞 | Content Core |
| P0 | P0-3 | RenderDependencyHasher 覆盖不足 | 增量构建 |
| P1 | P1-1 | propertyMap 缺 SEO 字段 | Notion |
| P1 | P1-2 | propertyMap 与 includeSlugs 分离 | Notion |
| P1 | P1-3 | CollectionWarningStage 未检测 type/collection 冲突 | 引擎 |
| P1 | P1-4 | template doctor 未实现 | CLI |
| P1 | P1-5 | route inspect 未实现 | CLI |
| P2 | P2-1 | CI workflow run 确认 | 工程 |
| P2 | P2-2 | DataCommand 输出增强 | CLI |

---

## 二、逐项设计

### P0-2：BodyCache BodyKey 多源碰撞

**根因**：`CompositeContentProvider` 在聚合多源 item 时只重写了 `Id`（加 `sourceKey:` 前缀），未处理 `BodyKey`。`BodyCacheDecorator` 使用 `BodyKey ?? Id` 作为缓存 key，当两个源的 item 有相同 BodyKey 时（如 `index.md`），缓存会错误复用第一个源的 body。

**涉及文件**：
- `src/Bukit.Content/CompositeContentProvider.cs` — L59-63（主路径）、L75-86（addToCollections 路径）

**修复策略**：

1. **主路径**（L59-63）：生成 item 投影时，重写 BodyKey：
   ```
   BodyKey = item.BodyKey is null
       ? $"{sourceKey}:{item.Id}"
       : $"{sourceKey}:{item.BodyKey}"
   ```
   格式为 `sourceKey:原始Key`。

2. **addToCollections 路径**（L75-86）：复制品使用与主路径**相同的 BodyKey**（共享 body 缓存），但 Id 仍加 `:extraCollection` 后缀以区分路由：
   ```
   // addToCollections 复制品：
   Id = $"{sourceKey}:{item.Id}:{extraCollection.Trim()}"
   BodyKey = 与主路径相同（共享）
   ```

**测试要求**：
- `BodyCacheDecoratorTests` 新增 2 个测试：
  - Composite sources with same BodyKey should not share cached body
  - addToCollections duplicated route should share same source body safely

**风险**：低。BodyKey 改写是确定性的，不影响现有单源场景。

---

### P0-3：RenderDependencyHasher 覆盖补强

**根因**：当前 hash 采用逐个字段手写方式，导致遗漏大量影响构建输出的配置字段。

**涉及文件**：
- `src/Bukit.Engine/Incremental/RenderDependencyHasher.cs`
- `src/Bukit.Config/AppConfig.cs` — CollectionConfig、TaxonomyConfig、TaxonomyKindConfig 等

**修复策略**：采用**结构化方法**——为每个配置类型编写专门的 `AppendStableXxx` 方法，覆盖完整子结构。

**Collection 补强（新增 fields）**：

| 子结构 | 覆盖字段 |
|--------|----------|
| `Pagination` | `Enabled`, `PageSize`, `UrlPattern`, `FirstPageUsesListRoute` |
| `Output` | `Rss`, `Sitemap`, `Archive`, `FeedPath`, `FeedTitle`, `FeedDescription` |
| `ArchiveDetail` | `Depth`, `Template`, `RoutePrefix` |
| `FilteredLists` | 每个条目 `Field`, `Value`, `ListRoute`, `ListTemplate`（按 Field 排序） |
| `Schema` | 每个定义 `Name`, `Type`, `Label`, `Format`, `Enum` (排序), `Min`, `Max`, `Required`, `Default`（按 Name 排序） |
| `SchemaFailMode` | 字符串值 |

**Taxonomy 补强（新增 fields）**：

| 子结构 | 覆盖字段 |
|--------|----------|
| 顶层 | `Template`, `IndexTemplate`, `TermTemplate`, `OutputMode`, `PageSize`, `IndexEnabled`, `PinField`, `PinOrderField` |
| `ItemFields` | 排序后逐个追加 |
| `PinFieldBySource` | 按 key 排序后逐对追加 |
| `PinOrderFieldBySource` | 按 key 排序后逐对追加 |
| `Templates.Tags` | `Template`, `IndexTemplate`, `TermTemplate` |
| `Templates.Categories` | `Template`, `IndexTemplate`, `TermTemplate` |
| `Kinds[]` | 每个 kind 的 `Kind`, `Title`, `SingularTitlePrefix`, `Template`, `IndexTemplate`, `TermTemplate`, `IndexEnabled`, `Hierarchical`（按 Key 排序） |

**实施方式**：
- 在 `RenderDependencyHasher.Compute()` 中调用新的 `AppendStableCollectionConfig` 和 `AppendStableTaxonomyConfig`
- 所有集合按字母序排序后序列化（确保确定性）
- null 值跳过或写空标记

**测试要求**：
- 为每个新增字段编写验证：修改该字段 → hash 变化
- 覆盖：Pagination.Enabled、Output.Rss、Taxonomy.PageSize、Taxonomy.OutputMode、Taxonomy.Templates 等关键路径

**风险**：中。新增 hash 字段会导致增量构建缓存全部失效一次（预期行为）。需要仔细处理 null 和默认值，避免不必要的失效。

---

### P1-1：propertyMap SEO 字段补全

**根因**：`NotionPropertyMapConfig` 只有 8 个通用字段，缺少 SEO 映射字段。

**涉及文件**：
- `src/Bukit.Config/AppConfig.cs` — `NotionPropertyMapConfig`
- `src/Bukit.Content/Notion/NotionPropertyParser.cs` — 属性提取逻辑

**修复策略**：

1. 在 `NotionPropertyMapConfig` 新增 4 个字段：
   ```csharp
   public string? SeoTitle { get; init; }
   public string? SeoDescription { get; init; }
   public string? SeoImage { get; init; }
   public string? Canonical { get; init; }
   ```

2. 在 `NotionPropertyParser` 中将映射值标准化写入：
   - `meta["seo_title"]` ← SeoTitle 映射的属性值
   - `meta["seo_desc"]` ← SeoDescription 映射的属性值
   - `meta["seo_image"]` ← SeoImage 映射的属性值
   - `meta["canonical"]` ← Canonical 映射的属性值

**测试要求**：验证 propertyMap 设置后，提取的 item meta 包含对应标准化字段。

**风险**：低。纯增量字段，不影响现有行为。

---

### P1-2：propertyMap.Slug 作为 includeSlugProperty fallback

**根因**：`NotionDatabaseSchemaResolver` 解析 `includeSlugProperty` 时有独立 fallback 链 `IncludeSlugProperty ?? "Slug"`，不回退到 `propertyMap.Slug`。

**涉及文件**：
- `src/Bukit.Content/Notion/NotionDatabaseSchemaResolver.cs` L17

**修复策略**：修改 fallback 链为三级：

```
options.IncludeSlugProperty ?? options.PropertyMap?.Slug ?? "Slug"
```

即：显式设置 → propertyMap.Slug → "Slug" 默认。

**测试要求**：
- 只设 propertyMap.Slug 不设 IncludeSlugProperty → 使用 propertyMap.Slug
- 两者都设 → IncludeSlugProperty 优先
- 都不设 → 回退到 "Slug"

**风险**：极低。单行修改，逻辑清晰。

---

### P1-3：CollectionWarningStage 检测 type/collection 冲突

**根因**：当前只检测"有 type 无 collection"的 legacy 场景，不检测"type 与 collection 同时存在且冲突"的场景。

**涉及文件**：
- `src/Bukit.Engine/Stages/CollectionWarningStage.cs`

**修复策略**：在现有循环中新增检测分支：

```csharp
if (hasCollection && (typeVal.Equals("post", ...) || typeVal.Equals("page", ...)))
{
    input.Logger.Warn(
        $"[WARN] Content \"{item.Id}\" defines both type={typeVal} and collection={collectionVal}. " +
        "Collection routing takes precedence; type is treated as legacy metadata.");
}
```

**测试要求**：验证冲突场景输出正确 warning，不冲突场景不误报。

**风险**：极低。纯新增 warning，不影响构建流程。

---

### P1-4：template doctor 实现

**根因**：`TemplateCommand.validate` 只做 Scriban 语法检查，缺少更深层的模板正确性校验。

**涉及文件**：
- `src/Bukit.Cli/Commands/DoctorCommand.cs` — 扩展现有 doctor 命令
- 可能新增 `src/Bukit.Cli/Commands/DoctorTemplateChecker.cs` — 模板校验逻辑

**修复策略**：在 `DoctorCommand` 中新增模板校验阶段，或在 `TemplateCommand` 中新增 `doctor` 子命令。建议加入 DoctorCommand（与其他 doctor 检查统一入口）：

| 检查项 | 实现方式 |
|--------|----------|
| Include 文件存在性 | 正则提取 `{{ include "xxx" }}`，检查主题 layouts 目录下是否存在 |
| Layout 文件存在性 | 解析 frontmatter `_layout`，检查 layouts 目录下是否存在 |
| Page fields vs schema | 仅对有 collection 的模板，提取 `page.fields.xxx`，与 collection schema 交叉验证 |
| Site modules vs data | 提取 `site.modules.xxx` 或 `site.Data.xxx`，与 data source 对比 |
| 上下文类型正确性 | 在 list/taxonomy/search 模板中检查是否误用了 `page`（应为 `this`）|

**测试要求**：每种检查类型至少 1 个正向和 1 个负向测试。

**风险**：低-中。新增检查不影响构建，纯诊断。正则提取可能误匹配，需测试边界。

---

### P1-5：route inspect 实现

**根因**：路由信息无 CLI 调试入口，`RouteGenerator.GenerateWithSource` 已有路由来源枚举但未暴露。

**涉及文件**：
- 新增 `src/Bukit.Cli/Commands/RouteCommand.cs`
- `src/Bukit.Cli/Program.cs` — 注册新命令

**修复策略**：新建 `RouteCommand`（遵循 `DataCommand`/`TemplateCommand` 的 CliBoundCommand 模式）：

| 子命令 | 参数 | 输出 |
|--------|------|------|
| `route inspect` | 无 | 表格：url / outputPath / template / collection / type / language / routeSource |
| `route inspect --json` | `--json` | JSON 数组 |
| `route inspect --collection <name>` | `--collection` | 仅该 collection 的路由 |

**实现要点**：
- 复用现有 `RouteGenerator.GenerateWithSource` 获取路由及来源信息
- `RouteSource` 枚举值：FullOverride / PartialOverride / Collection / Permalink / BuiltinFallback
- 同时标记 derived（来自插件）和 plugin 来源

**测试要求**：
- `RouteCommandTests`：验证子命令解析、输出格式
- 集成测试：验证实际路由输出正确

**风险**：低。新增只读命令，不影响构建。

---

### P2-1：CI workflow run 确认

**内容**：确认最新 commit 是否已触发并通过 CI。

**建议**：在仓库设置中要求 `build-and-test` 和 `aot-check` 必须通过才能合并。

**风险**：无代码修改，仅流程确认。

---

### P2-2：DataCommand 输出增强

**根因**：`data inspect` 输出维度偏少，缺少 source 元数据。

**涉及文件**：
- `src/Bukit.Cli/Commands/DataCommand.cs` — `PrintModuleSummary` 方法

**修复策略**：在 `PrintModuleSummary` 输出中增加列：

| 新增列 | 来源 |
|--------|------|
| `sourceKey` | `meta["sourceKey"]` |
| `sourceMode` | `meta["sourceMode"]` |
| `language` | `item.Language` 或 `meta["language"]` |
| `field count` | `fields.Count` |

输出格式示例：
```
Data modules:
  testimonials   ×3  source=notion   mode=full    language=en  fields=4  [author, content, rating, ...]
  faq            ×5  source=local    mode=merge   language=zh  fields=2  [answer, question]
```

如果所有 item 同语言则显示 `language=zh`，如果混合则显示 `language=mixed`。

**测试要求**：验证 summary 输出包含新列。

**风险**：极低。纯输出格式增强。

---

## 三、实施顺序建议

| 阶段 | 问题 | 理由 |
|------|------|------|
| **Phase 1** | P0-2, P0-3 | 构建正确性优先 |
| **Phase 2** | P1-1, P1-2, P1-3 | Notion + 引擎，有上游依赖关系 |
| **Phase 3** | P1-4, P1-5, P2-2 | CLI 增强，独立模块 |
| **Phase 4** | P2-1 | 流程确认 |
