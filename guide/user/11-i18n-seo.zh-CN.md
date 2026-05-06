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
type: page
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
  rssMode: split
  searchMode: split
```

适合：

- 每个语言独立站点体验更强（每个语言有独立 sitemap/rss/search）
- 你希望搜索引擎把每种语言视为相对独立的入口

### merged：合并一份

```yaml
site:
  sitemapMode: merged
  rssMode: merged
  searchMode: merged
```

适合：

- 语言数量少、内容量不大
- 你想让站点级产物尽量简单（根目录一份）

### index：根输出索引，指向各语言文件

```yaml
site:
  sitemapMode: index
  searchMode: index
```

> **注意**：`rssMode` 仅支持 `split` / `merged`，不支持 `index`。

适合：

- 语言多、内容多
- 你希望保留每语言产物，同时给一个“总入口”

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
- 确认主题在 `<head>` 输出 `canonical`、`alternate hreflang`（多语言站点更重要）

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
