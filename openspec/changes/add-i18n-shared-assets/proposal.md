# Share assets across languages

## Why

配置了 `site.languages` 后，Bukit 让每种语言各自把主题 `assets/`、SCSS 编译结果、Notion 媒体和响应式图片变体复制或生成到 `dist/<lang>/` 下。N 种语言就有 N 份相同的文件：输出体积随语言数线性增长，构建时重复复制和生成图片变体，用户在不同语言之间切换时浏览器也无法复用缓存。Jekyll Polyglot 的 `exclude_from_localization` 让资源只在根目录放一份，各语言共用。本变更为 Bukit 提供同样的能力，并对多语言站点默认开启。

目前没有需要兼容的已上线站点，本变更不提供升级迁移或兼容改写。

## Sequencing

本提案在 `add-i18n-default-language-root`（提案 B）实施并验收完成后才开始实施。实施前须按 B 的最终实现复核本提案中与布局相关的描述（根目录默认语言、保留路径、`I18nOutputLayout`）。

## What Changes

- 新增 `site.sharedAssets`：
  - `enabled`（布尔，`site.languages` 非空时默认 `true`）。为 `false` 时恢复每种语言各一份资源。
  - `paths`（输出路径前缀列表，可选）。不写时由 `assets/`、SCSS 输出目录、媒体 `urlBase` 推导；写了则完全替换。
- 共用规则：复制型和生成型资源输出的路径匹配前缀即共用，与来源无关。主题和父主题 assets、SCSS 结果、theme tokens、内容媒体、共用原图的图片变体默认共用；输出到 `assets/` 下的复制型 static 文件默认共用，其他 static 文件默认不共用。渲染型输出（页面、带模板的 static）永不共用，在任何布局下落在共用前缀下都报错。
- 共用资源只写一份到站点根输出目录，由根目录这一层负责归属。**不在语言管线中引入跨语言同步点**，语言管线的阶段顺序不变：
  - 构建前：根目录这一层写出主题 assets、SCSS 结果、tokens、匹配前缀的复制型 static，并生成共用主题图片变体和共用 webp 映射，供各语言使用。
  - 语言管线中：各语言把引用的媒体写入共用位置，按目标路径去重，`media-validate` 照常执行；webp 改写合并共用映射。
  - 收尾：根目录这一层按完整媒体引用并集记录归属并清理过期文件。
  - 响应式 `srcset` transform 仍在渲染阶段执行，只改地址前缀。
- 新增模板变量 `site.asset_base_url`：共用开启时等于站点根 baseUrl，关闭时等于当前语言的 `base_url`。Core 不对 HTML 中的资源地址做隐式改写。
- 图片处理插件改造：把变体生成、归属、孤儿清理和 webp 映射计算抽成可以指定输出根的服务；共用主题图片在构建前处理，共用媒体图片由各语言去重生成；共用媒体变体通过共用结果缓存复用；语言不清理共用前缀；共用变体失败一律致命（不受 `pluginFailMode: warn` 影响）；尺寸规划与命名为同一纯函数；归属记入根 manifest 新增的插件输出记录；`srcset` 和 webp 改写接受 `asset_base_url` 前缀，webp 改写保持幂等。
- `paths` 条目会规范化；规范化后为空或根目录的前缀会被拒绝，默认推导出的根目录前缀会被跳过并给出警告。配置前缀减去内部排除前缀得到"有效共用集合"，扫描、清理和写入限制都以它为准。
- 共用变体的上次归属记录（prior outputs）和 freshness 只从根插件输出记录读取，语言 manifest 不记录共用变体。
- 插件在 AfterBuild 产生的输出不共用，落在有效共用集合内时报错；构建统计对共用资源只计一次。
- 同一共用路径在不同语言下内容不同时，构建失败并报 `i18n-shared-asset-conflict`。
- 根目录这一层写共用资源时禁止以裁剪模式同步输出根目录。
- 带前缀布局下共用前缀首段成为根目录保留路径，与提案 B 的保留前缀校验合并实现。
- Worktree 构建：父进程在分发前完成构建前共用资源和共用主题图片变体，并下发共用 webp 映射；Worker 不写共用路径，共用媒体存在性检查推迟到父进程，共用媒体变体的 webp 改写按预测文件名执行；Worker 结果新增完整媒体引用（可选字段）；父进程收尾时复制共用媒体、生成共用媒体变体并执行推迟的检查。
- 分两步交付：第一步包括主题资源共用、共用主题图片变体和 webp 映射，媒体暂不共用；第二步加入媒体共用。
- Starter 主题改用 `site.asset_base_url`；配置读取、严格字段白名单、校验、JSON Schema、公共 API 基线、i18n skill、用户指南、开发文档、主题作者说明、CHANGELOG 同步更新。

## Impact

- 受影响代码（以提案 B 落地后的代码为准复核）：`Bukit.Config`（`SiteConfig`、`ConfigLoader`、`ConfigStrictFieldValidator`、`SiteConfigValidator`、`ConfigJsonSchemaGenerator`），`Bukit.Engine`（`AssetOutputPlan`、`AssetPipeline`、`ContentMediaOutput`、`SiteEngine`、`VariantBuildPipeline`、`VariantPluginStages`、`VariantReportRunner`、`PluginPipeline`、`BuildDataKeys`、`PluginExecutionPolicy`、`BuildResult`、`Incremental/BuildManifest`、`Incremental/ImageSourceFingerprint`、根 manifest 相关类型（`PublicOutputLifecycle` 使用的 manifest 模型）、`I18nWorktreeBuild`、`I18nWorktreeProtocol`、`PublicOutputLifecycle`、模板上下文构建、`Incremental` 站点哈希），`Plugins/BuiltIn`（`ImageProcessingPlugin`，`ImageProcessing/` 下的 `ImageVariantGenerator`、`ImageVariantOwnership`、`ImageVariantFreshness`、`ResponsiveImageHtmlTransform`），`Bukit.Cli`（`WorktreeBuildRun`、`WorktreeBuildPaths`、Starter 主题 `BaseLayout.html`）。
- 规范：新增能力 `i18n-shared-assets`；实施时修改 `i18n-worktree-build` 中 Worker 结果和根级输出的要求（届时以提案 B 合入后的文本为基准编写 delta）。
- 公共契约：`SiteConfig` 新增属性，`site.yaml` 新增可选字段，模板新增一个变量，新增一个报错码；Worktree 结果新增可选字段；根 manifest 新增插件输出记录并提升版本号。
- 兼容性：多语言站点默认输出布局变化（`dist/<lang>/assets/` 改为 `dist/assets/`）。没有已上线站点，不提供迁移。仍用 `{{ base_url }}/assets/...` 的主题在非默认语言页面会找不到资源，主题作者说明须写明。
- 跨仓影响：网站仓库无需改动。

## Approval Scope

- 本提案只是方案文档。实施属于 Core 任务，须在提案 B 验收后、经用户确认再单独开展。
- commit、push、PR、发布和 full/release gate 均需另行授权。

## Non-Goals

- 不对 HTML 中用 `base_url` 拼出的资源地址做自动改写。
- 不改写 JS 运行时拼出的地址，也不改写 CSS 中用根绝对路径写的 `url()`。
- `static/` 默认不共用；不按语言自动识别哪些 static 文件可以共用。
- 不支持把共用资源放到独立 CDN 域名（可作为后续 `asset_base_url` 的扩展）。
- 单语言站点行为不变。
