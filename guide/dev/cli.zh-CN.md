# 命令行（CLI）参数参考

本文档面向维护者，目标是把 CLI 的命令、参数、覆盖关系与常见用法说清楚。

实现参考：
- `src/Bukit.Cli/Cli/BukitCliSpecs.cs`
- `src/Bukit.Cli/Cli/Parsing/CliParser.cs`
- `src/Bukit.Cli/Commands/*Command.cs`

说明：
- 顶层命令与首批命令 help 已由元数据层统一生成
- `--jobs` 已进入统一 help 口径
- 参数定义现在以 `BukitCliSpecs.CreateRegistry()` 中的 `CliCommandSpec` / `CliOptionSpec` 声明为准

## 命令总览

| 命令 | 作用 |
|---|---|
| `create <dir>` | 从零创建站点工程（等价 `init`） |
| `init <dir>` | 初始化站点工程骨架 |
| `build` | 生成静态站点 |
| `dev` | HMR 开发服务器（文件监控 + 增量构建 + 浏览器实时刷新） |
| `preview` | 本地预览输出目录 |
| `clean` | 清理输出与缓存 |
| `config check` | 验证配置但不构建站点 |
| `config schema` | 生成 site.yaml JSON Schema |
| `doctor` | 环境与配置诊断 |
| `plugin` | 插件相关命令 |
| `theme` | 主题相关命令 |
| `template` | 模板相关命令 |
| `intent` | AI Intent 相关命令 |
| `deploy` | 构建并部署到 GitHub Pages |
| `seo` | SEO 审计与回归检测 |
| `geo` | GEO（生成式引擎优化）审计 |
| `clone` | 从目标网站提取数据生成主题与内容 |
| `webhook` | Webhook 触发器 |
| `data` | 数据模块查看与导出 |
| `completion` | 生成 shell 自动补全脚本 |
| `lint` | 检查配置和 Markdown 内容 |
| `visual` | 生成 Playwright 视觉回归测试脚本 |
| `docs` | 文档一致性检查 |
| `version` | 版本信息 |

说明：
- 执行大多数命令时，CLI 会先输出一行 `bukit <version>`（用于确认当前运行版本；`help/version` 例外）
- 版本号来自 `src/Bukit.Cli/Bukit.Cli.csproj` 的 `BuildInfoVersionBase`，也可通过 `VersionPrefix` 或 `Version` 覆盖

## 关键覆盖关系

构建相关的覆盖顺序（从高到低）：

1. CLI 参数（例如 `--output` / `--base-url` / `--clean` / `--draft` / `--site-url`）
2. `site.yaml`
3. 代码默认值（见 `Bukit.Config` 的默认值与 `ConfigLoader`）

## 通用构建参数（build/doctor 等共用）

来源：`BukitCliSpecs` 与 `BuildCommand`

| 参数 | 作用 | 覆盖字段/行为 |
|---|---|---|
| `--config <path>` | 指定配置文件路径 | 作为 config rootDir 与默认相对路径基准 |
| `--site <name>` | 多站点读取 `sites/<name>.yaml` | rootDir 仍为当前目录 |
| `--output <dir>` | 覆盖输出目录 | 覆盖 `build.output` |
| `--base-url <path>` | 覆盖 baseUrl | 覆盖 `site.baseUrl` |
| `--site-url <url>` | 覆盖站点绝对 URL | 覆盖 `site.url`（用于 sitemap/rss） |
| `--clean` | 构建前清理 | 覆盖 `build.clean=true` |
| `--no-clean` | 禁用构建前清理 | 覆盖 `build.clean=false` |
| `--draft` | 渲染草稿 | 覆盖 `build.draft=true` |
| `--ci` | CI 模式 | 会影响日志等级等策略（示例：build 默认 WARN） |
| `--incremental` | 启用增量构建 | 覆盖增量开关（默认启用） |
| `--no-incremental` | 关闭增量构建 | 覆盖增量开关 |
| `--cache-dir <dir>` | 覆盖缓存目录 | 默认 `<rootDir>/.cache` |
| `--jobs <n>` | 并行渲染并发度 | 正整数；默认 CPU 核心数 |
| `--metrics <path>` | 输出构建指标 JSON | 相对路径按 rootDir 解析 |
| `--log-format <text|json>` | 控制日志输出格式 | 默认 `text` |

## build

实现参考：`src/Bukit.Cli/Commands/BuildCommand.cs`

常用示例：

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean
```

多站点：

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --site blog --clean
```

覆盖输出与 baseUrl（GitHub Pages 子路径）：

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --output dist --base-url /my-repo --site-url https://user.github.io/my-repo --clean
```

## dev

实现参考：`src/Bukit.Cli/Commands/Dev/`（DevServerHost、DevWebSocketHub、DevFileWatcher、DevRequestHandler、DevPathGuard）

```
bukit dev [--config <path>] [--site <name>] [--host <host>] [--port <port>] [--output <dir>] [--no-watch]
```

开发用途：监控 content/themes/layouts/assets/static 目录的文件变更，自动增量重构建，通过 WebSocket 实时刷新浏览器。端口默认 35729，`--no-watch` 禁用文件监控（纯静态服务）。

## preview

实现参考：`src/Bukit.Cli/Commands/PreviewCommand.cs`

| 参数 | 默认值 | 说明 |
|---|---|---|
| `--dir <path>` | `dist` | 预览目录 |
| `--host <host>` | `localhost` | 监听地址 |
| `--port <port\|auto>` | `4173` | `auto` 自动选择可用端口 |
| `--strict-port` | false | 端口占用则失败（默认会递增重试） |

## doctor / clean / config / theme / template / deploy / seo / geo / clone / plugin / intent / webhook / data / completion / lint / visual / docs

这些命令的参数细节随版本演进，优先以对应 `*Command.cs` 为准：

- `src/Bukit.Cli/Commands/DoctorCommand.cs`
- `src/Bukit.Cli/Commands/CleanCommand.cs`
- `src/Bukit.Cli/Commands/ConfigCommand.cs`
- `src/Bukit.Cli/Commands/ThemeCommand.cs`
- `src/Bukit.Cli/Commands/TemplateCommand.cs`
- `src/Bukit.Cli/Commands/DeployCommand.cs`
- `src/Bukit.Cli/Commands/SeoCommand.cs`
- `src/Bukit.Cli/Commands/GeoCommand.cs`
- `src/Bukit.Cli/Commands/Clone/`（CloneInputLoader、CloneAssetDownloader、CloneContentWriter、CloneFidelityRunner、CloneThemeGenerator、CloneVerifier）
- `src/Bukit.Cli/Commands/PluginCommand.cs`
- `src/Bukit.Cli/Commands/IntentCommand.cs`
- `src/Bukit.Cli/Commands/WebhookCommand.cs`
- `src/Bukit.Cli/Commands/DataCommand.cs`
- `src/Bukit.Cli/Commands/CompletionCommand.cs`
- `src/Bukit.Cli/Commands/LintCommand.cs`
- `src/Bukit.Cli/Commands/VisualCommand.cs`
- `src/Bukit.Cli/Commands/DocsCheck/DocsCheckCommand.cs`

补充说明：

- init/create 的脚手架输出与目录结构见 [init/create](./init-create.md)。
- doctor 的检查项与常见失败修复见 [doctor](./doctor.md)。
- clean 与缓存目录语义见 [缓存与清理](./cache-clean.md)。
- theme 的开发与参数使用见 [主题开发](./theme.md)。
- intent 的 CLI 落地与 rootDir 推断规则见 [Intent](./intent-cli.md)。
- webhook 的安全约束与环境变量说明见 [Webhook](./webhook.md)。
