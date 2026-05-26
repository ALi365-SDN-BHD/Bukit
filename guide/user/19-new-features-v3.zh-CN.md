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

---

## 构建核心加固 (v3.x)

此版本还包含多项构建引擎可靠性和安全性改进：

| 特性 | 说明 | 影响 |
|---|---|---|
| **插件环境隔离** | 外部插件在干净环境中运行，仅暴露 `BUKIT_PLUGIN_NAME`、`BUKIT_PLUGIN_HOOK`、`BUKIT_PROJECT_ROOT`、`BUKIT_OUTPUT_DIR`。使用 `allowEnvironment` 可显式透传宿主变量。 | 插件开发者需读取这些变量，而不是依赖宿主环境 |
| **插件输出限制** | `externalPlugins.<name>.maxStdoutBytes` / `maxStderrBytes` 限制插件输出量。超出则 kill 进程。 | 防止失控插件消耗资源 |
| **插件输出清单 + stale 清理** | 所有插件输出以 plugin/hook/path/hash 记录在 `build-manifest.json` 中。增量构建时自动删除不再产生的旧输出。 | 跨构建保持输出目录干净 |
| **资源哈希模式** | `build.assetHashMode: "sha256"` 启用 SHA256 内容哈希的资源复制检测（推荐 CI 和网络文件系统使用）。 | 避免不必要的资源重复复制 |
| **路由安全校验** | 所有生成的路由和输出路径都经过路径穿越（`../`）、绝对路径、跨盘符路径和 Windows 保留名校验。 | 防止输出文件越界 |
| **静态 HTML 路由冲突检测** | `static/` 目录下的 `.html` 文件现在纳入路由冲突检测，与内容页和派生页一起校验。 | 防止静默路由冲突 |
| **Clean marker 保护** | `build.clean` 现在需要输出目录中存在 `.bukit-output-marker` 文件才允许清理。拒绝清除非 Bukit 目录。 | 防止误删除 |
| **远程主题可复现性** | 已缓存的远程主题不再自动 `git pull`。`@ref` 检出通过 `bukit-theme.lock.json` 锁定。commit 不匹配时构建失败。 | 跨环境构建一致 |
| **组合模板指纹** | 增量模板哈希现在组合 child/parent/user layouts、`theme.yaml` 和渲染器版本标记。父主题或 user layout 更改会触发重渲染。 | 减少"模板没更新"的意外 |
| **多语言并发预算** | 多语言构建遵循全局并发预算，防止资源耗尽。 | 更可预测的资源使用 |
| **诊断码体系** 🆕 | 所有构建错误现在携带稳定的 `BKT-XXXX` 诊断码（8 个分类，27 个码）。详见下方[诊断码参考](#诊断码参考)。 | 机器可读的错误码；跨版本稳定不变 |
| **插件能力系统** 🆕 | 每个外部插件可声明 `capabilities: [emit-outputs, derive-pages]`。运行时 hook 执行将被**强制校验**。声明了 capabilities 但缺少对应能力的插件会导致构建失败，错误码 `[BKT-0701]`。 | 沙箱机制 — 阻止插件执行未授权的 hook |
| **模板变量拼写检查** 🆕 | `bukit doctor` 现在扫描所有 Scriban 模板中的未知变量引用（如 `site.settings` 实际应写 `site.params`）。使用 AST 分析 + 已知字段白名单比对。 | 捕获变量拼写错误导致的静默渲染失败 |
| **内容管道阶段** 🆕 | 内容加载管道拆分为 5 个命名阶段（`ContentLoad` → `ImageLocalize` → `DraftFilter` → `SchemaDefaults` → `SchemaValidate`），每阶段记录耗时。可通过 `IContentStage` 扩展。 | 每个阶段的性能可见；支持插件开发者注入自定义阶段 |
| **渲染入口统一** 🆕 | 页面、列表和静态 HTML 渲染现在共享统一的调度循环 `PageRenderDispatcher.DispatchAsync()`。通过 `theme.staticTemplate` 渲染的静态 HTML 页面享有与内容页面相同的增量构建、SEO 注入和错误处理。 | 简化渲染管道；静态页面获得与内容页面同等的处理 |

---

## 诊断码参考

从 v3.x 起，所有 Bukit 异常携带稳定的 `BKT-XXXX` 格式诊断码：

| 分类 | 码段 | 示例码 |
|---|---|---|
| **Config（配置）** | `BKT-0001` – `BKT-00FF` | `BKT-0001` RequiredFieldMissing, `BKT-0002` InvalidValue, `BKT-0003` YamlSyntaxError, `BKT-0004` PathTraversal |
| **Theme（主题）** | `BKT-0101` – `BKT-01FF` | `BKT-0101` ManifestInvalid, `BKT-0102` ComponentNotFound, `BKT-0104` SourceUnavailable |
| **Route（路由）** | `BKT-0201` – `BKT-02FF` | `BKT-0201` RouteConflict, `BKT-0202` DuplicateOutputPath, `BKT-0204` ListRouteInvalid |
| **Render（渲染）** | `BKT-0301` – `BKT-03FF` | `BKT-0301` TemplateNotFound, `BKT-0302` TemplateParseError, `BKT-0303` LayoutNestingExceeded, `BKT-0304` ComponentFailed |
| **Schema** | `BKT-0401` – `BKT-04FF` | `BKT-0401` ValidationFailed, `BKT-0402` StrictModeBlocked |
| **Content（内容）** | `BKT-0501` – `BKT-05FF` | `BKT-0501` LoadFailed, `BKT-0502` ProviderUnavailable |
| **Build（构建）** | `BKT-0601` – `BKT-06FF` | `BKT-0601` OutputUnsafe, `BKT-0602` OutputNoMarker |
| **Plugin（插件）** | `BKT-0701` – `BKT-07FF` | `BKT-0701` ExecutionFailed, `BKT-0702` TimeoutExceeded |

诊断码出现在 `bukit doctor` 输出、构建错误和 CLI 消息中。相同错误始终产生相同的 `BKT-XXXX` 码。

## 模板变量拼写检查

`bukit doctor` 现在包含**模板变量拼写检查**部分，可检测 Scriban 变量名中的拼写错误：

```
--- Template variable spell check ---
⚠ pages/index.html: Unknown variable 'site.settings.theme' — did you mean 'site.params'?
⚠ pages/post.html: Unknown variable 'page.auther' — did you mean 'page.fields.author.value'?
✔ No unknown template variables detected
```

其原理是使用 Scriban 的 AST 解析每个 `.html` 模板，提取所有变量引用，然后与 `page`、`site`、`pages`、`p`、`item` 等循环变量的已知字段白名单进行交叉比对。

## 内容管道阶段

内容加载管道现在分为 5 个命名阶段，每个阶段记录自己的耗时：

```
event=content.stage stage=ContentLoad duration_ms=234
event=content.stage stage=ImageLocalize duration_ms=156
event=content.stage stage=DraftFilter duration_ms=1
event=content.stage stage=SchemaDefaults duration_ms=3
event=content.stage stage=SchemaValidate duration_ms=12
```

| 顺序 | 阶段 | 职责 |
|---|---|---|
| 1 | `ContentLoad` | 创建内容提供者，加载内容条目 |
| 2 | `ImageLocalize` | 下载并本地化远程图片 |
| 3 | `DraftFilter` | 过滤草稿条目（除非 `build.draft: true`） |
| 4 | `SchemaDefaults` | 应用 schema 默认值 |
| 5 | `SchemaValidate` | 按集合 schema 验证 |

插件开发者可通过实现 `IContentStage` 接口注入自定义阶段。
