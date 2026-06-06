# Bukit vNext 合规复审

发布日期：**2026-06-07**（Asia/Kuala_Lumpur）  
项目：**Bukit**  
范围：vNext Meta 移除、协议/CLI cleanup、文档一致性、`.trae/specs` 合规、`InternalsVisibleTo` 边界、空 `catch`、依赖矩阵

## 结论

当前 vNext Meta 移除主目标与本轮合规治理项 **通过复审（PASS）**：

- 运行时 ABI 中已无 `ContentItem` / `ContentItem.Meta`。
- `MetaHelpers.cs` 与 `ContentItemExtensions.cs` 已删除。
- 生产代码与测试未再通过 `.Meta`、`MetaHelpers`、legacy `ContentItem` 路径驱动业务。
- 协议 routed page payload 不再暴露 `Meta`，host 代码也已移除旧 `Meta` 命名。
- 协议 invocation / handshake 默认 schema version 已提升到 `2`，after-build negotiation 不再回退 v1。
- `seo audit` / `geo audit` 默认报告发现路径以 `.bukit/seo-report.json` 为首选，不再回退到根目录 `seo-report.json`。
- `.trae/specs/compliance-hardening-vnext/` 已补齐 spec、tasks、checklist。
- 生产程序集不再通过 `InternalsVisibleTo` 暴露给其他生产程序集；CLI 需要的 Engine 能力已改为显式 public API。
- `src/**/*.cs` 与 `tests/**/*.cs` 中已无空 `catch {}` 命中。
- 依赖矩阵与 IVT allowlist 已由 architecture tests 覆盖。

本复审不宣称整个仓库所有历史技术债都已清零；本轮已把生产代码与测试代码中的空 `catch` 写法收敛为显式处理或命名 best-effort helper。

## 本轮验证

已执行：

```bash
dotnet build bukit.slnx -p:UseSharedCompilation=false -m:1 --no-restore
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -p:UseSharedCompilation=false --no-restore --no-build
dotnet test bukit.slnx -p:UseSharedCompilation=false -m:1 --no-restore --no-build
rg -n "catch\\s*(\\([^)]*\\))?\\s*\\{\\s*\\}" src tests -g '*.cs' -g '!bin' -g '!obj'
rg -n "InternalsVisibleTo" src tests -g '*.cs' -g '*.csproj' -g '!bin' -g '!obj' -g '!lscache'
```

结果：

- Build 通过。
- Architecture tests 通过：12 passed。
- Full test suite 通过：3435 passed。
- 全仓 `src + tests` 空 `catch` 扫描无命中。
- IVT 扫描仅保留测试程序集/benchmark 目标，未发现生产程序集目标。

建议在提交前再跑一次全量门禁：

```bash
bash scripts/quality-gate.sh
```

本轮没有重新执行该脚本，因此 quality-gate shell wrapper 仍标为“待最终确认”；编译、架构测试与完整 dotnet test 已通过。

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

### Compliance hardening

- 新增 `.trae/specs/compliance-hardening-vnext/spec.md`、`tasks.md`、`checklist.md`，让本轮治理有可追踪规范。
- 移除 `Bukit.Engine -> Bukit.Cli/bukit`、`Bukit.Engine.Abstractions -> Bukit.Engine/bukit`、`Bukit.Shared -> Bukit.Content` 等生产 `InternalsVisibleTo` 目标。
- 删除空壳 `src/Bukit.Engine.Abstractions/InternalsVisibleTo.cs`。
- 将 CLI 真实需要的 Engine 表面改为显式 public API：`BuildPathUtils`、`DefaultContentProviderFactory`、`ThemeBootstrapper`、`ThemeTemplateResolver`、`TemplateCapabilitiesResolver`、`ScribanTemplateLinter` 及其必要 DTO。
- 收紧 `DependencyMatrixTests.InternalsVisibleTo_MustOnlyExposeTo_TestAssemblies`，allowlist 不再包含生产程序集。
- 替换生产代码中的空 `catch {}`：build manifest cleanup、dev response handling、theme install/pack cleanup、theme pack YAML parse、template linter。
- 新增 `tests/TestCleanup.cs` 与 `tests/Directory.Build.props`，把测试 teardown 中的目录/文件清理统一为命名 best-effort helper。
- 将测试中的预期异常吞掉改为显式 `Assert.NotNull(ex)`，保留测试意图但去掉空 catch。
- 未做宽泛 project reference 重排；当前依赖矩阵测试无违规，避免无证据的大规模项目引用调整。

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

### 4. 测试 teardown 已清理，但仍是 best-effort 策略

测试项目的临时目录/文件删除已集中到 `TestCleanup`，并显式消费允许忽略的异常。

建议：如后续要进一步提升可观测性，可让 `TestCleanup` 在诊断模式下写入 test output；当前保持静默以避免 teardown 噪声。

### 5. Full quality gate wrapper 尚未在本轮复跑

`dotnet build`、architecture tests 和 full `dotnet test` 已通过，但 `scripts/quality-gate.sh` 未在本轮复跑。

建议：提交前运行完整门禁脚本并把结果写入 PR 描述。

## 当前判定

- vNext Meta ABI removal: **PASS**
- Protocol/CLI old Meta naming cleanup: **PASS**
- Protocol v2-only after-build negotiation: **PASS**
- vNext content type shape hardening: **PASS**
- Root `seo-report.json` automatic fallback removal: **PASS**
- `.trae/specs` compliance hardening: **PASS**
- Production `InternalsVisibleTo` hardening: **PASS**
- Empty `catch` cleanup across `src + tests`: **PASS**
- Dependency matrix tests: **PASS**
- Full repository compliance beyond this scope: **PARTIAL / quality-gate wrapper remains a separate follow-up**
