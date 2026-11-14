# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。经用户确认 proposal.md 中的决策 A1–A5 后，作为单独的 Core 任务开展。

## 0. 前置与核查

- [x] 前置变更已实施并归档：`2026-11-12-add-i18n-default-language-root`、`2026-11-13-add-i18n-default-language-legacy-redirects`、`2026-11-13-add-i18n-root-layout-merged-search`、`2026-11-14-unify-site-languages`。
- [x] 按 `main`（`e7651fb1`）核实现状，结论记入 design.md "Context"：webp 改写幂等（`ImageProcessingPluginTests.cs:567`）；`__image_srcsets` 没有消费方；`ImageSourceFingerprint` 各语言相同、不需要修改；构建前写入处于构建事务内；`build.clean` 默认 `true`，staging 从空目录开始；不支持按语言覆盖主题或 static；受保护清理的触发条件中没有站点哈希；根 manifest 与语言 manifest 共用 `BuildManifest`，版本写死为 3。
- [ ] 用户确认决策 A1–A5；选非推荐项时同步修改 proposal、design、规范和本清单。
- [ ] 列出 Core 中所有把站内相对地址拼上语言 baseUrl 的位置（`BuildMaybeAbsoluteUrl` 全部调用点、`ContentMediaOutput` 占位图、其他 `_baseUrl` 拼接），确认 design.md D7 的覆盖范围。
- [ ] 确认 `SeoImageResolver` 对 `record.Media` 地址是否重复拼接 baseUrl（与本提案无关，有问题另行记录）。

## 1. 尺寸规划纯函数（可作为独立前置重构）

- [ ] 把尺寸规划和 `{base}-{w}w{ext}` 命名、反解析抽成同一个纯函数，区分"要生成的文件"和"srcset / WebP 候选"。
- [ ] `ImageProcessingPlugin`、`ImageVariantGenerator`、`ImageVariantOwnership`、`ResponsiveImageHtmlTransform` 改为调用它，行为不变；补充等价测试。

## 2. 配置

- [ ] `SiteConfig.SharedAssets`（`Enabled`、`Paths`）；YAML 读取、严格字段白名单（`SiteKeys`、`RequireOnly`）、JSON Schema。
- [ ] 默认值 `Enabled = false`；`Paths` 默认为 `assets/` 加 SCSS 输出目录（去重；SCSS 输出为根目录时不加入并警告）。
- [ ] 条目规范化与校验：`\`→`/`、去开头 `/`、补结尾 `/`；空列表、绝对路径、`..`、规范化后为根目录的条目报错；大小写规则同 `OutputDestinationIdentityComparer`。
- [ ] 媒体前缀：始终从有效共用集合中扣除；显式条目等于或位于媒体前缀之内时报错。
- [ ] `enabled: true` 且 `defaultLanguageInSubdir: true` 时配置校验失败；少于 2 种语言且显式开启时记录 `i18n.config.shared_assets_ignored`。
- [ ] 提供"有效共用集合"的单一实现（含匹配函数），供计划过滤、地址规则和图片插件共用。

## 3. 资源计划与构建顺序

- [ ] 非默认语言的资源计划去掉 `Category ∈ {Assets, Tokens, Static}`、`Operation ∈ {Copy, Generate}` 且目标落在有效共用集合内的条目；默认语言不变。
- [ ] `BuildVariantContext` 新增共用资源根路径（关闭时为空）。
- [ ] `SiteEngine`：开启共用时先单独构建默认语言，成功后其余语言按 `languageJobs` 并发；默认语言失败时不启动其他语言。关闭时调度不变。

## 4. 模板变量与 Core 拼出的地址

- [ ] `SiteModel` 新增 `AssetBaseUrl`；`VariantDataSitePlanner.BuildSiteModel`、`ScribanSiteModelMapper`、`ScribanModelKnownFields` 接入 `site.asset_base_url`。
- [ ] Starter 主题 `BaseLayout.html` 改用 `site.asset_base_url`。
- [ ] `BuildMaybeAbsoluteUrl`：站内相对地址落在有效共用集合内时使用根 baseUrl；覆盖第 0 节列出的全部调用点。
- [ ] `ContentMediaOutput`：非默认语言中占位图落在有效共用集合内时，`RewriteUrl` 使用根 baseUrl，`ValidateOutputs` 改查共用资源根中的文件。

## 5. 图片处理

- [ ] 非默认语言 AfterBuild：用尺寸规划函数对共用原图只读构建共用 WebP 映射（文件存在且 freshness 有效才收录）；webp 改写使用"自身映射 ∪ 共用映射"。
- [ ] `TryNormalizeLocalImageUrl` 接受 `asset_base_url + "/"` 前缀，并解析到共用资源根。
- [ ] 非默认语言不在共用资源根的有效共用集合内生成、改写或删除文件，不执行孤儿和 freshness 清理。
- [ ] 默认语言的变体生成和失败策略不变。

## 6. 增量

- [ ] `sharedAssets` 有效值和 `asset_base_url` 写入 `SiteIdentityAndModesContributor`。

## 7. Worktree

- [ ] `WorktreeBuildRun`：开启共用时先运行默认语言 Worker，成功后再并发运行其余 Worker（不超过 `languageJobs`）。
- [ ] `I18nWorkerRequest` 新增共用资源根路径（staging 根目录），Worker 写入 `BuildVariantContext`。

## 8. 测试

- [ ] Config：默认关闭、开启、自定义 `paths`、非法值、根前缀报错、规范化、大小写去重、SCSS 输出在根目录时警告且不纳入、显式媒体前缀报错、带前缀布局报错、单语言忽略并警告、Schema。
- [ ] Engine（根目录布局，`languages: [en, zh-CN]`）：
  - 计划过滤：`dist/assets/` 只有一份，`dist/zh-CN/assets/` 下没有主题资源；tokens 共用；`static/assets/` 共用，`static/fonts/` 不共用，加入 `paths` 后共用；插件在 `assets/` 下的输出照常按语言写出；媒体仍按语言输出；关闭时与现状一致。
  - 顺序：`languages: [zh-CN, en]`、`languageJobs: 2` 时默认语言先完成；默认语言失败时其他语言不启动且正式输出不变。
  - `asset_base_url`：开启 / 关闭两种取值、`/repo`。
  - Core 地址：`seo.defaultImage`、`og_image`、作者图片落在共用集合内时使用根 baseUrl；占位图地址和 `media-validate` 在非默认语言中通过。
  - 图片：共用主题图片变体只在 `dist/assets/` 下生成一次，两种语言页面的 WebP `<picture>` 指向根；非默认语言不在共用集合内写入或删除文件；`warn` 模式下默认语言变体失败时，非默认语言页面保留原 `<img>`；语言目录内图片的变体行为不变。
  - 增量：`build.clean: false` 下开启和关闭各一次，非默认语言副本被删除 / 恢复，页面地址随之变化；只改一种语言时共用资源不重写。
- [ ] CLI（Worktree）：默认语言 Worker 先完成；Worker 收到共用资源根；与进程内构建的相对文件路径和内容一致。

## 9. 文档与契约

- [ ] i18n skill、用户指南 `11-i18n-seo.md`、`04-site-yaml-config.md`、开发文档 `config-site-yaml.md`、参数速查。
- [ ] 主题作者说明：共用资源用 `asset_base_url`，按语言变化的输出用 `base_url`；JS 拼地址和 CSS 根绝对路径的注意事项；只支持根目录布局；`languageJobs > 1` 时默认语言先构建。
- [ ] 中英马 CHANGELOG、公共 API 基线（`SiteConfig`、`SiteModel`）、verification closure 策略。

## 10. 验证

- [ ] Config、Engine、CLI 专项测试；API 漂移、格式检查；`openspec validate add-i18n-shared-assets --strict`；当前平台 AOT 二进制编译。
