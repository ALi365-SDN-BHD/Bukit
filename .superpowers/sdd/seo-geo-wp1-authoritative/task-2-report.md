# Task 1-02：跨源 Notion relation 投影报告

## 交付

- 增加 `NotionCrossSourceRelationProjector`：在所有 source batch 已加载后建立全局目标索引；schema `relationMappings` 是唯一关系入口。
- 已加载目标优先投影为 `id`、`title`、`slug`、`type`、`url`、`image`、`sameAs`，并复用 `reference` 的 id/label/url 字段别名。
- 只有带 `reference` 的 mapping 会使用可选受限 resolver；禁止补查、无 resolver、权限失败和未命中均保留原始 ID 并写入结构化诊断。
- `CompositeContentProvider` 在全部 provider 完成加载后调用 projector；`ContentProviderFactory` 注入最终 schema。tag/category 原字段及既有 `_links` 行为没有被 projector 改写。
- relation cache 升为 version 2，支持 database/source scope，并持久化 image 与 sameAs；version 1 安全失效。

## TDD 证据

- RED-1：唯一允许命令 exit 1；编译失败于缺失 `NotionRelationProjectionSource`、`INotionRelationFallbackResolver`、`NotionRelationFallbackResult`，即 projector API 尚不存在。
- RED-2：同一唯一命令 exit 1；新增的合并路径测试因 `CompositeContentProvider` 尚不接受 schema 而失败。
- GREEN：同一唯一命令 exit 0，`Passed: 6, Failed: 0, Skipped: 0`。

```bash
dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj \
  -c Release \
  --filter "FullyQualifiedName~CrossSourceRelationProjectionTests"
```

覆盖：作者/来源/企业跨库投影、重复 ID 与循环、未发布的已加载目标、schema 禁止/允许补查、权限诊断、tag/category 保留、缓存 scope/version/image/sameAs round-trip，以及真实 Composite 合并路径。

## 自审与边界

- 未解析投影没有生成 slug、title 或 URL；canonical relation builder 不再把 map 中仅有的 ID 当作已解析 relation title。
- Raw document diagnostics 会被 normalizer 保留，从而不会在 canonical 路径中丢失。
- 未运行任何额外测试、构建、真实 Notion 调用、审计、config check、post-change、CI、smoke 或 release 命令。
- Commit：`feat(notion): project cross-source relations after merge`（本报告随该独立提交交付；最终 SHA 见任务交接状态）。
