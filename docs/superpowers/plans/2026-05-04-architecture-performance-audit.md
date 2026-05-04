# Bukit 架构与性能审计 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 基于当前代码与文档，对 Bukit 的架构成熟度、性能水平、主要瓶颈和后续优化路径形成一份可执行、可验证的审计结论。

**Architecture:** 审计从主构建链路 `Cli -> Config -> Content -> Routing -> Rendering -> Engine -> Plugins` 出发，结合测试、文档和治理脚本判断项目的结构质量与工程成熟度；性能部分聚焦增量构建、列表构建、Notion 请求路径、模板渲染与媒体重写。结论按“现状优势、结构风险、性能热点、优先级优化路线”组织，避免泛泛而谈。

**Tech Stack:** .NET 10, C#, Native AOT, Scriban, YamlDotNet, Notion API, xUnit

---

## 当前判断摘要

- 项目水平：中上。
- 架构质量：分层明确，模块边界总体健康，扩展点设计较成熟。
- 工程治理：较强，已具备测试、AOT、smoke、格式与性能基线治理。
- 性能状态：已做第一阶段优化，但在大规模内容、远程内容源和列表型页面场景仍有明显热点。
- 优化优先级：先补观测，再压低增量判定成本，再重构列表路径与 Notion 请求拓扑。

### Task 1: 完成架构现状审计

**Files:**
- Read: `e:\Github\Bukit\README.zh-CN.md`
- Read: `e:\Github\Bukit\guide\dev\architecture.md`
- Read: `e:\Github\Bukit\src\Bukit.Cli\Program.cs`
- Read: `e:\Github\Bukit\src\Bukit.Config\ConfigLoader.cs`
- Read: `e:\Github\Bukit\src\Bukit.Engine\SiteEngine.cs`
- Read: `e:\Github\Bukit\src\Bukit.Engine\Plugins\PluginRunner.cs`
- Read: `e:\Github\Bukit\src\Bukit.Rendering\Scriban\ScribanTemplateRenderer.cs`

- [ ] 确认主链路分层是否清晰，是否存在明显反向依赖或职责错位。
- [ ] 记录当前架构优势：
  - `SiteEngine` 已从 God Class 收敛为薄编排器。
  - `ContentItem` 统一抽象了承载 Markdown / Notion 内容。
  - 渲染器、内容提供器、搜索索引 builder 具备接口替换能力。
  - 插件体系已支持 built-in / protocol / assembly 扩展。
- [ ] 标记高复杂度组件：
  - `src/Bukit.Engine/SiteEngine.cs`
  - `src/Bukit.Engine/PageRenderDispatcher.cs`
  - `src/Bukit.Content/Notion/NotionContentProvider.cs`
- [ ] 输出架构结论：
  - 不是“简单脚本式项目”。
  - 已具备产品化小型平台雏形。
  - 主要风险来自编排组件持续膨胀，而非基础分层缺失。

### Task 2: 完成工程治理成熟度审计

**Files:**
- Read: `e:\Github\Bukit\Directory.Build.props`
- Read: `e:\Github\Bukit\src\Bukit.Cli\Bukit.Cli.csproj`
- Read: `e:\Github\Bukit\guide\dev\perf-aot-governance.md`
- Read: `e:\Github\Bukit\scripts\perf-baseline.sh`
- Read: `e:\Github\Bukit\tests\Bukit.Engine.Tests\PageRenderDispatcherLazyBodyTests.cs`

- [ ] 确认是否具备全局质量门：
  - `TreatWarningsAsErrors`
  - `EnforceCodeStyleInBuild`
  - 最新分析器级别
- [ ] 确认是否具备可持续性能治理：
  - AOT 构建与告警策略
  - 性能基线脚本
  - smoke 验证链路
- [ ] 给出治理结论：
  - 工程治理成熟度高于普通 side project。
  - 已进入“可维护的小型产品工程”层级。

### Task 3: 完成性能热点审计

**Files:**
- Read: `e:\Github\Bukit\src\Bukit.Engine\PageRenderDispatcher.cs`
- Read: `e:\Github\Bukit\src\Bukit.Engine\Incremental\IncrementalBuildEngine.cs`
- Read: `e:\Github\Bukit\src\Bukit.Content\Notion\NotionContentProvider.cs`
- Read: `e:\Github\Bukit\src\Bukit.Content\Media\ContentImageRewritePipeline.cs`
- Read: `e:\Github\Bukit\src\Bukit.Engine\Plugins\BuiltIn\PaginationPlugin.cs`
- Read: `e:\Github\Bukit\src\Bukit.Engine\Plugins\BuiltIn\ArchivePlugin.cs`
- Read: `e:\Github\Bukit\src\Bukit.Engine\Plugins\BuiltIn\TaxonomyPlugin.cs`

- [ ] 标记 P0 热点：
  - 列表与集合的重复 `Where/OrderBy/ToList`
  - 增量判定时过早读取正文 HTML
  - Notion relation / block children 路径可能演化为大量远程调用
- [ ] 标记 P1 热点：
  - 特殊列表页仍按顺序渲染
  - HTML 图片重写做多轮 regex pass
  - 模板目录 hash 与媒体复制在大项目下形成固定成本
- [ ] 输出性能结论：
  - 性能并非“无治理”，而是“已有并发、缓存、增量，但算法与请求路径仍有优化空间”。

### Task 4: 形成优先级优化路线图

**Files:**
- Modify later: `e:\Github\Bukit\src\Bukit.Engine\MetricsWriter.cs`
- Modify later: `e:\Github\Bukit\src\Bukit.Engine\PageRenderDispatcher.cs`
- Modify later: `e:\Github\Bukit\src\Bukit.Engine\Incremental\IncrementalBuildEngine.cs`
- Modify later: `e:\Github\Bukit\src\Bukit.Content\Notion\NotionContentProvider.cs`
- Modify later: `e:\Github\Bukit\src\Bukit.Content\Media\ContentImageRewritePipeline.cs`
- Modify later: `e:\Github\Bukit\src\Bukit.Engine\Plugins\BuiltIn\PaginationPlugin.cs`
- Modify later: `e:\Github\Bukit\src\Bukit.Engine\Plugins\BuiltIn\ArchivePlugin.cs`
- Modify later: `e:\Github\Bukit\src\Bukit.Engine\Plugins\BuiltIn\TaxonomyPlugin.cs`

- [ ] 第一优先级：补细粒度 metrics
  - 目标：把“页面渲染慢”拆成正文 hash、列表装配、Notion resolve、block 渲染、图片重写等子指标。
- [ ] 第二优先级：压低增量判定成本
  - 目标：减少 skip 判定触发的正文读取。
  - 方法：拆分轻量 fingerprint 与重量正文 hash。
- [ ] 第三优先级：重构列表路径
  - 目标：减少重复排序、全量物化与无意义正文装载。
- [ ] 第四优先级：优化 Notion 请求拓扑
  - 目标：减少 N 次 relation resolve 与深层 block 拉取成本。
- [ ] 第五优先级：收尾优化
  - 目标：并行特殊列表、减少 HTML 多轮扫描、优化模板/媒体固定成本。
- [ ] 第六优先级：结构降复杂度
  - 目标：避免 `PageRenderDispatcher` 和 `NotionContentProvider` 演变为新的 God Class。

### Task 5: 输出最终审计结论

**Files:**
- Reference: `e:\Github\Bukit\.trae\documents\架构与性能审计计划.md`
- Reference: `e:\Github\Bukit\docs\superpowers\plans\2026-05-04-architecture-performance-audit.md`

- [ ] 给出项目水平判断：
  - 架构：中上
  - 工程治理：较强
  - 性能治理：中上，但热点仍集中在大列表、远程请求、增量判定成本
- [ ] 给出不建议事项：
  - 不建议先做大规模重写
  - 不建议在缺乏 metrics 的情况下直接重构所有性能路径
- [ ] 给出建议执行顺序：
  1. 增加 metrics
  2. 优化增量 hash
  3. 优化列表路径
  4. 优化 Notion 请求
  5. 再做架构拆分

## 验证方式

- 代码验证：
  - 对照 `guide/dev/architecture.md` 与核心实现，确认文档和代码边界一致。
- 工程验证：
  - 对照 `Directory.Build.props`、`Bukit.Cli.csproj`、`perf-baseline.sh`、测试工程结构确认治理成熟度。
- 风险验证：
  - 用“影响面 + 发生概率 + 收益”对每个热点排序，确保优先级合理。
- 执行前置：
  - 如果进入实现阶段，先为 metrics 建立基线，再做热点优化。
