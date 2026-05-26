---
alwaysApply: false
description: 
---
# Bukit Project Rules

## Lint & TypeCheck

```bash
dotnet build bukit.slnx -c Release -warnaserror
```

## Test Commands

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release
```

## Format Checks

```bash
dotnet format bukit.slnx --verify-no-changes
```

## Conventions

- All CLI commands go in `src/Bukit.Cli/Commands/` with namespace `Bukit.Cli.Commands`
- New commands must be registered in both `Program.cs` (fallback switch) and `BukitCliSpecs.cs` (spec-based registry)
- Theme/template scaffolding uses `ThemeTemplateResource.Get("Name")` for template loading
- Wizard presets defined in `WizardPresets.cs` as `public static readonly WizardPreset` fields
- Agent skills in `src/skills/<skill-name>/SKILL.md` — 18 skills covering all Bukit subsystems:
  - Gateway: `using-bukit` routes to sub-skills via Skill tool
  - Reference: `bukit-cli-reference` (CLI single source of truth), `bukit-notion` (Notion content)
  - Technique: `bukit-config`, `bukit-templating`, `bukit-routing`, `bukit-plugins-debug`, `bukit-seo`, `bukit-geo`, `bukit-content-to-template`
  - Pattern: `bukit-theme`, `bukit-design-tokens`, `bukit-i18n`
  - Operation: `bukit-deploy`, `bukit-clone`, `bukit-preview`, `bukit-dev`, `bukit-webhook`
  - CLI operations: always load `bukit-cli-reference` first
  - See `src/skills/skills-index.yaml` for complete skill catalog with triggers, dependencies, and platform loading instructions
- User docs in `guide/user/` — three languages: `.md` (EN), `.zh-CN.md` (CN), `.ms.md` (MS)
- Developer docs in `guide/dev/` — maintainer-facing contracts and implementation reference
- No TODO/FIXME/HACK comments in production code

## Engineering Principles

任何新增功能、修复 bug、重构或文档以外的代码变更，无论作者是人还是 AI，**必须**同时满足以下四项原则。违反任一项的 PR 评审应直接打回。

### 1. 面向对象编程（OOP / SOLID）

- **接口先行**：凡新增**有副作用**的服务类（IO / 网络 / 渲染 / 配置加载 / 进程交互）必须先定义 `I*` 接口，再写实现。参照已有 [IContentProvider](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Content/IContentProvider.cs)、[ITemplateRenderer](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ITemplateRenderer.cs)、[IOutputFileSystem](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Output/IOutputFileSystem.cs)。
- **构造函数注入**：依赖必须通过构造函数传入，禁止在业务类内部 `new` 出有副作用的依赖（值对象、纯函数 helper 除外）。参照 [SiteEngine](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/SiteEngine.cs#L16-L41) 的 DI 模式。
- **单一职责**：单类不得同时承担"采集 + 处理 + 输出"两种以上职责。出现复合职责时立即拆分到独立类（参考 Taxonomy 一族从 God Class 拆分为 `TaxonomyDataWriter` / `TaxonomyHierarchyBuilder` / `TaxonomyPageCreator` 等）。
- **不可变模型**：跨阶段传递的数据模型（如 `ContentItem`、`RouteInfo`）应保持只读 / `record`，禁止在管线中途修改。

### 2. 高内聚低耦合

- **依赖矩阵**：`Bukit.Cli` → `Bukit.Engine` → `Bukit.Engine.Abstractions` / `Content` / `Routing` / `Rendering` / `Theme` / `Shared`。`*.Abstractions` 不得引用其它生产工程，`plugins/*` 只能依赖 `*.Abstractions`。
- **单文件 ≤ 600 行**：超过 600 行的 `.cs` 文件必须拆分。`SiteEngine.cs`（约 409 行）是上限参考。
- **命名空间与目录一致**：命名空间必须匹配目录结构（Roslyn `IDE0130` 视为 error）。
- **`InternalsVisibleTo` 白名单**：仅允许暴露给同名 `*.Tests` 测试工程，禁止暴露给其它生产工程。
- **横切关注点集中**：`Logger`、`SlugHelper`、`UrlRedactor`、`DiagnosticCode` 等沉淀到 [Bukit.Shared](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Shared)，不得在业务工程内重复实现。
- **慎引第三方包**：新增 NuGet 依赖须在 PR 描述中给出"为什么不能内置实现"的说明，避免被绑架。

### 3. 敏捷开发

- **Spec 先行**：≥ 200 行变更或新增公共 API 的改动必须先建 `.trae/specs/<change-id>/{spec.md,tasks.md,checklist.md}` 三件套，PR 描述里通过 `Spec:` 字段引用该路径。
- **小 PR**：单个 PR diff ≤ 400 行。超过需在 PR 描述写 `Reason: oversized because ...`。
- **可追溯**：PR 描述必须含 `Closes #<issue>` 或 `Spec:` 字段二选一。
- **主干常绿**：`main` 分支必须始终通过 `bash scripts/quality-gate.sh`；不允许"先合再修"。
- **需求澄清优于猜测**：模糊需求必须先用 `brainstorming` skill 或 `AskUserQuestion` 收敛，禁止"我猜用户想要…"直接动手。
- **文档同步**：用户可见行为变化必须同步更新 `guide/user/` 三语文档；维护者契约变化必须同步更新 `guide/dev/`。

### 4. TDD（测试驱动开发）

- **Red → Green → Refactor 三步不可省**：
  1. **Red**：先在 `tests/<Project>.Tests/` 下写一个失败的 xUnit 测试，运行 `dotnet test --filter FullyQualifiedName~XxxTests` 确认失败。
  2. **Green**：写最小实现让该测试通过。
  3. **Refactor**：在测试保护下重构与命名优化，所有测试保持绿色。
- **Bug 必先复现**：任何 bugfix 的首个 commit 必须是能稳定复现 bug 的失败测试，commit message 形如 `test: reproduce <bug-id>`。
- **覆盖率门禁**：[scripts/quality-gate.sh](file:///Users/ali/mydev/Git/Github/Bukit/scripts/quality-gate.sh) 强制行覆盖率 ≥ 80%（环境变量 `COVERAGE_THRESHOLD` 可临时上调）。**禁止**通过 `[ExcludeFromCodeCoverage]` 绕开门禁。
- **测试命名**：`MethodUnderTest_Should<Behavior>_When<Condition>`，便于回看 TDD 红绿循环的意图。
- **测试一一对应**：每个生产类 `Foo.cs` 至少要有对应的 `FooTests.cs`，边界场景可拆 `FooExtendedTests.cs` / `FooEdgeCasesTests.cs`。
- **可测性是设计要求**：若某段逻辑"难以测试"，先重构使其可测，禁止以"难测"为由免测。

### 5. Definition of Done（任务完成的硬门槛）

任何 PR 在打开评审前，作者必须确认：

- [ ] 已遵循 Red → Green → Refactor，或在 PR 模板勾选 `⚪ N/A — 本 PR 不涉及代码逻辑`
- [ ] 本地已通过 `bash scripts/quality-gate.sh`（build + test + 覆盖率 ≥ 80% + format + doc check + smoke）
- [ ] 新增有副作用的服务类已暴露为 `I*` 接口并通过构造函数注入
- [ ] 改动文件均 ≤ 600 行；命名空间匹配目录
- [ ] 跨工程引用未越过依赖矩阵
- [ ] ≥ 200 行变更已有对应 `.trae/specs/<change-id>/` 三件套
- [ ] 用户/开发者文档已同步更新（如涉及）
- [ ] PR 描述含 `Closes #` 或 `Spec:` 链接，PR 模板的 TDD 与质量门禁勾选项已逐项确认

### 6. AI 协作硬指令

当 AI 协作者（Trae / Claude / Codex / Copilot 等）处理本仓库时，**必须**：

1. 在动手前读取 [src/skills/AGENTS.md](file:///Users/ali/mydev/Git/Github/Bukit/src/skills/AGENTS.md) 与本规则文件。
2. 对 ≥ 200 行变更走 `/spec` 流程，禁止跳过 spec 直接写实现。
3. 触碰 `.cs` 文件时先执行 `test-driven-development` skill 流程（Red → Green → Refactor）。
4. 在声称"完成"前必须实际运行 `bash scripts/quality-gate.sh` 并查看输出，禁止凭直觉断言成功（参考 `verification-before-completion` skill）。
5. 不允许为通过门禁而调低 `COVERAGE_THRESHOLD` 或临时移除测试。如需调整门禁，必须单独开 spec 走评审。
