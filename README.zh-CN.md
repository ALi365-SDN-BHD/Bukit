# Bukit — .NET Native AOT 静态站点引擎

<p align="center">
  <img src="docs/bukit-logo.svg" alt="bukit logo" width="400">
</p>

语言版本：[English](./README.md) | 简体中文（当前）| [Bahasa Melayu](./README.ms.md)

Bukit 是一个面向 **笔记即 CMS**、**AI Agent 工作流**和 **GEO 优化**的 .NET Native AOT 静态网站生成引擎。将 Notion 数据库和 Markdown 内容转换为高性能、可部署的静态网站。

## 什么是 Bukit？

```
Bukit
= 静态网站引擎
= 构建核心
= 内容读取、路由生成、Scriban 模板渲染、SEO/GEO 输出

BukitJalil
= 本地应用 / 控制面板
= 项目管理、主题管理、AI 对话式操作、构建发布控制

Notes-as-CMS
= 内容生产方式
= Notion / Markdown / Obsidian / 飞书 / 语雀 / 其他知识库
```

Bukit 负责内容读取、路由生成、Scriban 模板渲染、SEO/GEO 输出和静态 HTML 生成。适用于公司官网、文档站、内容站、落地页和 AI 辅助发布工作流。

**BukitJalil** 是独立的本地控制面板，不属于 Bukit 运行时引擎，使用 Bukit 构建网站不需要它。

Bukit **不是** SaaS 平台、全功能 CMS 后端、可视化页面构建器或 BukitJalil 的替代品。

## 为什么选择 Bukit？

- **Native AOT** — 启动低于 50ms，内存占用低，Linux/macOS/Windows 单文件部署
- **笔记即 CMS** — 用 Notion 或 Markdown 写内容，Bukit 转为静态站点
- **AI Agent 原生** — `src/skills/` 提供 AI 编程助手的知识层
- **GEO 就绪** — 内置 AI 搜索引擎优化，支持 `llms.txt`、FAQ/HowTo 结构化数据、GEO 评分审计

## 核心功能

- **Markdown 与 Notion 内容源**，支持可配置的字段映射
- **Scriban 模板引擎**，支持布局继承、局部模板和代码片段库
- **基于集合的路由**，支持永久链接、列表页、分页和分类
- **多语言支持** — 按语言构建，合并 sitemap/RSS/搜索
- **SEO** — sitemap、RSS/Atom/JSON Feed、JSON-LD、Open Graph、Twitter Cards、canonical URL、hreflang
- **GEO** — `llms.txt`、AI 爬虫 robots.txt 规则、FAQ/HowTo 结构化数据、GEO 评分审计
- **主题系统**，含设计令牌和组件化主题；主题注册表计划在下一阶段推出
- **HMR 开发服务器**（WebSocket 实时刷新）；构建产物的本地预览服务器
- **插件系统** — 内置 `derive-pages` 与 `after-build` 钩子；外部进程 / WASM 插件生态为计划特性，不属于 Core 1.0。
- **增量构建** — 内容感知变更检测；可选 SHA256 资源哈希
- **GitHub Pages 部署**，通过 CLI 或 GitHub Actions 工作流

## 快速开始

```bash
# 构建 CLI
dotnet build bukit.slnx -c Release

# 验证示例站点配置
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml

# 构建示例站点
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com

# 本地预览
dotnet run --project src/Bukit.Cli -c Release -- preview --dir examples/starter/dist --port auto
```

完整上手教程见 [快速开始指南](guide/user/01-quick-start.zh-CN.md)。

## 文档

| 读者 | 入口 |
|---|---|
| 新用户 | [`guide/user`](guide/user/README.zh-CN.md) — 快速开始、配置、内容、部署、排障 |
| 维护者 / 贡献者 | [`guide/dev`](guide/dev/README.zh-CN.md) — 架构、CLI 契约、渲染、插件、可观测性 |
| AI Agent 用户 | [`src/skills`](src/skills/README.zh-CN.md) — 面向 Codex、Claude Code、Copilot、Gemini CLI 的技能文件 |
| AI 建站 | [`guide/ai/chatgpt`](guide/ai/chatgpt/README.zh-CN.md) — ChatGPT 提示包与意图契约 |
| CLI 参考 | [`guide/user/12-cli-reference.zh-CN.md`](guide/user/12-cli-reference.zh-CN.md) |
| 配置参考 | [`guide/user/04-site-yaml-config.zh-CN.md`](guide/user/04-site-yaml-config.zh-CN.md) |
| 部署 | [`guide/user/13-deploy-github-pages.zh-CN.md`](guide/user/13-deploy-github-pages.zh-CN.md) |

## Notion CMS 工作流

- 在 `content.sources[]` 中添加 `notion` 源（参见[配置参考](guide/user/04-site-yaml-config.zh-CN.md)）
- 通过环境变量提供 Token：`NOTION_TOKEN`（切勿写在 `site.yaml` 中）
- 默认数据库字段：`Published`（checkbox）、`Title`、`Slug`、`Type`（post/page）、`PublishAt`
- 完整指南：[`guide/user/06-notion-content.zh-CN.md`](guide/user/06-notion-content.zh-CN.md)
- Schema 参考：[`guide/dev/content.zh-CN.md`](guide/dev/content.zh-CN.md)

## AI / Agent 工作流

`src/skills/` 是面向 AI Agent 的知识层，不是运行时代码。帮助编程助手理解 Bukit CLI、配置、主题、模板、Notion、路由、i18n、部署、SEO/GEO 和调试。

- 适用工具：Codex CLI、Claude Code、Copilot CLI、Gemini CLI 等
- 普通用户：从 [`guide/user`](guide/user/README.zh-CN.md) 开始
- Agent 用户：从 [`src/skills/using-bukit/SKILL.md`](src/skills/using-bukit/SKILL.md) 或 [`src/skills/bukit-cli-reference/SKILL.md`](src/skills/bukit-cli-reference/SKILL.md) 开始
- 技能目录：[`src/skills/README.zh-CN.md`](src/skills/README.zh-CN.md)

## 部署

GitHub Actions 工作流模板位于 [`.github/workflows/release.yml`](.github/workflows/release.yml)。

1. 在 GitHub **Settings → Pages** 中选择 "GitHub Actions"
2. 如使用 Notion，在仓库 Secrets 中添加 `NOTION_TOKEN`
3. 推送至 `main` 分支 — 工作流将自动构建并部署站点

详细指引见 [`guide/user/13-deploy-github-pages.zh-CN.md`](guide/user/13-deploy-github-pages.zh-CN.md)。

## 项目状态

**Bukit Core 1.0 Stable**

稳定承诺：

- CLI: build / clean / config / doctor / preview / dev / deploy / seo / geo / publish
- `content.sources[]` 配置契约
- Markdown 内容源
- Notion 内容源
- `content.media`
- 基于集合的路由
- Scriban 渲染
- 安全输出文件系统
- SEO / RSS / sitemap / JSON Feed
- GEO / `llms.txt` / publish audit
- 构建报告
- 增量构建
- Native AOT CLI
- GitHub Pages 部署

**Next Stage / Preview**

不纳入 1.0 稳定承诺：

- 主题注册表
- clone-to-theme
- import html-demo 工作流
- import seed
- notion push / Notion 迁移
- 外部插件生态
- 插件市场
- BukitJalil
- 高级 AI 自动化

## 路线图

| 领域 | 状态 |
|---|---|
| 构建、预览、路由、模板 | 已稳定 |
| Markdown、Notion、SEO/GEO | 已稳定 |
| 主题生态、模板工具 | 改进中 |
| AI 意图工作流 | 改进中 |
| BukitJalil 控制面板 | 未来 |
| 插件市场 / 注册表 | 未来 |
| 更广泛的知识源集成 | 未来 |

## 参与贡献

欢迎贡献。开发者指南包含架构文档、测试流程和贡献流程：

- [`guide/dev/README.zh-CN.md`](guide/dev/README.zh-CN.md)
- [`guide/dev/testing-smoke.zh-CN.md`](guide/dev/testing-smoke.zh-CN.md)

## 许可证

本项目基于 [LICENSE](./LICENSE) 中的条款授权。
