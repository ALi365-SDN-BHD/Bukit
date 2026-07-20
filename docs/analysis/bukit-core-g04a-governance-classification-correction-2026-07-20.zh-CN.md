# Bukit Core G-04A 治理分类语义纠正与闭环记录

日期：2026-07-20

范围：仅更正 6 个既有导出 CLR 类型的治理元数据；不更改 CLR 可见性、签名、源码、schema、writer、插件协议、运行时行为或生成的序列化代码。

## 纠正项目

| 类型 | 纠正前 | 纠正后 | 证据 |
|---|---|---|---|
| `Bukit.Engine.BuildEnvironmentInfo`、`BuildIncrementalSummary`、`BuildProjectInfo`、`BuildSummary` | `implementation-public`；`2.0-candidate`；`2.0-review` | `serialized-contract`；`1.x-shape-stable`；`retain-1.x` | `BuildResult.cs` 暴露记录；`BuildReporter.cs` 映射字段；`build-report.v1` schema 与 1.0 契约矩阵冻结报告 shape。 |
| `Bukit.Theme.ThemeManifestException` | `implementation-public`；`2.0-candidate` | `cross-assembly-implementation`；`1.x-do-not-narrow` | Roslyn semantic analysis 确认 Engine 生产代码在 `ThemeBootstrapper.cs` 与 `ThemePathResolver.cs` 引用 owning `Bukit.Theme` 类型（`coreSourceReferences=2`）。 |
| `Bukit.Theme.ThemeTokensProcessor` | `implementation-public`；`2.0-candidate` | `cross-assembly-implementation`；`1.x-do-not-narrow` | Roslyn semantic analysis 确认 Engine `AssetPipeline.cs` 引用 owning `Bukit.Theme` 类型（`coreSourceReferences=1`）。 |

其余变化字段的完整对照如下：

- 4 个 build report CLR mirror：`contractSurface` 从 `module implementation CLR surface` 改为 `build-report.v1 public report CLR mirror`；`evidenceConfidence` 从 `medium` 改为 `high`；`classificationBasis` 从 `no formal external CLR support evidence and no proven cross-project non-test consumer; same-project use may exist and absence is not removal proof` 改为 `type is exposed by BuildResult and mapped by BuildReporter into the frozen build-report.v1 schema`；`riskFlags` 从 `["no-proven-cross-project-non-test-consumer"]` 改为空数组，`consumerEvidence` 不变。
- 2 个 Theme 类型：`contractSurface` 从 `module implementation CLR surface` 改为 `repository-internal CLR collaboration`；`evidenceConfidence` 从 `medium` 改为 `high`；`classificationBasis` 从 `no formal external CLR support evidence and no proven cross-project non-test consumer; same-project use may exist and absence is not removal proof` 改为 `Roslyn semantic analysis confirms Bukit.Engine production references to the owning Bukit.Theme type`；`riskFlags` 从 `["no-proven-cross-project-non-test-consumer"]` 改为空数组。`ThemeManifestException.consumerEvidence.coreSourceReferences` 从 0 改为 2，`ThemeTokensProcessor.consumerEvidence.coreSourceReferences` 从 0 改为 1，其他 `consumerEvidence` 字段不变。

基线仅同步分类、兼容性和迁移窗口，保留 owner、signature、publicMembers 与 protectedMembers 原字节内容。

## 汇总对账

| 指标 | 纠正前 | 纠正后 |
|---|---:|---:|
| `serialized-contract` | 88 | 92 |
| `cross-assembly-implementation` | 170 | 172 |
| `implementation-public` | 182 | 176 |
| `1.x-shape-stable` | 111 | 115 |
| `1.x-do-not-narrow` | 173 | 175 |
| `2.0-candidate` | 142 | 136 |
| 无已证实跨项目非测试消费者 | 182 | 176 |

不变量保持：12 个程序集、472 个 exported types、3,898 个 public members、52 个 protected members。`Bukit.Engine` 汇总为跨程序集 16、实现层 57、内部持久化 3、序列化形状 4；`Bukit.Theme` 汇总为 AOT 2、跨程序集 11、实现层 3、内部持久化 3、序列化形状 15。

## 排除范围

- `BuildVariantSummary` 虽由 `BuildResult` 暴露，但不写入 `build-report.v1`，不在本次范围。
- G-03 报告是发现记录，其 142-candidate 总体为纠正前输入，未修改。
- 未修改 `src/`、`tests/`、`docs/schemas/`、插件协议或任何 backup/reference 目录。

## 验证结果

| 检查 | 实际结果 |
|---|---|
| JSON parse、6 项精确断言、记录范围与汇总重算 | 通过；仅 6 个目标 inventory/baseline 记录变化；summary、assemblies 和 12/472/3,898/52 不变量一致。 |
| `bash scripts/checks/public-api-drift-self-test.sh` | 通过（exit 0）。 |
| `bash scripts/checks/public-api-drift.sh check Release` | 通过（exit 0）。 |
| `dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore` | 通过（81 passed；0 failed；0 skipped）。 |
| `bash scripts/checks/post-change-targeted.sh -- <four changed paths>` | 通过（exit 0）。 |
| `git diff --check` | 通过（exit 0）。 |
