# 更新日志

Bukit 所有重要变更都将记录在此文件中。

## [1.0.6] - 2026-05-21

### 新增
- **Shortcodes 系统**：`theme.shortcodes` 配置 — 定义可复用片段（youtube、callout 等），同时支持 Markdown（`{% name args %}`）和 Scriban 模板（`{{ shortcode }}`）
- **内容 Schema 校验**：collection 配置中的 `schema` — 按类型校验 Front Matter 字段（string/number/bool/date/list），支持 warn/strict 两种失败模式
- **SCSS 编译管道**：`theme.scss` — 构建时自动编译 `.scss` → `.css`，自动检测系统 `sass`/`dart-sass` CLI
- **主题继承**：`theme.extends` — 子主题级联父主题；模板查找（子优先→父回退）、静态文件/资源合并
- **组件化模板**：`theme.components` — 在 site.yaml 中声明带 props 的可复用组件，Scriban 中使用 `{{ comp.render "Name" args }}`
- **图片优化管道**：`theme.images` — 自动 WebP/AVIF 转换（自动检测系统 `cwebp`/`magick` CLI），内置 `ImageOptimizer.BuildSrcset()` 响应式图片生成
- **HMR 开发服务器**：`bukit dev` 命令 — 文件监控、增量重构建（300ms 去抖）、WebSocket 实时刷新所有浏览器
- **Layout 指令解析器共享化**：`ScribanLayoutDirectiveParser` 提取至 `Bukit.Shared`，消除渲染器与静态分析器的 DRY 违规
- **核心管道异步 I/O**：`TemplateCapabilitiesResolver` 改用 `File.ReadAllTextAsync` + `Task<T>` 缓存

### 变更
- **重构上帝类**：`SiteEngine` 从 1122 行缩减至 558 行（提取 `SeoAlternatesService`、`RobotsTxtWriter`、`StaticFileService`）；`PageRenderDispatcher` 从 581 行缩减至 491 行（提取 `SpecialListRouteBuilder`）
- **ScribanTemplateRenderer** 支持运行时注入 `shortcodes` 和 `components` 字典
- **FileTemplateLoader** 支持级联查找：主目录优先，可选回退目录（用于主题继承）
- **BuildVariantContext** 新增 `ParentLayoutsDir`、`ParentAssetsDir`、`ParentStaticDir`
- **ConfigLoader** 新增 YAML 反序列化：`ReadComponents`、`ReadImageOptimizationConfig`、`ReadScssConfig`、`ReadSchema`

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
