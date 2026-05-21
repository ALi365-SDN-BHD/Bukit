# 10 内置功能与输出：sitemap/rss/search、标签分类与派生页

Bukit 在生成页面 HTML 之外，还会根据内容与配置生成一组“站点级产物”，用于 SEO、订阅、搜索与内容聚合。

本页以“用户能控制什么、会生成什么文件”为主；如果你需要更细的插件契约与边界，见开发者文档：[guide/dev/built-in-plugins](../dev/built-in-plugins.md)。

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

### 常见坑

- 没设 `site.url`：sitemap 里可能生成相对或错误的绝对链接
- baseUrl 配错：sitemap 里的 URL 带错前缀，搜索引擎抓取失败

部署相关详见：[13-部署-GitHub-Pages](./13-deploy-github-pages.zh-CN.md)。

## rss.xml：订阅源（博客/更新日志）

RSS 通常依赖：

- 站点 URL（`site.url`）
- 内容的标题/发布时间/type（尤其是 post）

如果你发现 rss 内容不全，优先检查：

- 你的内容是否有 `publishAt`
- 是否被草稿/过滤条件排除了（Notion Published、build.draft 等）

## search.json：站内搜索数据

search.json 通常是“每个页面的标题/摘要/URL”的列表，供前端 JS 实现搜索。

你通常需要：

- 主题中实现搜索 UI（读取 search.json 并过滤）
- 或直接用内置 `search.index.json`（视主题/实现而定）

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

Markdown 示例（tags/categories）见：[05-内容-Markdown](./05-markdown-content.zh-CN.md)。

Notion 示例见：[06-内容-Notion](./06-notion-content.zh-CN.md) 的模拟数据表。

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
