# Stabilize render dependency inputs

## Why

构建耗时修复计划（`docs/plans/2026-11-10-build-time-incremental-remediation-plan.zh-CN.md`）问题 4：silkroutecc 的 `--no-clean` 构建中 4352 页每次都是 `render_dependency_changed`，怀疑共享数据（`site.modules`、`site.data.pages_by_id` 等）里的签名链接每次查询都变，带动全站依赖哈希。计划要求先做 V1，再按结果在三层方案里选择。

### V1 结果（2026-11-10，silkroutecc 站点副本）

- **诊断方法**：四次 `--no-clean` 诊断构建，诊断模板把 `site`、`pages`、`data` 等模型逐字段写进首页注释。第 3 次在第 2 次后约 10 分钟，第 4 次在第 3 次后满 1 小时（签名已过期）。
- **第 3 次构建**：4651 页全部跳过，没有新媒体。
- **第 4 次构建**：期间 Notion 下架了一篇文章（`/6166/`），结果是 `content_changed 295`、`render_dependency_changed 4352`。逐页对比两次清单和模型：
  - 295 页：列表、分类、分页里的「共 N 项」从 3342 变为 3341，是下架引起的正常变化。
  - 70 页：文章正文里的图片换成新文件名。原因是签名过期后同一张图被当成新图重新下载，即问题 7。
  - 4285 页：只有依赖哈希变化。模型对比只有下架相关的字段不同（`data` 页面索引少一项，分类与标签计数变化），没有任何签名链接。
- **结论**：
  - 渲染模型里没有残留的签名链接，本地化文件名的差异来自问题 7。全站失效的原因是全站粒度的依赖哈希（问题 5），不是签名链接。
  - 按计划，这属于「只需第 1 层」：问题 7 稳定本地化文件名即可，不需要第 2 层扩大本地化覆盖面。
- **复测**（v2.0.13 起，含问题 7 与按页依赖）：间隔 1 小时 16 分钟的第二次 `--no-clean` 构建为 `unchanged 4648`、`list_unchanged 3`，`render_dependency_changed 0`，媒体无新下载（`/tmp/claude/codex-reports/real-v1/build2.md`）。这满足问题 4 的验收标准。

## What Changes

按计划问题 4 的实施步骤 2（只需第 1 层），本变更只包含两部分：

- **第 3 层诊断提示**：增量开启时，扫描进入依赖哈希的共享数据集合（`site.modules`、`site.data`、`site.data_index`、`site.related_pages`、`site.data_files`），找出已识别签名方案的链接（判定与媒体身份相同，用 `SignedUrlIdentity`）。若有，输出一条汇总警告 `event=incremental.signed_links keys=<读取键>:<个数>,… links=<总数>`，说明读取这些键的路由每次构建都会重渲染，并提示用 `content.media` 本地化。
  - 只加诊断，不改哈希内容。计划明确不做在哈希里忽略签名参数：页面若被跳过，输出目录里的旧 HTML 会带着已过期的签名链接，线上会出现 403。
- **回归测试**：同一输入连续两次构建，每个页面的依赖哈希都相同，且不出警告。另有一个测试：共享数据里的签名链接重新签名后出现警告，读取它的页面以 `data_dependency_changed` 照常重渲染。

## Impact

- 受影响代码：
  - 新增 `Bukit.Engine/Incremental/SignedLinkDiagnostics.cs`。
  - `Bukit.Engine/Incremental/SiteModelDataContributor.cs`：拆出 `EnumerateCollectionValues`，哈希写入的字节不变。
  - `Bukit.Engine/VariantRenderStage.cs`：增量开启时调用诊断。
- 受影响测试：新增 `SignedLinkDiagnosticsTests`。
- 公共契约：无公共 API、清单格式或报告结构变化，只新增一条警告日志。
- 不涉及 `PagesIndexPlugin`：V1 没有发现它的字段里残留签名链接，它的下载器已在问题 7 中改用同一身份函数。
