# 12 命令行参考：最常用命令与参数（用户版）

本页给普通用户一份"够用、好抄、少踩坑"的 CLI 速查。更完整的维护者版见：[guide/dev/cli](../dev/cli.md)。

说明：
- 你可以用 `bukit build --help`、`bukit preview --help`、`bukit theme --help` 查看命令专属参数
- 参数名称与默认值以 CLI 内置 help 为准

## 命令总览（你大概率只用这几个）

| 命令 | 你什么时候用它 |
|---|---|
| `create <dir>` | 新建一个站点工程（脚手架），也可用 `init` 别名 |
| `build` | 生成静态站点（输出到 dist/） |
| `preview` | 本地预览输出目录 |
| `dev` | 启动 HMR 开发服务器（文件监控 + 实时刷新） | `--config` `--site` `--host` `--port` `--output` `--no-watch` |
| `config check` | 只校验 site.yaml，不执行构建 |
| `config schema` | 生成 site.yaml JSON Schema |
| `doctor` | 环境/配置自检（排障第一步） |
| `clean` | 清理输出目录与缓存 |
| `theme` | 创建、列出、切换、探索、分享和安装主题 |
| `template` | 创建、列出、查看、校验、同步和浏览模板文件 |
| `clone` | 将任意网站的视觉设计克隆为 Bukit 主题 |
| `seo` | SEO 审计与 diff（验证 seo-report.json） |
| `publish` | 机器可读与可信发布审计（验证 publish-audit-report.json） |
| `visual` | 生成 Playwright 视觉测试脚本 |
| `webhook` | Notion 变更触发 GitHub Actions（可选） |
| `intent` | AI Intent 相关（可选） |
| `version` | 输出版本号 |

说明：
- 执行大多数命令时，CLI 会先输出一行 `bukit <version>`（用于确认当前运行版本；`help/version` 例外）

## 通用参数（build/doctor 等共用）

| 参数 | 作用 | 典型用法 |
|---|---|---|
| `--config <path>` | 指定配置文件路径 | `--config site.yaml` / `--config examples/starter/site.yaml` |
| `--site <name>` | 多站点读取 `sites/<name>.yaml` | `--site blog` |
| `--output <dir>` | 覆盖输出目录 | `--output dist` |
| `--base-url <path>` | 覆盖 baseUrl | `--base-url /my-repo` |
| `--site-url <url>` | 覆盖站点绝对 URL | `--site-url https://user.github.io/my-repo` |
| `--clean` / `--no-clean` | 构建前清理输出目录 | `--clean` |
| `--draft` | 渲染草稿内容 | `--draft` |
| `--incremental` / `--no-incremental` | 增量构建开关 | `--no-incremental`（排查用） |
| `--cache-dir <dir>` | 缓存目录 | `--cache-dir .cache` |
| `--jobs <n>` | 并行渲染并发度（正整数；默认 CPU 核心数） | `--jobs 8` |
| `--metrics <path>` | 输出构建指标 JSON | `--metrics metrics.json` |
| `--log-format <text|json>` | 日志格式 | `--log-format json`（CI 推荐） |
| `--ci` | CI 模式（日志级别默认 WARN） | `--ci`（GH Actions 推荐） |

## create / init：创建站点

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site
```

`init` 是 `create` 的等效别名：

```bash
dotnet run --project src/Bukit.Cli -c Release -- init my-site
```

Notion 模式（脚手架会生成对应配置）：

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site --provider notion
```

指定模板（默认 `minimal`）：

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site --template minimal
```

脚手架会生成 `themes/starter/`，这是一套内容站 starter 主题，包含可复用 partial、响应式 CSS，以及可选的分页/搜索/taxonomy 模板。

## build：构建站点（最常用）

在站点目录：

```bash
dotnet run --project ../src/Bukit.Cli -c Release -- build --clean --site-url https://example.com
```

### GitHub Pages 子路径（baseUrl）示例

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean --base-url /my-repo --site-url https://user.github.io/my-repo
```

### 产出 metrics 与结构化日志（CI 推荐）

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean --metrics metrics.json --log-format json
```

## preview：本地预览输出目录

```bash
dotnet run --project src/Bukit.Cli -c Release -- preview --dir dist --port auto
```

常用参数：

- `--dir <path>`：预览目录（默认 `dist`）
- `--host <host>`：默认 `localhost`
- `--port <port|auto>`：`auto` 自动选端口
- `--strict-port`：严格端口模式（端口占用时报错而非自动切换）

### `dev`

```
bukit dev [--config <path>] [--site <name>] [--host <host>] [--port <port>] [--output <dir>] [--no-watch]
```

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `--config` | `site.yaml` | 配置文件路径 |
| `--site` | — | 多站点名 |
| `--host` | `localhost` | 监听地址 |
| `--port` | `35729` | 监听端口（被占用时自动递增） |
| `--output` | `dist` | 输出目录 |
| `--no-watch` | false | 禁用文件监控（仅作为静态服务器） |

工作原理：
1. 执行一次全量初始构建
2. 启动 HTTP 服务器 + WebSocket 端点
3. 监控 content/、themes/、layouts/ 等目录的文件变更
4. 检测到变更时，自动增量重构建，并通过 WebSocket 通知浏览器刷新

每个 HTML 响应中自动注入 livereload 脚本，连接到 WebSocket 端点以接收实时刷新通知。

与 `preview` 的区别：`bukit dev` 适合开发阶段（自动构建 + 实时刷新），`bukit preview` 仅用于预览已构建的 `dist/` 目录。

## config check：只校验配置

```bash
dotnet run --project src/Bukit.Cli -c Release -- config check --config site.yaml
```

当你只想验证 `site.yaml`，但不想加载内容、渲染模板、连接 Notion 或执行构建时使用它。命令会解析配置路径，应用 `--site-url` 覆盖，然后运行配置校验。

常用参数：

- `--config <path>`：配置文件路径
- `--site <name>`：多站点配置，读取 `sites/<name>.yaml`
- `--site-url <url>`：覆盖 `site.url` 后再校验

## config schema：生成配置 Schema

```bash
dotnet run --project src/Bukit.Cli -c Release -- config schema --output site.schema.json
```

生成 JSON Schema，供 VSCode/YAML LSP 等工具对 `site.yaml` 做自动补全与基础校验。不传 `--output` 时会直接输出到 stdout。

## doctor：自检与排障（第一步）

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config site.yaml
```

当你遇到这些问题，先跑 doctor：

- Notion token 缺失
- 路径不存在（content/theme/build output）
- 配置字段写错、类型不匹配
- **模板变量拼写错误** — 变量名写错导致静默空值，doctor 会检测
- **路由冲突** — 以 `[BKT-0201]` 诊断码格式显示

所有配置错误现在显示稳定的诊断码：
```
✖ Config error
[BKT-0601] Refusing to clean unsafe output directory: /Users/xxx.

--- Template variable spell check ---
⚠ pages/index.html: Unknown variable 'site.settings' — did you mean 'site.params'?
✔ No unknown template variables detected
```

排障清单见：[14-故障排查](./14-troubleshooting.zh-CN.md)。

## import：HTML Demo 与 Seed 导入

```bash
# 把本地 HTML demo 转成可构建的主题/站点草稿
bukit import html-demo ./demo --theme silkroadbiz --force --verify

# 把生成的 JSON/YAML seed 转成本地 Markdown 内容
bukit import seed sites/silkroadbiz/data --output sites/silkroadbiz/content --force
```

`import html-demo` 会生成 `themes/<theme>/`、`sites/<theme>/site.yaml`、本地 Markdown 草稿、seed 审核文件、`original-demo/` 和 `import-report.md`。默认是 `--content-source notion --build-source markdown`：生成 Notion seed 供审核，但站点仍从本地 Markdown 构建，`--verify` 不需要外部凭据。只有当生成站点需要构建时直接读取 Notion，才使用 `--build-source notion`，且它只能和 `--content-source notion` 一起使用。

`import seed` 只把 JSON/YAML seed 转成本地 Markdown，不会写入 Notion。

Notion 写入必须显式执行：

```bash
bukit notion push --input sites/silkroadbiz/notion-seed --dry-run
bukit notion push --input sites/silkroadbiz/notion-seed --database-id <notion-database-id>
bukit notion validate-schema --database-id <notion-database-id> --report notion-schema-report.json
```

`notion validate-schema` 会校验目标 Notion database 是否包含 Bukit seed push 需要的字段。默认读取 `NOTION_TOKEN`，也可以用 `--token-env <name>` 指定其他环境变量。

完整流程、审核清单和 Notion 推送决策见：[21-导入-HTML-Demo](./21-import-html-demo.zh-CN.md)。

## clean：清理输出与缓存

```bash
dotnet run --project src/Bukit.Cli -c Release -- clean --dir dist
```

建议在以下情况下执行：

- 切换了主题目录结构
- 大量改动路由规则/输出模式
- 怀疑增量缓存导致"看起来没更新"

## theme：主题创建、发现与分享

```bash
# 列出所有主题（显示版本、描述、标签）
bukit theme list --config site.yaml

# 从 starter 创建
bukit theme create custom --config site.yaml --brand "My Site" --primary-color "#0b5fff" --use

# 交互式向导（问答式，可选择预设）
bukit theme wizard my-blog

# 使用预设快速创建
bukit theme wizard my-blog --preset blog

# 查看主题详情
bukit theme info starter --config site.yaml

# 列出主题参数
bukit theme params --config site.yaml

# 切换当前主题
bukit theme use alt --config site.yaml
```

`theme create` 默认从内置 starter 创建 `themes/<name>/`。使用 `--from <已有主题>` 可复制已有主题，`--force` 可覆盖，`--use` 会把 `theme.name` 写回当前配置。

`theme wizard` 运行交互式问答。使用 `--preset`（blog/docs/landing/minimal/portfolio）可基于预设快速创建。

### 主题分发

```bash
# 打包主题以供分享
bukit theme pack my-blog          # → my-blog-1.0.0.tar.gz

# 从本地文件安装
bukit theme install ./my-blog-1.0.0.tar.gz

# 从 URL 安装
bukit theme install https://github.com/user/theme/releases/download/v1.0/theme.tar.gz

# 搜索社区主题仓库（Experimental）
bukit theme search               # 列出 Experimental registry 条目
bukit theme search blog          # 按名称/标签过滤

# 从仓库安装（Experimental）
bukit theme install --registry blog-clean
```

## template：模板级别管理

```bash
# 列出当前主题中的所有模板
bukit template list --config site.yaml

# 查看模板内容
bukit template show pages/index.html --config site.yaml

# 校验所有模板的 Scriban 语法
bukit template validate --config site.yaml

# 交互式创建模板
bukit template create pages/gallery.html --config site.yaml

# 浏览代码片段库
bukit template snippets
bukit template snippets post-card

# 显示所有可用模板变量
bukit template hints

# 自动生成 bukit.templates.yaml
bukit template sync --config site.yaml
```

### theme preview

显示主题详细信息，包括 sections、components、设计令牌和布局模板。

```
bukit theme preview [<name>]
```

| 参数 | 默认值 | 说明 |
|---|---|---|
| `<name>` | 当前主题 | 要预览的主题名称 |

**输出包含：**
- 基本元数据：名称、版本、描述、主页、缩略图、标签
- Sections：数量、描述、插件关联
- Components：数量和声明的 props
- 设计令牌：分组计数（colors/font/radius/spacing/layout）及颜色采样
- 布局模板：`layouts/` 下所有 `.scriban`/`.html`/`.sbn` 文件
- 文件统计：assets 和 static 文件数量

示例输出：
```
Theme preview: my-blog
Version:      1.0.0
Description:  A clean blog theme with dark mode support
Tags:         blog, minimal, dark-mode

Sections (4):
  hero                      Hero section with CTA
  features                  Feature grid section
  recent-posts              Recent posts list
  footer-cta                Footer call-to-action [plugin: sample-plugin]

Components (2):
  PostCard                  props: [title, url, date]
  TagBadge                  props: [tag]

Design tokens: colors (12), font (8), radius (4), spacing (10)
  Color samples:
    primary: #0b5fff
    accent: #0f7b6c
    bg: #fbfaf8
    text: #202124
    ... and 8 more

Layout templates (8):
  layouts/base.html
  pages/index.html
  pages/list.html
  pages/page.html
  pages/post.html
  partials/footer.html
  partials/header.html
  partials/list-card.html

Assets: 3 files  |  Static: 1 files
Local path:   /project/themes/my-blog
```

主题与模板详细使用见：[08-主题与模板](./08-themes-templates.zh-CN.md)。

## clone：将网站视觉设计克隆为主题

```bash
# 克隆网站的视觉设计
bukit clone https://example.com --name my-theme

# 指定输出目录
bukit clone https://example.com --name my-theme --output ./themes

# 仅克隆特定页面
bukit clone https://example.com/about --name about-theme --page-only
```

`clone` 命令分析目标网站的颜色、排版、间距、布局等视觉元素，生成对应的 Bukit 主题文件。

## webhook：Notion 变更触发 GitHub Actions（可选）

```bash
dotnet run --project src/Bukit.Cli -c Release -- webhook --repo owner/repo --port 8787 --path /webhook/notion --event bukit_notion
```

可用参数：

- `--host <host>`：监听地址（默认 `localhost`）
- `--port <port>`：监听端口（默认 `8787`）
- `--path <path>`：HTTP 路径（默认 `/webhook/notion`）
- `--repo <owner/repo>`：目标仓库
- `--event <type>`：repository_dispatch 事件类型

它需要环境变量：

- `BUKIT_WEBHOOK_TOKEN`（入站请求头 `X-Sitegen-Token`）
- `BUKIT_GITHUB_TOKEN`（或 `GITHUB_TOKEN`）

安全与部署说明见开发者文档：[guide/dev/webhook](../dev/webhook.md)。

## seo：验证 SEO 报告质量

```bash
# 审计当前 seo-report.json
bukit seo audit --dir dist --config site.yaml

# 严格模式（warning 也失败）
bukit seo audit --dir dist --strict

# 同时检查外部链接和图片
bukit seo audit --dir dist --external

# 对比两份报告（回归检查）
bukit seo diff --dir dist --config site.yaml

# diff 带预算控制
bukit seo diff --max-new-errors 3 --max-new-warnings 5
bukit seo diff --fail-on-route-removed
bukit seo diff --fail-on-indexable-drop
```

`seo audit` 校验 `build` 生成的 `seo-report.json` — 检查 schema 结构、统计错误/警告数，可选验证外部链接。`seo diff` 与上一份报告对比，检测回归。

## publish：校验机器可读与可信发布

```bash
# 审计当前 publish-audit-report.json
bukit publish audit --dir dist

# warning 也按失败处理
bukit publish audit --dir dist --strict

# 对比两份发布审计报告
bukit publish diff --baseline previous/.bukit/publish-audit-report.json --current dist/.bukit/publish-audit-report.json
```

`publish audit` 校验 `.bukit/publish-audit-report.json`，这是语义 HTML、来源/审核可信度、representation 覆盖率和聚合输出一致性的主报告。`seo audit` 仍用于兼容 SEO 报告。

## visual: 生成视觉测试脚本

```bash
bukit visual generate [--config site.yaml] [--dir dist] [--site-url http://localhost:4173] [--out visual-tests.spec.js]
```

**参数说明:**

| 参数 | 用途 | 默认值 |
|---|---|---|
| `--config` | 配置文件路径 | `site.yaml` |
| `--dir` | 包含已构建 HTML 的输出目录 | `dist` |
| `--site-url` | 测试页面导航的基础 URL | `http://localhost:4173` |
| `--out` | 输出脚本文件名 | `visual-tests.spec.js` |

生成 Playwright 测试脚本，对输出目录中的每个 HTML 页面进行全页截图并与视觉基线对比。

**使用流程:**
1. `bukit build`
2. `bukit visual generate --dir dist`
3. `npx playwright test visual-tests.spec.js --update-snapshots`（首次运行）
4. `npx playwright test visual-tests.spec.js`（后续运行）

另见: `VisualFeedbackPlugin`（构建后插件，通过 AI 进行 5 维度视觉质量评分分析）。

## data：内容源检查

> **高级。** 检查或导出内容源数据，用于调试。

```bash
bukit data inspect --source page --config site.yaml
bukit data dump --source page --format json --config site.yaml
```

## docs：文档一致性检查

> **维护者。** 验证文档一致性（CLI 覆盖率、配置字段引用、文件引用）。

```bash
bukit docs check
```

## geo：GEO 审计

> **预览。** 审计 GEO（生成引擎优化）就绪状态。

```bash
bukit geo audit --dir dist --config site.yaml
```

## intent：AI 意图管理

> **预览。** 初始化、应用或验证站点意图。

```bash
bukit intent init
bukit intent apply
bukit intent validate
```

## route：路由检查

> **高级。** 检查路由解析，用于调试。

```bash
bukit route inspect --url /blog/hello-world/ --config site.yaml
```

## dev：HMR 开发服务器

> **高级。** 启动 HMR 开发服务器，支持实时重载。

```bash
bukit dev --config site.yaml
```

## plugin：插件管理

> **高级。** 列出已安装的插件。

```bash
bukit plugin list --config site.yaml
```

## deploy：部署到 GitHub Pages

将构建后的站点部署到配置的提供商（如 GitHub Pages）。

```bash
bukit deploy --config site.yaml
```

详见 [13 部署 GitHub Pages](./13-deploy-github-pages.md)。

## completion：Shell 补全

> **高级。** 生成 Shell 自动补全脚本。

```bash
bukit completion bash
bukit completion zsh
```

## lint：内容检查

> **高级。** 检查内容文件的 schema 合规性。

```bash
bukit lint --config site.yaml
```

## version：查看版本

```bash
dotnet run --project src/Bukit.Cli -c Release -- version
```

输出当前 CLI 版本号。
