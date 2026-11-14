# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。proposal.md 中的决策 A1–A5 已确认（全部采用推荐项），实施须在用户明确开始后作为单独的 Core 任务开展。实施顺序、提交边界和专项测试以 `docs/plans/2026-11-14-add-i18n-shared-assets-implementation-plan.zh-CN.md` 为准。

## 0. 前置与核查

- [x] 前置变更已实施并归档：`2026-11-12-add-i18n-default-language-root`、`2026-11-13-add-i18n-default-language-legacy-redirects`、`2026-11-13-add-i18n-root-layout-merged-search`、`2026-11-14-unify-site-languages`。
- [x] 按 `main`（`e7651fb1`）核实现状，结论记入 design.md "Context"：webp 改写幂等（`ImageProcessingPluginTests.cs:567`）；`__image_srcsets` 没有消费方；`ImageSourceFingerprint` 各语言相同、不需要修改；构建前写入处于构建事务内；`build.clean` 默认 `true`，staging 从空目录开始；不支持按语言覆盖主题或 static；受保护清理的触发条件中没有站点哈希；根 manifest 与语言 manifest 共用 `BuildManifest`，版本写死为 3。
- [x] 用户确认决策 A1–A5（2026-11-14，全部采用推荐项）。
- [x] 列出 Core 中所有把站内相对地址拼上语言 baseUrl 的位置（`BuildMaybeAbsoluteUrl` 全部调用点、`ContentMediaOutput` 占位图、其他 `_baseUrl` 拼接），确认 design.md D7 的覆盖范围。

## 1. 尺寸规划纯函数（可作为独立前置重构）

- [x] 把尺寸规划和 `{base}-{w}w{ext}` 命名、反解析抽成同一个纯函数，区分"要生成的文件"和"srcset / WebP 候选"。
- [x] `ImageProcessingPlugin`、`ImageVariantGenerator`、`ImageVariantOwnership`、`ResponsiveImageHtmlTransform` 改为调用它，行为不变；补充等价测试。

## 2. 配置

- [x] `SiteConfig.SharedAssets`（`Enabled`、`Paths`）；YAML 读取（`SiteDefaultsApplier` 与 `ConfigLoader` 赋值）、严格字段白名单（`SiteKeys`、`RequireOnly`）、JSON Schema。
- [x] 默认值 `Enabled = false`；`Paths` 默认为 `assets/`，`theme.scss.enabled: true` 时再加 SCSS 输出目录（去重；SCSS 输出为根目录时不加入并警告）。
- [x] 条目规范化与校验：`\`→`/`、去开头 `/`、补结尾 `/`；空列表、绝对路径、`..`、规范化后为根目录的条目报错；大小写规则同 `OutputDestinationIdentityComparer`。
- [x] 媒体前缀：始终从有效共用集合中扣除；显式条目等于或位于媒体前缀之内时报错。
- [x] `enabled: true` 且 `defaultLanguageInSubdir: true` 时 `ConfigValidator.Validate` 失败；少于 2 种语言且显式开启时记录 `i18n.config.shared_assets_ignored`。
- [x] 提供"有效共用集合"的单一实现 `SharedAssetScope`：`Contains` = 命中任一配置前缀且不命中媒体前缀；在站点层创建一次（Worker 不传 logger），供计划过滤、地址规则、图片插件、渲染哈希和 Worktree 共用。

## 3. 资源计划与构建顺序

- [x] 非默认语言的资源计划去掉 `Category ∈ {Assets, Tokens, Static}`、`Operation ∈ {Copy, Generate}` 且目标落在有效共用集合内的条目；默认语言不变。
- [x] `BuildVariantContext` 新增共用资源根路径（关闭时为空）。
- [x] `SiteEngine`：开启共用时先单独构建默认语言，成功后其余语言按 `languageJobs` 并发；默认语言失败时不启动其他语言。关闭时调度不变。

## 4. 模板变量与 Core 拼出的地址

- [x] `SiteModel` 新增可空、非 required 的 `AssetBaseUrl`；`VariantDataSitePlanner.BuildSiteModel` 显式赋值，`ScribanSiteModelMapper` 为空时回退到 `BaseUrl`，`ScribanModelKnownFields` 接入 `site.asset_base_url`。
- [x] Starter 主题 `BaseLayout.html` 的样式表改用 `site.asset_base_url`，并为它单独做 `/` → 空串映射（否则根 baseUrl 下输出 `//assets/...`）。
- [x] `BuildMaybeAbsoluteUrl`：站内相对地址落在有效共用集合内时使用根 baseUrl；覆盖第 0 节列出的全部调用点。scope 沿 `VariantSeoStage` → `SeoPipeline` → `SeoIndexBuilder` → `SeoModelBuilder` → `SeoImageResolver` / `SeoJsonLdBuilder` 传入。
- [x] `ContentMediaOutput`：非默认语言中占位图落在有效共用集合内时，`RewriteUrl` 使用根 baseUrl，`ValidateOutputs` 改查共用资源根中的文件（路径仍经 `FileWriter.GetSafeFullPath` 解析）。

## 5. 图片处理

- [x] 非默认语言 AfterBuild：共用原图清单取自本语言资源计划中被过滤的图片条目（不扫描磁盘），在 `AssetPipeline.PrepareAsync` 之后、AfterBuild 之前写入插件上下文；映射 key 和候选 URL 共用去掉结尾 `/` 的根 baseUrl 前缀，原图宽度用会捕获异常的 `TryGetImageWidth` 读取；用尺寸规划函数只读构建共用 WebP 映射（文件存在，`ImageVariantFreshness.TryReadFreshness` 有效且记录对当前源文件 `Matches` 才收录，URL 用根 baseUrl；源不可读时记录 `event=image_processing.skip`）；映射在"没有自有原图就提前返回"之前构建；webp 改写使用"自身映射 ∪ 共用映射"。
- [x] `TryNormalizeLocalImageUrl` 接受 `asset_base_url + "/"` 前缀且剩余路径落在有效共用集合内的地址，并解析到共用资源根。
- [x] 非默认语言不在共用资源根的有效共用集合内生成、改写或删除文件，不执行孤儿和 freshness 清理。
- [x] 默认语言的变体生成和失败策略不变。

## 6. 增量

- [x] 有效共用集合经 `RenderDependencyContext` 写入 `SiteIdentityAndModesContributor`，`asset_base_url` 不单独入哈希（由已入哈希的 `site.baseUrl` 与共用集合推出），用例断言根 baseUrl 变化时哈希变化；未开启时哈希输入不变。

## 7. Worktree

- [x] `WorktreeBuildRun`：开启共用时先运行默认语言 Worker，成功后再并发运行其余 Worker（不超过 `languageJobs`）。
- [x] `I18nWorkerRequest` 新增共用资源根路径（staging 根目录），Worker 写入 `BuildVariantContext`。

## 8. 测试

- [x] Config：默认关闭、开启、自定义 `paths`、非法值、根前缀报错、规范化、大小写去重、SCSS 未启用时不纳入、SCSS 输出在根目录时警告且不纳入、`assets/uploads/` 下的路径不属于共用集合、显式媒体前缀报错、带前缀布局报错、单语言忽略并警告、Schema。
- [x] Engine（根目录布局，`languages: [en, zh-CN]`）：
  - 计划过滤：`dist/assets/` 只有一份，`dist/zh-CN/assets/` 下没有主题资源；tokens 共用；`static/assets/` 共用，`static/fonts/` 不共用，加入 `paths` 后共用；插件在 `assets/` 下的输出照常按语言写出；媒体仍按语言输出；关闭时与现状一致。
  - 顺序：`languages: [zh-CN, en]`、`languageJobs: 2` 时默认语言先完成；默认语言失败时其他语言不启动且正式输出不变。
  - `asset_base_url`：开启 / 关闭两种取值、`/repo`；Starter 主题在根 baseUrl `/` 下（开启与关闭）输出 `/assets/style.css`。
  - Core 地址：`seo.defaultImage`、`og_image`（含带 query 的 `/assets/og.png?v=2`）、作者图片落在共用集合内时使用根 baseUrl；占位图地址和 `media-validate` 在非默认语言中通过。
  - 图片：共用主题图片变体只在 `dist/assets/` 下生成一次，两种语言页面的 WebP `<picture>` 指向根；非默认语言不在共用集合内写入或删除文件；`warn` 模式下默认语言变体失败时，非默认语言页面保留原 `<img>`；语言目录内图片的变体行为不变；根 baseUrl 下 `/images/a.jpg` 不被当作共用资源；本语言没有自有图片时仍改写共用图片。
  - 覆盖与代理说明：插件输出保留由 `PrepareAsync_PluginOutputInsideSharedPrefix_IsKeptPerLanguage` 和 `RunAfterBuild_PluginOutputInsideAssets_WritesToLanguageOutput` 两个测试组合覆盖；媒体按语言输出由 `AssetPipelineTests` 计划保留用例和媒体变体端到端用例覆盖；`warn` 模式默认语言变体失败的完整构建路径仍为代理覆盖，由共用变体缺失负向测试和插件级 `warn` 测试代表。
  - 增量：`build.clean: false` 下开启和关闭各一次，非默认语言副本被删除 / 恢复，页面地址随之变化；只改 `paths` 时全部页面重新渲染；关闭时渲染哈希与现状一致；只改一种语言时共用资源不重写。
- [x] CLI（Worktree）：默认语言 Worker 先完成；Worker 收到共用资源根；与进程内构建的相对文件路径和内容一致。

## 9. 文档与契约

- [x] i18n skill、用户指南 `11-i18n-seo.md`、`04-site-yaml-config.md`、开发文档 `config-site-yaml.md`、参数速查。
- [x] 主题作者说明：共用资源用 `asset_base_url`，按语言变化的输出用 `base_url`；JS 拼地址和 CSS 根绝对路径的注意事项；只支持根目录布局；`languageJobs > 1` 时默认语言先构建。
- [x] 中英马 CHANGELOG、公共 API 基线（`SiteConfig`、`SiteModel`）、verification closure 策略。

## 10. 验证

- [x] Config、Engine、CLI 专项测试；API 漂移、格式检查；`openspec validate add-i18n-shared-assets --strict --no-interactive`；当前平台 AOT 二进制编译。

## 后续（不在本变更内）

不设勾选项，只作记录：`SeoImageResolver` 对 `record.Media` 地址是否重复拼接 baseUrl；百分号编码路径是否参与共用集合判断；`BuildResult` 计数；CDN 域名；媒体共用（A5）。
