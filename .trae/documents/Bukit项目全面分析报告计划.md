# Bukit 项目全面分析报告计划

> **执行说明**：本计划用于产出一份面向维护者的“项目全面分析报告”，重点覆盖架构与风险，并补充代码、文档、测试、交付治理之间的不一致问题。

## Summary

- 目标：对 `Bukit` 仓库做一次维护者视角的全面审计，输出结构化报告，包含当前状态、主要问题、影响范围、根因判断、解决方案和优先级建议。
- 目标受众：维护者。
- 分析重点：架构与风险优先，其次覆盖文档一致性、测试与交付治理。
- 交付形式：最终在对话中提交正式报告；如执行过程中需要沉淀中间结论，可临时整理为工作笔记，但不修改业务源码。

## Current State Analysis

### 1. 已确认的仓库边界

- 当前有效解决方案是 `bukit.slnx`，仅包含 `Bukit.Cli`、`Bukit.Config`、`Bukit.Content`、`Bukit.Engine`、`Bukit.Rendering`、`Bukit.Routing`、`Bukit.Shared`、测试工程和示例插件。
- `src/` 目录下不存在 `AIBuilding` 子目录，当前仓库主线是 `Bukit` 静态站点生成器，而不是 “Bukit + AIBuilding 双产品线”。
- 当前仓库未发现 `.github/workflows/` 目录；但多个文档把 `pages.yml`、`smoke.yml`、`build.yaml` 描述为仓库内已存在文件。

### 2. 已确认的主链路与入口

- CLI 入口：`src/Bukit.Cli/Program.cs`
- 构建编排入口：`src/Bukit.Engine/SiteEngine.cs`
- 内容提供器装配：`src/Bukit.Engine/ContentProviderFactory.cs`
- 内容抽象：`src/Bukit.Engine.Abstractions/ContentItem.cs`、`src/Bukit.Engine.Abstractions/ContentBody.cs`
- 文档总览：`README.md`、`guide/dev/README.md`
- 架构类文档：`guide/dev/architecture-review.md`、`guide/dev/code-wiki.md`、`guide/dev/maintainer-entrypoints.md`

### 3. 已识别的高价值核验点

- 文档是否系统性引用了当前仓库不存在的模块、解决方案或工作流。
- 现有架构评审文档中的结论，是否仍与当前源码一致。
- `Bukit` 当前的内容模型、路由/渲染/插件边界、AOT 约束与文档说法是否匹配。
- 测试与交付路径是否存在“文档有说明但仓库缺失资产”的治理问题。

### 4. 已发现的明确信号

- `README.md`、`README.zh-CN.md`、`guide/dev/publish-deploy.md`、`guide/user/13-部署-GitHub-Pages.md`、`guide/dev/code-wiki.md` 等文件均引用 `.github/workflows/pages.yml` 或其他工作流文件，但仓库内未发现对应目录。
- `README.zh-CN.md`、`guide/dev/code-wiki.md`、`guide/dev/maintainer-entrypoints.md`、`guide/dev/new-developer-30min.md` 等文件反复引用 `src/AIBuilding/*` 和 `aibuilding.slnx`，但当前仓库中不存在这些路径。
- `guide/dev/architecture-review.md` 中“正文 HTML 早期全量入内存”的判断，需要和当前源码复核；从 `ContentItem.cs`、`ContentProviderFactory.cs`、`MarkdownFolderProvider.cs`、`NotionContentProvider.cs` 可见，当前主链更接近 `BodyKey + BodyStore` 的延迟正文读取模型，而不是简单的全量常驻内存。

## Proposed Changes

### 1. 建立“真实仓库边界”基线

**文件**
- `README.md`
- `README.zh-CN.md`
- `bukit.slnx`
- `src/`
- `tests/`

**做什么**
- 确认当前仓库实际包含的产品范围、工程边界、解决方案组成和测试矩阵。

**为什么**
- 全面分析必须先区分“当前真实存在的系统”与“历史文档中残留的系统”，否则报告会把过期模块当成现状。

**怎么做**
- 以 `bukit.slnx` 和 `src/` 目录为准建立活动模块清单。
- 对比 `README*` 中的产品叙述，标出“当前代码存在 / 仅文档存在 / 缺少资产支撑”三类内容。

### 2. 做一次源码优先的架构核对

**文件**
- `src/Bukit.Cli/Program.cs`
- `src/Bukit.Engine/SiteEngine.cs`
- `src/Bukit.Engine/ContentProviderFactory.cs`
- `src/Bukit.Engine.Abstractions/ContentItem.cs`
- `src/Bukit.Content/Markdown/MarkdownFolderProvider.cs`
- `src/Bukit.Content/Notion/NotionContentProvider.cs`
- `src/Bukit.Engine/Plugins/*`
- `src/Bukit.Routing/RouteGenerator.cs`

**做什么**
- 复原当前真实数据流：`CLI -> Config -> Content -> Routing -> Rendering -> Engine -> Plugins -> Output`。
- 重点核对内容正文加载模型、插件边界、AOT 兼容边界、路由与增量构建的现实约束。

**为什么**
- 报告需要区分“真实结构风险”和“历史文档观点”，避免沿用过期判断。

**怎么做**
- 以入口文件和抽象类型为主线梳理调用关系。
- 对“正文延迟加载”“AOT 下插件能力”“配置驱动边界”做证据化说明。
- 若发现文档结论已过时，则在最终报告中单列为“文档误导风险”，而非源码缺陷。

### 3. 做文档与治理一致性审计

**文件**
- `guide/dev/architecture-review.md`
- `guide/dev/code-wiki.md`
- `guide/dev/maintainer-entrypoints.md`
- `guide/dev/new-developer-30min.md`
- `guide/dev/publish-deploy.md`
- `guide/user/13-部署-GitHub-Pages.md`

**做什么**
- 识别架构文档、维护文档、交付文档中与当前仓库现实不一致的部分。

**为什么**
- 对维护者来说，文档失真会直接降低定位效率，并在交付、培训、接手和排障时放大风险。

**怎么做**
- 逐项比对文档中的路径、解决方案名、工作流名、模块名是否真实存在。
- 对每个偏差给出严重度分类：
  - 高：会误导构建、发布、上手路径
  - 中：会误导架构判断或维护入口
  - 低：属于描述不精确但不直接阻断执行

### 4. 做测试与交付可信度审计

**文件**
- `tests/Bukit.Cli.Tests/*`
- `tests/Bukit.Content.Tests/*`
- `tests/Bukit.Engine.Tests/*`
- `tests/Bukit.Rendering.Tests/*`
- `scripts/smoke.ps1`
- `scripts/smoke.sh`
- `global.json`
- `Directory.Packages.props`

**做什么**
- 评估当前测试矩阵是否覆盖主链路，并核对仓库是否保有与文档描述相匹配的交付资产。

**为什么**
- “有测试项目”不等于“交付链可靠”；对维护者更重要的是：测试覆盖了什么、没有覆盖什么、CI 资产是否真的存在。

**怎么做**
- 从测试项目命名和重点测试文件切入，整理“已覆盖主能力 / 覆盖薄弱区 / 需要实测确认”的三段式结论。
- 将缺失的 `.github/workflows/*` 资产归类为交付治理问题，而不是运行时代码问题。

### 5. 输出正式报告

**报告结构**
- 项目现状摘要
- 架构真实性结论
- 主要问题清单（按严重度排序）
- 每个问题的证据、影响、根因、修复方案
- 优先级路线图（短期 / 中期）
- 可保持不动的设计亮点

**问题分类原则**
- 代码结构风险
- 文档失真风险
- 测试与验证风险
- 交付治理风险

**输出要求**
- 只写有证据支撑的问题。
- 明确区分：
  - “源码本身有缺陷”
  - “文档落后于源码”
  - “仓库资产缺失导致治理风险”

## Assumptions & Decisions

- 受众固定为“维护者”，因此报告优先关注结构合理性、事实一致性、长期维护成本和风险优先级。
- 分析重点固定为“架构 + 风险”，但不会忽略测试与交付治理，因为这两项已出现明确失真信号。
- 计划执行时将以“源码与仓库实际存在的文件”为最高事实来源；现有文档只作为待核验材料，不默认视为正确。
- 本次任务的成功标准不是提出很多泛泛建议，而是输出一份可让维护者据此排优先级的报告。
- 若执行阶段发现某个结论无法仅靠静态阅读确认，再用构建/测试命令补充证据。

## Verification Steps

### 1. 只读核验

- 复查以下路径是否真实存在：
  - `.github/workflows/`
  - `src/AIBuilding/`
  - `aibuilding.slnx`
- 复查以下主线文件是否支持报告中的架构判断：
  - `src/Bukit.Cli/Program.cs`
  - `src/Bukit.Engine/SiteEngine.cs`
  - `src/Bukit.Engine/ContentProviderFactory.cs`
  - `src/Bukit.Engine.Abstractions/ContentItem.cs`

### 2. 运行时验证（执行阶段）

- `dotnet build bukit.slnx -c Release`
  - 目的：确认当前有效解决方案可构建，验证文档中的主入口与工程边界判断。
- `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release`
- `dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release`
- `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release`
- `dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release`
  - 目的：确认测试矩阵可运行，并为“测试可信度”结论提供依据。
- `dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml`
- `dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean`
  - 目的：确认文档所述典型主链路仍可跑通，并识别是否还有“说明存在但资产缺失”的问题。

### 3. 报告完成判定

- 报告必须包含至少以下内容：
  - 当前真实仓库范围
  - 已确认问题列表
  - 每个问题对应的证据路径
  - 解决方案与优先级
- 报告必须明确指出哪些问题属于：
  - 源码风险
  - 文档失真
  - 交付治理缺口
