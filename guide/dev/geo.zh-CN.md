# GEO（生成式引擎优化）架构

本文档说明 Bukit 生成式引擎优化（GEO）系统的实现 —— 如何在构建过程中生成 llms.txt、AI 爬虫规则和 GEO 结构化数据。

实现参考：
- `src/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs`
- `src/Bukit.Cli/Commands/GeoCommand.cs`
- `src/Bukit.Engine/SeoModelBuilder.cs`（GEO Front Matter 解析）
- `src/Bukit.Config/AppConfig.cs`（SeoGeoConfig 模型）

相关文档：[内置插件](./built-in-plugins.zh-CN.md)、[SEO 与 i18n](./i18n-seo.zh-CN.md)、[用户指南：17 GEO](../../guide/user/17-geo.zh-CN.md)

## 概述

GEO 在传统 SEO 的基础上扩展，增加了针对 AI 驱动搜索引擎（ChatGPT Search、Perplexity、Google AI Overviews、Bing Copilot）优化的产物和结构化数据。共分三层：

1. **静态产物** — `llms.txt`、`llms-full.txt`、AI 爬虫 `robots.txt` 规则
2. **结构化数据** — 来自 Front Matter 的 FAQPage、HowTo、Person、Article、Speakable JSON-LD
3. **审计诊断** — 7 个 `geo.*` 诊断码 + GEO 评分

## 配置模型

所有 GEO 配置位于 `site.seo.geo` 下：

| 字段 | 类型 | 默认值 | 实现位置 |
|------|------|------|------|
| `enabled` | bool | `true` | LlmsTxtPlugin、SeoModelBuilder |
| `llmsTxt` | bool | `true` | LlmsTxtPlugin |
| `llmsFullTxt` | bool | `false` | LlmsTxtPlugin |
| `llmsTxtMaxArticles` | int | `20` | LlmsTxtPlugin |
| `aiBotMode` | string | `"allow"` | LlmsTxtPlugin (robots.txt) |
| `aiBotAllowList` | string[] | — | LlmsTxtPlugin |
| `aiBotBlockList` | string[] | — | LlmsTxtPlugin |
| `llmsTxtOptionalLinks` | array | — | LlmsTxtPlugin |

## 构建管线

### 1. 内容加载

GEO Front Matter 在内容加载期间通过 `SeoModelBuilder` 解析。Front Matter 中的 `geo:` 键被读取为结构化对象。此阶段不需要对内容加载做任何改动。

### 2. 派生页面阶段

此阶段没有 GEO 特定工作。GEO 仅在 after-build 阶段运作。

### 3. After-Build 阶段

`LlmsTxtPlugin.AfterBuild(context)` 执行：

1. **检查启用状态**：如果 `!geo.Enabled` 则立即返回
2. **llms.txt 生成**（如果 `geo.LlmsTxt`）：
   - 遍历 `context.Routed` + `context.DerivedRouted`
   - 通过 `context.SeoIndex` 过滤到 `Indexable` 条目
   - 分组为 **文档**（非 post 页面）和 **文章**（post，按 `PublishAt` 降序排列）
   - 将文章数量限制为 `geo.LlmsTxtMaxArticles`
   - 追加来自 `geo.LlmsTxtOptionalLinks` 的 **可选** 节
   - 写入 `<outputDir>/llms.txt`
3. **llms-full.txt 生成**（如果 `geo.LlmsFullTxt`）：
   - 遍历所有可索引路由
   - 从内容中去除 HTML 标签
   - 用 `---` 分隔符连接
   - 写入 `<outputDir>/llms-full.txt`
4. **AI 爬虫规则**（追加到 `robots.txt` 或内联）：
   - 识别 12 个 AI 机器人 user-agent
   - 应用 `allow`/`block`/`selective` 模式

### 4. SEO 索引集成

`LlmsTxtPlugin` 复用现有的 `context.SeoIndex`（由 `SeoIndexBuilder` 构建）来决定哪些页面可索引。带有 `robots: noindex` 的页面会被从 llms.txt 中排除。

## Front Matter GEO 模型

从内容 Front Matter 的 `geo:` 键下解析。实现在 `SeoModelBuilder` 中：

| Front Matter 字段 | 类型 | Schema.org 输出 |
|------|------|-----------------|
| `schema_type` | string | 覆盖 `@type`：BlogPosting（默认）、Article、NewsArticle、FAQPage、HowTo |
| `faq` | {question, answer} 数组 | `FAQPage` 含 `Question`/`Answer` 项 |
| `steps` | {name, text, image?, url?} 数组 | `HowTo` 含 `HowToStep` 项 |
| `author` | {name, url, same_as} | `Person` 含 `sameAs` 链接 |
| `citations` | {title, url} 数组 | `WebPage` 含 `mentions` |
| `same_as` | string[] | 主要实体的 `sameAs` |
| `about` | string | `about` 属性 |
| `date_reviewed` | string | `dateReviewed`（ISO 8601） |
| `speakable.xpath` | string | `SpeakableSpecification` |

## GEO 审计

实现：`src/Bukit.Cli/Commands/GeoCommand.cs`

读取构建审计产物（主要是 `.bukit/publish-audit-report.json`，以及作为 SEO 兼容视图的 `.bukit/seo-report.json`）并计算：

### GEO 评分（0–100）

| 评分标准 | 最高分 | 来源 |
|-----------|-----------|--------|
| llms.txt 已生成 | 25 | 文件存在性检查 |
| llms-full.txt 已生成 | 15 | 文件存在性检查 |
| 至少 1 条 GEO 增强路由 | 10 | 路由元数据检查 |
| 文章 Schema 覆盖率 | 15 | GEO 路由与总路由的比例 |
| 使用了 FAQPage/HowTo | 15 | Schema 类型检测 |
| Person 作者 Schema | 10 | 作者字段存在性 |
| SpeakableSpecification | 5 | XPath 字段存在性 |
| 多路由 GEO 覆盖 | 5 | GEO 路由数量 > 1 |

### 诊断码

在 `bukit build` 诊断期间生成（当 `site.seo.diagnostics` 为 `warn` 或 `strict` 时）：

| 码 | 严重级别 | 触发条件 |
|------|---------|---------|
| `geo.llms_txt_missing` | warning | GEO 已启用但未找到 llms.txt |
| `geo.llms_full_txt_missing` | warning | llmsFullTxt 已启用但未找到文件 |
| `geo.schema_type_missing` | info | 内容有发布日期但没有 GEO 字段 |
| `geo.faq_empty_question` | error | FAQ 项有空间题 |
| `geo.faq_empty_answer` | error | FAQ 项有空答案 |
| `geo.howto_step_empty_name` | error | HowTo 步骤有空名称 |
| `geo.howto_step_empty_text` | error | HowTo 步骤有空文本 |
| `geo.citation_url_invalid` | warning | 引用 URL 不是绝对地址 |
| `geo.author_no_sameas` | info | 定义了作者但没有 sameAs 链接 |
| `geo.speakable_path_invalid` | warning | XPath 不以 `/` 开头 |

## AI 爬虫机器人列表

硬编码在 `LlmsTxtPlugin` 中：

```csharp
static readonly string[] AiBots = {
    "GPTBot", "ChatGPT-User",            // OpenAI
    "Google-Extended",                    // Google AI
    "Claude-Web", "ClaudeBot", "Anthropic-AI",  // Anthropic
    "PerplexityBot",                      // Perplexity
    "Cohere-AI",                          // Cohere
    "CCBot", "Diffbot",                   // Common Crawl / Diffbot
    "FacebookBot",                        // Meta
    "OAI-SearchBot"                       // OpenAI Search
};
```

robots.txt 规则生成逻辑：

| `aiBotMode` | 对每个机器人 | 未列出的机器人 |
|------------|-------------|--------------|
| `allow` | `Allow: /` | （无规则） |
| `block` | `Disallow: /` | （无规则） |
| `selective` | 在 `aiBotAllowList` 中则 Allow, 在 `aiBotBlockList` 中则 Disallow | `Disallow: /` |

## CLI 入口

| 命令 | 用途 | 关键选项 |
|---------|------|---------|
| `bukit build` | 构建并生成 GEO 产物 | （读取 site.seo.geo 配置） |
| `bukit geo audit` | 审计现有 dist 的 GEO 就绪度 | `--dir <path>` |

GEO 审计从构建输出目录读取已生成的审计报告。不需要重新构建；更完整的机器可读与可信发布门禁请使用 `bukit publish audit`。

## 文件输出

| 文件 | 插件 | 所需配置 |
|------|--------|----------------|
| `llms.txt` | LlmsTxtPlugin | `geo.enabled && geo.llmsTxt` |
| `llms-full.txt` | LlmsTxtPlugin | `geo.enabled && geo.llmsFullTxt` |
| `robots.txt`（AI 规则） | LlmsTxtPlugin | `geo.enabled && seo.robotsTxt.enabled` |

llms.txt 内容结构遵循 [llmstxt.org](https://llmstxt.org) 规范：`# 标题` → `> 描述` → `## 文档` → `## 文章` → `## 可选`。
