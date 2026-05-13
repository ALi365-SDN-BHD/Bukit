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
| `doctor` | 环境/配置自检（排障第一步） |
| `clean` | 清理输出目录与缓存 |
| `theme` | 列出/切换主题 |
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

## theme：列出与切换主题

```bash
dotnet run --project src/Bukit.Cli -c Release -- theme list --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- theme use alt --config site.yaml
```

主题使用见：[08-主题与模板](./08-themes-templates.zh-CN.md)。

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

## version：查看版本

```bash
dotnet run --project src/Bukit.Cli -c Release -- version
```

输出当前 CLI 版本号。
