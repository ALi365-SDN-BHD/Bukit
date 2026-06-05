# 主题开发（Themes）与参数使用

主题是“模板 + 资源 + 静态文件”的组合，用于控制站点的 HTML 结构、样式与可复用组件（partials）。本项目主题默认基于 Scriban 模板引擎渲染。

相关文档：
- [渲染与模板（Scriban）](./rendering-scriban.md)
- [Modules 数据源（mode=data → site.modules）](./modules-data.md)
- [配置（site.yaml）字段参考](./config-site-yaml.md)

示例主题（建议先阅读）：
- 默认主题：`examples/starter/layouts/`
- alt 主题：`examples/starter/themes/alt/`
- seo-best-practice 主题：`examples/starter/themes/seo-best-practice/`

## 主题目录结构（约定）

一个主题放在工程根目录的 `themes/<name>/` 下，通常包含：

```text
themes/<name>/
  layouts/        # Scriban 模板根目录
  assets/         # 会拷贝到输出的 /assets/
  static/         # 会原样拷贝到输出根目录
```

其中 `layouts/` 内建议按功能再分子目录：

```text
layouts/
  layouts/        # layout 模板（例如 base.html）
  pages/          # 页面模板（post/page/list/index）
  partials/       # 可复用片段（header/footer/seo 等）
```

## 主题选择与目录解析规则（重点）

主题相关配置来自 `site.yaml` 的 `theme` 节点：

```yaml
theme:
  name: alt
  layouts: layouts
  assets: assets
  static: static
  params:
    brand: ALT THEME
```

引擎对 layouts/assets/static 的解析规则（见 `SiteEngine.ResolveThemeDirectories`）：

- 当 `theme.name` 为空：
  - `theme.layouts/assets/static` 会被当作相对 `rootDir` 的路径
- 当 `theme.name` 非空：
  - 若 `theme.layouts == "layouts"`，实际 layoutsDir = `themes/<name>/layouts`
  - 若 `theme.assets == "assets"`，实际 assetsDir = `themes/<name>/assets`
  - 若 `theme.static == "static"`，实际 staticDir = `themes/<name>/static`
  - 如果你把 `theme.layouts/assets/static` 改成了非默认值，则会改为使用 `rootDir` 下的自定义路径（而不是 themes/<name> 下）

这意味着：
- 最推荐做法：只设置 `theme.name`，并保留 layouts/assets/static 默认值，让主题完全自包含于 `themes/<name>/`。
- 高级用法：在指定 `theme.name` 的同时，把 `theme.layouts` 指向一个自定义目录，用于“复用某套 layouts 但仍保留 theme.name 作为语义标识”。

## 主题命令（theme list / use）

CLI 支持两类主题管理命令（见 `ThemeCommand`）：

- `bukit theme list`：列出 `<rootDir>/themes/*` 下包含 layouts/assets/static 任一目录的主题名
- `bukit theme use <name>`：把 `theme.name` 写回指定配置文件（`--config` 或 `--site`）

适用场景：
- 多主题切换（例如 `alt` / `seo-best-practice`）
- CI 或多站点复用同一仓库时，按 `sites/<name>.yaml` 选择不同主题

## 必备模板清单（最小可运行主题）

引擎在构建时会固定渲染以下入口模板（相对 layoutsDir）：

- `pages/index.html`：站点首页（列表页，变量包含 `pages`）
- `pages/list.html`：
  - `/blog/index.html`
  - `/pages/index.html`

路由默认模板（由 `RouteGenerator` 决定）：

- `pages/post.html`：type=post 的内容页
- `pages/page.html`：type=page（或其他）内容页

如果你缺失这些模板，将在渲染时抛出 “Template not found”。

主题可以通过 `layouts/bukit.templates.yaml` 声明模板能力清单。最常见的能力是列表页是否依赖 `pages[*].content`：

```yaml
templates:
  pages/list.html:
    capabilities:
      needs_page_content: true
```

也可以继续声明更通用的能力，例如：

```yaml
templates:
  pages/list.html:
    capabilities:
      needs_page_content: true
      supports_pagination: true
      supports_taxonomy: false
      supports_search_snippets: false
```

这样在 `build.listPageContentMode: auto` 下，引擎会优先根据显式声明决定是否为列表页装配正文，而不是继续依赖兼容性启发式。

同一份能力清单也会影响专用模板选择：

- `pages/pagination.html + supports_pagination: true`
- `pages/taxonomy-index.html / pages/taxonomy-term.html + supports_taxonomy: true`
- `pages/search.html + supports_search_snippets: true`

`examples/starter/layouts/pages/` 现在已经提供了这 4 个 starter 级示例模板，可直接作为自定义主题的起点。

完整说明见：[template-capabilities.md](./template-capabilities.zh-CN.md)。

## Layout 与 include 机制

### layout 指令

如果一个模板的第一行非空内容是 layout 指令（支持 `{{ ... }}` 或 `{% ... %}`），引擎会先渲染当前模板正文，再把结果注入变量 `content`，最后渲染 layout 模板：

```scriban
{{ layout "layouts/base.html" }}
<h1>{{ page.title }}</h1>
{{ page.content }}
```

layout 模板中使用 `{{ content }}` 占位：

```html
<!doctype html>
<html>
  <body>
    {{ include "partials/header.html" }}
    <main>{{ content }}</main>
    {{ include "partials/footer.html" }}
  </body>
</html>
```

### include

Scriban 的 `include` 会从 layoutsDir 下按相对路径读取模板：

```scriban
{{ include "partials/seo.html" }}
```

## 主题参数：theme.params → site.params

`theme.params` 是给主题注入“配置化数据”的稳定方式，适合放置：
- 品牌名、导航开关、统计脚本开关
- 第三方 ID（注意不要放密钥）
- 主题样式变量（颜色、logo url 等）

模板中读取方式：

```scriban
{{ if site.params && site.params.brand }}
  {{ site.params.brand }}
{{ else }}
  {{ site.title }}
{{ end }}
```

参数类型：
- YAML scalar/sequence/mapping 都会被保留为可读的对象结构
- 建议 key 使用小写下划线风格（例如 `brand`, `ga_id`, `footer_links`），避免大小写与空格带来的可读性问题

示例配置与模板：
- `examples/starter/site.theme.yaml`
- `examples/starter/themes/alt/layouts/partials/header.html`

### 主题继承 (`extends`)

子主题通过 `extends` 字段继承父主题的模板、静态文件和资源：

```yaml
theme:
  name: my-child-theme
  extends: parent-theme-name
```

级联规则：模板查找子主题优先、父主题回退；`static/` 和 `assets/` 子主题先复制然后合并父主题。

### SCSS 编译 (`scss`)

构建时自动编译 `assets/` 中的 `.scss` 为 `.css`，需系统安装 `sass` 或 `dart-sass` CLI。

### 图片优化 (`images`)

构建时 `assets/` 中的 PNG/JPG 自动转为 WebP/AVIF，需 `cwebp` 或 `magick` CLI。未安装则跳过。

### Shortcodes (`shortcodes`)

```yaml
theme:
  shortcodes:
    youtube: '<iframe src="https://www.youtube.com/embed/{{ $1 }}"></iframe>'
```

Markdown 中用 `{% youtube "id" %}`，Scriban 中用 `{{ shortcode "youtube" "id" }}`。

### 组件 (`components`)

```yaml
theme:
  components:
    PostCard:
      template: "partials/post-card.html"
      props:
        title: ""
        url: ""
```

Scriban 中用 `{{ comp.render "PostCard" page.title page.url }}`。

## 静态资源与 base_url（避免路径问题）

构建时的拷贝规则（由 `SiteEngine` 执行）：
- `staticDir`：原样拷贝到输出根目录
- `assetsDir`：拷贝到输出目录的 `assets/`

模板里引用资源时应拼接 `site.base_url`，以兼容 GitHub Pages 子路径：

```html
<link rel="stylesheet" href="{{ site.base_url }}/assets/style.css" />
```

注意：当 baseUrl 为 `/` 时，`site.base_url` 会被注入为空字符串（避免出现 `//assets/...`）。

## 主题与 Modules（推荐组合）

当你用 `content.sources[].mode: data` 注入模块数据后，主题可以通过 `site.modules.<type>` 渲染结构化组件，例如：

```scriban
{{ if site.modules && site.modules.navigation }}
  {{ for item in site.modules.navigation }}
    <a href="{{ if item.fields && item.fields.link }}{{ item.fields.link.value }}{{ end }}">{{ item.title }}</a>
  {{ end }}
{{ end }}
```

更完整的模块建模与字段约定见：[modules-data.md](./modules-data.md)。

## 开发工作流建议

1. 从 `examples/starter/themes/alt` 复制一个新主题目录作为起点
2. 在 `theme.yaml` 声明需要的 `templates`，确认 `home` 与 `required: true` 模板存在
3. 把可配置项放进 `theme.params`，避免在模板里硬编码
4. 本地开发时用 `bukit dev`（HMR 开发服务器）获得文件监控和实时刷新；快速验证构建产物时用 `bukit build` + `bukit preview`
5. 发布前检查：
   - `site.baseUrl` 下的资源链接是否正确
   - 多语言时是否正确使用 `site.language` / `site.base_url`
   - SEO 主题是否依赖 `site.url`（canonical、OG）并在 CI 中用 `--site-url` 覆盖
