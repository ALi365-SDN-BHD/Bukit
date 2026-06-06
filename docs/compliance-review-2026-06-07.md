# Bukit vNext 合规复审

发布日期：**2026-06-07**（Asia/Kuala_Lumpur）  
项目：**Bukit**  
范围：vNext Meta 移除、协议/CLI cleanup、文档一致性

## 结论

当前 vNext Meta 移除主目标 **通过复审（PASS）**：

- 运行时 ABI 中已无 `ContentItem` / `ContentItem.Meta`。
- `MetaHelpers.cs` 与 `ContentItemExtensions.cs` 已删除。
- 生产代码与测试未再通过 `.Meta`、`MetaHelpers`、legacy `ContentItem` 路径驱动业务。
- 协议 routed page payload 不再暴露 `Meta`，host 代码也已移除旧 `Meta` 命名。
- 协议 invocation / handshake 默认 schema version 已提升到 `2`，after-build negotiation 不再回退 v1。
- `seo audit` / `geo audit` 默认报告发现路径以 `.bukit/seo-report.json` 为首选，不再回退到根目录 `seo-report.json`。

本复审不宣称整个仓库所有历史技术债都已清零；它只覆盖本轮 vNext Meta cleanup 的合规性。

## 本轮验证

已执行：

```bash
dotnet build bukit.slnx -p:UseSharedCompilation=false -m:1 --no-restore
dotnet test bukit.slnx -p:UseSharedCompilation=false -m:1 --no-restore --no-build
```

结果：

- Build 通过。
- Test 通过。

建议在提交前再跑一次全量门禁：

```bash
bash scripts/quality-gate.sh
```

本轮没有重新执行该脚本，因此 full quality gate 仍标为“待最终确认”。

## 关键证据

代码扫描目标：

- `ContentItem`
- `MetaHelpers`
- `ContentItemExtensions`
- `BuildLegacy`
- `LoadLegacyRaw`
- `SchemaDefaultsStage`
- `SchemaValidateStage`
- `.Meta`
- `Page.Meta`
- `MaterializeRoutedPagesMeta`

当前生产与测试主路径未发现旧核心入口。文档中仍保留 `Meta` 字样用于描述设计历史、breaking change 和迁移计划，这是允许的。

## 已完成整改

### 协议 cleanup

- `ProcessPluginHost` 中旧方法名 `MaterializeRoutedPagesMeta` 已改为 `MaterializeRoutedPageFields`。
- routed page materialization 只处理 `Fields`，不再暗示 page-level `Meta`。
- 删除了 handshake 中未使用的 `supported` 局部变量。
- `ProtocolPluginInvocationRequest` 默认 `schemaVersion` 从 `1` 提升到 `2`。
- `ProtocolHandshakeRequest.HostSupportedSchemaVersions` 只声明 `2`。
- `ProtocolAfterBuildRunner` 不再在 handshake 失败、v1-only、无效 JSON 时回退 v1，而是 fail-fast。
- `ProtocolDerivePagesRunner` 发出的 request schema version 已改为 `2`。

### 类型与 canonical graph cleanup

- `RawContentDocument` 已引入 `RawBody`、`RawContentValue`、`ContentSourceInfo`。
- `ContentDocument` 已引入 `ContentBodyRef`、`ContentRoutePolicy`、`ContentPublishPolicy`、`ContentSourceInfo`、`ContentDiagnostic`。
- `ContentModelSchema` 已扩展 canonical/custom/entity/relation/media schema 形态。
- `ContentDocumentNormalizer` 已通过 `IContentNormalizer.Normalize(raw, schema)` 入口执行 schema-aware normalization。
- `CanonicalContentGraph` 已携带 graph-level `Documents` 与 `Relations`。

### CLI cleanup

- `SeoCommand.ResolveAuditReportPath` 默认只查找：
  - `.bukit/seo-report.json`
  - `.bukit/publish-audit-report.json`
- `GeoCommand.ResolveSeoReportPath` 默认只查找：
  - `.bukit/seo-report.json`
  - `.bukit/publish-audit-report.json`
- 根目录 `seo-report.json` 不再被默认发现，也不再写入文档契约。

### 测试 cleanup

- SEO/GEO 命令测试改为默认写入 `.bukit/seo-report.json`。
- 新增 SEO 测试覆盖默认忽略根目录 `seo-report.json`。
- 新增 GEO 测试覆盖默认忽略根目录 `seo-report.json`。

### 文档 cleanup

- vNext plan 状态已更新到 2026-06-07。
- compatibility governance 已把根目录 `seo-report.json` fallback 从 `deprecated-but-working` 改为 `rejected-with-message`，并删除历史报告审计叙事。
- SEO 文档与技能说明已改为 `.bukit/publish-audit-report.json` / `.bukit/seo-report.json` 默认发现模型。
- `seo-report.json` schema 文档已说明兼容报告位于 `.bukit/seo-report.json`。

## 剩余风险

### 1. Content model schema 尚未完全替代 collection field schema

`ContentModelSchema` 已有 canonical/custom/entity/relation/media shape，但 collection field schema 尚未完全收敛为它的投影。

建议：下一阶段把 collection field schema 收敛为 content model schema 的投影，而不是并行存在。

### 2. Normalizer strict diagnostics 尚未接入 build fail-fast 策略

未知 raw key strictness 当前落为 `ContentDocument.Diagnostics`，还没有统一纳入 `ContentGraphValidateStage` 的 strict fail-fast。

建议：下一阶段让 content diagnostics 进入统一 schema error report，并由 `SchemaFailMode` 控制 warn/strict。

### 3. Aggregated projections 仍有包装旧 generator 的实现

RSS、search、llms、robots 等聚合输出已经读 document-first 数据，但部分仍包装既有 generator。

建议：P3 阶段继续将它们改为纯 `IPublishProjection` 消费者。

### 4. Full quality gate 尚未在本轮复跑

`dotnet build` 和 `dotnet test` 已通过，但 `scripts/quality-gate.sh` 未在本轮复跑。

建议：提交前运行完整门禁并把结果写入 PR 描述。

## 当前判定

- vNext Meta ABI removal: **PASS**
- Protocol/CLI old Meta naming cleanup: **PASS**
- Protocol v2-only after-build negotiation: **PASS**
- vNext content type shape hardening: **PASS**
- Root `seo-report.json` automatic fallback removal: **PASS**
- Full repository compliance beyond this scope: **PARTIAL / requires separate audit**
