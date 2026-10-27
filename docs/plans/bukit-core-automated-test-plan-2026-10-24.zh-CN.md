# Bukit Core 自动化测试计划书

> 日期：2026-10-24
> 状态：草案，第 9 节待决事项已全部决定
> 范围：`src/Bukit-Core`（14 个项目）；`src/Bukit-Plugins` 与 `src/Bukit-Labs` 后置，不在本期
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

14 个 Core 源项目复用现有 13 个 Core 测试项目：`Bukit.Cli` 与 `Bukit.Cli.Shared` 都映射到 `tests/Bukit.Cli.Tests`，其余 12 个源项目映射到 `tests/<源项目名>.Tests`。不另建 `Bukit.Cli.Shared.Tests`；架构专项使用现有 `tests/Bukit.Architecture.Tests`。CI 的 Core 测试项目入口以 `scripts/checks/coverage/list-core-projects.sh` 为准，每批闭包核对源项目、测试项目及实际执行入口，避免新增测试未被调度。

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

**阶段 2：SUCCESS（I-1/I-2/I-3 已关闭；保留两个非阻塞 Minor）。**

原始执行基线为 `bcae33b2`；KD-005 的 `ConfigCollectionReader` 生产修复已在该提交中。本复审新增 `CollectionsValidator` 的语义回退，避免把所有额外 collection 一律判为未引用；同时补测试、执行证据和计划记录。证据根为 `/Users/ali/.local/state/bukit-verification/config-phase2-audit-remediation/`，提交前证据另见 `/Users/ali/.local/state/bukit-verification/config-phase2-20261027/`。

| 要求 | 证据 |
|---|---|
| 完整 Config 专项 | `bash scripts/checks/coverage/run-one.sh Release tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj TestResults/coverage/config-phase2-audit-final`；**428 passed / 0 failed / 0 skipped**，wrapper exit 0；`tests.trx` 与 `coverage.cobertura.xml` 位于该目录 |
| 单项目覆盖率 | Config **89.45% (2966/3316)**；`coverage-summary/coverage-summary.json`。Config-only policy 的自动最小值为 0/0，70% 门槛为人工核对；整体 75.88% 不是阶段指标 |
| 缓存记录 | `cache/config-final.record.json`，59 个 Config 源码/测试/SDK 配置文件；`cache/check.log` 显示 CACHE HIT `356370b3...95a4f` |
| I-1 schema 逐字段对照 | `tests/Bukit.Config.Tests/ConfigSchemaNestedContractTests.cs`：50 个嵌套/动态映射 case，覆盖 `site.seo`、`site.analytics`、`collections`、`modelSchema`、`notion`、`menus`、`components`、`build.report` 等；provider `oneOf`、plugin toggle 和动态 map 单独断言。5 个测试全部通过 |
| I-3 Engine 闭包 | 11 个含 collection 配置的测试类完整执行：**255/255 passed**，`results/engine-2/engine-closure-2.trx` |
| I-3 CLI 闭包 | 3 个 CLI 测试类完整执行：**54/54 passed**，`results/cli-2/cli-closure-2.trx` |
| KD-005 | `ConfigCollectionReader` 对 canonical/custom/fieldScopes/entity/relation 映射缺失必填键抛 `ConfigRequiredFieldMissing`；五组 RED 后转 GREEN，无 Skip |
| CFG-001 | 保留 `addToCollections` 引用和大小写比较修复；关闭“额外 collection 一律拒绝”的过度收紧，恢复“所有 source 已声明 collection 时允许额外 collection”的既有语义，避免 Engine 中依赖 front matter/空集合的 89 个用例回归 |
| 审查 | 审计发现 I-1/I-2/I-3 已关闭；Core 修复专项复核、限定复核及最终 delta 复核完成，Critical 0 / Important 0。SR-004（裸键诊断精度）、KD-005-TEST-01（索引/实体/关系边界用例）、M-1（默认值断言广度）和 M-2（KD-005 边界）保留为非阻塞 Minor；最终复核见 `config-phase2-audit-remediation/final-review.md` |

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

### 阶段 4：路由、主题、渲染

- 路由：permalink 模板、分页、列表路由、冲突、路径安全。
- 主题：`theme.yaml` 严格校验、extends、section 数据与 schema、tokens。
- Scriban：模板模型字段、函数、错误定位、`page.content_model.slug` 契约、linter 与符号分析。
- 渲染确定性：同一输入同一输出。

出口：三个项目分别达到第 9 节的单项目覆盖率下限。

### 阶段 5：Engine 安全层

按顺序：
1. `OutputDirectoryCleaner`：站点根、用户主目录、文件系统根、`.git`、符号链接、无 marker 的非空目录。
2. `BuildTransaction`：提交前失败保持原状、提交中途失败反向还原、备份保留、异步上下文、资源租约冲突。
3. `AssetOutputPlan`：精确冲突、结构冲突、大小写敏感与不敏感文件系统。
4. 增量构建：manifest v3、各 Contributor 的失效条件、目录哈希超限行为、增量与全量等价。
5. 输出归属：过期文件清理、未跟踪文件保留、遗留或无效归属触发的恢复。

出口：以上五项每项至少含正常、失败、边界三类用例；按第 4.3 节比较的增量等价测试通过，三个安全层文件分别达到第 9 节阈值并附第 4.4 节证据。

### 阶段 6：Engine 输出与审计

- 内置插件逐个：Alias、Archive、DataFiles、Feed、ImageProcessing、LlmsTxt、Menu、PagesIndex、Pagination、RelatedContent、SearchIndex、Sitemap、Taxonomy、Analytics。
- 搜索索引：HTML 去标签、script 与 style 处理、截断、片段。
- SEO：JSON-LD 各类型、标题模板、canonical、hreflang、alternates、Open Graph。
- GEO：`llms.txt`、`llms-full.txt`，FAQ、HowTo、引用结构。
- 审计：SEO、GEO、发布、机器可读性信任、隐私检查、路由清单。
- 对阶段 0 的当前 Core 报告清单逐项做契约校验，使用各报告声明版本对应的本地 schema。

出口：Engine 达到第 9 节的单项目覆盖率下限；当前 Core 报告清单中的每份报告均有真实生成的通过用例，以及按对应 schema 拒绝错误字段或类型的失败用例。

### 阶段 7：管线集成与端到端

- 三个 fixture 站点完整构建，公共产物与 golden 快照按第 4.3 节逐字节比较，报告独立校验。
- 确定性：连续构建两次的公共产物一致；改动单文件后，增量与相同最终输入的全量构建公共产物一致，分别验证缓存与渲染统计。
- 多语言根聚合：sitemap、feed、llms、robots、search、agent manifest。
- 失败注入：渲染中途失败、磁盘满、插件崩溃，验证正式输出不被破坏。
- 并发：并行与串行渲染的公共产物一致，各自报告的计数和哈希正确。

出口：golden 与确定性测试稳定通过，连续运行 20 次无抖动。

### 阶段 8：CLI

- 解析器与帮助：选项、必填、冲突、未知参数、退出码。
- 命令：`build`、`doctor`、`config`、`clean`、`preview`、`version`、`completion`；以当前 Core 注册表为准，不因存在未注册的命令类而增加公开命令。
- `seo`、`geo`、`publish` 及子命令：报告读取、schema 校验、`diff`、`--external`。
- `deploy`：本地裸 git 仓库验证 `--dry-run`、分支名校验、CNAME、超时。
- `dev`：监听范围、重建、WebSocket 握手、普通 HTTP 请求处理。
- 命令与文档一致：每个注册命令都出现在 CLI 参考文档中，反之亦然。

出口：Cli 与 Cli.Shared 分别达到第 9 节的单项目覆盖率下限；每个注册命令至少一个成功和一个失败退出码用例。

### 阶段 9：插件体系

- PluginHost：清单加载与迁移、平台选择、哈希与路径校验、权限评估、环境清空与白名单、密钥掩码。
- 协议 v1：handshake、manifest、invoke 的正常与异常（超时、崩溃、乱码、超大输出、进程树泄漏）。
- 锁文件与 CI 策略。

出口：PluginHost 与 Plugin.Abstractions 分别达到第 9 节的单项目覆盖率下限；每类异常都有对应用例。

### 阶段 10：架构与门禁

- 依赖矩阵：`Engine.Abstractions` 不引用 `Config`；Core 不引用 Plugins 与 Labs。
- 公共 API 漂移基线。
- AOT 发布冒烟：`PublishAot` 成功后，对实际原生可执行文件运行配置校验、完整构建和发布审计。复用 `bash scripts/smoke/release-artifacts.sh <archive-or-publish-dir> <rid>`，它将 fixture 复制到临时目录，再调用 `scripts/smoke/core.sh` 的 `config check`、`build --clean`、`publish audit`；只运行 `version` 不算通过。执行前按第 7 节单独明确授权并固定产物路径与 RID。
- 覆盖率下限校验：第 4.4 节的整体、各源项目和三个安全层文件检查全部通过。
- 平台专项：取得 Linux x64、macOS arm64、Windows x64 的实际执行证据，按第 7 节核对新增测试类的 CI 入口及适用用例数。

出口：架构测试全绿、基线与源码一致，原生冒烟、覆盖率和三个平台专项均有对应证据；缺授权、缺执行或异常跳过均记录为未完成。

### 阶段 11：Labs 与导入、克隆（后置，不在本期）

仅补契约与冒烟：命令解析、输出结构、不联网路径。外部插件（IndexNow、WechatSync、Import、Clone）契约测试同样后置，不属于阶段 9 出口。

## 6. 已知缺陷转化为测试

下列条目由源码观察与第 9 节决定形成回归清单，编号用于测试 Skip 原因及证据关联。每条先按第 4.3 节复现并确认期望行为，再进入 `KnownDefect`；不能仅凭方法返回原值或命令类未注册认定缺陷。

| 编号 | 缺陷或待复现条目 | 阶段 |
|---|---|---|
| KD-001 | `SeoExternalAuditor` 硬编码读取 `routes`，发布报告用 `documents` | 8 |
| KD-002 | `clean` 删除 `.bukit`，含手写 `plugins.yaml` 与锁文件（决定：应保留） | 5 |
| KD-003 | `HashUtil` 对超限文件只记长度，同长度改动会误命中缓存 | 5 |
| KD-004 | `TaxonomyRedirectWriter` 别名经 `Trim` 后拼路径，是否可越界待复现（决定：含 `..` 或绝对路径应拒绝并报错） | 5 |
| KD-005 | **已修复并通过**：`ConfigCollectionReader` 缺必填键的 `modelSchema` 条目曾静默丢弃（决定：应加载时报错）；证据见 2.1 | 2 |
| KD-006 | 无条目的 FAQPage、HowTo 走 Article 构建器，且引用输出两份 | 6 |
| KD-007 | `StripHtmlToText` 丢失两个 script 或 style 块之间的文本 | 6 |
| KD-008 | `deploy --dry-run` 仍执行 clean 构建（决定：应不构建，只校验现有输出） | 8 |
| KD-009 | **已修复并通过**：三入口共用歧义 URL 校验，拒绝 `/\attacker.example/payload`、反斜杠与嵌入控制字符；已移除 Skip/KnownDefect，证据见 1.7 | 1 |
| KD-010 | **已修复并通过**：`IsSubPathOf` 先排除规范化后的相等路径，卷根自身返回 false，`IsSameOrSubPathOf` 仍返回 true；已移除 Skip/KnownDefect，证据见 1.7 | 1 |

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
| 4 | `deploy --dry-run` | 不构建，只校验现有输出；无输出时给出明确错误，提示先 build | 阶段 8：测试期望输出目录不被改动 |
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
