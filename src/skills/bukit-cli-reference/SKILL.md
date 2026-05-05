---
name: bukit-cli-reference
description: Use when using bukit CLI — agent needs to execute Bukit commands (build, init, preview, clean, doctor, plugin, theme, intent, webhook, version), detect whether the Bukit CLI tool is installed, install or upgrade bukit, or interpret bukit build output and exit codes
---

# Bukit CLI 命令操作指引

## Overview

Bukit 是一个 .NET 单文件可执行 CLI 工具，Agent 通过原生 Shell 直接执行 `bukit` 命令完成站点初始化、构建、预览等操作。本技能是所有 CLI 操作的单一知识源——其他 Bukit 技能引用本技能获取命令执行指引，不重复包含命令指令。

## CLI 检测

**检测 CLI 是否可用：**

```
bukit version
```

输出示例：
```
bukit 2.x.x
runtime: jit   (或 runtime: native-aot)
```

Windows 下如果是 `.exe` 文件不在 PATH 中，需使用 `.\bukit.exe` 或 `./bukit.exe`。PowerShell 下建议用 `&` 调用：

```powershell
& .\bukit.exe version
```

**特别注意**：除 `version` 命令外，其他所有命令在执行前都会在 stderr 输出版本号（如 `bukit 2.x.x`），这是正常行为，不是错误。

## 安装指引

Bukit 通过 GitHub Releases 分发平台二进制，不通过 NuGet 发布 dotnet tool。

| 方式 | 命令 | 适用场景 |
|------|------|---------|
| 直接下载二进制 | 从 [GitHub Releases](https://github.com/ALi365-SDN-BHD/Bukit/releases) 下载对应平台文件 | 推荐，无需 .NET SDK |
| 从源码构建 | `dotnet publish src/Bukit.Cli -c Release` | 开发者 / 尝鲜最新代码 |

下载后放置到 PATH 目录或项目根目录即可使用。

## 命令速查表

| 命令 | 用途 | 关键参数 |
|------|------|---------|
| `init` | 初始化站点脚手架 | `<target-dir>` `--provider`(markdown/notion) `--template`(minimal) |
| `create` | 同 `init`（别名） | 同上 |
| `build` | 构建静态站点 | `--config` `--output` `--base-url` `--draft` `--ci` `--incremental` / `--no-incremental` `--jobs` `--metrics` `--log-format` |
| `preview` | 本地预览 dist | `--dir` `--host` `--port` `--strict-port` |
| `clean` | 清理输出目录和缓存 | `--config` `--site` `--dir` |
| `doctor` | 诊断配置和模板 | `--config` `--site` `--site-url` |
| `plugin list` | 列出已注册插件 | `--config` `--site` |
| `theme list` | 列出 themes/ 下可用主题 | `--config` `--site` |
| `theme use` | 切换当前主题 | `<name>` `--config` `--site` |
| `intent init` | 交互式创建意图文件 | `--out` |
| `intent validate` | 验证意图文件 | `<intent.yaml>` `--root-dir` `--out` |
| `intent apply` | 应用意图生成 site.yaml | `<intent.yaml>` `--out` |
| `webhook` | 启动 Notion→GitHub webhook 服务 | `--repo` `--host` `--port` `--path` `--event` |
| `version` | 输出版本号 | 无参数 |

## 关键命令详解

### build

构建站点，将内容源 + 模板渲染为静态 HTML 文件。

```
bukit build [--config <path>] [--output <dir>] [--base-url <url>] [--draft] [--ci] [--incremental|--no-incremental] [--jobs <n>] [--metrics <path>] [--log-format text|json]
```

| 参数 | 说明 |
|------|------|
| `--config` | 指定 site.yaml 路径，默认当前目录 `site.yaml` |
| `--site` | 多站点模式下指定 `sites/<name>.yaml` |
| `--output` | 覆盖输出目录 |
| `--base-url` | 覆盖站点 baseUrl |
| `--site-url` | 覆盖站点 URL（用于 sitemap/RSS 等绝对链接） |
| `--clean` / `--no-clean` | 强制启用/禁用构建前清理 |
| `--draft` | 包含标记为 draft 的内容 |
| `--ci` | CI 模式（日志级别自动设为 warn） |
| `--incremental` / `--no-incremental` | 启用/禁用增量构建 |
| `--cache-dir` | 覆盖缓存目录 |
| `--metrics` | 输出 JSON 构建指标到指定文件 |
| `--jobs` | 并行渲染并发度（正整数） |
| `--log-format` | 日志格式：`text`（默认）或 `json` |

**工作目录要求：** 必须在包含 `site.yaml` 的站点根目录执行。

**退出码：** 0 = 成功

### init / create

在当前目录下初始化一个新的 Bukit 站点。

```
bukit init <target-dir> [--provider markdown|notion] [--template minimal]
```

生成的目录结构：
```
<target-dir>/
  site.yaml
  content/
    hello-world.md
  themes/starter/
    layouts/layouts/base.html
    layouts/pages/{page,post,index,list}.html
    layouts/partials/{header,footer}.html
    assets/style.css
    static/
  .gitignore
  README.md
```

`--provider notion` 会生成预配 Notion 内容源的 site.yaml，`--provider markdown`（默认）生成 Markdown 内容源配置。

### preview

启动本地 HTTP 文件服务器预览构建输出。

```
bukit preview [--dir <dir>] [--host <host>] [--port <port>] [--strict-port]
```

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `--dir` | `dist` | 要预览的目录 |
| `--host` | `localhost` | 监听地址 |
| `--port` | `4173` | 监听端口（`auto` = 自动选择空闲端口） |
| `--strict-port` | false | 端口被占用时直接报错，不自动切换 |

**端口选择逻辑：**
- 默认端口 4173 → 被占用则尝试 4174，最多尝试 20 次
- `auto` 模式：系统自动分配空闲端口
- `--strict-port` 模式：端口冲突直接报错

**MIME 类型支持：** HTML、CSS、JS、JSON、XML、SVG、PNG、JPG、GIF、TXT

### clean

清理输出目录和缓存目录。

```
bukit clean [--config <path>] [--site <name>] [--dir <dir>]
```

删除的内容：
- 输出目录（默认 `dist`，从 site.yaml 读取）
- `.cache/` 目录（增量构建清单等）
- `.bukit/` 目录

### doctor

诊断站点配置和模板健康状态。

```
bukit doctor [--config <path>] [--site <name>] [--site-url <url>]
```

检查项目：
1. Config 加载和验证
2. Collections 配置是否就位（否则提示迁移）
3. 模板文件存在性（base.html, page.html, post.html, index.html, list.html）
4. 模板语法解析
5. 模板 capabilities manifest 验证
6. Assets 和 Static 目录存在性
7. 构建清单 JSON 格式
8. 插件发现数量
9. Notion 数据库连通性（如配置了 Notion 内容源）
10. 列表页内容模式启发式回退警告

### plugin list

列出当前配置下所有已注册的插件及其状态。

```
bukit plugin list [--config <path>] [--site <name>]
```

输出格式：
```
PluginName@1.0.0 [BuiltIn] enabled=true (derive-pages, after-build)
PluginName@1.0.0 [ExternalAssembly] enabled=false (after-build)
```

### theme

```
bukit theme list [--config <path>] [--site <name>]
bukit theme use <name> [--config <path>] [--site <name>]
```

`theme list` 列出 `themes/` 目录下所有有效主题名。
`theme use` 修改 site.yaml 中的 `theme.name` 为指定主题名。

### intent

意图驱动配置：通过交互式问答或意图文件生成 site.yaml。

```
bukit intent init [--out <intent.yaml>]    # 交互式创建意图
bukit intent validate <intent.yaml>        # 验证意图文件
bukit intent apply <intent.yaml> [--out <path>]  # 应用意图生成 site.yaml
```

## 退出码

| 退出码 | 含义 |
|--------|------|
| 0 | 成功 |
| 1 | 运行时错误（配置错误、模板错误、Notion 连接失败等） |
| 2 | 参数错误（未知命令、无效参数、缺少必需参数） |

## 跨平台执行注意事项

| 场景 | 指引 |
|------|------|
| Windows | 可能需要 `.\bukit.exe` 或 `./bukit.exe`。PowerShell 下建议用 `& .\bukit.exe <cmd>` |
| Linux/macOS | `./bukit`，可能需要先 `chmod +x bukit`。放置到 `/usr/local/bin/` 即可全局调用 |
| 工作目录 | 始终在站点根目录（包含 `site.yaml` 的目录）执行 |
| 输出编码 | Windows 非英语环境下可能有编码问题 |
| 首次构建 | `build` 会创建 `dist/` 目录，首次为全量构建（无增量跳过） |
| stderr 版本输出 | 除 `version` 外，所有命令都向 stderr 输出版本号，不属于错误 |

## Agent 典型调用流程

用户说"帮我建一个 Bukit 博客"：

```
1. 检测 CLI: bukit version
   → CLI 不可用 → 引导安装
   → CLI 可用 → 继续

2. 初始化: bukit init ./my-blog --provider markdown

3. 加载 bukit-config skill → 按需修改 site.yaml

4. 加载 bukit-theme skill → 按需调整主题

5. 加载 bukit-templating skill → 按需编写模板

6. 构建: bukit build

7. 预览 (可选): bukit preview
```

## 常见错误

| 错误现象 | 原因 | 修复方法 |
|---------|------|---------|
| `Unknown command: xxx` | 命令名拼写错误 | 检查命令名，可执行 `bukit` 或 `bukit help` 查看完整列表 |
| `init requires a target directory` | 未指定目标目录 | `bukit init ./my-site` |
| `Directory not found: dist` | preview 前未构建或输出目录被清理 | 先执行 `bukit build` |
| `Failed to listen on ... (port conflict)` | 端口被占用且 strict-port 模式 | 换端口 `--port 8080` 或用 `--port auto` |
| Config 加载失败 | site.yaml 不存在或语法错误 | 检查路径，确保 YAML 语法正确 |
| Notion 连接失败 (401) | NOTION_TOKEN 未设置或无效 | 设置环境变量 `NOTION_TOKEN` |
| Notion 连接失败 (404) | databaseId 错误 | 检查 site.yaml 中 content.notion.databaseId |
| `Config error` (doctor) | site.collections 未配置 | 按 doctor 提示添加 collections 配置 |
| `Missing templates` (doctor) | 模板文件缺失 | 确保 themes/<name>/layouts/ 下有必需的 5 个模板文件 |

## 环境变量

| 变量 | 用途 | 相关命令 |
|------|------|---------|
| `NOTION_TOKEN` | Notion API 密钥 | build, doctor |
| `BUKIT_WEBHOOK_TOKEN` | Webhook 认证令牌 | webhook |
| `BUKIT_GITHUB_REPO` | GitHub 仓库名（owner/repo） | webhook |
| `BUKIT_GITHUB_TOKEN` | GitHub PAT | webhook |
| `GITHUB_TOKEN` | GitHub PAT（与上者 fallback） | webhook |
| `BUKIT_AUTO_SUMMARY` | 自动摘要开关（内部） | build |
| `BUKIT_AUTO_SUMMARY_MAXLEN` | 自动摘要最大长度（内部） | build |
