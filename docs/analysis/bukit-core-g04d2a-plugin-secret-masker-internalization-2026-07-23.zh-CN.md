# Bukit Core G-04D2A `PluginSecretMasker` 单类型 internalization 决策账本

日期：2026-07-23
基线：本地 `2.0@764f0eddd242ed67eb88c1e323910d2cf55ea1c3`
范围：只收窄 `Bukit.PluginHost.PluginSecretMasker` 的 CLR 可见性

## 决策

G-04D2A 只批准把
`Bukit.PluginHost.PluginSecretMasker` 从 public 收窄为 internal。唯一生产
代码变化是把：

```csharp
public static class PluginSecretMasker
```

改为：

```csharp
internal static class PluginSecretMasker
```

这是一个访问修饰符 token 的变化。类型身份、文件位置、四个方法的访问
修饰符与方法体、secret-key fragments、替换顺序和比较模式均保持不变。

## Breaking boundary 与支持面

该变化仅用于 2.0。任何未披露且直接引用此 CLR helper 的 consumer 都会
遇到 source/binary breaking change。无需提供 replacement API，因为支持的
外部 plugin surface 是 `bukit-plugin-v1` process protocol，而不是这个只由
`Bukit.PluginHost` 同 assembly 调用的 helper。

入口 characterization 继续从
`PluginExecutionReporter.WriteAsync(PluginExecutionReport,
CancellationToken)` 与 report JSON 边界验证：环境 secret 出现在 URL 文本
时仍被替换为 `***`，secret 原值不进入 JSON，公开环境值保持可见。此任务
不修改 report 字段、JSON shape、路径或协议。

## Consumer 与 Native AOT 证据边界

关闭的 136-entry candidate manifest 保留该类型的历史记录：

- declaration status：`consumer-declaration-pending`；
- private status：`unknown-until-voluntary-declaration`；
- public-search status：`no-public-match-found`；
- Git blob：`7b07d6890562387010b52301e9f8716e9bf10ed1`。

公开搜索无匹配不证明 private、unindexed 或 undisclosed consumer 不存在。
本任务只提供 assembly、baseline、Reporter 行为与 focused gate 证据；
真实 `osx-arm64` Native AOT package、release-artifact smoke、process-plugin
report path、parent aggregate 与最终只读复审由 parent controller 在任务
提交后执行，本文不预先声明其结果。

## TDD 证据

characterization GREEN（生产代码未改）：

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter FullyQualifiedName~PluginLockAndReportTests
```

结果：4 passed / 0 failed / 0 skipped。

assembly/baseline RED：

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter \
  "FullyQualifiedName~PluginHostAssembly_KeepsMaskerInternalAndDoesNotExportIt|FullyQualifiedName~CurrentBaseline_ContainsFourteenAssemblies508TypesAnd104Candidates"
```

结果：2 failed / 0 passed / 0 skipped。预期失败为
`Assert.False()` 的 Actual `True`（类型仍 public），以及
`Assert.Equal()` 的 Expected `508` / Actual `509`（旧 baseline）。

任务 GREEN 命令：

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off

dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release --nologo --verbosity minimal --tl:off \
  --filter \
  "FullyQualifiedName~G04D2APluginSecretMaskerInternalizationTests|FullyQualifiedName~G04D1CM2AtomicRemovalTests|FullyQualifiedName~G04D1BBlockRendererFacadeRemovalTests|FullyQualifiedName~G04D1AStaticNotionFacadeRemovalTests|FullyQualifiedName~G04CPublicSurfacePilotTests|FullyQualifiedName~PluginBoundaryTests"
```

Public API owner 与 focused gate 命令按实施计划原样执行；实际 exit、计数与
状态以最新 Task 1 handoff/report 为准。

## Baseline delta

生成并审阅的新 snapshot 为 14 assemblies / 508 exported types / 104
`2.0-candidate` entries。相对基线只删除一个类型记录：

- `Bukit.PluginHost.PluginSecretMasker`；
- `MaskEnvironment`；
- `MaskText`；
- `MaskValue`。

没有其他 type/member 或 governance metadata 变化。关闭的 candidate
manifest 未改动。

## 明确排除

- 不做通用 URL query、userinfo 或 fragment 清洗；
- 不改变 masking 行为或降低任何 masking assertion；
- 不改变其他 `Bukit.PluginHost` 类型或 candidate；
- 不改变 `PluginHostErrorCodes`；
- 不增加 `InternalsVisibleTo`、facade、factory、adapter 或 replacement API；
- 不改变 `bukit-plugin-v1`、schema、report、配置、权限、执行、timeout、
  output limit、CLI text、CI、release 或 gate；
- 不修改 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/`。

## Stop conditions

发现以下任一情况即停止而不是扩大范围：

- snapshot 不是精确 14 / 508 / 104；
- baseline 除目标类型及其三个成员外还有 drift；
- closed manifest blob 不再是
  `7b07d6890562387010b52301e9f8716e9bf10ed1`；
- Reporter masking characterization 失败；
- assembly 不再保留该 internal 类型，或类型仍被导出；
- 需要修改协议、schema、report、其他 PluginHost 类型、friendship、CI、
  release 或 gate 才能通过。

最终 aggregate、Native AOT、release smoke、process-plugin/report proof 和
独立 review 状态只以最新 task handoff 为准；本账本不预先声明通过。
