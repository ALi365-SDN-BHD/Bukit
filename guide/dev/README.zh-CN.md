# Bukit 开发者文档（维护与扩展）

语言版本：简体中文（当前）| [English](./README.md) | [Bahasa Melayu](./README.ms.md)

本目录面向维护者与贡献者，聚焦 Bukit 的稳定契约（配置、参数、数据模型）和实现细节（构建流水线、增量构建、插件加载），帮助你在修改代码时更快找到边界、入口和验证路径。

## 最快入门路径

1. 先跑通示例站点链路：查看 [CLI](./cli.zh-CN.md)
2. 理解 `site.yaml` 字段与校验规则：查看 [配置](./config-site-yaml.zh-CN.md)
3. 建立端到端主线认知：配置 → 内容 → 路由 → 渲染 → 插件 → 输出，查看 [架构总览](./architecture.zh-CN.md)

## 按维护任务导航

| 任务 | 入口文档 |
|---|---|
| 修改 CLI 行为 | [CLI 参数参考](./cli.zh-CN.md) |
| 修改 site.yaml schema | [site.yaml 字段参考](./config-site-yaml.zh-CN.md) |
| 修改内容模型 | [内容系统](./content.zh-CN.md) |
| 修改路由 | [路由系统](./routing.zh-CN.md) |
| 修改渲染/Scriban | [渲染与模板](./rendering-scriban.zh-CN.md) |
| 修改主题行为 | [主题开发](./theme.zh-CN.md)、[Git 主题源](./theme-source.zh-CN.md) |
| 修改插件 | [插件系统](./plugins.zh-CN.md)、[内置插件](./built-in-plugins.zh-CN.md) |
| 修改 Notion 源 | [内容系统](./content.zh-CN.md)（Notion 章节） |
| 修改 AOT/性能 | [AOT](./aot.zh-CN.md)、[性能/AOT 治理](./perf-aot-governance.zh-CN.md) |
| 修改发布/文档流程 | [文档治理](./documentation-governance.zh-CN.md)、[发布检查清单](./release-checklist.zh-CN.md) |

## 如果你通过 AI / Agents 维护 Bukit

如果你是在 Trae、Claude Code、Copilot CLI、Codex CLI、Gemini CLI 这类支持 skill 的环境里维护 Bukit，建议把 `src/skills/` 当作 Agent 侧导航层，而把本目录当作维护者侧的契约与实现参考。

- Agent skills 总览：[`src/skills`](../../src/skills/README.zh-CN.md)
- 统一入口：[`using-bukit`](../../src/skills/using-bukit/SKILL.md)
- 命令执行参考：[`bukit-cli-reference`](../../src/skills/bukit-cli-reference/SKILL.md)
- 本目录继续负责：架构、配置契约、渲染细节、插件、可观测性、测试与运行治理

## 导航

- [代码 Wiki（仓库总览）](./code-wiki.zh-CN.md)
- [模块调用图](./code-wiki-call-graph.zh-CN.md)
- [新开发者 30 分钟上手路线](./new-developer-30min.zh-CN.md)
- [按改动类型查入口](./maintainer-entrypoints.zh-CN.md)
- [架构评审草案](./architecture-review.zh-CN.md)
- [架构与模块边界](./architecture.zh-CN.md)
- [维护治理清单](./governance-checklist.zh-CN.md)
- [CLI 参数参考](./cli.zh-CN.md)
- [`site.yaml` 字段参考](./config-site-yaml.zh-CN.md)
- [Init/Create 脚手架](./init-create.zh-CN.md)
- [内容系统（Markdown / Notion / sources）](./content.zh-CN.md)
- [路由系统](./routing.zh-CN.md)
- [渲染与模板（Scriban）](./rendering-scriban.zh-CN.md)
- [主题开发](./theme.zh-CN.md)
- [Git 主题源](./theme-source.zh-CN.md)
- [Modules 数据源（`mode=data`）](./modules-data.zh-CN.md)
- [引擎固定产物](./engine-outputs.zh-CN.md)
- [插件系统](./plugins.zh-CN.md)
- [内置插件输出与边界](./built-in-plugins.zh-CN.md)
- [Intent CLI 集成](./intent-cli.zh-CN.md)
- [AOT 与非 AOT 构建模式](./aot.zh-CN.md)
- [性能 / AOT / 治理说明](./perf-aot-governance.zh-CN.md)
- [发布与部署](./publish-deploy.zh-CN.md)
- [增量构建](./incremental-build.zh-CN.md)
- [缓存与清理](./cache-clean.zh-CN.md)
- [Doctor 检查](./doctor.zh-CN.md)
- [可观测性（日志与指标）](./observability.zh-CN.md)
- [多语言与 SEO](./i18n-seo.zh-CN.md)
- [Webhook 触发与安全约束](./webhook.zh-CN.md)
- [测试与 smoke 验收](./testing-smoke.zh-CN.md)
- [文档治理规则](./documentation-governance.zh-CN.md)
- [发布检查清单](./release-checklist.zh-CN.md)
- [公开测试范围](./public-preview-scope.zh-CN.md)

## 如何使用仓库中的其他文档

`docs/` 目录更偏产品、方案、验收与治理过程文档。本目录作为开发者入口，会尽量链接这些资料而不是重复维护一份内容。

常用入口：

- AI 建站指南：[chatgpt/README.zh-CN.md](../ai/chatgpt/README.zh-CN.md)
- Intent 契约与映射：[intent-cli.zh-CN.md](./intent-cli.zh-CN.md)
- Notion schema 与内容模型：[content.zh-CN.md](./content.zh-CN.md)
- 企业官网 Modules 建模：[modules-data.zh-CN.md](./modules-data.zh-CN.md)
- 验收与 smoke 文档：[testing-smoke.zh-CN.md](./testing-smoke.zh-CN.md)

## 快速概念

- `ContentItem`：来自 Markdown 或 Notion 的统一内容模型
- Meta 与 Fields：Meta 决定引擎行为，Fields 主要供模板消费（`page.fields.*.value`）
- `mode=content` 与 `mode=data`：前者生成路由和页面，后者只把数据注入 `site.modules`
- 插件生命周期：当前核心钩子是 `derive-pages` 与 `after-build`

英文版入口：[README.md](./README.md)

