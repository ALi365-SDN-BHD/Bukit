# Bukit 深度代码审计计划

## 审计目标

基于对 Bukit 仓库源码的全面阅读和深度理解，从架构、安全、性能、工程质量、可维护性五个维度进行系统性审计，产出可执行的问题清单和改进路线图。

## 审计范围

- **src/ 全部 8 个核心模块**（Bukit.Cli、Bukit.Config、Bukit.Content、Bukit.Engine、Bukit.Engine.Abstractions、Bukit.Rendering、Bukit.Routing、Bukit.Shared）
- **额外模块**：Bukit.Theme、Bukit.PluginSourceGenerator
- **测试覆盖**：tests/ 下 4 个测试项目
- **工程治理**：构建系统、CI/CD、AOT、编码规范
- **与历史审计的差异**：不重复已有审计结论，聚焦新发现或深度分析

## 执行步骤

### 第一步：架构审计

**目标**：验证模块边界、依赖方向、职责单一性是否符合设计预期。

1. **模块依赖图绘制**
   - 基于 `.csproj` 引用关系绘制有向依赖图
   - 检查是否存在循环依赖或违反分层原则的依赖
   - 重点检查 Bukit.Engine ↔ Bukit.Rendering ↔ Bukit.Theme 的耦合

2. **接口抽象质量评估**
   - 统计各模块的接口/实现比例
   - 评估 `IContentProvider`、`ITemplateRenderer`、`ISearchIndexBuilder` 等核心接口的抽象质量
   - 检查是否存在"假抽象"（只有一个实现的接口）

3. **God Class 残留检测**
   - 检查 `SiteEngine.cs` 是否仍然过大（目标 < 500 行）
   - 检查 `PageRenderDispatcher` 的职责膨胀趋势
   - 检查 `VariantBuildPipeline.ExecuteAsync` 是否已成为新的 God Method
   - 检查 `NotionContentProvider` 的职责是否过于集中

4. **Pipeline 模式一致性**
   - 验证 `ContentPipeline`（Pipe-and-Filter 模式）与 `BuildPipeline`/`RenderPipeline`/`AssetPipeline` 的模式一致性
   - 检查各 Pipeline 的接口是否统一
   - 评估 Stage 模式的成熟度

### 第二步：安全审计

**目标**：识别安全漏洞和潜在风险。

1. **路径遍历（Path Traversal）检测**
   - 检查 `ThemeComponentRegistry.ResolveSectionTemplate` / `ResolveComponentTemplate` 中 `Path.Combine(themeRoot, "layouts", def.Template)` 是否防御 `../`
   - 检查 `FileTemplateLoader` 是否对所有文件访问做了 root-boundary 检查
   - 检查 `BuildPathUtils`、`DirectoryCopy` 中的路径拼接

2. **XSS / HTML 注入检测**
   - 检查 `ImageImgFunction`（image.img helper）的 `src`、`alt`、`className` 是否做 HTML 属性转义
   - 检查 `ImageSrcsetFunction` 的 URL 协议白名单
   - 检查 `ScribanTemplateRenderer` 中所有写入 HTML 的输出路径
   - 检查 Notion rich text 渲染链路是否存在注入点

3. **SSRF 检测**
   - 检查 `SsrfGuard.cs` 的实现覆盖范围
   - 检查 `ImageAssetLocalizer` 的 URL 下载是否有协议/域名限制
   - 检查 `NotionApiClient` 的 HTTP 请求是否可控

4. **敏感信息泄露**
   - 检查 `ConfigLoader` 和 `AppConfig` 中是否可能通过日志或错误信息泄露 Notion API Key
   - 检查 `MetricsWriter` 输出是否包含敏感信息
   - 检查 `BuildReporter` / `BuildReportPipeline` 的输出

5. **并发安全**
   - 检查 `ComponentFunctions` 是否仍有 static 可变状态（已知 P0 问题）
   - 检查 `ScribanTemplateRenderer` 的 `_cache` 和 `_sectionTemplateCache`（ConcurrentDictionary，安全）
   - 检查 `PluginRunner` 的并行安全性

### 第三步：性能审计

**目标**：识别性能热点和优化机会。

1. **内存分配分析**
   - 检查 `PageRenderDispatcher` 中 `renderQueue.Concat(...).ToList()` 的全量物化
   - 检查列表页构建中的 `Where/OrderByDescending/ToList` 重复排序
   - 检查 `I18nOutputMerger` 中的内存分配

2. **IO 热点分析**
   - 检查 `IncrementalBuildEngine.ComputeContentHash` 中正文读取的代价
   - 检查 `ContentImageRewritePipeline` 中 6 轮正则扫描的 CPU 开销
   - 检查模板目录 hash 计算 (`ComputeCompositeTemplateHash`) 的 IO 频率

3. **并发效率**
   - 检查 `PageRenderDispatcher.DispatchAsync` 的 `Parallel.ForEachAsync` 使用
   - 检查 `SpecialListRenderer` 的串行尾部
   - 检查 `AssetPipeline` 中文件拷贝的并行度

4. **缓存效率**
   - 检查 `ScribanTemplateRenderer._cache` 的缓存策略和淘汰机制
   - 检查 `BodyCacheDecorator` 的命中率
   - 检查 `DirectoryHashCache` 的粒度

### 第四步：工程质量审计

**目标**：评估代码质量、测试覆盖、编码规范。

1. **测试覆盖分析**
   - 统计各模块的测试文件数和测试用例数
   - 识别零测试覆盖的核心文件
   - 检查是否存在没有集成测试的关键路径
   - 评估测试质量（是否有边界条件、异常路径、并发测试）

2. **错误处理审计**
   - 检查 `catch (Exception)` 的静默吞错误模式
   - 检查 `ThemeManifestLoader.Load` 是否已修复静默失败（已知已改为抛异常）
   - 检查 `ScribanTemplateRenderer` 中 component/section 渲染错误是否仍为 HTML 注释化
   - 检查 `DiagnosticCode` 枚举的覆盖范围和使用一致性

3. **编码规范一致性**
   - 检查 `#nullable enable` 是否在所有模块一致启用（Directory.Build.props 已全局设置）
   - 检查命名规范的一致性
   - 检查 using 排序和命名空间组织

4. **技术债务识别**
   - 检查 `ArgReader.cs`（旧 CLI 解析）与 `BukitCliSpecs`（新 CLI 绑定）的并存问题
   - 检查 `DevCommand` 是否仍手写解析
   - 检查 `BuildPipeline` 是否只是 executor 包装而无真正阶段模型
   - 检查魔法字符串和硬编码路径

### 第五步：可维护性审计

**目标**：评估长期维护和扩展的友好性。

1. **文档与代码一致性**
   - 对比 `guide/dev/architecture.md` 与当前实现的模块边界
   - 检查是否有架构文档描述但代码中不存在的组件
   - 检查代码注释的准确性和完整性

2. **扩展点设计**
   - 评估插件系统的扩展友好性（Plugin 接口、Protocol Plugin、Source Generator）
   - 评估 ContentProvider 接口是否便于添加新的内容源
   - 评估 Theme 系统的继承和覆盖机制

3. **配置模型演进**
   - 检查 `AppConfig` 的字段是否与 `site.yaml` schema 一致
   - 检查 `ConfigLoader` 是否读取了 `AppConfig` 中的所有字段（已知 theme.source 已修复）
   - 检查配置 deprecation 机制 (`ConfigDeprecationScanner`)

4. **诊断与调试能力**
   - 评估 `DoctorCommand` 和其子检查器的覆盖范围
   - 评估 `MetricsWriter` 输出的可用性
   - 评估 `Bukit.Diagnostics` 日志级别和结构化程度

## 输出物

1. **审计报告**（Markdown 格式）
   - 执行摘要（1 页）
   - 架构评分和详细分析
   - 安全问题清单（按严重度排序）
   - 性能热点清单（按影响面排序）
   - 工程质量评分
   - 可维护性评估

2. **问题追踪清单**（CSV/表格格式）
   - 问题编号、模块、文件、严重度、描述、修复建议、验证方法

3. **优先级路线图**
   - P0：必须立即修复（安全/并发/数据正确性）
   - P1：核心稳定性增强（架构/测试/错误处理）
   - P2：工程优化（性能/代码质量/技术债务）
   - P3：长期能力建设（文档/工具/扩展性）

## 验证方法

- **代码级验证**：逐个文件检查，使用 grep 搜索模式
- **构建验证**：`dotnet build bukit.slnx -c Release` 
- **测试验证**：`dotnet test bukit.slnx --no-restore`（目标 2000+ 通过）
- **静态分析**：检查 AOT warning、code style enforcement
- **文档对比**：将 `guide/dev/` 与 `src/` 实现逐项核对

## 时间安排

本次审计为全面深度审计，按模块逐层深入：

1. 架构层（Bukit.Engine、Bukit.Engine.Abstractions）
2. 内容层（Bukit.Content、Bukit.Routing）
3. 渲染层（Bukit.Rendering、Bukit.Theme）
4. 配置层（Bukit.Config）
5. CLI 层（Bukit.Cli）
6. 共享层（Bukit.Shared）
7. 测试与工程治理
8. 综合评估与报告
