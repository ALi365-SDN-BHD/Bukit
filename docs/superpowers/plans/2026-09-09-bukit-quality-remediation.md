# Bukit 质量改进执行方案

批准基线：`main / 65e761486f7ad5741aa569a52c3a94cf76cc4fc5`。用户于 2026-09-09 授权实施以下方案及所列本地专项验证。实施、测试、提交、推送、远端运行分别授权；本次不提交、不推送、不发布，不启动远端 CI。

## 1. 目标与范围

完成五类改进：增量诊断准确性、SSRF 测试有效性、内部结构精简、三平台验证、CI 与文档收敛。

- 覆盖 Linux x64、macOS arm64、Windows x64 的必要专项。
- 调整指定内部结构契约，删除已确认无价值的转发层。
- 合并普通 Core 测试和 coverage 执行，保留必要的非插桩专项。
- 公开 API、配置、序列化格式、插件协议和安全边界保持不变。
- 不新增依赖，不修改 Labs/外部插件业务，不改变发行审批和部署流程。

执行顺序：**验证映射补齐 → Engine 修复与精简 → SSRF 测试修复 → CI/平台改造 → 文档同步 → 最终增量复审**。

## 2. 分批实施

### P0：补齐验证映射和执行基线

预检发现 CI workflow、coverage 单项目入口、三份 README、消费者声明及部分历史计划没有专项映射。

1. 在现有 `scripts/checks/codex-workflow-policy.v1.json` 补充精确路径规则，不增加整个 `docs/**` 或 `.github/**` 的宽泛豁免。
2. 为 coverage runner 注册直接 owner self-test；同步 owner 路由及其测试。
3. 固定映射关系：

| 变更面 | 专项归属 |
|---|---|
| `.github/workflows/ci.yaml` | Architecture、active-workflow boundary 及其 self-test |
| `scripts/checks/coverage/run-one.sh` | 新增 `coverage-run-one-self-test.sh`、Architecture |
| `README.md`、`README.zh-CN.md`、`README.ms.md` | `readme-sync.sh`、`cli-docs-sync.sh` |
| 当前消费者声明、内部契约说明 | Architecture、现有 public-api owner self-test |
| 本次计划及历史状态说明 | 精确映射到本次记录的文档/契约检查 |
| 映射策略及 owner 路由自身 | 各自现有 self-test |

4. 本文件为唯一实施计划和状态记录。
5. 每批开始前用计划变更清单生成 closure，新增文件同样纳入；`unmappedFiles` 必须为空才能修改该批业务文件。

验收：新增路径全部可映射；self-test 能发现漏映射、错误 owner 和重复执行项；不修改原有验证阈值或授权边界。

### P1：修正增量诊断，并精简指定内部执行层

主要入口：`src/Bukit-Core/Bukit.Engine/PageRenderDispatcher.cs`、`SiteEngine.cs`、`VariantBuildPipeline.cs`。

**A. 修复重建原因误分类。** 固定诊断优先级：

1. 新页面：`new_page`
2. 输出缺失：`output_missing`
3. 模板变化：`template_changed`
4. 页面元数据变化：`content_changed`
5. 路由变化：`route_changed`
6. 渲染依赖变化：`render_dependency_changed`
7. 已计算的正文哈希变化：`content_changed`
8. 其他需要渲染的情形：`render`

只有正文哈希已计算时才比较正文哈希；不为诊断额外读取正文；不改变是否跳过渲染的判断、输出内容和 manifest 格式。在现有 `PageRenderDispatcherMetricsTests` 增加表驱动场景：仅路由、仅依赖、仅正文、模板、输出缺失、完全未变化、多项同时变化的优先级，并断言实际 rendered/skipped 数量。

**B. 调整指定内部契约后精简执行层。** 先更新消费者声明和 public-api 治理说明中 G-04D9A 当前决策，与实现、测试同批交付：

- 删除内部 `BuildPipeline`、`BuildPipelineContext`，公开 `SiteEngine.BuildAsync` 直接调用私有构建实现，传入原 config、root、overrides、token。
- 删除 `SiteEngine` 中无调用的私有 `FilterDocumentsByLanguage`。
- 删除 `VariantBuildPipeline` 中仅转发到现有 planner 的入口，测试调用真实实现。
- 删除仅由测试调用的 `BuildStaticHtmlData`，将静态 HTML 场景迁移到生产使用的 `VariantRouteStage` 测试。
- 保留真实编排或错误处理的 `ExecuteAsync`、路由检查和执行记录逻辑。

移除原 `BuildPipelineTests` 委托身份断言；通过 `SiteEngine` 入口或已有集成测试验证取消、参数、异常、结果。G-04D9A 架构测试验证两类型不存在、公开入口不变；不删除其他 RoutePipeline 契约。不修改公开 API baseline 掩盖差异，预期 drift 为零；不改写不可变候选清单及历史决策事实。

验收：原因分类正确；公开入口与产物语义不变；删除入口无生产/测试调用残留；Engine、直接 CLI 消费者、Architecture 专项通过。

### P2：让 SSRF 集成测试真正验证防护

修改现有 `SsrfGuardIntegrationTests`，不修改 SSRF 生产策略。

- 使用 `SsrfGuard.CreateSafeHandler()` 验证真实工厂接线；handler 显式关闭代理。
- 使用 `TcpListener(IPAddress.Loopback, 0)` 获取受控临时端口。
- 普通 handler 先完成本地请求证明同一端点可达，再用安全 handler 请求；要求 `HttpRequestException` 且异常链含 SSRF 拒绝原因。
- 确认服务端未收到被保护请求；取消令牌和有界等待完成清理，不用固定 sleep 或外部网络。
- 临时测试副本移除安全 callback，确认负向断言失败，副本不进入仓库。
- 保留并执行现有 Shared 地址筛选测试。不扩展 `security-regression.sh` 选择器，现有入口会选中修复后的集成测试。

验收：连接拒绝、普通超时、代理失败不能当成防护成功；无监听端口/后台任务残留；不增加框架或网络依赖。

### P3：合并 CI 执行并加入三平台专项

主要修改 `.github/workflows/ci.yaml`、coverage 单项目 runner、对应 Architecture/owner 测试。

**Core 完整测试只执行一次。**

- 保留现有 13 项 Core 清单，由 `coverage-projects` 执行测试和 coverage；移除普通 CI 再次调用 `ci-full.sh` 的完整测试步骤。
- 保留 job id `core-tests`、检查名称 `Core tests`，改为汇总；必须等待 coverage 项目矩阵、coverage 汇总、平台专项，必需项失败/取消/意外跳过均不能成功。
- 保留 `Core coverage`、`Fast contracts` 检查名称；整体 84%、单项目 70%、13 项清单、缺失 coverage 检查不变。
- 不改 `.github/workflows/release.yaml` 执行结构，不删除本地 `ci-full.sh`。

**补足测试结果证据。** 同一次 `dotnet test` 增加 TRX logger，保留安全目录校验和单项目目录清理。用标准库 XML 简短校验 TRX 有效、有实际测试、无失败/中断，单独记录跳过项。失败仍上传已有诊断，上传不能覆盖测试失败。新增 `coverage-run-one-self-test.sh`，假 `dotnet` 验证只调用一次、参数/filter、失败码透传、零测试/坏 TRX 被拒绝。

**三平台专项。**

| 平台 | Runner | 非插桩专项 |
|---|---|---|
| Linux x64 | `ubuntu-24.04` | PluginHost 进程限制、Engine 外部工具进程 |
| macOS arm64 | `macos-15` | 进程专项及路径、安全文件输出、预览、dev 服务 |
| Windows x64 | `windows-2025` | 同 macOS 范围，验证 Windows 实际分支 |

标签与架构依据 [GitHub 官方 runner 清单](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)。每任务记录并检查实际 OS、进程架构、SDK，不能仅依据名称宣称覆盖。

完整类选择器：

- PluginHost：`SystemProcessRunnerTests`、`PluginPathValidatorTests`。
- Engine：`ExternalToolProcessRunnerTests`；macOS/Windows 加 `SafeOutputFileSystemTests`、`DirectoryCopyFollowSymlinksTests`。
- Shared，仅 macOS/Windows：`PlatformPathHelperTests`、`PathUtilsTests`、`PlatformSafeSourceFileOpenerTests`。
- CLI，仅 macOS/Windows：`PreviewCommandTests`、`PreviewCommandExtendedTests`、`DevCommandTests`。

明确 POSIX 专用用例 Windows 记不适用；目标平台应支持的路径/链接检查因环境跳过必须记录验证缺口，不能宣称通过。

| 触发方式 | 执行范围 |
|---|---|
| PR、原有 push 分支、手动 `core` | fast、单次完整 Core coverage、三平台专项、汇总 |
| 手动 `coverage` | fast、完整 Core coverage |
| 手动 `fast` | fast，原语义不变 |

不改分支保护；远端验证前只读核对 required checks 与检查名称兼容性。

现有 Architecture 增加结构与反向用例：普通 CI 无完整 `ci-full.sh`；清单完整且 runner 一次测试；汇总依赖齐全且 fail/cancel/unexpected skipped 不变绿；平台/selector 存在；fast/core/coverage 条件无扩大遗漏；release workflow 合同仍通过。

### P4：收敛文档和历史措辞约束

- 三份 README 子命令说明链接权威 CLI reference，避免重复完整子命令列表。
- 为两份旧计划加历史/当前状态标识：`2026-08-06-core-release-must-fix-closure.md`、`2026-08-05-bukit-seo-geo-next-master.md`。
- 本文件记录问题、实现状态、验证命令和证据，不新增平行状态文档。
- 已有修复仅标“实现已存在”；无当前证据仍为“验证未确认”，不机械勾选。
- 当前机器 baseline 为 427 类型、14 assemblies；当前摘要引用机器 baseline，425 记录明确历史状态。
- 只调整相关 G-04D9A 测试，以及 G-04C 固定历史“编译通过/复审通过”措辞断言；保留候选清单完整性、公开 API、兼容性约束。
- 同步测试指南，说明单次 coverage、平台专项、非插桩边界。

## 3. 验证计划

所有命令在仓库根目录执行。下表是授权的验证清单，实际结果见第 6 节，不代表已执行。

| 批次 | 完整专项或直接 owner 验证 |
|---|---|
| P0 | `bash scripts/checks/codex-workflow-self-test.sh`；`bash scripts/checks/post-change-focused-owner-checks-self-test.sh` |
| P1 | `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release`；`dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release`；`dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release` |
| P1 API | `bash scripts/checks/public-api-drift.sh check Release`；`bash scripts/checks/public-api-drift-self-test.sh` |
| P2 | 完整 Cli；`dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c Release` |
| P3 | 完整 Architecture；`bash scripts/checks/coverage-run-one-self-test.sh`；`bash scripts/checks/active-workflow-boundary-self-test.sh`；`bash scripts/checks/active-workflow-boundary.sh` |
| P3 平台 | 对上述每项目执行 `dotnet test <项目> -c Release --filter '<完整类选择器以 OR 连接>' --logger trx` |
| P4 | `bash scripts/checks/readme-sync.sh`；`bash scripts/checks/cli-docs-sync.sh`；完整 Architecture；修改 testing 指南时 `bash scripts/checks/agent-governance-contract.sh` |

修复前运行新针对性用例取得 RED；修复后所属完整专项 GREEN。同工作区 .NET、fixture、缓存、manifest 写入串行。证据按 HEAD、closure 内容、精确命令、相关环境状态、SDK 记录，只在条件一致时复用。只读 source-consumer 调查可并行，仓库单一写入者。不额外运行 `test-all.sh`、`smoke-all.sh`、全量/发布门禁。本地检查不能替代另行授权的远端三平台结果。

## 4. 验收、交付与回退

每批通过专项后一次专项复审；仅 Critical/Important 触发修复和局部复审。最后生成一次 `review-scope` 并做一次最终增量复审，复用未失效证据。

整体完成条件：

1. 增量原因正确，渲染决策/输出不变。
2. SSRF 测试能区分安全拒绝与普通网络失败。
3. 指定内部转发层删除，公开 API/报告格式/安全语义不变。
4. 普通 CI 各 Core 项目完整执行一次，保留非插桩重复用途明确。
5. 三平台必需专项有实际证据。
6. 文档、实施状态、验证证据一致。
7. 无未关闭 Critical/Important、无未映射变更文件。

交付：代码/测试差异、内部契约修订、本计划/关闭记录、仓库外详细日志和指标。远端未授权或平台不可用则 **PARTIAL：本地实施已验证，远端/平台验收待完成**；不得删测试、降阈值、改平台保证获取绿色。

按批次差异回退：CI 可恢复双执行结构，Engine 可恢复该批实现；不回滚无关用户修改、不清理用户目录。未获 Git 交付授权，保留可审阅工作区差异。

## 5. 本期暂不实施

- Section plugin 已完成失败 Task 的潜伏问题：启用实际消费者前单独修复。
- 首次失败后输出目录恢复：保持 marker 安全规则，不引入事务发布。
- 新缓存、全面异步、细粒度依赖重构：等待真实性能数据。
- 真实 Notion、部署、收益验收：另指定消费者/环境，不以合成测试代替生产证明。
- 不扩大为全仓历史测试清理、通用 SSG、插件生态建设。

完成五批及必要验收后停止扩展，依据真实内部使用反馈决定后续开发。

## 6. 实施状态与证据

证据目录：`/tmp/codex-reports/bukit-remediation-20260909/`。外部日志是本次会话证据，未获得远端平台验证前不标整体完成。

以下批次表及修复小节保留各阶段的当时状态，包括历史 `PARTIAL`、失败和“待验收”记录；当前最终结论见本节末尾“最终关闭”。

| 批次 | 实现状态 | 当前验证 | 证据 |
|---|---|---|---|
| P0 | 精确映射、owner 路由及唯一计划已实施；控制器专项复审无阻塞发现 | 两个授权 self-test 均通过；RED 已确认；closure 无 unmapped | `P0-mapping-red.log`、`P0-owner-red.log`、`P0-workflow-green.log`、`P0-owner-green.log`、`P0-closure.json`、`P0-tests.json` |
| P1 | 原因分类及指定内部精简已实施；控制器专项复审无阻塞发现 | Engine 2322、Cli 986、Architecture 301 全通过；API drift 为零，API owner self-test 通过 | `P1-metrics-red.log`、`P1-engine-first.log`、`P1-*-green.log`、`P1-tests.json`、`P1-closure.json` |
| P2 | 真实工厂接线、受控可达端点及拒绝原因验证已实施；控制器专项复审无阻塞发现 | 定向测试通过；临时移除 callback 后预期拒绝断言失败；Cli 986、Shared 376 全通过 | `P2-targeted-green.log`、`P2-mutation-red.log`、`P2-*-green.log`、`P2-tests.json`、`P2-closure.json` |
| P3 | CI 单次 coverage、汇总、TRX owner 及三平台专项已实施；控制器专项复审无阻塞发现 | Architecture 310、runner/active owner 均通过；macOS arm64/SDK 10.0.100 实测 250 项适用、2 项明确 Windows 不适用；远端/Linux/Windows 待验收 | `P3-tests.json`、`P3-platform/platform.json`、`P3-parser-cases.json`、`P3-core-aggregate-cases.json`、`P3-closure.json` |
| P4 | README 权威链接、历史状态标识、当前 baseline 引用、指定措辞断言及测试指南已收敛；控制器专项复审已完成，无 Critical/Important/Minor 发现 | 当前 inventory 断言先 RED；Architecture 310、README/CLI docs、agent governance、API owner、workflow self-test 全通过；closure 无 unmapped | `P4-current-inventory-red.log`、`P4-*-green.log`、`P4-tests.json`、`P4-closure.json`、`P4-implementation.md`、`P4-review.md` |
| 最终增量复审 | 控制器已完成唯一一次增量复审，无 Critical/Important/Minor 发现 | 37 个变更文件全部映射；跨批次契约与证据差异已核对 | `final-review-scope.json`、`final-content-deltas.json`、`final-static-checks.json`、`final-review.md` |

阶段历史状态：**PARTIAL**。五批初始本地实施、授权专项与最终增量复审已完成。用户随后已授权验证分支提交/推送及草稿 PR；当时远端验证已开始，实际三平台和完整 coverage 汇总验收尚未完成。

最终复审逐项核对旧缓存失效及当前内容差异：P0/P2/P3 后续差异仅为本计划状态文字；P1 后续文档、架构断言及平台测试差异分别由 P4、P3 验证覆盖。保留原始日志作为对应版本的证据，不将失效记录宣称为当前缓存命中；环境状态差异见 `final-cache-checks.json`。本次最终状态注记不增加运行时证据，也不为刷新注记重复 fixture 或重建缓存。

### 远端集成修复 P3-CI

草稿 PR #65、远端提交 `9595d6f`、首次运行 `34309836839`：13 个 coverage 项目测试均通过，但汇总检测到 26 份报告而非 13 份。新增 TRX logger 会把 collector 原始报告复制为 TRX 附件；本地真实 collector 单项测试重现了同一份报告的两个相同副本。远端 SDK 为 10.0.401，本地复现 SDK 为 10.0.100。

修复仅在单项目 runner 中保留 TRX 声明的附件；只有本项目 GUID 原始报告与附件逐字节相同时才删除重复原件。额外报告、内容不一致、附件缺失、引用越界或符号链接均拒绝，不修改 `find-results.sh`、13 项清单、84%/70% 门槛或 TRX 证据。owner 用例先 RED 后 GREEN，真实 collector 修复后保留一份报告并通过原始计数检查；Architecture 310 通过，直接 owner 证据见 `P3-CI-tests.json`、`P3-CI-owner-green.log`。

coverage 修复已通过控制器专项复审，待远端重跑；初始最终增量复审不视为本后续修复的复审证据。详细记录：`P3-CI-coverage-repair.md`、`P3-CI-closure.json`、`P3-CI-real-duplicate-proof.json`、`P3-CI-real-green-files.log`。

### Windows 专项测试边界修复 P3-CI-Windows

首次 Windows 运行长时间未产生日志，具体等待位置尚未确证。源码确认 Dev 测试 helper 可在客户端提前结束后无界等待应用 context；1024 字符单 URL 段及编码空字符也可能被 Windows HTTP.sys 先于应用拒绝。本修复不把这些源码风险宣称为该次远端等待的已证实根因。

仅调整已批准的 Dev/Preview 测试：Dev 共享请求使用统一 5 秒截止、context/client 竞争、handler 与响应读取有界等待；提前响应明确失败，取消后关闭 listener 并有界观察任务结束。两项长路径改用 9 个 128 字符段，仍以真实 handler 验证总长超过 1024 的路径返回 404 且不泄漏路径；`/%00` 转入准确命名的 DevPathGuard 跨平台理论断言，其余可传输编码穿越继续通过 HTTP 验证 403。两项 shutdown 测试 finally 先释放受控 TCS，再完成取消/释放，保持真实等待断言。生产代码和 registry 未变。

有界阻塞回归先在 8 秒外层截止 RED 并释放清理，修复后 5 秒取消 GREEN；真实早响应反例确认未经应用 handler 的 400 不视为成功。完整 CLI 987 项通过、零 skipped；workflow self-test 通过。证据见 `P3-CI-Windows-helper-red.log`、`P3-CI-Windows-helper-green.log`、`P3-CI-Windows-tests.json`、`P3-CI-Windows-closure.json`、`P3-CI-windows-test-repair.md` 和 `P3-CI-windows-test-review.md`。本批控制器专项复审已完成，新的真实 Windows 运行验收待完成，旧运行取消不计通过。

### Windows 反斜杠传输输入修复 P3-CI-Backslash

首轮 Windows 完整日志 `remote-platform-windows-first.log` 确认 Preview 单编码反斜杠用例在客户端响应先完成、应用 context 未到达的分支超时；日志没有记录该响应的状态码，后续长期等待的具体用例仍未确证。保留这份目标平台失败证据，同时记录当前 macOS 原始输入的 4 项通过基线。

仅将 Dev theory、Dev fact、Preview fact 三处 HTTP 输入改为 `/%255c..%255csecret`，两个 fact 改名明确 DoubleEncodedBackslash；既有直接 DevPathGuard theory 保留原始 `/%5c..%5csecret`。已有三轮解码逻辑支持该输入，生产实现、helper、selector 均未改动。真实 handler 403 和无路径泄漏断言保持严格，不接受 400、不跳过测试。修复后 macOS 对应 4 项通过；双编码输入能否通过 Windows 原生传输到达应用仍须远端实测，不以本地通过代替。

本批完整 CLI 987 项、workflow self-test 均通过，命令及结果见 `P3-CI-Backslash-tests.json`；闭包、精确测试差异和实施记录见 `P3-CI-Backslash-closure.json`、`P3-CI-Backslash-test-delta.patch`、`P3-CI-backslash-repair.md`。控制器专项复审已通过，见 `P3-CI-backslash-review.md`；远端验收待完成。

### Windows 路径与 Dispose 契约修复 P3-CI-PlatformFix

首轮及第二轮 Windows 日志均有相同三项 PluginPathValidator 失败，不能将首轮 PluginHost 记为通过：一项是 `/usr/local/bin/plugin` 根相对输入被清洗为相对路径接受；两项是 fixture 的混合分隔符与规范化结果进行字符串前缀比较。第二轮另确认 Dispose 测试在成功等待 dispose/loop 后，客户端遭遇 `HttpRequestException → IOException → SocketException` 连接重置；这一传输结果不等于 gate 提前释放。

生产只将既有相对路径前置条件的 `IsPathFullyQualified` 换成 `IsPathRooted`，保留 Windows regex 和后续限制。三处测试 pluginRoot 改用平台路径组件。Dispose 测试使用既有 tracked-request 钩子，要求 entered、释放前 dispose 未完成、释放后真实 dispatch/loop/dispose 均成功且无日志错误；之后客户端成功仍须成功状态，仅 Windows 明确 `IOException → SocketException(ConnectionReset)` 可作为已停止传输的结果。没有 skip 或宽泛异常豁免。

本地新增日志空断言先暴露受控 handler 在 Stop 后写已 disposed 响应的问题，见 `P3-CI-PlatformFix-dev-response-after-stop-red.log`。仅调整该 handler 在进入前设 204、释放后不再写响应，由 host.Dispose 负责传输关闭；真实 dispatch/gate 完成断言保留，随后定向通过。此修复不改变生产 shutdown 行为。PluginPathValidator 定向 16、Dispose 定向 1、完整 PluginHost 215、完整 CLI 987、API drift 零及 workflow self-test 均通过。完整命令与结果见 `P3-CI-PlatformFix-tests.json`；闭包、实施记录见 `P3-CI-PlatformFix-closure.json`、`P3-CI-platform-fix.md`。本批控制器复审与新的 Windows 运行验收待完成。

### 最终关闭（2026-09-09，本地证据注记）

当前总体状态：**SUCCESS**。批准的五批改进、远端发现的限定修复及必要验收均已完成，无未关闭 Critical/Important/Minor 发现。[草稿 PR #65](https://github.com/ALi365-SDN-BHD/Bukit/pull/65) 的远端提交 `71c8a2fdabe50c466d1c2eb6c4b590fc305553cb` 在[第三轮 CI 34313388341](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/34313388341) 全部成功。

- `Fast contracts` 成功，Architecture 310 项通过。
- 13 个 Core coverage 项目共执行 5661 项测试，零失败、零跳过；总体覆盖率 **89.42%**，整体 84%/单项目 70% 门槛及恰好 13 份 coverage 文件检查全部通过。
- 三平台完整指定类专项及 `Core tests` 汇总成功。实际 OS、.NET host 架构和 SDK **10.0.401** 已由运行脚本校验；`platform.json`、`dotnet-info.txt`、TRX 与 coverage 产物已上传。

| 实际目标 | 适用用例通过 | 明确不适用 |
|---|---:|---:|
| Linux x64 | 57 | 1 |
| macOS arm64 | 251 | 2 |
| Windows x64 | 240 | 13 |

不适用项按既定精确方法/OS 规则单独记录，不计入适用通过数。Windows 的根相对路径拒绝、双编码反斜杠真实 HTTP 403、Dispose 排空及其余要求的路径/进程/服务专项已在完整类中通过；前两轮失败和取消没有被计作通过。

P0–P4 专项及一次最终增量复审已通过。所有后续限定修复也已通过控制器专项复审，见 `P3-CI-coverage-review.md`、`P3-CI-windows-test-review.md`、`P3-CI-backslash-review.md`、`P3-CI-platform-fix-review.md`。最终机器证据见 `remote-final-evidence.json`、`remote-platform-final-counts.json` 及 `remote-fast-third.log`、`remote-core-tests-third.log`、`remote-coverage-summary-third.log`、三份 `remote-platform-*-third.log`。

本段是远端 `71c8a2f` 已通过之后的**本地收尾注记**，不再推送或触发 CI；本地实施内容与该远端版本仅此计划注记不同，远端 PR 描述由控制器同步。本次仅运行计划的直接 workflow owner 检查，不重跑 .NET/平台 fixture。未合并、发布或部署；第 5 节暂不实施事项保持原范围。
