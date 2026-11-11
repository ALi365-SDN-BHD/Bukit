# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。

## 1. 依赖拆分

- [x] `RenderDependencyHasher`：基础哈希（除共享集合外的全部贡献者与 SEO 备用链接）与集合指纹表；集合指纹按需计算、每个变体最多一次。
- [x] `SiteModelDataContributor` 改为按集合输出；`pages` 集合指纹覆盖页面模型可见的全部 `PageInfo` 字段。
- [x] 路由依赖哈希 = H(基础哈希, 排序后的读取集合键与指纹)，再叠加路由元数据与面包屑。

## 2. 读取记录

- [x] 渲染器记录每次渲染使用的模板文件：主模板、布局链、`include`、组件、区块模板（含缓存命中）。
- [x] 模板静态分析：引用路径 → 集合键；无法确定键、整体使用、无法识别的全局名、分析失败 → 扩大到对应范围或全部；动态模板名按渲染时实际加载的文件记录。
- [x] 带 `source` 的区块 → `pages`；区块插件 → 全部集合。
- [x] 非 Scriban 渲染器或记录不可用 → 全部集合。

## 3. 清单与判定

- [x] `BuildManifestEntry` internal `RenderReads`、`RenderBaseHash`，读写可选字段；旧清单缺字段 → 全部集合。
- [x] 页面、列表、特殊列表三条路径：用上次读取集合计算比较哈希；渲染后用本次读取集合写入。
- [x] 渲染原因：基础哈希变化 → `render_dependency_changed`；读取集合变化 → `data_dependency_changed`。

## 4. 测试

- [x] 静态分析：各种引用形态到集合键的映射与保守扩大。
- [x] 端到端：改一篇文章只重渲染该文章与读取相关集合的页面；模板遍历 `pages` 时随其他页面变化重渲染；条件分支切换后读取记录更新；旧清单回退全部；基础输入变化全站重渲染；原因 `data_dependency_changed`；多语言构建成立。
- [x] 现有增量、路由元数据、面包屑相关测试通过。

## 5. 文档

- [x] 用户指南与技能文档：新的失效规则、已知局限更新、`data_dependency_changed`。
- [x] 开发文档：基础部分与集合、读取记录、回退规则。
- [x] 中英马 CHANGELOG。

## 6. 验证

- [x] `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包并解决未映射文件。
- [x] 闭包专项测试、格式检查、`openspec validate add-per-page-render-dependencies --strict`、AOT 编译。
- [x] 真实站点：改一篇文章后的重渲染页数。证据：用户在 Notion 编辑 /6167/ 后，silkroutecc 副本 `--no-clean` 构建重渲染 812/4651 页（metadata_changed 1 即 /6167/、data_dependency_changed 809 为标签页、list_render 2），此前同类改动为 4651 页；随后 `--clean` 构建与增量输出逐字节一致（`/tmp/claude/codex-reports/real-s6/report.md`）。标签页因主题 `partials/sidebar.html` 读取未声明的 `category_name`、`tags` 被记为读取全部。

证据（2026-11-10）：Rendering 404、Engine 3298、Architecture 422、cli-build、PublicApiDrift、格式检查、闭包 `/tmp/claude/codex-reports/stage6/closure.json`（未映射 `guide/dev/incremental-build.md` 由文档链接与大小检查覆盖）、静态检查全部通过；审查 `/tmp/claude/codex-reports/review-s6/report.md`（修复后 0 Critical / 0 Important）；AOT `aot-s6` 无 IL 警告。
