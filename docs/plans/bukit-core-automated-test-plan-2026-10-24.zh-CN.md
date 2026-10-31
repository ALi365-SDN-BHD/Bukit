# Bukit Core 自动化测试计划书

> 日期：2026-10-24
> 状态：草案，第 9 节待决事项已全部决定
> 范围：`src/Bukit-Core`（18 个项目）；`src/Bukit-Plugins` 与 `src/Bukit-Labs` 后置，不在本期
> 前提：按"从零开始"盘点测试需求；复用现有测试项目、辅助代码与 CI 入口，不以已有用例数量代替行为验证

## 1. 目的与背景

Bukit 是 .NET 10 Native AOT 静态站点引擎，当前处于企业内部优先的稳定运行模式（见 `docs/governance/bukit-core-product-positioning.md`）。核心承诺是**确定性的可信内容发布**：同一输入得到同一输出，失败不破坏已发布产物，输出符合声明的 schema。

本计划书定义：测试什么、按什么顺序测、怎样判断完成，以及哪些行为需要先由负责人拍板才能写测试。

## 2. 测试对象

| 层 | 项目 | 代码规模（约） |
|---|---|---|
| 基础 | Bukit.Shared | 1.7k 行 |
| 配置 | Bukit.Config | 6.8k 行 |
| 领域 | Bukit.Engine.Abstractions、Bukit.Content、Bukit.Content.Notion、Bukit.Notion | 13k 行 |
| 路由、主题、渲染 | Bukit.Routing、Bukit.Theme、Bukit.Rendering | 6.9k 行 |
| 引擎 | Bukit.Engine | 41.8k 行 |
| CLI | Bukit.Cli、Bukit.Cli.Shared | 17.1k 行 |
| 插件宿主 | Bukit.Plugin.Abstractions、Bukit.PluginHost | 4.2k 行 |

不在范围内：Bukit-Plugins、Bukit-Labs、BukitJalil、远程主题源与主题市场（Core 明确不支持）、第三方外部插件。

## 3. 质量目标

| 目标 | 度量 |
|---|---|
| 确定性 | 同一输入、固定平台与工具链下连续构建两次，公共产物的路径集合与文件字节一致；报告按第 4.3 节独立校验 |
| 失败安全 | 提交前任一阶段失败，正式输出与托管缓存保持不变 |
| 增量正确 | 对相同最终输入，增量与全量构建的公共产物一致；缓存统计等运行信息分别验证，不要求相等 |
| 路径安全 | 任何内容、配置、别名输入不能让写入落到输出根之外 |
| 契约稳定 | 按每份报告声明的 schema ID 匹配 `docs/schemas` 中的本地文件，校验当前 Core 报告清单中的所有版本，包括 Analytics v2 |
| 覆盖率 | 统一采用第 9 节决定 1 的阈值；项目与安全层的统计方式见第 4.4 节 |

## 4. 测试策略

### 4.1 层次与比例（粗估）

| 类型 | 占比 | 说明 |
|---|---|---|
| 单元 | 约 65% | 纯函数与单类，无文件系统副作用或只用临时目录 |
| 契约 | 约 15% | JSON schema、序列化、CLI 选项与文档一致性 |
| 集成 | 约 15% | fixture 站点完整构建、Notion 假传输、假外部插件进程 |
| 端到端与架构 | 约 5% | golden 输出树、AOT 发布冒烟、依赖矩阵 |

### 4.2 工具

沿用项目已有栈：xunit、coverlet、NetArchTest.Rules、BenchmarkDotNet。属性式随机测试（slug、路径规范化）可用 xunit 的 `Theory` 加种子化生成器实现，不新增依赖。

18 个 Core 源项目复用现有 13 个 Core 测试项目：`Bukit.Cli` 与 `Bukit.Cli.Shared` 都映射到 `tests/Bukit.Cli.Tests`；`Bukit.Engine` 及其 5 个 `Bukit.Engine.*` 子项目映射到 `tests/Bukit.Engine.Tests`；`Bukit.Content` 由 `tests/Bukit.Content.Tests` 与 `tests/Bukit.Content.Notion.Tests` 覆盖；其余源项目映射到 `tests/<源项目名>.Tests`。不另建 `Bukit.Cli.Shared.Tests` 或 `Bukit.Content.Notion` 源项目；架构专项使用现有 `tests/Bukit.Architecture.Tests`。CI 的 Core 测试项目入口以 `scripts/checks/coverage/list-core-projects.sh` 为准，每批闭包核对源项目、测试项目及实际执行入口，避免新增测试未被调度。

### 4.3 约定

- 每个测试使用独立临时目录，结束后清理；仓库中的 fixture、schema 和源码只读，测试写入限于沙箱。
- 时间、网络、环境变量、进程启动均通过可替换接缝注入。
- 用 `[Trait("Category", "<分类>")]` 分类：`Unit`、`Contract`、`Integration`、`Slow`、`KnownDefect`。Trait 只作标签，不会自动排除测试。
- 已知缺陷测试先写正确行为并用专项命令复现失败，记录第 6 节编号、输入、预期、实际结果及失败证据；确认后同时标 `KnownDefect` 和 `[Fact(Skip = "KD-编号：原因")]` 或 `[Theory(Skip = "KD-编号：原因")]`。修复时移除 Skip，专项通过后摘除 KnownDefect；跳过不算通过，也不能作为质量目标的绿色证据。平台专项要求实际执行，不能用 Skip 代替平台证明。
- 公共产物快照比较实际输出目录的相对路径集合和文件字节；仅排除根目录及语言目录下的 `.bukit/`、`.bukit-output-marker`、`.bukit-build-state.json`。fixture 固定内容日期等输入，golden 在相同平台与工具链下比较。
- 报告不参与整树字节等价比较，但每次构建都校验 schema、路径清单、计数及哈希与本次实际产物一致。`generatedAt`、`startedAt`、`endedAt`、`durationMs`、缓存命中/未命中、渲染/跳过数及其派生哈希允许随运行变化，分别断言其语义，不能靠删除字段或跳过整份报告让测试通过。
- 失败安全快照覆盖正式输出和全部托管缓存，包括上述元数据；比较失败前后的原始字节，不套用公共产物比较的排除规则。

### 4.4 覆盖率证据

- 所有阶段出口和最终验收只使用第 9 节决定 1 的阈值。整体及单项目统计沿用 `docs/coverage-baselines.json` 与 `scripts/checks/coverage/summarize.py`；`Bukit.Cli.Shared` 仍按独立源项目统计，不与 Cli 合并覆盖率。
- 阶段 0 补充一个读取 Cobertura 的测试辅助检查器，分别统计 `src/Bukit-Core/Bukit.Engine/BuildTransaction.cs`、`OutputDirectoryCleaner.cs`、`AssetOutputPlan.cs` 中各类及其嵌套类型、异步状态机对应的可执行源码行。按规范化文件路径与行号去重，多份报告中任一命中即算覆盖；每个文件独立计算覆盖行数/可执行行数，缺文件、零分母或低于安全层阈值均失败。
- 现有 `summarize.py` 只检查整体与项目下限，不能替代上述安全层检查。阶段 5 出口须附三个文件的覆盖/总行数和检查结果；最终验收还须附完整 Core 项目汇总。专项或完整覆盖率采集的确切命令、过滤条件及工具链在第 7 节执行闭包中固定，完整 Core 覆盖率采集须单独明确授权。

## 5. 阶段计划

阶段按依赖自底向上，同一阶段内按风险排序。每阶段都有出口标准，达标才进入下一阶段。

### 阶段 0：测试基础设施

产物：
- 临时站点沙箱（含符号链接构造）。
- 站点构造器：代码生成 `site.yaml`、Markdown、主题目录。
- 三个最小 fixture 站点：单语 Markdown、多语、含分类法与分页。
- Notion 假传输：复用 `NotionClient` 的 internal `HttpMessageHandler`、时钟和延迟注入接缝，返回分页、429、5xx 响应；请求仍使用 `https://api.notion.com:443`。不把 API 地址改为 localhost，也不放宽目标地址或重定向校验。
- 假外部插件进程：可配置为正常、超时、崩溃、乱码输出、超大输出。
- 断言助手：第 4.3 节的公共产物比较、失败安全快照与报告校验；第 4.4 节的安全层覆盖率检查器及其缺文件、重复行合并、阈值边界自检。
- 当前 Core 报告清单：生成入口、产物路径、声明的 schema ID、本地 schema 文件及对应测试；例如 `analytics-report.json` 对应 `https://bukit.dev/schemas/analytics-report.v2.json` 与 `docs/schemas/analytics-report.v2.schema.json`。按当前生成入口盘点，不用 `*.v1.schema.json` 或全部历史 schema 代替清单。

出口：空站点在沙箱中构建成功，公共产物快照和报告校验通过；报告清单及安全层覆盖率检查器自检完成。

#### 0.1 基础设施实施与证据（2026-10-25）

**阶段 0：SUCCESS。专项、三平台验收及最终增量复核均已通过。**

本节证据根目录 `R` 为 `/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/core-phase0-rNC5Fv`；证据文件名均相对此目录。下表 `Infrastructure/` 相对于 `tests/Bukit.Engine.Tests/`；测试计数均为 Passed / Failed / Skip。此前 Shared 验收与图片迁移提交保留。

| 阶段 0 要求 | 文件 | 已执行证据 |
|---|---|---|
| 独立沙箱、文件/目录链接及 YAML/Markdown/主题构造；空站点与三个最小 fixture | `Infrastructure/SiteSandbox.cs`、`Infrastructure/SiteBuilder.cs`、`TestInfrastructureTests.cs`；复用 `tests/fixtures/{basic-markdown-site,i18n-site,taxonomy-site}/` 的 20 个显式输入 | 四场景各两次真实构建通过；日期固定，多语核对 en/zh-CN，分类法在沙箱设 pageSize=1 并验证两页/顺序/前后链接；`core-engine-green.trx` |
| 公共产物精确路径及字节比较、失败安全原始快照 | `Infrastructure/OutputSnapshot.cs`、`TestInfrastructureTests.cs` | 公共比较仅排除根/配置语言根的 `.bukit/` 和确切 marker/state 文件；嵌套同名项、隐藏文件及公共 manifest 保留。真实阻塞构建失败后 output/全部实际托管缓存原始字节与身份保持不变；失败快照不采用公共排除规则；同一 GREEN TRX |
| 报告独立 schema、路径/计数/长度/哈希及运行字段语义校验 | `Infrastructure/LocalJsonSchema.cs`、`Infrastructure/ReportAssertions.cs`、`TestInfrastructureTests.cs` | 单语 14 / 多语 23 份报告检查集合通过；32 个 schema 用例及独立字段/文件/计数/哈希变异检查，含精确数值边界；四场景每次构建均验报告；同一 GREEN TRX |
| Notion 分页、429、5xx，复用 handler/时钟/延迟接缝 | `tests/Bukit.Notion.Tests/NotionTestTransport.cs`、`NotionTestTransportTests.cs`、`NotionClientTests.cs` | **54/0/0**：新类14、既有类40；`notion-results/notion.trx`。目标仍为 `https://api.notion.com:443`；429 重试，500/503 按现有策略立即失败，302/307 拒绝 |
| 真实假插件进程：正常、超时、崩溃、乱码、超大输出 | `tests/PluginProcessProbe/Program.cs`、`tests/Bukit.PluginHost.Tests/PluginProtocolProbeTests.cs`、`SystemProcessRunnerTests.cs` | **31/0/0**：新类7、既有类24；`plugin-results/plugin.trx`。崩溃用 exit 7，乱码含实际无效 UTF-8；TPR-001 修正后以 `1024 < 2048 < 4096` 判别较小上限，限定复核已关闭 |
| 三文件安全覆盖率检查器 | `scripts/checks/coverage/security-layer.py`、`security-layer-self-test.py`，复用 `summarize.py` | `coverage-green.log`：**11 项通过**；独立检查缺文件/零分母、规范化路径+行号跨报告/嵌套/状态机合并；19/20 通过，1899/1999 不因四舍五入通过；专项复核无 finding |
| 当前生成入口驱动的报告清单 | `docs/testing/core-report-inventory.json`、`scripts/checks/core-report-inventory.py` | `core-inventory-green.log`：**5 自检 + 24 条记录 / 20 本地 schema 通过**；入口、产物路径、声明 ID/版本、本地 schema、测试绑定齐备，Analytics 为 v2 / 2.0 |
| 新平台类接入 CI，配置变更有直接 owner 自检 | `.github/workflows/ci.yaml`、`scripts/checks/test-infrastructure-platform-self-test.py` | `core-ci-green.log`：三 OS 选择、确切命令、helper 顺序、4 个负向变异通过；已选择 `TestInfrastructureTests` 与 `PluginProtocolProbeTests`；原生执行证据见0.4 |

#### 0.2 历史 RED、授权修复与当前 GREEN

本机基准 `c34bf2333db28270bfdd7ec10e0a38c52be45122`；**macOS 15.8.1 / Darwin 24.6.0 / arm64，SDK 10.0.100、runtime 10.0.0、VSTest 18.0.1，Release；Python 3.9.6**。结果据实际 TRX 行与类定义核对。

| 执行阶段 | 结果 | 原始证据 |
|---|---|---|
| 首次基础设施，Core 修复前 | **36/4/0，历史 RED，退出1** | `engine-final.trx`、`engine-final.log`；文件名含 final 不表示阶段最终成功 |
| 加入直接回归后、修复前 | **164/16/0，历史 RED，退出1** | `core-engine-red.trx`、`core-engine-red.log`；16失败为隐私5、根状态清单2、未知日期2、Windows形式查找3、真实场景4 |
| 授权修复后 Engine 组合专项 | **180/0/0，退出0** | `core-engine-green.trx`、`core-engine-green.log`；Infrastructure **43**、BuildReporter **33**、SEO writer **58**、Publish writer **46** |
| CLI 直接消费者专项 | **21/0/0，退出0** | `core-cli-green.trx`、`core-cli-green.log`；既有 nullable reader 的兼容回归，CLI runtime 未改 |

已通过的独立 Core 修复边界：
- **SR-001**：`docs/schemas/security-report.v1.schema.json` 声明并要求 `publicOutputPrivacy`，保留未知字段拒绝；`BuildReporterTests.cs` 验证五种真实状态及缺失/未知/非法枚举负例。
- **SR-002**：`docs/schemas/{seo-report,publish-audit-report}.v1.schema.json` 保留必填 `lastModified`，允许 string/null 并保持 date-time 约束；对应两份 schema 文档、两个 Engine writer 类及 CLI `SeoReportValidatorTests.cs` 均有直接证据。
- **SR-003**：`src/Bukit-Core/Bukit.Engine/BuildReportInventories.cs` 仅排除根内部 `.bukit-build-state.json`；嵌套同名公共文件、已存在 marker、原事务顺序保留；启用/禁用报告两种回归独立重算完成字节的长度、SHA-256、计数及聚合哈希。
- **SSR-001**：`Infrastructure/ReportAssertions.cs` 三个 audit/semantic 查找规范化反斜杠；Infrastructure 三行回归独立到达各查找，仍拒绝缺失文件/改变的语义内容；原生 Windows 证明见0.4。

`core-report-repair-review.md` 已完成唯一预定修复专项复核，**SR-001/002/003、SSR-001 均 reviewed-resolved，Critical 0 / Important 0 / Minor 0**；四个真实场景的两次构建/报告检查及失败安全均通过。此前 coverage/transport 专项证据沿用，TPR-001 已由 `transport-process-scoped-rereview.md` 关闭。

#### 0.3 本机确切复现/收口命令

从仓库根目录运行；`R` 仅缩写已记录的绝对结果路径，shell 展开后前七条的可执行文件、参数、过滤、logger 与路径和原回执相同。第八条用于本次计划及 OpenSpec 状态文档的 owner 检查，日志为 `docs-owner.log`。Engine fixture 独占，其他 .NET 串行；按 closure/classification 执行，匹配的 GREEN 不为刷新证据重复运行。

```sh
R=/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/core-phase0-rNC5Fv

python3 -B scripts/checks/coverage/security-layer-self-test.py

dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --filter "FullyQualifiedName~NotionTestTransportTests|FullyQualifiedName~NotionClientTests" --logger "trx;LogFileName=notion.trx" --results-directory "$R/notion-results"

dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release --filter "FullyQualifiedName~SystemProcessRunnerTests|FullyQualifiedName~PluginProtocolProbeTests" --logger "trx;LogFileName=plugin.trx" --results-directory "$R/plugin-results"

dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~TestInfrastructureTests|FullyQualifiedName~BuildReporterTests|FullyQualifiedName~SeoAuditReportWriterTests|FullyQualifiedName~PublishAuditReportWriterTests" --logger "trx;LogFileName=core-engine.trx" --results-directory "$R/core-engine-results"

dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter FullyQualifiedName~SeoReportValidatorTests --logger "trx;LogFileName=core-cli.trx" --results-directory "$R/core-cli-results"

python3 -B scripts/checks/core-report-inventory.py --self-test

python3 -B scripts/checks/test-infrastructure-platform-self-test.py

git diff --check -- docs/plans/bukit-core-automated-test-plan-2026-10-24.zh-CN.md openspec/changes/fix-core-report-contracts/tasks.md
```

#### 0.4 冻结版本与三平台证据

`snapshot.json`：临时分支 **`evidence/core-phase0-20261025`**，提交 **`c4e09fc07321ed682ae48b3b6b50a1926a5177a4`**；main 保留。冻结 **935 项 sourceInputs**，摘要 **`4ecfa8bbbeac4adc86769500f8b5a445009d89658373b0efa5dbb20ce2e2d6fa`**；临时工作流摘要 `7eaf3a1801fd8891c24f2842b975ddc43224795f37ebafac3c93cab98c3440f3`。

`local-platform-evidence.json` 保存冻结输入、macOS 实际身份及 Engine/Plugin 原始 TRX 身份，冻结时二者 cache HIT；Core 专项复核另核对 Engine/CLI/inventory/CI 四条缓存全部 HIT，包含 HEAD、闭包内容、原命令、环境状态及工具链。本机复用匹配证据，未为补平台记录重跑 fixture。

临时工作流 owner 自检 **24 个 mock 场景 PASS**，回执 `remote-workflow-owner-result.json` / `remote-workflow-self-check.py.log`。随后实际 [Actions run 40138709708](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40138709708) 及两个 native job 均 **success**，见 `remote-run.json`、`platform-acceptance.json`；mock 与原生结果分别归档。

| 平台 / 实际 OS | Infrastructure P/F/Skip / 实际适用数 | PluginProtocolProbe P/F/Skip / 实际适用数 | 执行证据 |
|---|---|---|---|
| macOS 15.8.1 / Darwin 24.6.0 / arm64 | **43/0/0 / 43** | **7/0/0 / 7** | `core-engine-green.trx`、`plugin-results/plugin.trx`、`local-platform-evidence.json` |
| Linux 6.17.0-1022-azure / x86_64 / glibc2.39，.NET x64 | **43/0/0 / 43** | **7/0/0 / 7** | [Linux job 121005666511](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40138709708/job/121005666511)；`remote-artifacts/core-phase0-Linux-x64/` |
| Windows 2025Server-10.0.26100-SP0 / AMD64，.NET x64 | **43/0/0 / 43** | **7/0/0 / 7** | [Windows job 121005666789](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40138709708/job/121005666789)；`remote-artifacts/core-phase0-Windows-x64/` |

三平台均为 **SDK 10.0.100、runtime 10.0.0、VSTest 18.0.1**。两个远端目录各含 `platform.json` 及 `Bukit.Engine.Tests/tests.trx`、`Bukit.PluginHost.Tests/tests.trx`；四条命令退出0。专项复核逐项确认935项输入相同、两个类的完整 `(testName, methodName)` 清单含 theory 参数一致，所有行 Passed，零 Skip/N/A。本机 Engine 180、Plugin 31 是组合总数；其他类未被记作远端已执行。

远端实际使用 `platform.json.commands` 的参数数组；下列为其确切命令展示（Windows 行不是未经转义即可粘贴的 shell 语法）：

```text
/home/runner/work/_temp/core-phase0-dotnet-10.0.100/dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter 'FullyQualifiedName~Bukit.Engine.Tests.TestInfrastructureTests' --logger 'trx;LogFileName=tests.trx' --results-directory /home/runner/work/_temp/core-phase0-evidence/Bukit.Engine.Tests
/home/runner/work/_temp/core-phase0-dotnet-10.0.100/dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release --filter 'FullyQualifiedName~Bukit.PluginHost.Tests.PluginProtocolProbeTests' --logger 'trx;LogFileName=tests.trx' --results-directory /home/runner/work/_temp/core-phase0-evidence/Bukit.PluginHost.Tests
D:\a\_temp\core-phase0-dotnet-10.0.100\dotnet.exe test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter FullyQualifiedName~Bukit.Engine.Tests.TestInfrastructureTests --logger trx;LogFileName=tests.trx --results-directory D:\a\_temp\core-phase0-evidence\Bukit.Engine.Tests
D:\a\_temp\core-phase0-dotnet-10.0.100\dotnet.exe test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release --filter FullyQualifiedName~Bukit.PluginHost.Tests.PluginProtocolProbeTests --logger trx;LogFileName=tests.trx --results-directory D:\a\_temp\core-phase0-evidence\Bukit.PluginHost.Tests
```

平台专项发现并关闭 **PSR-001**：本机后处理 Python 启动曾把远端 `LC_CTYPE=unset` 记录为 set；外部 `record-remote-cache.py` 改为解释器启动后应用状态并断言54项完整状态映射等于原回执。`remote-cache-corrected-receipts.json` 指向四份新 `*-corrected-cache.json`，明确替代但保留原错误元数据记录，其余命令/源码/提交/工具链/结果/时长不变。`platform-cache-scoped-rereview.md` 确认 resolved，**Critical 0 / Important 0 / Minor 0**；这是缓存记账修正，原生测试没有重跑，也不是 Core runtime 缺陷。

#### 0.5 收口与范围

Core GREEN 和预定复核均已接受，`openspec/changes/fix-core-report-contracts/tasks.md` 两个验收项据实际回执勾选。控制器在本次状态修改前已生成 `execution-cache-refinement.json` 及四份 `*-execution-cache.json`，仅排除未参与运行的 OpenSpec tasks 清单；原 test cache/完整闭包及935项冻结快照身份保留。本次两文档修改由独立 owner 检查负责，不把状态修改后的完整闭包冒称旧 cache HIT。

既有上下文继续明示：agent-manifest 本地 schema 缺失，公共字节仍入快照；PluginHost schema 缺少可选 `resourceLimitExceeded` / `networkPermissionGranted`；metrics、search-index-index、ThemeCatalog 保留无 schema 状态，ThemeCatalog 为 public-API-only。清单检查不声称全部24类真实实例 schema 已通过，也不追加本次修复范围。

本次仅阶段0；三个安全层文件实际 **≥95%** 采集在阶段5，整体 **≥84%** / 各源项目 **≥70%** 按最终覆盖率验收；阶段6全报告实例、阶段7连续20次稳定性仍按原阶段执行。未执行全量/发布/整个解决方案门禁，离线 Notion 与 mock owner 证据保持其原有边界。

最终收口：一次生成的 `final-review-scope.json` 与唯一 `final-review.md` 确认阶段出口满足要求，**Critical 0 / Important 0 / Minor 0**，六项历史 finding 均已关闭。状态回填后的两文档 owner 检查见 `docs-final-owner.log`；父任务唯一指标汇总见 `metrics-report.json`。阶段 0 提交由用户在验收后另行授权；临时 evidence 分支保留供追溯。

### 阶段 1：Bukit.Shared

#### 1.1 范围与行为依据

复用 `tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj`（net10.0、xunit、coverlet.collector），测试 `src/Bukit-Core/Bukit.Shared/` 全部有运行行为的文件，包括 `IO/`。`InternalsVisibleTo.cs`、`DisableRuntimeMarshalling.cs` 与接口声明没有独立运行分支；不为声明增加凑数测试。

本批只补现有测试和本节计划。先核对调用关系：文件枚举影响 Engine 静态资源、哈希和报告清单；路径判断影响输出清理、预览服务及 PluginHost；安全文件打开影响 Markdown 加载与目录复制；SSRF/脱敏影响媒体下载、Notion 传输及 CLI 外链审计；布局/短代码影响 Rendering 和 Engine。当前 Core 未发现 `SafeUrl`、`ValueCoercion` 的外部生产调用，仍验证现有 helper 的行为。

必须区分以下契约，避免测试把错误假设固化：

- `SafeUrl` 是输出 URL 的 scheme 白名单，`SsrfGuard` 才是连接时地址约束；允许 HTTP URL 不等于允许连接内网。`IsExternal` 只是相对/外部形式分类，不是 URL 安全验证器。
- `IsPrivateHostAsync` 对 DNS 混合结果保守拒绝；`SsrfSafeConnectAsync` 则跳过私有地址、依序尝试公有地址。连接接缝必须使用已解析的 IP、原端口和取消令牌，不重新按主机名连接。HTTP 重定向处理属于调用方/handler 的集成测试；本批验证连接回调接线与每次连接重新分类，不声称执行了真实重定向链。
- `PathUtils.IsSubPathOf` 不包含父目录自身；`IsSameOrSubPathOf` 包含。对现存链接解析物理目标，对不存在的尾部保留词法路径。枚举跳过链接不等于防止枚举后的路径替换，后者由安全 opener 处理。
- `Slugify` 允许空结果、保留中文、转写部分拉丁字母；它不负责 Windows 保留文件名。`ScribanLayoutDirectiveParser.NormalizePath` 只转换分隔符，不负责拒绝 `..` 或绝对路径。删除原“所有规范化结果都不能含 `..`/空段”的错误属性要求。
- `ValueCoercion` 采用严格大小写白名单，未知值可同时既非真也非假；保留已有混合大小写、自定义 `ToString` 和异常传播测试。

#### 1.2 风险—用例—执行文件矩阵

下表文件均位于 `tests/Bukit.Shared.Tests/`。P0 是安全/数据边界，P1 是确定性及兼容性；每行均包含现有回归用例，新断言采用明确预期值或副作用计数。

| 编号 / 优先级 | 源码与风险 | 必测输入与可观察结果 | 测试文件 / 本批行动 |
|---|---|---|---|
| SH-01 / P0 | `SafeUrl`：scheme 绕过、外部 URL 误当本地路径 | link 允许 HTTP/HTTPS/mailto/tel，media 仅 HTTP/HTTPS，embed 仅 HTTPS；根相对路径保留；null/空白、无效 URI、裸相对路径、协议相对路径拒绝；修剪空白、scheme 大小写；反斜杠 authority 混淆写正确行为并单独复现 | `SafeUrlTests.cs`：补三入口矩阵及外部分类 |
| SH-02 / P0 | `UrlRedactor`：日志凭据泄漏 | userinfo、路径、query、fragment 同时存在时仅保留 scheme/host/非默认端口；IPv6；畸形/相对 URL 固定占位符，绝不回显 token | `UrlRedactorTests.cs`：补组合敏感字段与非法输入 |
| SH-03 / P0 | `SsrfGuard.IsPrivateAddress`：网段边界及 mapped IPv6 绕过 | IPv4 的零地址、私网、CGNAT、环回、链路本地、协议保留、文档、6to4 relay、benchmark、组播/保留段；两端及相邻公有地址；IPv6 的 unspecified、loopback、link/site-local、ULA、multicast、文档地址；IPv4-mapped 私有/公有对照 | `SecurityFuzzingTests.cs`：明确地址表；移除公网域名解析依赖 |
| SH-04 / P0 | DNS 预检与实际连接不同步、重试吞异常 | literal 不调用 DNS；全公有、混合、空解析；Socket/Argument 错误 fail closed，意外异常传播；空/全私有结果零连接；端口/令牌传递；允许的网络异常回退，非网络异常立即传播；取消不继续下一地址；后续调用重新解析以防 rebinding | `SsrfGuardHostTests.cs`、`SsrfGuardConnectTests.cs`：已有 resolver/connector 注入，断言调用次数/顺序及异常身份 |
| SH-05 / P0 | `PathUtils`：父路径前缀碰撞、点段和链接越界 | 同目录两种 API 差别；缺失尾部；`content-other` 不能混入 `content`；`..` 逃逸、尾分隔符、卷根；目录/文件链接指向外部拒绝，父目录别名和内部目标接受；null/空白参数报错；大小写比较按平台 | `PathUtilsTests.cs`：补路径关系；保留 `PlatformPathHelperTests.cs` 平台对照 |
| SH-06 / P0 | `SafeFileEnumerator`：跟随链接、遗漏隐藏输入或静默失败 | 默认递归、`recurse:false`、glob、空/缺失目录；隐藏文件保留；目录链接（含自环）、文件链接、悬空链接都跳过；不能返回外部目标；排序后比较相对路径集合，不依赖枚举顺序 | `PathUtilsTests.cs`：在已接入 CI 的文件系统类中新增枚举测试 |
| SH-07 / P0 | `IO/PlatformSafeSourceFileOpener`、`VerifiedSourceFile`：打开未验证目标或资源泄漏 | 嵌套普通文件成功；缺失文件、目录、父目录外目标及外部目录链接拒绝；枚举后替换为链接拒绝；已存在 final-symlink/FIFO 用例保留；用 A 的句柄/流搭配 B 的路径（不同长度/mtime/内容）模拟过期路径，断言仍读取 A；Dispose 关闭流及句柄且可重复，长度/mtime 访问报 disposed；支持 ABI/flags 和不支持架构 fail closed | `PlatformSafeSourceFileOpenerTests.cs`：补真实临时文件、替换事件、同句柄元数据和生命周期测试 |
| SH-08 / P1 | `SlugHelper`：国际化与重复规范化不稳定 | 精确中文/混合中文输出；预组合与分解重音等价；特殊转写、emoji、标点、分隔符；固定种子输入满足幂等、合法字符、无首尾或连续 `-`；土耳其文化下结果不变 | `SlugHelperTests.cs`：加强弱断言，新增固定种子属性测试 |
| SH-09 / P0 | `ShortcodeProcessor`：HTML 注入、参数错位 | 引用参数、空参数、多行、缺参数、未知名称；HTML 实体解码后编码一次；直接位置参数的引号/尖括号/`&` 编码；替换值不再次解释成另一个占位符 | `ShortcodeProcessorTests.cs`：补编码和单次替换边界 |
| SH-10 / P1 | `ScribanLayoutDirectiveParser`：误删正文、行尾漂移 | 只提取首个非空行；两种定界符、大小写、单双引号；成功时删除指令并统一 LF，失败保留原正文/CRLF；空模板、缺引号/闭合符、空路径；NormalizePath 幂等且仅替换 `\\` | `ScribanLayoutDirectiveParserTests.cs`：补精确 body/path 及失败矩阵 |
| SH-11 / P1 | `ValueCoercion`、诊断码/异常：兼容性与错误上下文丢失 | 已有真/假/未知三态、数值和文化测试；BKT 大写四位十六进制、未知 code 的 fallback；错误类型保留 message/inner/code，异常 formatter 有/无码均正确 | 复用 `ValueCoercionTests.cs`、四个异常测试类、`BuildDiagnosticCodeTests.cs`；`DiagnosticCodeFormatterTests.cs` 补 fallback |
| SH-12 / P1 | `EnvironmentHelper`、`ConsoleLogger`：进程状态污染及日志不可解析 | env 缺失、trim、混合大小写、正整数边界/溢出/非整数回退；Notion token 原样返回；日志等级过滤、stderr、格式归一化、JSON 控制字符往返、ISO 时间戳、trace/span 缺省省略 | `EnvironmentHelperTests.cs`、`ConsoleLoggerTests.cs`：补边界，所有环境/Console/文化修改 finally 恢复 |

#### 1.3 隔离与实施顺序

1. 生成本批机器可读 closure，登记以上测试文件、本计划、完整 Shared 源码/测试、`TestCleanup.cs`、项目/SDK/包设置及覆盖率入口为证据输入。默认 policy 中未映射的计划文件通过仓库外任务 policy 解决；按本批测试-only 闭包执行完整 Shared 专项。public/serialized contract 无改动，消费者只读核查。
2. 先替换依赖公网 DNS 的用例：主机解析通过 resolver 注入，literal 通过不允许被调用的 resolver 验证；不将“fake 总返回失败”冒充路径穿越检测。路径穿越在 SH-05/07 的实际文件系统中验证。
3. 在现有文件中补 SH-01～12。每个文件系统测试独立 GUID 临时目录，使用现有清理方式；链接创建失败直接失败，不能静默 return。随机测试固定种子 `20261024`、512 个有效 Unicode 字符串，失败信息包含种子、序号、输入；不对孤立 surrogate 强加新契约。
4. 先对可疑缺陷运行最小复现，记录正确预期与实际结果；只有确认的运行时缺陷才进入第 6 节并同时标记 KnownDefect/Skip。修复另行授权。其他编译/测试问题就地诊断，完成后执行下面的完整 Shared 专项。
5. coverage 与非插桩平台专项串行执行；同一命令通过后记录 cache。新增测试通过且无新改动时不重复执行。完成一次专项 review，再生成一次 delta-only `review-scope`。

#### 1.4 确切命令与覆盖率口径

从仓库根目录运行；输出目录为本阶段专用，runner 会清理其 Shared 子目录。不要指向其他任务产物。

```bash
# 完整 Shared 专项 + TRX + Cobertura（无测试过滤）
bash scripts/checks/coverage/run-one.sh Release \
  tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj \
  TestResults/coverage/shared-phase1-20261024

# 同 CI 类集合的非插桩平台专项
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~Bukit.Shared.Tests.PlatformPathHelperTests|FullyQualifiedName~Bukit.Shared.Tests.PathUtilsTests|FullyQualifiedName~Bukit.Shared.Tests.PlatformSafeSourceFileOpenerTests' \
  --logger 'trx;LogFileName=tests.trx' \
  --results-directory TestResults/shared-phase1-platform-20261024

git diff --check -- tests/Bukit.Shared.Tests \
  docs/plans/bukit-core-automated-test-plan-2026-10-24.zh-CN.md
```

Shared 项目阈值用现有汇总器的行归并逻辑计算，不调用要求全部 Core 项目齐全的 `summarize.py main`。不新增排除项，不用 Cobertura 包级四舍五入结果作阈值判断。读取 TRX 明确引用的附件：collector 可能同时保留一份相同 GUID 副本，不能当成两次独立证据。下面的行覆盖统计不替代 runner 的 TRX/退出码验收：

```bash
python3 -B - <<'PY'
import json, runpy, xml.etree.ElementTree as ET
from pathlib import Path
repo = Path.cwd().resolve()
helper = runpy.run_path('scripts/checks/coverage/summarize.py')
trx = Path('TestResults/coverage/shared-phase1-20261024/Bukit.Shared.Tests/tests.trx')
root = ET.parse(trx).getroot()
deployment = root.find('{*}TestSettings/{*}Deployment').get('runDeploymentRoot')
refs = root.findall("{*}ResultSummary/{*}CollectorDataEntries/{*}Collector[@uri='datacollector://microsoft/CoverletCodeCoverage/1.0']/{*}UriAttachments/{*}UriAttachment/{*}A")
assert len(refs) == 1, 'expected one TRX coverage attachment'
attachment = trx.parent / deployment / 'In' / refs[0].get('href').replace('\\', '/')
assert attachment.is_file(), 'missing coverage attachment'
copies = list(trx.parent.rglob('coverage.cobertura.xml'))
assert all(p.read_bytes() == attachment.read_bytes() for p in copies), 'unequal coverage copies'
hits = helper['collect'](repo, [attachment])
values = [v for (p, _), v in hits.items() if p.startswith('src/Bukit-Core/Bukit.Shared/')]
assert values, 'missing Shared source coverage'
covered, total = sum(v > 0 for v in values), len(values)
floor = json.loads(Path('docs/coverage-baselines.json').read_text())['minimums']['projectFloor']
print(f'Bukit.Shared: {covered}/{total} = {helper["pct"](covered, total):.2f}% (floor {floor}%)')
assert not helper['below_threshold'](covered, total, floor), 'Shared below project floor'
PY
```

#### 1.5 出口与平台证据

- 完整 Shared 测试零失败；KnownDefect 的 Skip 数量、编号和复现证据单列，不计成功。Shared 源项目行覆盖达到第 9 节 **≥70%**，同时列出未覆盖的高风险分支，不能用总比例替代安全断言。
- 非插桩平台 TRX 中三个类均有实际适用用例；现有 `BUKIT_NOT_APPLICABLE` 只允许 Windows 的 POSIX FIFO/non-Windows comparison，以及非 Windows 的 Windows comparison，单列不计适用用例。
- 本机只证明实际运行的 OS/进程架构/SDK。`.github/workflows/ci.yaml` 的 macOS/Windows 类过滤已包含上述文件系统类；Linux 通过完整 Core coverage 的 Shared 项目入口执行。三平台最终验收须取得同一测试版本的 Linux x64、macOS arm64、Windows x64 证据；未取得者标待验，不为本阶段启动完整 Core/发布门禁。
- 授权范围内执行：Shared 专项、Shared 覆盖率、上述非插桩平台专项与差异 review。记录计划/测试修改、准确命令、TRX、覆盖率、缺陷和剩余平台缺口。

#### 1.6 首次执行记录（2026-10-24，Core 修复前）

**首次状态：PARTIAL。计划细化与测试补充已执行；覆盖率达标，当时阶段安全验收及三平台证据尚未闭环。后续授权修复见 1.7，最终三平台结果与阶段出口见 1.8。**

环境：macOS 15.8 / .NET 进程 arm64，SDK 10.0.100、runtime 10.0.0、VSTest 18.0.1，Release。修改 13 个既有测试文件，新增 47 个测试方法，均在最终 TRX 中被发现（含两个显式 KnownDefect）；无新测试项目或依赖。`ValueCoercion`、异常类型和平台比较复用已有用例；其他矩阵项均已补充并执行。没有执行旧版本覆盖率基线，因此不宣称覆盖率提升幅度。

| 项目 | 实际结果 | 证据 / 判定 |
|---|---|---|
| 新缺陷最小复现（加 Skip 前） | 4 失败：KD-009 的 link/media/embed 三行，KD-010 一行 | `TestResults/shared-phase1-defect-repro-20261024/tests.trx`；失败符合正确预期与现状的差异 |
| 完整 Shared 测试 | 513 个 TRX 条目：511 Passed、2 NotExecuted、0 Failed；Passed 含 1 个明确的平台 N/A | `TestResults/coverage/shared-phase1-20261024/Bukit.Shared.Tests/tests.trx`；两个跳过是 KD-009 theory、KD-010 fact，xunit 跳过 theory 时合为一个条目 |
| Shared 行覆盖 | **607 / 696 = 87.21% ≥ 70%** | TRX 关联 `coverage.cobertura.xml`；按源码路径和行号去重，原始比例判阈值 |
| 覆盖率 runner | **退出 1，不是 GREEN** | `run-one.sh` 接受了 `dotnet test` 的退出 0，随后校验失败：TRX `Counters.notExecuted=0`，明细却有 2 个 `NotExecuted`；保留原始 TRX 和两份字节相同的覆盖率附件 |
| macOS 非插桩平台专项 | `dotnet test` 退出 0：41 Passed、1 Skip；适用用例 40 | `TestResults/shared-phase1-platform-20261024/tests.trx`；PathUtils 18、opener 18、PlatformPathHelper 4；另有 1 个 Windows comparison N/A |
| 平台验收 | **未通过** | KD-010 的 Skip 不满足现有 CI 的“不得跳过适用用例”规则；不能用命令退出 0 充当平台 GREEN，也不修改过滤/allowlist 掩盖缺口 |
| Linux x64 / Windows x64 | 待验 | 本机不能提供这两个平台的实际执行证据 |

最小复现命令（首次运行时两个测试尚未标 Skip；本次修复已在 1.7 移除 Skip）：

```bash
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~UrlPolicies_BackslashAuthorityConfusion_IsRejected|FullyQualifiedName~IsSubPathOf_VolumeRootItself_ReturnsFalse' \
  --logger 'trx;LogFileName=tests.trx' \
  --results-directory TestResults/shared-phase1-defect-repro-20261024
```

当时的缺陷与后续动作（KD-009/KD-010 已在 1.7 闭环）：

1. **KD-009**：三个 URL 入口均返回 `/\attacker.example/payload`，而 JavaScript 标准 URL 解析 `new URL('/\\attacker.example/payload', 'https://site.example/page').href` 得到 `https://attacker.example/payload`（只解析，没有网络请求）。当时保留正确的 null 断言与显式 Skip，等待另行授权 Core 修复。
2. **KD-010**：卷根目录被判断为自己的严格子目录，正确 false 断言已留存。当时 `PathUtilsTests` 的 Skip 会使现有 platform CI 拒绝此版本，必须修复并摘除 Skip 后才能声称平台通过。
3. **验证工具兼容缺口**：`scripts/checks/coverage/run-one.sh` 第 58 行假定 `Counters.notExecuted` 与跳过明细一致，本次 xunit/VSTest 组合不满足。后续独立验证配置任务应以结果行核对跳过、总数、执行数及失败项，并补 runner owner 自检；本批不把该命令写成通过。

专项复核补强了一个 Important 证据缺口：增加句柄 A / 路径 B 的判别用例，并断言 Dispose 后 mtime 访问报错；因此重新执行了完整 Shared 与受影响平台专项，表中使用最终结果，原始运行另存备查。限定复核确认该项已解决。另记录一个非阻塞 Minor：日志时间戳测试的 wall-clock 上下界断言在系统时钟回拨时存在偶发失败可能，本次未发生。

逐文件行覆盖：

| 源文件 | 覆盖行 / 总行 | 行覆盖 |
|---|---|---|
| `DiagnosticCodeFormatter.cs` | 44 / 44 | 100% |
| `DiagnosticExceptionFormatter.cs` | 3 / 3 | 100% |
| `EnvironmentHelper.cs` | 7 / 7 | 100% |
| `Exceptions.cs` | 37 / 39 | 94.87% |
| `IO/PlatformSafeSourceFileOpener.cs` | 150 / 220 | 68.18% |
| `IO/VerifiedSourceFile.cs` | 13 / 13 | 100% |
| `Logger.cs` | 40 / 41 | 97.56% |
| `PathUtils.cs` | 44 / 45 | 97.78% |
| `PlatformPathHelper.cs` | 1 / 1 | 100% |
| `SafeFileEnumerator.cs` | 8 / 8 | 100% |
| `SafeUrl.cs` | 31 / 31 | 100% |
| `ScribanLayoutDirectiveParser.cs` | 45 / 45 | 100% |
| `ShortcodeProcessor.cs` | 35 / 35 | 100% |
| `SlugHelper.cs` | 32 / 32 | 100% |
| `SsrfGuard.cs` | 78 / 93 | 83.87% |
| `UrlRedactor.cs` | 10 / 10 | 100% |
| `ValueCoercion.cs` | 29 / 29 | 100% |

未覆盖的关键范围：Windows `CreateFileW`/最终路径检查、Linux `/proc/self/fd` 与各 OS 原生故障分支、真实 socket 连接与其清理、HTTP 重定向链。本批证明了注入连接逻辑及本机 no-follow/FIFO/链接替换边界；不等于上述跨平台/网络集成证明。修复前 `SafeUrl` 行覆盖 100% 仍存在 KD-009，说明覆盖率不能替代安全用例。

详细日志、closure、缓存状态及逐行覆盖记录保存在本次工作目录外：`/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/shared-phase1-uwb8ppax/`；可通过本节命令重建证据。失败的 wrapper 与未满足语义的平台专项均记录为非 GREEN，不触发重复测试。

#### 1.7 授权 Core 修复记录（2026-10-24）

**KD-009、KD-010 已修复并通过本机专项；本次修复结束时阶段 1 为 PARTIAL，尚缺 Linux x64、Windows x64 的同版本实际执行证据。最新三平台结果见 1.8。** 用户在首次缺陷报告后明确授权独立 Core 修复任务，本节记录该任务，不改变测试计划中其他 Core 修复的授权边界。

- `SafeUrl`：三个入口在允许根相对路径前共用校验，拒绝 `//` 前缀、反斜杠及嵌入控制字符。保留原有 Trim、各入口 scheme 白名单、合法 Unicode 与百分号编码 URL；未新增公开 API。
- `PathUtils.IsSubPathOf`：规范化并解析链接后，使用 `PlatformPathHelper.PathComparison` 排除相等路径，再执行严格子路径前缀判断。卷根自身及 `根/.` 返回 false；`IsSameOrSubPathOf` 的包含相等语义保留。
- 先解除 Skip 获得 RED，再修复至 GREEN，最后摘除两处 `KnownDefect`。新增 1 个 URL theory（12 行）与 3 行合法对照，补充卷根规范化断言。首次阶段 1 的 13 个测试文件改动完整保留。

| 项目 | 最新结果 | 证据 / 判定 |
|---|---|---|
| 修复前两类回归 | 81 Passed、15 Failed | `TestResults/shared-kd-fix-red-20261024/tests.trx`；原四个缺陷数据行及 11 个新增边界失败；另一个绝对 URL 对照原本已被 `Uri` 拒绝 |
| 修复后两类回归 | **96 Passed、0 Failed、0 Skip** | `TestResults/shared-kd-fix-green-20261024/tests.trx` |
| 完整 Shared / coverage runner | **530 Passed、0 Failed、0 Skip；runner 退出 0** | `TestResults/coverage/shared-kd-fix-20261024/Bukit.Shared.Tests/tests.trx`；含 1 个明确的平台 N/A，实际适用 529 项 |
| Shared 行覆盖 | **610 / 699 = 87.27% ≥ 70%** | TRX 唯一关联的 `coverage.cobertura.xml`；`SafeUrl` 33/33，`PathUtils` 45/46；其余文件与 1.6 相同 |
| macOS arm64 非插桩平台专项 | **42 Passed、0 Skip；41 个适用用例** | `TestResults/shared-kd-fix-platform-20261024/tests.trx`；PathUtils 19、opener 18、PlatformPathHelper 4；Windows comparison 1 项 N/A 单列；本机平台验收通过 |
| PluginHost 路径消费者 | **16 Passed** | `TestResults/shared-kd-fix-pluginhost-20261024/tests.trx` |
| CLI clean / 严格子路径消费者 | **17 Passed** | `TestResults/shared-kd-fix-cli-20261024/tests.trx`；使用下述 NU1902/NU1903 命令行调整 |
| Wechat 缓存路径消费者 | **5 Passed** | `TestResults/shared-kd-fix-wechat-20261024/tests.trx`；同上，仅运行现有直接消费者用例 |
| Labs Intent 消费者 | **9 Passed** | `TestResults/shared-kd-fix-labs-20261024/tests.trx`；同上，仅运行现有直接消费者用例 |
| Linux x64 / Windows x64 | 待验 | 本机未取得实际执行证据 |

消费者测试的环境限制：CLI 原始命令在还原阶段因既有 `SixLabors.ImageSharp` 3.1.12 的 5 条漏洞公告产生 NU1902/NU1903，被 `TreatWarningsAsErrors` 阻断，未执行测试。CLI、Wechat、Labs 均引用该依赖，因此本次专项命令显式增加 `-p:WarningsNotAsErrors=NU1902%3BNU1903`，**保留漏洞告警**并允许行为测试运行；没有修改依赖版本、项目配置或 CI。上述通过结果仅对应这些确切命令，不代表默认还原或依赖审计通过。原始失败日志另存于本次证据目录。

本次环境仍为 macOS 15.8 / .NET 进程 arm64，SDK 10.0.100、runtime 10.0.0、VSTest 18.0.1。确切命令：

```bash
# RED 时源码尚未修复；GREEN 使用同一过滤，输出目录改为 shared-kd-fix-green-20261024
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~Bukit.Shared.Tests.SafeUrlTests|FullyQualifiedName~Bukit.Shared.Tests.PathUtilsTests' \
  --logger 'trx;LogFileName=tests.trx' --results-directory TestResults/shared-kd-fix-red-20261024

bash scripts/checks/coverage/run-one.sh Release \
  tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj TestResults/coverage/shared-kd-fix-20261024

dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~Bukit.Shared.Tests.PlatformPathHelperTests|FullyQualifiedName~Bukit.Shared.Tests.PathUtilsTests|FullyQualifiedName~Bukit.Shared.Tests.PlatformSafeSourceFileOpenerTests' \
  --logger 'trx;LogFileName=tests.trx' --results-directory TestResults/shared-kd-fix-platform-20261024

dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~Bukit.PluginHost.Tests.PluginPathValidatorTests' \
  --logger 'trx;LogFileName=tests.trx' --results-directory TestResults/shared-kd-fix-pluginhost-20261024

dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release -p:WarningsNotAsErrors=NU1902%3BNU1903 \
  --filter 'FullyQualifiedName~Bukit.Cli.Tests.CleanCommandTests|FullyQualifiedName~Bukit.Cli.Tests.DevCommandTests.PathUtils_IsSubPathOf_DoesNotMatchPrefixSibling' \
  --logger 'trx;LogFileName=tests.trx' --results-directory TestResults/shared-kd-fix-cli-20261024

dotnet test tests/Bukit.Plugin.WechatSync.Tests/Bukit.Plugin.WechatSync.Tests.csproj -c Release -p:WarningsNotAsErrors=NU1902%3BNU1903 \
  --filter 'FullyQualifiedName~Bukit.Plugin.WechatSync.Tests.SyncCacheManagerTests.ResolvePath_' \
  --logger 'trx;LogFileName=tests.trx' --results-directory TestResults/shared-kd-fix-wechat-20261024

dotnet test tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj -c Release -p:WarningsNotAsErrors=NU1902%3BNU1903 \
  --filter 'FullyQualifiedName~Bukit.Labs.Cli.Tests.IntentApplierTests|FullyQualifiedName~Bukit.Labs.Cli.Tests.IntentCommandTests' \
  --logger 'trx;LogFileName=tests.trx' --results-directory TestResults/shared-kd-fix-labs-20261024

git diff --check -- src/Bukit-Core/Bukit.Shared/SafeUrl.cs src/Bukit-Core/Bukit.Shared/PathUtils.cs \
  tests/Bukit.Shared.Tests/SafeUrlTests.cs tests/Bukit.Shared.Tests/PathUtilsTests.cs \
  docs/plans/bukit-core-automated-test-plan-2026-10-24.zh-CN.md
```

覆盖率仍按 1.4 的附件定位及行归并方法计算，将 TRX 路径替换为本节路径。所有直接消费者专项零失败、零跳过；完整 Shared 与平台命令的默认检查均通过。1.6 的 runner 跳过计数兼容问题仍待独立验证配置任务修复，本次零跳过的成功运行不能证明该兼容问题已解决。

详细日志、修改前快照、closure、缓存、逐行覆盖及复核证据目录：`/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/shared-kd-fix-hhynlaf9/`。当时未运行全量/发布门禁；其他平台证据在 1.8 补齐，网络集成边界仍按 1.5/1.6 跟踪。

#### 1.8 Linux / Windows 补证与阶段出口（2026-10-25）

**阶段 1：SUCCESS。三平台执行证据已齐，专项复核与一次 delta-only 最终复核完成，满足 1.5 的 Shared 阶段出口条件。** 用户明确要求补齐 Linux、Windows 证据，并选择、授权 GitHub Actions 临时专项分支。既有 SH-01～SH-12、KD-009/KD-010 修复及 1.6/1.7 历史结果保留。

版本与执行环境：

- 临时分支：`evidence/shared-phase1-20261025`，基于已发布 `c992a6c35367b3d04d55f2d958ee0e31364d71b0`，带入此前完成的 15 个 Shared 源码/测试文件修改及独立专项工作流。本机 `main` 的 HEAD 仍为 `c938a1aaca2ab7843fec1b8856f9e0a8bb324949`。
- 三平台的 **53 个源码、测试、项目、SDK/包配置及 coverage 输入逐文件 SHA-256 相同**，聚合指纹为 `762af41d8eebf03a0d47a99583183823499c4b96c32c3f5f4fd2f162c9e6332f`。远端使用与 1.7 相同的中央包文件；本机并行图片迁移修改的包项不被 Shared 项目引用，Shared 的四个测试依赖版本未变。
- Linux：GitHub `ubuntu-24.04`，实际 Linux x86_64 / .NET x64；Windows：GitHub `windows-2025`，实际 Windows Server 2025 / AMD64 / .NET x64。均为 **SDK 10.0.100、runtime 10.0.0、VSTest 18.0.1，Release**，默认警告策略。
- macOS arm64 复用 1.7 的原始 TRX 与覆盖率，逐项核对输入字节和测试清单；没有将历史执行冒充本次重跑，也没有将不同 HEAD 的缓存称为同一缓存命中。

| 平台 | 完整 Shared：Passed / Failed / Skip | 完整专项实际适用 | 非插桩平台：Passed / 实际适用 | Shared 行覆盖 |
|---|---|---:|---:|---|
| Linux x64 | **530 / 0 / 0** | 529 | **42 / 41** | **599/699＝85.69% ≥70%**；coverage runner 退出 0 |
| Windows x64 | **530 / 0 / 0** | 528 | **42 / 40** | 本次未采集；完整测试及非插桩平台命令均退出 0 |
| macOS arm64（1.7 历史证据） | **530 / 0 / 0** | 529 | **42 / 41** | **610/699＝87.27% ≥70%** |

平台 N/A 逐项核对并从适用数扣除：Linux/macOS 各只有 `PlatformPathHelperTests.PathComparison_OnWindows_ReturnsOrdinalIgnoreCase`；Windows 只有 `PlatformPathHelperTests.PathComparison_OnNonWindows_ReturnsOrdinal` 和 `PlatformSafeSourceFileOpenerTests.Open_WhenTargetIsFifo_FailsClosedWithoutBlocking`。三个文件系统类均有实际通过用例：PathUtils 19、PlatformPathHelper 4、opener 在 Linux/macOS 为 18、Windows 为 17。完整专项均包含相同的 20 个测试类与 530 个测试条目，没有遗漏或新增 Skip。

原始远端证据：

1. [运行 40111506691](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40111506691)，提交 `641fee8a2ccc001a10b500799b1b7edceda545fd`：Windows job 成功，包含完整 Shared 及平台专项；Linux 平台专项成功。该次运行整体失败，因为 Linux coverage 输出目录未位于 runner 允许的 `TestResults/coverage/` 下，完整 Shared 尚未执行；不将整次运行记为 GREEN。
2. [运行 40111976112](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40111976112)，提交 `d4be941e625334328997d48da3fd85e34e919415`：修正输出目录后，仅执行缺失的 Linux 完整 Shared / coverage，job 与 runner 均成功。两个提交的 Shared 验证输入完全相同，未重复执行前一次已通过的 Windows 和 Linux 平台专项。
3. 两次更早的工作流启动失败（配置表达式位置、安装步骤额外带入 runtime 10.0.13）均未执行测试，保留诊断记录，不计入成功结果。最终使用官方 SDK 安装脚本取得精确版本。

确切成功命令：

```bash
# Linux：完整 Shared + coverage
bash scripts/checks/coverage/run-one.sh Release \
  tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj \
  TestResults/coverage/shared-platform-linux

# Windows：完整 Shared
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c Release \
  --logger 'trx;LogFileName=tests.trx' \
  --results-directory TestResults/shared-platform/full/Bukit.Shared.Tests

# Linux / Windows：非插桩平台专项
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~Bukit.Shared.Tests.PlatformPathHelperTests|FullyQualifiedName~Bukit.Shared.Tests.PathUtilsTests|FullyQualifiedName~Bukit.Shared.Tests.PlatformSafeSourceFileOpenerTests' \
  --logger 'trx;LogFileName=tests.trx' \
  --results-directory TestResults/shared-platform/platform
```

覆盖率仍使用 1.4 的 TRX 唯一附件与按源码行归并口径，仅汇总 Shared。新增工作流的直接 owner 自检、参考区边界自检和实际边界检查已通过；没有启动既有完整 Core、全量或发布门禁。

证据目录：`/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/shared-platform-TIfifB/`，包含两个平台原始 artifacts、四条成功命令及其各自提交的缓存、源码指纹、`platform-summary.json`、N/A/测试清单核对和复核记录。缓存只对应各自 HEAD、输入、命令、环境状态和工具链；补跑提交不覆盖旧缓存或重标其适用范围。

出口复核：专项 **Critical 0 / Important 0 / Minor 1**；唯一 Minor（SPE-001，自检回执归档）已通过 `owner-check-final.md` 补齐既有执行输出关闭。最终 `review-scope.json` 仅核对计划出口结论与回执增量，复用未变化的专项证据，**无未关闭 Critical / Important / Minor**，详见 `final-review.md`。本结论仅确认 Shared 阶段 1，不代表整个 Core 测试计划或发布验收完成。

剩余边界：1.6 的 coverage runner 有 Skip 时计数兼容问题仍属独立工具修复；本次零 Skip 不证明该问题已解决。真实 socket / HTTP 重定向链仍属于后续调用方集成验证，各 OS 原生故障分支也不以总覆盖率宣称全部覆盖。图片后端迁移的未关闭项另行跟踪，不属于 Shared 阶段出口。

### 阶段 2：Bukit.Config

- 严格 YAML：未知字段、类型错误、重复键、必填缺失。
- 默认值：逐项断言。
- 校验器：collections、i18n、provider、theme manifest、SEO 标题模板、时区。
- `modelSchema` 各映射：必填键缺失时的行为（见第 9 节待决 2）。
- 环境变量覆盖：优先级、非法值、无关 `BUKIT_*__*` 变量的影响。
- JSON Schema 生成与运行时校验器逐字段对照，防漂移。

出口：Config 达到第 9 节的单项目覆盖率下限，schema 对照测试覆盖全部配置节。

#### 2.1 执行、复审与证据（2026-10-27）

**阶段 2：SUCCESS（R-1、I-2、I-3 已关闭；4 个非阻塞 Minor 保留）。**

原始执行基线为 `bcae33b2`；KD-005 的 `ConfigCollectionReader` 生产修复已在该提交中。本复审新增 `CollectionsValidator` 的语义回退，避免把所有额外 collection 一律判为未引用；同时补测试、执行证据和计划记录。证据根为 `/Users/ali/.local/state/bukit-verification/config-phase2-audit-remediation/`，提交前证据另见 `/Users/ali/.local/state/bukit-verification/config-phase2-20261027/`；R-1 复审证据根为 `/Users/ali/.local/state/bukit-verification/config-phase2-r1-remediation/`。

| 要求 | 证据 |
|---|---|
| 完整 Config 专项 | `bash scripts/checks/coverage/run-one.sh Release tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj TestResults/coverage/config-phase2-r1-final`；**430 passed / 0 failed / 0 skipped**，wrapper exit 0；`tests.trx` 与 `coverage.cobertura.xml` 位于该目录 |
| 单项目覆盖率 | Config **89.45% (2966/3316)**；`config-phase2-r1-remediation/coverage-summary/coverage-summary.json`。Config-only policy 的自动最小值为 0/0，70% 门槛为人工核对；整体 75.88% 不是阶段指标 |
| 缓存记录 | `config-phase2-r1-remediation/cache/config-final.record.json`，59 个 Config 源码/测试/SDK 配置文件；`cache/check.log` 显示 CACHE HIT `54c901ca...a944` |
| I-1 schema 逐字段对照 | `tests/Bukit.Config.Tests/ConfigSchemaNestedContractTests.cs`：52 个嵌套/动态映射 case，覆盖 `site.seo`、`site.analytics`、`collections`、`modelSchema`、`notion`、`menus`、`components`、`build.report` 等；provider `oneOf`、plugin toggle 和动态 map 单独断言。7 个测试全部通过 |
| R-1 源文本双向对照 | 解析 `src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs` 中每处 `RequireOnly` / `ValidateSequenceMappings` 的路径与字段集合（含 `{name}`/`{scope}`/`{index}` 插值和 path 参数种子），与 `NestedCases` 及生成的 schema 双向比对；负向自检证明校验器单侧新增字段、新增路径或 schema 单侧变化都会失败。provider 类型分支改由源码 switch 驱动 |
| I-3 Engine 闭包 | 11 个含 collection 配置的测试类完整执行：**255/255 passed**，`results/engine-2/engine-closure-2.trx` |
| I-3 CLI 闭包 | 3 个 CLI 测试类完整执行：**54/54 passed**，`results/cli-2/cli-closure-2.trx` |
| KD-005 | `ConfigCollectionReader` 对 canonical/custom/fieldScopes/entity/relation 映射缺失必填键抛 `ConfigRequiredFieldMissing`；五组 RED 后转 GREEN，无 Skip |
| CFG-001 | 保留 `addToCollections` 引用和大小写比较修复；关闭“额外 collection 一律拒绝”的过度收紧，恢复“所有 source 已声明 collection 时允许额外 collection”的既有语义，避免 Engine 中依赖 front matter/空集合的 89 个用例回归 |
| 审查 | 审计发现 R-1；限定复核与最终 delta 复核均通过（Critical 0 / Important 0），R-1 已关闭，I-2/I-3 已关闭。原审计 3 个唯一非阻塞 Minor：SR-004（裸键诊断精度）、M-1（默认值断言广度）、M-2 / KD-005-TEST-01（KD-005 边界用例，合并计数）；另有 R1-M2（解析器注释/维护性）1 个非阻塞 Minor。复核见 `config-phase2-r1-remediation/r1-review.md` 与 `config-phase2-r1-remediation/final-review.md` |

本轮额外修正了 CLI Worktree 测试在 macOS `/var` 与 `/private/var` 路径别名下的误报（仅测试路径规范化，无生产行为变化）。未运行 full/release、全解决方案、其他平台或远程 provider 门禁。

### 阶段 3：领域与内容源

- `ContentDocument.Create`：字段回退链、状态默认值、`draft` 与 `status` 关系。
- `CanonicalContentGraphBuilder` 与 Validator：关系有向、悬空引用、重复 id。
- Markdown：front-matter（BOM、CRLF、YAML 边界）、正文投影、目录。
- 媒体：本地化、签名与格式校验、重复引用、下载失败。
- Notion（假传输）：属性映射、分页、限流重试、块渲染、HTML 转块、写入。
- 组合提供者：多源冲突、body 缓存。
- 业务规则：draft 撤下页面与投影；过期、noindex 只退出索引聚合；`VerifiedAt` 不映射 `reviewedAt`。

出口：领域与内容各项目达到第 9 节的单项目覆盖率下限。

#### 3.1 执行与证据（2026-10-27）

**阶段 3：SUCCESS（审计 B/B 修复完成；限定复核与最终 delta 复核均通过）。**

基线为 `89c2c015`（阶段 2 R-1 提交后）。阶段 3 初版只补测试；审计后按负责人决定 **B/B** 修改 Core：`requireRelationTargets` 改为解析目标，重复 content ID 改为报错。证据根为 `/Users/ali/.local/state/bukit-verification/content-phase3/`。

| 要求 | 证据 |
|---|---|
| 领域专项 | `tests/Bukit.Content.Tests` **568/568**；`tests/Bukit.Content.Notion.Tests` **41/41**；`tests/Bukit.Notion.Tests` **427/427**；`tests/Bukit.Engine.Abstractions.Tests` **88/88**；完整 `tests/Bukit.Engine.Tests` **2671/2671**；`tests/Bukit.Architecture.Tests` **415/415**；全部 0 失败/跳过 |
| 直接消费者（Doctor） | `tests/Bukit.Cli.Tests` **1076/1076**；新增 `canonical_duplicate_id` / `canonical_relation_target_unresolved` 阻断用例 |
| 单项目覆盖率 | `Bukit.Content` **93.08% (3458/3715)**；`Bukit.Engine.Abstractions` **80.48% (606/753)**；`Bukit.Notion` **95.30% (1623/1703)**；均 ≥70%。`coverage-summary/coverage-summary.json`、`coverage-check.json`；工具 policy 为 0/0，70% 为人工核对 |
| Canonical 类覆盖 | 完整 Engine 覆盖率：`CanonicalContentGraphBuilder` **94.63% (282/298)**、Helpers **89.13% (82/92)**、`CanonicalContentValidator` **100% (42/42)**、`ContentModelSchemaValidator` **84.03% (200/238)**；`canonical-coverage.json` |
| `ContentDocument.Create` | draft/status 优先级、summary→description→excerpt 回退、categories→sections 与 tags 合并、显式 sections 优先、image/cover 媒体投影、translation/related/mentions 有向关系、`authoredBy` 标量/map 投影、`reviewed_at` 映射而 `verified_at` 不映射；`ContentDocumentTests` **24/24** |
| Canonical 图与关系 | B/B 决定：重复 ID 报 `canonical_duplicate_id`；`requireRelationTargets=true` 时 target 必须解析到 content record 或 entity，`translation-of` 语言键与绝对 HTTP(S) URL 例外，未解析报 `canonical_relation_target_unresolved`；`CanonicalContentGraphContractTests` **4/4**、`CanonicalContentValidatorTests` **4/4**、`ContentModelSchemaValidatorRelationTargetTests` **12/12**、`ContentGraphValidateStageRelationTargetTests` **4/4** |
| Markdown | 既有 front-matter/正文/TOC；新增 UTF-8 BOM、BOM+CRLF、CRLF 正文、正文 `---` 水平线端到端解析；`MarkdownFolderProviderTests` 通过 |
| 媒体 | 既有 `ImageAssetLocalizerTests`、`ImageContentValidatorTests`、`ContentImageRewritePipelineTests`、`MediaIndexManagerTests` 覆盖本地化、签名/格式、重复引用、下载失败；无 Skip |
| Notion（假传输） | 既有 `NotionContentSource*`、`NotionContentPropertyParser*`、`NotionDatabaseQueryBuilder`、`NotionBlock*`、`NotionWriteClient` 等覆盖属性映射、分页、限流重试、块渲染、HTML 转块、写入；`Bukit.Notion.Tests` **427/427** |
| 组合提供者/缓存 | 既有 `CompositeContentProviderTests`、`CompositeContentBodyStoreTests`、`BodyCacheDecoratorTests` 覆盖多源冲突与 body 缓存；无 Skip |
| 缓存记录 | `content-phase3/cache/*.record.json`；Content / Content.Notion / Notion / Engine.Abstractions / 完整 Engine / CLI / Architecture 均 CACHE HIT：`b4c3dba8...`、`25e98497...`、`6b7cc944...`、`19cc4cd4...`、`c50c0174...`、`743de8c7...`、`fb858da6...` |
| 业务规则 | 既有 `DraftFilterStage_*`、`BuildAsync_SeoInjectMode_InjectsHeadAndExcludesNoindexFromOutputs`（hidden/expired 页面仍生成 HTML，只退出聚合）、`BuildAsync_LlmsTxt_ExcludesNoindexAndExpiredRoutes`；`BodyProjection_DraftWithdrawal...` 补 `index.html` 删除断言；`verified_at` 不映射 `reviewed_at`；完整 Engine 覆盖 |
| 审查 | 审计复审（Critical 0 / Important 3 / Minor 7）后按 B/B 修复；限定复核与最终 delta 复核均通过（Critical 0 / Important 0）；A-1、M-1~M-7、D-1~D-4 已关闭；复核见 `content-phase3/audit-review.md` 与 `content-phase3/final-audit-review.md` |

未运行 full/release、全解决方案、其他平台或远程 provider 门禁。

#### 3.2 阶段 3 审计复审与修复（2026-10-27）

审计结论为 **Critical 0 / Important 3 / Minor 7**，阶段 3 曾回退为 CONDITIONAL；本轮按负责人决定 **B/B** 完成修复。

| 审计项 | 状态 | 修复与证据 |
|---|---|---|
| I-1 Canonical 表征固化未决定行为 | 已修复 | 负责人决定：`requireRelationTargets` 必须解析目标；重复 ID 必须报错。`ContentModelSchemaValidator` 新增 `canonical_relation_target_unresolved` 与目标解析（content ID/slug、entity ID/name、外部 HTTP(S) URL、`translation-of` 例外）；`CanonicalContentValidator` 新增 `canonical_duplicate_id`。原先“断言不报错”的测试已改写为报错契约测试 |
| I-2 Engine/Architecture 闭包未跑全 | 已修复 | 完整 Engine **2671/2671**、Architecture **415/415**；`BodyProjection_DraftWithdrawal...` 已包含在完整 Engine；定向子集重新记录缓存 |
| I-3 新测试缺少 Trait | 已修复 | 新增/修改测试使用 `Unit`、`Contract`、`Integration` 分类；`ContentDocumentTests` 为 Unit，Canonical/关系为 Contract，Markdown/SiteEngine 为 Integration |
| Minor：VerifiedAt 代用 | 已修复 | 计划明确写成“`verified_at` front-matter 不映射到 `ReviewedAt`”；Core 无 `VerifiedAt` 符号 |
| Minor：Canonical 覆盖率未统计 | 已修复 | 完整 Engine 覆盖率按类统计，见 3.1 的 Canonical 类覆盖行 |
| Minor：Markdown 边界/Git 测试名 | 已修复 | 补 BOM+CRLF、CRLF 正文、正文 `---`；BOM 测试名改为 `LoadRawAsync_*` |
| Minor：draft 只验证部分产物 | 已修复 | `BodyProjection_DraftWithdrawal...` 增加 `dist/blog/one/index.html` 不存在断言 |
| Minor：作者资料投影未测 | 已修复 | 新增 `authoredBy` 标量/map/缺省测试；`ContentAuthorProfileProjectionReader` 覆盖率 **93.94%**，Engine.Abstractions 由 76.23% 升至 80.48% |
| Minor：项目映射/目录残留 | 已修复 | 计划第 2 节与 §4.2 更新为 18 个源项目/13 个测试项目；§3.1 说明 Content.Notion.Tests 计入 Bukit.Content；删除 `src/Bukit-Core/Bukit.Content.Notion/` 未跟踪 `obj/bin` |
| Minor：证据隐患 | 已修复 | TRX/coverage/summary/review 复制到 `content-phase3/evidence/`；重复运行以最终一次为主证据，缓存仅作指纹 |
| Minor：projection/doctor 未消费新规则 | 已修复 | `ContentModelSchemaProjection.ValidateDocuments` 纳入 `CanonicalContentValidator`；Doctor 将 `canonical_duplicate_id`/`canonical_relation_target_unresolved` 视为阻断；新增 2 个 CLI 测试 |
| Minor：resolver 边界/性能 | 已修复 | 补 target trim、TargetId-first、targetType mismatch、root-relative 用例；`O(records×relations)` 查询保留为已记录非阻塞性能项 |

本轮 Core 修改为 `src/Bukit-Core/Bukit.Engine/CanonicalContentValidator.cs`、`src/Bukit-Core/Bukit.Engine/ContentModelSchemaValidator.cs`、`src/Bukit-Core/Bukit.Engine/ContentModelSchemaProjection.cs`、`src/Bukit-Core/Bukit.Cli/Commands/DoctorSchemaChecker.cs` 四个文件，以及 `guide/user/04-site-yaml-config.md` 的语义说明；其余为测试与计划。`warn`/`off` 模式下重复 ID 仍会进入图并按 `type/targetType/targetId` 合并关系；`strict` 模式会阻断，该限制已在审查中记录。

### 阶段 4：路由、主题、渲染

- 路由：permalink 模板、分页、列表路由、冲突、路径安全。
- 主题：`theme.yaml` 严格校验、extends、section 数据与 schema、tokens。
- Scriban：模板模型字段、函数、错误定位、`page.content_model.slug` 契约、linter 与符号分析。
- 渲染确定性：同一输入同一输出。

出口：三个项目分别达到第 9 节的单项目覆盖率下限。

#### 4.1 执行与证据（2026-10-27）

**阶段 4：SUCCESS（按 C 完成扩展名路由判定；审计修复最终复核通过）。**

基线为 `f95a9e96`（阶段 3 审计修复提交后）。本阶段补测试、计划记录，并按审计决定 C 按模板判定扩展名 permalink；同时拒绝编码控制字符。Core 变更仅限 `RoutePathBuilder`、`RouteGenerator` 与 `RouteSecurityValidator`。证据根为 `/Users/ali/.local/state/bukit-verification/content-phase4/`。

| 要求 | 证据 |
|---|---|
| 路由专项 | `tests/Bukit.Routing.Tests` **112/112**，覆盖率 **90.24% (333/369)**（含 4.3、4.4 修复后）；新增 encoding、扩展名 permalink、编码穿越/斜杠、`ValidateOutputPath` 用例 |
| 路由闭包 | 完整 `tests/Bukit.Engine.Tests` **2671/2671**（含 `RouteSecurityValidatorTests`）；Engine 路由 7 类 **118/118**；`tests/Bukit.Architecture.Tests` **415/415** |
| 主题专项 | `tests/Bukit.Theme.Tests` **179/179**，覆盖率 **79.59% (1349/1695)**；7 组严格 `theme.yaml` 诊断；既有 extends、tokens、section data/schema |
| 渲染专项 | `tests/Bukit.Rendering.Tests` **346/346**，覆盖率 **90.31% (1473/1631)**；`content_model.slug` 模板级用例、确定性用例；既有模型/函数/linter/符号/错误定位 |
| 覆盖率校核 | 三个项目均 ≥70%；`coverage-check-c.json`；工具 policy 为 0/0，70% 为人工核对 |
| 缓存记录 | `content-phase4/cache/*.record.json`；Routing / Theme / Rendering / 完整 Engine / Architecture CACHE HIT：`12104837...`、`4348af54...`、`9538eff0...`、`cbd66790...`、`3cd512b4...` |
| 审查 | 限定复核与最终 delta 复核均通过（Critical 0 / Important 0）；I-1/I-2/I-3、A-2/A-3/A-4、M-9/M-10 关闭；A-1/A-5~A-8 为非阻塞残留；见 `content-phase4/final-audit-review.md` |

#### 4.2 阶段 4 审计修复（2026-10-27）

审计结论为 **Critical 0 / Important 3 / Minor 5**；阶段 4 曾回退为 CONDITIONAL。本轮修复如下。

| 审计项 | 状态 | 修复与证据 |
|---|---|---|
| I-1 路径安全证据缺口 | 已修复 | 新增 `%2e%2e`、`%2f`、编码控制字符拒绝、非法百分号行为、`ValidateOutputPath` 安全/不安全用例；完整 Engine 已包含 `RouteSecurityValidatorTests`；`RouteSecurityValidator` 覆盖率 **91.55% (65/71)**。`Uri.UnescapeDataString` 在 .NET 10 对畸形转义不抛异常，测试记录该行为，catch 为防御分支 |
| I-2 扩展名 permalink | 已修复（C） | 由 **permalink 模板**决定文件/目录：末段有字面扩展名（`{slug}.html`）为文件，否则为目录（`/blog/{slug}` + `release.2026` 仍生成目录）。`NormalizeUrl` 增加显式 route-kind 重载；`RouteGenerator` 按模板判定；显式 route URL 仍按扩展名/尾斜杠判定（已由 4.3 收窄为常见 Web 扩展名）。新增 dotted slug 目录/文件用例 |
| I-3 主题失败用例输入被改 | 已记录 | `layouts.base: 1` / `assets.css: 1` 是 YAML scalar，严格校验将其按路径字符串接受；最终测试用 sequence/map 触发类型错误。该 scalar 语义已在此记录，不改变生产行为 |
| Minor：确定性过浅 | 部分记录 | 当前为表达式级同输入两次渲染；循环/字典顺序/文化差异未覆盖，列为后续测试深度项 |
| Minor：错误定位 | 记录 | 解析错误仅断言文件名；行/span 后缀未单独断言 |
| Minor：70% 人工核对 | 记录 | `coverage-check-c.json` 提供机器可读的逐项目 70% 结论；仓库 policy 仍为 0/0，工具级硬门禁留待后续 |
| Minor：证据/M-9/M-10 | 已修复 | `final-findings` 补 M-9/M-10；旧 196/277 声明随旧复核归档；`corrections.md` 记录更正；metrics 补齐阶段事件 |
| Minor：A-1 dotted heuristic | 已由 C 解决模板场景 | `/blog/{slug}` + dotted slug 保持目录；显式 route URL 仍按扩展名/尾斜杠判定，作为显式 URL 约定记录 |

未运行 full/release、全解决方案、其他平台或远程 provider 门禁。

#### 4.3 独立 Core 修复：显式 URL 只认常见 Web 扩展名（N-2，2026-10-27）

**授权**：负责人于 2026-10-27 确认决定 C，并明确授权本次 Core 修复（复审 N-2）。本节为独立 Core 任务记录，不并入阶段 4 测试结论。

**问题**：决定 C 的首版实现让单参数 `RoutePathBuilder.NormalizeUrl` 用 `Path.HasExtension` 判定文件，Engine 内 20 余处调用方随之改变。`route.url: /docs/v1.0` 从 `docs/v1.0/index.html` 变为无扩展名文件 `docs/v1.0`，用户文档未说明。

**决定**：只有末段以 `.html`、`.htm`、`.xml`、`.json`、`.txt`（不区分大小写）结尾且无尾斜杠时为文件；其余一律为目录并补 `index.html`。permalink 模板使用同一扩展名表，保证 `route.Url` 再次规范化后文件/目录类型不变（`/download/{slug}.php` 由此变为目录，属于对 C 的收窄）。

| 输入 | 修复前 | 修复后 |
|---|---|---|
| `route.url: /docs/v1.0` | `/docs/v1.0` → `docs/v1.0` | `/docs/v1.0/` → `docs/v1.0/index.html` |
| `route.url: /docs/page.html` | 文件 `docs/page.html` | 不变 |
| `route.url: /feed.xml` | 文件 | 不变 |
| `route.url: /tags/node.js` | 文件 `tags/node.js` | 目录 `tags/node.js/index.html` |
| permalink `/blog/{slug}.html` | 文件 | 不变 |
| permalink `/download/{slug}.php` | URL 无尾斜杠、输出与再规范化不一致 | 目录 `download/<slug>.php/index.html` |

| 项 | 证据 |
|---|---|
| Core 文件 | `src/Bukit-Core/Bukit.Routing/RoutePathBuilder.cs`（`WebFileExtensions`、`IsWebFileSegment`）、`src/Bukit-Core/Bukit.Routing/RouteGenerator.cs`（`IsFilePattern` 共用扩展名表） |
| 测试 | `RoutePathBuilderTests.NormalizeUrl_ExplicitUrl_OnlyWebExtensionsAreFiles`（11）、`BuildOutputPathFromUrl_ExplicitUrl_OnlyWebExtensionsAreFiles`（4）；`RouteGeneratorTests.GenerateWithSource_FullOverrideDottedUrl_OnlyWebExtensionIsFile`（3）、`GenerateWithSource_PartialOverrideDottedUrl_RemainsDirectory`、`Generate_WithNonWebExtensionPermalink_RemainsDirectoryAndConsistent` |
| RED（修复前） | 同一过滤 **8 失败 / 11 通过**：`/docs/v1.0`、`/blog/release.2026`、`/tags/node.js`、`/download/app.php` 被当作文件 |
| GREEN | 定向 **20/20**；完整 Routing **98/98**（88.96%，298/335）、完整 Engine **2671/2671**、Content **568/568**、Architecture **415/415**；`scripts/checks/config-docs/validate_docs.py` 通过 |
| 闭包 | `codex-workflow.py closure`：7 个变更文件，专项为 Routing / Engine / Content / Architecture；唯一未映射项为本计划文件（文档，无测试 owner） |
| 用户文档 | `guide/user/05-markdown-content.md`（`route.url` 文件/目录规则）、`guide/user/04-site-yaml-config.md`（permalink 文件/目录规则） |
| 证据根 | `/Users/ali/.local/state/bukit-verification/content-phase4-n2/` |

剩余：路由层仍无"文件与同名目录"结构冲突检测（如 `/a.html` 与 `/a.html/x/`），也无 `{slug}.html` 的 Engine 管线级用例（复审 N-3），留待阶段 5/7。未运行 full/release、全解决方案或其他平台门禁。

#### 4.4 独立 Core 修复：静态文件扩展名路由诊断（2026-10-27）

**授权**：负责人于 2026-10-27 明确授权本次 Core 修改：`route.url` 与 permalink 末段为非 Web 扩展名的静态文件时给出构建诊断，提示放到 `static/` 或 `assets/`。

**决定**：
- 只对明确的静态文件扩展名报错（构建失败）：`.css .js .mjs .map .wasm`、常见图片、字体、`.pdf`、压缩包、音视频、`.csv` 与 Office 文档。未知带点段（`/docs/v1.0`、`.php`）仍按 4.3 规则生成目录，不报错。
- permalink（`site.collections.*.permalink`、`site.permalinks.*`）只检查模板字面末段，诊断码 `RouteInvalidPattern`；slug 本身（如 `node.js`）不触发。
- `route.url`（完整与部分覆盖）检查原始 URL，诊断码 `RouteInvalidInternalUrl`；以 `/` 结尾表示页面目录，不触发。
- 复用既有诊断码，不新增公共枚举；报错信息含 URL/模板、内容 id、扩展名及 `static/`、`assets/` 与尾斜杠提示。

| 项 | 证据 |
|---|---|
| Core 文件 | `src/Bukit-Core/Bukit.Routing/RoutePathBuilder.cs`（`StaticAssetExtensions`、`GetStaticAssetExtension`）、`src/Bukit-Core/Bukit.Routing/RouteGenerator.cs`（`RejectStaticAssetPattern`、`RejectStaticAssetUrl`） |
| 测试 | `RouteGeneratorTests`：静态扩展名 permalink（4）、`site.permalinks`（1）、完整覆盖 route.url（4）、部分覆盖（1）报错；尾斜杠/`v1.0`/`.php` 完整覆盖（3）与 `node.js` slug（1）允许 |
| RED（修复前） | 同一过滤 **10 失败 / 4 通过**（报错用例全部未报错） |
| GREEN | 定向 **14/14**；完整 Routing **112/112**（90.24%，333/369）、完整 Engine **2671/2671**、Content **568/568**、Architecture **415/415**；`config-docs/validate_docs.py`、`cli-docs-sync.sh` 通过；仓库示例与 fixture 无受影响配置 |
| 闭包 | `codex-workflow.py closure`：7 个变更文件；专项另含 `openspec validate add-i18n-worktree-isolation --strict`，该 change 已不存在（策略文件过期映射），无法执行，记录为验证缺口，未改策略文件 |
| 用户文档 | `guide/user/05-markdown-content.md`、`guide/user/04-site-yaml-config.md`、`guide/user/14-troubleshooting.md`（新增 "Route Ends In A Static File Extension"） |
| 证据根 | `/Users/ali/.local/state/bukit-verification/content-phase4-diag/` |

剩余：未覆盖 `listRoute` 与插件生成路由；页面输出与 `static/` 复制之间的同名文件/目录冲突检测仍属复审 N-3。未运行 full/release、全解决方案或其他平台门禁。

### 阶段 5：Engine 安全层

按顺序：
1. `OutputDirectoryCleaner`：站点根、用户主目录、文件系统根、`.git`、符号链接、无 marker 的非空目录。
2. `BuildTransaction`：提交前失败保持原状、提交中途失败反向还原、备份保留、异步上下文、资源租约冲突。
3. `AssetOutputPlan`：精确冲突、结构冲突、大小写敏感与不敏感文件系统。
4. 增量构建：manifest v3、各 Contributor 的失效条件、目录哈希超限行为、增量与全量等价。
5. 输出归属：过期文件清理、未跟踪文件保留、遗留或无效归属触发的恢复。

出口：以上五项每项至少含正常、失败、边界三类用例；按第 4.3 节比较的增量等价测试通过，三个安全层文件分别达到第 9 节阈值并附第 4.4 节证据。

#### 5.1 执行与证据（2026-10-27，2026-10-28 整改）

**阶段 5：SUCCESS（两项 Important 已修复；最终只读复核 Critical 0 / Important 0）。**

基线为 `b11fc4fe`。证据根为 `/Users/ali/.local/state/bukit-verification/content-phase5/`。

| 要求 | 证据 |
|---|---|
| 三个安全文件覆盖率 | `OutputDirectoryCleaner` **96.30% (52/54)**、`BuildTransaction` **96.46% (191/198)**、`AssetOutputPlan` **99.34% (150/151)**；`security-layer.py` 全部 passed；完整 Engine 项目 **89.73% (14578/16247)** |
| 完整闭包 | `tests/Bukit.Engine.Tests` **2697 总数 / 2694 通过 / 0 失败 / 3 KnownDefect Skip**；`tests/Bukit.Cli.Tests` **1077 / 1076 / 0 / 1 Skip**；`tests/Bukit.Architecture.Tests` **415/415**；`core-report-inventory.py --self-test` OK |
| 覆盖率采集 | `engine-final2` 使用 `FullyQualifiedName!~HashUtilTests.Sha256HexForDirectory_Over&FullyQualifiedName!~TaxonomyRedirectWriterTests.WriteRedirects_AliasTraversal` 排除 2 个 KnownDefect Skip 后 **2694/2694、0 跳过**，`security-layer.py` 与完整 Engine 数字取自该次；覆盖率不把 Skip 计入通过 |
| B1 KD-002/003/004 | 已补 RED 与 KnownDefect+Skip：`results/kd002-red` 1 失败、`results/kd003-004-red` 2 失败；最终用例带 `[Trait("Category","KnownDefect")]`、`[Trait("Defect","KD-编号")]` 且 Skip；KD-004 以 Theory 覆盖 `..` 与绝对路径，并先断言未写入任何文件；KD-003 覆盖总大小与文件数两种超限 |
| B2 增量构建 | 新增 `RenderDependencyContributorInvalidationTests`：9 个 Contributor 各自同配置确定性/相关变更/边界断言；KD-003 覆盖超限同长度改动误命中；`DirectoryHashCacheTests` 3、`RenderDependencyHasherTests` 51 个方法、`IncrementalBuildEngineTests` 46 + Async 10、manifest v3/损坏 ownership 11 |
| B3 AsyncLocal | 新增 `AsyncLocalFlowsIntoTaskRunAndSuppressedFlowIsIsolated`：`Task.Run` 继承映射、`ExecutionContext.SuppressFlow` 隔离、事务释放后恢复逻辑路径 |
| B4 三类用例 | 见下表逐项对照 |
| I2 跨平台 | `.github/workflows/ci.yaml` 的 `platform-tests` 已加入 `OutputDirectoryCleanerTests` 与 `AssetPipelineTests`；实际 `CI` run `40556553321`（`gate=platform`）在 **Linux x64 / Windows x64 / Darwin arm64** 执行 canonical `platform-tests`，全部选中类 0 失败/0 Skip，Cleaner 13、AssetPipeline 30 三平台均通过；`platform.json`、`dotnet-info.txt`、逐项目 TRX 存于 `results/platform-canonical2`；临时 run `40544532088` 仅作补充。该临时分支额外修复两项既有 CI 阻塞（文档链接、public API baseline）；平台选中类与当前工作树一致，随后仅改 Trait/文档/Architecture 断言，平台类未再变化 |
| 审查 | 2026-10-28 复审确认 2 项 Important（Cleaner 非 macOS 静默 return、KD-004 未覆盖绝对路径/越界写入）已修复；最终只读复核 Critical 0 / Important 0，7 项 Minor 非阻塞；见 `stage5-important-fixes-review.md` |

**五项逐项正常/失败/边界对照：**

| 项 | 正常 | 失败 | 边界 |
|---|---|---|---|
| 1 `OutputDirectoryCleaner` | `CleanIfExists_WithMarker_DeletesOutput`、`EnsureCanClean_EmptyOutput_Allows`、`EnsureCanClean_RequireMarkerFalse_AllowsNonEmpty` | `CleanIfExists_NonEmptyWithoutMarker_Throws`、`EnsureCanClean_Root/FilesystemRoot/GitSegment/UserHome/SymlinkBelowRoot_Throws` | `CleanIfExists_MissingOutput_DoesNothing`、`EnsureCanClean_MissingOutputSegment_Returns`、`EnsureCanClean_ExistingDirectoryWithoutReparsePoint_Returns` |
| 2 `BuildTransaction` | `CommitPreservesAbsoluteLinksAcrossTargetsAndWithinOutput`、`NotionAndMediaWriteSetCommitsTogetherOrPreservesOldBytes(commit=true)`、`NewOutputAndCacheParentCommitsOnceWithFormalOutputIdentity` | `FailureAndCancellationLeaveTargetsUnchanged`、`SwapFailureRollsBackAndPreservesBackupWhenRecoveryFails`、`ReadOnlyReadersCoexistButWritersConflictInBothDirections`、`SeparateProcessCannotClaimOverlappingResource` | `NestedDisjointBuildRetainsItsOwnMappingAndRestoresParentScope`、`AsyncLocalFlowsIntoTaskRunAndSuppressedFlowIsIsolated` |
| 3 `AssetOutputPlan` | `ParentAndSiteStaticSameDestination_PreservesSiteOverride`、`FollowedFileSymlinkWithoutCollision_IsCopiedAndTracked` | `StaticAndAssetsSameDestination_FailsBeforeWriting`、`AssetsAndGeneratedTokensSameDestination_FailsBeforeWriting`、`StaticAndMediaSameDestination_FailsBeforeWriting`、`FollowedFileSymlinkCollision_IsDetectedBeforeWriting` | case-sensitive APFS **30/30**、symlink 循环/敏感文件/外部中间目录、`ThemeWithoutTokens_DoesNotCreateTokenClaim`、`FollowedSymlinkToCaseVariantExternalRoot...` |
| 4 增量构建 | `PublicLifecycle_ReplayMatchesCleanBuild(incremental:true)` 9 步逐字节一致、`IncrementalBuildEngineTests` 46、`RenderDependencyContributorInvalidationTests` 9 个相关变更断言 | KD-003 两种超限 Skip RED、`BuildManifestTests` 11 中的损坏 ownership | 9 个 Contributor 的顺序/空集合/禁用插件/内部键边界、`DirectoryHashCacheTests` 3、manifest v3 |
| 5 输出归属 | `AfterBuildAsync_TracksVariantAndFreshnessSidecarAsOwnedOutputs`、`AfterBuildAsync_SizeConfigReduced_DeletesStaleVariants` | `AfterBuildAsync_StaleVariantRebuildFails_RemovesVariantAndProjection`、`AfterBuildAsync_NoResizeTool_CleansOwnedStaleHistoricalVariant` | `PublicLifecycle_UntrackedFileSurvivesSuccessfulWithdrawal`、`PublicLifecycle_LegacyManifestRequiresMarker`、`AfterBuildAsync_CurrentSizeUserLookalikeWithoutOwnership_IsNeverOverwritten` |

**整改说明：** B1 按第 6 节只补复现与 KnownDefect，不修改 Core 缺陷代码；B2 的 Contributor 用例直接引用 9 个 Contributor 类，补上 §5.1 原先缺失的 `RenderDependencyHasherTests` 与目录哈希边界；B3 补直接 AsyncLocal 用例。I1 见 5.2；I2 已把两个测试类接入 CI `platform-tests`；canonical run `40556553321` 在 Linux/Windows/macOS 均实际执行 Cleaner 13 + AssetPipeline 30，0 失败/0 Skip。

已知有效的局部证据：在 case-sensitive APFS 卷上复跑 `AssetPipelineTests` **30/30**，修复前唯一的 `ExecuteAsync_FollowedSymlinkToCaseVariantExternalRoot_IsSkippedOnCaseSensitiveFileSystem` 失败已转绿。未运行 full/release、全解决方案或远程 provider 门禁；平台证据覆盖 canonical `platform-tests` 选中类。剩余非阻塞：`SkipException.ForSkip` 在 xUnit 2.9.3 下不等于 Skip、macOS 默认卷大小写变体链接被保守跳过、Cleaner 的 `LinkTarget` 分支仍属加固。

#### 5.2 独立 Core 修复任务（2026-10-28 授权）

用户于 2026-10-28 确认：把测试阶段涉及的两处 Core 改动拆为独立 Core 修复任务并保留，不并入阶段 5 测试结论。

| 项 | 状态 | 证据与说明 |
|---|---|---|
| `DirectoryCopy.TryResolveSafeTarget` | 有效修复，保留 | case-sensitive APFS 上 `AssetPipelineTests` RED 29/30 → GREEN 30/30；改用 `PathUtils.IsSameOrSubPathOf`，跳过指向外部大小写变体目标的符号链接 |
| `OutputDirectoryCleaner` | 已修复 | 根路径归一化 RED（输出为 `/` 时抛 `ArgumentException`）；删除不可达的文件系统根分支；macOS 用户目录比较改为 `OrdinalIgnoreCase`，新增跨平台 RED→GREEN 用例 `EnsureCanClean_UserHomeCaseVariant_FollowsPlatformComparison`；`LinkTarget` 保留为防御 |
| 后续 | 待办 | 独立 Core 复核 `LinkTarget` 防御分支与大小写变体链接策略；阶段 5 结论不替代该任务 |

#### 5.3 独立 Core 修复：KD-002 保留项目元数据（2026-10-28 授权）

用户明确授权“先单独修 KD-002”，本任务不重开阶段 6，不涉及 KD-003/004 或 SCSS。仅修改 CLI `CleanCommand`：停止整体删除项目根 `.bukit`，保留配置、锁文件、未知文件及已有生成元数据；正常输出和 `.cache` 继续清理。显式输出为根 `.bukit` 或子目录时（含大小写变体、即使有 marker），在任何删除前拒绝并返回 2；`.bukit-output` 等兄弟目录不误拒绝。共享 Engine cleaner 和既有路径／marker／符号链接检查不变。

独立清单为 `openspec/changes/archive/2026-10-30-fix-kd002-clean-metadata-retention/tasks.md`。持久证据根 `R=/Users/ali/.local/state/bukit-verification/kd002-clean-preserve-s2CkH1`；闭包 8 个允许路径、0 未映射，冻结并保留原有 48 个变更路径（包括不属于本任务的 `Directory.Build.props`）。先观察真实行为 RED：`red/tests.trx` 为 **56 通过 / 9 失败 / 0 Skip**；补大小写别名回归后 `red-case/tests.trx` 为 **56 通过 / 13 失败 / 0 Skip**。失败均为元数据被删或显式保护目标未拒绝，不是编译／环境失败；原 RED 未覆盖或混记。

**KD-002：SUCCESS（本次授权范围）。** 完整 `CleanCommandTests` **29** + `ProgramEntryPointTests` **29** + `CliContractTests` **11** = **69 通过 / 0 失败 / 0 Skip**，实际退出码 0；见 `R/green.log`、`R/green/tests.trx`、`R/green-receipt.json`。原 KD-002 Skip/KnownDefect 排除已移除；既有路径、marker、符号链接和 CLI 契约用例保持启用。`git diff --check` 通过。

一次独立专项复核 `R/specialty-review.md` 与一次控制器 delta-only 最终复核 `R/final-unified-review.md` 均为 **Critical 0 / Important 0 / Minor 0**。`R/final-review-scope.json` 仅生成一次；无跨任务交集、失效证据、未覆盖变更或未结阻塞，复用命中的专项证据，仅核对三项公开行为焦点。759 项运行输入保持通过时的字节；范围外原有变更（尤其 `Directory.Build.props`）不变。复核后仅回填结果／清单状态并做最终静态核对，不重跑未变化 fixture；原始证据、闭包、分类、缓存、队列及指标均持久保存在 R。

确切 GREEN 命令：

```sh
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter "FullyQualifiedName~CleanCommandTests|FullyQualifiedName~ProgramEntryPointTests|FullyQualifiedName~CliContractTests" --logger "trx;LogFileName=tests.trx" --results-directory /Users/ali/.local/state/bukit-verification/kd002-clean-preserve-s2CkH1/green
git diff --check
```

仅验证本机 macOS arm64；不运行完整 Engine／CLI、全解决方案、覆盖率或 full/release 门禁，不自动提交、推送、合并。

#### 5.4 独立 Core 修复：KD-004 redirect writer 边界（2026-10-28 授权）

用户“按计划 执行”后，按 `docs/plans/2026-10-28-core-residual-risk-next-steps.md` 的交接先单独修 KD-004；本批不包含 KD-003、SCSS、阶段 7 或既有阶段 6 重验。仅修改内部 `TaxonomyRedirectWriter`，不新增 aliases 入口，不改共享路径策略、公开 API、依赖或 CI。普通生产构造链尚未发现非空 Aliases 入口；已证明的是 writer 函数级边界，不夸成已证实的远程利用。

每次 writer 调用先校验全部 aliases 和最终 `index.html` 路径，再执行原写入；危险 alias、后置非法项、逃逸 routePrefix 及现有目录／文件符号链接均须以 `ConfigException` / `BuildOutputUnsafe` 拒绝，不创建目录／文件、不覆盖既有字节、不追加生成路径。保留正常／空／自别名、安全层级、Unicode、routePrefix、原 HTML／UTF-8 及路径记录行为。此预检不承诺整个 `AfterBuild` 或任意 I/O 失败的事务性。

独立清单 `openspec/changes/archive/2026-10-30-fix-kd004-taxonomy-redirect-boundary/tasks.md`。持久证据根 `R=/Users/ali/.local/state/bukit-verification/kd004-redirect-boundary-bh6Eqt`；7 个允许路径全部映射，冻结原有 55 个变更路径的基线。`R/red/tests.trx` 为 **33 通过 / 19 预期失败 / 0 Skip**，实际退出码 1；包括不安全路径被写入、后置非法输入发生部分写入、外部链接目标被覆盖和错误类型不符，无编译／环境失败。

**KD-004：SUCCESS（本批授权范围）。** 最终完整三类专项 `R/review-green/tests.trx` 为 **53 通过 / 0 失败 / 0 Skip**（writer 27 + page creator 23 + ensure terms 3），实际退出码 0；原 KD-004 Skip/KnownDefect 排除已移除。首轮 `R/green/tests.trx` 的 52/52 仅为候选证据：专项复核发现 Important I01（NUL 自别名在最终路径校验前被跳过），`R/review-red/tests.trx` 以真实 sentinel 覆盖复现 **1 失败 / 52 通过**，移动校验顺序后才得到最终 53/53；全部历史原件保留。

一次专项复核 `R/specialty-review.md`、仅 I01 修补范围的 `R/specialty-rereview-I01.md` 已关闭唯一 Important；控制器唯一最终复核 `R/final-unified-review.md` 为 **Critical 0 / Important 0 / Minor 0 remaining**。`R/final-review-scope.json` 仅生成一次，无交集、失效证据、未覆盖改动或未结阻塞；复用当前命中缓存，仅核对两项公开行为焦点。722 项运行输入保持通过时的字节，范围外既有改动不变；复核后仅回填结果／清单并最终静态核对，未为刷新证据重跑 fixture。

最终确切 GREEN 命令（原 RED 同过滤、结果目录 R/red；I01 补修 RED 为 R/review-red）：

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~TaxonomyRedirectWriterTests|FullyQualifiedName~TaxonomyEnsureTermsTests|FullyQualifiedName~TaxonomyPageCreatorTests" --logger "trx;LogFileName=tests.trx" --results-directory /Users/ali/.local/state/bukit-verification/kd004-redirect-boundary-bh6Eqt/review-green
git diff --check
```

仅安排本机 macOS arm64；不跑完整 Engine、覆盖率、跨平台、全解决方案或 full/release 门禁，不提交／推送／合并。收口后停在 KD-003 策略确认点。

#### 5.5 独立 Core 修复：KD-003 模板哈希预算降级（2026-10-28 授权方案 A）

用户在 KD-004 收口后确认方案 A：保留默认每目录 10,000 文件／100 MiB 内容预算，超限不返回正常不完整摘要，当前变体关闭增量复用并继续完整渲染。不采用随机摘要、mtime 或只增大阈值，不新增公开 API、配置、依赖或 manifest 字段。只在 `HashUtil` 提供内部预算信号、`VariantManifestPlanner` 捕获该信号复用既有非增量分支；`DirectoryHashCache` 保持生产代码不变，工厂异常不缓存。一般 I/O、权限或其他 InvalidOperationException 不吞成降级；清空渲染复用 entries，但保留匹配输出根的归属及其他 manifest 状态。

独立清单 `openspec/changes/archive/2026-10-30-fix-kd003-hash-budget-fallback/tasks.md`；持久证据根 `R=/Users/ali/.local/state/bukit-verification/kd003-budget-fallback-SkfsyV`。12 个允许路径全部映射、原有 60 个变更路径建立当前基线；范围外不得覆盖。原生产实现下 `R/red/tests.trx` 为 **147 通过 / 21 预期失败 / 0 Skip**，实际退出码 1；其中六项真实 planner→Scriban page/list 输出对照覆盖文件数／大小超限和 child/parent/user 三种布局，同长度且恢复 mtime 后产物仍为旧内容。其余 RED 是不完整摘要、缓存／日志及 manifest 降级契约，不是编译／环境失败。

最终 fixture 使用现有 `DictionaryContentBodyStore` 及 body key 提供真实正文，不走无关的 inline-body 指纹首轮转换路径；页面／列表字节、同长度／同 mtime、降级、恢复普通复用及 ownership 断言均保留。原生产文件精确恢复至任务开始字节后，最终 `R/body-red/tests.trx` 再次得到 **147 通过 / 21 预期失败 / 0 Skip**，六组真实产物均为旧页面字节。原 `R/green/` 的六项普通复用控制失败和 `R/fixture-red/` 的正文供应错误保留为诊断记录，不算通过或最终有效 RED。

最终结果：**SUCCESS（仅本批 KD-003 方案 A）**。`R/body-green/tests.trx` 为 **168 通过 / 0 失败 / 0 Skip**，实际退出码 0；`runtime-green-cache.json` 已记录并检查 HIT，HEAD、闭包内容、确切命令、环境设置状态及 SDK 均匹配。一次专项复核 `R/reviews/specialty-v1.md` 和唯一 delta-only 最终复核 `R/reviews/final-delta-v1.md` 各为 **Critical 0 / Important 0 / Minor 0**，无需补修；最终静态、范围保护、writer 和 metrics 结果记录于 `R/completion.json`。保留原 KD-003 RED 的输入／历史证据；已确认 A 的用例要求超限不提供可复用摘要，不再要求超限仍返回另一个完整摘要。

确切最终 GREEN 命令（最终 RED 同过滤，独立结果目录 R/body-red）：

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~HashUtilTests|FullyQualifiedName~DirectoryHashCacheTests|FullyQualifiedName~LibraryDiagnosticOutputTests|FullyQualifiedName~RenderPipelineTests|FullyQualifiedName~IncrementalBuildEngineTests|FullyQualifiedName~IncrementalBuildEngineAsyncTests" --logger "trx;LogFileName=tests.trx" --results-directory /Users/ali/.local/state/bukit-verification/kd003-budget-fallback-SkfsyV/body-green
git diff --check
```

本批不执行 SCSS、阶段 7、全 Engine、覆盖率、跨平台、全解决方案或 full/release 门禁，不提交／推送／合并；KD-002/004 与阶段 6 原授权结论不扩展也不重跑。

### 阶段 6：Engine 输出与审计

**阶段 6：SUCCESS（2026-10-29 正式判定）。** 依据：Engine 出口——完整 Engine 2934 通过、0 失败、0 KnownDefect Skip，行覆盖 89.92%（§6.10、§6.12）；报告契约出口——24 份报告均有真实生成的正例与 schema 反例，CLI／PluginHost 承接的 6 份报告证据见 §6.11；平台专项——`TaxonomyRedirectWriterTests`、`CleanCommandTests`、`TestInfrastructureTests` 在 Linux x64、macOS arm64、Windows x64 实际执行并通过（CI run `40800207604`，`907544fd`，§6.12）。

阶段 6 不包含以下项目，均不影响本判定：SCSS 历史偶发缺 sass.log 的根因仍未知（已有生产观测，见 §6.9）；全 Core 覆盖率门禁、`gate=core` 与发布门禁不属于阶段 6；阶段 7 及之后未开始。审计中列出的 Minor 项（schema 原地放宽、性能、搜索摘要中的原始标签等）按审计记录保留，不阻塞。

- 内置插件逐个：Alias、Archive、DataFiles、Feed、ImageProcessing、LlmsTxt、Menu、PagesIndex、Pagination、RelatedContent、SearchIndex、Sitemap、Taxonomy、Analytics。
- 搜索索引：HTML 去标签、script 与 style 处理、截断、片段。
- SEO：JSON-LD 各类型、标题模板、canonical、hreflang、alternates、Open Graph。
- GEO：`llms.txt`、`llms-full.txt`，FAQ、HowTo、引用结构。
- 审计：SEO、GEO、发布、机器可读性信任、隐私检查、路由清单。
- 对阶段 0 的当前 Core 报告清单逐项做契约校验，使用各报告声明版本对应的本地 schema。

出口：Engine 达到第 9 节的单项目覆盖率下限；当前 Core 报告清单中的每份报告均有真实生成的通过用例，以及按对应 schema 拒绝错误字段或类型的失败用例。

**现行进度：清单与审计修复见 §6.6–6.7；当前证据为 §6.10（完整 Engine 与覆盖率）、§6.11（CLI／PluginHost／Shared／Architecture）和 §6.12（Linux／macOS／Windows 平台专项）。** §6.1–6.5 保留历史失败、撤回候选和当时 PARTIAL/BLOCKED 状态，不是当前完成证据。

#### 6.1 首批测试与未完成项（2026-10-28）

**阶段 6：PARTIAL，未达到出口。** 本批只补测试和记录，不修改 Core 或公开 schema。证据根 `R` 为 `/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-GrS7jP`；结果与覆盖率按 `R` 中实际回执核对，历史阶段 5 覆盖率不冒充本批结果。

| 项 | 本批证据与边界 |
|---|---|
| KD-007 搜索正文 | `SearchIndexBuilderTests.StripHtmlToText_SeparateBlocks_PreserveTextBetweenThem` 的 script/style 两个实际输入均 RED：预期保留 `visible-between`，实际仅 `visible-after`；见 `R/kd007-red/kd007-red.trx`。当时回归用例标记 `KnownDefect`、`Defect=KD-007` 和明确 Skip；xUnit 对跳过的 Theory 记为一条，不能算修复通过。`MarkdownBodyProjection` 和 `llms-full.txt` 也间接使用此剥离函数；用户后来授权修复，但安全边界阻塞，见 §6.4。 |
| Engine 报告负例 | `TestInfrastructureTests` 读取 basic 站点真实构建产生的 14 份报告，每份先按自身清单绑定 schema 校验原件，再把必填字段改为错误类型并断言该字段被拒绝；含 Analytics v2。该证据不包含 CLI、PluginHost、无本地 schema 的四项，也不等同其余插件/审计行为已覆盖。 |
| 定向回归 | `R/targeted/stage6-targeted.trx`：`SearchIndexBuilderTests` 与 `TestInfrastructureTests` 合计 **80 通过 / 0 失败 / 1 KnownDefect Skip**。首次编译曾因局部变量名冲突失败，改名后通过；没有将 Skip 计作通过。 |
| Engine 项目专项 | 首次带覆盖率的完整 Engine 测试 **2693 通过 / 1 失败 / 4 KnownDefect Skip**，见 `R/engine/engine.trx`。失败为 `BuildAsync_ScssEnabled_PreservesSourceAndCompilesOnceAcrossLanguages` 找不到测试用 `sass.log`；相同覆盖率设置下单独执行 **1/1**（`R/scss-diagnostic/scss-diagnostic.trx`），尚未确定整套失败原因，不能以单测通过替代整套 GREEN，也不能将失败采集作为阶段出口覆盖率证据。后续诊断与最新专项见 §6.2。 |
| CLI 报告 | 四种 CLI 报告均调用真实 writer、读取实际文件并校验对应 schema 及错误类型；三个原先仅内存序列化的用例现验证真实文件。CLI 错误 envelope 同时验证真实入口写到 stderr 的实例及负例；`R/cli-targeted2/cli-targeted.trx` 为 **49 通过 / 0 失败 / 1 KnownDefect Skip**，`R/cli-stderr/cli-stderr.trx` 为 **1/1**。完整 CLI 专项 `R/cli/cli.trx` 为 **1076 通过 / 0 失败 / 2 KnownDefect Skip**（KD-002、KD-011）；测试项目变更的 Architecture owner `R/architecture/architecture.trx` 为 **415/415**。首次 SEO insights 校验发现原测试反射构造了不符合 schema 的占位 route key，后改用符合 schema 的输入，原有其他用例不变。 |
| KD-011 CLI error 契约 | 公开 `CliErrorRenderer.RenderJson` 接受 null command 并省略字段，但本地 schema 要求字段存在；正确行为测试 RED（`R/cli-error-red/cli-error-red.trx`，`$: missing command`），现标 KnownDefect+Skip。真实 CLI 入口无命令时直接显示帮助；未声称该输入来自当前入口。修复需要另行决定 API/schema 兼容方式并授权。 |
| PluginHost 报告 | 正常真实 reporter 输出按显式本地绑定验证 schema，并对 `success` 错误类型做负例；`R/plugin-targeted/plugin-targeted.trx` 为 **5 通过 / 0 失败 / 1 KnownDefect Skip**，完整 PluginHost 专项 `R/pluginhost/pluginhost.trx` 为 **226 通过 / 0 失败 / 1 KnownDefect Skip**。生产 writer 的 `resourceLimitExceeded` 与 `networkPermissionGranted` 两个输入均被本地 schema 拒绝（`R/plugin-report-red/plugin-report-red.trx`，**2 RED**），登记为 KD-012；该报告不在 payload 声明 schema ID。 |
| 待完成 | 当时其余内置插件和实际发布投影、搜索/SEO/GEO/审计行为矩阵、KD-011/012 的公开契约决定，以及每个无本地 schema 项的验收处理仍待完成。`agent-manifest` 当时声明 schema 但缺本地文件；metrics、search-index-index、theme-catalog 无声明和本地 schema。后续用户授权及本地 schema 状态见 §6.3。 |
| KD-006 口径 | 空 FAQ/HowTo 落入 Article 构建分支仍待确认正确行为；Article 的 `citation` 与独立 WebPage 的 `mentions` 同时输出是 `guide/user/17-geo.md` 明示的兼容契约，不将其视为重复缺陷。 |

**内置输出的现有测试入口盘点**：以下数字为 `R/engine/engine.trx` 中对应类的 Passed 行，来自有一项失败的整套运行；仅说明所列测试实际通过，不代表整套 GREEN 或该领域风险矩阵已完成。Feed、Sitemap、SearchIndex、LlmsTxt 不在当前内置插件注册表中，须以发布投影和真实构建为证据，不能只测同名旧插件类。

| 输出 | 现有执行入口与 Passed 行 |
|---|---|
| Alias / Archive / DataFiles | `AliasPluginTests` 8；`ArchivePluginTests` 9；`DataFilesPluginTests` 68 |
| ImageProcessing / Menu / PagesIndex | `ImageProcessingPluginTests` 47；`MenuPluginTests` 5；`PagesByIdDataPluginTests` 6 |
| Pagination / RelatedContent / Taxonomy | `PaginationPluginDerivePagesTests` 14 与 `PaginationPluginCapabilityTests` 1；`RelatedContentPluginTests` 8；`TaxonomyPluginDerivePagesTests` 24 |
| Analytics | `AnalyticsProviderTests` 11、`AnalyticsHtmlTransformTests` 24、`AnalyticsReportWriterTests` 8 |
| Feed | `RssGeneratorTests` 20；`I18nMergedFeedProjectionTests` 5，含真实构建与投影接线 |
| LlmsTxt | `LlmsProjectionIntegrationTests` 1；`I18nMergedFeedProjectionTests` 中真实多语言 GEO 构建通过 |
| SearchIndex | `SearchIndexBuilderTests` 34 Passed、1 KD-007 Skip；`DefaultSearchIndexBuilderTests` 3；真实多语言 search 构建用例通过 |
| Sitemap | `SiteEngineIntegrationTests.BuildAsync_FilteredLists_FromSiteYaml_RendersHtmlSitemapAndSeo` 与多语 hreflang/sitemap 构建用例通过 |

本批定向命令：

```sh
R=/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-GrS7jP

dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter 'FullyQualifiedName~SearchIndexBuilderTests|FullyQualifiedName~TestInfrastructureTests' --logger 'trx;LogFileName=stage6-targeted.trx' --results-directory "$R/targeted"

dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --settings coverage.runsettings --collect 'XPlat Code Coverage' --logger 'trx;LogFileName=engine.trx' --results-directory "$R/engine"

dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --settings coverage.runsettings --collect 'XPlat Code Coverage' --filter 'FullyQualifiedName~BuildAsync_ScssEnabled_PreservesSourceAndCompilesOnceAcrossLanguages' --logger 'trx;LogFileName=scss-diagnostic.trx' --results-directory "$R/scss-diagnostic"

dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --logger 'trx;LogFileName=cli.trx' --results-directory "$R/cli"

dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --logger 'trx;LogFileName=architecture.trx' --results-directory "$R/architecture"

dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release --logger 'trx;LogFileName=pluginhost.trx' --results-directory "$R/pluginhost"
```

#### 6.2 SCSS 与并发日志诊断（2026-10-28）

**阶段 6 仍为 PARTIAL。** 本节证据根 `D` 为 `/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-scss-diagnostic-ks3tlq`；继续只修改测试和计划，未修改 Core 或公开 schema。

| 项 | 诊断结果与边界 |
|---|---|
| SCSS 偶发失败 | 首次失败耗时约 3.196 秒且缺少 `sass.log`；Sass 可执行文件探测的超时为 3 秒，但原失败没有记录探测日志，不能据此认定超时为根因。补充失败时输出 debug/warning 的断言后，同覆盖率设置下 `SiteEngineIntegrationTests` **118/118**，随后两次 Engine 专项中的 SCSS 用例也通过；问题未复现，根因未明。`D/site-engine/site-engine.trx`、`D/engine/engine-diagnostic.trx`、`D/engine-after/engine-after.trx`。 |
| 另一次专项失败 | 修改测试替身前的完整 Engine 测试 **2693 通过 / 1 失败 / 4 Skip**；`BuildAsync_IncrementalBuildDeletesRemovedPages` 在并行页面渲染警告路径中抛 `List<T>.AddWithResize` 异常（`D/engine.log`）。测试私有 `TestLogger` 原用未同步的列表；直接并发回归在修改前 RED，预期 20000 条，实际 15143 条（`D/logger-red/logger-red.trx`）。这证实测试替身并发不安全，且与堆栈一致，但不能仅凭此证明该次异常的全部因果。 |
| 测试替身修正 | 四级日志改为 `ConcurrentQueue<string>`，并保留并发回归；改后回归、增量构建失败用例和 SCSS 用例 **3/3**（`D/logger-green/logger-green.trx`）。公开 `ILogger` 未规定并发要求；Engine 并行渲染会经 `BuildDiagnosticLogger` 转发到调用方 logger，而该转发不串行化。调用方契约或 Core 是否须保障串行化需另行决定，不视为已修复。 |
| 最新 Engine 专项 | 改后带覆盖率运行 **2695 通过 / 0 失败 / 4 KnownDefect Skip**（`D/engine-after/engine-after.trx`）；四个 Skip 分别为 KD-003 的两条、KD-004 和 KD-007，均不算通过。TRX 引用的覆盖率附件按第 1.4 节归并，`Bukit.Engine` 为 **89.73% (14578/16247)**，超过 70% 单项目门槛（`D/engine-after-coverage-summary.json`）；该数值不消除 Skip、未定契约或其余行为矩阵缺口。 |
| 当时待决 schema | `agent-manifest` 已声明 v1 schema 但当时无本地文件；metrics v2、search-index-index v1、theme-catalog 当时无本地绑定。用户后来选择补齐四项，执行结果见 §6.3。theme-catalog 的 `version` 是主题版本，不能当 schema 版本。KD-007、KD-011、KD-012、空 FAQ/HowTo 与 `ILogger` 并发契约仍需单独决定和授权。 |

诊断命令（完整专项在测试替身修正前后各一次，保留两次结果）：

```sh
D=/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-scss-diagnostic-ks3tlq
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --settings coverage.runsettings --collect 'XPlat Code Coverage' --filter 'FullyQualifiedName~Bukit.Engine.Tests.SiteEngineIntegrationTests' --logger 'trx;LogFileName=site-engine.trx' --results-directory "$D/site-engine"
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --settings coverage.runsettings --collect 'XPlat Code Coverage' --logger 'trx;LogFileName=engine-diagnostic.trx' --results-directory "$D/engine"
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~TestLogger_ConcurrentCalls_PreserveAllMessages' --logger 'trx;LogFileName=logger-red.trx' --results-directory "$D/logger-red"
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~TestLogger_ConcurrentCalls_PreserveAllMessages|FullyQualifiedName~BuildAsync_IncrementalBuildDeletesRemovedPages|FullyQualifiedName~BuildAsync_ScssEnabled_PreservesSourceAndCompilesOnceAcrossLanguages' --logger 'trx;LogFileName=logger-green.trx' --results-directory "$D/logger-green"
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --settings coverage.runsettings --collect 'XPlat Code Coverage' --logger 'trx;LogFileName=engine-after.trx' --results-directory "$D/engine-after"
```

#### 6.3 四项本地 schema（2026-10-28）

用户选择补齐四项本地 schema、真实产物与错误类型负例；未授权修改 Core 生产输出或修复既有缺陷。证据根 `S` 为 `/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-schemas-MBpoak`。**阶段 6 仍为 PARTIAL。**

| 项 | 本批结果与边界 |
|---|---|
| 四项绑定 | 新增 `agent-manifest.v1.schema.json`、`metrics.v2.schema.json`、`search-index-index.v1.schema.json` 和 `theme-catalog.schema.json`。前者匹配产物已声明的 schema ID；metrics 和 search-index-index 分别按已有 `version` 2、1 在清单中显式绑定；theme-catalog 无格式版本，清单不把其主题 `version` 当 schema 版本。后三项产物仍不声明 schema ID；本地绑定不代表对应 URL 已部署。 |
| 测试先行 | 四项真实产物测试先因缺失各自 schema **4 RED**（`S/schemas-red/schemas-red.trx`），补齐后 **4/4**（`S/schemas-green/schemas-green.trx`）。复核发现版本负例只测了错误类型，改为同类型错误版本后再次 **4/4**（`S/schemas-version/schemas-version.trx`）。测试读取多语言构建的根及 en/zh-CN agent manifest、metrics、search.index.json，以及主题 writer 写出的非空和空目录；每项校验原件，并拒绝根级和有条目时的嵌套错误类型。三项版本化输出还验证同类型错误版本被拒绝。 |
| 清单检查器 | 原检查器不允许无 payload schema 声明但有版本的显式绑定；新增自检先以 `Invalid explicit local binding` RED（`S/inventory-red.log`），再最小调整检查器。`python3 -B scripts/checks/core-report-inventory.py --self-test` **6 项通过**，当前清单为 **24 项 / 24 个本地 schema**；KD-011/012 契约冲突并未因此修复。 |
| 专项与消费者 | 修改版本负例后的完整 Engine **2699 通过 / 0 失败 / 4 KnownDefect Skip**（`S/engine-final/engine-final.trx`）；主题目录测试 **4/4**、微信同步 loader/invoker 测试 **19/19**、Labs 主题命令测试 **11/11**，分别见 `S/theme`、`S/wechat`、`S/labs`，其直接消费者闭包未受测试代码调整影响。微信消费者现有测试使用自己的旧式 `bukit.agent-manifest` / `1.0.0` fixture，loader 不检查该标识；本批 schema 按当前 Core 声明的 ID / `1.0` 校验，不声称兼容旧式输入或已对本批真实构建做消费者端到端验证。 |
| 未完成 | 本地 schema 使用仓库既有、限定词汇且 fail-closed 的 `LocalJsonSchema` 检查，未用独立 Draft 2020-12 验证器交叉验证。四项本地缺失已补，不代表所有报告现行契约均通过：KD-007、KD-011、KD-012、其他 KnownDefect Skip、SCSS 偶发根因以及 §6.1 的行为矩阵仍未完成。 |

本批确切验证命令：

```sh
S=/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-schemas-MBpoak
python3 -B scripts/checks/core-report-inventory.py --self-test
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ReportSchemaContractTests' --logger 'trx;LogFileName=schemas-green.trx' --results-directory "$S/schemas-green"
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --logger 'trx;LogFileName=engine.trx' --results-directory "$S/engine"
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ReportSchemaContractTests' --logger 'trx;LogFileName=schemas-version.trx' --results-directory "$S/schemas-version"
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --logger 'trx;LogFileName=engine-final.trx' --results-directory "$S/engine-final"
dotnet test tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj -c Release --filter 'FullyQualifiedName~ThemeCatalogWriterTests' --logger 'trx;LogFileName=theme.trx' --results-directory "$S/theme"
dotnet test tests/Bukit.Plugin.WechatSync.Tests/Bukit.Plugin.WechatSync.Tests.csproj -c Release --filter 'FullyQualifiedName~WechatSyncInputLoaderTests|FullyQualifiedName~WechatSyncPluginInvokeCompatibilityTests' --logger 'trx;LogFileName=wechat.trx' --results-directory "$S/wechat"
dotnet test tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj -c Release --filter 'FullyQualifiedName~ThemeCommandTests' --logger 'trx;LogFileName=labs.trx' --results-directory "$S/labs"
```

#### 6.4 KD-007 授权修复受阻（2026-10-28）

**当时 KD-007：BLOCKED，未修复；阶段 6 仍为 PARTIAL。** 用户已授权修复 KD-007。证据根 `K` 为 `/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-kd007-tMUI8h`。真实构建回归确认 `search.json` 丢字；文档 Markdown 和 `llms-full.txt` 也调用此剥离函数，但该基线构建的断言在 search 处即失败，并未分别证明这两个产物的结果。该次生产代码尝试已撤回；用户随后确认扩大至 Markdown 消费路径，结果见 §6.5。

| 证据 | 结果与边界 |
|---|---|
| 原始 RED | `K/red2/kd007-red2.trx`：4 个 script/style 大小写及属性回归和 1 个真实构建回归均失败，实际搜索正文丢失块间可见文字。首次 `K/red/kd007-red.trx` 中的集成失败使用了会转义 HTML 的 Markdown 输入，不作为隐藏块回归证据。 |
| 安全边界 | 简单清除标签状态会把相似结束标签后的隐藏正文暴露出来（`K/security-red/security-red.trx`）；使用现有扫描器的宽松闭合匹配又会接受 `</ script>` 和非 ASCII 空格（`K/invalid-end-red/invalid-end-red.trx`）。恢复原始生产代码后，`K/baseline-red/baseline-red.trx` 直接确认合法换行/制表符属性也会让隐藏块正文进入文本输出；这项风险原本就存在，尚未修复。 |
| 引号属性回归 | 进一步尝试在开标签带有引用中的 `>` 和 `</script>` / `</style>` 时将隐藏正文输出（`K/quoted-red/quoted-red.trx`，2 RED）。恢复本轮前的生产代码后，同两项 **2/2**（`K/quoted-baseline/quoted-baseline.trx`），确认该泄露是本次尝试引入的回归，不能保留该生产改动。先前整套或定向通过只对应中间实现，不是当前修复证据。 |
| Markdown 消费路径的独立风险 | 对恢复后的 Release 二进制直接调用方法（`K/consumer-check/observed.txt`）：输入 `<script>hidden-before</ script><a href="/private">hidden-link</a>hidden-after</script>visible`，完整输入的 `StripHtmlToText` 返回 `visible`，但 `MarkdownBodyProjection.FromHtml` 输出隐藏正文和链接。宽松扫描将 `</ script>` 视为结束，再在剥离前提取内部链接，因此单改剥离函数无法阻止该路径的泄露。`llms-full.txt` 调用同一 Markdown 投影，但尚未对该具体输入运行真实构建验证。 |
| 当时测试状态 | 保留安全回归和待修用例；当时基线本就失败的用例明确标记 `KnownDefect`、`Defect=KD-007`、Skip，Skip 不算通过。`InvalidClosingTag` 与 `QuotedOpeningAttribute` 保持启用，以防后续修复引入已观察到的泄露。恢复后定向运行 **20 通过 / 0 失败 / 5 Skip**（`K/baseline-status/baseline-status.trx`），未在该次恢复后重新运行完整 Engine。 |
| 待决定 | 逐个补丁已暴露解析器结构性问题。建议在 `StripHtmlToText` 内正确识别引用属性及严格的 script/style 边界，安全处理未闭合隐藏块；同时在 `MarkdownBodyProjection` 处理上述独立的隐藏链接泄露，保留注释、实体、空格及合法链接的现有回归。不增加依赖，不改变共享扫描器、其他缺陷或公开 schema；实现前需用户确认扩大至 Markdown 消费路径。 |

#### 6.5 扩大授权后的解析器验证（2026-10-28）

**历史状态：当时 KD-007 为 BLOCKED，阶段 6 为 PARTIAL；现行决议和验证见 §6.6。** 用户当时确认同时处理剥离函数和 Markdown 隐藏链接，不新增依赖、不修改共享扫描器。证据根 `A` 为 `/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-kd007-authorized-5wNoGe`。

| 证据 | 结果与边界 |
|---|---|
| 基线 RED | 启用原有回归并补充隐藏链接、引用属性、大小写、空白、脚本转义和真实构建后，`A/red/red.trx` 为 **24 失败 / 64 通过**；失败包括丢字和 Markdown 提取隐藏链接。 |
| 中间候选 | 无依赖的有界扫描候选曾通过定向 **95/95**（`A/focused-final/focused-final.trx`），完整 Engine 为 **2740 通过 / 0 失败 / 3 KnownDefect Skip**（`A/engine/engine.trx`）。这些结果只属于随后撤回的候选，不是当前修复或阶段出口证据。 |
| 候选回归 | 候选在 `textarea` 等原始文本中把脚本示例当成真实标签，丢掉后续内容：`A/raw-context-red/raw-context-red.trx` 为 **8 RED**，恢复基线后同八项均通过。SVG CDATA 与带引号属性组合下，候选把实际 script 元素内的正文和链接输出：`A/svg-red/svg-red.trx` 为 **2 RED**；恢复基线后搜索剥离用例通过，但 Markdown 用例仍失败（`A/baseline-red/baseline-red.trx`）。Chromium `DOMParser` 确认该输入中的标记位于 SVG script 元素内。 |
| 参考解析器边界 | Chromium 对照的 1728 个脚本边界组合未暴露候选中的隐藏标记或丢失可见标记（`A/browser-reference-summary.txt`），但不覆盖上述 SVG 反例，也不证明完整 HTML 安全。仓库其他项目使用的 AngleSharp 在 10 个畸形脚本转义组合上与 Chromium 对可见链接的判断不同（`A/reference.log`），不能未经验证直接视为安全替代。 |
| 当前状态 | 本轮两处生产代码均已恢复至尝试前内容；保留新增回归。恢复后失败的测试方法标 `KnownDefect`、`Defect=KD-007` 和明确 Skip，基线通过的安全回归保持启用。恢复后定向运行 **78 通过 / 0 失败 / 12 Skip**（`A/baseline-status/baseline-status.trx`），Skip 不计通过；未在本轮回滚后重跑完整 Engine。 |
| 下一步 | 自行实现的有界扫描器已出现多处结构性边界，不能继续以局部通过认定安全。需用户决定是否授权评估并在证据支持时为 Core 增加合适的 HTML 解析依赖，或明确接受保守丢弃部分可见文本；任何候选都必须先通过 SVG、双重转义、真实产物及既有回归。 |

#### 6.6 审计前按指定顺序完成阶段 6 的历史证据（2026-10-28）

**历史记录，当前验收见 §6.7。** 独立审计曾因 GEO 原始条目与 template/noscript/raw-text 投影反例重新打开 3/4/5；其修复与新专项／复核已在 §6.7 闭环。以下原执行数字、失败及复核均保留为历史证据，不冒充修复后的当前验收。

用户要求严格按“行为矩阵 → KD-011/012 → FAQ/HowTo 与 logger → 独立 KD-007 → 报告、专项、覆盖率”执行。本轮在 `codex/stage6-completion` worktree 实施，原工作树 24 个未提交文件逐字节保留；不自动提交、推送或合并。唯一实施清单为 `openspec/changes/archive/2026-10-30-complete-core-stage6-validation/tasks.md`，行为及 24 报告正反例台账见 `docs/testing/core-stage6-behavior-matrix.md`。

证据根 `R=/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-completion-8WEr6X`。以下结果仅对应本轮，不复用已撤回候选的通过结果。

| 顺序 | 决议与证据 |
|---|---|
| 1 行为矩阵 | 逐项盘点十个现行插件与 Feed/Sitemap/Search/Llms 发布投影，核对 76 个测试方法引用；`01-owner.json`。盘点不是运行通过证明。 |
| 2 KD-011/012 | 先观察 CLI 1 RED、reporter 2 RED；只从 CLI schema required 移除 command，并声明 reporter 两个可选字段，保持输出字节和版本。CLI 37/37、PluginHost 26/26、Architecture owner 5/5；`02-*`。错误类型与未知字段仍拒绝。 |
| 3 FAQ/HowTo、logger | 空集合不生成无效 FAQPage/HowTo，不伪造 Article；warn/strict/off 分别处理 missing-list 诊断。8 RED/2 个既有 off-mode 通过 → GEO/诊断/报告消费者 85/85。ILogger 明确要求线程安全，不保证跨 worker 顺序；ConsoleLogger 21/21，含 2000 条并发 JSON；`03-*`。citation 与独立 mentions 并存保留。 |
| 4 独立 KD-007 | 原实现 30 RED/103 通过；复用中央已固定的 AngleSharp 1.5.2（Engine 新引用，无新版本），DOM 处理 namespace/CDATA/raw-text，源范围防护已证实的脚本转义缺陷。定向 133/133、0 Skip，含 search/Markdown/llms-full 三个独立真实构建断言；Architecture owner 43/43。与 Chromium 参考的 1728 组合及 Unicode/换行前缀变体共 6912 输入零差异；`04-*`。无 Native AOT 发布证明。 |
| 5 收口 | 清理 5 个过期 knownGap，逐项绑定 24 报告真实生成正反例。完整 Engine 2764 通过/0 失败/3 个既有 KnownDefect Skip，行覆盖 14686/16335 = 89.91% ≥70%；CLI 100/100、PluginHost 26/26、Shared ConsoleLogger 21/21、Architecture 直接 owner 48/48。清单自检 6/6。见 `05-verification-summary.json` 与 `05-*/` TRX；第五步专项及唯一最终增量复核均无 Critical/Important，实施清单已全部完成。 |

首轮完整 Engine 发现旧 `SeoPublisherJsonLdTests` 使用空 FAQ 来检查 publisher，2763 通过/1 失败/3 Skip；补真实问答并保留 publisher 断言后，直接消费者 51/51，完整重跑得上表结果。首轮失败覆盖率不计出口。矩阵 76 个引用在当前 TRX 均 Passed；24 报告逐项真实原件通过，字段错误类型被拒绝。

SCSS 处置：完整 Engine 本次通过，保留缺 sass.log 时的诊断；历史偶发失败根因仍未明，之后通过不构成根因修复。KD-002/003/004 不在本轮修复授权内，3 个 Engine Skip 不计通过。阶段 7 二十次稳定性、其他平台、全 Core 覆盖率、全解决方案和发布门禁均不在本轮完成声明内。

本轮最终确切专项命令（R 取本节证据根）：

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --settings coverage.runsettings --collect "XPlat Code Coverage" --logger "trx;LogFileName=engine.trx" --results-directory "$R/05-engine-after"
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter "FullyQualifiedName~ExternalAuthorityReportWriterTests|FullyQualifiedName~GenerativeCitationReportWriterTests|FullyQualifiedName~SeoInsightsReportWriterTests|FullyQualifiedName~SeoQuestionInsightsReportWriterTests|FullyQualifiedName~CliErrorRendererTests|FullyQualifiedName~ProgramEntryPointTests|FullyQualifiedName~SeoReportValidatorTests" --logger "trx;LogFileName=cli.trx" --results-directory "$R/05-cli"
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release --filter "FullyQualifiedName~PluginLockAndReportTests|FullyQualifiedName~PluginProtocolClientTests" --logger "trx;LogFileName=plugin.trx" --results-directory "$R/05-plugin"
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c Release --filter FullyQualifiedName~ConsoleLoggerTests --logger "trx;LogFileName=shared.trx" --results-directory "$R/05-shared"
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --filter "FullyQualifiedName~DependencyMatrixTests|FullyQualifiedName~EngineModuleBoundaryTests|FullyQualifiedName~Ad03C4ContentNotionBridgeInternalizationTests|FullyQualifiedName~G04D2GExecutionReportTests" --logger "trx;LogFileName=owner.trx" --results-directory "$R/05-owner"
python3 -B scripts/checks/core-report-inventory.py --self-test
git diff --check
```

覆盖率仅取成功完整 Engine TRX 引用的附件，按路径/行号归并并以原始比例判断 70%；`05-verification-summary.json` 保存附件路径、24 项报告、76 引用与 Skip 明细。代码稳定后未为刷新缓存重跑 fixture；首次失败和修正后的必要重跑分别保存，不混合统计。详细 closure、classification、缓存与 phase 指标均在 R。

第五步只读专项复核 `05-specialty-review.md` 无 finding。`final-review-scope.json` 只生成一次，唯一最终增量复核 `final-unified-review.md` 批准收口：Critical 0 / Important 0 / Minor 1，唯一 Minor 是第四步已记录的无行为影响测试冗余块，无新增发现。此前确切命令缓存随后续闭包变化失效，最终完整专项替代它们；没有将历史缓存或撤回候选称为当前命中。最终仅更新结果状态，并静态核对 974 项实现输入仍为被验证的字节；不重跑未变化 fixture，不自动提交、推送、合并或归档变更。

#### 6.7 独立审计后的最小修复与当时证据（2026-10-28；证据根已清理，当前证据见 §6.10–6.11）

**阶段 6：本轮授权范围 COMPLETE；七项审计问题已修复并通过规定复核。** 用户授权深入定位并实际修复审计 I-01/I-02/I-03、M-01–M-04；现有 worktree 内单 writer 串行实施，保留审计前 44 个变更文件及原 974 项输入在授权范围外的字节，不自动提交。OpenSpec 3/4/5 均已完成；一次 combined spec-then-quality 专项复核与一次 delta-only 最终复核均为 0 Critical / 0 Important / 0 Minor。此结论不扩展到继承缺陷或全 Core／发布验收。

当前证据根 `R=/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-repairs-APNcZD`。闭包、分类、确切命令和原始 baseline 均先生成；21 个允许路径全部映射，无新增未映射文件。执行设计与确切 RED/GREEN 命令见 `docs/plans/2026-10-28-core-stage6-audit-repairs.md`，完整根因及替代方案见 R/implementation-report.md。

| 修复 | 最小实施与可见证明 |
|---|---|
| I-01 GEO 原始条目绕过诊断 | parser 不再丢弃缺 question/answer、name/text 或非 map/null 条目，复用现有模型的空字符串交由既有 warn/strict/off 诊断。仅 JSON-LD 过滤无效条目，全无效无专用节点／无 Article 回退，合法 step 连续编号；真实 raw map→ContentDocument→BuildForContent→AnalyzeIndex。未改公开 Rendering 模型、schema、citation/mentions、publisher 或 author 策略。 |
| I-02 HTML template 锚点作用域 | 用实际 IHtmlTemplateElement 与其 Content 转移状态保存／恢复外层锚点，非模板标签名深度计数；保留嵌套／未闭合／空模板、SVG 自闭合与 foreignObject 对照，以及真正畸形锚点最内层目标策略。初始两个额外 foreign-template 断言误当惰性 HTML 作用域，调查后改为既有非惰性策略对照，原 RED 保留；未放宽审计主反例。 |
| I-03 noscript 字面链接 | IsScripting=true 下保留字面文本，像其他 raw-text 一样归一化后再供 Markdown 扫描；实际 LinkInline 仅真实 `/visible`，不是宣称可执行 XSS 已被证明。 |
| M-01 搜索 raw-text 粘字 | 替换 textarea 没有 SourceReference，文本遍历补其边界；xmp/iframe/noembed/noframes 的 search／Markdown 精确保持 `before example after`，不改隐式 html/head/body 边界。 |
| M-02/M-03/M-04 测试证据 | RelatedContent 禁用有两个匹配文档及显式索引／阈值，禁用和单文档均检查实际 RelatedPages；Archive 改证实 collection 边界，空内容测试承担矩阵不输出列，未新增缺日期拒绝策略；移除继承冗余 brace block。两个插件生产实现均未改，未跑 mutation gate。 |

| 确切命令 key | 实际结果 | 原始证据（相对 R） |
|---|---|---|
| geo-red | 67 Passed / 72 Failed / 0 Skip，预期 RED | `geo-red.log`、`geo-red/tests.trx` |
| geo-green | 139 Passed / 0 Failed / 0 Skip | `geo-green/tests.trx`、`geo-green-receipt.json` |
| projection-red | 157 Passed / 18 Failed / 0 Skip | `projection-red.log`、`projection-red/tests.trx`；foreign oracle 更正单列 |
| projection-green | 176 Passed / 0 Failed / 0 Skip | `projection-green/tests.trx`；增加一个空 HTML template 对照，保留所有旧 script/style/CDATA/entity/unsafe-URI 回归 |
| 完整受影响 Engine 专项 | **2881 Passed / 0 Failed / 3 inherited NotExecuted** | `engine.log`、`engine/engine.trx`、`engine-receipt.json` |
| Engine-only 行覆盖 | **14702/16351 = 89.91% ≥70%** | 成功 TRX 自身唯一 Cobertura 附件；路径+行号去重，`final-evidence.json` |

完整 Engine 确切命令：

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --settings coverage.runsettings --collect "XPlat Code Coverage" --logger "trx;LogFileName=engine.trx" --results-directory /private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/bukit-stage6-repairs-APNcZD/engine
```

新增 117 个用例（84 GEO、33 projection）；template/noscript/xmp × search/Markdown/llms-full 九个真实构建分别断言，后两者检查实际 LinkInline 目标。矩阵当前 88 个唯一方法引用均在新完整 Engine TRX Passed，引用数量不当作行为充分性证明。原三个 KD-003/004 未执行项不算通过；SCSS 本次通过，历史偶发根因仍未明。

首次 Engine 后台工具调用误设 60 秒 timeout 导致进程终止、无 TRX；已确认无残余 testhost，失败日志／回执保留为 `engine-interrupted.*`，未改运行输入，以无工具 timeout 重启同一命令后得到上表证据。中断采集不用于覆盖率或 GREEN，不属于刷新缓存重跑。首个 GEO wrapper 的错误队列转换亦在报告保留，不重跑原 RED。

成功确切命令缓存使用 HEAD、957 项 runtime-closure 输入、命令、SDK 10.0.100 和环境变量名称／状态（不存值）；`runtime-frozen-inputs.json` 冻结成功完整运行输入。GEO focused cache 随后续投影测试变化失效，只保留历史 TDD，完整 Engine 承担当前证明，不重跑 fixture 刷新。原 974 项输入在授权路径外不变，原 44 个变更中授权路径外 28 项逐字节保留，HEAD 不变；仅文档状态写入不改变运行证据。

§6.6 的 CLI 100/100、PluginHost 26/26、ConsoleLogger 21/21、Architecture 48/48 是未变更领域的历史结果，本次未重跑，不冒称当前完整闭包 HIT。只运行授权的受影响完整 Engine 专项与 `git diff --check`；无全解决方案／发布门禁、新平台／浏览器矩阵、AOT 或二十次稳定性声明。

当前 R 的 `specialty-review.md` 独立接受七项修复，`final-review-scope.json` 仅生成一次；唯一 `final-unified-review.md` 复用专项证据，只检查公共契约衔接与验收元数据，无失效证据、未覆盖变更或开放阻断项，批准状态回填。两次复核均为 0 Critical / 0 Important / 0 Minor。最后仅更新四份验收状态文档，核对 957 项成功运行输入仍匹配，并执行静态差异检查；没有再次运行 fixture 或扩大门禁。

#### 6.8 独立 SCSS 有界诊断（2026-10-29）

KD-003 方案 A 收口后，用户“继续执行”，按剩余风险计划单独执行任务 3，不重开阶段 6，也不进入阶段 7。仅增强原 `BuildAsync_ScssEnabled_PreservesSourceAndCompilesOnceAcrossLanguages`：假工具分别记录 probe/compile trace，原 `sass.log` 保持 compile-only；失败及成功均在 fixture 清理前保存脱敏日志、源文件／三语言 CSS 状态和总耗时。通过不启动进程／构建的合成失败自检验证同一保存路径，随后删除仅自检创建的输入；不添加实际构建 warm-up／重试。

原有源 SCSS、compile 恰好一次及各语言 CSS 精确内容断言保留，生产 3 秒／5 秒超时及其他生产代码不变；`ExternalToolProcessRunnerTests` 不改，只按原计划完整运行。独立清单 `openspec/changes/archive/2026-10-30-diagnose-scss-missing-log/tasks.md`；证据根 `R=/Users/ali/.local/state/bukit-verification/scss-causal-diagnosis-z6Jmp9`。

唯一带覆盖率专项 `R/diagnostic/tests.trx`：**20 通过 / 0 失败 / 0 Skip**、实际退出码 0。其中 `ExternalToolProcessRunnerTests` 19 个 TRX Passed 包含 1 个 Windows-only NOT_APPLICABLE（macOS 未执行其分支），另 1 个为原 SCSS 集成用例；不把它冒充 Windows 证明。真实构建及断言约 704 ms，probe/compile trace 各记录一次，`sass.log` 仍只有一条 compile，源及 en/fr/de CSS 原断言通过。合成异常由同一保存路径保留原异常、三份 sentinel trace、脱敏消息及缺失 CSS 状态，未启动额外进程／构建；与实际构建快照分开保存在 `R/captures/`。覆盖率附件原样保留（两处同哈希副本属于一次运行），不据此声称全 Engine 覆盖率出口。`R/diagnostic-analysis.json` 记录可观测事实及缺证；运行 GREEN 已缓存并检查 HIT。专项复核 `R/reviews/specialty-v1.md` 和唯一最终增量复核 `R/reviews/final-delta-v1.md` 各为 **Critical 0 / Important 0 / Minor 0**，无需补修或重跑；最终静态、范围保护、writer 及 metrics 记录于 `R/completion.json`。**有界诊断已收口；定因仍为 PARTIAL，本次未复现、历史根因仍未知，不标已修复**。进一步生产观测补证／修复须另行定界授权，不自动启动阶段 7。

```sh
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --settings coverage.runsettings --collect "XPlat Code Coverage" --filter "FullyQualifiedName~ExternalToolProcessRunnerTests|FullyQualifiedName~SiteEngineIntegrationTests.BuildAsync_ScssEnabled_PreservesSourceAndCompilesOnceAcrossLanguages" --logger "trx;LogFileName=tests.trx" --results-directory /Users/ali/.local/state/bukit-verification/scss-causal-diagnosis-z6Jmp9/diagnostic
git diff --check
```

测试专用 `BUKIT_TEST_SCSS_EVIDENCE` 由执行器指向 R 下的持久目录；测试不保存该变量或 PATH 的值，只保存设置状态和匹配布尔量。shell trace 的退出标记是分支自报，不代表已观测 wrapper 退出；成功探测／编译的独立进程退出码、stderr、耗时及调度延迟仍不可观测，明确记录为缺证。本机有界通过也不证明历史偶发故障已修复，不恢复或重跑旧临时附件。

#### 6.9 独立 SCSS 生产观测补证（2026-10-29）

用户在阶段范围澄清中明确选择 SCSS 生产观测补证，并批准新 Debug 事件全文遮蔽 stderr。**本批生产观测范围 SUCCESS，已收口；历史根因仍未知。** 阶段 6 原清单及七项审计修复保持已完成；本批不是重复审计或历史根因修复。§6.8 保留为当时诊断快照，成功返回进程的退出码／调用耗时缺口由本节补齐，不改其历史结论。独立计划 `docs/plans/2026-10-29-scss-process-observability.md`、OpenSpec `openspec/changes/archive/2026-10-30-observe-scss-process-attempts/`；证据根 `R=/Users/ali/.local/state/bukit-verification/scss-process-observation-Ycjxhu`，十个允许路径已映射，原有 73 个变更建立当前基线；最终静态、范围保护、writer 及 metrics 结果记录于 `R/completion.json`。

仅 `ScssCompiler` 的两次调用复用同一个私有观测边界，输出配对 attempt ID、probe/compile、逻辑 sass/dart-sass、原超时参数、真实返回退出码及调用耗时；抛出时保持原异常并标明 exit/stderr unavailable。新 Debug 完全遮蔽 stderr 正文，只留状态和返回诊断字符串的 UTF-16 长度；不记录环境值、参数、路径、stdout、内容摘要或异常消息。补充日志 best-effort，不让新接收器异常取代工具结果。原 runner、3 秒／5 秒、选择顺序、取消、CSS 检查及 warning／Info 字节不改；原失败 warning 仍遵循其旧 stderr detail 行为，不冒称已统一脱敏。

| 证据 | 结果与边界 |
|---|---|
| `R/red/tests.trx` | 10 项缺事件预期失败／93 通过／0 Skip，实际退出码 1；原生产精确基线，旧行为及真实源／CSS 断言均先通过。不是历史缺日志复现。 |
| `R/green/tests.trx` | **104 通过／0 失败／0 Skip**，实际退出码 0：runner 类 29（含十个新增场景）、诊断日志类 74、原多语言用例 1；1 个 Windows-only NOT_APPLICABLE 不算 Windows 证明。RED 后额外加入 failing-Debug-sink 控制，解释数量增加 1。 |
| 实际进程观测 | `R/captures/green/` 的真实构建 probe／compile 各返回 exit 0，调用耗时 163／26 ms；总构建及断言 521 ms，源内容与三语言 CSS 保留。`R/green-analysis.json` 记录原始出处、覆盖率附件及缺证。 |
| 缓存／复核 | 运行 GREEN 已按 HEAD、闭包字节、确切命令、环境状态和 SDK 记录并检查 HIT；专项复核 `R/reviews/specialty-v1.md` 及唯一 delta-only 最终复核 `R/reviews/final-delta-v1.md` 各为 Critical 0／Important 0／Minor 0，无需补修或重跑。 |

受控成功、fallback、拒绝、编译失败、缺 CSS、超时、取消及日志接收器故障验证的是观测与旧结果保持，不等于历史故障复现或修复。原始 stderr 有意遮蔽，抛错时 runner 未暴露结果、物理工具解析、OS 调度延迟仍不可观测；耗时含包装／清理，不能当作 Sass CPU 或超时根因。用户指南新增字段说明；不改公开 API／schema／配置／依赖／CI，不跑全 Engine、跨平台、稳定性循环、阶段 7 或发布门禁。

#### 6.10 当前分支完整 Engine 出口复验（2026-10-29）

**Engine 出口证据：GREEN（本机 macOS arm64）；阶段 6 跨平台验证仍为缺口。** 只读审计发现：§6.7 的 2881 Passed／89.91% 证据根位于系统临时目录且已被清理，并早于 KD-003（`HashUtil`、`VariantManifestPlanner`）和 KD-004（`TaxonomyRedirectWriter`）的 Engine 改动；当前分支移除全部 KnownDefect Skip 后从未整体运行。用户授权对 `codex/stage6-completion` 的 `79df55f8` 运行一次完整 Engine 专项及覆盖率。持久证据根 `R=/Users/ali/.local/state/bukit-verification/stage6-engine-current-EzuJkM`；闭包 80 个变更路径全部映射（`R/closure.json`），分类为 fixture-exclusive，单独运行，无缓存可复用。

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --settings coverage.runsettings --collect "XPlat Code Coverage" --logger "trx;LogFileName=engine.trx" --results-directory "$R/engine"
```

| 证据 | 结果与边界 |
|---|---|
| 完整 Engine 专项 | **2934 Passed／0 Failed／0 Skip**，实际退出码 0，约 111 秒；TRX 逐行 2934 条均为 Passed，无 NotExecuted。见 `R/engine/engine.trx`、`R/engine.log` |
| KnownDefect | **0 个 Skip**。KD-002/003/004/007/011/012 相关用例首次在同一次完整运行中全部启用并通过 |
| 平台 N/A | 1 个：`ExternalToolProcessRunnerTests.RunAsync_WindowsNativeProcess_CapturesOutputAndExitCode` 在 macOS 记为 Passed，不算 Windows 证明 |
| Engine 行覆盖 | **14701/16349 = 89.92% ≥70%**；Engine.Assets 990/1200 = 82.50%。仅用成功 TRX 引用的 `In/*/coverage.cobertura.xml`（与 collector 副本 SHA-256 相同），按 `scripts/checks/coverage/summarize.py` 路径+行号归并，见 `R/coverage/coverage-summary.json`。汇总脚本对其他项目的低值与缺失报错来自顺带插桩，不是全 Core 门禁结论 |
| 缓存／指标 | `R/engine-cache.json` 按 HEAD、闭包内容、确切命令、环境变量名状态和 SDK 10.0.100 记录并检查 HIT；`R/metrics.json` 记录 test 阶段；汇总见 `R/final-evidence.json` |

未覆盖：CLI、PluginHost、Shared、Architecture 的改动只有 §6.6 的历史专项结果（其证据根同样已清理），本次未重跑；Linux／Windows `platform-tests`（`TestInfrastructureTests`、`ExternalToolProcessRunnerTests` 已改动但未在两平台运行；`TaxonomyRedirectWriterTests`、`CleanCommandTests` 尚未接入，前者的 `SkipException.ForSkip` 在 xUnit 2.9.3 下不等于 Skip）；全 Core 覆盖率、阶段 7 及发布门禁。SCSS 历史根因仍未知。

#### 6.11 CLI、PluginHost、Shared 与 Architecture 当前专项（2026-10-29）

**报告契约出口：24 份报告均有当前通过证据；阶段 6 仅剩跨平台缺口。** 复审发现 §4 台账中 6 份报告（`seo-insights-report`、`seo-question-insights-report`、`generative-citation-report`、`external-authority-report`、`cli-error`、`plugin-execution-report`）由 CLI／PluginHost 测试承接，其通过证据只存在于已清理的 §6.6 临时目录。经授权对 `800472a2`（相对 §6.10 仅文档变化）补跑 §6.6 的四条专项命令；CLI 过滤额外加入 KD-002 后改动的 `CleanCommandTests|CliContractTests`。持久证据根 `C=/Users/ali/.local/state/bukit-verification/stage6-consumers-current-2ZqUpM`，确切命令见 `C/commands.json`，闭包无未映射文件，四条命令均为 dotnet-serial，依次运行。

| 专项 | 结果 | 证据（相对 C） |
|---|---|---|
| CLI（四个报告 writer、`CliErrorRendererTests`、`ProgramEntryPointTests`、`SeoReportValidatorTests`、`CleanCommandTests`、`CliContractTests`） | **141 Passed／0 Failed／0 Skip**，退出码 0 | `cli/**/cli.trx`、`cli.log` |
| PluginHost（`PluginLockAndReportTests`、`PluginProtocolClientTests`） | **26／0／0** | `plugin/**/plugin.trx` |
| Shared `ConsoleLoggerTests` | **21／0／0** | `shared/**/shared.trx` |
| Architecture 直接 owner（4 类） | **48／0／0** | `owner/**/owner.trx` |

四组均无 NOT_APPLICABLE；清单为上述 6 份报告绑定的全部测试方法均在 TRX 中为 Passed。四组均按 HEAD、闭包内容、确切命令、环境变量名状态和 SDK 10.0.100 记录缓存并检查 HIT（`C/*-cache.json`），指标见 `C/metrics.json`，汇总见 `C/final-evidence.json`。未运行：Linux／Windows `platform-tests`、全 Core 覆盖率、阶段 7 及发布门禁。

#### 6.12 三平台 `platform-tests` 接线与 CI 证据（2026-10-29）

**阶段 6 平台专项：GREEN（Linux x64／macOS arm64／Windows x64）。** 复审 I-2：`TestInfrastructureTests`、`ExternalToolProcessRunnerTests` 已改动但未在两平台运行，`TaxonomyRedirectWriterTests`、`CleanCommandTests` 未接入 `platform-tests`。经授权完成接线并推送 `codex/stage6-completion`，以 `workflow_dispatch gate=platform` 运行。`SkipException.ForSkip` 在 CI 中不会被当作通过：任何非 Passed 行都使平台验收失败，因此无需修改测试。

| 提交 | 内容与验证 |
|---|---|
| `387ffb71` | 两个类加入三平台 `platform-tests`；owner 自检 `test-infrastructure-platform-self-test.py` 加两个变异（共六个）通过，`CoverageGateTests` 32/32。证据 `/Users/ali/.local/state/bukit-verification/stage6-platform-wiring-TlrACb` |
| `4b93d968` | 修正 `main` 既有断链（`relocate-template-theme` 提案已归档），fast gate 文档链接检查通过 |
| `3a99dcce` | 清除本阶段 Core 改动新增的 5 条分析器回归（IDE0042、CA1502、CA1859、CA1865、CA2000），只做行为不变的重构；ratchet 回到基线 657/657、421/421。完整 Engine **2934／0／0 Skip**，Engine 行覆盖 **89.92%（14704/16352）**，缓存 HIT；证据 `/Users/ali/.local/state/bukit-verification/stage6-engine-after-analyzer-ilnpp9` |
| `f19c08d0` | 阶段 4 `b11fc4fe` 新增的公开重载 `RoutePathBuilder.NormalizeUrl(string, bool)` 改为 internal（public API drift 检查拒绝）；Routing 不得添加 friend assembly，故移除直接调用它的单元 theory，可达行为由 `RouteGeneratorTests` 经公开路径覆盖。Routing 108/108、Architecture 守卫 26/26、API drift OK；证据 `/Users/ali/.local/state/bukit-verification/routing-normalizeurl-internal-xHkEw3` |
| `907544fd` | Windows 将 POSIX-only 的 `ScssCompiler_ProcessObservation_PreservesOutcomesAndMasksStderr` 报为 N/A，加入既有 POSIX 允许名单；该类 12 个 Unix-only 方法与名单逐一对齐 |

最终 CI run `40800207604`（HEAD `907544fd`，SDK 10.0.403）：fast gate、Architecture contracts、fast contracts 与三平台全部 success。首次尝试 macOS 因 runner 无法访问 `api.nuget.org`（NU1301 No route to host）未能还原，属环境故障，未改代码，仅重跑失败任务（attempt 2）通过。

| 平台 | PluginHost | Engine（执行／N/A） | Cli（执行／N/A） | Shared（执行／N/A） |
|---|---|---|---|---|
| Linux x64 | 51/51 | 141／1 | 29／0 | 不选 |
| macOS arm64 | 51/51 | 156／1 | 196／0 | 41／1 |
| Windows x64 | 51/51 | 136／21 | 195／1 | 40／2 |

所有 TRX 行均为 Passed，无选中类适用数为零。`TaxonomyRedirectWriterTests` 27、`CleanCommandTests` 29、`TestInfrastructureTests` 43 在三平台均实际执行；`ExternalToolProcessRunnerTests` 在 Linux／macOS 执行 28、Windows 执行 8，其余为允许名单内 POSIX 进程用例。逐平台 `platform.json`、`dotnet-info.txt` 与 TRX 见 `/Users/ali/.local/state/bukit-verification/stage6-platform-ci4-RnFDSz/artifacts`，汇总 `final-evidence.json`；中间失败的 run `40795879565`、`40797555266`、`40798930266` 只作诊断记录。

未运行：全 Core 覆盖率门禁、`gate=core`、阶段 7 及发布门禁。`f19c08d0` 未重跑完整 Engine：仅改变 Routing 内部可见性，Engine 不调用该重载，API drift 检查已完整编译解决方案。

### 阶段 7：管线集成与端到端

- 三个 fixture 站点完整构建，公共产物与 golden 快照按第 4.3 节逐字节比较，报告独立校验。
- 确定性：连续构建两次的公共产物一致；改动单文件后，增量与相同最终输入的全量构建公共产物一致，分别验证缓存与渲染统计。
- 多语言根聚合：sitemap、feed、llms、robots、search、agent manifest。
- 失败注入：渲染中途失败、磁盘满、插件崩溃，验证正式输出不被破坏。
- 并发：并行与串行渲染的公共产物一致，各自报告的计数和哈希正确。

出口：golden 与确定性测试稳定通过，连续运行 20 次无抖动。

#### 阶段 7 首批执行记录（2026-10-29）

**状态：PARTIAL。** 从已合并阶段 6 的 `main` 提交 `3bcc6e068c997aa5a8d7a896ba15a9a7641deda7` 创建独立工作区；本批只新增 golden 测试与快照，不修改 Core 生产代码、原始 fixture 或公开契约。

`Stage7GoldenTests` 复用现有 `SiteBuilder` 在隔离目录构建三个站点，以固定时钟、发布日期和站点 URL 生成可重复输入；taxonomy 站点还在隔离副本中设置分页及分类。测试先独立校验报告，再逐字节比较完整公共文件路径集合；另有路径增删与字节变化的负例。基准不在测试运行时更新。

| 证据 | 实际结果 | 口径 |
|---|---|---|
| macOS arm64 | **47 通过、0 失败、0 跳过** | `Stage7GoldenTests` 4 + `TestInfrastructureTests` 43；Windows JSON 外层换行在此后规范化，不影响本机读取的 Unix 基准 |
| [Linux / Windows 原生采集 #40809595691](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40809595691) | 各 **46/46** | 候选采集与基础设施测试，不计为 golden 复验 |
| [Linux / Windows golden 复验 #40811061112](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40811061112) | 各 **4/4**，0 失败、0 跳过 | 实际 golden 测试；证据提交 `b89239113b8ef9cf445a78660ab216a0be4bcc0f`，989 项输入与主工作区一致，SDK 10.0.100、runtime 10.0.0 |

三个站点分别固定 12、28、33 个公共文件。Linux 与 macOS 使用相同 `unix/` 基准；Windows 路径集合相同但分别有 10、23、31 个文件字节不同，使用原生采集的 `windows/` 基准，不对产物字节做换行归一化。详细命令、TRX 和证据位置见 `2026-10-29-core-stage7-golden-start.md`；原生证据工作流只存在于临时证据分支。

**仍待完成：**连续两次构建和单文件修改后的增量／全量等价、缓存与渲染统计、并发、三类故障注入、完整多语言聚合和连续 20 次稳定性。现有 i18n fixture 的根输出包含 sitemap、search、llms、agent manifest，但不包含根 feed 或 robots。`PluginRegistry.BuildPlugins` 当前仅注册内置插件，尚不能将 PluginHost 进程崩溃单测当作真实构建管线的故障注入证据。未运行全量、覆盖率或发布门禁。

#### 阶段 7 后续执行记录（2026-10-29）

**历史判定：测试出口已满足；审计后修正为 PARTIAL。** 下述结果仍真实有效，但编辑后增量复用、非增量报告的独立断言及正式 CI 入口仍须补证。主阶段分支仍未提交、推送或合并，且未修改 Core 生产代码、原始 fixture 或公开契约。

- `Stage7DeterminismTests` 比较无变动复建，以及修改一个 Markdown 后增量构建与相同最终输入的独立全量构建；每次独立校验报告、缓存和渲染统计。`Stage7ParallelTests` 用有界同步探针证明真实渲染重叠，再比较串行／并行公共文件的完整路径和原始字节。
- `Stage7I18nAggregateTests` 在沙箱启用配置，检查两种语言的根 sitemap、RSS／Atom／JSON Feed、llms、robots、search 和 agent manifest。渲染中途异常与真实 strict 内置 `data-files` 插件异常分别检查正式输出、完整托管缓存及事务清理。
- 用户确认阶段 7 的插件失败按真实内置插件异常验收；外部插件进程崩溃属于阶段 9，不以此处结果冒充。磁盘满专项在真实限额卷上触发原生磁盘满和 Core 写入失败，检查相同失败安全属性；只有显式 `-p:BukitDiskFullTest=true` 才编入该专项。

| 证据 | 实际结果 | 口径 |
|---|---|---|
| macOS arm64 受影响普通专项 | **56/56**，0 失败、0 跳过 | 基础设施 43、报告契约 4、阶段 7 测试 9 |
| macOS arm64 限额卷 | **2/2** | 隔离 32 MiB HFS+ 映像，包含拒绝不安全卷负例和真实写入磁盘满；卷已卸载 |
| [Linux / Windows 原生专项 #40817294951](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40817294951) | 两平台各 **9/9** 普通阶段 7、**2/2** 磁盘满，0 失败、0 跳过 | 临时证据提交 `ac35002446ce3fb4fcd12d1ee1471ee61526dcf9`；1000 项输入与主工作区逐字节一致；SDK 10.0.100／runtime 10.0.0 |
| 审计修复前版本稳定性（macOS arm64） | **20/20 轮、120/120 条通过**，0 失败、0 跳过 | 每轮 golden 4、增量确定性 1、真实并发 1；记录各轮 TRX、程序集及 995 项输入指纹；不作为修复后最终版本证据 |

Linux 使用 32 MiB tmpfs；Windows 只初始化／格式化由新建 64 MiB VHD 映射且通过非系统盘、设备归属等检查的设备，安全自检通过且已卸载 VHD。CI 的 `platform-tests` 已显式选择各普通阶段 7 类；磁盘满由独立步骤运行，Windows 在检出前保留 fixture 的 LF 输入。直接 owner 自检通过；本批未运行全量测试、覆盖率或发布门禁。

#### 跨平台生成文本换行差异（独立 Core 风险，2026-10-29）

固定输入和 .NET 10.0.100 下，三个站点的 Linux／macOS 与 Windows 公共路径相同，但分别有 10、23、31 个文件的字节不同；Windows 的部分 HTML 同时含 LF 与 CRLF。taxonomy 的 5 个 JSON 连解析后的 `body` 字符串也分别包含 LF／CRLF。`SeoHtmlRenderer`、feed 生成器和 `TaxonomyPageCreator` 使用平台换行或 `AppendLine`；文件哈希及依赖这些哈希的发布包摘要因此可能因平台不同而不同。

当前第 3 节及第 4.3 节只要求固定平台与工具链内确定性。用户确认将跨平台字节差异登记为独立 Core 风险，不阻塞阶段 7；本批不修改 Core，也不对实际产物做换行归一化。若以后要求跨平台字节一致，须单独确认 Core 生成文本、用户内容和静态资源的处理边界，再实施及验证。

#### 阶段 7 审计修复进展（2026-10-29）

**当时状态：PARTIAL（最终判定见本阶段末尾）。** 用户已授权在临时证据分支运行 `gate=platform`，包括其依赖的 `ci-fast` 和 Architecture 测试。正式 CI 在仅重跑 Windows 失败作业后成功，但首次出现的进程取消安全测试失败尚未查明；不运行全量解决方案测试、覆盖率或发布门禁。

- 默认 taxonomy 场景的单文件编辑使 `pages-index` 暴露的 body 指纹改变全站渲染依赖；诊断为 0 命中、8 未命中，其中 6 项为 `render_dependency_changed`。保留该真实场景的增量／全量字节等价，另外在沙箱关闭 `pages-index`，验证编辑后有命中及 `unchanged`／`content_changed` 原因，并通过真实渲染器探针确认修改页重渲染、未修改页未重渲染。
- 报告助手由调用方提供预期增量模式，非增量默认恢复零命中、全部页面未命中和各变体零跳过的独立断言。真实增量结果误作全量，以及报告与内存结果同步伪造错误缓存计数的两个负例均先 RED 后 GREEN。
- macOS 受影响普通专项 **55/55**、限额卷直接消费者 **2/2**；最终程序集的 golden、两种增量场景与并发测试连续 **20/20 轮、140/140 条通过**，0 失败、0 跳过。每轮 4 条 golden 测试中，3 条是真实站点构建，1 条是比较器负例。
- 本批证据根：`/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/stage7-audit-repair-8kno84nc/`；旧测试程序集的 20 轮不用于当前版本。

#### 正式三平台 CI 与待决风险（2026-10-29）

[正式 CI #40822628445](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40822628445) 使用临时证据提交 `8701fb177b4d2a87089b6991599ff630b173aad7`；运行前核对主工作区和该提交的 2433 个受跟踪／新增文件字节、类型和可执行位。Fast gate（含解决方案还原与格式检查）和 Architecture **415/415** 均在第一次通过。三平台实际 SDK 为 **10.0.403**、host runtime 为 **10.0.14**。

| 平台 | 阶段 7 普通测试 | 真实磁盘满 | 来源与清理 |
|---|---|---|---|
| Linux x64 | **10/10** | **2/2** | 第一次；脚本成功执行 tmpfs 卸载及挂载状态检查 |
| macOS arm64 | **10/10** | **2/2** | 第一次；日志记录 HFS+ 映像已 eject |
| Windows x64 | **10/10** | **2/2** | 仅重跑失败作业后的第二次；VHD 安全自检通过、日志记录已卸载 |

每个平台的 10 条包括 golden 4、增量 2、聚合／并发／渲染失败／内置插件失败各 1；均为实际通过，非平台 N/A。第二次 Windows 工件按 ID `12875778056` 下载并核对摘要，未与第一次同名工件混用。第一次 Windows 作业的 PluginHost 为 **50/51**，因此当次 VHD 步骤未执行；第二次为 **51/51**。完整 TRX、两次日志和工件摘要保存在上述证据根的 `official-platform/`。

**待决 Important：**第一次 Windows 的 `RunAsync_CancellationDuringBlockedStdinWriteKillsProcess` 已收到取消异常，但发现探针的完成标记，违背测试对进程终止的预期；第二次及阶段 6 同 SDK 的旧运行均通过。现有日志不足以确定取消通知、阻塞的 stdin 写入、进程终止与标记写入的精确时序，也不能证明是测试抖动还是 Core 缺陷。用户已选择另立 Core 调查，阶段 7 继续保持 PARTIAL；当时未修改 PluginHost，也不因重跑通过而声称问题已修复。后续诊断及测试修正见下文。

只读调查确认当前 Runner 要等 stdin 写入返回或抛错后才进入进程终止分支；[.NET 10.0.14 Windows 进程实现](https://github.com/dotnet/runtime/blob/v10.0.14/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs#L633-L636) 使用同步重定向管道，[运行时异步包装](https://github.com/dotnet/runtime/blob/v10.0.14/src/libraries/Common/src/System/Threading/AsyncOverSyncWithIoCancellation.cs) 会在线程池中尝试 `CancelSynchronousIo`。这些事实支持定向检查时序，但不能证明此次失败的根因。只读调查记录在证据根的 `windows-cancel-investigation/`。

#### Windows 取消时序诊断（2026-10-29）

经授权在临时证据分支运行两次 Windows 定向诊断：[#40826824451](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40826824451)（提交 `9a2bb4c6`）和 [#40828158420](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40828158420)（提交 `a1678369`）。两次的 PluginHost 相关专项均为 **53/53**，SDK 10.0.403／runtime 10.0.14，未修改 Core 生产代码。

- 两次共 10 个就绪后主动取消样本：从取消请求到 `RunAsync` 返回约 3.8–5 ms，随后检查时进程已不存活。这些数据支持取消路径正常工作，但没有记录进程退出的精确时刻，也没有直接记录 stdin 写入何时返回。
- 计时器样本中，名义 150 ms 的取消有 3 次明显延迟。767 ms、892 ms 是独立线程观察到令牌信号的时间，只是实际信号时间的上界；1052 ms 是另一轮记录的回调时间。其余样本约为 150–163 ms。
- 两次诊断均未复现完成标记。原测试的 150 ms 计时器与子进程 1 s 窗口确实存在计时敏感性；线程池饱和是合理假设，但没有线程池计数或队列证据。首次失败时取消令牌触发、写入返回和进程终止的先后顺序仍未知，Core 取消缺陷既未被证明，也未被排除。

#### PluginHost 进程终止测试修正（2026-10-29）

经用户确认，只修改测试与测试探针，不修改 Core 生产代码。复核发现两个缺陷：取消测试依赖与子进程窗口竞争的计时器；超时测试的子进程停留 30 s，但 `RunAsync` 返回后立即检查完成标记，因此即使进程未被终止也会通过。

- 探针模式 `ignore-stdin-then-mark` 只被这两个测试使用，现改为 `ready-ignore-stdin-then-mark`：先原子写出自身 PID 作为就绪信号，再停留 30 s，最后写完成标记。
- `RunAsync_CancellationDuringBlockedStdinWriteKillsProcess`：等子进程就绪并取得该进程；确认请求仍未完成后主动取消，不再使用计时器。10 s 内须抛出取消异常，并在 10 s 内直接确认该进程已退出，且无完成标记。
- `RunAsync_TimeoutBoundsBlockedStdinWriteAndKillsProcess`：同样等就绪并取得该进程，由 Runner 自身 3 s 超时触发；断言 `TimedOut`、该进程在 10 s 内退出，且无完成标记。
- 两个测试的 `finally` 只清理本测试启动的进程。

| 证据 | 结果 |
|---|---|
| 反向检查（macOS） | 临时让 Runner 在超时和取消时都不终止进程，两个测试均失败，分别报告进程仍在运行；随后用 Git 还原，`src/` 与 HEAD 一致，无残留探针进程 |
| macOS 专项 | `SystemProcessRunnerTests`、`PluginPathValidatorTests`、`PluginProtocolProbeTests` **51/51**，0 跳过；闭包 68 个文件、无未映射项；GREEN 已缓存并检查 HIT |
| [三平台 CI #40832277705](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40832277705) | `gate=platform` 第一次即成功，未重跑。临时证据提交 `92b9237175cbbc8b7a4c752a4f10ebec98e5f095`，推送前与工作区 2415 个文件逐字节一致；SDK 10.0.403 |

| 平台 | PluginHost | 取消测试 | 超时测试 | 阶段 7 普通测试 | 真实磁盘满 |
|---|---|---|---|---|---|
| Windows x64 | **51/51** | 0.13 s | 3.02 s | **10/10** | **2/2** |
| Linux x64 | **51/51** | 0.13 s | 3.06 s | **10/10** | **2/2** |
| macOS arm64 | **51/51** | 0.23 s | 3.15 s | **10/10** | **2/2** |

Fast gate、Fast contracts 与 Architecture 也均通过；各平台 N/A 数与阶段 6 已登记的允许名单一致。证据根为 `/Users/ali/.local/state/bukit-verification/stage7-pluginhost-kill-20261029/`，内含策略、闭包、缓存、反向检查与 CI 工件。

修正后的测试在三平台单次运行中证明：就绪后的主动取消和 Runner 自身超时都终止了被观测的进程。这只是单次结果，不能解释首次 Windows 失败的根因。主阶段分支未提交、推送或合并；未运行全量解决方案测试、覆盖率或发布门禁。

#### 阶段 7 最终判定（2026-10-29）

**阶段 7：SUCCESS。** 用户明确接受下述遗留风险不阻塞本阶段。接受风险不等于风险已解决。

- 出口达成：golden 与确定性测试最终程序集连续 **20/20 轮、140/140 条**通过；三平台阶段 7 普通测试各 **10/10**、真实磁盘满各 **2/2**，由正式 CI [#40822628445](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40822628445) 与 [#40832277705](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40832277705) 证明。审计提出的增量复用、非增量独立断言和正式 CI 入口缺口均已补证。
- 已接受的遗留风险：#40822628445 第一次 Windows 作业中 `RunAsync_CancellationDuringBlockedStdinWriteKillsProcess` 失败（PluginHost 50/51），该记录保留。首次失败时取消令牌触发、stdin 写入返回与进程终止的先后顺序仍未知，Core 取消缺陷既未被证明，也未被排除。修正后的测试在三平台单次运行中通过，不构成根因证明。若该测试再次失败，应作为 Core 取消路径问题单独调查。
- 其他已登记、不阻塞的事项：跨平台生成文本换行差异（独立 Core 风险）；外部插件进程崩溃留待阶段 9。
- 未运行：全量解决方案测试、全 Core 覆盖率及发布门禁。主阶段分支未提交、推送或合并。

### 阶段 8：CLI

- 解析器与帮助：选项、必填、冲突、未知参数、退出码。
- 命令：`build`、`doctor`、`config`、`clean`、`preview`、`version`、`completion`；以当前 Core 注册表为准，不因存在未注册的命令类而增加公开命令。
- `seo`、`geo`、`publish` 及子命令：报告读取、schema 校验、`diff`、`--external`。
- `deploy`：本地裸 git 仓库验证 `--dry-run`、分支名校验、CNAME、超时。
- `dev`：监听范围、重建、WebSocket 握手、普通 HTTP 请求处理。
- 命令与文档一致：每个注册命令都出现在 CLI 参考文档中，反之亦然。

出口：Cli 与 Cli.Shared 分别达到第 9 节的单项目覆盖率下限；每个注册命令至少一个成功和一个失败退出码用例。

#### 阶段 8 首批执行记录（2026-10-29）

**状态：PARTIAL（KD 已修复，待三平台执行证据）。** 从已合并阶段 7 的 `main` 提交 `b90fd240` 创建 `codex/stage8` 工作区。经用户明确授权，本批除测试、CI 接线与计划文档外，修改了 `SeoExternalAuditor`/`SeoCommand` 与 `DeployCommand` 三处 Core 生产代码。

现有注册表为 12 个顶层命令、23 个命令路径（含子命令）。基线完整 CLI 专项 `1092/1092`、0 跳过；`Bukit.Cli` 行覆盖 **7926/9002 = 88.05%**、`Bukit.Cli.Shared` **355/367 = 96.73%**，均 ≥70%。解析器、帮助、`build`/`doctor`/`config`/`preview`/`clean`、`seo`/`geo`/`publish`、`dev` 监听与 WebSocket、`deploy` 分支/CNAME/超时已有专项；本批补充并修复以下缺口：

| 缺口 | 本批处理 | 结果 |
|---|---|---|
| KD-001：发布报告用 `documents`，外部审计硬读 `routes` | `SeoExternalAuditor` 同时支持 `routes`/`url` 与 `documents`/`routeUrl`；`PublishCommandTests` 新增 `--external` + 有效发布报告用例，canonical 与链接均使用回环地址；先 RED（实际退出 2，期望 1）后 GREEN | 修复通过，专项 4/4 |
| KD-008：`--dry-run` 仍执行 clean 构建 | `DeployCommand` 在 `--dry-run` 时跳过构建和 provider secret 校验，只校验现有输出；`DeployCommandTests` 新增既有输出不被改动的用例；先 RED（`sentinel.txt` 被清理）后 GREEN | 修复通过 |
| 命令与 CLI 参考文档无强制一致性检查 | `CliContractTests` 新增测试，双向比较注册表命令路径与 `guide/user/12-cli-reference.md` | 通过 |
| 本地裸 git 仓库只验证 clone/push | 扩展 `DeployAsync_RealLocalGitClonesAndPushesWithAskpassAlive`，改用 `pages` 分支和 CNAME，并断言远端内容 | 通过（POSIX 真实 git） |
| `version` 失败退出码、`completion` 成功退出码无直接用例 | 入口测试新增 `version --bogus` → 2；补 `completion bash` → 0 | 通过 |

完整 CLI 专项 **1096/1096**、0 失败、0 跳过；`Bukit.Cli` 行覆盖 **7942/9013 = 88.12%**、`Bukit.Cli.Shared` **355/367 = 96.73%**；Architecture **415/415**。含有效 TRX 与 Cobertura 的最终证据为 `TestResults/coverage/cli-stage8-final3`。`CliContractTests`、`CompletionCommandTests`、`DeployCommandTests`、`PublishCommandTests` 已加入三平台 `platform-tests`，`test-infrastructure-platform-self-test.py` 直接 owner 自检通过；正式 CI 见下文。

**仍待完成：** 正式三平台执行证据（见下节）。

#### 阶段 8 最终判定（2026-10-29）

**阶段 8：SUCCESS。** 正式 CI [run 40844256570](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40844256570) 对提交 `1fee2730` 执行 `gate=platform`，Fast gate、Fast contracts、Architecture 与三个平台全部成功；实际 SDK **10.0.403**。首次运行 `40842704489` 在 Fast gate 报 CA1502 复杂度 36 > 35，已在 `1fee2730` 通过提取 `DeployCommand.ValidateExistingOutput` 修复，保留为诊断记录。

| 平台 | CLI 新增/受影响类实际执行数 | 允许的平台 N/A | 来源 |
|---|---|---|---|
| Linux x64 | CliContract 12、Completion 12、Deploy 12、Publish 4、Clean 29 | 0 | 第一次 |
| macOS arm64 | 同上，另 Preview 20、PreviewExtended 78、Dev 69 | 0 | 第一次 |
| Windows x64 | 同上，另 Preview 20、PreviewExtended 78、Dev 68 | 1（既有 DevPathGuard POSIX 比较） | 第一次 |

三个平台均无失败、无未批准跳过、无验证缺口；`platform.json`、逐项目 TRX、`dotnet-info.txt` 与磁盘满专项位于 `/Users/ali/.local/state/bukit-verification/stage8-platform-ci-20261029/artifacts`。本地最终覆盖率证据仍为 `TestResults/coverage/cli-stage8-final3`：CLI 1096/1096、Cli 88.12%、Cli.Shared 96.73%。未运行全量解决方案测试、覆盖率总门禁或发布门禁。

#### 阶段 8 审计修复（2026-10-29）

只读审计确认 CI 与三平台证据属实，并提出三项收口。经用户确认后执行：

- **A（Core，已修）**：`deploy --dry-run` 或 `--skip-build` 缺少输出目录时，错误提示现在包含 `Run 'bukit build' first.`；测试断言完整提示，不再只匹配前缀。
- **B（行为确认，无 Core 变更）**：接受 `--dry-run` 不校验 provider secrets。dry-run 的契约是“只校验现有输出”，`NOTION_TOKEN` 等属于构建/provider 输入。已补显式用例：有效输出 + Notion source + 无 token + 不带 `--skip-build` → 0；并同步第 9 节决定 4 与 CLI 参考文档。
- **C（测试，已补）**：逐注册路径核对退出码。`publish diff` 增加相同报告的 0 用例，`config schema` 增加输出路径为目录时的 1 用例；其余路径沿用既有成功/失败用例。

| 注册路径 | 成功退出码用例 | 失败退出码用例 |
|---|---|---|
| `build` | `ImportThenBuild_GeneratedListPageRendersListTitle` (0) | `RunAsync_MissingConfig_ReturnsExitCode2` (2) |
| `doctor` | `RunAsync_Passes_WhenCollectionsNotConfiguredAndNoContentUsesOnlyHome` (0) | `RunAsync_ReturnsError_WhenThemeManifestHasUnknownField` (1) |
| `config check` | `Check_ValidConfig_ReturnsZero` (0) | `Check_InvalidConfig_ReturnsOne` (1) |
| `config schema` | `Schema_WritesJsonSchemaToOutputFile` (0) | `Schema_OutputPathIsDirectory_ReturnsOne` (1) |
| `preview` | `RunAsync_ExplicitConfig_WinsOverNearestSiteYaml` (取消后 0) | `RunAsync_MissingDir_ReturnsExitCode2` (2) |
| `dev` | `Main_DevHelp_PrintsLiveReloadDescription` (0) | `LoggerStartup_DevLanRefusalPrecedesMissingConfigAndUsesJsonBootstrap` (2) |
| `clean` | `RunAsync_WithConfig_CleansOutput` (0) | `RunAsync_PathTraversal_ReturnsError` (2) |
| `version` | `RunAsync_ReturnsZero` (0) | `Main_VersionUnknownOption_ReturnsTwo` (2) |
| `completion` | `RunAsync_ValidShell_ReturnsZero` (0) | `RunAsync_InvalidShell_ReturnsTwo` (2) |
| `seo audit` | `Main_SeoAudit_WithValidReport_ReturnsZero` (0) | `RunAsync_Audit_WhenReportMissing_ReturnsOne` (1) |
| `seo diff` | `RunAsync_Diff_WithArgumentsAndNoChanges_ReturnsZero` (0) | `Diff_WhenContractRequiresPublishButSeoReportProvided_ReturnsTwo` (2) |
| `seo insights` | `RunAsync_ValidInputs_WritesConfiguredReportAndCountsFindingsOnce` (0) | `RunAsync_SemanticallyDuplicateObservationPaths_ReturnsTwoWithoutLeakingPaths` (2) |
| `seo question-insights` | `RunAsync_ValidInputs_WritesDefaultReportAndReturnsZero` (0) | `RunAsync_MissingTargets_ReturnsTwoWithStableCode` (2) |
| `seo generative-insights` | `RunAsync_JoinGapWithoutStrict_ReturnsZeroWithGapClassification` (0) | `RunAsync_MissingObservations_ReturnsTwoWithStableCode` (2) |
| `seo authority-insights` | `RunAsync_JoinGapWithoutStrict_ReturnsZeroWithGapClassification` (0) | `RunAsync_MissingObservations_ReturnsTwoWithStableCode` (2) |
| `geo audit` | `Main_GeoAudit_WithValidReport_ReturnsZero` (0) | `RunAsync_Audit_WithInvalidJson_ReturnsOne` (1) |
| `publish audit` | `RunAsync_Audit_WithExplicitValidReport_ReturnsZero` (0) | `Main_PublishAudit_WithoutReport_ReturnsOne` (1) |
| `publish diff` | `RunAsync_Diff_WithIdenticalValidReports_ReturnsZero` (0) | `RunAsync_Diff_MissingBaselineAndCurrent_ReturnsTwo` (2) |
| `deploy` | `RunAsync_DryRunSkipBuild_ReturnsZero` (0) | `RunAsync_DryRunWithoutSkipBuild_MissingOutput_ReturnsErrorWithoutBuild` (1) |

`config`、`seo`、`geo`、`publish` 是无独立执行行为的父命令，分别由子命令成功路径覆盖；无子命令时返回 2。`dev` 为长驻服务，成功退出码由 `--help` 路径覆盖，服务行为另有 Dev/Preview 专项。

修复后完整 CLI 专项 **1099/1099**、0 失败、0 跳过；`Bukit.Cli` 行覆盖 **7951/9013 = 88.22%**、`Bukit.Cli.Shared` **355/367 = 96.73%**；Architecture **415/415**；分析器棘轮 421/421。证据位于 `TestResults/coverage/cli-stage8-followup`。正式 CI [run 40848651711](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40848651711)（提交 `b86f2b57`，SDK 10.0.403）Fast gate、Architecture、Fast contracts 与三平台全部成功；DeployCommandTests 13、PublishCommandTests 5 在三平台实际执行，Windows 仅既有 1 项 DevPathGuard N/A。平台证据位于 `/Users/ali/.local/state/bukit-verification/stage8-followup-ci-20261029/artifacts`。

### 阶段 9：插件体系

- PluginHost：清单加载与迁移、平台选择、哈希与路径校验、权限评估、环境清空与白名单、密钥掩码。
- 协议 v1：handshake、manifest、invoke 的正常与异常（超时、崩溃、乱码、超大输出、进程树泄漏）。
- 锁文件与 CI 策略。

出口：PluginHost 与 Plugin.Abstractions 分别达到第 9 节的单项目覆盖率下限；每类异常都有对应用例。

#### 阶段 9 首批执行记录（2026-10-29）

**状态：测试批次完成；I-01 已由独立 Core 授权任务修复；最终判定见本节末。**

基线为已合并阶段 8 的 `main` 提交 `7a8b7135`，新建 `codex/stage9`。基线 PluginHost **228/228**、Plugin.Abstractions **15/15**，均 0 失败、0 跳过；PluginHost 行覆盖 **1444/1777 = 81.26%**，Plugin.Abstractions 独立套件 **2970/5664 = 52.44%**（合并 PluginHost 套件后 81.07%），独立套件低于 70% 下限；基线 TRX/Cobertura 保留在 `R/baseline/`。本批补充以下缺口：

| 缺口 | 本批用例 | 结果 |
|---|---|---|
| `PluginCommandManifestValidator` 无直接用例（基线 0/120 行） | 新增 `PluginCommandManifestValidatorTests`：空静态命令放行、合法子集、重复命令、未知运行时命令、alias/argument/option 子集与 required/type 冲突、嵌套路径、参数校验 | 通过 |
| `PluginCiPolicy` 仅覆盖 2/4 分支 | 补非 CI 放行、缺 sha256、sha256 未验证、permissions 非显式 | 通过 |
| 协议响应校验分支缺失 | 补 handshake 的 type/success/缺失 identity，manifest 的 type/success/空命令，invoke 的 type/protocol/requestId | 通过 |
| 密钥掩码只测 `NOTION_TOKEN` 值 | 新增 `PluginSecretMaskerTests`：五个键名片段、非密钥键、空值、环境掩码、最长值优先、空环境 | 通过 |
| 哈希校验只测 match/mismatch | 补空路径、空期望值、大小写不敏感比较 | 通过 |
| 清单版本加载门禁无直接用例 | 补 `manifestVersion` 0/2 经 `LoadAsync` 拒绝 | 通过 |
| Abstractions 响应 DTO 独立套件偏薄 | 补 manifest request/request envelope、response envelope、handshake/invoke/manifest response 的 round-trip | 通过 |

**最终专项：** PluginHost **271/271**、Plugin.Abstractions **20/20**，均 0 失败、0 跳过。首次 PluginHost 运行 **268/269**，失败原因是新加的大小写测试错误断言 `ExpectedSha256` 会被小写化；现有实现保留输入值，修正测试断言后通过，非产品缺陷，原始 TRX 保留。

**覆盖率（本机 macOS arm64、SDK 10.0.100、Release；Cobertura 合并）：** PluginHost **1519/1777 = 85.48%**；Plugin.Abstractions 独立套件 **4986/5664 = 88.03%**，其中手写源 **204/204 = 100%**；合并 PluginHost 套件后 **4989/5664 = 88.08%**。两个项目均 ≥70%。

**覆盖率口径：** 本批 Cobertura 采集命令未使用 `coverage.runsettings`，因此分母包含 `obj/.../System.Text.Json.SourceGeneration/*.g.cs` 生成代码，88.03% 是含生成代码的采集值，不是现行 CI 门禁口径。CI 门禁 `run-one.sh` 会传 `coverage.runsettings`，排除 `**/obj/**/*.g.cs`；阶段 10 以该门禁口径重跑并同时记录总比例。生成/手写拆分见 `R/coverage-breakdown.json`。

**证据：** `R=/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/stage9-plugin-batch1-20261029`（执行时根）；持久镜像 `D=/Users/ali/.local/state/bukit-verification/stage9-plugin-batch1-20261029` 与 `R` 逐字节一致，避免系统临时目录清理。基线 TRX/Cobertura 在 `R/baseline/`，`R/closure.json` 无未映射文件，`R/classification.json` 将两个专项归为 `dotnet-serial`，按 Abstractions → PluginHost 串行执行；最终 TRX 在 `R/green/abstractions-2/`、`R/green/pluginhost-4/`，覆盖率附件在 `R/coverage/abstractions-final/`、`R/coverage/pluginhost-final2/`，覆盖率汇总在 `R/coverage-summary.json`。cache record 的精确命令包含原 `R` 输出路径；在 `D` 上重跑应记录新的缓存条目，不能把 `D` 路径冒充原缓存命中。

**确切命令：**

```sh
dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj -c Release --logger "trx;LogFileName=tests.trx" --results-directory "$R/green/abstractions-2"
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release --logger "trx;LogFileName=tests.trx" --results-directory "$R/green/pluginhost-4"
dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj -c Release --collect:"XPlat Code Coverage" --logger "trx;LogFileName=tests.trx" --results-directory "$R/coverage/abstractions-final"
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release --collect:"XPlat Code Coverage" --logger "trx;LogFileName=tests.trx" --results-directory "$R/coverage/pluginhost-final2"
```

**专项复核（只读，2026-10-29）：** 首轮 Critical 0 / Important 1 / Minor 6（`R/reviews/specialty-v1.md`）；增量复评 Critical 0 / Important 1 / Minor 4（`R/reviews/specialty-v2.md`）；最终 delta-only 复核 Critical 0 / Important 1 / Minor 3（`R/reviews/final-delta-v1.md`）。Minor 中已在本批处理：DTO round-trip 增加 `success` 断言与 request/envelope 反序列化；清单版本门禁补测；CI 策略补 `DiagnosticCode` 断言；命令路径与基线证据补齐；cache record 使用与上方完全相同的含 logger/results-directory 命令，`cache check` 4/4 HIT（`R/cache/`）。该批次最终复核当时确认 I-01 仍为唯一 open Important，故当时不得标记 SUCCESS；I-01 随后按用户单独授权修复，见下节。以下事项不在本批修改：

- **Important（已按用户单独授权修复，见下方 I-01 记录）：** 首轮复核发现 `PluginSecretMasker` 只覆盖 `NOTION_TOKEN`/`API_KEY`/`PASSWORD`/`TOKEN`/`SECRET` 五个片段；ADR §22.3 要求执行报告不得包含明文 `COOKIE`/`Authorization` 值。该发现已进入独立 Core 任务恢复 RED → GREEN，阶段 9 的“密钥掩码”现覆盖七个片段；修复前不具备完整 ADR 覆盖这一历史事实保留。
- **Minor（Core 后续）：** 重复的 argument/option 名或运行时命令名会抛裸 `ArgumentException` 而非带诊断码的 `ConfigException`；`PluginHashVerifier` 对缺失/不可读文件抛原始 I/O 异常；`MaskText` 会掩码所有环境值而非仅密钥键，契约未定。（2026-10-30 更新：argument/option 与 runtime 命令重名、以及 M-02 均已关闭；M-03 也已关闭，仅 S-01 仍开放。）
- **平台边界：** CLI 加载器的“清单缺少当前 RID”分支属于阶段 8 CLI 范围，未在本阶段重跑；Windows Job Object / Linux `/proc` 进程树分支仍需阶段 10 三平台证据，本机覆盖率不冒充这些分支已覆盖。

**未执行：** 全量解决方案测试、完整 Core 覆盖率总门禁、发布门禁、三平台 CI；本批新增用例均为平台无关，未改动既有进程/探针类。按用户决定，真实异常终止探针留待所有阶段完成后统一汇总补证，本阶段仍以既有 `exit 7` 崩溃用例为准。

#### 阶段 9 I-01 授权修复（2026-10-29）

用户单独授权后，以独立 Core 安全任务修复 I-01。OpenSpec：`openspec/changes/archive/2026-10-30-fix-plugin-secret-masker-cookie-authorization/`。生产修改仅为 `PluginSecretMasker.SecretKeyFragments` 增加 `COOKIE` 与 `AUTHORIZATION` 两个片段；不改报告 schema、公开 API、权限模型或 CLI。

- **RED：** 新增 `COOKIE`/`HTTP_COOKIE`/`AUTHORIZATION`/`HTTP_AUTHORIZATION`/小写 `cookie` 五个掩码 theory 用例及一个真实 reporter 报告用例；聚焦 `PluginSecretMaskerTests|PluginLockAndReportTests` 为 **7 失败 / 21 通过 / 0 跳过**，失败均为断言失败（`***` 未出现、明文仍在），非编译失败。
- **GREEN：** 聚焦 **28/28**；完整 PluginHost **277/277**，0 失败、0 跳过。PluginHost 行覆盖 **1521/1779 = 85.50%**（较批次 1 增加 2 行且均已覆盖）；Abstractions 覆盖不变（独立 4986/5664 = 88.03%，合并 4989/5664 = 88.08%）。
- **证据：** `R2=/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/stage9-plugin-secret-masker-20261029`（执行时根）；持久镜像 `D2=/Users/ali/.local/state/bukit-verification/stage9-plugin-secret-masker-20261029` 与 `R2` 逐字节一致。`R2/red/`、`R2/focused-green/`、`R2/green/`、`R2/coverage/`；`R2/closure.json` 无未映射文件；测试与覆盖率 cache record 均 `CACHE HIT`；`openspec validate fix-plugin-secret-masker-cookie-authorization --strict` 通过。
- **掩码取舍：** 片段采用大小写不敏感子串匹配，`COOKIE_CONSENT_MODE`、`AUTHORIZATION_ENDPOINT` 等非密钥键也会写成 `***`。这是已接受的安全偏向，报告读取无功能影响；不改为精确键匹配，避免漏掉 `HTTP_COOKIE`/`PROXY_AUTHORIZATION` 等变体。
- **复核：** 专项只读复核 **Critical 0 / Important 0 / Minor 2**（`R2/reviews/specialty-v1.md`）；唯一 delta-only 最终复核 **Critical 0 / Important 0 / Minor 2**（`R2/reviews/final-delta-v1.md`）。S-01：`PluginExecutionReporter` 仍原样写入插件可控的 `artifacts[].path`、`diagnostics[].code` 等字段，若插件主动把已授权 secret 回显到这些字段仍可能落盘；这是 I-01 之前已存在、且同样影响旧片段的独立问题，登记为后续 Core 事项，不扩大本任务。S-02：首份 diff 未包含未跟踪的新测试文件，最终复核 diff 已补齐；S-03：最终 diff 未包含三个未跟踪 OpenSpec 文件，已重新生成完整 diff。
- **未执行：** 未跑全量解决方案、完整覆盖率总门禁、发布门禁或三平台 CI；未推送或合并；本任务随阶段 9 提交入库。

#### 阶段 9 最终判定（2026-10-29）

**阶段 9：SUCCESS。** 用户已确认本阶段范围并单独授权 I-01 修复；已提交到 `codex/stage9`，未推送。

- **出口达成：** 测试批次 PluginHost 271/271、Abstractions 20/20；I-01 修复后完整 PluginHost 277/277，均 0 失败、0 跳过。PluginHost 行覆盖 **1521/1779 = 85.50%**；Abstractions 独立套件 **4986/5664 = 88.03%**（手写源 204/204 = 100%），合并 **4989/5664 = 88.08%**，均 ≥70%。协议正常/异常、锁文件、CI 策略、权限/路径/哈希/平台、密钥掩码均有对应用例。
- **复核：** 批次复核与 I-01 修复复核的最终结果均为 **Critical 0 / Important 0**；I-01 已关闭并登记为 KD-013。剩余 Minor 为已披露的独立 Core 事项：S-01 插件可控报告字段回显、M-01 重复名称抛裸 `ArgumentException`、M-02 哈希缺失文件抛原始 I/O、M-03 `MaskText` 掩码范围契约。它们不阻塞本阶段出口。（2026-10-30 更新：M-01 的 argument/option 与 runtime 命令重名、M-02 均已关闭；M-03 也已关闭，仅 S-01 仍开放。）
- **用户接受的延后项：** 真实异常终止探针（SIGKILL/FailFast）留待所有阶段完成后统一补证；当前以既有 `exit 7` 崩溃用例为准，不声称已完成真实异常终止覆盖。
- **未取得：** 三平台 CI、全量解决方案、完整 Core 覆盖率总门禁与发布门禁。阶段 9 出口不要求这些；Windows Job Object / Linux `/proc` 进程树分支仍按阶段 10 三平台计划验证，本机覆盖率不冒充。

#### 阶段 9 指标口径补充与审计修补（2026-10-29）

本节补充最终判定后的指标口径说明，并修补审计发现的两处断言弱点。阶段 9 判定维持 SUCCESS，不改变出口结论。

- **Abstractions 分母构成：** 独立套件 Cobertura 分母 **5664 行**中，`obj/` 下 System.Text.Json 源生成文件占 **5460 行（约 96%）**，手写源仅 **204 行**。该次采集未使用 `coverage.runsettings`，故包含生成代码；**4986/5664 = 88.03%** 是含生成代码的采集值，不是 CI 门禁分母。CI 门禁通过 `run-one.sh` 使用 `coverage.runsettings` 排除 `**/obj/**/*.g.cs`，门禁口径为手写源 **204/204 = 100%**。阶段 10 以门禁口径重跑并同时记录总比例。
- **PluginHost 过滤口径：** 合并报告含 Shared、测试辅助程序等程序集，须按 `src/Bukit-Core/Bukit.PluginHost/` 路径过滤后，再按路径与行号去重取原始比例：**1519/1777 = 85.48%**（批次 `pluginhost-final2`，已复算）。I-01 运行所记 **1521/1779 = 85.50%** 本次审计未复算，以原记录为准。
- **ProcessTree 分项：** `src/Bukit-Core/Bukit.PluginHost/ProcessTree/` 为 **110/254 = 43.31%**。其中 `PlatformProcessTreeLimiter.cs` 为 **108/252**；未覆盖的 144 行中：
  - Windows Job 限制器类（`WindowsJobProcessTreeLimiter`）**60/60 未覆盖**，属 Windows 专属，按阶段 10 三平台验证。
  - Linux 常量类（`LinuxConstants`）**8/8 未覆盖**，属 Linux 专属。
  - 其余 **76 行**位于同时包含 macOS 与 Linux 分支的方法内（如 `EnumerateChildren`、`TryReadProcessUsage`、`TryReadStatField`），**尚未逐行归属到平台**；在完成手工拆分前，不得声称 macOS 可达分支已全部覆盖。
- **断言补修：** `PluginCommandManifestValidatorTests` 中两个原本只靠“不抛异常”的用例（`ValidateRuntimeCommands_EmptyStaticCommands_AllowsAnyRuntimeCommands`、`ValidateRuntimeCommands_RuntimeSubset_AllowsNestedDeclarations`）已改为 `Record.Exception` 并断言为 `null`。聚焦 `PluginCommandManifestValidatorTests` **12/12** 通过；完整 `Bukit.PluginHost.Tests` **277/277**，0 失败、0 跳过。
- **验证与状态：** 聚焦 `PluginCommandManifestValidatorTests` **12/12**、完整 `Bukit.PluginHost.Tests` **277/277**，均 0 失败、0 跳过；证据根 `R3=/Users/ali/.local/state/bukit-verification/stage9-audit-patch-20261029`。本次审计修补随阶段 9 提交入库；未运行 Abstractions 专项、全量解决方案或覆盖率总门禁。

#### 阶段 9 审计确认（2026-10-29）

只读审计结论：**阶段 9 的 SUCCESS 判定与代码和记录基本一致，未发现会推翻判定的问题；审计未重跑测试。** 对审计提出的三项注意：

- **覆盖率分母（2026-10-30 更正）：** 该次采集未使用 `coverage.runsettings`，88.03% 含生成代码，不是现行 CI 门禁口径；CI 门禁 `run-one.sh` 使用 `coverage.runsettings` 排除 `**/obj/**/*.g.cs`，门禁口径为手写源 **204/204 = 100%**。阶段 10 以门禁口径重跑，并同时记录总比例。
- **证据持久性：** 两个执行根已复制到 `/Users/ali/.local/state/bukit-verification/stage9-plugin-batch1-20261029` 与 `.../stage9-plugin-secret-masker-20261029`，所有已复制文件经 `diff -qr` 验证一致；计划与 OpenSpec 已记录持久镜像。另生成归档 `/Users/ali/.local/state/bukit-verification/stage9-evidence-20261029.tar.gz`（1.6 MB，SHA256 `6c8505e54d145b047b0d72b76748ca4815e960e9395bfb5848a2026fd6bd7cbf`），并提交到仓库 `docs/testing/evidence/stage9-20261029/`（含 README、manifest、归档与校验文件）；仓库不提交 24 MB 原始目录。cache record 的精确命令仍保留原执行路径，避免把镜像路径冒充缓存命中。
- **过度掩码：** 已在 I-01 记录中明确：`COOKIE`/`AUTHORIZATION` 采用子串匹配，`COOKIE_CONSENT_MODE` 等非密钥键也会写 `***`；这是已接受的安全偏向，不改为精确键匹配。
- **未闭环项：** S-01 仍开放且已登记；M-01（argument/option 与 runtime 命令重名）、M-02、M-03 已于 2026-10-30/31 关闭。它们不阻塞阶段 9；真实崩溃探针与三平台进程树分支按既定计划后置。审计建议的重跑专项未被采用：当前 cache 记录与最终 TRX 均匹配，按 `AGENTS.md` 不对未变化输入重复运行。

#### 阶段 9 M-01/M-02 错误契约收口（2026-10-30）

用户单独授权执行 M-01 + M-02 Core 任务。分支 `codex/plugin-error-contracts` 从阶段 10 `d41a2b6a` 创建；单 writer，生产修改仅 `PluginCommandManifestValidator` 与 `PluginHashVerifier`，测试仅两个既有测试类。生产+测试提交 `98169d48`、`921f7764`、`9285ed15`；本记录随文档提交。

- **M-01：** `ValidateArgumentSubset`/`ValidateOptionSubset` 的 `ToDictionary` 改为共享 `ToNamedMap`；同一命令的静态 argument/option 重名抛 `ConfigException` + `ConfigInvalidValue`，消息为 `declares duplicate argument|option for command {path}: {name}.`，嵌套命令保留完整路径。runtime manifest 命令树查重由后续任务关闭，见下节。
- **M-02：** `VerifySha256Async` 捕获 `FileNotFoundException`/`DirectoryNotFoundException` → `Plugin executable was not found.`；`UnauthorizedAccessException` → `Plugin executable could not be read: access denied.`；其他 `IOException` → `Plugin executable could not be read.`；取消继续抛 `OperationCanceledException`，未捕获 `Exception`，签名与调用方未变。
- **RED：** 新增 4 个 M-01 与 4 个 M-02 用例；聚焦 **7 失败 / 20 通过 / 0 跳过**，失败为原始 `ArgumentException`、`DirectoryNotFoundException`、`UnauthorizedAccessException`，取消守护用例通过。
- **GREEN：** 聚焦 **27/27**；完整 PluginHost **286/286**；CLI `PluginCliIntegrationTests` **40/40**；`public-api-drift.sh check Release` 退出 0；均 0 失败、0 跳过。分析器棘轮保持 **421/421**（`ToNamedMap` 使用具体 `Dictionary` 类型，未增加 CA1859 预算）。
- **测试边界：** `MissingFile` 用例先创建父目录，直接覆盖 `FileNotFoundException`；通用 `IOException` 分支未构造专用用例；目录路径用例按平台异常映射只断言 `could not be read` 前缀。复核后已补直接覆盖，仍保留这一非阻塞测试边界。
- **证据：** `D=/Users/ali/.local/state/bukit-verification/plugin-error-contracts-20261030`（`red/`、`green-focused/`、`full/`、`cli/`、`public-api/`、`closure.json`、`cache/`）。
- **未执行：** 全量解决方案、完整 Core 覆盖率、release/AOT、三平台 CI、push/PR/merge；S-01 保持开放；M-03 与 runtime 命令重名已由后续任务关闭。

#### 阶段 9 M-01 runtime 命令重名收口（2026-10-30）

用户授权执行下一项 Core 契约修复。分支 `codex/plugin-runtime-duplicates` 从 `main` `cf356b28` 创建；生产修改 `PluginCommandManifestValidator`（递归 runtime 命令树查重）与 `PluginCliLoader`（runtime-only 保留静态子集语义，仅做 runtime 树查重，并安全构建 runtime 命令映射），测试修改 PluginHost/CLI 两个既有测试类。提交 `dd631b76`、`78a4fe0d`、`07243704`。

- **行为：** runtime manifest 顶层或嵌套命令重名抛 `ConfigException` + `ConfigInvalidValue`，消息为 `Plugin {pluginId} runtime manifest declares duplicate command: {path}.`；`staticCommands` 为空时也查重。
- **CLI：** `SelectExposedCommands` 不再用裸 `ToDictionary`；`PluginCliLoader` 对 runtime-only 清单传入空静态命令做 runtime 树查重（保留 runtime-only 不校验静态子集的既有语义），因此 runtime-only 顶层与嵌套重名均转 `ConfigException`。
- **RED：** PluginHost 3 失败 / 16 通过 / 0 跳过；CLI 顶层 1 失败 / 0 通过；失败为缺失查重与原始 `ArgumentException`。runtime-only 嵌套与静态命令共存用例为修复后新增的守护覆盖。
- **GREEN：** PluginHost 聚焦 19/19、完整 289/289；CLI 重复专项 2/2（runtime-only 全组 5/5）、完整 1102/1102；public API drift 退出 0；分析器棘轮 421/421；`dotnet format` 退出 0。
- **证据：** `D=/Users/ali/.local/state/bukit-verification/plugin-runtime-duplicates-20261030`（`red/`、`green/`、`full/`、`public-api/`、`public-api-2/`、`public-api-3.log`、`ratchet.log`、`ratchet-2.log`、`ratchet-3.log`、`format.log`、`format-2.log`、`closure.json`、`cache/`）。
- **未执行：** 全量解决方案、完整覆盖率、release/AOT、三平台 CI。

#### 阶段 9 M-03 MaskText 契约收口（2026-10-31）

用户授权执行 M-03 契约任务。分支 `codex/plugin-masktext-contract` 从 `main` `e77b71e5` 创建；仅 `PluginSecretMasker` 注释与测试，无行为、签名或 schema 变化。提交 `5cdfeb06`。

- **契约：** `MaskText` 按值匹配、不按 key；调用方只传入 `permissions.environment.read` 中的授权变量，任何授权值都可能是 secret，因此非密钥键的授权值也会被掩码。授权 `PATH`/`LANG` 等常见变量时的过掩码是接受的安全偏向，不改为 `IsSecretKey`。
- **测试：** 新增“授权非密钥值”“短授权值”“未授权值不参与掩码”3 个用例；本任务为既有行为的契约固化，无 RED。
- **GREEN：** 聚焦 **19/19**；完整 PluginHost **292/292**；public API drift 退出 0；分析器棘轮 421/421；`dotnet format` 退出 0。
- **证据：** `D=/Users/ali/.local/state/bukit-verification/plugin-masktext-contract-20261031`（`focused/`、`full/`、`public-api/`、`ratchet.log`、`format.log`、`closure.json`）。
- **未执行：** 全量解决方案、完整覆盖率、release/AOT、三平台 CI。

### 阶段 10：架构与门禁


- 依赖矩阵：`Engine.Abstractions` 不引用 `Config`；Core 不引用 Plugins 与 Labs。
- 公共 API 漂移基线。
- AOT 发布冒烟：`PublishAot` 成功后，对实际原生可执行文件运行配置校验、完整构建和发布审计。复用 `bash scripts/smoke/release-artifacts.sh <archive-or-publish-dir> <rid>`，它将 fixture 复制到临时目录，再调用 `scripts/smoke/core.sh` 的 `config check`、`build --clean`、`publish audit`；只运行 `version` 不算通过。执行前按第 7 节单独明确授权并固定产物路径与 RID。
- 覆盖率下限校验：第 4.4 节的整体、各源项目和三个安全层文件检查全部通过。
- 平台专项：取得 Linux x64、macOS arm64、Windows x64 的实际执行证据，按第 7 节核对新增测试类的 CI 入口及适用用例数。

#### 阶段 10 首批执行记录（2026-10-29）

**状态：SUCCESS。** 入口为已合并阶段 9 的 `main` 提交 `e26354d1`，工作分支 `codex/stage10`。架构、API 基线、完整覆盖率、安全层、macOS arm64 AOT 冒烟与三平台 CI 均已取得证据。

**授权记录：** 2026-10-29 用户明确授权批次 2（完整 Core 覆盖率采集）、批次 3（本机 osx-arm64 AOT 发布冒烟）与批次 4（推送 `codex/stage10` 并触发三平台 CI），同时同意阶段 9 覆盖率口径更正与真实异常终止探针纳入阶段 10 最终验收。本节不以“列入计划”代替授权；`stage10-readiness-20261029/README.md` 的 Pending 清单是授权前的入口盘点，已由本记录取代。

| 出口项 | 结果 |
|---|---|
| 依赖矩阵 | `Ad01ConfigDecouplingTests` 继续覆盖 Engine.Abstractions 与 Config 的解耦（不依赖 Config）；新增 `CoreProjectReferenceBoundaryTests` 扫描全部 `src/Bukit-Core/**/*.csproj`，禁止引用 `src/Bukit-Plugins` 或 `src/Bukit-Labs`，并同时覆盖 `\` 与 `/` 两种引用写法。完整 Architecture **420/420**，0 失败、0 跳过。 |
| 公共 API 漂移 | `bash scripts/checks/public-api-drift.sh check Release` 退出 0；基线 18 个程序集 / 425 个类型与源码一致，无诊断。 |
| 完整覆盖率 | `BUKIT_COVERAGE_ROOT=TestResults/coverage/stage10-final bash scripts/checks/coverage.sh Release`：整体 **90.81% (42724/47047)**，18 个源项目全部 ≥70%，见下表。 |
| 安全层 | `BuildTransaction` **191/198 = 96.46%**、`OutputDirectoryCleaner` **52/54 = 96.30%**、`AssetOutputPlan` **150/151 = 99.34%**，均 ≥95%，`security-layer.py` 退出 0。 |
| AOT 冒烟 | `bash scripts/build/package-native-aot.sh 2.0.4 osx-arm64 <dir>` 成功生成 `bukit-2.0.4-osx-arm64.tar.gz`（原生 `bukit` 28.5 MB；archive SHA-256 `6ba112a25ac84285cab6acc5a6cd9bb19f16b7ff1344c365c83ff24d9f5ac936`）；`release-artifacts.sh <archive> osx-arm64` 解包后执行 `config check`、`build --clean`、`publish audit`，退出 0，publish errors=0（22 条 warnings，其中 publish 组 16 条，均为 minimal fixture 的内容质量告警）。 |
| 三平台专项 | CI [run 40904333690](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40904333690)（提交 `28f74c07`，SDK 10.0.403）Fast gate、Architecture、Fast contracts 与 Linux x64 / macOS arm64 / Windows x64 全部成功；三平台 PluginHost 均执行 52 项（SystemProcessRunner 28 / PathValidator 16 / ProtocolProbe 8），0 平台 N/A。 |

覆盖率采集共 **7009** 个测试通过、0 失败、0 跳过；其中 PluginHost 为新增异常终止用例前的 277 项。

| 源项目 | 覆盖行/总行 | 行覆盖 |
|---|---:|---:|
| Bukit.Cli | 7951/9013 | 88.22% |
| Bukit.Cli.Shared | 355/367 | 96.73% |
| Bukit.Config | 3153/3316 | 95.08% |
| Bukit.Content | 3470/3715 | 93.41% |
| Bukit.Engine | 14803/16352 | 90.53% |
| Bukit.Engine.Abstractions | 725/753 | 96.28% |
| Bukit.Engine.Analytics | 594/612 | 97.06% |
| Bukit.Engine.Assets | 993/1200 | 82.75% |
| Bukit.Engine.Audit | 2176/2330 | 93.39% |
| Bukit.Engine.Feeds | 481/528 | 91.10% |
| Bukit.Engine.Primitives | 866/908 | 95.37% |
| Bukit.Notion | 1633/1703 | 95.89% |
| Bukit.Plugin.Abstractions | 204/204 | 100.00% |
| Bukit.PluginHost | 1486/1732 | 85.80% |
| Bukit.Rendering | 1489/1631 | 91.29% |
| Bukit.Routing | 356/369 | 96.48% |
| Bukit.Shared | 548/619 | 88.53% |
| Bukit.Theme | 1441/1695 | 85.01% |

#### 阶段 10 三平台证据（2026-10-29）

**阶段 10：SUCCESS。** 正式 CI [run 40904333690](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40904333690) 对提交 `28f74c07` 执行 `gate=platform`；Fast gate、Architecture、Fast contracts 与三个平台全部成功，实际 SDK **10.0.403**。覆盖率作业在该 gate 下按设计跳过，完整覆盖率证据来自本机门禁采集。该 CI 之后新增了架构守卫的复核修复（`7d9ebe60`，测试代码）与文档；生产代码与平台测试未变，修正后的 Architecture **420/420** 由 fast gate [run 40907848663](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40907848663)（提交 `f78ac331`）验证。

| 平台 | 实际 OS / 架构 | SDK | PluginHost 执行（SystemProcessRunner / PathValidator / ProtocolProbe） | 允许的 N/A | 结果 |
|---|---|---|---:|---:|---|
| Linux x64 | Linux / x64 | 10.0.403 | 28 / 16 / 8 | 1（Windows-only ExternalTool 方法） | success |
| macOS arm64 | Darwin / arm64 | 10.0.403 | 28 / 16 / 8 | 2（Windows-only ExternalTool；Windows 路径比较） | success |
| Windows x64 | Windows / x64 | 10.0.403 | 28 / 16 / 8 | 15 个唯一方法（12 个 POSIX 进程方法 + 3 个平台守卫） | success |

三平台均无失败、无未批准跳过；PluginHost 三平台均 **52/52**、0 平台 N/A。Windows Job Object 与 Linux `/proc` 进程树分支由 `SystemProcessRunnerTests` 的 28 项实际执行；新增真实异常终止用例在三个平台各执行 1 项，均通过；最终精确退出码断言由 [run 40973740319](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40973740319)（提交 `b40184fb`）复跑三平台确认（Windows `-1`，Linux/macOS `137`）。`platform.json`、逐项目 TRX、`dotnet-info.txt` 与磁盘满专项位于 `D=/Users/ali/.local/state/bukit-verification/stage10-platform-ci-40904333690/artifacts/{Linux-x64,Darwin-arm64,Windows-x64}`；精确退出码复跑证据位于 `D=/Users/ali/.local/state/bukit-verification/stage10-exitcode-ci-40973740319/artifacts`。

**验收映射（§10）：**

| 质量目标 | 当前证据 |
|---|---|
| 确定性 | Stage 7 `Stage7DeterminismTests` / `Stage7GoldenTests` 在三平台实际执行通过；本机完整 Engine 覆盖采集 2946/2946。 |
| 失败安全 | Stage 7 `Stage7FailureTests` / `Stage7PluginFailureTests` 三平台通过；失败安全快照沿用阶段 0/5 证据。 |
| 增量正确 | 阶段 5/7 增量与全量对照证据；本阶段完整 Engine 覆盖 90.53%。 |
| 路径安全 | 三平台 `OutputDirectoryCleanerTests` 13、`AssetPipelineTests` 30、`TaxonomyRedirectWriterTests` 27 通过；Shared 路径专项通过。 |
| 契约稳定 | CI Fast gate 的 public API drift 退出 0；阶段 0 报告清单与 schema 用例沿用。 |
| 覆盖率 | 整体 **90.81%**，18 个源项目全部 ≥70%，三个安全层文件均 ≥95%。 |

**阶段 9 覆盖率口径更正：** 阶段 9 的 88.03% 采集未使用 `coverage.runsettings`，包含 5460 行 `obj/**/*.g.cs` 生成代码；CI 门禁 `run-one.sh` 使用该设置，门禁口径为手写源 **204/204 = 100%**。计划阶段 9 记录、修订记录与本节均按此更正，不再把 88.03% 当作 CI 门禁口径。

**真实异常终止探针（阶段 9 延后项）：** `PluginProcessProbe` 新增 `self-kill` 模式（`Process.GetCurrentProcess().Kill()`；Unix 为 SIGKILL，Windows 为 TerminateProcess）；`PluginProtocolProbeTests` 新增真实异常终止用例，断言 `PluginExecutionFailed`、精确退出码（Unix **137** / Windows **-1**）及 15 秒内有界返回。聚焦 **8/8**、完整 PluginHost **278/278**，0 失败、0 跳过；三平台 CI [run 40973740319](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40973740319)（提交 `b40184fb`）中该用例在 Windows 以 `-1`、Linux/macOS 以 `137` 各执行 1 次并全部通过。该用例已由 `PluginProtocolProbeTests` 类过滤自动纳入三平台 `platform-tests`，无需新增 CI 过滤项；FailFast 未单独执行，登记为残余边界。

**只读专项复核与修复：** 复核 Critical 0 / Important 1 / Minor 4。Important 为守卫未把 `\` 归一化为平台分隔符，Unix CI 会漏检仓库常用写法；已在 `7d9ebe60` 修复为 `include.Replace('\\', Path.DirectorySeparatorChar)`，并新增 `\`/`/` × Plugins/Labs 四个负向 theory；聚焦 **5/5**、Architecture **420/420**，修正后 Ubuntu Architecture 由 fast gate `40907848663` 验证。delta 复核 Critical 0 / Important 0 / Minor 1，剩余 Minor 为本记录的证据路径与措辞，已在本记录修正。仍不在守卫范围：`Directory.Build.props/targets`、`<Reference>`、MSBuild property expansion、分号列表、symlink/8.3 与 macOS 大小写变体；FailFast 未单独覆盖。

**审计修补（2026-10-30）：** 只读审计确认 SUCCESS 成立，提出 1 项 Important 与 4 项 Minor。Important 为授权记录缺失；本节已补 2026-10-29 的明确授权记录，`stage10-readiness-20261029/README.md` 的授权前 Pending 清单也已改为已授权/已执行。Minor 处理：阶段 9 归档 README 的“follows summarize.py”改为历史采集说明并与更正日期统一为 2026-10-30；AOT archive SHA-256 已写入正文本表；self-kill 改为精确退出码断言（Unix **137** / Windows **-1**）；守卫新增 Core 项目数 **18** 的断言，防止扫描范围静默缩小。修补后聚焦 **5/5**、Architecture **420/420**（本机 macOS arm64）；最终 fast gate `40971907989`（提交 `10b3cb11`）Architecture 与 Fast gate 均成功。self-kill 精确退出码由三平台 CI `40973740319` 证明（Windows `-1`，Linux/macOS `137`）。

**证据：** `D=/Users/ali/.local/state/bukit-verification/stage10-readiness-20261029`（`architecture-final2` 的 416/416 修正前证据、`public-api`、`coverage-self-tests`、`closure.json`、`cache`）；`D=/Users/ali/.local/state/bukit-verification/stage10-guard-fix-20261029`（`focused/guard.trx` 5/5、`full/architecture.trx` 420/420、`closure.json`、`cache.json`）；`D=/Users/ali/.local/state/bukit-verification/stage10-audit-fix-20261030`（`focused/guard.trx` 5/5、`full/architecture.trx` 420/420、`cache.json`）；`D=/Users/ali/.local/state/bukit-verification/stage10-coverage-20261029`（`coverage/coverage-summary.json`、`coverage/coverage-summary.txt`、`security-layer.json`、`coverage.log`）；`D=/Users/ali/.local/state/bukit-verification/stage10-crash-probe-20261029`（`probe.trx`、`full/pluginhost.trx`、`closure.json`、`classification.json`、`cache.json`）；`D=/Users/ali/.local/state/bukit-verification/stage10-exitcode-20261030`（`focused/probe.trx` 8/8、`full/pluginhost.trx` 278/278）；`D=/Users/ali/.local/state/bukit-verification/stage10-exitcode-ci-40973740319/artifacts`（三个平台的 `platform.json` 与 PluginHost TRX，AbnormalTermination 各 1 次通过）；`D=/Users/ali/.local/state/bukit-verification/stage10-aot-20261029`（`package.log`、`smoke.log`、`artifacts/bukit-2.0.4-osx-arm64.tar.gz`、`artifacts/publish/osx-arm64/bukit`）；`D=/Users/ali/.local/state/bukit-verification/stage10-platform-ci-40904333690/artifacts`（三个平台的 `platform.json`、逐项目 TRX 与磁盘满专项）。`coverage.sh` 的 zsh 包装在完成后因只读变量 `status` 退出 1，门禁本身已输出 `Coverage check OK`；不做重复采集，日志已注明。

出口：架构测试全绿、基线与源码一致，原生冒烟、覆盖率和三个平台专项均有对应证据；缺授权、缺执行或异常跳过均记录为未完成。

### 阶段 11：Labs 与导入、克隆（后置，不在本期）

仅补契约与冒烟：命令解析、输出结构、不联网路径。外部插件（IndexNow、WechatSync、Import、Clone）契约测试同样后置，不属于阶段 9 出口。

## 6. 已知缺陷转化为测试

下列条目由源码观察与第 9 节决定形成回归清单，编号用于测试 Skip 原因及证据关联。每条先按第 4.3 节复现并确认期望行为，再进入 `KnownDefect`；不能仅凭方法返回原值或命令类未注册认定缺陷。

| 编号 | 缺陷或待复现条目 | 阶段 |
|---|---|---|
| KD-001 | **已按用户单独授权修复并通过**：`SeoExternalAuditor` 显式接收已验证报告契约，分别读取 SEO 的 `routes`/`url` 与发布的 `documents`/`routeUrl`；回环 canonical 与链接用例 GREEN，完整 CLI 1096/1096，见阶段 8 首批记录 | 8 |
| KD-002 | **已独立修复并通过，移除 Skip**：项目根 `.bukit` 全量保留，显式元数据输出目标先拒绝，正常输出／缓存清理不变；四选择方式与真实入口专项 69/69，专项／最终增量复核均 0 Critical / 0 Important / 0 Minor；当前见 5.3，历史 RED 保留 | 5 |
| KD-003 | **已按单独授权修复（方案 A）**：保留预算，超限禁用当前变体增量并完整渲染，不吞普通错误；最终 fixture 21 项行为 RED → 完整六类专项 168/168、0 Skip，六项真实产物对照通过，专项及唯一最终增量复核均 0/0/0，见 5.5 | 5 |
| KD-004 | **已独立修复并通过，移除 Skip**：writer 全量预检后写入，危险 alias／路径／链接以 `BuildOutputUnsafe` 拒绝并保留既有字节；包含 NUL 自别名补修，完整专项 53/53、0 Skip，规定复核无剩余阻塞；见 5.4，不新增 aliases 入口 | 5 |
| KD-005 | **已修复并通过**：`ConfigCollectionReader` 缺必填键的 `modelSchema` 条目曾静默丢弃（决定：应加载时报错）；证据见 2.1 | 2 |
| KD-006 | **原空集合修复保留；原始不完整条目补修已验证并经两次规定复核接受**：诊断保留全部条目，JSON-LD 仅完整条目、全无效无专用节点／无 Article 回退，warn/strict/off；`mentions` 与 Article `citation` 并存。历史见 6.6，当前见 6.7 | 6 |
| KD-007 | **原 Skip 已移除；审计后上下文补修已验证并经两次规定复核接受**：DOM 与窄脚本防护保留，新增 template/noscript/raw-text 及三个真实产物独立断言，当前 projection 176/176，见 6.7。6.4–6.6 保留历史，不冒充新闭包证明 | 6 |
| KD-008 | **已按用户单独授权修复并通过**：`deploy --dry-run` 不再构建，只校验现有输出并保留字节；RED 曾清理 `sentinel.txt`，GREEN 专项通过，完整 CLI 1096/1096，见阶段 8 首批记录 | 8 |
| KD-009 | **已修复并通过**：三入口共用歧义 URL 校验，拒绝 `/\attacker.example/payload`、反斜杠与嵌入控制字符；已移除 Skip/KnownDefect，证据见 1.7 | 1 |
| KD-010 | **已修复并通过**：`IsSubPathOf` 先排除规范化后的相等路径，卷根自身返回 false，`IsSameOrSubPathOf` 仍返回 true；已移除 Skip/KnownDefect，证据见 1.7 | 1 |
| KD-011 | **已修复并通过，移除 Skip**：保留公开 renderer 省略 null command；schema command 为可选 string/null，错误类型和未知字段仍拒绝。见 6.6 | 6 |
| KD-012 | **已修复并通过，移除 Skip**：schema 声明已有 `resourceLimitExceeded: string`、`networkPermissionGranted: boolean` 可选字段，版本和未知字段拒绝不变。见 6.6 | 6 |
| KD-013 | **已按用户单独授权修复并通过**：`PluginSecretMasker` 环境键掩码补充 `COOKIE`/`AUTHORIZATION`（含 `HTTP_*`）；RED 7 失败 / 21 通过 → 聚焦 28/28、完整 PluginHost 277/277、行覆盖 85.50%；报告 schema、公开 API 与权限模型不变。见阶段 9 I-01 记录 | 9 |

阶段 1 的首次复现见 1.6；KD-009/KD-010 经用户明确授权，在独立 Core 任务中修复，最新结果见 1.7。没有证据表明修复前 KD-010 可直接绕过 `OutputDirectoryCleaner`：该消费者还有路径相等和卷根的独立拒绝条件；本次对应 CLI 消费者测试已通过。

## 7. 执行规则

本计划的执行遵守 `AGENTS.md`：

- 只运行与改动对应的专项测试，例如 `dotnet test tests/Bukit.Config.Tests -c Release --filter "FullyQualifiedName~<类名>"`。
- 未经明确授权，不运行 `scripts/test-all.sh`、`scripts/smoke-all.sh`、整个解决方案测试、完整 Core 覆盖率采集或发布门禁；原生发布与产物冒烟同样先取得对应任务授权，列入计划不等于授权执行。
- 每批开工前用 `python3 scripts/checks/codex-workflow.py closure` 生成受影响闭包，并处理所有未映射文件。
- 每批核对测试类到执行命令的映射。跨平台用例须进入 `.github/workflows/ci.yaml` 的 `platform-tests` 对应项目与完整类过滤列表，不能只加入测试项目；记录实际 OS、进程架构、SDK、TRX 及适用用例数。仅允许既有明确的 `BUKIT_NOT_APPLICABLE` 方法/平台组合；能力缺失、未知跳过或适用用例为零均是验证缺口。
- 同一时刻只有一个写入者修改仓库文件。
- 专项测试通过后用 `codex-workflow.py cache record` 记录绿色证据；仅在 `cache check` 确认 HEAD、闭包内容、确切命令、相关环境变量状态及工具链全部匹配时复用。第 7 阶段明确要求的稳定性重复次数单独记录，不以缓存命中替代。
- 本计划新增或补充测试与测试辅助代码，不修改 Core 源码；缺陷修复是单独的、需用户确认的 Core 任务。若新增测试需要 CI 过滤列表或验证脚本接线，先登记为独立的验证配置任务，明确改动范围及直接 owner 自检；不得假定新增类会自动获得 CI 证据。

## 8. 风险

| 风险 | 缓解 |
|---|---|
| 12 万行代码，用例量约 3000 到 4000，周期长 | 按阶段出口分批交付，每阶段独立可用 |
| golden 快照随功能变化频繁失效 | 快照只放三个最小站点，更新需要评审 |
| 文件系统差异（大小写、符号链接、Windows） | 路径、链接、进程相关专项必须取得 Linux x64、macOS arm64、Windows x64 的实际证据；按第 7 节核对类过滤列表与适用用例，不能把条件跳过当作通过 |
| 假传输与真实 Notion 行为偏差 | 假传输只验证本项目的请求构造与错误处理，不声称等价真实服务；微信业务后置 |
| 竞态用例抖动 | 并发用例连续运行 20 次作为稳定标准 |

用例总量为按"每百行 1 到 3 个"的经验比例粗估，不是精确数字。

## 9. 待决事项

以下 6 项已于 2026-10-24 由负责人决定。

| # | 事项 | 决定 | 影响 |
|---|---|---|---|
| 1 | 覆盖率目标 | 整体行覆盖 ≥ 84%，每个 Core 源项目 ≥ 70%；仅对 Engine 安全层上调：`BuildTransaction`、`OutputDirectoryCleaner`、`AssetOutputPlan` 各自行覆盖 ≥ 95% | 所有阶段统一使用本行阈值；阶段 5 按第 4.4 节分别验证安全层，最终验收另验整体与全部源项目 |
| 2 | `modelSchema` 缺必填键的条目 | 加载时报错 | 阶段 2：测试期望抛 `ConfigException` |
| 3 | `clean` 对 `.bukit/plugins.yaml` 与 `plugins.lock.yaml` | 保留，只删缓存与构建产物 | 阶段 5：测试期望配置与锁文件不被删除 |
| 4 | `deploy --dry-run` | 不构建，只校验现有输出；无输出时报错并提示先 build；不校验 provider secrets | 阶段 8：测试期望输出目录不被改动 |
| 5 | 范围 | 只做 Core，外部插件与 Labs 后置 | 阶段 9 只含 PluginHost 与协议；阶段 11 及外部插件契约延后 |
| 6 | 别名含 `..` 或绝对路径 | 拒绝并报错，给出诊断码 | 阶段 5：测试期望构建失败且不写入任何文件 |

**对 Core 源码的影响**：决定 2、3、4、6 对应独立 Core 修复任务；第 6 节先补齐复现与期望行为证据，尤其不能把仍待确认的路径越界当作已验证。按 `AGENTS.md`，Core 修改须经用户确认；本计划提供测试，确认失败的用例在修复前按第 4.3 节同时标 `KnownDefect` 与显式 Skip。

## 10. 验收

计划视为完成的条件：
1. 阶段 0 到 10 的出口标准全部满足。
2. 第 3 节的 6 项质量目标均有对应的自动化测试和逐项证据；已执行测试稳定通过，涉及 KnownDefect 的未满足项单列，不把跳过写成质量目标已通过。
3. 第 6 节每条都有测试与行为依据，处于"已修复并通过"或"已复现、显式跳过并说明原因"状态；若复现证明不构成缺陷，附依据并从缺陷清单移除，保留正确契约测试。
4. 覆盖率按第 4.4 节取得证据并达到第 9 节统一阈值。

## 11. 修订记录

| 日期 | 说明 |
|---|---|
| 2026-10-24 | 初稿 |
| 2026-10-24 | 记录 6 项待决事项的决定；覆盖率、缺陷语义、范围随之更新 |
| 2026-10-24 | 按审计修订：澄清公共产物与报告比较、已知缺陷显式跳过、Notion 假传输、覆盖率证据及项目映射、当前 schema、原生冒烟、三平台执行与 Core 范围；移除两项缺乏失败用例的缺陷认定 |
| 2026-10-24 | 细化并执行阶段 1：12 组 Shared 风险用例、确定性 DNS/路径/句柄边界、精确命令与覆盖率证据；记录最终 511 Passed、2 KnownDefect Skip、87.21% 行覆盖，以及 KD-009/KD-010、TRX 跳过计数兼容问题和平台验收缺口 |
| 2026-10-24 | 经授权独立修复 KD-009/KD-010，摘除 Skip/KnownDefect；最新 Shared 530 Passed、零跳过、87.27% 行覆盖，macOS 平台 41 项适用用例通过，直接消费者 47 项通过；保留修复前历史、ImageSharp 还原告警限制及 Linux/Windows 待验证状态 |
| 2026-10-25 | 记录阶段0基础设施、授权 Core 修复与专项复核、三平台同输入43+7项通过及确切命令；保留36/4、164/16历史 RED、PSR-001缓存记账修正及独立文档收口边界 |
| 2026-10-27 | 按阶段 2 审计修复 I-1/I-2/I-3：补 50 项嵌套 schema 对照、完整 Engine/CLI 集合闭包、阶段 2 执行记录；KD-005 转已修复，保留 SR-004/KD-005-TEST-01 非阻塞 Minor |
| 2026-10-27 | 关闭 R-1：嵌套 schema 对照改为解析 `ConfigStrictFieldValidator` 的 `RequireOnly`/`ValidateSequenceMappings` 源文本，双向覆盖字段与路径并加负向自检；完整 Config 430/430、覆盖 89.45%；非阻塞 Minor 合并为 SR-004、M-1、M-2 / KD-005-TEST-01 三项 |
| 2026-10-27 | 执行阶段 3：补 `ContentDocument` 回退/状态/关系/媒体、Markdown BOM/CRLF、Canonical 图重复 id/悬空引用/validator 表征用例；领域专项 566+41+427+85，Engine 定向 11/11；Content 93.06%、Engine.Abstractions 76.23%、Notion 95.30%，均 ≥70% |
| 2026-10-27 | 阶段 3 审计复审（Critical 0 / Important 3 / Minor 7）后按 B/B 修复：`requireRelationTargets` 解析 content/entity 目标、重复 ID 报错；完整 Engine 2671/2671、Architecture 415/415、CLI 1076/1076；领域 568+41+427+88；Content 93.08%、Engine.Abstractions 80.48%、Notion 95.30%；补 Trait、Markdown 边界、作者投影、draft `index.html`、项目映射与证据复制 |
| 2026-10-27 | 阶段 3 审计修复最终复核：Critical 0 / Important 0；A-1、M-1~M-7、D-1~D-4 关闭；完整 Engine 2671/2671、CLI 1076/1076、Architecture 415/415；15 个文件未提交 |
| 2026-10-27 | 执行阶段 4 基线：Routing 81/81（88.89%）、Theme 179/179（79.59%）、Rendering 346/346（90.31%）、Engine 路由 118/118、Architecture 415/415；补 permalink encoding、严格 theme.yaml、`content_model.slug` 与渲染确定性用例；按审计决定 C 按模板判定文件/目录（Core） |
| 2026-10-27 | 阶段 4 最终复核：Critical 0 / Important 0；M-2/M-3/M-5/M-8/M-9/M-10 关闭，M-1/M-4/M-6/M-7 记录为非阻塞；Routing 88.89%、Theme 79.59%、Rendering 90.31%；10 个文件未提交 |
| 2026-10-27 | 执行阶段 5 基线：OutputDirectoryCleaner 96.15%、BuildTransaction 96.46%、AssetOutputPlan 99.34%，均 ≥95%；Engine 2682/2682、Architecture 415/415；补 Cleaner 11 个安全边界用例并修复根路径/Unix symlink 检测；增量等价与 ownership 复用既有完整 Engine 用例 |
| 2026-10-27 | 阶段 5 安全层：OutputDirectoryCleaner 96.23%、BuildTransaction 96.46%、AssetOutputPlan 99.34%，`security-layer.py` 通过；Engine 2683/2683、Architecture 415/415；case-sensitive APFS 上 AssetPipeline 30/30，修复 DirectoryCopy symlink 外部目标跳过与 Cleaner 根/用户目录/符号链接守卫 |
| 2026-10-27 | 阶段 5 最终复核：Critical 0 / Important 0；I-1/I-2 关闭，6 项 Minor 非阻塞；Engine 2683/2683、Architecture 415/415、Cli 1076/1076；安全文件 51/53、191/198、150/151；4 个文件未提交 |
| 2026-10-28 | 阶段 5 审计：**FAIL**。覆盖率/回归数字属实，但 KD-002/003/004 未复现、增量证据不足、AsyncLocal 无直接用例、缺五项三类对照表；测试阶段 Core 改动未授权、跨平台未接线。§5.1 状态改回 FAIL |
| 2026-10-28 | 阶段 5 整改：KD-002/003/004 补 RED+KnownDefect+Skip；9 个 Contributor 失效/边界用例、AsyncLocal 直接用例、五项三类对照表；Engine 2693/2695（2 Skip）、Cli 1076/1077（1 Skip）、Architecture 415/415；安全文件 51/53、191/198、150/151；I1 拆 5.2，I2 待接 CI |
| 2026-10-28 | 阶段 5 整改续：Cleaner 删除不可达根分支、macOS 用户目录大小写比较修复（RED→GREEN）；CI `platform-tests` 加入 Cleaner/AssetPipeline，临时运行 `40544532088` 的 43/43 后由 canonical run `40556553321` 取代；Engine 2694/2696（2 Skip）、Cli 1076/1077（1 Skip）、Architecture 415/415；安全文件 52/54、191/198、150/151 |
| 2026-10-28 | 阶段 5 I2 复跑：实际 `CI` run `40556553321`（gate=platform）在 Linux x64/Windows x64/Darwin arm64 执行 canonical `platform-tests`，全绿；Cleaner 13、AssetPipeline 30 三平台通过；平台 `platform.json`/SDK/架构/逐项目 TRX 已归档 |
| 2026-10-28 | 阶段 5 最终复核：Critical 0 / Important 0，6 项 Minor 非阻塞；阶段 5 判定 **SUCCESS**；12 个文件未提交 |
| 2026-10-28 | 阶段 5 两项 Important 修复复核：Cleaner 13 在 Linux/Windows/macOS 实际执行；KD-004 覆盖 `..` 与绝对路径并断言不写入；canonical run `40556553321`；Critical 0 / Important 0；判定 **SUCCESS** |
| 2026-10-28 | 经用户授权单独修复 KD-002：CLI clean 保留根 `.bukit`，拒绝显式元数据目标（含子目录／大小写变体），保留正常输出与 `.cache` 清理；RED 13 失败 → GREEN 清理／真实入口／CLI 契约 69/69、0 Skip；专项与唯一最终增量复核均 0/0/0。仅本机 macOS，不扩展阶段 6、KD-003/004、SCSS 或全量／发布验收；见 5.3 |
| 2026-10-27 | 经负责人授权的独立 Core 修复（4.3，复审 N-2）：显式 `route.url` 与 permalink 只对 `.html/.htm/.xml/.json/.txt` 视为文件，其余为目录；RED 8 失败 → GREEN Routing 98/98（88.96%）、Engine 2671/2671、Content 568/568、Architecture 415/415；同步用户文档 04/05 |
| 2026-10-27 | 经负责人授权的独立 Core 修复（4.4）：`route.url` 与 permalink 末段为静态文件扩展名时构建失败并提示 `static/`/`assets/`；RED 10 失败 → GREEN Routing 112/112（90.24%）、Engine 2671/2671、Content 568/568、Architecture 415/415；同步用户文档 04/05/14；openspec 过期映射无法执行已记录 |
| 2026-10-29 | 阶段 6 只读审计后经授权复验当前分支 `79df55f8`：完整 Engine 2934/2934、0 Skip（含全部已修复 KD）、Engine 行覆盖 89.92%，证据持久保存；跨平台与 CLI/PluginHost/Shared/Architecture 当前专项仍未验证；见 6.10 |
| 2026-10-29 | 阶段 6 复审后经授权补跑非 Engine 专项（`800472a2`）：CLI 141/141、PluginHost 26/26、ConsoleLogger 21/21、Architecture owner 48/48，均 0 Skip；24 份报告正反例均有当前证据；§6.7 与矩阵 §6 标注为已清理的历史证据；见 6.11 |
| 2026-10-29 | 接入 `TaxonomyRedirectWriterTests`、`CleanCommandTests` 至三平台 `platform-tests`；为通过 fast gate 修正既有断链、清除 5 条分析器回归、将 `NormalizeUrl(string, bool)` 改为 internal、补 Windows POSIX N/A 名单；CI run `40800207604` 三平台全部 success，完整 Engine 2934/2934、覆盖 89.92%；见 6.12 |
| 2026-10-29 | 阶段 8 审计修复：无输出错误补充 `Run 'bukit build' first.`；确认 `--dry-run` 不校验 provider secrets 并补显式用例；补齐 `publish diff` 成功 0、`config schema` 失败 1；逐路径退出码矩阵入计划；CLI 1099/1099、0 跳过，Cli 88.22%、Cli.Shared 96.73%、Architecture 415/415；三平台 CI `40848651711` 全部成功 |
| 2026-10-29 | 阶段 8 最终判定 **SUCCESS**：正式 CI `40844256570`（提交 `1fee2730`，SDK 10.0.403）三平台全部成功；CliContract 12、Completion 12、Deploy 12、Publish 4 在三平台实际执行，Windows 仅既有 1 项 DevPathGuard N/A；本地 CLI 1096/1096、0 跳过，Cli 88.12%、Cli.Shared 96.73%；首次运行 CA1502 超基线已修复并保留为诊断 |
| 2026-10-29 | 阶段 8 首批：基线 CLI 1092/1092、Cli 88.05%、Cli.Shared 96.73%；补命令与 CLI 参考文档一致性、真实裸仓库分支/CNAME、version/completion 退出码用例；经用户单独授权修复 KD-001/KD-008，移除 Skip；完整 CLI 1096/1096、0 跳过，Cli 88.12%、Cli.Shared 96.73%；四个 CLI 测试类接入三平台 `platform-tests`，owner 自检通过；阶段保持 PARTIAL，待 Linux/Windows 实际执行证据 |
| 2026-10-29 | 阶段 9 测试批次 + I-01 授权修复：PluginHost 277/277、Abstractions 20/20，覆盖 85.50%/88.03%（手写源 100%）；补 manifest validator、CI policy、协议响应、哈希边界、清单版本门禁、DTO round-trip；经用户单独授权补充 `COOKIE`/`AUTHORIZATION` 掩码，RED 7 失败→GREEN，登记 KD-013；阶段 9 判定 **SUCCESS**；未跑全量/三平台/发布门禁；已提交到 `codex/stage9`，未推送 |
| 2026-10-29 | 阶段 9 只读审计确认：SUCCESS 判定成立；该次覆盖率采集未使用 `coverage.runsettings`，88.03% 含生成代码；CI 门禁口径为手写源 204/204 = 100%（2026-10-30 更正）；执行证据已镜像到 `/Users/ali/.local/state/bukit-verification/stage9-plugin-*` 并逐文件校验；过度掩码取舍入档；S-01/M-01~M-03 非阻塞，真实崩溃探针与三平台分支按计划后置 |
| 2026-10-29 | 阶段 10 执行：Architecture 420/420（修正后 fast gate `40907848663`），public API drift 退出 0，完整覆盖率 90.81%（18 个源项目均 ≥70%，安全层 96.46%/96.30%/99.34%），macOS arm64 AOT 冒烟通过，三平台 CI `40904333690` 全绿；新增 Core→Plugins/Labs 守卫（含反斜杠路径回归）与真实异常终止 self-kill 用例；阶段 10 判定 **SUCCESS**；未跑全量解决方案或发布门禁 |
| 2026-10-30 | 阶段 10 审计修补：补 2026-10-29 完整覆盖率/AOT/三平台 CI 的明确授权记录；阶段 9 归档 README 与更正日期统一；AOT SHA-256 入正文；self-kill 改为精确退出码断言并经三平台 CI `40973740319` 验证（Windows -1，Linux/macOS 137）；守卫增加 Core 项目数 18 断言；聚焦 5/5、Architecture 420/420 |
| 2026-10-30 | 阶段 9 M-01/M-02 错误契约收口：argument/option 重名改为 `ConfigException` + `ConfigInvalidValue`；哈希文件缺失/不可读返回失败结果而非原始 I/O；RED 7 失败/20 通过，GREEN 聚焦 27/27、PluginHost 286/286、CLI PluginCliIntegrationTests 40/40、public API drift 退出 0；runtime 命令重名随后续任务关闭，M-03、S-01 保持开放 |
| 2026-10-30 | 阶段 9 M-01 runtime 命令重名收口：validator 递归查重、CLI 对 runtime-only 传入空静态命令做查重并保留既有语义，顶层/嵌套重名抛 `ConfigException` + `ConfigInvalidValue`；RED 3+1 失败，GREEN PluginHost 289/289、CLI 1102/1102、public API drift 0、分析器 421/421；M-03、S-01 保持开放 |
| 2026-10-31 | 阶段 9 M-03 MaskText 契约收口：明确按值、仅授权环境变量、非密钥键也掩码的安全偏向；新增 3 个契约用例；聚焦 19/19、PluginHost 292/292、public API drift 0、分析器 421/421；仅 S-01 保持开放 |
