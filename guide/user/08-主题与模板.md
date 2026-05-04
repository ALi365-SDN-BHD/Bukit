# 08 主题与模板：站点长什么样、字段怎么在模板里用

主题（Theme）决定站点的视觉与页面结构。对普通用户来说，你通常会做三件事：

1. 选择/切换一个主题
2. 调整主题参数（例如品牌名、导航开关、SEO 片段）
3. 小幅改模板（例如首页布局、页脚内容、插入统计代码）

## 主题由哪些目录组成

主题通常包含三类目录（相对 `site.yaml` 所在目录）：

- `layouts`：模板（Scriban 语法）
- `assets`：构建时拷贝到输出目录的资源（例如 CSS）
- `static`：原样拷贝到输出目录的静态文件（可选）

仓库内示例主题：

- `examples/starter/themes/alt/`
- `examples/starter/themes/seo-best-practice/`

## 方式 A：使用 themes/<name> 切换主题（推荐）

### 配置写法

```yaml
theme:
  name: alt
  params:
    brand: my-site
```

### CLI：列出与切换主题

列出工程根目录下的 `themes/<name>`：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme list --config site.yaml
```

写回配置（设置 `theme.name`）：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme use alt --config site.yaml
```

## 方式 B：站点根目录直接维护模板（适合单站点快速改）

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

你可以直接在 `layouts/` 下改模板文件。

## 模板里能用哪些变量（用户最常用）

你不需要理解引擎内部模型，只要记住三类对象：

- `site`：站点信息与全局数据（`site.title/site.baseUrl/site.modules...`）
- `page`：当前页面/文章的信息（`page.title/page.slug/page.contentHtml/page.fields...`）
- `pages`：列表页里的页面集合（常见于首页、博客列表、页面列表）
- `paginator`（如果你的主题/页面有分页）：分页信息（见：[10-内置功能与输出](./10-内置功能与输出.md)）

### 1）读取站点信息

```scriban
<h1>{{ site.title }}</h1>
```

### 2）读取自定义字段（Markdown/Notion 通用）

```scriban
{{ if page.fields.seo_title }}
  <title>{{ page.fields.seo_title.value }}</title>
{{ end }}
```

### 3）读取主题参数（theme.params）

你在 `site.yaml`：

```yaml
theme:
  params:
    brand: starter
    showNewsletter: true
```

模板中（常见是通过 `site.theme.params` 或主题映射变量，具体以主题约定为准）：

```scriban
{{ if site.theme.params.showNewsletter }}
  <section class="newsletter">…</section>
{{ end }}
```

如果你不确定当前主题如何暴露参数，最稳的做法是：

- 在主题模板里搜索 `params` 的使用方式
- 或对照示例主题的写法：`examples/starter/themes/*/layouts/`

### 4）读取 Modules（site.modules）

当你启用 `mode: data` 的 sources 后，modules 会注入到 `site.modules.<type>[]`：

```scriban
{{ for b in site.modules.banner }}
  <a href="{{ b.fields.link.value }}">
    <img src="{{ b.fields.image.value }}" alt="{{ b.title }}" />
  </a>
{{ end }}
```

Modules 的数据建模与示例见：[09-Modules-结构化数据](./09-Modules-结构化数据.md)。

### 5）按 pageId 查页面详情（site.data.pages_by_id）

当你在模板里拿到一个页面 id（例如 Notion relation 的 pageId），并希望获取该页面的 URL/标题等信息时，可以使用内置插件注入的索引：

- `site.data.pages_by_id[pageId]` → `{ id, title, url, slug, type, publish_date, summary, fields }`
- pages-index 与内容源无关：Markdown/Notion/多源 sources 都可使用该索引
- 该索引在构建阶段生成，模板读取不会触发 API 请求

Notion relation 补全（可选）：
- 如果 relation 指向的页面不在本站输出范围内，你可以开启 pages-index 的 Notion 补全能力，让索引里也包含这些页面，并提供 `external_url`（Notion URL）。

示例：

```scriban
{{ p = site.data.pages_by_id[pid] }}
{{ if p }}
  {{ if p.url }}
    <a href="{{ site.base_url }}{{ p.url }}">{{ p.title }}</a>
  {{ else }}
    <a href="{{ p.external_url }}">{{ p.title }}</a>
  {{ end }}
{{ end }}
```

## 常见改动清单（带例子）

### 1）改首页布局

常见文件：

- `layouts/pages/index.html`

对照示例：`examples/starter/layouts/pages/index.html`。

如果你在首页或列表页里循环 `pages`：

- 优先使用 `p.title`、`p.summary`、`p.publish_date`
- 只有明确需要正文片段时才使用 `p.content`

当你确实依赖 `p.content` 时，有两种做法：

1. 在 `site.yaml` 中设置 `build.listPageContentMode: always`
2. 在 `layouts/bukit.templates.yaml` 中显式声明该模板需要列表页正文

`bukit.templates.yaml` 不只可以声明正文依赖，也可以顺手记录模板能力，例如分页、taxonomy、搜索摘要片段等。当前构建流程已经会校验这个文件的格式和模板路径。

### 2）改页头/页脚（partials）

常见文件：

- `layouts/partials/header.html`
- `layouts/partials/footer.html`

### 3）插入统计代码 / meta 标签

通常在基础布局里做：

- `layouts/layouts/base.html`

SEO 相关建议见：[11-多语言与SEO](./11-多语言与SEO.md) 与示例主题 `seo-best-practice`。

## 常见错误与修复

- 模板文件缺失：构建时报“找不到模板/布局” → 检查 `theme.name` 是否存在、目录结构是否完整
- CSS/资源 404：多见于 `site.baseUrl` 配错或模板里没拼上 baseUrl（见：[13-部署-GitHub-Pages](./13-部署-GitHub-Pages.md)）
- 字段为空：模板读取 `page.fields.xxx` 但内容没提供该字段 → 在内容里补字段或加 `if` 保护
- 列表页里 `p.content` 为空：不一定是内容没加载，可能是 `build.listPageContentMode` 为 `never`，或当前主题没有声明该列表模板需要正文
