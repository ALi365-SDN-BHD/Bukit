# Bukit Build Report 系统 TDD 开发技术方案

## Summary

本计划基于 `.trae/documents/bukit-next-stage-roadmap-codex (2).md` 的第二阶段：Build Report 系统。目标是在不继续扩展全路线图范围的前提下，优先落地可审计、UI 可读取、AI 可判断的构建报告能力。

本次范围锁定为：

- 通过配置开启 Build Report，而不是每次默认生成。
- 新增 `build.report.enabled` 配置入口。
- 构建成功后生成 `dist/.bukit/` 下的报告文件。
- 将现有根目录 `seo-report.json` 迁移到 `dist/.bukit/seo-report.json`。
- 使用 TDD：每个行为先写失败测试，确认失败原因正确，再写最小实现，最后重构。
- 保持现有 CLI 调用方式兼容：`bukit build`、`BuildCommand.RunAsync` 仍可直接调用，不引入新的必填参数。
- 保持 Native AOT 兼容：JSON 输出继续优先使用 `Utf8JsonWriter` 或 `System.Text.Json` source generation，避免反射依赖。

不纳入本次实施：

- 不拆分完整 Build Pipeline。
- 不实现 BukitJalil 本地 App。
- 不实现 AI Agent 自动建站。
- 不新增 CLI 开关，除非后续明确要求。
- 不补全 roadmap 中所有 example site，只在必要时使用最小测试 fixture。

## Current State Analysis

### Roadmap 目标

`.trae/documents/bukit-next-stage-roadmap-codex (2).md` 第二阶段要求构建后生成：

- `dist/.bukit/build-report.json`
- `dist/.bukit/routes.json`
- `dist/.bukit/assets.json`
- `dist/.bukit/security-report.json`
- `dist/.bukit/seo-report.json`
- 可选：`dist/.bukit/incremental-manifest.json`

Roadmap 还要求：

- `SiteEngine.BuildAsync` 返回 `BuildResult`。
- 不破坏现有 CLI 行为。
- 新增报告相关测试。
- 保持 Native AOT 兼容。

### 当前代码结构

关键入口：

- `src/Bukit.Cli/Commands/BuildCommand.cs`
  - 负责解析 build 命令参数、加载配置、构造 `ConfigOverrides`。
  - 当前调用 `await engine.BuildAsync(config, resolved.RootDir, overrides);` 并返回 `0`。
  - 计划中保留 CLI 行为，不要求 CLI 消费 `BuildResult`。

- `src/Bukit.Engine/SiteEngine.cs`
  - 主构建流程集中在 `BuildAsync(AppConfig config, string rootDir, ConfigOverrides overrides, CancellationToken cancellationToken = default)`。
  - 单语言构建调用 `BuildVariantAsync` 后写 metrics、标记 recovery 完成并返回。
  - 多语言构建并发生成 `BuildVariantResult[]`，再合并 sitemap/search/rss/SEO，并写 metrics。
  - `BuildVariantAsync` 已返回 `BuildVariantResult`，其中包含 routes、SEO index、SEO models、插件执行、render counts、stage metrics 等报告需要的大量数据。

- `src/Bukit.Engine/BuildVariantResult.cs`
  - 当前为 `internal sealed record BuildVariantResult(...)`。
  - 已包含 `Routed`、`DerivedRouted`、`DerivedRoutes`、`SeoIndex`、`SeoModels`、`PluginExecutions`、`RenderedCount`、`SkippedCount`、`RenderReasons`、`StageMetrics`。
  - 可作为 `BuildResult` 聚合输入，避免重复扫描输出目录。

- `src/Bukit.Engine/MetricsWriter.cs`
  - 当前通过 `--metrics` 仅在显式指定路径时写 metrics JSON 和 HTML。
  - 内部使用 `Utf8JsonWriter`，符合 AOT 友好方向。
  - 可借鉴其写法，但 Build Report 应作为独立系统，不应复用 `--metrics` 语义。

- `src/Bukit.Engine/SeoAuditReportWriter.cs`
  - 当前 `WriteReport` 将 SEO 报告写到输出根目录的 `seo-report.json`。
  - 已有 source generation：`SeoAuditReportJsonContext`。
  - 本计划要求迁移到 `.bukit/seo-report.json`，这会影响现有 SEO 测试和任何期望根目录文件的调用。

- `src/Bukit.Config/AppConfig.cs`
  - `BuildConfig` 当前包含 `Output`、`Clean`、`Draft`、`ListPageContentMode`、`SchemaFailMode`。
  - 需要新增 `BuildReportConfig Report { get; init; } = new();`，其中至少有 `Enabled`。

- `src/Bukit.Config/ConfigLoader.cs`
  - 当前读取 `build.output`、`build.clean`、`build.draft`、`build.listPageContentMode`、`build.schemaFailMode`。
  - 需要读取 `build.report.enabled`。

- `src/Bukit.Config/ConfigValidator.cs`
  - 当前校验 `build.output` 与 `build.listPageContentMode`。
  - `build.report.enabled` 是 bool，无需复杂校验；如果后续增加路径字段再校验 path traversal。

### 当前测试结构

- `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`
  - 已有最小站点集成测试，可作为 Build Report 集成测试模式参考。

- `tests/Bukit.Engine.Tests/MetricsWriterTests.cs`
  - 已覆盖 metrics JSON/HTML 的写出行为，可作为新 `BuildReporterTests` 的风格参考。

- `tests/Bukit.Engine.Tests/SeoAuditReportWriterTests.cs`
  - 已大量覆盖 SEO audit build 行为。
  - 需要新增或调整测试来验证 `seo-report.json` 写入 `.bukit/`，且根目录不再生成旧文件。

- `tests/Bukit.Config.Tests/ConfigLoaderTests.cs` 或 `ConfigLoaderFullCoverageTests.cs`
  - 适合新增 `build.report.enabled` 配置加载测试。

### 构建与质量约束

项目规则中要求：

- 构建/类型检查：`dotnet build bukit.slnx -c Release -warnaserror`
- 测试：
  - `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release`
  - `dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release`
  - `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release`
  - `dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release`
- 格式检查：`dotnet format bukit.slnx --verify-no-changes`

CI 还运行：

- `dotnet build bukit.slnx -c Release`
- `dotnet test bukit.slnx -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults`
- `dotnet format bukit.slnx --verify-no-changes`
- `bash scripts/check-doc-asset-consistency.sh`
- `bash scripts/smoke.sh Release`

全局启用 nullable、latest analysis、EnforceCodeStyleInBuild、TreatWarningsAsErrors。

## Proposed Changes

### 1. 新增配置模型与加载

#### 文件：`src/Bukit.Config/AppConfig.cs`

新增：

- `public sealed record BuildReportConfig`
- `public bool Enabled { get; init; }`
- `BuildConfig` 中新增 `public BuildReportConfig Report { get; init; } = new();`

建议默认值：

- `build.report.enabled` 默认 `false`。

原因：用户已明确选择“配置开启”。默认关闭可以避免对所有现有构建产生额外文件，同时保留向后兼容。

#### 文件：`src/Bukit.Config/ConfigLoader.cs`

新增读取逻辑：

- 从 `build.report.enabled` 读取 bool。
- 若 `build` 或 `report` 节点不存在，则默认 `Enabled = false`。

需要优先查看现有 helper 是否支持读取嵌套 mapping；若没有，使用项目现有 YamlDotNet 节点读取模式实现私有 helper。

#### 文件：`src/Bukit.Config/ConfigValidator.cs`

暂不需要新增复杂校验。若实现中新增 `build.report.outputDir` 之类字段，则必须校验不能包含 `..`；本计划不新增路径配置。

#### TDD 步骤

1. RED：在 `tests/Bukit.Config.Tests/ConfigLoaderTests.cs` 或 `ConfigLoaderFullCoverageTests.cs` 新增测试：当 YAML 包含
   ```yaml
   build:
     report:
       enabled: true
   ```
   时，`config.Build.Report.Enabled` 为 `true`。
2. RED：新增默认值测试：未配置 `build.report.enabled` 时为 `false`。
3. 确认测试失败原因是属性或加载逻辑缺失。
4. GREEN：最小实现 `BuildReportConfig` 和 loader 读取。
5. REFACTOR：如有重复 YAML 节点读取逻辑，再提取私有 helper。

### 2. 新增 BuildResult 聚合模型

#### 文件：`src/Bukit.Engine/BuildResult.cs`

新增 internal record，建议字段：

- `string Version`
- `DateTimeOffset StartedAt`
- `DateTimeOffset EndedAt`
- `long DurationMs`
- `BuildEnvironmentInfo Environment`
- `BuildProjectInfo Project`
- `BuildSummary Summary`
- `BuildIncrementalSummary Incremental`
- `IReadOnlyList<BuildVariantResult> Variants`
- `IReadOnlyList<string> GeneratedFiles`

辅助 records：

- `BuildEnvironmentInfo`
  - `string OS`
  - `string Runtime`
  - `bool Aot`
- `BuildProjectInfo`
  - `string Root`
  - `string Output`
  - `string ContentSource`
  - `string? ThemeName`
  - `string? ThemeSource`
- `BuildSummary`
  - `int PageCount`
  - `int RouteCount`
  - `int AssetCount`
  - `int MediaCount`
  - `int PluginCount`
  - `int WarningCount`
  - `int ErrorCount`
- `BuildIncrementalSummary`
  - `bool Enabled`
  - `int CacheHitCount`
  - `int CacheMissCount`

实现初期可接受部分字段先由现有数据推导：

- `RouteCount` = 所有 variant 的 `Routed.Count + DerivedRouted.Count + list route count` 的可得部分。第一版建议用 `Routed.Count + DerivedRouted.Count`，后续在 routes.json 包含 list routes 后再同步扩展。
- `PageCount` = `RenderedCount + SkippedCount` 聚合，代表本次参与构建的页面数。
- `PluginCount` = distinct plugin executions count 或 `PluginExecutions.Count`，先按现有执行记录聚合。
- `CacheHitCount` = render reasons 中 skip 相关项的合计；`CacheMissCount` = render reasons 中 render 相关项合计。若 reason 语义不足，第一版可直接从 `SkippedCount` / `RenderedCount` 映射，并在测试中锁定。
- `WarningCount` / `ErrorCount` 当前 logger 未结构化统计，第一版为 `0`，后续可引入 collecting logger 或 diagnostic sink。

#### 文件：`src/Bukit.Engine/SiteEngine.cs`

修改公开构建方法：

- 将 `public async Task BuildAsync(...)` 改为 `public async Task<BuildResult> BuildAsync(...)`。
- 调用方可以忽略返回值，因此 CLI 源码只需 `await engine.BuildAsync(...)`，行为不变。
- 单语言和多语言路径都应返回 `BuildResult`。
- 保留旧 `BuildAsync(IContentProvider provider, BuildOptions options, ...)` 的签名不变，除非测试要求；该 overload 看起来是简化构建路径，不属于 roadmap 阶段 2 主线。

实现方式：

- 方法开头记录 `startedAt` 与 stopwatch。
- 构建成功后根据 `BuildVariantResult` 聚合 `BuildResult`。
- 在 `BuildRecoveryTracker.MarkCompleted(outputDir)` 之前或之后生成报告都可，但建议在所有报告文件写入成功后再 `MarkCompleted`，避免报告失败却标记完成。
- 如 Build Report 未启用，也仍返回 `BuildResult`，但不写文件。

#### TDD 步骤

1. RED：新增 `tests/Bukit.Engine.Tests/BuildResultTests.cs` 或在 `SiteEngineIntegrationTests.cs` 新增测试，调用 `var result = await engine.BuildAsync(...)`，断言：
   - `result.Project.Output == "dist"`
   - `result.Project.ContentSource == "markdown"`
   - `result.Summary.PageCount > 0`
   - `result.Variants.Count == 1`
2. 确认测试因 `BuildAsync` 返回 `Task` 或 `BuildResult` 缺失而失败。
3. GREEN：新增最小 `BuildResult` 模型并让 `BuildAsync` 返回。
4. REFACTOR：把聚合逻辑移到 `BuildResultFactory` 或后续 `BuildReporter` 内部，避免 `SiteEngine` 进一步膨胀。

### 3. 新增 BuildReporter 写入器

#### 文件：`src/Bukit.Engine/BuildReporter.cs`

新增 internal static writer，职责：

- 在 `outputDir/.bukit/` 下写报告文件。
- 只负责报告写出，不负责驱动构建。
- JSON 输出保持确定性，便于测试。
- 使用 `Utf8JsonWriter` 或 source-generated `JsonSerializerContext`。

建议第一版方法：

- `WriteIfEnabled(AppConfig config, string rootDir, string outputDir, BuildResult result, IReadOnlyList<BuildVariantResult> variants, ILogger logger)`
- 如果 `!config.Build.Report.Enabled`，直接 return。
- 创建 `Path.Combine(outputDir, ".bukit")`。
- 写入：
  - `build-report.json`
  - `routes.json`
  - `assets.json`
  - `security-report.json`

`seo-report.json` 由 `SeoAuditReportWriter` 迁移写入 `.bukit/`，但也可以由 `BuildReporter` 调用统一复制/写入。为了避免双重职责，推荐迁移 `SeoAuditReportWriter` 的输出路径，让 SEO writer 自己写 `.bukit/seo-report.json`。

#### build-report.json 内容

第一版必须包含：

- `version`
- `startedAt`
- `endedAt`
- `durationMs`
- `environment`
- `project`
- `summary`
- `incremental`
- `generatedFiles`

为测试稳定性：

- 测试不应断言具体时间值，只断言字段存在、可解析、`durationMs >= 0`。
- 列表排序使用 `StringComparer.OrdinalIgnoreCase` 或固定路由顺序。

#### routes.json 内容

从 `BuildVariantResult.Routed` 和 `DerivedRouted` 生成。

字段：

- `url`
- `outputPath`
- `template`
- `source`
- `kind`
- `language`

推导规则：

- `url`、`outputPath`、`template` 来自 `RouteInfo`。
- `source` 优先从 `ContentItem.Meta` 中的 source path 类字段读取；若当前模型没有稳定字段，则第一版可以为 `null`，但测试必须锁定当前可获得行为。
- `kind` 优先读取 `collection`，其次 `type`，否则 `page`。
- `language` 来自 variant 的 `Language`。
- 多语言时 outputPath 是否包含语言前缀应按实际 variant 输出保持一致；不要人为改写已有 RouteInfo，除非 merged report 需要展示相对 `dist` 的路径。第一版推荐保留 variant 内 `Route.OutputPath`，并用 `language` 字段区分。

#### assets.json 内容

第一版可从输出目录扫描 `assets/` 下文件生成：

- `path`
- `hash`
- `size`

`source` 当前 `DirectoryCopy.Sync` 未返回 source mapping，第一版允许 `source: null`。如果后续要精确 source，需要改造 asset copy pipeline，不在阶段 2 第一版内完成。

哈希：

- 使用现有 `src/Bukit.Engine/Incremental/HashUtil.cs`，如果 API 合适。
- 如果 HashUtil 不适合公开调用，则在 `BuildReporter` 内部使用 `SHA256.HashData`。
- 输出为 `sha256:<hex>`。

扫描约束：

- 仅扫描 `outputDir/assets`，避免报告自身进入 assets 列表。
- 排序按相对路径 ordinal ignore case。

#### security-report.json 内容

第一版输出最小可审计结构：

- `status`: `passed`
- `warnings`: `[]`
- `errors`: `[]`
- `checks`:
  - `routeTraversal`: `passed`
  - `unsafeSlug`: `passed`
  - `pluginOutputPath`: `passed`
  - `remoteThemeLock`: `passed`

原因：当前安全失败会在构建过程中抛出异常，成功到达 reporter 即代表这些 gate 未失败。后续可接入结构化 diagnostic sink，报告具体 warnings/errors。

#### TDD 步骤

1. RED：新增 `tests/Bukit.Engine.Tests/BuildReporterTests.cs`，构造最小 `BuildResult` 和 `BuildVariantResult`，启用 `BuildConfig.Report.Enabled = true`，调用 writer，断言 `.bukit/build-report.json` 存在且包含关键字段。
2. RED：新增 routes 测试，构造两个 routed item，断言 `routes.json` 按稳定顺序输出路由。
3. RED：新增 assets 测试，在 temp output 的 `assets/css/main.css` 写文件，断言 `assets.json` 包含 path、size、`sha256:` hash。
4. RED：新增 security report 测试，断言 status 和 checks。
5. RED：新增 disabled 测试，`Enabled = false` 时不创建 `.bukit/build-report.json`。
6. GREEN：最小实现 `BuildReporter`。
7. REFACTOR：提取 `WriteJson`、`EnumerateAssets`、`BuildRouteEntries` 私有方法。

### 4. 将 BuildReporter 接入 SiteEngine

#### 文件：`src/Bukit.Engine/SiteEngine.cs`

单语言路径：

- 在 `BuildVariantAsync` 返回后，构造 `BuildResult`。
- 若 `effectiveConfig.Build.Report.Enabled`，调用 `BuildReporter.WriteIfEnabled(...)`。
- 继续执行 `MetricsWriter.WriteIfRequested(...)`。
- 标记 build completed。
- 返回 `BuildResult`。

多语言路径：

- `variantResults` 完成后，执行现有 root outputs、merged SEO、metrics。
- 构造 merged `BuildResult`。
- 若启用，写 `.bukit` 报告。
- 标记 build completed。
- 返回 `BuildResult`。

注意：

- 多语言分支目前没有显式 `BuildRecoveryTracker.MarkCompleted(outputDir)` 的早期 return 之外逻辑已存在，需要确认保留。
- 报告生成失败应让 build 失败还是 warn？第一版建议严格失败，因为报告是明确启用的构建产物；测试可不覆盖异常路径。

#### TDD 步骤

1. RED：在 `SiteEngineIntegrationTests.cs` 新增测试：配置 `Build = new BuildConfig { Output = "dist", Clean = true, Report = new BuildReportConfig { Enabled = true } }`，构建后断言：
   - `dist/.bukit/build-report.json` 存在。
   - `dist/.bukit/routes.json` 存在且包含 `/blog/hello-world/` 或实际生成 URL。
   - `dist/.bukit/security-report.json` 存在。
2. RED：新增测试：未启用 report 时，`dist/.bukit/build-report.json` 不存在。
3. GREEN：接入 `BuildReporter`。
4. REFACTOR：将 result 构造提取到 `BuildResultFactory`，让 `SiteEngine` 只负责 orchestrate。

### 5. 迁移 SEO 报告输出路径

#### 文件：`src/Bukit.Engine/SeoAuditReportWriter.cs`

修改：

- `WriteReport` 从 `FileWriter.WriteUtf8(outputDir, "seo-report.json", ...)` 改为写入 `Path.Combine(".bukit", "seo-report.json")`。
- 确保 `FileWriter.WriteUtf8` 会创建子目录；如果不会，则在 writer 中先 `Directory.CreateDirectory(Path.Combine(outputDir, ".bukit"))`。
- 不再写根目录 `seo-report.json`。

影响：

- 单语言 `BuildVariantAsync` 每个 variant 仍会写该 variant outputDir 下的 `.bukit/seo-report.json`。
- 多语言 `WriteMerged` 会写 root outputDir 下的 `.bukit/seo-report.json`。
- 若同时单语言 variant outputDir 与 root outputDir 相同，最终为单份 `.bukit/seo-report.json`。

#### TDD 步骤

1. RED：在 `SeoAuditReportWriterTests.cs` 新增 `Write_WritesReportUnderBukitDirectory`：调用 `Write` 后断言 `_outputDir/.bukit/seo-report.json` 存在。
2. RED：新增 `Write_DoesNotWriteLegacyRootReport`：断言 `_outputDir/seo-report.json` 不存在。
3. GREEN：修改输出路径。
4. REFACTOR：提取 `GetReportPath(outputDir)` 或 `ReportOutputDirectoryName` 常量，供 BuildReporter 复用。

### 6. 更新配置文档或 schema 生成测试

#### 文件：`tests/Bukit.Config.Tests/ConfigJsonSchemaGeneratorTests.cs`

如果当前 schema 生成器自动从模型反射或手写字段，需要新增/调整测试，确保 `build.report.enabled` 出现在生成 schema 中。

#### 用户文档

本次计划不主动创建或修改文档，除非实现阶段发现现有文档测试要求更新。若 CI 的 doc consistency 因配置 schema/example 变化失败，再最小更新对应既有文档，不创建新文档。

## TDD Execution Order

严格按以下顺序执行，不跳过 RED：

1. 配置读取测试先行
   - 写 `build.report.enabled` 加载测试。
   - 运行目标测试，确认失败。
   - 实现配置模型与 loader。
   - 运行目标测试，确认通过。

2. BuildResult 返回值测试先行
   - 写 `SiteEngine.BuildAsync` 返回 `BuildResult` 的测试。
   - 运行目标测试，确认因返回类型或模型缺失失败。
   - 实现 `BuildResult` 与返回值。
   - 运行目标测试，确认通过。

3. BuildReporter 单元测试先行
   - 逐个写 `build-report.json`、`routes.json`、`assets.json`、`security-report.json`、disabled 行为测试。
   - 每个测试都先单独运行并确认失败。
   - 最小实现对应 writer 行为。
   - 每个测试通过后再进入下一个行为。

4. SiteEngine 集成测试先行
   - 写启用 report 后构建产生 `.bukit` 报告的集成测试。
   - 写未启用 report 不产生 Build Report 的集成测试。
   - 确认失败后接入 `BuildReporter`。

5. SEO 迁移测试先行
   - 写 `.bukit/seo-report.json` 输出测试。
   - 写根目录不再输出测试。
   - 确认失败后迁移 `SeoAuditReportWriter`。

6. 回归与重构
   - 在所有新增行为变绿后，清理重复代码。
   - 不新增额外功能。
   - 保持 JSON 输出稳定、排序稳定。

## Verification Steps

实现完成后必须运行：

```bash
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj -c Release
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release
dotnet build bukit.slnx -c Release -warnaserror
dotnet format bukit.slnx --verify-no-changes
```

如果时间允许或准备进入 PR，还应运行 CI 同等命令：

```bash
dotnet test bukit.slnx -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
bash scripts/check-doc-asset-consistency.sh
bash scripts/smoke.sh Release
```

如果改动涉及 AOT 风险，应额外运行 roadmap 建议命令：

```bash
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c Release -p:PublishAot=true
```

## Acceptance Criteria

- `build.report.enabled: true` 时，成功构建会生成：
  - `dist/.bukit/build-report.json`
  - `dist/.bukit/routes.json`
  - `dist/.bukit/assets.json`
  - `dist/.bukit/security-report.json`
  - `dist/.bukit/seo-report.json`
- `build.report.enabled` 未配置或为 false 时，不生成 Build Report 主报告文件。
- SEO 报告只写入 `.bukit/seo-report.json`，不再写根目录 `seo-report.json`。
- `SiteEngine.BuildAsync(AppConfig, string, ConfigOverrides, CancellationToken)` 返回 `BuildResult`。
- `BuildCommand.RunAsync` 外部行为不变，仍返回 `0` 表示成功。
- 新增测试都先经历失败，再通过最小实现变绿。
- Release build 无警告错误。
- 格式检查通过。
- 不引入反射重 JSON 序列化模式导致 Native AOT 风险。

## Assumptions & Decisions

- `build.report.enabled` 默认 false，是已确认的产品决策。
- SEO 报告迁移到 `.bukit/seo-report.json`，是已确认的兼容性取舍；这可能是破坏性变更，需要测试明确锁定。
- `BuildReporter` 第一版负责报告，不负责拆分 pipeline。
- `assets.json` 第一版从输出目录扫描 assets，`source` 可以为 null，因为现有 `DirectoryCopy.Sync` 没有返回 source mapping。
- `security-report.json` 第一版以构建成功为 passed，后续再接入结构化安全诊断。
- `WarningCount` 与 `ErrorCount` 第一版可为 0，后续由结构化 logger/diagnostic sink 增强。
- 时间字段不参与确定值断言，只断言存在和格式。
- 不创建新文档文件；只在已有文档或 schema 测试要求时最小修改既有文件。
