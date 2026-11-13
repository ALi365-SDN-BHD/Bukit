# Migration redirects for default-language legacy URLs

## Why

`add-i18n-default-language-root` 增加了默认语言根目录布局（`site.defaultLanguageInSubdir: false`）。站点从带前缀布局切过来后，只有旧首页 `/<lang>/` 有跳转页兜底，其余旧地址 `/<lang>/<path>/` 全部 404。GitHub Pages 等纯静态托管做不了 301，用户只能放弃根目录布局，或给每个页面手写 alias。该提案第 9 节把"为全部旧地址自动生成迁移跳转"列为后续事项。

分析与已确认的决策见 `docs/analysis/bukit-core-i18n-default-language-migration-redirects-proposal-2026-11-13.zh-CN.md`（D1–D9，用户 2026-11-13 确认，经两轮审计修订）。

## What Changes

- 新增 `site.defaultLanguageLegacyRedirects`（布尔，可选，默认 `false`，D1）。多语言站点且默认语言在根目录时，为默认语言每条 HTML 路由在旧前缀路径 `/<lang>/<path>` 派生一个 noindex 跳转文档，复用 `RedirectPageDocument`。
- 纳入来源：内容页、派生前的集合列表与分页、非跳转派生文档（如分类页）、static HTML；排除首页路由 `/`（D2，首页只由 `defaultLanguageRedirect` 控制）、`404.html` 和非 HTML 输出；按输出路径去重。
- alias 跳转在 custom field 中记录 `redirectTarget`；为 alias 派生 `/<lang>/<alias>/` 直接跳到页面最终地址，第一段已是默认语言代码的 alias 跳过；分类别名不迁移（D5）。
- 用户内容优先：路径已被内容页、派生文档、列表路由、static HTML 或 static 源文件占用时跳过（旧首页跳转同样补上列表路由检查）。占用检查使用一次性构建的不区分大小写集合。
- 文档 ID 与 slug 为 `i18n-legacy-<lang>-<短哈希>`，满足安全报告的单段 slug 校验。
- 汇总日志 `event=i18n.legacy_redirects.derived count= skipped= bytes=`（D8）；多语言带前缀布局下开启记 `i18n.config.legacy_redirects_ignored` 警告并忽略（D3）；单语言站点沿用 `default_language_layout_ignored`。
- 写入跳转前逐段拒绝写入路径上的符号链接（分析文档 4.8 方案 a：staging 会保留上次输出中的链接）。
- 新配置不写入增量站点哈希（D7 a，以 T7、T18 为前提）。
- 配置读取、严格字段白名单、JSON Schema、公共 API 基线、用户指南、配置文档、i18n skill、中英马 CHANGELOG 同步更新。

## Impact

- 代码：`Bukit.Config`（`SiteConfig`、`ConfigLoader`、`ConfigStrictFieldValidator`、`ConfigJsonSchemaGenerator`），`Bukit.Engine`（`I18nLegacyRedirectDeriver`、`VariantBuildPipeline`、`RedirectPageDocument`、`I18nLanguagePolicy`、`Plugins/BuiltIn/AliasPlugin`）。
- 规范：`i18n-output-layout` 新增一条要求；`i18n-worktree-build` 新增一条等价要求。
- 公共契约：`SiteConfig` 新增属性（新增性变更）；`site.yaml` 新增可选字段。
- 兼容性：默认关闭，未开启的站点输出、报告和增量站点哈希不变。alias 跳转文档多一个 custom field，不影响其 HTML。
