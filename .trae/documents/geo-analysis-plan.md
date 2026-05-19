# Bukit 生成式引擎优化（GEO）分析与实现方案

## 1. 概述

生成式引擎优化（Generative Engine Optimization, GEO）是针对 AI 驱动的生成式搜索引擎（如 ChatGPT Search、Perplexity、Google AI Overviews、Bing Copilot 等）优化网站内容的一套技术。与传统 SEO 关注搜索排名不同，GEO 关注内容是否能被 AI 引擎准确抓取、理解、引用和摘要。

本方案分析 Bukit 现有 SEO 架构，识别 GEO 所需能力的差距，并设计增量实现路线。

## 2. 当前状态分析

### 2.1 现有 SEO 架构（已具备的 GEO 基础）

Bukit 已有相对完整的传统 SEO 实现，这些也是 GEO 的基础：

| 组件 | 文件 | GEO 相关性 |
|------|------|-----------|
| 元标签渲染 | [SeoHtmlRenderer.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/SeoHtmlRenderer.cs) | 高 — OG、Twitter Card、description 等是 AI 引擎提取摘要的重要来源 |
| JSON-LD 结构化数据 | [SeoModelBuilder.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/SeoModelBuilder.cs#L145-L288) | 高 — 已生成 WebSite、Organization、WebPage/CollectionPage、BreadcrumbList、BlogPosting、ItemList |
| SEO 模型 | [Models.cs:45-87](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Rendering/Models.cs#L45-L87) | 中 — SeoModel、SeoOpenGraphModel、SeoArticleModel 等 |
| SEO 配置 | [AppConfig.cs:45-81](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/AppConfig.cs#L45-L81) | 高 — SeoConfig.Enabled、Schema 开关、Organization 配置 |
| SEO 模板片段 | [SeoPartial.html](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Resources/StarterTheme/SeoPartial.html) | 中 — Scriban 模板端渲染 SEO 标签 |
| SEO 诊断/审计 | [SeoDiagnostics.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/SeoDiagnostics.cs) / [SeoCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/SeoCommand.cs) | 高 — 支持 `bukit seo audit`、`bukit seo diff` |
| robots.txt | [SeoRobotsTxtConfig](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/AppConfig.cs#L71-L74) | 中 |
| Sitemap 插件 | [SitemapPlugin.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/SitemapPlugin.cs) | 中 — AI 爬虫也读 sitemap |
| RSS 插件 | [RssPlugin.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/RssPlugin.cs) | 低 |

### 2.2 插件系统能力

Bukit 有灵活的插件架构（[PluginRunner.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/PluginRunner.cs)），支持 `IDerivePagesPlugin` 和 `IAfterBuildPlugin` 两种 Hook 点：
- **derive-pages**：可生成额外页面内容
- **after-build**：可在构建完成后生成额外文件

这对实现 GEO 产出物（如 `llms.txt`、AI 爬虫 robots 规则）非常有利。

## 3. GEO 差距分析

### 3.1 缺失的结构化数据类型

| Schema 类型 | 当前状态 | GEO 重要性 | 说明 |
|------------|---------|-----------|------|
| `FAQPage` | **缺失** | 高 | AI Overviews 直接引用 FAQ 内容 |
| `HowTo` | **缺失** | 高 | 步骤型教程直接被 AI 摘要 |
| `Article` / `NewsArticle` | 仅有 `BlogPosting` | 中 | 为普通文章/新闻提供更精确的类型 |
| `Person` | **缺失** | 中 | 作者实体信息，AI 用于引用和信誉评估 |
| `Speakable` | **缺失** | 中 | 标记适合语音朗读的部分 |
| `sameAs` | **缺失** | 高 | 链接到维基百科、社交媒体等，建立实体关联 |
| `citation` / `mentions` | **缺失** | 高 | AI 引擎偏好的引用型内容 |
| `about` / `subjectOf` | **缺失** | 中 | 内容主题描述 |

### 3.2 缺少的 AI 爬虫支持

| 能力 | 当前状态 | GEO 重要性 |
|------|---------|-----------|
| AI 爬虫专属 robots.txt 规则 | **缺失** | 高 |
| `llms.txt` / `llms-full.txt` | **缺失** | 高 |
| AI 爬虫 user-agent 识别（GPTBot、Claude-Web、PerplexityBot 等） | **缺失** | 中 |

### 3.3 内容层面缺失

| 能力 | 当前状态 | GEO 重要性 |
|------|---------|-----------|
| 摘要/摘要结构化 | 仅 `site.description` 和 `seo_desc` | 中 |
| 引用与来源标记 | 无 | 高 |
| 内容新鲜度信号（`dateModified`、`dateReviewed`） | `BlogPosting` 部分支持 | 中 |
| 作者信誉（作者页、bio、sameAs） | `article:author` 仅含名字 | 中 |
| FAQ 问答结构 | 无 | 高 |

## 4. 实现方案

### 4.1 总体策略

采用**渐进式、向后兼容**的方式扩展：

**阶段 1**：低风险、高价值的静态产出物（无需内容格式变更）
**阶段 2**：内容 Front Matter 扩展，让用户显式标注 GEO 信息
**阶段 3**：诊断与审计增强

### 4.2 阶段 1：GA 产出物（`llms.txt` 与 AI 爬虫规则）

#### 4.2.1 `llms.txt` 生成（新增插件）

在 `src/Bukit.Engine/Plugins/BuiltIn/` 新增 `LlmsTxtPlugin.cs`：

```
实现 IAfterBuildPlugin
├── 遍历 SeoIndex 中的可索引路由
├── 生成 llms.txt（结构化导航，Markdown 格式）
│   ├── # 站点名称
│   ├── > 站点描述
│   ├── ## 核心页面（链接 + 描述）
│   ├── ## 文章/博客（按时间排序的最近 N 篇）
│   └── ## 可选外部链接（文档、社交媒体等）
└── 输出到 dist/llms.txt
```

`llms.txt` 是 Markdown 格式的标准，目前被 Anthropic、OpenAI 等多家公司的爬虫采纳。

示例输出：
```markdown
# Bukit Docs
> Static site generator for modern web.

## Quick Start
- [Getting Started](https://bukit.dev/docs/quick-start/): 5-minute setup guide.
## Documentation
- [Configuration](https://bukit.dev/docs/config/): Full site.yaml reference.
```

#### 4.2.2 `llms-full.txt` 生成

与 `llms.txt` 一起生成 `llms-full.txt`，包含所有可索引页面**完整的 Markdown 渲染内容**（不包含 HTML 模板），方便 AI 引擎在训练/检索中完整理解站点内容。

#### 4.2.3 AI 爬虫 robots.txt 规则

扩展 `SeoRobotsTxtConfig` 配置模型，新增：
- `aiBotMode`: `"allow"` | `"block"` | `"selective"`

生成 robots.txt 时自动追加 AI 爬虫指令：
```
User-agent: GPTBot
Disallow: /

User-agent: Claude-Web
Disallow: /

User-agent: PerplexityBot
Allow: /
```

#### 4.2.4 配置扩展

在 `SeoConfig` 中新增：

```csharp
public sealed record SeoConfig
{
    // existing...
    public SeoGeoConfig Geo { get; init; } = new();
}

public sealed record SeoGeoConfig
{
    public bool Enabled { get; init; } = true;       // 总开关
    public bool LlmsTxt { get; init; } = true;        // 是否生成 llms.txt
    public bool LlmsFullTxt { get; init; } = false;   // 是否生成 llms-full.txt（默认关，文件较大）
    public int LlmsTxtMaxArticles { get; init; } = 20; // llms.txt 最多列出多少篇文章
    public string AiBotMode { get; init; } = "allow";  // allow | block | selective
    public IReadOnlyList<string>? AiBotAllowList { get; init; } // 白名单爬虫列表
    public IReadOnlyList<string>? AiBotBlockList { get; init; } // 黑名单爬虫列表
}
```

### 4.3 阶段 2：内容层面的 GEO 增强

#### 4.3.1 Front Matter 扩展

用户可在内容 Front Matter 中声明 GEO 相关元数据：

```yaml
---
title: 如何用 Bukit 构建博客
type: post
geo:
  schema_type: HowTo        # BlogPosting | Article | NewsArticle | FAQPage | HowTo
  faq:                       # FAQPage 模式
    - question: Bukit 支持哪些内容源？
      answer: Notion、Markdown 和本地文件。
    - question: 如何部署？
      answer: 支持 GitHub Pages、Vercel、Netlify 等。
  citations:                 # 引用来源
    - title: Schema.org HowTo
      url: https://schema.org/HowTo
  same_as:                   # 实体关联
    - https://github.com/user/repo
    - https://twitter.com/user
  author:                     # 扩展作者信息
    name: 张三
    url: https://example.com/about
    same_as:
      - https://github.com/zhangsan
      - https://linkedin.com/in/zhangsan
  speakable:                  # 语音朗读标记
    xpath: /html/body/article
---
```

#### 4.3.2 JSON-LD 扩展

在 `SeoModelBuilder.BuildJsonLd()` 中新增 Schema 类型生成：

- **FAQPage**：从 `geo.faq` 字段生成
- **HowTo**：从 `geo.steps` 字段生成
- **Person + Author**：从 `geo.author` 字段生成，含 `sameAs` 关联
- **Article / NewsArticle**：根据 `geo.schema_type` 精确选择类型
- **Speakable**：生成 `speakable` 标注

#### 4.3.3 `SeoModel` 扩展

```csharp
public sealed record SeoModel
{
    // existing...
    public string? SchemaType { get; init; }          // "BlogPosting" | "Article" | "NewsArticle" | "FAQPage" | "HowTo"
    public IReadOnlyList<GeoFaqModel>? FaqItems { get; init; }
    public IReadOnlyList<GeoHowToStepModel>? HowToSteps { get; init; }
    public IReadOnlyList<GeoCitationModel>? Citations { get; init; }
    public GeoAuthorModel? GeoAuthor { get; init; }
    public string? SpeakableXPath { get; init; }
    public IReadOnlyList<string>? SameAs { get; init; }
}
```

### 4.4 阶段 3：GEO 诊断与审计

#### 4.4.1 新增诊断规则

在 `SeoDiagnostics.cs` 中新增 GEO 相关检查：

| 诊断码 | 严重度 | 说明 |
|--------|--------|------|
| `geo.llms_txt_missing` | warning | 未生成 llms.txt |
| `geo.schema_type_missing` | info | 页面未声明 schema_type |
| `geo.author_no_sameas` | info | 作者缺少 sameAs 关联 |
| `geo.faq_empty_question` | error | FAQ 项缺少问题文本 |
| `geo.faq_empty_answer` | error | FAQ 项缺少回答文本 |
| `geo.citation_url_invalid` | warning | 引用的 URL 格式无效 |
| `geo.speakable_path_invalid` | warning | speakable XPath 表达式无效 |

#### 4.4.2 SEO 审计报告扩展

在 SEO 报告 JSON 中增加 `geo` 字段：
```json
{
  "schema": "...",
  "summary": {
    "geoScore": 75,
    "llmsTxtGenerated": true,
    ...
  }
}
```

### 4.5 模板更新

更新 [SeoPartial.html](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Resources/StarterTheme/SeoPartial.html) 以渲染新增的 GEO 标签。

### 4.6 CLI 命令扩展

新增 `bukit geo audit` 子命令，专门检查 GEO 指标。

## 5. 实现路线图

| 阶段 | 内容 | 改动文件 | 优先级 |
|------|------|---------|--------|
| 1a | `SeoGeoConfig` 配置模型 | `AppConfig.cs` | P0 |
| 1b | `LlmsTxtPlugin` 插件（llms.txt + llms-full.txt） | 新文件 `LlmsTxtPlugin.cs` | P0 |
| 1c | AI 爬虫 robots.txt 规则 | `AppConfig.cs` + robots.txt 生成逻辑 | P1 |
| 2a | GEO Front Matter 字段解析 | `SeoModelBuilder.cs` + 元数据解析 | P1 |
| 2b | JSON-LD 扩展（FAQPage/HowTo/Person/Article） | `SeoModelBuilder.cs` | P1 |
| 2c | `SeoModel` 与 `Models.cs` 扩展 | `Models.cs` | P1 |
| 2d | Scriban 模板片段更新 | `SeoPartial.html` | P2 |
| 3a | GEO 诊断规则 | `SeoDiagnostics.cs` | P2 |
| 3b | SEO 报告扩展 | `SeoAuditReportWriter.cs` | P2 |
| 3c | `bukit geo audit` CLI | `GeoCommand.cs` | P2 |
| 4 | 用户文档 (zh-CN / MS / EN) | `guide/user/11-i18n-seo.*.md` | P2 |

## 6. 假设与决策

1. **`llms.txt` 标准**：采用 [llmstxt.org](https://llmstxt.org) 规范，这是目前最被广泛采纳的 LLM 可读站点地图标准
2. **向后兼容**：所有新配置项默认值保持现有行为不变（`GEO.Enabled = true` 仅控制 `llms.txt` 和 AI 爬虫规则，不影响已有 SEO 输出）
3. **插件架构**：`llms.txt` 生成作为内置插件实现，遵循现有 `IAfterBuildPlugin` 模式，用户可通过 `site.plugins.lmstxt.enabled` 开关
4. **Schema 类型选择**：用户通过 Front Matter 的 `geo.schema_type` 显式声明，引擎不做自动推断
5. **增量构建兼容**：`llms.txt` 和 `llms-full.txt` 在每次全量构建时重新生成

## 7. 验证步骤

1. 单元测试：验证 `LlmsTxtPlugin` 输出格式
2. 集成测试：端到端构建验证 `dist/llms.txt` 存在且内容正确
3. SEO 审计：运行 `bukit seo audit` 确认新增诊断规则生效
4. 格式检查：`dotnet build` + `dotnet format --verify-no-changes`
5. 用户文档审查：确认三种语言的文档一致且准确

## 8. 不在此范围内

- AI 爬虫流量分析/日志——属于运维/分析层面
- LLM 训练数据提交 API——不在 SSG 职责范围
- 内容自动改写为 AI 友好格式——保持内容由作者控制
- 第三方 Schema 验证服务集成——可作为后续独立功能
