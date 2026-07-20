# Bukit Core G-03 外部消费者证据与 2.0 公共面收窄准备审计

日期：2026-07-20

源码基线：`main@d9a3d650`

审计对象：G-01/G-02 清单中 142 个 `2.0-candidate` 导出 CLR 类型；Core、Labs、官方插件、测试、活动文档、发布资产和可公开检索的外部消费信号

前置清单：[bukit-core-public-api-inventory-2026-07-20.json](bukit-core-public-api-inventory-2026-07-20.json)

## 1. 执行结论

**G-03 的结论是“具备分批准备条件，但不具备批量收窄授权”。** 当前不应把 142 个候选一次性改成 `internal`，也不应把“没有找到公开消费者”解释为“外部消费者为零”。

- 语义审计加载当前 40 个活动源码/测试项目，142/142 个候选均成功绑定，workspace diagnostics 和 compilation errors 均为 0。140 个候选在仓库内有语义引用，116 个被测试直接引用；真正零引用的只有 `RouteInventoryInspectEntry` 和 `TemplateRendererBase`。
- 2 个候选存在此前词法清单漏报的跨 Core 程序集生产引用：`ThemeManifestException` 和 `ThemeTokensProcessor` 均由 `Bukit.Engine` 消费，不能按当前 `implementation-public / 2.0-candidate` 结论直接收窄。
- 29 个候选被非候选 public/protected 签名直接暴露。其中 4 个 `BuildResult` 组成记录同时承载冻结的 `build-report.v1` 形状；其余 25 个必须先迁移 facade、构造器、返回值或接口边界，不能单独 internalize。
- 17 个候选拥有 48 个 protected members。排除已归入其他高优先级组的 2 个后，仍有 15 个必须按派生类扩展面处理；零仓库引用的 `TemplateRendererBase` 也不能因此直接删除。
- 未发现候选类型被完整类型名反射、`Activator.CreateInstance`、`JsonSerializable`、`JsonDerivedType`、`DataContract`、`KnownType` 或 `YamlMember` 直接注册。这个阴性结果降低了仓库内隐式调用风险，但不能覆盖私有仓库、未公开程序集或运行时字符串拼接。
- 当前正式分发是 Native AOT CLI archive，不存在 Core NuGet SDK package、`dotnet pack` 发布链或第三方 CLR SDK 安装文档。公开仓库在审计快照中显示 0 stars、0 forks、7 issues、6 releases；这说明公开采用信号很弱，不等于没有私有消费者。
- GitHub 未登录 code search 不能提供穷尽式结果，GitHub REST API 又因匿名额度耗尽返回 403。本次外部消费者证据置信度只能评为**中低**，不足以授权破坏性变更。

142 个候选的互斥处置结果如下：

| 处置 | 数量 | 当前判定 |
|---|---:|---|
| 冻结契约分类纠正 | 4 | `build-report.v1` 组成记录；先纠正治理元数据，不收窄 |
| 跨程序集分类纠正 | 2 | Engine 真实消费 Theme 类型；先纠正语义分类，不收窄 |
| public 签名重构优先 | 25 | 被稳定/非候选 public surface 暴露；先提供替代边界 |
| protected 扩展面暂缓 | 15 | 需要消费者声明、弃用窗口和继承树迁移设计 |
| 插件持久化报告契约复核 | 2 | 有手写 JSON 持久化，但缺少独立 schema/版本政策 |
| 单类型试点候选 | 1 | `RouteInventoryInspectEntry`；仅可在 2.0 分支和通知后试点 |
| 仓库内可迁移、外部证据待补 | 93 | 没有当前生产图阻断，但尚无破坏性变更授权 |
| **合计** | **142** | 完整对账 |

因此，下一步不应直接启动“142 类型收窄”。推荐先做 **G-04A 治理分类纠正**，再建立消费者声明窗口，最后才在 2.0 分支对一个零引用、无 protected surface 的类型做可回滚试点。

## 2. 审计边界与判定规则

### 2.1 本次包含

1. 复核 142 个 `compatibility = 2.0-candidate` 类型；不把 CLI 可执行程序集的 40 个 `not-a-clr-contract` 类型重新混入候选池。
2. 使用 Roslyn symbol binding 检查 Core、Labs、官方插件和测试消费者，并区分同程序集、跨 Core 程序集和测试引用。
3. 检查 public/protected 签名暴露、继承面、反射、静态序列化、手写 JSON、AOT 发布和 `InternalsVisibleTo` 迁移条件。
4. 检查仓库当前分发方式、NuGet 可发现性、GitHub 公开采用信号和公开代码检索能力。
5. 为每一类候选给出 2.0 前置条件、风险和建议批次。

### 2.2 本次不包含

- 不修改任何访问级别、公共 API、schema、插件协议、序列化格式、项目引用或运行时行为。
- 不把 Labs 或官方插件内部实现扩展成新的 Core 承诺。
- 不访问私有仓库、用户本地源码、私有包源或组织内部遥测。
- 不发布消费者征集 issue、release note 或 deprecation notice；这些会改变外部状态，须独立批准。
- 不执行 2.0 收窄代码试点；本报告只定义是否已准备及其安全顺序。

### 2.3 证据等级

| 等级 | 含义 |
|---|---|
| 已确认阻断 | 当前源码语义图、公开签名或冻结 schema 已直接证明不能独立收窄 |
| 仓库内可迁移 | 当前活动仓库没有生产图阻断，但测试、同程序集实现或批次依赖仍需迁移 |
| 外部证据待补 | 没有发现公开消费者，但检索并不穷尽，不能推出不存在消费者 |
| 试点候选 | 仓库内零引用、无 protected/serializer/contract 证据；仍须走 2.0、通知和 AOT 验证 |

## 3. 可复现语义证据

本次建立了临时 Roslyn/MSBuild 审计器，加载全部活动 `src/` 和 `tests/` 项目；临时工具和输出未进入仓库。审计器对每个 `SimpleNameSyntax` 做 symbol binding，并将构造器调用归一到其 containing type，避免仅依赖文本或 `SymbolFinder` 时漏掉 `new Type(...)`。

| 指标 | 结果 |
|---|---:|
| 活动项目 | 40 |
| 候选类型 | 142 |
| 成功解析 | 142 |
| workspace diagnostics | 0 |
| compilation errors | 0 |
| 仓库内有引用 | 140 |
| 仓库内零引用 | 2 |
| 有测试引用 | 116 |
| 有跨 Core 程序集生产引用 | 2 |
| 有任意 public/protected 签名暴露 | 53 |
| 被非候选 public/protected 签名暴露 | 29 |

完整 restore 后连续两次运行，移除 `generatedAtUtc` 再按 key 排序的 SHA-256 均为：

```text
7b843414a0a037d6cc179851c813030d39f23c7f8d1daa4f7fbbc34085476904
```

这证明本次统计在同一源码和项目图下确定性一致。它不把临时审计器升级为正式门禁；如果未来要让 semantic graph 持续阻断 drift，应另立治理任务，将工具、输入和 golden 结果纳入代码审查。

## 4. 已确认的分类偏差

### G03-F01 高：2 个 Theme 类型被错误标为“无跨项目消费者”

`ThemeManifestException` 在 [ThemeBootstrapper.cs](../../src/Bukit-Core/Bukit.Engine/ThemeBootstrapper.cs) 和 [ThemePathResolver.cs](../../src/Bukit-Core/Bukit.Engine/ThemePathResolver.cs) 中被 Engine 捕获；`ThemeTokensProcessor` 在 [AssetPipeline.cs](../../src/Bukit-Core/Bukit.Engine/AssetPipeline.cs) 中被 Engine 调用。Roslyn 结果分别确认 2 次和 1 次跨程序集生产引用。

**根因：** G-01 使用项目引用约束下的词法扫描；类型定义嵌在大文件、简单名引用和所有权项目之间的组合没有形成可靠 symbol identity，导致 `coreSourceReferences = 0`。

**影响：** 如果根据原清单直接 internalize，`Bukit.Engine` 将无法编译；如果用 `InternalsVisibleTo` 临时绕过，又会把真实模块协作隐藏成非正式友元关系。

**处置：** G-04A 只纠正清单元数据，将两者标为 `cross-assembly-implementation / 1.x-do-not-narrow`。未来要收窄，先将异常映射和 token 输出职责迁移到明确 facade；本次不改代码。

### G03-F02 高：4 个 build report 组成记录不是真正的普通 2.0 候选

[BuildResult.cs](../../src/Bukit-Core/Bukit.Engine/BuildResult.cs) 的以下记录是 public `BuildResult` 的属性/构造参数：

- `BuildEnvironmentInfo`
- `BuildProjectInfo`
- `BuildSummary`
- `BuildIncrementalSummary`

[BuildReporter.cs](../../src/Bukit-Core/Bukit.Engine/BuildReporter.cs) 又逐字段将其中前四组数据写入 `.bukit/build-report.json`；[1.0 契约矩阵](../bukit-1.0-contract-matrix.zh-CN.md)明确把 `.bukit/build-report.json` 标为 `GA-locked`、schema 冻结，[build-report.v1 schema](../schemas/build-report.v1.schema.json)是当前权威形状。

**根因：** G-01 将“公开 JSON schema”和“public CLR”作为不同兼容轴是正确的，但逐类型分类时只将 `BuildResult` 视为非候选，没有把其 public component records 和手写 writer 的字段映射传播到同一 contract family。

**影响：** 单独收窄任一组成记录会破坏 `BuildResult` 的 public signature；重塑记录还可能让 writer、测试和冻结 schema 漂移。

`BuildVariantSummary` 也由 public `BuildResult.Variants` 暴露，但当前 `build-report.v1` writer/schema 不写 `variants`；它属于下一节的 public 签名重构组，不能误标成冻结 JSON shape。

**处置：** G-04A 将上述 4 个类型移出普通候选池，至少标为 `serialized-contract / 1.x-shape-stable` 或新增更准确的“公开报告 CLR mirror”分类。2.0 若要重新设计，必须先版本化 report contract 或引入内部 report DTO，再保留兼容 writer；不得在 access-level 批次顺带处理。

### G03-F03 中：2 个插件执行报告类型存在未治理的持久化形状

`PluginExecutionReport` 和 `PluginExecutionResponseSummary` 由 [PluginExecutionReporter.cs](../../src/Bukit-Core/Bukit.PluginHost/PluginExecutionReporter.cs)手写到 `.bukit/reports/plugin-executions/*.json`，并有 masking/shape 测试。当前未找到对应独立 schema、`schemaVersion` 或明确迁移政策。

**影响：** 把它们当普通实现类型可能忽略工具或用户对报告 JSON 的依赖；反过来直接宣布 GA schema，又会无证据地扩大 1.x 承诺。

**处置：** 暂缓收窄。先由独立任务决定该报告是“受支持诊断工件”还是“best-effort 内部日志”；若公开，补 schema/version；若内部，补文档声明和兼容性边界。`PluginExecutionReporter` 本身还暴露于 public constructor，必须与其调用 facade 一起处理。

## 5. public 签名与继承面

### 5.1 25 个必须先重构边界的候选

以下类型被非候选 public/protected surface 暴露，不能单独改为 `internal`：

| 程序集 | 类型 |
|---|---|
| `Bukit.Cli.Shared` | `CliParseResult` |
| `Bukit.Engine` | `BuildOptions`、`BuildVariantSummary`、`ContentPipelineResult`、`ContentValidationIssue`、`IContentProviderFactory`、`IContentStage`、`ListPageContentResolution`、`TemplateCapabilityFlags`、`TemplateFieldDeclaration`、`TemplateVariableWarning` |
| `Bukit.PluginHost` | `IPluginProcessInvoker`、`IPluginRequestIdFactory`、`IProcessRunner`、`PluginExecutionReporter`、`PluginFileSystemPermissionEvaluator`、`PluginProcessRequest`、`PluginProcessResult`、`PluginRuntimeOnlyContext`、`ProcessRunRequest`、`ProcessRunResult` |
| `Bukit.Routing` | `RouteGenerator.RouteGenerationResult` |
| `Bukit.Shared` | `NotionBlock` |
| `Bukit.Theme` | `SchemaValidationError`、`ThemeDoctorCommand.DoctorResult` |

这里的“重构优先”不是要求立即拆程序集。最小策略是先确定真正受支持的入口，然后把参数、返回值和接口实现收口到 facade 或 contracts owner；只有稳定入口不再暴露候选类型时，才可在后续 breaking batch 调整访问级别。

### 5.2 15 个 protected 扩展面暂缓项

排除已由 public signature 阻断的 `CliParseResult` 和 `NotionBlock` 后，仍有：

- CLI：`SimpleParseResult`、`SubcommandParseResult`、`CliErrorRenderer.CliErrorPayload`；
- Engine：`TemplateRendererBase`；
- Shared Notion block：`BulletedListItemBlock`、`CalloutBlock`、`CodeBlock`、`Heading1Block`、`Heading2Block`、`Heading3Block`、`ImageBlock`、`NumberedListItemBlock`、`ParagraphBlock`、`QuoteBlock`、`ToggleBlock`。

其中多数 protected members 来自 record 继承/生成形状，但 `TemplateRendererBase` 明确公开抽象和 virtual/protected 扩展点。它虽然在当前仓库零引用，源码 XML 文档却描述了替换模板引擎的派生路径。因此它必须经历外部消费者征集、弃用说明和替代扩展点设计，不能作为首个“零引用即删除”样本。

## 6. 反射、序列化与 Native AOT 复核

### 6.1 反射

对活动 Core 和测试源码扫描以下模式，并与 142 个候选类型 identity 交叉：

```text
Assembly.GetType / Type.GetType / GetNestedType / GetExportedTypes
Activator.CreateInstance / GetProperty / GetMethod
```

未发现以候选完整类型名反射构造或定位的生产代码。测试中唯一相关命中是对 `BuiltInPluginSource.GetPlugins()` 返回对象调用 `GetType().Name`，它不依赖候选类型名完成发现或实例化。

### 6.2 静态与手写序列化

候选类型上未发现 `JsonSerializable`、`JsonDerivedType`、`DataContract`、`KnownType`、`YamlMember` 或同类静态注册属性。已确认的隐式契约来自手写 writer，而非 attributes：

- 4 个 Build records → `build-report.v1`，已冻结；
- 2 个 PluginHost records → 插件 execution report，契约等级待定。

因此“没有 serializer attribute”不能作为收窄依据。未来门禁应同时检查 source-generated context、手写 `Utf8JsonWriter`、schema 和 golden fixtures。

### 6.3 Native AOT

当前 [release workflow](../../.github/workflows/release.yaml)通过 [package-native-aot.sh](../../scripts/build/package-native-aot.sh)只发布 `linux-x64`、`osx-arm64` 和 `win-x64` Native AOT CLI archive。访问级别变化本身通常不会改变编译期可解析的静态调用，但 facade/serializer/反射替代方案可能触发 trimming 或 source-generation 差异。

任何实际 2.0 收窄批次都必须至少包含：

1. Core、Labs、官方插件 Release 编译；
2. 相关 serializer round-trip 和报告 schema/golden 测试；
3. 至少一个真实 RID 的 Native AOT publish 与 packaged smoke；
4. public API drift baseline 的 deliberate approval；
5. 独立只读 aggregate diff 复审。

G-03 只做审计，不以运行完整 release gate 代替未来变更批次的验收。

## 7. 外部消费者与分发证据

检索日期：2026-07-20。所有结论只代表检索快照。

### 7.1 已确认事实

- [GitHub 仓库](https://github.com/ALi365-SDN-BHD/Bukit)是公开仓库；页面快照显示 0 stars、0 forks、7 issues。
- [GitHub Releases](https://github.com/ALi365-SDN-BHD/Bukit/releases)页面显示 6 个 release；最新为 [v1.0.10-rc.1](https://github.com/ALi365-SDN-BHD/Bukit/releases/tag/v1.0.10-rc.1)，资产是三个 RID 的 CLI archive、checksums 和 manifest，没有 `.nupkg`。
- 活动 `.csproj` 未发现 `PackageId`/`GeneratePackageOnBuild`，活动 workflow/scripts 未发现 Core `dotnet pack` 或 NuGet publish 链。
- NuGet 官方 flat-container 对 `bukit`、`bukit.engine`、`bukit.content`、`bukit.pluginhost`、`bukit.shared`、`bukit.theme`、`bukit.cli.shared` 返回未找到；按 owner 和 Bukit 前缀的 NuGet 搜索也没有发现相应 Core SDK package。
- 公开 Web 精确搜索没有发现 142 个候选类型的站外代码引用。

### 7.2 不能据此推出的结论

- 0 forks/stars 不能证明没有直接 clone、vendor、私有 fork 或内部程序集引用。
- 没有 NuGet package 不能证明用户没有从源码项目引用或复制 DLL。
- GitHub code search 未登录时要求登录，不能当作完整全网代码索引；匿名 REST API 在本次审计中因 rate limit 返回 403。
- release 页面没有提供本次可验证的逐资产下载计数，因此本报告不编造 adoption/download 数字。

外部消费者证据综合置信度为**中低**。这足以支持“没有正式 CLR SDK 分发路径”和“公开采用信号弱”，不足以支持“可无通知删除 public surface”。

## 8. 按程序集的准备度

| 程序集 | 候选 | 分类纠正 | public 重构 | protected 暂缓 | 报告复核 | 单类型试点 | 外部证据待补 |
|---|---:|---:|---:|---:|---:|---:|---:|
| `Bukit.Cli.Shared` | 5 | 0 | 1 | 3 | 0 | 0 | 1 |
| `Bukit.Content` | 35 | 0 | 0 | 0 | 0 | 0 | 35 |
| `Bukit.Engine` | 61 | 4 | 10 | 1 | 0 | 1 | 45 |
| `Bukit.PluginHost` | 16 | 0 | 10 | 0 | 2 | 0 | 4 |
| `Bukit.Rendering` | 2 | 0 | 0 | 0 | 0 | 0 | 2 |
| `Bukit.Routing` | 1 | 0 | 1 | 0 | 0 | 0 | 0 |
| `Bukit.Shared` | 17 | 0 | 1 | 11 | 0 | 0 | 5 |
| `Bukit.Theme` | 5 | 2 | 2 | 0 | 0 | 0 | 1 |
| **合计** | **142** | **6** | **25** | **15** | **2** | **1** | **93** |

说明：表中“外部证据待补”只表示当前仓库生产语义图没有直接阻断，不等于已证明安全。Content 的 23 个 Notion block renderer、registry、context 和 transformer 应作为一个 owner batch 设计，不能逐类型机械收窄；Shared Notion hierarchy 也必须按继承树整体处理。

## 9. 测试可见性与迁移成本

116 个候选被测试直接引用。当前存在的友元关系主要是：

- `Bukit.Engine` → `Bukit.Engine.Tests`；
- `Bukit.Content` → `Bukit.Content.Tests`、`Bukit.Engine.Tests`；
- `Bukit.Rendering` → `Bukit.Rendering.Tests`、`Bukit.Engine`；
- `Bukit.Shared` → `Bukit.Shared.Tests`、`Bukit.Content.Tests`、`Bukit.Content`、`Bukit.Engine`。

`Bukit.Theme`、`Bukit.PluginHost`、`Bukit.Cli.Shared` 和 `Bukit.Routing` 没有对应测试友元声明。未来 internalize 时不能为了让测试继续编译而无条件扩大 `InternalsVisibleTo`：

1. 行为可以经正式入口验证的，测试应迁移到入口；
2. 确需白盒验证的，友元只授予对应测试程序集；
3. 跨生产程序集不能用测试友元策略掩盖 boundary 设计；
4. 每批必须对比测试覆盖没有因访问级别调整而退化。

## 10. 推荐实施顺序

### G-04A：治理分类纠正，不改访问级别

只处理 6 个已确认误分类类型：4 个映射 `build-report.v1` 的 Build records 和 2 个 Theme 类型。同步机器清单、报告说明和 drift baseline 的分类元数据；不改 schema、代码可见性或序列化 writer。

验收条件：142 候选池与新分类重新对账；semantic graph 重跑；现有 public API baseline 无二进制变化；docs/architecture gate 通过；独立只读复审确认没有把“报告 shape”误写成“全部 CLR SDK”。

### G-04B：外部消费者声明与弃用准备

在实际 breaking change 前建立可公开追溯的候选清单、反馈渠道、迁移目标版本和至少一个完整发布周期的声明窗口；使用已认证 GitHub code search/API 复查精确类型名和仓库依赖。私有消费者只能通过自愿声明或组织内资产盘点补证，不能由公开数据推断。

该步骤涉及外部 issue/release note 时必须另行批准，不应在审计任务中自动发布。

### G-04C：单类型 2.0 试点

首选 `Bukit.Engine.RouteInventoryInspectEntry`：当前仓库语义零引用、无 protected members、无 serializer/reflection/活动文档命中，其定义也没有被 `RouteInventoryValidator` 使用。仍仅限 2.0 分支；在消费者窗口结束前不删除 1.x public surface。

试点验收：先增加能够证明其确实无运行时职责的架构测试，再变更 access/removal；运行 targeted gate、public API deliberate approval、Core+Labs+官方插件编译、真实 Native AOT publish/package smoke和独立 aggregate review。若任何外部消费者出现，改为 obsolete/deprecation 路线。

### G-04D：按 owner 批次处理剩余类型

建议顺序：

1. Content Notion renderer cluster；
2. PluginHost process helper/facade；
3. CLI parse/result hierarchy；
4. Shared Notion inheritance cluster；
5. Engine pipeline/helper cluster；
6. Theme/Routing public result facade。

每批只处理一个 owner 和一个兼容目标。禁止把 schema、插件 wire protocol、asset URL、配置默认值或路径工具顺带纳入 API 收窄提交。

## 11. 重构判定

G-03 不支持整体重构，也不支持一次性 assembly 合并。当前没有依赖循环、AOT 平台阻断或无法验证的核心链路；问题集中在“实现 public 被误读为 SDK”和“候选分类缺少语义/持久化传播”。这些都可以渐进处理。

推荐决策为：

> **1.x 保持二进制可见性稳定；2.0 先纠正治理事实，再通过消费者窗口、facade 迁移和单类型试点逐批收窄。没有外部证据不等于获得删除授权。**

停止条件：如果消费者声明窗口仍没有可验证采用证据，且维护收益不足以覆盖迁移/AOT/测试成本，则保留现有可见性或冻结为内部稳定引擎，不为“减少 public 数量”本身制造破坏性变更。

## 12. 验证记录与剩余限制

| 检查 | 结果 |
|---|---|
| G-03 worktree 基线 | `main@d9a3d650`，独立分支 |
| Core public API drift check | 通过；Release build 0 warning / 0 error |
| Architecture tests | 81/81 通过 |
| Roslyn all-project load | 40 projects；0 diagnostics；0 compilation errors |
| Candidate resolution | 142/142 |
| Semantic result determinism | 两次 normalized SHA-256 一致 |
| Reflection/static serializer candidate scan | 未发现候选 identity 的直接生产注册 |
| Public distribution audit | Native AOT CLI archives；未发现 Core NuGet SDK 发布链 |
| External code search | 非穷尽；GitHub 登录/API rate limit 构成证据限制 |
| `post-change-targeted.sh` | 非沙箱完整通过；docs、链接、public API drift、Release build、现行 fast contracts 均通过 |
| Runtime/API/schema/protocol change | 0；本任务只新增本报告 |

首次沙箱运行在 `brainstorm server self-test` 的 `mv-1 left a live spawned server` 处失败；同一自检在非沙箱环境完整通过，随后完整 `post-change-targeted.sh` 也通过。该差异来自沙箱对子进程身份/存活状态的观察限制，不是 G-03 报告引起的产品回归，也没有为消除环境症状而修改测试或运行时代码。

上述检查只能证明报告和当前仓库一致，不能消除私有外部消费者的不确定性。
