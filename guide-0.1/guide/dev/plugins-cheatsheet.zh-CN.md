# Bukit 插件速查

## 内置插件一览

| 插件 | 类型 | 输出 | 关键依赖 |
|------|------|------|---------|
| taxonomy | DerivePages + AfterBuild | `/tags/`、`/categories/` 页面 + `taxonomy.json` | 内容需含 `meta.tags`/`meta.categories` |
| pagination | DerivePages | `/blog/page/2/` 等分页 | blog 文章数 > 10 |
| archive | DerivePages | `/blog/archive/` 年月归档 | 需有 blog 内容 |
| pages-index | DerivePages | 注入 `site.data.pages_by_id` | 无特殊依赖 |
| path-report | AfterBuild（外部） | `_debug/paths-report.json` | 调试用 |

## 插件开关配置

在 `site.yaml` 中通过 `site.plugins` 控制：

```yaml
site:
  plugins:
    sitemap: true          # 简写：布尔值开关
    rss: true
    search-index: false    # 关闭搜索索引
```

或使用完整格式（带自定义参数）：

```yaml
site:
  plugins:
    path-report:
      enabled: true
      options: {}
```

- `enabled` 默认为 `true`
- `options` 为插件自定义参数字典

## 插件失败策略

```yaml
site:
  pluginFailMode: strict   # strict=插件失败中断构建，warn=记录错误继续
```

## Publish Projection 输出

- `sitemap.xml`：projection 拥有，必须配置 `site.url`
- `rss.xml`、`feed/atom.xml`、`feed/feed.json`：projection 拥有，必须配置 `site.url`
- `search.json` 与可选 `bukit-search.html`：projection 拥有
- `llms.txt`、`llms-full.txt`、`robots.txt`、`agent-manifest.json`：projection 拥有

## sitemap projection

- 输出：`<outputDir>/sitemap.xml`
- **必须配置 `site.url`**，否则不生成
- 包含：首页、blog、pages、所有内容页、taxonomy/pagination/archive 派生页
- 会排除含 `<meta name="robots" content="noindex">` 的页面
- 多语言：`site.sitemapMode` 控制（`split`/`merged`/`index`）

## feed projection

- 输出：`<outputDir>/rss.xml`
- **必须配置 `site.url`**，否则不生成
- 只包含 routed 内容（不含派生页）
- 多语言 feed 行为：
  - `site.rssMode` 在 1.0 中已移除，用户配置中不再支持
  - 1.0 配置默认按语言目录输出 feed，并使用 `site.feed` 与 `site.plugins.feed` 默认行为

## search projection

- 输出：`<outputDir>/search.json`
- 不依赖 `site.url`
- 字段：id/title/url/content/summary/type/tags/categories/language/sourceKey/publishAt
- `site.searchIncludeDerived: true` 可将派生页纳入索引
- 多语言：各语言目录生成各自的 `search.json`

## taxonomy 插件（分类/标签）

根据内容的 `meta.tags`/`meta.categories` 自动生成分类页：

- `/tags/` → 标签索引页
- `/tags/<slug>/` → 标签详情页（含分页）
- `/categories/` → 分类索引页
- `/categories/<slug>/` → 分类详情页（含分页）

### taxonomy 配置

```yaml
taxonomy:
  template: pages/page.html        # 默认模板
  indexTemplate: pages/tax-index.html  # 索引页模板（可选）
  termTemplate: pages/tax-term.html    # 详情页模板（可选）
  pageSize: 10                      # 分页大小
  indexEnabled: true                # 是否生成索引页
```

### taxonomy 模板变量

索引页（`/tags/`）：
- `page.fields.terms.value[]` → `{ title, slug, url, count, description?, image?, weight?, parent?, children?, ancestors?, aliases? }`

详情页（`/tags/<slug>/`）：
- `page.fields.items.value[]` → `{ title, url, publish_date, summary }`
- `page.fields.taxonomy.value` → `{ kind, term, slug, count, description?, image?, weight?, parent?, children?, ancestors?, aliases? }`
- `page.fields.pagination.value` → `{ page, page_size, total, total_pages, has_prev, has_next }`

### taxonomy 新增字段说明（v3.0.0+）

| 字段 | 类型 | 来源 | 说明 |
|------|------|------|------|
| `description` | string? | data 源或 _index.md | term 描述文本 |
| `image` | string? | data 源或 _index.md | term 封面图 |
| `weight` | int? | data 源或 _index.md | 排序权重（越大越靠前） |
| `parent` | string? | data 源或 _index.md | 父级 term slug |
| `children` | string[]? | 自动计算（hierarchical） | 子级 term slug 列表 |
| `ancestors` | string[]? | 自动计算（hierarchical） | 祖先 term slug 链 |
| `aliases` | string[]? | data 源 | 别名列表（自动生成 redirect） |

### taxonomy 自动输出产物（v3.0.0+）

| 产物 | 路径 | 说明 |
|------|------|------|
| `taxonomy.json` | `<output>/taxonomy.json` | 结构化数据（schema v2） |
| RSS feeds | `<output>/<kind>/<slug>/feed.xml` | 每个 term 独立 RSS 2.0 |
| 别名 redirect | `<output>/<kind>/<alias>/index.html` | HTML meta refresh redirect |

### taxonomy 模板示例

```scriban
{{ layout "layouts/base.html" }}
<h1>{{ page.title }}</h1>
<ul>
{{ for item in page.fields.items.value }}
  <li>
    <a href="{{ site.base_url }}{{ item.url }}">{{ item.title }}</a>
    {{ if item.publish_date }}
      <time>{{ item.publish_date | date.to_string "%Y-%m-%d" }}</time>
    {{ end }}
  </li>
{{ end }}
</ul>
{{ if page.fields.pagination.value.has_prev }}
  <a href="{{ site.base_url }}/{{ page.fields.taxonomy.value.kind }}/{{ page.fields.taxonomy.value.slug }}/page/{{ page.fields.pagination.value.page - 1 }}/">上一页</a>
{{ end }}
{{ if page.fields.pagination.value.has_next }}
  <a href="{{ site.base_url }}/{{ page.fields.taxonomy.value.kind }}/{{ page.fields.taxonomy.value.slug }}/page/{{ page.fields.pagination.value.page + 1 }}/">下一页</a>
{{ end }}
```

### 自定义 taxonomy kinds

```yaml
taxonomy:
  kinds:
    - key: tags
      kind: tags
      title: Tags
    - key: categories
      kind: categories
      title: Categories
    - key: series         # 自定义分类维度
      kind: series
      title: Series
```

## 图片本地化（content.media）

对所有内容源生效，Notion 内容源尤其重要：

```yaml
content:
  media:
    downloadToLocal: true
    downloadDir: assets/uploads
    urlBase: /assets/uploads
    defaultImageUrl: /assets/images/default.jpg
    fieldKeys: [cover, image, thumbnail, og_image]
```

- `downloadToLocal: true` 时自动下载远程图片到本地
- Notion 图片 URL 为临时链接，必须本地化才能在部署后正常显示
