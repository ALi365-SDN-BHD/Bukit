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

排障清单见：[14-故障排查](./14-troubleshooting.zh-CN.md)。

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

# 搜索社区主题仓库
bukit theme search               # 列出全部
bukit theme search blog          # 按名称/标签过滤

# 从仓库安装
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

## version：查看版本

```bash
dotnet run --project src/Bukit.Cli -c Release -- version
```

输出当前 CLI 版本号。
