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

- `examples/starter/layouts/` + `examples/starter/assets/`（默认 starter 结构）
- `examples/starter/themes/alt/`
- `examples/starter/themes/seo-best-practice/`

`bukit init <dir>` 现在会生成同一套内容站 starter 设计到 `themes/starter/`：包含可复用 partial、卡片列表、分页/搜索/taxonomy 模板，以及 `bukit.templates.yaml` 能力声明。

## 主题预览：查看主题结构

使用 `bukit theme preview` 快速查看主题的完整结构：

```bash
bukit theme preview my-blog
```

展示内容：
- **元数据**：名称、版本、描述、主页、缩略图、标签（来自 `theme.yaml`）
- **Sections**：注册的页面 section 及其描述和插件关联
- **Components**：可复用模板组件及其声明的 props
- **设计令牌**：分组计数（colors/font/radius/spacing/layout）及颜色采样
- **布局模板**：`layouts/` 目录下所有 `.scriban`/`.html`/`.sbn` 文件
- **文件统计**：assets 和 static 文件数量

此命令有助于在安装或自定义主题之前了解主题的能力。

## 方式 A：使用 themes/<name> 切换主题（推荐）

### 配置写法

```yaml
theme:
  name: alt
  params:
    brand: my-site
```

### CLI：列出与切换主题

创建基于 starter 的自定义主题并切换过去：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme create custom --config site.yaml --brand "My Site" --primary-color "#0b5fff" --accent-color "#0f7b6c" --use
```

从已有本地主题创建：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme create custom --from alt --config site.yaml
```

列出工程根目录下的 `themes/<name>`：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme list --config site.yaml
```

写回配置（设置 `theme.name`）：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme use alt --config site.yaml
```

## 主题创建：交互式向导（Wizard）

如果你不想手动拼命令，可以用交互式向导快速创建主题：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme wizard my-blog --config site.yaml
```

向导会依次询问你：

- 主题名称
- 主题类型/预设
- 品牌名称
- 主色调与强调色
- 是否立即切换到该主题

每次回答后按回车进入下一题，或按 Ctrl+C 退出。回答完所有问题后，Bukit 自动生成主题目录并写入配置。

共有 5 种预设可选：

| 预设 | 适用场景 |
|------|---------|
| `blog` | 个人博客/技术博客，含列表、文章、标签、归档 |
| `docs` | 文档站点，含侧边栏导航与搜索 |
| `landing` | 单页落地页，含 Hero、特性、CTA |
| `minimal` | 最小化模板，只含基础布局与页面模板 |
| `portfolio` | 作品集，含项目卡片与分类筛选 |

如果想跳过交互式问答，直接用 `--preset` 指定预设：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme wizard my-blog --preset blog --config site.yaml
```

也可以组合 `--brand`、`--primary-color`、`--accent-color`、`--use` 等参数一步到位。

## 主题发现：info/params

当你想了解某个主题的详细信息时，使用 `theme info`：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme info alt --config site.yaml
```

输出内容包括：

- 主题名称、版本号
- 描述信息
- 模板能力声明（是否支持分页、搜索、taxonomy 等）
- 可用的模板文件清单

查看当前主题所有可配置参数：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme params --config site.yaml
```

这会列出当前主题 `theme.params` 中所有可用的键名及其默认值和说明，方便你在 `site.yaml` 中按需覆盖。

## 主题分发与分享

Bukit 支持将主题打包、分享和从注册表安装。

打包主题为可分发的 `.tar.gz` 文件：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme pack my-theme
```

安装主题支持三种来源：

从本地目录安装：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme install /path/to/theme.tar.gz
```

从 URL 安装：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme install https://example.com/themes/my-theme.tar.gz
```

从注册表搜索并安装（Experimental；不属于 Bukit 1.0 GA 兼容承诺）：

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme search blog
dotnet run --project src/Bukit.Cli -c Release -- theme install --registry my-theme --config site.yaml
```

## 模板级命令

除了主题级命令，Bukit 还提供模板级的细粒度操作，共 7 个子命令：

| 命令 | 作用 |
|------|------|
| `template create` | 在主题的 `layouts/` 下创建新模板文件 |
| `template list` | 列出当前主题所有模板文件 |
| `template show` | 查看指定模板文件的内容 |
| `template validate` | 校验所有模板语法是否符合 Scriban 规范 |
| `template snippets` | 列出或插入内置代码片段库 |
| `template hints` | 查看当前模板可用的变量与对象提示 |
| `template sync` | 将主题模板同步到站点根目录 |

示例用法：

```bash
dotnet run --project src/Bukit.Cli -c Release -- template create about --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- template list --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- template show pages/index.html --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- template validate --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- template hints --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- template sync --config site.yaml
```

### 内置代码片段库（Snippets）

`template snippets` 提供了一个内置片段库，包含 **8 个 Scriban 片段**和 **9 个 CSS 片段**，覆盖常见的模板与样式需求。

查看可用片段清单：

```bash
dotnet run --project src/Bukit.Cli -c Release -- template snippets --config site.yaml
```

Scriban 片段涵盖：页面循环、分页导航、SEO meta 标签、分析代码、多语言切换器、面包屑导航、相关文章、搜索框。

CSS 片段涵盖：基础重置、文章排版、响应式网格、卡片组件、导航栏、页脚、按钮、暗色模式、打印样式。

插入指定片段到模板文件：

```bash
dotnet run --project src/Bukit.Cli -c Release -- template snippets pagination --config site.yaml
```

片段会被写入到当前模板的合适位置。如果模板已有相似代码，命令会提示冲突并要求确认覆盖。

## 方式 B：站点根目录直接维护模板（适合单站点快速改）

```yaml
theme:
  layouts: layouts
  assets: assets
  static: static
```

你可以直接在 `layouts/` 下改模板文件。

## 推荐的 Starter 定制顺序

从生成的 starter 主题开始，按这个顺序改，成本最低：

1. 先改 `site.yaml` 的 `theme.params` 做基础品牌配置：

```yaml
theme:
  name: starter
  params:
    brand: My Site
    footer_text: My Site
```

2. 再改 `assets/style.css` 里的视觉变量，例如 `--primary`、`--accent`、间距、字体。
3. 然后改 `layouts/partials/header.html` 与 `layouts/partials/footer.html`。
4. 只有页面结构真的变化时，再改 `layouts/pages/index.html`、`list.html`、`post.html`、`page.html`。

starter 也内置了可选功能模板：

- `layouts/pages/pagination.html`
- `layouts/pages/taxonomy-index.html`
- `layouts/pages/taxonomy-term.html`
- `layouts/pages/search.html`

如果要做一个新的可复用主题，优先运行 `theme create <name>`，再修改生成的文件。只有明确要替换已有主题目录时才使用 `--force`。

## 模板里能用哪些变量（用户最常用）

你不需要理解引擎内部模型，只要记住三类对象：

- `site`：站点信息与全局数据（`site.title/site.baseUrl/site.modules...`）
- `page`：当前页面/文章的信息（`page.title/page.slug/page.contentHtml/page.fields...`）
- `pages`：列表页里的页面集合（常见于首页、博客列表、页面列表）
- `paginator`（如果你的主题/页面有分页）：分页信息（见：[10-内置功能与输出](./10-built-in-features.zh-CN.md)）

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

模板中，Bukit 会把 `theme.params` 暴露为 `site.params`：

```scriban
{{ if site.params.showNewsletter }}
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

Modules 的数据建模与示例见：[09-Modules-结构化数据](./09-modules-data.zh-CN.md)。

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

新版 starter 推荐把 SEO 与统计代码拆成 partial：

```scriban
<title>{{ if page.seo }}{{ page.seo.title }}{{ else }}{{ page.title }}{{ end }}</title>
{{ include "partials/seo.html" }}
{{ include "partials/analytics.html" }}
```

对应文件：

- `layouts/partials/seo.html`
- `layouts/partials/analytics.html`

如果你用 `bukit theme create <name>` 新建主题，Bukit 会自动生成这两个 partial。

#### 引擎是否会自动注入 SEO？

不会。Bukit 的策略是“引擎统一计算，主题显式渲染”：

- 引擎会为页面计算 `page.seo`
- 引擎会为站点暴露 `site.analytics`
- 主题没有 include SEO/Analytics partial 时，HTML 不会自动出现这些标签
- 这样可以避免引擎在未知 HTML 结构里做脆弱的字符串注入，也不会破坏老主题

#### 主题已经有自己的 SEO 逻辑怎么办？

如果旧主题已经手写了 canonical、OG、Twitter 或 JSON-LD，不要同时保留旧逻辑又 include 新 partial，否则可能出现重复标签。

推荐迁移方式：

1. 保留 `<title>`，但优先读取 `page.seo.title`
2. 删除主题里手写拼接 canonical/OG/Twitter/JSON-LD 的逻辑
3. 在 `<head>` 中 include `partials/seo.html`
4. 需要 GA4 时 include `partials/analytics.html`

Analytics partial 的输出条件是：

```scriban
{{ if site.analytics && site.analytics.enabled && site.analytics.googleAnalyticsId }}
  ...
{{ end }}
```

所以只要 `site.analytics.googleAnalyticsId` 存在且没有 `enabled: false`，就会输出 GA4 gtag 代码。

SEO 相关配置与多语言 hreflang 行为见：[11-多语言与SEO](./11-i18n-seo.zh-CN.md) 与示例主题 `seo-best-practice`。

## Shortcodes <Badge type="tip" text="1.0.6" />

Shortcodes 让你在 Markdown 正文和 Scriban 模板中插入可复用的 HTML 片段。

### 配置 Shortcodes

在 `site.yaml` 的 `theme.shortcodes` 中声明：

```yaml
theme:
  shortcodes:
    youtube: '<div class="video"><iframe src="https://www.youtube.com/embed/{{ $1 }}" frameborder="0" allowfullscreen></iframe></div>'
    callout: '<div class="callout callout-{{ $1 }}">{{ $2 }}</div>'
```

参数用 `{{ $1 }}`、`{{ $2 }}` 表示位置参数。

### 在 Markdown 中使用

```markdown
## 我的视频

{% youtube "dQw4w9WgXcQ" %}

{% callout "warning" "请注意备份数据！" %}
```

Shortcodes 在渲染阶段处理，自动解码 Markdown 管道中的 HTML 实体。

### 在 Scriban 模板中使用

```
{{ shortcode "youtube" "dQw4w9WgXcQ" }}
```

---

## 主题继承 (Theme Inheritance) <Badge type="tip" text="1.0.6" />

子主题可以继承父主题的模板、静态文件和资源，只需覆盖需要定制的部分。

```yaml
theme:
  name: my-custom-theme
  extends: official-blog-theme
```

级联规则：

- **模板**：先在子主题 `themes/my-custom-theme/layouts/` 中查找，找不到则回退到父主题 `themes/official-blog-theme/layouts/`
- **静态文件**：子主题和父主题的 `static/` 目录内容合并
- **资源文件**：子主题和父主题的 `assets/` 目录内容合并（SCSS 编译和图片优化也会应用到父主题的资源）

---

## 组件 (Components) <Badge type="tip" text="1.0.6" />

在主题中声明可复用的模板组件，在 Scriban 模板中调用。

### 声明组件

```yaml
theme:
  components:
    PostCard:
      template: "partials/post-card.html"
      props:
        title: ""
        url: ""
    AuthorBio:
      template: "partials/author-bio.html"
      props:
        name: ""
        avatar: ""
```

### 组件模板

```html
<!-- themes/my-theme/layouts/partials/post-card.html -->
<article class="post-card">
  <h3>{{ title }}</h3>
  <a href="{{ url }}">阅读更多</a>
</article>
```

### 在模板中使用

```
{{ for p in pages }}
  {{ comp.render "PostCard" p.title p.url }}
{{ end }}
```

组件继承父模板的全局变量（`page`、`site` 等），props 按声明顺序作为局部变量绑定。

---

## SCSS 编译 <Badge type="tip" text="1.0.6" />

构建时自动将 `.scss` 编译为 `.css`。需安装 `sass` 或 `dart-sass` CLI：

```bash
npm install -g sass
```

配置：

```yaml
theme:
  scss:
    enabled: true
```

在 `assets/` 目录编写 `.scss` 文件，构建后自动生成 `.css` 文件。

---

## 图片优化 <Badge type="tip" text="1.0.6" />

构建时自动将 PNG/JPG 图片转换为 WebP/AVIF 格式。需安装 `cwebp`（libwebp）或 `magick`（ImageMagick）：

```bash
# macOS
brew install webp imagemagick
# Linux
sudo apt install webp imagemagick
```

配置：

```yaml
theme:
  images:
    enabled: true
    formats: [webp]          # 还支持 avif
    sizes: [480, 768, 1200]  # 响应式尺寸
    quality: 85
```

没有安装转换工具时不会报错，只会输出警告信息。

---

## HMR 开发服务器 <Badge type="tip" text="1.0.6" />

使用 `bukit dev` 代替 `bukit preview` 获得实时预览体验：

```bash
bukit dev                    # 默认 http://localhost:35729
bukit dev --port 3000        # 自定义端口
bukit dev --no-watch         # 不监控文件（纯静态服务）
```

功能：

- 监控 content/、themes/、layouts/ 等目录的文件变更
- 自动增量重构建（仅渲染变更页面）
- WebSocket 实时刷新所有连接的浏览器
- 300ms 去抖，避免频繁构建

---

## 内容 Schema 校验 <Badge type="tip" text="1.0.6" />

在集合配置中声明字段类型，构建时自动校验 Front Matter：

```yaml
collections:
  posts:
    schema:
      - name: featured
        type: bool
        required: true
      - name: rating
        type: number
        required: true
```

支持的类型：`string`、`number`、`bool`、`date`、`list`。

失败模式：

```yaml
build:
  schemaFailMode: warn     # 默认：警告但继续构建
  # schemaFailMode: strict  # 严格模式：校验失败中断构建
```

---

## 常见错误与修复

- 模板文件缺失：构建时报“找不到模板/布局” → 检查 `theme.name` 是否存在、目录结构是否完整
- CSS/资源 404：多见于 `site.baseUrl` 配错或模板里没拼上 baseUrl（见：[13-部署-GitHub-Pages](./13-deploy-github-pages.zh-CN.md)）
- 字段为空：模板读取 `page.fields.xxx` 但内容没提供该字段 → 在内容里补字段或加 `if` 保护
- 列表页里 `p.content` 为空：不一定是内容没加载，可能是 `build.listPageContentMode` 为 `never`，或当前主题没有声明该列表模板需要正文

## 远程主题（Git 来源）

Bukit 支持直接从 Git 仓库获取主题：

```yaml
theme:
  source: https://github.com/user/theme.git@v1.0.0
```

- **首次构建**：仓库会克隆到本地缓存。
- **后续构建**：不会自动更新已缓存的主题——这确保了构建的可复现性。
- **版本锁定**：通过 `@ref` 指定标签或分支；Bukit 会检出到该精确引用。
- **锁定文件**：`bukit-theme.lock.json` 记录已解析的 commit。如果缓存的 commit 与锁定文件不一致，构建会失败（防止意外的远程更改）。
- **更新主题**：删除缓存目录或锁定文件即可强制重新克隆。

这对 CI/CD 流水线和需要主题版本一致性的团队环境尤为有用。
