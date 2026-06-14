# 10 内置功能与输出：sitemap/rss/search、标签分类与派生页

Bukit 在生成页面 HTML 之外，还会根据内容与配置生成一组“站点级产物”，用于 SEO、订阅、搜索与内容聚合。

本页以“用户能控制什么、会生成什么文件”为主；如果你需要更细的插件契约与边界，见开发者文档：[guide/dev/built-in-plugins](../dev/built-in-plugins.zh-CN.md)。

## 你将获得什么

- 会生成哪些额外文件、在哪里
- 多语言时这些文件如何输出（split/merged/index）
- 标签/分类/归档/分页这类“派生页”是什么
- 常见问题：为什么 sitemap 里链接不对、为什么 search.json 为空

## 站点级产物清单（常见）

构建输出目录（`build.output`，默认 `dist/`）里通常会看到：

- `sitemap.xml`
- `rss.xml`
- `search.json`（面向浏览器的搜索数据）
- `search.index.json`（可选：聚合索引）
- `tags/`、`categories/`（派生列表页，具体取决于主题与派生逻辑）

对照可运行示例：

- `examples/starter/dist/`
- `examples/starter/.bukit_test/dist/`（用于测试的完整输出）

## sitemap.xml：搜索引擎索引入口

### 你能配置什么

- `site.url`：站点绝对域名（生成绝对链接的基础）
- `site.baseUrl`：子路径（GitHub Pages 常见）
- `site.sitemapMode`：多语言输出模式（见下一节）
- `site.sitemapDetail.defaultPriority`：默认 `<priority>` 值（0.0-1.0，v3.0+）
- `site.sitemapDetail.defaultChangefreq`：默认 `<changefreq>` 值（v3.0+）
- `site.sitemapDetail.imageEnabled`：是否启用图片 Sitemap 扩展（v3.0+）
- `site.sitemapDetail.videoEnabled`：是否启用视频 Sitemap 扩展（v3.0+）

### Per-Page 覆盖（v3.0+）

```yaml
---
sitemap:
  priority: 0.8
  changefreq: "daily"
  images:
    - url: "/images/hero.jpg"
      caption: "主图"
---
```

### 常见坑

- 没设 `site.url`：sitemap 里可能生成相对或错误的绝对链接
- baseUrl 配错：sitemap 里的 URL 带错前缀，搜索引擎抓取失败

部署相关详见：[13-部署-GitHub-Pages](./13-deploy-github-pages.zh-CN.md)。

## rss.xml → 多格式 Feed（v3.0 升级）

原来只生成 `rss.xml`。v3.0 起可同时生成 RSS 2.0 + Atom 1.0 + JSON Feed 1.1。

配置方式（v3.0 新增）：

```yaml
site:
  feed:
    formats: ["rss", "atom", "json"]
    limit: 20
    path: feed
```

生成的文件：
- `rss.xml`（RSS 2.0，原有格式）
- `feed/atom.xml`（Atom 1.0，新增）
- `feed/feed.json`（JSON Feed 1.1，新增）

⚠️ 插件开关 key 从 `rss` 改为 `feed`：
```yaml
site:
  plugins:
    feed:
      enabled: false   # 禁用全部 feed 生成
```

> 每日 collection 独立 feed：见 `collection.output.feedPath`。

订阅源通常依赖：
- 站点 URL（`site.url`）
- 内容的标题/发布时间/type（尤其是 post）

如果你发现 feed 内容不全，优先检查：
- 你的内容是否有 `publishAt`
- 是否被草稿/过滤条件排除了（Notion Published、build.draft 等）

## search.json：站内搜索数据

search.json 通常是"每个页面的标题/摘要/URL"的列表，供前端 JS 实现搜索。

### 搜索权重与排除（v3.0+）

在 front matter 中控制搜索行为：

```yaml
---
searchWeight: 5        # 权重越高排序越靠前（默认 1）
searchExclude: true    # 不加入搜索索引
---
```

### 内置搜索 UI（v3.0+）

```yaml
site:
  search:
    ui: "default"      # 启用内置搜索 UI（false 关闭）
    uiTheme: "dark"    # light / dark / auto
    placeholderText: "搜索..."
```

生成 `bukit-search.html`，可在模板中引入：

```html
{{ include "bukit-search.html" }}
```

搜索 UI 包含输入框、关键词匹配、键盘导航和高亮结果，无需额外 JS 库。

你通常需要：
- 主题中实现搜索 UI（读取 search.json 并过滤）
- 或直接用内置 `bukit-search.html`

如果 search.json 是空的：
- 站点可能没有内容项（content 读入失败/被过滤）
- 或主题/配置没有启用对应输出（取决于版本与模式）

## 标签与分类（tags / categories）

当你的内容中包含 `tags` 或 `categories`：

- 引擎/插件会聚合这些信息
- 主题一般会渲染 tags/categories 的列表页与详情页

可选：为某个分类/标签下的内容启用置顶排序：

- 在内容里标注 `pinned: true`（可选 `pinOrder` 数字，数字越小越靠前）
- 配置项：`taxonomy.pinField` / `taxonomy.pinOrderField`（多数据源可用 `pinFieldBySource` / `pinOrderFieldBySource` 做字段名映射）

### term 元数据（v3.0.0+）

可以为每个 tag/category 设置附加信息，两种方式任选：

**方式 1：data 文件**（`content/data/tags.yaml`）：
```yaml
- title: Machine Learning
  slug: ml
  description: Everything about ML and AI
  image: /assets/images/ml-cover.png
  weight: 10          # 排序权重，越大越靠前
  parent: tech        # 父级分类（层次化）
```

**方式 2：目录约定**（`content/_taxonomy/tags/ml/_index.md`），仿 Hugo：
```yaml
---
description: Everything about ML and AI
image: /assets/images/ml-cover.png
---
```

### 层次化分类

通过 `taxonomy.kinds[].hierarchical: true` 启用。term 通过 `parent` 字段建立父子关系，自动计算 `children` 和 `ancestors`（面包屑导航）。

### RSS feeds

每个 term 自动生成独立 RSS 2.0 feed：`/tags/python/feed.xml`，可独立订阅。

### 别名重定向

term 可配置别名（`aliases` 字段），自动生成跳转页面，确保旧 URL 不会 404。

Markdown 示例（tags/categories）见：[05-内容-Markdown](./05-markdown-content.zh-CN.md)。

## 派生页：tags/categories/分页/归档是什么

派生页（derived pages）不是你在内容源里直接写出来的页面，而是由引擎根据内容“派生”出来的页面，例如：

- `/tags/<tag>/`：某个标签下的文章列表
- `/categories/<category>/`：某个分类下的文章列表
- `/blog/page/2/`：分页后的列表页
- `/archive/2026/`：按年份归档

用户需要关心的点：

- 派生页是否渲染出来取决于：引擎是否启用对应派生能力 + 主题是否提供对应模板
- 派生页会参与 sitemap/search（因此 baseUrl 与 url 的准确性更重要）

## pluginFailMode：派生/输出失败时要不要中断构建

```yaml
site:
  pluginFailMode: strict  # strict（默认）| warn
```

- `strict`：插件错误会中断构建（适合生产）
- `warn`：记录错误但继续输出（适合迁移期/调试）

## 多语言输出模式（sitemap/rss/search）

多语言站点下，这些产物有三种常见模式（同一类产物的含义一致）：

- `split`：每个语言各一份（例如 `zh-CN/sitemap.xml` 与 `en-US/sitemap.xml`）
- `merged`：聚合成一份（通常在根目录输出一份）
- `index`：根目录输出索引文件，指向各语言文件

如何选择见：[11-多语言与SEO](./11-i18n-seo.zh-CN.md)。

## 图片自动优化（WebP / AVIF）

构建时自动将 `assets/` 目录中的 PNG/JPG 图片转换为 WebP/AVIF 格式。

**依赖**：需安装 `cwebp`（libwebp）或 `magick`（ImageMagick）：

```bash
# macOS
brew install webp imagemagick
# Linux
sudo apt install webp imagemagick
```

**配置**：

```yaml
theme:
  images:
    enabled: true
    formats: [webp]          # 也支持 avif
    sizes: [480, 768, 1200]  # 用于 srcset 的响应式尺寸
    quality: 85
```

没有安装转换工具时，构建过程会跳过图片优化并输出警告，不会报错。

## SCSS 自动编译

构建时自动将 `assets/` 目录中的 `.scss` 文件编译为 `.css`。

**依赖**：需安装 `sass` 或 `dart-sass` CLI：

```bash
npm install -g sass
```

**配置**：

```yaml
theme:
  scss:
    enabled: true
```

编译成功后自动删除原 `.scss` 文件。未安装 CLI 时跳过编译并输出警告。

## 相关内容推荐（v3.0+）

基于标签/分类/关键词等多维度自动匹配相关内容。

```yaml
site:
  related:
    enabled: true
    threshold: 80
    limit: 5
    indices:
      - name: tags
        weight: 100
      - name: categories
        weight: 60
```

## 菜单系统（v3.0+）

多菜单导航，支持嵌套子菜单。

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
```

## 数据文件（v3.0+）

在 `data/` 目录放置 YAML/JSON/TOML 文件，构建时自动加载到模板。

```
data/
  authors.yaml
  navigation.json
```

## URL 别名重定向（v3.0+）

在 front matter 中声明旧 URL，自动生成 HTML 重定向页：

```yaml
---
aliases:
  - /old-url/
  - /previous-permalink/
---
```

## 图片多尺寸处理（v3.0+）

对 `assets/` 下的图片自动生成多尺寸变体（依赖 ImageMagick）。

```yaml
theme:
  images:
    enabled: true
    sizes: [480, 768, 1200]
    quality: 80
```

📖 详细用法与完整配置见：[19-v3.0新增功能](./19-new-features-v3.zh-CN.md)。

## 外部插件安全性 (v3.x)

如果你使用了外部协议插件（`site.externalPlugins`），以下安全特性适用：

- **环境隔离**：插件进程运行在干净环境中——仅 `BUKIT_PLUGIN_NAME`、`BUKIT_PLUGIN_HOOK`、`BUKIT_PROJECT_ROOT`、`BUKIT_OUTPUT_DIR` 可用。使用 `allowEnvironment` 显式透传宿主变量。
- **输出限制**：配置 `maxStdoutBytes` / `maxStderrBytes` 限制插件输出，防止资源失控。
- **Stale 输出清理**：所有插件输出在构建清单中追踪。增量构建时，不再产生的旧文件会被自动删除。

详见：[外部插件协议](../dev/external-plugin-protocol.zh-CN.md)。
