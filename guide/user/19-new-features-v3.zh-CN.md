# 19 v3.0 新增功能：多格式 Feed、增强 Sitemap、搜索 UI、Taxonomy 重构、相关内容、菜单、数据文件、别名、图片处理

Bukit v3.0 在原有 9 个内置插件的基础上新增了 5 个插件，并对 6 个现有插件进行了重大升级。本页汇总所有变化。

## 快速一览

| 功能 | 状态 | 配置位置 | 输出 |
|------|------|---------|------|
| 多格式 Feed（RSS + Atom + JSON） | 🆕 升级 | `site.feed` | `rss.xml` / `feed/atom.xml` / `feed/feed.json` |
| Sitemap priority / changefreq | 🆕 升级 | `site.sitemapDetail` + front matter | `sitemap.xml`（含 `<priority>` `<changefreq>`） |
| Sitemap 图片/视频扩展 | 🆕 升级 | `site.sitemapDetail` + front matter | `sitemap.xml`（含 `<image:image>` `<video:video>`） |
| 搜索 UI | 🆕 升级 | `site.search` | `search.json` + `bukit-search.html` |
| searchWeight / searchExclude | 🆕 升级 | front matter | `search.json`（含 `weight` 字段） |
| 多 collection 分页 + urlPattern | 🆕 升级 | `collection.pagination` | 分页页 |
| 归档 daily 深度 + 自定义模板 | 🆕 升级 | `collection.output.archiveDetail` | 归档页 |
| **Taxonomy v3.0.0 全面升级** | 🆕 重构 | `taxonomy.kinds` + `_index.md` | 层次化分类 / term RSS / redirect |
| 相关内容推荐 | 🆕 新增 | `site.related` | 数据注入 `__related_pages` |
| 菜单系统 | 🆕 新增 | `site.menus` | `menus.json` + 数据注入 |
| 数据文件 | 🆕 新增 | `data/` 目录 | 数据注入 `__data_files` |
| URL 别名/重定向 | 🆕 新增 | front matter `aliases` | HTML redirect 页 |
| 图片多尺寸处理 | 🆕 新增 | `theme.images` | 多尺寸变体 + srcset |

---

## 一、多格式 Feed（RSS + Atom + JSON Feed）

原来只支持 RSS 2.0。现在可以同时生成三种格式。

```yaml
site:
  feed:
    formats: ["rss", "atom", "json"]   # 默认只有 rss
    limit: 20                           # 每 feed 最大条目
    path: feed                          # 输出路径前缀
```

**每 collection 独立 feed：**

```yaml
collections:
  post:
    output:
      rss: true
      feedPath: blog-feed          # 独立目录，如 /blog-feed/atom.xml
      feedTitle: "我的博客文章"
      feedDescription: "最新博客更新"
```

**排除某页面 / 播客附件：**

```yaml
---
feed:
  exclude: true                    # 不加入 feed
  enclosure:                       # 播客附件
    url: "https://example.com/ep1.mp3"
    length: 12345678
    type: "audio/mpeg"
---
```

> ⚠️ 插件开关 key 从 `rss` 改为 `feed`：`site.plugins.feed.enabled: false`

---

## 二、增强 Sitemap

### priority / changefreq

```yaml
site:
  sitemapDetail:
    defaultPriority: 0.5
    defaultChangefreq: "weekly"
```

**按页覆盖：**

```yaml
---
sitemap:
  priority: 0.8
  changefreq: "daily"
---
```

### 图片 Sitemap 扩展

```yaml
site:
  sitemapDetail:
    imageEnabled: true
```

在 front matter 中声明：
```yaml
---
sitemap:
  images:
    - url: "/images/hero.jpg"
      caption: "主图"
      title: "Hero"
---
```

### 视频 Sitemap 扩展

```yaml
site:
  sitemapDetail:
    videoEnabled: true
```

```yaml
---
sitemap:
  videos:
    - url: "https://youtube.com/watch?v=xxx"
      title: "教程视频"
      thumbnail: "/images/thumb.jpg"
---
```

---

## 三、搜索增强

### 搜索权重与排除

```yaml
---
searchWeight: 5        # 权重越高排序越靠前（默认 1）
searchExclude: true    # 不加入搜索索引
---
```

### 内置搜索 UI

```yaml
site:
  search:
    ui: "default"      # 启用内置搜索 UI（false 关闭）
    uiTheme: "dark"    # light / dark / auto
    placeholderText: "搜索文章..."
```

生成的 `bukit-search.html` 可通过模板引入：

```html
{{ include "bukit-search.html" }}
```

搜索 UI 特性：
- ~5KB 纯 JS，零依赖
- 输入即搜，标题+内容加权匹配
- 支持 `searchWeight` 权重
- 键盘导航（↑ ↓ Enter Escape）
- 高亮搜索结果
- 明暗主题切换

---

## 四、相关内容推荐

基于标签/分类/关键词等多维度自动匹配相关内容。

```yaml
site:
  related:
    enabled: true
    threshold: 80      # 最低分数
    limit: 5           # 每页最多 5 条
    indices:
      - name: tags
        weight: 100
      - name: categories
        weight: 60
      - name: keywords
        weight: 40
```

支持的匹配维度：`tags`、`categories`、`keywords`、`collection`（同类型加分）、`date`（90 天内加分）。

**模板中使用：**

数据可通过 `context.Data["__related_pages"]` 访问，按内容 ID 索引，每个条目包含 `{title, url, score}`。

---

## 五、菜单系统

```yaml
site:
  menus:
    main:
      - identifier: home
        name: 首页
        url: /
        weight: 1
      - identifier: blog
        name: 博客
        url: /blog/
        weight: 2
        children:
          - identifier: tech
            name: 技术
            url: /blog/tags/tech/
            weight: 1
    footer:
      - identifier: about
        name: 关于
        url: /about/
        weight: 1
```

**模板中渲染：**

```html
<nav>
  <ul>
    {{ for item in site.menus.main }}
      <li>
        <a href="{{ item.url }}">{{ item.name }}</a>
        {{ if item.children }}
          <ul>
            {{ for child in item.children }}
              <li><a href="{{ child.url }}">{{ child.name }}</a></li>
            {{ end }}
          </ul>
        {{ end }}
      </li>
    {{ end }}
  </ul>
</nav>
```

同时输出 `menus.json` 文件。

---

## 六、数据文件（data/ 目录）

在项目根目录创建 `data/` 文件夹，放置 YAML/JSON/TOML 文件：

```
data/
  authors.yaml
  navigation.json
  zh-CN/
    strings.yaml
  en/
    strings.yaml
```

数据自动加载到 `context.Data["__data_files"]`。

**多语言支持**：`data/{lang}/` 子目录中的数据按语言加载，共享根级文件对所有语言可用。

---

## 七、URL 别名（重定向）

在 front matter 中声明别名，自动生成 HTML 重定向页：

```yaml
---
title: "新文章"
aliases:
  - /old-permalink/
  - /another-old-url/
---
```

生成的 HTML 包含：

```html
<meta http-equiv="refresh" content="0; url=/new-url/">
<link rel="canonical" href="/new-url/">
```

别名页标记为 `type: redirect`，自动排除 sitemap。

---

## 八、图片多尺寸处理

对 `assets/` 下的 JPG/PNG 图片自动生成多尺寸变体：

```yaml
theme:
  images:
    enabled: true
    formats: ["webp", "avif"]
    sizes: [480, 768, 1200]
    quality: 80
```

生成的变体文件（如 `hero-480w.jpg`、`hero-768w.jpg`）及 srcset 数据注入 `__image_srcsets`。

**依赖**：需要安装 ImageMagick（`magick` 或 `convert` 命令）。未安装时跳过并输出警告。

---

## 九、分页增强

### 多 collection 独立分页

```yaml
collections:
  post:
    pagination:
      enabled: true
      pageSize: 10
      urlPattern: "p/:num/"           # 可选：自定义 URL 模式
      firstPageUsesListRoute: true    # 第 1 页使用 listRoute
  docs:
    pagination:
      enabled: true
      pageSize: 20
```

### 全局分页默认值

```yaml
site:
  pagination:
    pageSize: 10
```

---

## 十、归档增强

```yaml
collections:
  post:
    output:
      archive:
        enabled: true
        depth: "daily"              # yearly | monthly | daily
        template: "pages/archive.html"
        routePrefix: "archives"     # 自定义 URL 前缀
```

---

## 十一、Taxonomy v3.0.0 全面升级

Taxonomy 系统进行了从架构到功能的全面重构，TaxonomyPlugin 从 1194 行拆分为 7 个职责模块，并新增 7 项功能。

### 层次化分类

通过 `taxonomy.kinds[].hierarchical: true` 启用。term 通过 `parent` 字段建立父子关系，自动计算 `children` 和 `ancestors`（面包屑导航）。

```yaml
taxonomy:
  kinds:
    - key: categories
      kind: categories
      hierarchical: true
```

**模板中访问：**

```html
{{ if taxonomy.ancestors }}
  <nav class="breadcrumb">
  {{ for ancestor in taxonomy.ancestors }}
    <a href="{{ site.base_url }}/{{ taxonomy.kind }}/{{ ancestor }}/">{{ ancestor }}</a>
  {{ end }}
  </nav>
{{ end }}
```

### Term 元数据（`_index.md` 约定）

仿 Hugo 风格，在 `content/_taxonomy/<kind>/<slug>/_index.md` 中通过 YAML front matter 定义 term 元数据：

```yaml
---
title: "机器学习"
description: "关于机器学习算法、框架和实践的文章"
image: "/images/ml-cover.jpg"
weight: 10
parent: "ai"
aliases:
  - machine-learning
  - ml
---
```

支持的字段：`title`、`description`、`image`、`weight`、`parent`、`aliases`。

### Term RSS Feed

每个有文章的 term 自动生成 RSS 2.0 feed：

| 产物 | 路径 | 说明 |
|------|------|------|
| RSS feed | `<output>/<kind>/<slug>/feed.xml` | 最新 20 篇文章，含 `<atom:link>` 自动发现 |

### Slug 音译（Transliteration）

`SlugHelper` 支持 Unicode NFD 分解，自动将带变音符号的拉丁字符转为 ASCII：

| 输入 | 输出 | 说明 |
|------|------|------|
| `café` | `cafe` | 重音符号移除 |
| `naïve` | `naive` | 分音符移除 |
| `über` | `uber` | 元音变音移除 |
| `Straße` | `strasse` | 合字 `ß` → `ss` |
| `Æsop` | `aesop` | 合字 `Æ` → `ae` |
| `日本語` | `日本語` | CJK 字符保留 |

### 别名重定向

term 的 `Aliases` 字段自动生成 HTML redirect 页面：

```
content/_taxonomy/tags/dl/_index.md:
  aliases: [deep-learning, deep_learning]

→ 生成:
  /tags/deep-learning/index.html  → redirect to /tags/dl/
  /tags/deep_learning/index.html  → redirect to /tags/dl/
```

### Term 排序与可见性

- `weight`：数字越大排序越靠前（索引页中）
- `isVisible: false`：term 不生成页面（但保留在 JSON 数据中）

### taxonomy.json Schema v2

新增 `children` 和 `ancestors` 数组字段：

```json
{
  "tags": {
    "ml": {
      "title": "机器学习",
      "slug": "ml",
      "count": 15,
      "description": "...",
      "children": ["deep-learning", "nlp"],
      "ancestors": ["ai"]
    }
  }
}
```

---

## 迁移指南

| 旧配置 | 新配置 |
|--------|--------|
| `site.plugins.rss.enabled: false` | `site.plugins.feed.enabled: false` |
| `RssPlugin`（源码类名） | `FeedPlugin`（源码类名） |
| 仅生成 `rss.xml` | 可同时生成 RSS + Atom + JSON Feed |
| 搜索仅 `search.json` | + `searchWeight` / `searchExclude` + 内置 UI |
| `taxonomy.json` schema v1 | schema v2（新增 `children` / `ancestors` 数组） |
| Term 仅有 `title` + `slug` | 新增 `description`、`image`、`weight`、`parent`、`children`、`ancestors`、`aliases` |
| 无层次化分类 | `taxonomy.kinds[].hierarchical: true` 启用 |
| 无 term 元数据 | `content/_taxonomy/<kind>/<slug>/_index.md`（Hugo 风格） |
| 无 term RSS | 每 term 自动生成 `<kind>/<slug>/feed.xml` |
