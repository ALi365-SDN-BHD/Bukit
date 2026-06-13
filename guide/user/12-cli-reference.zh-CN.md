# 12 CLI 参考：用户常用命令（当前可用版）

本页仅列出当前仓库可执行的 `bukit` CLI 命令与参数。维护者级参数细节请看：[guide/dev/cli](./../dev/cli.md)。

> 提示：除 `version` 外，大多数命令都会先在标准错误输出 `bukit <version>`，用于确认当前运行版本。

## 命令总览

| 命令 | 用途 | 常用参数 |
|---|---|---|
| `build` | 生成静态站点 | `--config`、`--site`、`--output`、`--base-url`、`--site-url`、`--clean`/`--no-clean`、`--draft`、`--ci`、`--incremental`/`--no-incremental`、`--cache-dir`、`--jobs`、`--metrics`、`--log-format` |
| `config check` | 仅校验 `site.yaml` | `--config`、`--site`、`--site-url` |
| `config schema` | 生成 `site.yaml` JSON Schema | `--output` |
| `doctor` | 配置与模板健康检查 | `--config`、`--site`、`--site-url` |
| `preview` | 预览已构建目录 | `--dir`、`--host`、`--port`、`--strict-port`、`--config`、`--site` |
| `dev` | 实时开发预览，支持文件监控和浏览器刷新 | `--config`、`--site`、`--host`、`--port`、`--output`、`--no-watch` |
| `clean` | 清理输出与缓存目录 | `--config`、`--site`、`--dir` |
| `seo audit` | 校验 `seo-report.json` | `--dir`、`--report`、`--strict`、`--external` |
| `seo diff` | 对比两份 SEO 报告 | `--baseline`、`--current`、`--max-new-errors`、`--max-new-warnings`、`--max-new-issues`、`--fail-on-new-code`、`--fail-on-route-removed`、`--fail-on-indexable-drop` |
| `geo audit` | 校验 `geo-report.json` 与 llms 文件 | `--dir` |
| `publish audit` | 校验 `publish-audit-report.json` | `--dir`、`--report`、`--strict`、`--external` |
| `publish diff` | 对比两份发布审计报告 | `--baseline`、`--current`、`--max-new-errors`、`--max-new-warnings`、`--max-new-issues`、`--fail-on-new-code`、`--fail-on-route-removed`、`--fail-on-indexable-drop` |
| `deploy` | 构建（默认）并部署到 GitHub Pages | `--config`、`--site`、`--dry-run`、`--skip-build`、`--base-url`、`--site-url`、`--output`、`--branch`、`--message`、`--ci`、`--force` |
| `completion` | 生成 shell 自动补全脚本 | `<shell>`（`bash`/`zsh`/`fish`） |
| `version` | 输出 CLI 版本 | 无 |

## 运行方式

安装 `bukit` 二进制后：

```bash
bukit build --config site.yaml --clean
```

从源码运行：

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config site.yaml --clean
```

## build

构建站点到静态输出目录（默认通常是 `dist`）：

```bash
bukit build --config site.yaml --clean --site-url https://example.com
```

常用参数：
- `--config <path>`：配置路径（默认 `site.yaml`）
- `--site <name>`：使用 `sites/<name>.yaml`
- `--output <dir>`：覆盖输出目录
- `--base-url <path>`：覆盖 baseUrl
- `--site-url <url>`：覆盖 `site.url`
- `--clean` / `--no-clean`：是否构建前清理
- `--draft`：渲染草稿内容
- `--ci`：CI 下日志更偏谨慎
- `--incremental` / `--no-incremental`：增量开关
- `--cache-dir <dir>`：覆盖缓存目录
- `--metrics <path>`：输出构建指标 JSON
- `--jobs <n>`：渲染并发数（正整数）
- `--log-format text|json`：日志格式

## config check

仅校验配置，不进行构建：

```bash
bukit config check --config site.yaml
```

参数：`--config`、`--site`、`--site-url`。

## config schema

生成 `site.yaml` 的 JSON Schema：

```bash
bukit config schema --output site.schema.json
```

不加 `--output` 会直接输出到标准输出。

## doctor

诊断配置、模板、主题和内容源健康状态：

```bash
bukit doctor --config site.yaml
```

参数：`--config`、`--site`、`--site-url`。

## preview

启动静态目录本地预览服务：

```bash
bukit preview --dir dist --port auto
```

参数：
- `--dir <path>`：默认 `dist`（或从 `--config/--site` 推断）
- `--host <host>`：默认 `localhost`
- `--port <port|auto>`：默认 `4173`，`auto` 自动选空闲端口
- `--strict-port`：冲突则直接失败，不自动切换端口
- `--config`/`--site`：若提供则按配置推断输出目录

## dev

先构建一次站点，然后启动开发预览服务；文件变更时会增量重构建，并通过 WebSocket 刷新已连接的浏览器。

```bash
bukit dev --config site.yaml
bukit dev --port 3000
bukit dev --no-watch
```

参数：
- `--config <path>` / `--site <name>`：选择站点配置
- `--host <host>`：默认 `localhost`
- `--port <port>`：默认 `35729`，占用时自动递增
- `--output <dir>`：覆盖输出目录
- `--no-watch`：只作为静态服务器，不监控文件、不实时刷新

## clean

清理构建输出与缓存目录：

```bash
bukit clean --config site.yaml
```

参数：
- `--config <path>` / `--site <name>`：按配置解析输出目录（`build.output`）
- `--dir <path>`：手工指定输出目录（默认 `dist`）

## seo / geo / publish

### seo

读取 `dist/.bukit/seo-report.json` 做质量门禁：

```bash
bukit seo audit --dir dist --strict
```

对比两份报告：

```bash
bukit seo diff --baseline old/seo-report.json --current dist/.bukit/seo-report.json
```

### geo

读取 `dist/.bukit/geo-report.json`：

```bash
bukit geo audit --dir dist
```

### publish

读取 `dist/.bukit/publish-audit-report.json`：

```bash
bukit publish audit --dir dist
```

对比：

```bash
bukit publish diff --baseline old/publish-audit-report.json --current dist/.bukit/publish-audit-report.json
```

## deploy

部署前通常先构建一次（可用 `--skip-build` 跳过）：

```bash
bukit deploy --config site.yaml --dry-run
```

关键参数：
- `--config`/`--site`
- `--skip-build`
- `--dry-run`
- `--force`
- `--base-url`、`--site-url`、`--output`
- `--branch`
- `--message`
- `--ci`

## completion

```bash
bukit completion bash
bukit completion zsh
bukit completion fish
```

## version

```bash
bukit version
```

输出：
- `bukit <version>`
- `runtime: native-aot`

## Agent/LLM 使用

若以 agent 方式调用 CLI，请优先同步到 `src/skills/bukit-cli-reference/SKILL.md` 的命令约束。
