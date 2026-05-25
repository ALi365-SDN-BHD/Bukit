# Bukit SEO/GEO Engine P0 开发任务书

> 目标：为 Bukit 增加一套可落地、可测试、可扩展的 SEO/GEO 基础引擎，使 Bukit 构建出的静态网站天然具备标准 SEO 能力，并为后续 AI Search / GEO / Notion 内容自动化打基础。  
> 执行对象：Codex  
> 项目：Bukit 静态网站生成引擎  
> 阶段：P0 基础能力实现  
> 优先级：高  
> 输出要求：必须修改代码、补充测试、更新文档，并保证现有功能不破坏。

---

## 1. 背景说明

Bukit 当前定位为 .NET 静态网站生成引擎，未来需要服务于：

- 企业官网
- 文档站
- 内容站
- Notion CMS 内容发布
- AI 自动生成网站
- SEO 内容生产
- GEO / AI Search 优化
- BukitJalil AI 控制面板

BukitJalil 的上层流程是：

```text
Idea → AI Generation → Static Build → Deployment
```

Bukit 在该流程中负责：

```text
Structured Data / Content / Theme
  ↓
Static HTML Build
  ↓
SEO-ready Output
```

因此 Bukit 必须具备标准 SEO 输出能力，而不是依赖每个主题自行实现 SEO。

---

## 2. 本次开发目标

本次只实现 P0 基础能力，不做复杂 AI 生成，不做 Search Console 接入，不做站群。

### 2.1 必须实现

1. SEO Metadata 数据模型
2. 页面 `<head>` SEO 标签注入
3. OpenGraph 标签注入
4. Twitter Card 标签注入
5. Canonical URL 支持
6. Robots Meta 支持
7. `sitemap.xml` 生成
8. `robots.txt` 生成
9. JSON-LD 结构化数据生成
   - Article / BlogPosting
   - Organization
   - BreadcrumbList
10. SEO Audit 构建检测
11. 单元测试 / 快照测试 / 集成测试
12. 文档更新

### 2.2 暂不实现

以下内容本次不要做：

- AI 自动生成文章
- 关键词采集
- Search Console API 接入
- 自动外链
- 自动伪原创
- 复杂 GEO 算法
- 内容自动刷新
- 多站点 SEO 网络
- SaaS 功能
- BukitJalil UI 页面

---

## 3. 最高优先级原则

### 3.1 不破坏现有构建流程

必须保证现有：

```bash
dotnet build
dotnet test
bukit build
```

继续可用。

如果当前仓库中命令名称不同，按实际项目命令执行，但必须在最终报告中说明。

### 3.2 SEO 是核心输出能力，不是主题私有逻辑

不能让每个主题重复写一套 SEO 逻辑。

正确方式：

```text
Page Data
  ↓
SeoMetadata Resolver
  ↓
Render Context
  ↓
Theme Layout / Head Partial
  ↓
Final HTML
```

### 3.3 默认值必须安全

当页面没有显式 SEO 字段时，必须有合理 fallback。

例如：

```text
seo.title        → page.title → site.title
seo.description  → page.summary → site.description
canonical        → site.baseUrl + page.url
og:title         → seo.title
og:description   → seo.description
og:image         → page.cover → site.ogImage
robots           → index,follow
```

### 3.4 不允许生成错误 sitemap

如果页面是：

```text
draft
published = false
robots = noindex
url 为空
canonical 为空且无法推导
```

则不得进入 sitemap。

### 3.5 JSON-LD 必须可关闭

站点配置或页面配置中必须允许关闭 JSON-LD：

```yaml
seo:
  jsonLd:
    enabled: true
```

页面级别可覆盖：

```yaml
seo:
  schema:
    enabled: false
```

---

## 4. 推荐目录结构

请先检查当前 Bukit 仓库结构，再按项目实际结构调整。

如果当前是多项目结构，推荐新增：

```text
src/
├── Bukit.Core/
├── Bukit.Rendering/
├── Bukit.Seo/
│   ├── Abstractions/
│   ├── Metadata/
│   ├── Schema/
│   ├── Sitemap/
│   ├── Robots/
│   ├── Audit/
│   └── Extensions/
└── Bukit.Tests/
```

如果当前是单项目结构，推荐新增：

```text
src/Bukit/
├── Seo/
│   ├── Abstractions/
│   ├── Metadata/
│   ├── Schema/
│   ├── Sitemap/
│   ├── Robots/
│   ├── Audit/
│   └── Extensions/
```

测试目录建议：

```text
tests/
├── Bukit.Seo.Tests/
│   ├── Metadata/
│   ├── Schema/
│   ├── Sitemap/
│   ├── Robots/
│   └── Audit/
```

---

## 5. 配置设计

### 5.1 站点级配置

在站点配置中增加 SEO 配置。

示例：

```yaml
site:
  title: "Example Site"
  description: "Example site description"
  baseUrl: "https://example.com"
  language: "en"
  logo: "/assets/logo.png"
  defaultOgImage: "/assets/og-default.jpg"

seo:
  enabled: true

  defaults:
    robots: "index,follow"
    twitterCard: "summary_large_image"
    changefreq: "weekly"
    priority: 0.7

  sitemap:
    enabled: true
    filename: "sitemap.xml"

  robotsTxt:
    enabled: true
    filename: "robots.txt"
    rules:
      - userAgent: "*"
        allow:
          - "/"
        disallow:
          - "/drafts/"
          - "/admin/"

  jsonLd:
    enabled: true
    organization:
      enabled: true
      name: "Example Company"
      url: "https://example.com"
      logo: "https://example.com/assets/logo.png"
      sameAs: []

  audit:
    enabled: true
    failOnError: false
    reportFile: "seo-report.json"
```

### 5.2 页面级 Front Matter

Markdown 页面示例：

```yaml
---
title: "Malaysia ESD Guide"
summary: "A practical guide to Malaysia ESD company registration and Employment Pass application."
slug: "malaysia-esd-guide"
published: true
date: "2026-05-26"
updated: "2026-05-26"
type: "post"
language: "en"
cover: "/images/esd-guide.jpg"

seo:
  title: "Malaysia ESD Guide: Company Registration and Employment Pass"
  description: "Learn how Malaysia ESD company registration works, required documents, Employment Pass categories, and common approval issues."
  canonical: "https://example.com/blog/malaysia-esd-guide/"
  robots: "index,follow"
  ogTitle: "Malaysia ESD Guide"
  ogDescription: "Complete guide to Malaysia ESD and Employment Pass applications."
  ogImage: "https://example.com/images/esd-guide.jpg"
  twitterCard: "summary_large_image"
  schemaType: "Article"
  sitemap:
    include: true
    changefreq: "weekly"
    priority: 0.8
---
```

### 5.3 Notion 字段映射预留

如果项目中已有 Notion 数据源，请映射以下字段：

```text
SEO Title        → seo.title
SEO Description  → seo.description
Canonical URL    → seo.canonical
Robots           → seo.robots
OG Image         → seo.ogImage
Schema Type      → seo.schemaType
Published        → published
PublishAt        → date
UpdatedAt        → updated
Summary          → summary
Cover            → cover
Slug             → slug
Language         → language
```

本次不要求完整重构 Notion Provider，但必须预留扩展点，避免未来重复改动。

---

## 6. 数据模型设计

### 6.1 SeoMetadata

新增：

```csharp
public sealed class SeoMetadata
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? CanonicalUrl { get; set; }
    public string Robots { get; set; } = "index,follow";

    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }
    public string? OgImage { get; set; }

    public string TwitterCard { get; set; } = "summary_large_image";

    public string? SchemaType { get; set; }

    public SitemapMetadata Sitemap { get; set; } = new();
}
```

### 6.2 SitemapMetadata

```csharp
public sealed class SitemapMetadata
{
    public bool Include { get; set; } = true;
    public string ChangeFrequency { get; set; } = "weekly";
    public double Priority { get; set; } = 0.7;
}
```

### 6.3 SiteSeoOptions

```csharp
public sealed class SiteSeoOptions
{
    public bool Enabled { get; set; } = true;
    public SeoDefaultOptions Defaults { get; set; } = new();
    public SitemapOptions Sitemap { get; set; } = new();
    public RobotsTxtOptions RobotsTxt { get; set; } = new();
    public JsonLdOptions JsonLd { get; set; } = new();
    public SeoAuditOptions Audit { get; set; } = new();
}
```

### 6.4 SeoResolvedMetadata

新增一个解析后的只读模型，供渲染和生成器使用。

```csharp
public sealed class SeoResolvedMetadata
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string CanonicalUrl { get; init; }
    public required string Robots { get; init; }

    public required string OgTitle { get; init; }
    public required string OgDescription { get; init; }
    public string? OgImage { get; init; }

    public required string TwitterCard { get; init; }

    public string? SchemaType { get; init; }
}
```

---

## 7. SEO Metadata 解析规则

新增服务：

```csharp
public interface ISeoMetadataResolver
{
    SeoResolvedMetadata Resolve(SiteContext site, PageContext page);
}
```

### 7.1 Title 解析顺序

```text
page.seo.title
page.title
site.title
```

如果最终为空，记录 SEO Audit Error。

### 7.2 Description 解析顺序

```text
page.seo.description
page.summary
page.excerpt
site.description
```

如果最终为空，记录 SEO Audit Error。

### 7.3 Canonical URL 解析顺序

```text
page.seo.canonical
site.baseUrl + page.url
```

规则：

- 必须是绝对 URL
- 不允许为空
- 不允许包含重复斜杠，例如 `https://example.com//blog/a/`
- 末尾斜杠遵循项目当前 URL 策略
- 如果 `baseUrl` 为空，允许构建但必须产生 Warning

### 7.4 Robots 解析顺序

```text
page.seo.robots
site.seo.defaults.robots
"index,follow"
```

可接受值：

```text
index,follow
index,nofollow
noindex,follow
noindex,nofollow
```

如果出现未知值，记录 Warning，并 fallback 到 `index,follow`。

### 7.5 OpenGraph 解析规则

```text
og:title       → page.seo.ogTitle → resolved.title
og:description → page.seo.ogDescription → resolved.description
og:image       → page.seo.ogImage → page.cover → site.defaultOgImage
og:url         → resolved.canonicalUrl
og:type        → article for post, website for homepage/page
```

### 7.6 Twitter Card 解析规则

```text
twitter:card        → page.seo.twitterCard → site default → summary_large_image
twitter:title       → resolved.title
twitter:description → resolved.description
twitter:image       → resolved.ogImage
```

---

## 8. HTML Head 输出要求

最终 HTML `<head>` 必须包含：

```html
<title>...</title>
<meta name="description" content="...">
<link rel="canonical" href="...">
<meta name="robots" content="index,follow">

<meta property="og:title" content="...">
<meta property="og:description" content="...">
<meta property="og:url" content="...">
<meta property="og:type" content="article">
<meta property="og:image" content="...">

<meta name="twitter:card" content="summary_large_image">
<meta name="twitter:title" content="...">
<meta name="twitter:description" content="...">
<meta name="twitter:image" content="...">
```

### 8.1 转义规则

必须 HTML Encode：

- title
- description
- og:title
- og:description
- twitter:title
- twitter:description

必须保证输出不会破坏 HTML：

```text
" → &quot;
< → &lt;
> → &gt;
& → &amp;
```

### 8.2 空字段处理

如果图片为空，不输出：

```html
<meta property="og:image">
<meta name="twitter:image">
```

不要输出空 content。

---

## 9. Sitemap 生成器

新增：

```csharp
public interface ISitemapGenerator
{
    Task GenerateAsync(SiteBuildContext context, CancellationToken cancellationToken = default);
}
```

### 9.1 输出位置

默认输出：

```text
public/sitemap.xml
```

或当前构建输出目录：

```text
{output}/sitemap.xml
```

如果项目现有输出目录不是 `public`，按项目实际输出目录处理。

### 9.2 URL 收录规则

收录页面必须满足：

```text
published = true
draft != true
page.url 不为空
seo.sitemap.include = true
robots 不包含 noindex
canonicalUrl 不为空
```

### 9.3 XML 格式

输出：

```xml
<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>https://example.com/blog/malaysia-esd-guide/</loc>
    <lastmod>2026-05-26</lastmod>
    <changefreq>weekly</changefreq>
    <priority>0.8</priority>
  </url>
</urlset>
```

### 9.4 字段规则

| 字段 | 来源 |
|---|---|
| loc | resolved.canonicalUrl |
| lastmod | page.updated → page.date → build date |
| changefreq | page.seo.sitemap.changefreq → site default |
| priority | page.seo.sitemap.priority → site default |

### 9.5 XML 安全

必须 XML Encode：

- loc

日期必须为：

```text
YYYY-MM-DD
```

或 ISO 8601。

### 9.6 大站点预留

本次不强制实现 sitemap index，但代码结构要预留：

```text
SitemapIndexGenerator
```

未来用于超过 50,000 URL 或大文件拆分。

---

## 10. Robots.txt 生成器

新增：

```csharp
public interface IRobotsTxtGenerator
{
    Task GenerateAsync(SiteBuildContext context, CancellationToken cancellationToken = default);
}
```

### 10.1 输出位置

默认：

```text
{output}/robots.txt
```

### 10.2 默认输出

如果未配置：

```text
User-agent: *
Allow: /

Sitemap: https://example.com/sitemap.xml
```

如果 `site.baseUrl` 为空，则不输出 Sitemap 行，并产生 Warning。

### 10.3 配置示例

```yaml
seo:
  robotsTxt:
    enabled: true
    rules:
      - userAgent: "*"
        allow:
          - "/"
        disallow:
          - "/drafts/"
          - "/admin/"
```

输出：

```text
User-agent: *
Allow: /
Disallow: /drafts/
Disallow: /admin/

Sitemap: https://example.com/sitemap.xml
```

### 10.4 注意

robots.txt 不是防止页面进入 Google 索引的安全机制。页面不希望被索引时，应该使用：

```html
<meta name="robots" content="noindex,follow">
```

---

## 11. JSON-LD 结构化数据

新增：

```csharp
public interface IJsonLdBuilder
{
    string? Build(SiteContext site, PageContext page, SeoResolvedMetadata seo);
}
```

### 11.1 输出方式

在页面 HTML 中输出：

```html
<script type="application/ld+json">
{ ... }
</script>
```

必须使用 JSON Serializer 生成，不要手写拼接 JSON 字符串。

### 11.2 Article / BlogPosting

适用：

```text
page.type = post
seo.schemaType = Article
seo.schemaType = BlogPosting
```

输出示例：

```json
{
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "Malaysia ESD Guide",
  "description": "A practical guide to Malaysia ESD company registration.",
  "image": "https://example.com/images/esd-guide.jpg",
  "datePublished": "2026-05-26",
  "dateModified": "2026-05-26",
  "author": {
    "@type": "Person",
    "name": "Author Name"
  },
  "mainEntityOfPage": {
    "@type": "WebPage",
    "@id": "https://example.com/blog/malaysia-esd-guide/"
  }
}
```

字段规则：

| JSON-LD 字段 | 来源 |
|---|---|
| headline | seo.title |
| description | seo.description |
| image | seo.ogImage |
| datePublished | page.date |
| dateModified | page.updated → page.date |
| author.name | page.author → site.author |
| mainEntityOfPage.@id | seo.canonicalUrl |

### 11.3 Organization

适用：

```text
homepage
site.seo.jsonLd.organization.enabled = true
```

输出示例：

```json
{
  "@context": "https://schema.org",
  "@type": "Organization",
  "name": "Example Company",
  "url": "https://example.com",
  "logo": "https://example.com/assets/logo.png",
  "sameAs": []
}
```

字段规则：

| JSON-LD 字段 | 来源 |
|---|---|
| name | site.seo.jsonLd.organization.name → site.title |
| url | site.baseUrl |
| logo | site.logo |
| sameAs | config |

### 11.4 BreadcrumbList

适用：

```text
所有非首页页面，且 page.breadcrumbs 存在或可由路径推导
```

输出示例：

```json
{
  "@context": "https://schema.org",
  "@type": "BreadcrumbList",
  "itemListElement": [
    {
      "@type": "ListItem",
      "position": 1,
      "name": "Home",
      "item": "https://example.com/"
    },
    {
      "@type": "ListItem",
      "position": 2,
      "name": "Blog",
      "item": "https://example.com/blog/"
    },
    {
      "@type": "ListItem",
      "position": 3,
      "name": "Malaysia ESD Guide",
      "item": "https://example.com/blog/malaysia-esd-guide/"
    }
  ]
}
```

### 11.5 多个 JSON-LD

允许一个页面输出多个 JSON-LD script：

```text
Article + BreadcrumbList
Organization + WebSite
```

本次必须至少支持：

```text
Article + BreadcrumbList
Organization
```

---

## 12. SEO Audit

新增：

```csharp
public interface ISeoAuditService
{
    SeoAuditReport Audit(SiteBuildContext context);
}
```

### 12.1 SEO Issue 模型

```csharp
public sealed class SeoIssue
{
    public required string Code { get; init; }
    public required SeoIssueSeverity Severity { get; init; }
    public required string PageUrl { get; init; }
    public required string Message { get; init; }
}

public enum SeoIssueSeverity
{
    Info,
    Warning,
    Error
}
```

### 12.2 Audit Report

```csharp
public sealed class SeoAuditReport
{
    public int PagesScanned { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public IReadOnlyList<SeoIssue> Issues { get; init; } = [];
}
```

### 12.3 检查规则

| Code | 规则 | 等级 |
|---|---|---|
| SEO_TITLE_MISSING | title 为空 | Error |
| SEO_DESCRIPTION_MISSING | description 为空 | Error |
| SEO_CANONICAL_MISSING | canonical 为空 | Warning |
| SEO_CANONICAL_INVALID | canonical 不是绝对 URL | Error |
| SEO_ROBOTS_INVALID | robots 值非法 | Warning |
| SEO_TITLE_TOO_LONG | title 长度 > 60 | Warning |
| SEO_DESCRIPTION_TOO_LONG | description 长度 > 160 | Warning |
| SEO_DUPLICATE_TITLE | 多页面 title 重复 | Warning |
| SEO_DUPLICATE_DESCRIPTION | 多页面 description 重复 | Warning |
| SEO_SITEMAP_NOINDEX_CONFLICT | noindex 页面进入 sitemap | Error |
| SEO_SLUG_MISSING | slug/url 为空 | Error |
| SEO_OG_IMAGE_MISSING | og:image 缺失 | Info |
| SEO_SCHEMA_MISSING | 文章页缺少 Article Schema | Warning |
| SEO_BASE_URL_MISSING | baseUrl 缺失，无法生成绝对 URL | Warning |
| SEO_H1_INVALID | HTML 中 H1 数量不是 1 | Warning |
| SEO_IMAGE_ALT_MISSING | 图片缺少 alt | Warning |
| SEO_BROKEN_INTERNAL_LINK | 内链不存在 | Error |

### 12.4 H1 和图片 alt 检查

如果当前构建流程容易拿到最终 HTML，则检查最终 HTML。

如果当前流程难以拿到最终 HTML，本次可以先实现可扩展接口：

```csharp
public interface IHtmlSeoAnalyzer
{
    IReadOnlyList<SeoIssue> Analyze(string html, PageContext page);
}
```

并完成：

- H1 数量检查
- img alt 检查
- internal link 检查

### 12.5 输出文件

默认输出：

```text
{output}/seo-report.json
```

格式：

```json
{
  "pagesScanned": 128,
  "errorCount": 3,
  "warningCount": 21,
  "issues": [
    {
      "code": "SEO_DESCRIPTION_MISSING",
      "severity": "Error",
      "pageUrl": "/blog/example/",
      "message": "Meta description is missing."
    }
  ]
}
```

同时在控制台输出摘要：

```text
SEO Audit Summary
-----------------
Pages scanned: 128
Errors: 3
Warnings: 21
Report: public/seo-report.json
```

### 12.6 failOnError

配置：

```yaml
seo:
  audit:
    failOnError: true
```

当 `failOnError = true` 且存在 Error 时，构建失败。

默认：

```yaml
failOnError: false
```

避免影响已有项目。

---

## 13. 渲染集成要求

### 13.1 Render Context 增加 SEO 对象

页面模板中应该能访问：

```text
page.seo.title
page.seo.description
page.seo.canonicalUrl
page.seo.robots
page.seo.ogTitle
page.seo.ogDescription
page.seo.ogImage
page.seo.twitterCard
page.seo.schemaType
```

或者：

```text
seo.title
seo.description
seo.canonicalUrl
```

具体命名按当前模板系统风格决定，但必须统一。

### 13.2 推荐提供 Head Partial

如果当前使用 Scriban，建议提供官方 partial：

```text
partials/seo-head.html
```

示例：

```scriban
<title>{{ seo.title }}</title>
<meta name="description" content="{{ seo.description | html.escape }}">
<link rel="canonical" href="{{ seo.canonical_url }}">
<meta name="robots" content="{{ seo.robots }}">

<meta property="og:title" content="{{ seo.og_title | html.escape }}">
<meta property="og:description" content="{{ seo.og_description | html.escape }}">
<meta property="og:url" content="{{ seo.canonical_url }}">
<meta property="og:type" content="{{ seo.og_type }}">

{{ if seo.og_image }}
<meta property="og:image" content="{{ seo.og_image }}">
{{ end }}

<meta name="twitter:card" content="{{ seo.twitter_card }}">
<meta name="twitter:title" content="{{ seo.title | html.escape }}">
<meta name="twitter:description" content="{{ seo.description | html.escape }}">

{{ if seo.og_image }}
<meta name="twitter:image" content="{{ seo.og_image }}">
{{ end }}

{{ if seo.json_ld }}
<script type="application/ld+json">
{{ seo.json_ld }}
</script>
{{ end }}
```

注意：实际 filter 名称按项目当前 Scriban 配置调整。

---

## 14. 构建流程集成

构建流程建议：

```text
Load Site Config
  ↓
Load Content
  ↓
Resolve Routes
  ↓
Resolve SEO Metadata
  ↓
Render HTML
  ↓
Generate JSON-LD
  ↓
Write Pages
  ↓
Generate Sitemap
  ↓
Generate Robots.txt
  ↓
Run SEO Audit
  ↓
Write SEO Report
```

如果当前流程更适合在渲染前生成 JSON-LD，可以调整，但必须保证最终 HTML 有正确输出。

---

## 15. 测试要求

### 15.1 单元测试

必须新增测试：

```text
SeoMetadataResolverTests
SitemapGeneratorTests
RobotsTxtGeneratorTests
JsonLdBuilderTests
SeoAuditServiceTests
```

### 15.2 Metadata 测试

覆盖：

- 显式 SEO title
- fallback 到 page title
- fallback 到 site title
- description fallback
- canonical 自动拼接
- robots 默认值
- robots 非法值 fallback
- og:title fallback
- og:image fallback

### 15.3 Sitemap 测试

覆盖：

- published 页面进入 sitemap
- draft 页面不进入 sitemap
- noindex 页面不进入 sitemap
- canonical 缺失页面不进入 sitemap
- lastmod 正确
- XML 转义正确
- changefreq/priority 正确

### 15.4 Robots.txt 测试

覆盖：

- 默认 robots.txt
- 自定义 allow/disallow
- baseUrl 存在时输出 Sitemap
- baseUrl 缺失时不输出 Sitemap

### 15.5 JSON-LD 测试

覆盖：

- Article JSON-LD 字段完整
- Organization JSON-LD 字段完整
- BreadcrumbList 位置顺序正确
- JSON 可被反序列化
- 特殊字符不会破坏 JSON

### 15.6 SEO Audit 测试

覆盖：

- 缺少 title
- 缺少 description
- canonical 非法
- duplicate title
- noindex sitemap 冲突
- H1 数量异常
- 图片缺少 alt
- 内链断链

### 15.7 集成测试

准备一个测试站点 fixture：

```text
fixtures/seo-site/
├── site.yml
├── content/
│   ├── index.md
│   ├── blog/good-post.md
│   ├── blog/noindex-post.md
│   └── blog/missing-description.md
└── themes/default/
```

执行构建后验证：

```text
output/index.html
output/blog/good-post/index.html
output/sitemap.xml
output/robots.txt
output/seo-report.json
```

并检查 HTML 中包含：

```html
<title>
<meta name="description">
<link rel="canonical">
<meta property="og:title">
<script type="application/ld+json">
```

---

## 16. 文档更新要求

必须新增或更新：

```text
docs/seo.md
docs/configuration.md
docs/theme-development.md
docs/notion-seo-fields.md
```

### 16.1 docs/seo.md 内容

必须包含：

1. SEO Engine 简介
2. 站点级配置
3. 页面级配置
4. Metadata fallback 规则
5. Sitemap 规则
6. Robots.txt 规则
7. JSON-LD 规则
8. SEO Audit 规则
9. 常见问题

### 16.2 docs/theme-development.md 内容

必须说明主题如何使用官方 SEO Head Partial。

### 16.3 docs/notion-seo-fields.md 内容

必须说明 Notion 字段如何映射到 Bukit 页面 SEO 字段。

---

## 17. 禁止事项

Codex 执行时必须遵守：

1. 不要删除现有功能。
2. 不要大规模重写无关模块。
3. 不要改变现有公开 API，除非必须；如果必须，说明原因。
4. 不要让构建默认失败，除非用户设置 `failOnError = true`。
5. 不要把 SEO 逻辑硬编码到某一个主题。
6. 不要手写拼接 JSON-LD，必须使用 JSON Serializer。
7. 不要输出空 meta 标签。
8. 不要把 `noindex` 页面写入 sitemap。
9. 不要依赖网络请求生成 sitemap 或 JSON-LD。
10. 不要实现站群、自动采集、自动伪原创等非 P0 功能。

---

## 18. 验收标准

### 18.1 构建验收

执行：

```bash
dotnet build
dotnet test
```

必须通过。

如果项目已有 CLI 构建命令，也必须执行，例如：

```bash
dotnet run -- build
bukit build
```

### 18.2 文件输出验收

构建示例站点后，必须生成：

```text
sitemap.xml
robots.txt
seo-report.json
```

### 18.3 HTML 验收

文章页 HTML 必须包含：

```html
<title>
<meta name="description">
<link rel="canonical">
<meta name="robots">
<meta property="og:title">
<meta property="og:description">
<meta name="twitter:card">
<script type="application/ld+json">
```

### 18.4 Sitemap 验收

`sitemap.xml` 必须：

- 是合法 XML
- 不包含 draft 页面
- 不包含 noindex 页面
- 使用绝对 URL
- 包含 lastmod

### 18.5 Robots 验收

`robots.txt` 必须：

- 包含 `User-agent`
- 包含 Allow 或 Disallow
- 当 baseUrl 存在时包含 Sitemap 行

### 18.6 Audit 验收

`seo-report.json` 必须包含：

```json
{
  "pagesScanned": 0,
  "errorCount": 0,
  "warningCount": 0,
  "issues": []
}
```

字段名称可按项目风格调整，但必须结构化。

---

## 19. 推荐实现顺序

Codex 请按以下顺序执行：

```text
1. 阅读当前仓库结构、构建流程、页面模型、主题渲染机制
2. 找到 Page / Site / RenderContext / BuildPipeline 相关代码
3. 设计最小侵入式 SEO 数据模型
4. 实现 SeoMetadataResolver
5. 接入渲染上下文
6. 实现 Head Partial 或 Head 渲染方法
7. 实现 JSON-LD Builder
8. 实现 SitemapGenerator
9. 实现 RobotsTxtGenerator
10. 实现 SeoAuditService
11. 接入构建流程
12. 添加测试 fixture
13. 补充单元测试和集成测试
14. 更新文档
15. 执行 build/test
16. 输出变更报告
```

---

## 20. Codex 最终输出要求

执行完成后，请输出：

```markdown
# Bukit SEO/GEO Engine P0 实施报告

## 1. 修改摘要

## 2. 新增文件

## 3. 修改文件

## 4. 实现功能

## 5. 测试结果

## 6. 未完成事项

## 7. 风险与后续建议
```

必须列出：

- 具体修改文件路径
- 测试命令
- 测试结果
- 如果有失败，说明失败原因
- 如果某些功能因当前架构限制未完成，明确说明

---

## 21. 后续 P1 预留方向

本次 P0 完成后，下一阶段可以继续：

```text
P1-1 FAQ Schema
P1-2 Service Schema
P1-3 WebSite Schema
P1-4 Internal Link Engine
P1-5 Topic Cluster
P1-6 GEO Quick Answer Block
P1-7 Content Refresh Agent
P1-8 Notion SEO Audit 回写
```

本次代码结构要为这些能力预留扩展点，但不要提前实现复杂逻辑。

---

## 22. 参考标准

开发时请参考：

- Google Search Central: Structured Data
- Google Search Central: Sitemap
- Google Search Central: Robots.txt
- Google Search Central: Canonical URL
- Schema.org JSON-LD
- Sitemaps.org XML Sitemap Protocol

---

# 附录 A：最小页面 SEO 输出示例

```html
<head>
  <title>Malaysia ESD Guide</title>
  <meta name="description" content="Learn how Malaysia ESD company registration works.">
  <link rel="canonical" href="https://example.com/blog/malaysia-esd-guide/">
  <meta name="robots" content="index,follow">

  <meta property="og:title" content="Malaysia ESD Guide">
  <meta property="og:description" content="Learn how Malaysia ESD company registration works.">
  <meta property="og:url" content="https://example.com/blog/malaysia-esd-guide/">
  <meta property="og:type" content="article">
  <meta property="og:image" content="https://example.com/images/esd-guide.jpg">

  <meta name="twitter:card" content="summary_large_image">
  <meta name="twitter:title" content="Malaysia ESD Guide">
  <meta name="twitter:description" content="Learn how Malaysia ESD company registration works.">
  <meta name="twitter:image" content="https://example.com/images/esd-guide.jpg">

  <script type="application/ld+json">
  {
    "@context": "https://schema.org",
    "@type": "Article",
    "headline": "Malaysia ESD Guide",
    "description": "Learn how Malaysia ESD company registration works.",
    "image": "https://example.com/images/esd-guide.jpg",
    "datePublished": "2026-05-26",
    "dateModified": "2026-05-26",
    "mainEntityOfPage": {
      "@type": "WebPage",
      "@id": "https://example.com/blog/malaysia-esd-guide/"
    }
  }
  </script>
</head>
```

---

# 附录 B：最小 sitemap.xml 示例

```xml
<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>https://example.com/</loc>
    <lastmod>2026-05-26</lastmod>
    <changefreq>weekly</changefreq>
    <priority>1.0</priority>
  </url>
  <url>
    <loc>https://example.com/blog/malaysia-esd-guide/</loc>
    <lastmod>2026-05-26</lastmod>
    <changefreq>weekly</changefreq>
    <priority>0.8</priority>
  </url>
</urlset>
```

---

# 附录 C：最小 robots.txt 示例

```text
User-agent: *
Allow: /

Disallow: /drafts/
Disallow: /admin/

Sitemap: https://example.com/sitemap.xml
```
