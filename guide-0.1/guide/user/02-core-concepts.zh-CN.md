# 02 核心概念：你在配置什么、引擎在做什么

这页用“用户视角”的语言把 Bukit 的核心对象讲清楚：你在写什么文件、这些文件会变成哪些网页、你能用哪些字段控制输出。

## 一张图理解构建流程

```text
site.yaml
  │
  ├─ content.sources[]（Markdown / Notion / data sources）
  │     └─ 读取内容 → 统一为 ContentDocument
  │
  ├─ routing（按 route/front matter、site.collections、主题 templates.accepts 决定 URL 与模板）
  │
  ├─ rendering（按模板把内容渲染成 HTML）
  │
  └─ plugins（可选：生成 sitemap/rss/search、派生页等）
        ↓
      dist/（静态文件输出目录）
```

你要记住的只有三句话：

1. **内容来自哪里**（`content.sources[]`）
2. **每条内容输出到哪里**（通过 route/front matter 或 site.collections 显式配置）
3. **用什么模板渲染**（显式 template、collection template/listTemplate，或主题 templates.accepts 匹配）

## 站点配置（site.yaml）

- `site.*`：站点级信息（站点名、标题、URL、baseUrl、多语言、SEO 输出模式等）
- `content.*`：内容来源（Markdown / Notion / 多源）
- `build.*`：输出目录、是否清理、是否渲染草稿
- `theme.*`：主题目录与参数（模板/资源/静态文件）
- `logging.*`：日志等级

详细字段见：[04-配置-site-yaml](./04-site-yaml-config.zh-CN.md)。

## 内容（ContentDocument）= 一条“会被渲染/注入模板”的数据

不管你的内容来自 Markdown 还是 Notion，引擎都会把它们统一成“内容项”。对你最重要的是：**哪些字段会影响站点行为**。

### 1）Meta：影响引擎决策的元信息（建议少量、稳定）

常见 Meta 键（你在 Markdown Front Matter 或 Notion 字段里提供）：

- `collection`：内容所属集合（推荐），对应 site.collections 中的 key，决定路由与模板
- `type`：可选内容分类或主题模板匹配键；不会单独创建内置路由
- `slug`：URL 的核心部分（一般推荐稳定不变）
- `language`：内容语言归属（多语言时用于过滤与关联）
- `tags` / `categories`：标签/分类（用于派生列表页）
- `route` / `url` / `template`：显式指定 URL/模板的高级用法。`outputPath` 由最终 URL 派生，不可配置。

### 2）Fields：面向模板消费的自定义字段（你想加什么都可以）

模板里读取字段的统一入口是：

- `page.fields.<key>.value`
- `page.fields.<key>.type`

例如你在 Markdown 里写 `seo_title`，模板里就可以这样用：

```scriban
<title>
  {{ if page.fields.seo_title }}
    {{ page.fields.seo_title.value }}
  {{ else }}
    {{ page.title }}
  {{ end }}
  - {{ site.title }}
</title>
```

Notion 模式下字段是否进入 `page.fields` 由 `fieldPolicy` 控制（见：[06-内容-Notion](./06-notion-content.zh-CN.md)）。

## 路由：一条内容会变成哪个 URL？

推荐方式：通过 `site.collections` 为每个集合定义 permalink、template 和 listRoute（详见：[04-配置-site-yaml](./04-site-yaml-config.zh-CN.md)）。核心不会因为 `type: page` 或 `type: post` 自动生成内置路由。

你可以通过以下方式控制结果：

- 在 site.collections 中声明集合规则（推荐）
- 在内容的 meta 中指定 `collection` 对应集合 key（推荐）
- 改 `slug`：改变路径的一段
- 改 `type`：仅作为可选元数据或主题匹配键，不要依赖它驱动路由
- 用 `route.url` / `route.template` 覆盖：更强，但更容易配错（详见：[03-项目目录与约定](./03-project-structure.zh-CN.md) 与 [14-故障排查](./14-troubleshooting.zh-CN.md)）

## 主题与模板：页面长什么样

主题本质是三类东西：

- layouts：模板（Scriban）
- assets：构建时拷贝到输出目录的资源（例如 CSS）
- static：原样拷贝的静态文件（例如 robots.txt、图片）

你可以切换主题、覆盖参数、以及在模板里读取 `site.* / page.* / site.modules.*`（见：[08-主题与模板](./08-themes-templates.zh-CN.md)）。

## Plugins：构建后生成额外文件（sitemap/rss/search 等）

构建完成后，引擎会根据配置与内置插件生成额外产物，例如：

- `sitemap.xml`
- `rss.xml`
- `search.json` / `search.index.json`
- 标签/分类列表页（以及 tags/categories 的派生页）

用户视角你只需要知道：

- 你能用 `site.sitemapMode` 和 `site.search.mode` 控制多语言输出模式；`rssMode` 为 1.0 遗留字段，Feed 按默认方式处理
- 你能用 `site.pluginFailMode` 决定插件失败是否中断构建

详见：[10-内置功能与输出](./10-built-in-features.zh-CN.md) 与 [11-多语言与SEO](./11-i18n-seo.zh-CN.md)。

## Modules：不生成路由，只给模板“提供数据”

Modules 用于企业官网/落地页非常常见的“结构化内容块”：

- banner、导航、features、faq、pricing、footer...

它们来自 `content.sources[].mode: data`，不会成为独立页面，而是被分组注入到 `site.modules.<type>[]`，供首页/栏目页模板渲染。

详见：[09-Modules-结构化数据](./09-modules-data.zh-CN.md)。
