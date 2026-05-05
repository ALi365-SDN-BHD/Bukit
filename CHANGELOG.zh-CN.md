# 更新日志

Bukit 所有重要变更都将记录在此文件中。

## [1.0.0] - 2026-05-05

### 新增
- Bukit 首发版本，基于 .NET 10 Native AOT 的静态站点生成器
- Markdown 内容源，支持 Front Matter
- Notion 内容源，支持数据库映射与字段归一化
- 多源内容聚合（`markdown` + `notion`）
- Scriban 模板引擎，支持 AOT 兼容的 vendored 构建
- Collections 驱动的路由系统，带 permalink 兼容层
- 内置插件：sitemap、RSS、search JSON、taxonomy、pagination、archive、pages-index
- 多语言站点支持，含 split/merged/index 模式
- 增量构建，基于 manifest 的变更检测
- 主题系统，含 `theme list` / `theme use` 命令
- Modules 数据源（`mode=data`），用于结构化内容
- 外部插件协议 v1/v2（process 和 WASM 运行时）
- 插件源码生成器，实现零反射注册
- `doctor` 诊断命令，用于环境与配置检查
- `webhook` 命令，用于 Notion 到 GitHub Actions 的 repository dispatch
- Intent 系统，用于 AI 辅助站点配置
- AOT 发布，单文件输出
- 性能指标输出（`--metrics`）
- 并行渲染，可配置并发度（`--jobs`）
- CI 模式（`--ci`），支持 JSON 结构化日志
- 用户文档（16 章，3 语言）
- 开发者文档（35+ 文件，覆盖架构、CLI、插件等）
- ChatGPT prompt 包，用于对话式建站
