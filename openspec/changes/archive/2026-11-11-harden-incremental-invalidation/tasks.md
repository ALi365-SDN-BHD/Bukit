# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。

## 1. 引擎身份

- [x] 读取 `Bukit.Engine` 程序集 InformationalVersion 的 AOT 安全辅助方法（参照 `ImageOptimizer`）。
- [x] `VariantManifestPlanner.ComputeCompositeTemplateHash` 加入引擎身份，与 `scriban-renderer-v1` 并列。

## 2. 插件选项

- [x] `NonAnalyticsPluginEnablementContributor` 用 `AppendLabeledCanonicalValue` 写入 `Options`。

## 2b. 元数据变化原因（问题 6）

- [x] `PageRenderDispatcher`：元数据哈希变化的原因改为 `metadata_changed`，判定顺序不变。
- [x] `IncrementalBuildEngine`：元数据哈希排除 `bodyFingerprint`；不含正文的列表哈希仍纳入它。

## 3. 测试

- [x] 模板哈希：不同引擎身份 → 不同哈希；相同 → 相同。
- [x] 端到端（生产路径 `RenderPipeline` → `DispatchAsync`）：引擎身份变化后页面、列表、特殊列表全部重渲染；相同引擎、输入不变 → 全部 `unchanged`；i18n worktree 构建同样成立。
- [x] 插件选项变化 → `render_dependency_changed`；键顺序不同 → 哈希不变；嵌套列表与字典选项可哈希。
- [x] 原因：只改自定义字段 → `metadata_changed`；只改正文 → `content_changed`。
- [x] `bodyFingerprint` 变化：元数据哈希不变、内容哈希变化、列表哈希（含/不含正文）均变化。

## 4. 文档

- [x] 用户指南增量构建章节：失效条件；升级 Bukit 后首次全量渲染且显示 `template_changed`；无 git 信息或未提交修改时需要 `--clean`；改插件选项会全站重渲染；引用 `pages` 的主题与开启 `theme.images` 时的已知局限。
- [x] 开发文档：渲染依赖贡献者清单与模板哈希组成。
- [x] 中英马 CHANGELOG。

## 5. 验证

- [x] `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包并解决未映射文件。
- [x] Engine 专项测试，格式检查，`openspec validate harden-incremental-invalidation --strict`，AOT 二进制编译。

证据（2026-11-10）：Engine 3280/3280（后续阶段 6 后 3298/3298）、闭包 `/tmp/claude/codex-reports/stage8/closure.json`、格式检查与 `openspec validate --strict` 通过；AOT 引擎身份 `2.0.11+0cd4717c…` 含提交。用户指南中「引用 `pages` 的主题」已知局限已由 `add-per-page-render-dependencies` 取代。
