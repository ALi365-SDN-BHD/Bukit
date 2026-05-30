# SafeUrl + Task.WhenAny 残余修复 + 测试命名修正 Spec

## Why

审计报告中识别出 3 个残余问题：SafeUrl 作为跨模块安全基础组件却标记为 `internal`（虽已通过 `InternalsVisibleTo` 编译通过，但 `public` 更清晰）；两个 CI 插件测试仍使用 `if (completed == buildTask)` 虚假通过模式；`RunAsync_JobsFour_RunsSuccessfully` 命名与实际行为不符。

## What Changes

- **P0**: `SafeUrl` 从 `internal` 改为 `public`（保持 `InternalsVisibleTo` 对 `Bukit.Content` 不变，不影响其他 internal 类型）
- **P1**: `RunAsync_CIEnvWithAllowExternalPlugins_BuildSucceeds` 和 `RunAsync_NonCIEnv_ExternalPluginsWorkNormally` 的 `if (completed == buildTask)` 改为 `Assert.Same(buildTask, completed)` 守卫
- **P2**: `RunAsync_JobsFour_RunsSuccessfully` 重命名为 `RunAsync_JobsFour_StartsBuildWithoutArgumentError`

## Impact

- Affected specs: 无（为 audit-hardening-round2 的补充）
- Affected code:
  - `src/Bukit.Shared/SafeUrl.cs` — `internal` → `public`
  - `tests/Bukit.Cli.Tests/BuildCommandTests.cs` — 2 个测试的 Task.WhenAny 守卫模式 + 1 个测试重命名

---

## MODIFIED Requirements

### Requirement: SafeUrl public visibility (P0)
SafeUrl 类 SHALL 定义为 `public static class` 而非 `internal static class`，作为跨模块 URL 安全校验基础组件供所有消费者直接使用。

#### Scenario: Bukit.Content 直接访问
- **WHEN** `Bukit.Content` 中的 `NotionRichTextRenderer`、`AudioBlockRenderer` 等调用 `SafeUrl.ForLink()` / `SafeUrl.ForMedia()` / `SafeUrl.ForEmbed()` / `SafeUrl.IsExternal()`
- **THEN** 通过 `public` 修饰符直接访问，无需依赖 `InternalsVisibleTo`

#### Scenario: Bukit.Shared.Tests 直接访问
- **WHEN** `SafeUrlTests` 测试 SafeUrl 各方法
- **THEN** 通过 `public` 修饰符直接访问

#### Scenario: InternalsVisibleTo 保留
- **WHEN** SafeUrl 改为 `public`
- **THEN** `InternalsVisibleTo("Bukit.Content")` 保留不变（其他 internal 类型仍需该声明）
- **AND** 架构测试 `InternalsVisibleTo_MustOnlyExposeTo_TestOrSiblingAssemblies` 继续通过

---

### Requirement: Task.WhenAny false-green elimination (P1)
所有使用 `Task.WhenAny` 与 `CancellationTokenSource` 超时组合的测试 SHALL 使用 `Assert.Same(buildTask, completed)` 守卫模式，而非 `if (completed == buildTask)` 模式。

#### Scenario: CI 环境插件测试使用 Assert.Same 守卫
- **GIVEN** `RunAsync_CIEnvWithAllowExternalPlugins_BuildSucceeds`
- **WHEN** `buildTask` 未在超时前完成（即 timeout task 先返回）
- **THEN** `Assert.Same(buildTask, completed)` 明确失败
- **AND** 不存在 "if 条件跳过断言体导致假通过" 的情况

#### Scenario: 非 CI 环境插件测试使用 Assert.Same 守卫
- **GIVEN** `RunAsync_NonCIEnv_ExternalPluginsWorkNormally`
- **WHEN** `buildTask` 未在超时前完成
- **THEN** `Assert.Same(buildTask, completed)` 明确失败

#### Scenario: 修复后与已修复测试模式一致
- **GIVEN** `RunAsync_JobsFour_RunsSuccessfully`（已使用 Assert.Same 守卫）
- **WHEN** 新增两个插件测试改为 Assert.Same 模式
- **THEN** 三个测试使用统一的 Task.WhenAny 守卫模式

---

### Requirement: Test naming reflects actual behavior (P2)
测试方法名 SHALL 准确反映其实际验证的断言和行为。

#### Scenario: JobsFour 测试重命名
- **GIVEN** `RunAsync_JobsFour_RunsSuccessfully` 实际行为是吞咽 ConfigException/ContentException
- **AND** 测试不验证 build 真正成功（不检查 exit code）
- **WHEN** 重命名为 `RunAsync_JobsFour_StartsBuildWithoutArgumentError`
- **THEN** 方法名准确反映测试实际验证的内容："启动构建不因参数错误而阻塞"
