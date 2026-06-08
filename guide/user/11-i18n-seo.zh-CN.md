# 11 多语言与 SEO：languages、输出模式与常见坑

多语言站点最容易踩坑的地方不在“翻译内容”，而在“URL 结构、SEO 产物、以及语言之间的关联”。本页把这些点用可复制的配置与示例讲清楚。

对照可运行示例：

- `examples/starter/site.i18n.yaml`
- `examples/starter/site.i18n.merged.yaml`
- `examples/starter/site.i18n.index.yaml`
- `examples/starter/site.i18n.seo.yaml`

## 你将获得什么

- 如何开启多语言（最小配置）
- 内容如何标记语言（Markdown/Notion）
- sitemap/rss/search 的 split/merged/index 模式怎么选
- GitHub Pages 下最常见的 SEO/路径问题怎么修

## 第一步：开启多语言

最小多语言配置：

```yaml
site:
  language: zh-CN
  languages:
    - zh-CN
    - en-US
  defaultLanguage: zh-CN
```

注意：

- `site.language` 是“当前站点默认语言”（也可理解成主语言）
- `site.languages` 表示你要输出哪些语言
- `defaultLanguage` 用于决定默认语言的 URL 组织策略（取决于主题与输出模式）

## 第二步：每条内容标记语言

### Markdown

在 Front Matter 写 `language`：

```yaml
---
collection: page
title: Hello
slug: greeting
language: en-US
---
```

对照示例：`examples/starter/content/greeting-en.md`。

### Notion

在数据库里新增字段 `language`（建议 select 或 rich_text），值填 `zh-CN`/`en-US`，它会被提升为 meta 供引擎过滤。

Notion 细节见：[06-内容-Notion](./06-notion-content.zh-CN.md)。

## URL 结构：多语言站点会输出到哪里

常见输出结构（示例）：

```text
dist/
  zh-CN/
    index.html
    pages/...
  en-US/
    index.html
    pages/...
  sitemap.xml 或 zh-CN/sitemap.xml（取决于模式）
```

实际路径取决于你的主题与路由规则，但基本规律是：

- 每个语言会有一个“语言根目录”（例如 `zh-CN/`、`en-US/`）
- 站点级产物（sitemap/rss/search）可选择在根输出或在语言目录输出

## sitemap/rss/search 的输出模式怎么选

这三类产物都支持同样的模式选择（以 sitemap 为例）：

### split：每语言一份

```yaml
site:
  sitemapMode: split
  search:
    mode: split
```

适合：

- 每个语言独立站点体验更强（每个语言有独立 sitemap/rss/search）
- 你希望搜索引擎把每种语言视为相对独立的入口

### merged：合并一份

```yaml
site:
  sitemapMode: merged
  search:
    mode: merged
```

适合：

- 语言数量少、内容量不大
- 你想让站点级产物尽量简单（根目录一份）

### index：根输出索引，指向各语言文件

```yaml
site:
  sitemapMode: index
  search:
    mode: index
```

> **注意**：`site.rssMode` 在 1.0 已移除，用户配置中不再支持；Feed 输出遵循 `site.feed` 与 `site.plugins.feed` 的默认多语言行为。

适合：

- 语言多、内容多
- 你希望保留每语言产物，同时给一个“总入口”

## 引擎级 SEO 与主题输出

Bukit 会在引擎层统一计算 `page.seo`，主题只需要负责渲染。这样 canonical、OG、Twitter、JSON-LD 和多语言 hreflang 的规则可以集中在引擎里，避免每个主题重复拼 URL。

引擎提供的主要字段：

| 模板字段 | 说明 |
|---|---|
| `page.seo.title` | SEO 标题 |
| `page.seo.description` | SEO 描述 |
| `page.seo.canonical` | 规范 URL，由 `site.url + baseUrl + page.url` 统一生成 |
| `page.seo.robots` | robots meta，只有页面字段或配置提供时才输出 |
| `page.seo.og.*` | Open Graph 标题、描述、URL、图片、类型 |
| `page.seo.twitter.*` | Twitter Card 标题、描述、图片、站点账号 |
| `page.seo.alternates` | HTML `<link rel="alternate" hreflang=...>` 数据 |
| `page.seo.json_ld` | WebSite、Organization、BreadcrumbList、BlogPosting JSON-LD |
| `site.analytics.google_analytics_id` | GA4 Measurement ID |
| `site.analytics.enabled` | Analytics 输出开关 |

### SEO 字段优先级

内容字段优先，其次站点级回退：

1. 页面字段：`seo_title`、`seo_desc`、`canonical`、`robots`、`og_image`、`author`、`update_time`
2. 内容常规字段：`summary`、`cover`、`image`、`publishAt`
3. 站点字段：`site.title`、`site.description`、`site.seo.defaultImage`

collection 为 `post` 的内容会额外输出 `BlogPosting` JSON-LD。

### 配置 site.seo

```yaml
site:
  url: https://example.com
  baseUrl: /
  seo:
    enabled: true
    renderMode: inject        # inject | off — 控制 HTML <head> 标签注入
    diagnostics: warn         # warn | strict | off — 构建期 SEO 质量检查
    defaultImage: /assets/og-default.png
    twitterSite: "@your_account"
    robotsTxt:
      enabled: true           # 生成 robots.txt（默认 true）
    schema:
      webPage: true           # 每页生成 WebPage JSON-LD
      collectionPage: true    # 列表路由生成 CollectionPage JSON-LD
      searchAction: true      # Sitelinks 搜索框 SearchAction
    organization:
      name: Example Inc
      url: https://example.com/about
      logo: https://example.com/logo.png
```

`site.seo.enabled` 默认是 `true`。设为 `false` 时，引擎不会生成 `page.seo`，新版 SEO partial 也不会输出 SEO 标签。

#### 渲染模式（renderMode）

| 值 | 行为 |
|-------|----------|
| `inject`（默认） | 引擎将 SEO 标签（canonical、description、OG、Twitter、JSON-LD）注入 HTML `<head>`。主题需引用 `partials/seo.html` 和 `partials/analytics.html`。 |
| `off` | 引擎仍构建 `page.seo` 模型，但**不**注入标签。主题需自行渲染所有标签。 |

#### 诊断模式（diagnostics）

| 值 | 行为 |
|-------|----------|
| `warn`（默认） | SEO 问题记录为警告，构建继续 |
| `strict` | SEO 问题导致构建失败（用于 CI 强制检查） |
| `off` | 不输出任何 SEO 诊断信息 |

诊断检查包括：缺失 canonical、重复 canonical、双斜杠 canonical、指向外部域名的 canonical、缺失 hreflang `x-default`、缺失 HTML `<head>`、缺失 JSON-LD 以及 GEO 校验错误。

#### Schema 开关（schema）

每种结构化数据类型可独立启用：

| 字段 | 默认值 | 说明 |
|-------|---------|-------------|
| `schema.webPage` | `true` | 每个内容页输出 `WebPage` JSON-LD |
| `schema.collectionPage` | `true` | 列表/分类/归档页输出 `CollectionPage` JSON-LD |
| `schema.searchAction` | `true` | Sitelinks 搜索框 `SearchAction` JSON-LD |

### 主题不会被强制注入

引擎不会直接修改 HTML，也不会在主题没有写 SEO partial 时自动注入 `<meta>` 标签。主题必须在 `<head>` 显式渲染：

```scriban
<title>{{ if page.seo }}{{ page.seo.title }}{{ else }}{{ page.title }}{{ end }}</title>
{{ include "partials/seo.html" }}
{{ include "partials/analytics.html" }}
```

如果主题已有自己的 SEO 逻辑，迁移时不要重复输出。推荐删除旧的手写 canonical/OG/Twitter/JSON-LD，改用 `page.seo`。

## Google Analytics（GA4）

Bukit 只支持 GA4 `gtag` 配置，不支持旧版 Universal Analytics。字段名是 `google_analytics_id`：

```yaml
site:
  analytics:
    google_analytics_id: G-XXXXXXXXXX
```

默认只要配置了 ID，且没有 `enabled: false`，新版 `partials/analytics.html` 就会输出：

```html
<script async src="https://www.googletagmanager.com/gtag/js?id=G-XXXXXXXXXX"></script>
```

关闭 Analytics：

```yaml
site:
  analytics:
    enabled: false
    google_analytics_id: G-XXXXXXXXXX
```

## SEO 三件套：site.url、baseUrl、主题 SEO 片段

### 1）site.url：决定绝对链接

如果你要部署到 GitHub Pages 的 `https://user.github.io/my-repo/`：

```yaml
site:
  url: https://user.github.io/my-repo
```

你也可以在命令行覆盖：

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --site-url https://user.github.io/my-repo
```

### 2）baseUrl：决定资源与链接前缀

同样是 GitHub Pages 子路径场景：

```yaml
site:
  baseUrl: /my-repo
```

baseUrl 配错的典型症状：

- 首页能打开，但 CSS/图片 404
- sitemap/rss 里的 URL 指向错误路径

### 3）主题：是否输出 canonical/alternates/meta

SEO 的 HTML 细节通常由主题控制。建议：

- 对照 `examples/starter/themes/seo-best-practice/` 的模板写法
- 确认主题在 `<head>` include `partials/seo.html`
- 确认多语言页面输出 `alternate hreflang`

## 生成式引擎优化（GEO）

GEO 为 ChatGPT Search、Perplexity、Google AI Overviews、Bing Copilot 等 AI 驱动搜索引擎优化网站内容。它在传统 SEO 基础上帮助 AI 引擎准确抓取、理解和引用你的内容。

### 配置

```yaml
site:
  seo:
    geo:
      enabled: true            # 总开关（默认：true）
      llmsTxt: true            # 生成 llms.txt（默认：true）
      llmsFullTxt: false       # 生成包含完整页面内容的 llms-full.txt（默认：false）
      llmsTxtMaxArticles: 20   # llms.txt 中最多显示的文章数（默认：20）
      aiBotMode: allow          # allow | block | selective
      aiBotAllowList:           # 允许的爬虫（selective 模式使用）
        - GPTBot
      aiBotBlockList:           # 屏蔽的爬虫
        - CCBot
      llmsTxtOptionalLinks:     # llms.txt Optional 节的外部链接
        - title: GitHub 仓库
          url: https://github.com/user/repo
          description: 源代码
```

### llms.txt 与 llms-full.txt

启用后，Bukit 在输出目录生成两个文件：

- **`llms.txt`** — 遵循 [llmstxt.org](https://llmstxt.org) 标准的 Markdown 格式站点索引，包含站点标题、描述、页面/文档列表、最近文章（按时间排序）以及可选的"Optional"外部链接节。
- **`llms-full.txt`** — 完整内容版本，包含每个可索引页面的文本，以 Markdown 标题分隔。适合需要更丰富上下文的 AI 引擎。

### AI 爬虫 robots.txt 规则

Bukit 自动为以下 AI 爬虫在 `robots.txt` 中添加指令：

GPTBot, ChatGPT-User, Google-Extended, Claude-Web, ClaudeBot, Anthropic-AI,
PerplexityBot, Cohere-AI, CCBot, Diffbot, FacebookBot, OAI-SearchBot

三种模式：
- **`allow`**（默认）— 允许所有 AI 爬虫
- **`block`** — 禁止所有 AI 爬虫
- **`selective`** — `aiBotAllowList` 生成 `Allow: /`，`aiBotBlockList` 生成 `Disallow: /`

### Front Matter GEO 字段

在内容 Front Matter 中添加结构化数据，放在 `geo` 键下：

```yaml
---
title: 如何用 Bukit 构建博客
collection: post
geo:
  schema_type: HowTo         # BlogPosting | Article | NewsArticle | FAQPage | HowTo
  about: 静态站点生成器
  date_reviewed: "2026-05-19"
  faq:
    - question: Bukit 支持哪些内容源？
      answer: Notion、Markdown 和本地文件。
    - question: 如何部署？
      answer: 支持 GitHub Pages、Vercel、Netlify 等。
  steps:
    - name: 安装 Bukit
      text: 运行 dotnet tool install。
      image: https://example.com/step1.png
      url: https://example.com/docs/install
    - name: 初始化站点
      text: 运行 bukit init my-site。
  citations:
    - title: Schema.org HowTo
      url: https://schema.org/HowTo
  same_as:
    - https://github.com/user/repo
    - https://twitter.com/user
  author:
    name: 张三
    url: https://zhangsan.dev
    same_as:
      - https://github.com/zhangsan
      - https://linkedin.com/in/zhangsan
  speakable:
    xpath: /html/body/article
---
```

每个字段生成对应的 JSON-LD 结构化数据：

| 字段 | 生成的 Schema 类型 |
|------|-----------------|
| `faq` | FAQPage 含 Question/Answer |
| `steps` | HowTo 含 HowToStep |
| `author` | Person 含 sameAs |
| `citations` | WebPage 含 mentions |
| `schema_type` | Article / NewsArticle / BlogPosting |
| `about` | article 的 about 属性 |
| `date_reviewed` | article 的 dateReviewed |
| `same_as` | article 的 sameAs |
| `speakable` | SpeakableSpecification |

### GEO 审计

运行 `bukit geo audit` 检查站点的 GEO 准备度：

```
=== GEO Audit ===
  llms.txt: present
  llms-full.txt: missing
  robots.txt: present
  Geo-enhanced routes: 3
  Schema types: Article, FAQPage, HowTo, Person, WebPage
  GEO Score: 75/100
```

**GEO Score**（0–100）衡量站点对 AI 搜索引擎的适配程度，评分规则如下：
- llms.txt 已生成（25 分）
- llms-full.txt 已生成（15 分）
- 有 GEO 增强路由（10 分）
- 文章 Schema 类型覆盖率（最多 15 分）
- 使用了 FAQPage 或 HowTo（15 分）
- 使用了 Person 作者标记（10 分）
- 使用了 Speakable 标记（5 分）
- 多路由 GEO 覆盖（5 分）

诊断码（`geo.*`）会出现在构建日志、发布审计报告以及 SEO/GEO 兼容报告中：
- `geo.faq_empty_question` / `geo.faq_empty_answer`
- `geo.howto_step_empty_name` / `geo.howto_step_empty_text`
- `geo.citation_url_invalid`
- `geo.author_no_sameas`
- `geo.speakable_path_invalid`
- `geo.schema_type_missing`
- `geo.llms_txt_missing`

### 发布审计报告（`publish-audit-report.json`）

每次构建后，Bukit 会写入 `.bukit/publish-audit-report.json`。这是语义 HTML、可见正文、来源、审核状态、实体元数据和 representation 覆盖率的主机器可读与可信发布报告。

Bukit 也会为每篇内容写入机器可读发布投影：`content/*.json` 暴露 canonical content record，方便集成使用；`content/*.md` 暴露面向 RAG / 知识摄取的文本表示。`agent-manifest.json` 由 projection pipeline 生成，只枚举可索引内容以及它们可用的 HTML、semantic HTML、JSON、Markdown、JSON-LD representations。publish audit 会验证已声明的 JSON 与 Markdown representation 文件确实存在，并以 route-level inventory 盘点 RSS、Atom、JSON Feed、sitemap、search、llms.txt、robots.txt 和 agent manifest 等聚合输出，明确每条 route 是否进入面向 AI / crawler 的输出。

```bash
bukit publish audit --dir dist
bukit publish audit --dir dist --strict
bukit publish diff --baseline previous/.bukit/publish-audit-report.json --current dist/.bukit/publish-audit-report.json
```

### SEO 审计报告（`seo-report.json`）

Bukit 也会写入 `.bukit/seo-report.json`，用于传统 SEO 兼容检查。该结构化 JSON 报告包含：

- **路由清单** — 每条路由的标题、描述、canonical URL、robots 状态、sitemap/search/RSS 收录情况、schema 类型、hreflang 交替链接
- **问题列表** — 每个问题包含严重级别（`error`/`warning`）、错误码、关联路由、描述
- **摘要** — 总路由数、可索引数、错误/警告数、GEO 分数及明细

使用 `bukit seo audit` 验证报告质量：

```bash
bukit seo audit --dir dist --config site.yaml           # 检查当前报告
bukit seo audit --dir dist --strict                     # warning 也按失败处理
bukit seo audit --dir dist --external                   # 同时检查外部链接
bukit seo diff --dir dist --config site.yaml            # 与上一份报告比对
bukit seo diff --max-new-errors 3                       # 限制新增错误数
bukit seo diff --fail-on-indexable-drop                 # 可索引页下降则失败
```

## 常见坑与修复清单

### 1）多语言内容互相“串台”

现象：中文内容出现在英文列表里，或反过来。

修复：

- 确认每条内容都写了 `language`
- Notion 模式确认 `language` 字段存在且值一致（`en-US` 不要写成 `en`）

### 2）sitemap 里的 URL 不对

修复：

- 设置 `site.url`
- 设置正确的 `site.baseUrl`（尤其是 GitHub Pages 子路径）
- 重新构建（不要只改文件不 rebuild）

### 3）部署后 404（只有多语言时发生）

修复清单：

- GitHub Pages 的发布目录是否指向 `dist/`（不是 `dist/zh-CN`）
- 主题首页链接是否正确拼接语言前缀
- 如果你希望默认语言不带前缀，需要主题/路由策略配合（先用示例主题跑通）
