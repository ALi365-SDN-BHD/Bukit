# 引擎固定产物（不依赖内容的输出）

除“按内容路由渲染的页面”之外，引擎与内置插件还会生成一些固定产物（例如首页列表页、blog/pages 聚合页、SEO 文件）。这些输出属于稳定契约：主题开发时必须考虑它们对应的模板与路径。

实现参考：`src/Bukit.Engine/SiteEngine.cs`

## 固定页面输出

无论内容数量多少，引擎都会生成以下页面：

- `/` → `index.html`
  - 使用模板：`pages/index.html`
  - 模型：`ListPageModel`（提供 `site`、`pages`；其中 `pages` 为所有 routed 内容页按 publish_date 倒序）
- `/blog/` → `blog/index.html`
  - 使用模板：`pages/list.html`
  - 模型：`ListPageModel`（仅包含 url 以 `/blog/` 开头的 routed）
- `/pages/` → `pages/index.html`
  - 使用模板：`pages/list.html`
  - 模型：`ListPageModel`（仅包含 url 以 `/pages/` 开头的 routed）

注意：
- 这些页面与 `RouteGenerator` 的逐页路由是并行关系；即使你自定义了某些页面的 route，也不会影响固定聚合页是否生成
- 固定聚合页只使用 routed 内容，不会把 derived 页面合并进 `pages` 列表

## 静态目录拷贝规则

每个 build variant（单语言或每个语言子目录）都会：

- 把 `theme.static` 目录原样拷贝到输出根目录
- 把 `theme.assets` 目录拷贝到输出根目录的 `assets/`

因此主题模板中引用资源时应使用 `site.base_url` 拼接路径（见 [主题开发](./theme.md)）。

## 与内置插件的关系

部分内置插件会把这些固定页面也纳入产物（例如 sitemap 会包含 `/`、`/blog/`、`/pages/`）。

详见：[内置插件](./built-in-plugins.md)
