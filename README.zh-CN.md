# Bukit - .NET Native AOT 静态站点引擎

<p align="center">
  <img src="docs/bukit-logo.svg" alt="bukit logo" width="400">
</p>

语言版本：[English](./README.md) | 简体中文（当前）| [Bahasa Melayu](./README.ms.md)

Bukit 是面向 **笔记即 CMS**、**AI Agent 工作流**和 **GEO 优化网站**的 .NET Native AOT 静态站点生成引擎。它把 Markdown 和 Notion 内容转换为快速、可部署的静态网站。

## Bukit 是什么

Bukit 是运行时和构建引擎：

- 内容读取
- 路由生成
- Scriban 模板渲染
- SEO、GEO、feed、sitemap 和审计输出
- 静态 HTML 生成
- 通过 Core CLI 部署到 GitHub Pages

BukitJalil 是独立的本地控制面板。它不属于 Bukit runtime，也不是用 Bukit 构建站点的必要条件。

Bukit 不是 SaaS 平台、全功能 CMS 后端、可视化页面构建器，也不是 BukitJalil 的替代品。

## Core 1.0 能力

- **Native AOT CLI**：启动快、内存占用低，可面向 Linux、macOS、Windows 发布单文件二进制。
- **内容源**：Core 直接 provider 只有 Markdown 和 Notion。
- **笔记即 CMS**：Obsidian 和其他笔记应用通过 Markdown 兼容导出接入。飞书、语雀和其他知识库的直接集成属于未来工作。
- **Scriban 模板**：布局、局部模板、片段、集合页、分页、分类和多语言输出。
- **文件系统主题**：本地 `themes/<name>/` 目录，包含 layouts、assets、static 文件和必需的 `theme.yaml` 清单。
- **SEO 与 GEO 输出**：sitemap、RSS/Atom/JSON Feed、JSON-LD、Open Graph、Twitter Cards、canonical URL、hreflang、`llms.txt`、`robots.txt`、SEO audit、GEO audit 和 publish audit report。
- **LiveReload 开发服务器**：监听内容和主题文件，增量重建，通过 WebSocket 广播，并刷新浏览器。
- **静态预览服务器**：服务已经构建好的输出目录。
- **GitHub Pages 部署**：使用 `deploy.provider: github-pages` 和 `bukit deploy` 命令。

## 快速开始

从本仓库开发 Bukit 时：

```bash
dotnet build bukit-core.slnx -c Release
dotnet run --project src/Bukit-Core/Bukit.Cli -c Release -- config check --config path/to/site.yaml
dotnet run --project src/Bukit-Core/Bukit.Cli -c Release -- doctor --config path/to/site.yaml
dotnet run --project src/Bukit-Core/Bukit.Cli -c Release -- build --config path/to/site.yaml --clean --site-url https://example.com
```

在站点目录中使用已安装或已下载的 Bukit 二进制时：

```bash
bukit config check
bukit doctor
bukit build --clean
bukit dev
```

只需要服务已有构建产物时，使用 `bukit preview --dir dist`。

## Core CLI 命令

Bukit Core 1.0 只暴露以下稳定命令面：

| 命令 | 用途 |
|---|---|
| `build` | 构建静态站点 |
| `doctor` | 诊断配置、模板、provider 和构建就绪状态 |
| `config` | 校验配置或生成配置 schema |
| `preview` | 服务已有构建输出目录 |
| `dev` | 运行 LiveReload 开发服务器 |
| `clean` | 删除输出目录和缓存目录 |
| `version` | 输出版本信息 |
| `completion` | 生成 shell 自动补全 |
| `seo` | 审计或 diff SEO 报告 |
| `geo` | 审计 GEO 和 `llms.txt` 输出 |
| `publish` | 审计或 diff 发布就绪报告 |
| `deploy` | 将构建后的站点部署到 GitHub Pages |

稳定子命令是 `config check`、`config schema`、`seo audit`、`seo diff`、`geo audit`、`publish audit` 和 `publish diff`。

## 最小 `site.yaml`

```yaml
site:
  name: my-site
  title: My Site
  url: https://example.com
  baseUrl: /
  language: en
  collections:
    page:
      permalink: /{slug}/
      template: pages/page.html
      listRoute: /
      listTemplate: pages/index.html
content:
  sources:
    - type: markdown
      name: pages
      mode: content
      collection: page
      markdown:
        dir: content
build:
  output: dist
  clean: true
theme:
  name: starter
logging:
  level: info
```

Notion 也是 Core 内容 provider。在 `content.sources[]` 中添加 `notion` 源，并通过环境变量提供 `NOTION_TOKEN`，不要写入 `site.yaml`。

## 主题基础

Core 主题是本地文件系统目录：

```text
themes/<name>/
  layouts/
    layouts/base.html
    pages/page.html
    pages/post.html
    pages/index.html
    pages/list.html
    partials/
  assets/
  static/
  theme.yaml
```

在 `site.yaml` 中用 `theme.name` 选择主题。远程主题源、主题注册表、主题安装和主题市场工作流不属于 Core 1.0。

## 开发与预览

`bukit dev` 是 LiveReload 开发服务器。它会先执行一次构建，然后监听内容、布局、资源、静态文件和当前主题文件，按变更增量重建，并刷新已连接浏览器。它不会对前端框架组件做原地热替换。

`bukit preview` 是针对构建产物的静态文件服务器。已经执行 `bukit build` 且不需要文件监听时使用它。

## 部署

GitHub Pages 部署需要在站点中配置：

```yaml
deploy:
  provider: github-pages
  branch: gh-pages
```

然后校验并部署：

```bash
bukit config check
bukit doctor
bukit build --clean
bukit publish audit --dir dist
bukit deploy
```

CI 部署应使用站点自己的 GitHub Pages workflow。[`examples/github-pages-workflow.yml`](examples/github-pages-workflow.yml) 可作为站点 workflow 起点。不要复制本仓库的 release workflow；它用于发布 Bukit 二进制，不用于部署用户站点。

## 文档

| 范围 | 入口 |
|---|---|
| Core 用户指南 | [`guide/user`](guide/user/README.md) |
| Core 开发者指南 | [`guide/dev`](guide/dev/README.md) |
| Core 对齐的 Agent skills | [`guide/skills`](guide/skills/README.md) |
| Labs 与 preview 工作流 | [`guide/labs`](guide/labs/README.md) 和 [`guide/labs-skills`](guide/labs-skills/README.md) |
| 历史归档文档 | [`guide/archive`](guide/archive/README.md) |
| CLI 参考 | [`guide/user/12-cli-reference.md`](guide/user/12-cli-reference.md) |
| 配置参考 | [`guide/user/04-site-yaml-config.md`](guide/user/04-site-yaml-config.md) |
| GitHub Pages 部署 | [`guide/user/13-deploy-github-pages.md`](guide/user/13-deploy-github-pages.md) |

如果某份指南描述 clone、import、intent、webhook、远程主题源、主题注册表或外部插件市场行为，除非它被明确提升进 Core 命令白名单，否则应视为 Labs、preview 或历史材料。

## AI Agent Skills

面向 Agent 的说明位于 [`guide/skills`](guide/skills/README.md)。这套技能与 Core 1.0 对齐，只应教授稳定 Core 命令和契约。

Labs skills 位于 [`guide/labs-skills`](guide/labs-skills/README.md)。它们是显式选择使用的能力，不能当作默认 Core 行为。

## 稳定范围

**Bukit Core 1.0 Stable** 包含：

- [Core CLI 命令](#core-cli-命令)列出的命令
- `content.sources[]` 配置契约
- Markdown 和 Notion provider
- 通过 Markdown provider 接入的 Markdown 兼容笔记导出
- `content.media`
- 集合路由
- Scriban 渲染
- 本地文件系统主题
- 安全输出文件系统行为
- SEO、RSS、sitemap、JSON Feed 和搜索输出
- GEO、`llms.txt` 和 publish audit 输出
- 构建报告
- 增量构建
- Native AOT CLI
- GitHub Pages 部署

**不包含在 Core 1.0 中**：

- clone-to-theme
- HTML demo import
- import seed 工作流
- Notion push 或 Notion migration
- 主题注册表、主题市场、远程主题源或主题安装工作流
- 外部插件生态或插件市场
- AI intent 工作流
- webhook 自动化
- BukitJalil 控制面板
- 飞书、语雀和其他知识库的更广泛直接集成

## 路线图

| 领域 | 状态 |
|---|---|
| 构建、预览、dev、路由、模板 | Stable |
| Markdown、Notion、SEO/GEO、publish audit | Stable |
| GitHub Pages 部署 | Stable |
| 主题生态和模板工具 | Labs / Future |
| AI intent 工作流 | Labs / Future |
| 外部插件生态和市场 | Future |
| BukitJalil 控制面板 | Future |
| 更广泛的直接知识源集成 | Future |

## 参与贡献

欢迎贡献。请参阅：

- [`guide/dev/README.md`](guide/dev/README.md)
- [`guide/dev/testing.md`](guide/dev/testing.md)
- [`guide/dev/documentation-governance.md`](guide/dev/documentation-governance.md)

## 许可证

本项目基于 [LICENSE](./LICENSE) 中的条款授权。
