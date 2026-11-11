# Tasks

## 1. V1 与方案选择

- [x] V1：两次间隔超过 1 小时的 `--no-clean` 构建，逐字段对比 `site.modules`、`site.data`、`site.data_index`、`related_pages` 与页面字段，结论写入 proposal。
- [x] 按 V1 结论选择第 1 层（靠问题 7 稳定），不做第 2 层。

## 2. 诊断提示（第 3 层）

- [x] `SiteModelDataContributor` 拆出 `EnumerateCollectionValues`，`EnumerateCollections` 改为基于它，哈希写入的字节不变。
- [x] `SignedLinkDiagnostics`：遍历集合值（字符串、`ContentField`、`ModuleInfo`、`PageInfo`、字典、序列，防循环），只在含 `signature` 的文本里找链接；HTML 中的 `&amp;` 先还原；签名判定用 `SignedUrlIdentity`。
- [x] `VariantRenderStage`：增量开启时输出汇总警告，不改哈希。

## 3. 测试

- [x] 签名判定：AWS、HTML 转义、Notion 链接计数；非签名方案的 `signature` 参数、普通链接、无链接文本不计数。
- [x] 回归：同一输入连续两次构建，每页依赖哈希相同，全部命中，无警告。
- [x] 重新签名：出现 `keys=site.data_files:1` 警告，读取该数据的页面为 `data_dependency_changed`，其余页面 `unchanged`，输出带新签名。

## 4. 文档

- [x] 用户指南排错章节说明 `event=incremental.signed_links` 警告。
- [x] 中英马 CHANGELOG（Unreleased）。

## 5. 验证

- [x] Engine 专项测试、格式检查（含 CRLF）、`openspec validate stabilize-render-dependency-inputs --strict`。
- [x] 验收：silkroutecc 内容无改动、间隔超过 1 小时的两次 `--no-clean` 构建 `render_dependency_changed` 为 0（`/tmp/claude/codex-reports/real-v1/build2.md`：`unchanged 4648`、`list_unchanged 3`）。
