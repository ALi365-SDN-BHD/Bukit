# 03 项目目录与约定：文件放哪里、相对路径怎么算

这一页解决两个高频问题：

1. “我应该把内容、主题、资源分别放到哪？”
2. “配置里写的 `dir: content` 到底是相对哪个目录？”

## 推荐的最小目录结构

以一个 Markdown 站点为例：

```text
my-site/
  site.yaml
  content/            # Markdown 内容
    about.md
    hello-world.md
  assets/             # 资源（例如 CSS）
    style.css
  static/             # 静态文件原样拷贝（可选）
    robots.txt
  layouts/            # 主题模板（或使用 themes/<name>）
    layouts/
      base.html
    pages/
      index.html
      page.html
      post.html
      list.html
    partials/
      header.html
      footer.html
  dist/               # 构建输出（build.output）
```

仓库内可运行示例：`examples/starter/`，结构更完整，可直接对照。

## “相对路径基准”（非常重要）

Bukit 里绝大多数相对路径都按 **配置文件所在目录**（`site.yaml` 的目录）解析。

例如你这样写：

```yaml
content:
  provider: markdown
  markdown:
    dir: content
build:
  output: dist
theme:
  layouts: layouts
  assets: assets
```

含义是：

- 内容目录是 `<site.yaml 所在目录>/content`
- 输出目录是 `<site.yaml 所在目录>/dist`
- 模板目录是 `<site.yaml 所在目录>/layouts`

这也是为什么 `--config <path>` 很关键：它不仅指定配置文件，还确定了路径基准。

## 多站点：sites/<name>.yaml 怎么用

当你在同一个仓库里维护多个站点（例如 `main` 和 `blog`），可以使用：

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --site blog --clean
```

它会读取 `sites/blog.yaml` 作为配置，但 **rootDir 仍然是当前目录**（不是 `sites/` 目录）。

示例可参考：

- `examples/starter/sites/blog.yaml`

推荐约定：

```text
repo/
  site.yaml           # 主站配置（默认）
  sites/
    blog.yaml         # blog 站配置
  content/            # 可复用内容
  themes/             # 主题集合
```

## 主题目录约定：layouts/assets/static

你可以直接在站点根目录放 `layouts/assets/static`，也可以把主题集中放在 `themes/<name>/` 下，再用 `theme.name` 切换。

### 方式 A：站点内直接维护模板

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

### 方式 B：用 themes/<name> 切换主题（更推荐）

```yaml
theme:
  name: alt
```

并把主题目录放在：

```text
themes/
  alt/
    layouts/
    assets/
    static/
```

可运行示例：

- `examples/starter/themes/alt/`
- `examples/starter/site.theme.yaml`

## 内容文件命名与字段约定（建议）

### slug（强烈建议稳定）

- slug 是 URL 的核心片段；变更 slug 往往意味着 URL 变了
- 推荐：slug 与文件名一致（例如 `hello-world.md` → slug `hello-world`）
- 如果你要做多语言与 i18n 关联，建议同时维护一个稳定的 `i18n_key`（Notion 里尤其常见）

### type（page / post）— 兼容层

> 推荐优先使用 `site.collections` 定义内容集合与路由规则（见 [04-配置-site-yaml](./04-site-yaml-config.zh-CN.md)）。

当未配置 collections 时，引擎使用 type 字段作为兼容回退：

- `page`：页面（关于、帮助、产品介绍等）
- `post`：文章（博客、新闻、更新日志等）

主题一般会按 type 或 collection 区分模板和列表页；不建议随意增加太多自定义 type，除非你的主题已支持对应模板。

### language（多语言）

多语言站点时，每条内容应该明确属于哪个语言：

- Markdown：在 Front Matter 写 `language: zh-CN` / `language: en-US`
- Notion：增加字段 `language`（会提升到 meta）

多语言输出与 SEO 见：[11-多语言与SEO](./11-i18n-seo.zh-CN.md)。

## 高级：路由覆盖字段（谨慎使用）

如果你确实需要把某个页面输出到自定义路径，可以使用以下字段（不同内容源都会映射到 meta）：

- `route` 或 `url`：指定输出 URL
- `outputPath`：直接指定输出文件路径（例如 `pages/about/index.html`）
- `template`：指定使用哪个模板

这些字段一旦配错，常见后果是：

- 页面“消失了”（被输出到意想不到的路径）
- sitemap/rss/search 里链接不正确
- GitHub Pages 出现 404（baseUrl/路径不一致）

建议优先通过 `slug/type` 解决，确需覆盖时再查：[14-故障排查](./14-troubleshooting.zh-CN.md)。
