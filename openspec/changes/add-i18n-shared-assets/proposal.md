# Share assets across languages

## Why

多语言站点（`site.languages` 有 2 种及以上语言）里，每种语言都把主题 `assets/`、SCSS 编译结果、theme tokens 和 `static/assets/` 复制一份到自己的输出目录，并各自为主题图片生成响应式变体。N 种语言就有 N 份相同的文件：输出体积随语言数线性增长，AfterBuild 重复生成同样的图片变体，用户在语言之间切换时浏览器也无法复用缓存。

默认布局已经是根目录布局（`defaultLanguageInSubdir` 默认 `false`，见归档变更 `2026-11-14-unify-site-languages`），默认语言的资源本来就在 `dist/assets/`。本变更让其他语言直接引用这一份，不再各自复制，并为主题提供 `site.asset_base_url` 变量。

这只是优化，不修复正确性问题：现状每种语言自洽；SCSS 编译、图片优化和媒体下载缓存已经是每次构建一份，重复的只是输出副本和图片变体。目前没有需要兼容的已上线站点，不提供迁移。

## Sequencing

- 前置已完成：`add-i18n-default-language-root`（2026-11-12 归档）、`add-i18n-default-language-legacy-redirects`、`add-i18n-root-layout-merged-search`、`unify-site-languages`（2026-11-14 归档）。
- 本提案 v2（2026-11-14）按当前 `main` 重写，取代 2026-11-13 的 v1。v1 的根目录归属层方案及其被放弃的原因见 design.md "Alternatives"。

## What Changes

- 新增 `site.sharedAssets`：
  - `enabled`（布尔，默认 `false`，待决策 A1）。
  - `paths`（输出路径前缀列表，可选）。不写时为 `assets/` 加 SCSS 输出目录。
- 只支持根目录布局：`enabled: true` 与 `defaultLanguageInSubdir: true` 同时出现时配置校验失败。少于 2 种语言时忽略该设置，显式开启时给出警告。
- **默认语言是共用资源的唯一归属方**：默认语言照常写出并在自己的 manifest 中归属 `dist/assets/` 等共用路径；其他语言的资源计划去掉落在共用集合内的复制型和生成型条目（主题 / 父主题 assets、SCSS 结果、tokens、复制型 static）。页面、渲染型 static、插件输出、投影和媒体不受影响。
- 开启共用时，默认语言先构建完成，其他语言再按 `build.languageJobs` 并发构建；进程内与 Worktree 构建相同。
- 新增模板变量 `site.asset_base_url`：开启时等于站点根 baseUrl（保留 `/repo` 前缀，不含语言段），关闭时等于当前语言的 `base_url`。
- Core 自己拼出的站内地址跟随共用规则：SEO 图片（`og_image`、`cover`、`seo.defaultImage`、作者图片等）和媒体占位图 `content.media.defaultImageUrl` 落在共用集合内时使用根 baseUrl；占位图的存在性校验改查共用位置。
- 图片处理插件：默认语言照常生成共用原图的变体；其他语言以只读方式读取共用位置中已生成且 freshness 有效的 WebP 变体，合并进自己的 `<picture>` 改写，不在共用位置生成或清理任何文件。尺寸规划与变体命名收敛为同一个纯函数。
- 媒体本版不共用：媒体前缀（`content.media.urlBase`）始终从共用集合中扣除，显式写进 `paths` 时报错。
- `sharedAssets` 的有效值进入渲染依赖哈希；开关切换由现有过期清理处理，不需要新增受保护清理条件。
- Worktree：父进程先运行默认语言 Worker，再并发运行其他 Worker；Worker 请求新增共用资源根路径。
- Starter 主题改用 `site.asset_base_url`；配置读取、严格字段白名单、校验、JSON Schema、公共 API 基线、i18n skill、用户指南、开发文档、主题作者说明、CHANGELOG 同步更新。

## Impact

- 受影响代码：
  - `Bukit.Config`：`SiteConfig`、`ConfigLoader` / `SiteDefaultsApplier`、`ConfigStrictFieldValidator`、`SiteConfigValidator`、`ConfigJsonSchemaGenerator`。
  - `Bukit.Engine`：`AssetOutputPlan`（或其调用方）、`SiteEngine`（语言顺序）、`BuildVariantContext`、`ContentMediaOutput`（占位图）、`SeoModelBuilder` / `SeoImageResolver` / `SeoJsonLdBuilder`、`VariantDataSitePlanner`、`I18nLanguagePolicy`、`Incremental/SiteIdentityAndModesContributor`、`I18nWorktreeBuild`、`I18nWorktreeProtocolModels`。
  - `Plugins/BuiltIn`：`ImageProcessingPlugin`，`ImageProcessing/` 下的 `ImageVariantGenerator`、`ImageVariantFreshness`、`ResponsiveImageHtmlTransform`。
  - `Bukit.Rendering`：`SiteModel`、`ScribanSiteModelMapper`、`ScribanModelKnownFields`。
  - `Bukit.Cli`：`WorktreeBuildRun`（Worker 顺序与请求）、Starter 主题 `BaseLayout.html`。
- 规范：新增能力 `i18n-shared-assets`。Worktree 相关行为写在该能力中，不修改 `i18n-worktree-build` 现有要求（`languageJobs` 的上限语义不变）。
- 公共契约：`SiteConfig` 和 `SiteModel` 新增属性，`site.yaml` 新增可选字段，模板新增一个变量，新增配置校验错误（`ConfigException`，不新增 `DiagnosticCode`）和警告事件 `i18n.config.shared_assets_ignored`。v1 的 `i18n-shared-asset-conflict` 不再需要。Worker 请求新增字段（父进程与 Worker 为同一二进制，不升 schema）。根 manifest 和语言 manifest 结构不变。
- 兼容性：默认关闭，现有站点输出不变。开启后，仍用 `{{ base_url }}/assets/...` 的主题在非默认语言页面会找不到资源，主题作者说明须写明。
- 跨仓影响：网站仓库无需改动。

## 待用户确认的决策

| # | 决策 | 选项 | 推荐 |
|---|---|---|---|
| A1 | 默认值 | a) 多语言根目录布局站点默认开启；b) 默认关闭，显式开启 | b。默认开启会让所有用 `{{ base_url }}/assets/...` 的主题在非默认语言页面找不到资源；稳定后再评估 |
| A2 | 归属模型（design.md D3） | a) 默认语言归属（v2）；b) 根目录归属层（v1） | a。不需要根插件输出记录、manifest 升版本、构建前阶段和冲突检测，也没有根目录布局下自删共用文件的问题 |
| A3 | 布局范围 | a) 只支持根目录布局；b) 两种布局都支持 | a。根目录布局是默认值；带前缀布局需要 v1 那套根目录归属层 |
| A4 | 非资源输出落在共用前缀下（design.md D4） | a) 不处理，照常按语言输出；b) 报错 | a。这些输出本来就按 `base_url` 引用，没有物理冲突；v1 的报错规则只防"拼错前缀" |
| A5 | 媒体共用 | a) 本提案不做，另立提案；b) 作为本提案第二步 | a。媒体会出现"只被非默认语言引用"的情况，需要跨语言引用并集和根目录归属，复杂度与本提案不在一个量级 |

规范按推荐选项编写；任何一项选非推荐项时，须同步修改 design.md 对应决策、规范和 tasks。

## Approval Scope

- 本提案只是方案文档。实施属于 Core 任务，须经用户确认上表决策后再单独开展。
- commit、push、PR、发布和 full/release gate 均需另行授权。

## Non-Goals

- 不共用内容媒体及媒体图片变体（A5）。
- 不支持带前缀布局（A3）。
- 不对 HTML 中用 `base_url` 拼出的资源地址做自动改写；不改写 JS 运行时拼出的地址，也不改写 CSS 中用根绝对路径写的 `url()`。
- `static/` 中 `assets/` 以外的文件默认不共用；不按语言自动识别哪些 static 文件可以共用。
- 不支持把共用资源放到独立 CDN 域名（可作为后续 `asset_base_url` 的扩展）。
- 不修复 `BuildResult` 资源 / 媒体计数的现存问题（只统计根目录 `assets`，`MediaCount` 不跟随 `urlBase`），另行记录。
- 单语言站点行为不变。
