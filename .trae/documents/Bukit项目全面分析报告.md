# Bukit 项目全面分析报告

> **生成日期**：2026-05-05 | **审计范围**：全仓库 | **受众**：维护者  
> **执行方法**：源码优先 + 构建/测试验证 + 文档逐项核对

---

## 一、项目现状摘要

**Bukit** 是一个基于 .NET 10 的静态站点生成器，支持 Markdown / Notion 双内容源、Scriban 模板、多语言、增量构建、插件扩展与 Native AOT 发布。

### 1.1 仓库真实边界

| 维度 | 实际情况 |
|---|---|
| 解决方案 | `bukit.slnx`（唯一有效解决方案） |
| src 工程 | 8 个：`Bukit.Cli`、`Bukit.Config`、`Bukit.Content`、`Bukit.Engine`、`Bukit.Engine.Abstractions`、`Bukit.Rendering`、`Bukit.Routing`、`Bukit.Shared` |
| 内嵌依赖 | `tools/scriban/`（Scriban 模板引擎源码） |
| 内置插件 | 7 个：Archive / CollectionRouteIndex / PagesIndex / Pagination / Rss / SearchIndex / Sitemap / Taxonomy |
| 示例插件 | 2 个：SampleAfterBuildPlugin、PathReportPlugin |
| 测试项目 | 4 个：Bukit.Cli.Tests、Bukit.Content.Tests、Bukit.Engine.Tests、Bukit.Rendering.Tests |
| CI/CD 资产 | `.github/workflows/` **不存在** |
| BukitJalil | `src/BukitJalil/` **不存在**；`BukitJalil.slnx` **不存在** |

### 1.2 构建与测试状态

| 验证项 | 结果 |
|---|---|
| `dotnet build bukit.slnx -c Release` | ✅ 通过（0 错误） |
| `dotnet test Bukit.Engine.Tests` | ✅ 202 passed, 0 failed |
| `dotnet test Bukit.Content.Tests` | ✅ 66 passed, 0 failed |
| `dotnet test Bukit.Cli.Tests` | ✅ 36 passed, 0 failed |
| `dotnet test Bukit.Rendering.Tests` | ✅ 18 passed, 0 failed |
| `bukit doctor --config examples/starter/site.yaml` | ✅ Passed |
| `bukit build --config examples/starter/site.yaml --clean` | ✅ 5 items → 15 pages, 7 plugins executed |

**测试总计：322 个测试全部通过，无失败。**

---

## 二、架构真实性结论

### 2.1 主数据流 (源码验证通过)

```
CLI (Program.cs → ArgReader → BukitCliSpecs)
  → ConfigLoader.Load(site.yaml) → AppConfig
  → ConfigApplier.Apply(config, overrides) → effectiveConfig
  → ConfigValidator.Validate(effectiveConfig)
  → SiteEngine.BuildAsync(effectiveConfig, rootDir, overrides)
      → ContentProviderFactory.Create → MarkdownFolderProvider / NotionContentProvider / CompositeContentProvider
      → ContentLoadResult (Items + BodyStore)
      → I18nOutputMerger (语言检测与分流)
      → BuildVariantAsync (per-language)
          → RouteGenerator.Generate (ContentItem → RouteInfo)
          → TaxonomyTermsInjector (分类注入)
          → DataModuleBuilder (site.modules)
          → PluginRunner.RunDerivePagesAsync (派生页)
          → PageRenderDispatcher.RenderPagesAsync (并行渲染)
          → DirectoryCopy.Sync (assets 拷贝)
          → PluginRunner.RunAfterBuildAsync (sitemap/rss/search/taxonomy)
      → I18nOutputMerger.GenerateRootOutputs
      → MetricsWriter.WriteIfRequested
  → dist/ 输出
```

### 2.2 正文加载模型：延迟读取（已验证）

当前 `ContentItem` 的 `ContentHtml` 为 **nullable**，`BodyKey` 为 **nullable**。MarkdownFolderProvider 创建 item 时 `ContentHtml: null`，`BodyKey: file`（文件路径），正文通过 `IContentBodyStore.GetAsync(item)` 按需读取和渲染。

这与架构文档 (`architecture.md`) 和评审文档 (`architecture-review.md`) 的描述完全一致。**不存在"正文全量常驻内存"问题。**

### 2.3 SiteEngine：薄编排器（已验证）

`SiteEngine.cs` 约 409 行，将路由、渲染、模块构建、分类注入、插件执行、输出合并、指标写入等工作委托给专职组件。已从过去的 God Class 成功拆分。

### 2.4 路由模型：collections 优先 + 兼容层

路由决策优先级：
1. `route` override（来自 ContentItem meta）
2. `collections` 规则（来自 site.yaml → site.collections）
3. `permalinks` 模式匹配
4. 默认 post/page 规则

`collections` 已是主路径，`post/page` 为兼容层。与架构文档一致。

### 2.5 AOT 兼容性

`TimeZoneCompatibility.cs` 对 `Asia/Shanghai` → `China Standard Time` 做显式回退，处理 Windows NativeAOT + `InvariantGlobalization=true` 下的 IANA 时区兼容问题。这是已知约束，已有防护措施。

---

## 三、主要问题清单（按严重度排序）

### 🔴 高严重度

#### H1. `new-developer-30min.md` 中 BukitJalil 残留描述误导新开发者

- **证据路径**：[guide/dev/new-developer-30min.md](file:///e:/Github/Bukit/guide/dev/new-developer-30min.md) — 第 7-10 行（§1.1）、第 206-213 行（§8.1）
- **具体内容**：
  - §1.1: "仓库有两条主线：Bukit 是底层静态站点引擎，BukitJalil 是上层桌面 AI 建站工具"
  - §8.1: "正确理解是：Bukit 是核心引擎，BukitJalil 是上层产品"
  - §5 的时间表中提到"建立两条产品线"
  - §4.2.4 也引用了 "BukitJalil"
- **影响**：新开发者接受错误的双产品线心智模型，浪费时间在不存在 `src/BukitJalil/` 中搜索代码
- **根因**：该文档在 BukitJalil 从仓库移除后，§1.1 和 §8.1 未同步更新（尽管 §9.5 已添加边界说明，但矛盾和误导仍然存在）
- **分类**：文档失真风险
- **修复方案**：将 §1.1 改为 "当前仓库聚焦 Bukit 主线"，删除 §8.1 中 "BukitJalil 是上层产品" 的表述

---

### 🟡 中严重度

#### M1. `maintainer-entrypoints.md` 中 "BukitJalil 改动" 入口残留

- **证据路径**：[guide/dev/maintainer-entrypoints.md](file:///e:/Github/Bukit/guide/dev/maintainer-entrypoints.md) — 第 27 行
- **具体内容**：§1 改动类型列表中包含 "改 BukitJalil 的 AI、桌面流程或与 Bukit 的桥接"
- **影响**：维护者在定位入口时可能误判改动范围
- **根因**：尽管 §8 "仓库边界说明" 已正确声明不含 BukitJalil，但 §1 的目录列表未同步清理
- **分类**：文档失真风险
- **修复方案**：从 §1 的改动类型列表中删除 BukitJalil 条目

#### M2. 交付治理缺口：`.github/workflows/` 目录缺失

- **证据路径**：仓库根目录无 `.github/` 目录
- **影响**：
  - 无法通过 GitHub Actions 自动执行 CI/CD
  - 文档（README、publish-deploy、user/13）虽已说明"未内置 workflow"，但仍缺少可直接复用的参考 workflow
  - smoke 脚本只能本地手动执行，无法作为 CI 门禁
- **根因**：项目历史中去除了 workflow 资产
- **分类**：交付治理风险
- **修复方案**：考虑提供一个可选的 `pages.yml` 模板（放在 `docs/` 或 `examples/` 中），让用户自行复制到 `.github/workflows/`

#### M3. 测试覆盖缺口：关键路径缺少直接测试

以下核心组件缺少单元测试：

| 缺失测试的组件 | 风险 |
|---|---|
| `SiteEngine.cs` | 构建总控流程无直接测试，回归只能靠 smoke |
| `IncrementalBuildEngine.cs` | 增量构建核心引擎无测试 |
| `RssGenerator.cs` | RSS 生成器完全无测试 |
| `SitemapGenerator.cs` | 站点地图生成器无测试（仅 SitemapPolicy 有测试） |
| `BuildCommand.cs` | 核心 CLI 命令无直接测试 |
| `PreviewCommand.cs` | Preview 命令无测试 |
| `NotionContentProvider.cs` | Notion 主提供者无直接测试 |
| `ScribanModelBinder.cs` | Scriban 模型绑定无测试 |
| `DataModuleBuilder.cs` | 数据模块构建无测试 |
| `I18nOutputMerger.cs` | 国际化输出合并无测试 |

- **分类**：测试与验证风险
- **修复方案**：优先为 `SiteEngine`（集成测试）、`RssGenerator`、`SitemapGenerator`、`BuildCommand` 补充测试

---

### 🟢 低严重度

#### L1. `code-wiki.md` 目录树中使用 "WeBukit" 作为根目录名

- **证据路径**：[guide/dev/code-wiki.md](file:///e:/Github/Bukit/guide/dev/code-wiki.md) — 第 12 行
- **具体内容**：目录树 `├─ src/` 上方标注为 `WeBukit`
- **影响**：轻微混淆（仓库名为 Bukit）
- **分类**：文档不精确
- **修复方案**：将 `WeBukit` 改为 `Bukit` 或使用通用占位符

#### L2. `Bukit.PluginSourceGenerator` 和 `Bukit.Shared` 无任何测试覆盖

- **分类**：测试覆盖薄弱
- **影响**：低（这两个项目的代码量小、逻辑简单，但仍无回归保护）

---

## 四、架构优势（无需改动）

以下设计亮点应继续保持：

1. **单向依赖清晰**：CLI → Engine → Content/Routing/Rendering，无反向依赖
2. **正文延迟读取模型**：`ContentHtml: null` + `BodyKey` + `IContentBodyStore`，超大规模友好
3. **插件体系**：derive-pages / after-build 两阶段生命周期，配合冲突策略（fail/warn/last-wins）和失败模式（strict/warn），治理完整
4. **路由分层**：route override → collections → permalinks → defaults，灵活且可预测
5. **配置覆盖机制**：CLI 参数优先级高于 site.yaml，校验针对合并后的最终配置
6. **AOT 兼容**：显式时区回退、WASM 协议约束、外部程序集治理
7. **增量构建**：基于 hash 的跳过机制 + build-manifest，可量化渲染/跳过统计
8. **Scriban 内嵌**：`tools/scriban/` 源码内嵌，保证 AOT 兼容和版本可控

---

## 五、优先级路线图

### 短期（本周可完成）

| 优先级 | 行动 | 类型 |
|---|---|---|
| P0 | 修正 `new-developer-30min.md` §1.1 和 §8.1 中 BukitJalil 残留 | 文档修复 |
| P1 | 从 `maintainer-entrypoints.md` §1 删除 BukitJalil 条目 | 文档修复 |
| P2 | 将 `code-wiki.md` 中 `WeBukit` 改为 `Bukit` | 文档修复 |

### 中期（下一迭代）

| 优先级 | 行动 | 类型 |
|---|---|---|
| P3 | 为 `SiteEngine` 添加集成测试（至少覆盖主链路） | 测试加固 |
| P4 | 为 `RssGenerator`、`SitemapGenerator` 添加单元测试 | 测试加固 |
| P5 | 为 `BuildCommand`、`PreviewCommand` 添加测试 | 测试加固 |
| P6 | 提供可复用的 `pages.yml` 模板（`docs/` 或 `examples/` 中） | 交付治理 |

---

## 六、最终判断

**Bukit 不是一个架构混乱的项目。** 相反，它拥有清晰的分层、健康的单向依赖、成熟的插件体系和良好的 AOT 兼容策略。

当前阶段的核心挑战已经从"核心代码是否可靠"转移到：
1. **文档一致性治理**：少数文档的 BukitJalil 残留描述需要清理
2. **测试深度**：关键编排器和输出生成器缺少直接测试
3. **交付治理**：CI/CD 资产缺失使本地与线上验证链路不闭环

以上问题均为"治理型"而非"架构型"，修复成本低、收益明确。
