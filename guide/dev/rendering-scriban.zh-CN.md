# 渲染与模板（Scriban）

渲染层负责把引擎生成的模型渲染成 HTML。当前模板引擎使用 Scriban。

实现参考：
- 模型：`src/Bukit.Rendering/Models.cs`
- 模型绑定：`src/Bukit.Rendering/Scriban/ScribanModelBinder.cs`
- 渲染器：`src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs`

## 统一渲染管道

页面、列表和静态 HTML 渲染现在共享统一的调度循环 `PageRenderDispatcher.DispatchAsync()`（实现：`src/Bukit.Engine/PageRenderDispatcher.cs`）。三种入口类型定义在 `RenderEntry.cs` 中：

| 类型 | 来源 | 渲染方法 |
|---|---|---|
| `Page` | 带路由的内容条目 | `renderer.RenderPage(template, pageModel)` |
| `List` | 特殊列表路由（首页、分类法、分页） | `renderer.RenderList(template, listModel)` |
| `Static` | `static/` 目录下的 `.html` 文件（启用 `theme.staticTemplate` 时） | `renderer.RenderPage(template, pageModel)` |

三种类型共享相同的增量构建跳过逻辑、SEO 注入和错误处理。

## 模板变量拼写检查

当 `EnableRelaxedMemberAccess` 启用时（默认），Scriban 会对拼写错误的变量（如 `{{ page.titel }}`）静默返回 `null`。Bukit 的 `doctor` 命令现在通过 `ScribanTemplateLinter` 包含模板变量拼写检查，使用 Scriban 的 AST 解析所有 `.html` 模板并与已知模型字段白名单进行交叉比对。

实现：`src/Bukit.Rendering/Scriban/ScribanTemplateLinter.cs`

## 目录约定（theme.layouts / assets / static）

在 `site.yaml` 中配置：

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

行为（由 `SiteEngine` 实现）：
- `static/`：静态资源。非 HTML 文件原样复制。当 `theme.staticTemplate` 设置时，`.html` 文件通过统一调度循环使用 Scriban 渲染（与内容页面相同的管道）。实现：`src/Bukit.Engine/RenderEntry.cs` → `ForStaticDir()`。
- `assets/`：构建时拷贝到输出目录的 `assets/`
- `layouts/`：渲染时作为模板根目录

示例站点可参考：`examples/starter/layouts/`

主题开发与 `theme.params` 的使用见：[theme.md](./theme.md)。

## 模板变量结构

渲染时注入的根变量：
- `site`
- `page`
- （列表页额外）`pages`

### site

| 变量 | 含义 | 备注 |
|---|---|---|
| `site.name` | 站点内部名 |  |
| `site.title` | 站点标题 |  |
| `site.url` | 站点绝对 URL | 可为空 |
| `site.description` | 站点描述 | 可为空 |
| `site.base_url` | baseUrl | 当 baseUrl 为 `/` 时会注入空字符串 |
| `site.language` | 当前语言 | 多语言变体构建时会变化 |
| `site.params` | `theme.params` | 可为空 |
| `site.modules` | data 模块分组 | 见 [Modules](./modules-data.zh-CN.md) |
| `site.data` | 插件注入的全局数据 | 由插件通过 PluginContext.Data 写入；如 pages-index 插件注入 `site.data.pages_by_id` |

### page

| 变量 | 含义 | 备注 |
|---|---|---|
| `page.title` | 页面标题 |  |
| `page.url` | 页面 URL |  |
| `page.content` | HTML 正文 | Notion 的 renderContent=false 时可能为空 |
| `page.summary` | 摘要 | 取自 meta.summary |
| `page.publish_date` | 发布时间 | 绑定为 DateTime（可能为空） |
| `page.fields` | 自定义字段 | `page.fields.<key>.type/value` |

### pages（列表页）

当渲染列表页（例如首页、/blog/、/pages/）时，模板可使用 `pages` 数组，每一项结构与 `page` 类似（但仅包含列表必要字段）。

需要额外注意：

- `pages[*].title`、`url`、`summary`、`publish_date`、`fields` 始终可用
- `pages[*].content` 是否装配，受 `build.listPageContentMode` 控制

推荐顺序：

1. 列表卡片优先使用 `summary`
2. 只有明确需要正文片段时才使用 `content`
3. 若主题明确依赖列表页正文，优先在 `layouts/bukit.templates.yaml` 中声明模板能力，而不是依赖自动猜测

示例：

```yaml
templates:
  pages/list.html:
    capabilities:
      needs_page_content: true
```

## fields 的使用约定

fields 是统一的“模板扩展面”，推荐用法：

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

注意：
- Markdown 模式下，部分保留键不会进入 fields（例如 `title/slug/type/...`），但 tags/categories/summary 会以固定方式写入 fields
- Notion 模式下，fields key 会被归一化为"下划线小写"，并受 fieldPolicy 控制

### Shortcodes（短代码）

Shortcodes 是主题级可复用 HTML 片段，在 `site.yaml` 的 `theme.shortcodes` 中声明，可在 Markdown 和 Scriban 中调用。

配置方式见 [配置（site.yaml）字段参考](./config-site-yaml.zh-CN.md)。Scriban 中使用：

```
{{ shortcode "youtube" "dQw4w9WgXcQ" }}
```

Shortcodes 在 `ScribanTemplateRenderer` 的 `RenderTemplate` 方法中注入为内置函数。

### Components（组件）

组件是带 props 的可复用模板片段，声明为 `theme.components`，使用 `{{ comp.render }}` 调用。

配置方式见 [配置（site.yaml）字段参考](./config-site-yaml.zh-CN.md)。实现位于 `ComponentFunctions.cs`（`Bukit.Rendering.Scriban` 命名空间），通过 `comp.render` 方法调用，继承父模板全局变量。

### ScribanLayoutDirectiveParser

`ScribanLayoutDirectiveParser` 是位于 `Bukit.Shared` 的共享工具类，负责解析 `{% layout "path" %}` 指令。该解析器同时用于：

- `ScribanTemplateRenderer`：解析模板首行 layout 指令，提取布局路径以及布局前 body 文本
- `TemplateStaticAnalysisService.Analyzer`：静态分析模板链

消除原先在两个不同文件中重复定义的 5 个 `TryParseLayoutDirective`/`TryParseLayoutLine` 方法。
