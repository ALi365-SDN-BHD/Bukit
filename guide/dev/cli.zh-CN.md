# CLI 参数参考（维护者）

本文档面向维护者，要求与实现保持一致：`src/Bukit.Cli/Cli/BukitCliSpecs.cs` 与 `src/Bukit.Cli/Cli/BukitCliDescriptors.cs`。

## 当前支持的顶层命令

| 命令 | 用途 | 实现文件 |
|---|---|---|
| `build` | 生成静态站点 | `src/Bukit.Cli/Commands/BuildCommand.cs` |
| `config` | 配置校验/Schema 生成 | `src/Bukit.Cli/Commands/ConfigCommand.cs` |
| `clean` | 清理构建输出和 `.cache/.bukit` | `src/Bukit.Cli/Commands/CleanCommand.cs` |
| `completion` | 生成 shell 自动补全脚本 | `src/Bukit.Cli/Commands/CompletionCommand.cs` |
| `deploy` | 构建并部署到 GitHub Pages | `src/Bukit.Cli/Commands/DeployCommand.cs` |
| `doctor` | 配置 / 主题 / 模板诊断 | `src/Bukit.Cli/Commands/DoctorCommand.cs` |
| `geo` | GEO 质量门禁（` .bukit/geo-report.json`） | `src/Bukit.Cli/Commands/GeoCommand.cs` |
| `preview` | 本地静态预览服务器 | `src/Bukit.Cli/Commands/PreviewCommand.cs` |
| `publish` | 发布质量门禁（`.bukit/publish-audit-report.json`） | `src/Bukit.Cli/Commands/PublishCommand.cs` |
| `seo` | SEO 质量门禁（`.bukit/seo-report.json`） | `src/Bukit.Cli/Commands/SeoCommand.cs` |
| `version` | 输出版本与运行时 | `src/Bukit.Cli/Commands/VersionCommand.cs` |

子命令：
- `config check`
- `config schema`
- `seo audit`
- `seo diff`
- `geo audit`
- `publish audit`
- `publish diff`

## 覆盖优先级

对于可覆盖配置字段的参数，优先级为：

1. CLI 入口参数（如 `--output`/`--base-url`/`--site-url`/`--clean` 等）
2. 配置文件值
3. 运行时默认值

## build

```bash
bukit build --config site.yaml --clean --site-url https://example.com
```

常见参数：
- `--output <dir>`，`--base-url <path>`，`--site-url <url>`
- `--clean` / `--no-clean`
- `--draft`
- `--incremental` / `--no-incremental`
- `--cache-dir <dir>`
- `--metrics <path>`
- `--jobs <n>`
- `--log-format text|json`

## config

### `config check`

```bash
bukit config check --config site.yaml --site demo --site-url https://example.com
```

### `config schema`

```bash
bukit config schema --output site.schema.json
```

省略 `--output` 时输出到 stdout。

## doctor

```bash
bukit doctor --config site.yaml
```

包括：
- 配置合法性
- 主题 manifest 与必需模板
- Scriban 语法与变量引用链条
- 模板能力清单校验
- Markdown front matter / 语法 / 空文档检查
- 硬编码 URL、插件连通性、主题目录检查
- Notion token 与可选 schema 检查

## preview

```bash
bukit preview --dir dist --port auto
```

参数：
- `--dir <path>`（默认 `dist`）
- `--host <host>`（默认 `localhost`）
- `--port <port|auto>`（默认 `4173`，`auto` 自动取空闲端口）
- `--strict-port`（端口冲突直接失败）

## clean

```bash
bukit clean --config site.yaml
```

清理：
- 目标输出目录（配置值或 `--dir`）
- `.cache/`
- `.bukit/`

## deploy

```bash
bukit deploy --config site.yaml --dry-run
```

关键参数：
- `--dry-run`
- `--skip-build`
- `--force`
- `--base-url`、`--site-url`、`--output`
- `--branch`、`--message`
- `--ci`

默认会先执行 `build`，除非传 `--skip-build`。

## seo / geo / publish

- `seo audit [--dir dist] [--report file] [--strict] [--external]`
- `seo diff --baseline <old> --current <new> ...`
- `geo audit [--dir dist]`
- `publish audit [--dir dist] [--report file] [--strict] [--external]`
- `publish diff --baseline <old> --current <new> ...`

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
