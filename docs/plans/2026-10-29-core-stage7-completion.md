# Core Stage 7 Completion Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 完成阶段 7 的增量确定性、聚合、失败安全、并发与 20 次稳定性出口。

**Architecture:** 继续使用隔离站点、真实 `SiteEngine`、原始字节快照及独立报告校验。优先扩展现有测试助手，不修改 Core 或公开契约；发现运行时缺陷时保留复现证据并单独确认修复范围。

**Tech Stack:** .NET 10、xUnit、既有测试基础设施；无新依赖。

## Task 1：增量与全量等价

**Files:** `tests/Bukit.Engine.Tests/Stage7DeterminismTests.cs`、`Infrastructure/SiteBuilder.cs`、`Infrastructure/ReportAssertions.cs`、主测试计划。

1. 在真实 taxonomy 站点构建中开启增量，先确认现有报告助手因固定假定增量关闭而失败；保留正确的增量断言。
2. 最小扩展 `SiteBuilder` 的构建选项和报告助手：分别核对本次缓存／渲染计数，并按报告生成时 marker 是否存在校验 release hash；保留旧调用默认行为。
3. 比较初始和无变动的增量构建、修改一个 Markdown 后的增量构建以及相同最终输入的新沙箱全量构建：公共路径及字节精确相等，缓存／渲染统计分别断言，每次构建独立验证报告。
4. 完整运行 `TestInfrastructureTests`、`Stage7GoldenTests`、`Stage7DeterminismTests`，检查 TRX 及无跳过；记录闭包、缓存与一次专项复核。输出目录使用独立证据根 `R/determinism`。

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~TestInfrastructureTests|FullyQualifiedName~Stage7GoldenTests|FullyQualifiedName~Stage7DeterminismTests' \
  --logger 'trx;LogFileName=determinism.trx' --results-directory "$R/determinism"
```

## Task 2：多语言根聚合

**Files:** `tests/Bukit.Engine.Tests/Stage7I18nAggregateTests.cs`、主测试计划。

1. 在现有 i18n 沙箱中断言根 sitemap、RSS/Atom/JSON feed、llms、robots、search 与 agent manifest，先确认缺少 feed 的原 fixture 为 RED。
2. 只在沙箱副本启用 merged feed、robots 和完整 llms；以两种语言的实际路由验证各根产物内容及独立报告，不改现有 fixture 或 golden。
3. 完整运行 `Stage7I18nAggregateTests` 并记录 TRX、闭包、缓存及一次专项复核。

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~Stage7I18nAggregateTests' \
  --logger 'trx;LogFileName=aggregate.trx' --results-directory "$R/aggregate"
```

## Task 3：真实并行与串行等价

**Files:** `tests/Bukit.Engine.Tests/Stage7ParallelTests.cs`、`Infrastructure/SiteBuilder.cs`、主测试计划。

1. 在两个相同沙箱构建多个 Markdown 页面，注入委托给真实渲染器的探针；探针使用有界同步证明至少两个渲染调用实际重叠，串行对照最大并发为 1。
2. 比较完整公共路径与字节，并分别核对两次构建的报告、计数和哈希；不把仅设置 `jobs=2` 当作真实并行证据。
3. 运行 `TestInfrastructureTests`、`Stage7GoldenTests`、`Stage7DeterminismTests`、`Stage7ParallelTests` 完整专项，核对 TRX，记录闭包、缓存和专项复核。

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~TestInfrastructureTests|FullyQualifiedName~Stage7GoldenTests|FullyQualifiedName~Stage7DeterminismTests|FullyQualifiedName~Stage7ParallelTests' \
  --logger 'trx;LogFileName=parallel.trx' --results-directory "$R/parallel"
```

## Task 4：渲染中途失败安全

**Files:** `tests/Bukit.Engine.Tests/Stage7FailureTests.cs`、主测试计划。

1. 成功构建后对正式输出和整个托管缓存做原始快照；未注入故障时，期望抛错的测试必须 RED。
2. 使用真实模板渲染器的包装器，在第一份新内容写入事务暂存区后使下一次渲染抛出明确异常；验证确实发生中途写入、异常传播、正式输出和缓存原始字节不变、事务暂存/备份已清理。
3. 运行完整 `Stage7FailureTests` 专项并记录 TRX、闭包、缓存和专项复核；磁盘满及插件进程故障另行验证，不以此结果代替。

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~Stage7FailureTests' \
  --logger 'trx;LogFileName=failure.trx' --results-directory "$R/failure"
```

## Task 5：真实内置插件失败安全

**Files:** `tests/Bukit.Engine.Tests/Stage7PluginFailureTests.cs`、主测试计划。

1. 成功构建并对正式输出和托管缓存做原始快照；没有注入插件失败时，期望抛错的测试必须 RED。
2. 在沙箱中添加 `data/fault.toml`，触发真实 `data-files` 插件在 strict 模式拒绝不支持的格式；断言诊断、正式输出/缓存原始字节与事务清理。
3. 完整运行 `Stage7PluginFailureTests`，记录 TRX、闭包、缓存和专项复核。该结果只证明内置插件异常，不声称外部插件进程崩溃。

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~Stage7PluginFailureTests' \
  --logger 'trx;LogFileName=plugin-failure.trx' --results-directory "$R/plugin-failure"
```

## Task 6：真实磁盘满与 CI 接线

**Files:** `tests/Stage7DiskFull/Stage7DiskFullTests.cs`、`tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj`、`scripts/checks/stage7-diskfull-*`、`scripts/checks/test-infrastructure-platform-self-test.py`、`.github/workflows/ci.yaml`。

仅在 `BukitDiskFullTest=true` 时编入磁盘满测试。临时卷须核对独立挂载、容量和随机所有权标记；在渲染时填满该卷，断言原生磁盘满、Core 写入异常、输出／缓存字节不变和事务清理。普通测试不依赖特权卷。

CI 在 Linux 使用限额 tmpfs，在 macOS 使用 HFS+ 映像；Windows 创建独立 VHD，校验新设备与 VHD 的关联、非系统／启动盘和容量，验证新分区归属后才格式化，并在 `finally` 卸载。直接 owner 自检覆盖安全检查与 CI 选择；Windows 另先运行 VHD 安全负例。Windows 检出 fixture 前禁用换行转换。

**证据：**macOS 限额卷 2/2、受影响普通测试 56/56；[Linux / Windows #40817294951](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40817294951) 各 9/9 普通阶段 7 与 2/2 磁盘满，0 失败、0 跳过。1000 项输入与主工作区逐字节一致；Windows 自检通过并确认 VHD 已卸载，Linux tmpfs 已卸载。外部插件进程崩溃按用户确认留给阶段 9，阶段 7 使用 Task 5 的真实内置插件异常。

## Task 7：最终稳定性与收尾

审计修复前，普通测试程序集在磁盘满条件编译变更后重新构建，确认默认发现结果不包含该专项；然后以相同输入、程序集和 .NET 10.0.100 在 macOS arm64 顺序运行 golden、增量确定性和真实并发测试 **20/20 轮、120/120 条通过，0 失败、0 跳过**。各轮独立保存 TRX，并检查 995 项输入、程序集和环境状态；此结果不作为后续审计修复版本的最终证据。

证据目录为 `/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/stage7-start-AjtnVD/` 下的 `stability-final/`、`diskfull-persistent/` 和 `final-platform/`。执行规定的差异复核；不自动运行全量、覆盖率或发布门禁，也不自动提交、推送或合并主阶段分支。

## Task 8：审计后测试断言修复

**Files:** `tests/Bukit.Engine.Tests/Stage7DeterminismTests.cs`、`Infrastructure/ReportAssertions.cs`、`TestInfrastructureTests.cs`、主测试计划。

1. 将跨平台生成文本差异登记为独立 Core 风险；用户确认不阻塞本阶段，不修改 Core 或原始 fixture。
2. 先写报告助手负例：真实增量结果不能作为预期非增量构建通过；同时篡改报告与内存结果的非增量命中／未命中及变体计数，也必须被独立预期拒绝。确认 RED 后以调用方预期模式恢复非增量硬断言，并保留原有报告／结果一致性检查。
3. 编辑单文件后必须同时证明有缓存命中和重新渲染，并继续与独立全量构建逐字节比较；若现有 fixture 合法地全局失效，先诊断并选择能证明局部复用的输入，不修改 Core。
4. 完整运行 `TestInfrastructureTests` 与六个普通阶段 7 测试类，再运行真实限额卷的磁盘满直接消费者；记录 TRX、闭包和专项复核。最终测试程序集改变后重新运行 20 次稳定性，不复用旧循环。
5. 完成后再向用户申请正式 `gate=platform`；该入口同时执行 `ci-fast` 和 Architecture 测试，未获得明确授权前不运行。主阶段分支不提交、推送或合并。

**当前进展：**两个报告负例 RED→GREEN；保留默认 `pages-index` 场景，新增关闭该插件的真实局部复用场景，并确认编辑页渲染、未修改页未渲染。macOS 普通专项 55/55、限额卷直接消费者 2/2；最终程序集连续 20/20 轮、140/140 条通过。用户授权的正式 `gate=platform` 已执行，第二次仅重跑 Windows 后成功；旧版证据不冒充新版本。证据根为 `/private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/stage7-audit-repair-8kno84nc/`。

## Task 9：正式 CI 与待决事项

[正式 CI #40822628445](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40822628445) 的临时证据提交为 `8701fb177b4d2a87089b6991599ff630b173aad7`；执行前主工作区与该提交的 2433 个受跟踪／新增文件内容及模式一致。Fast gate、Architecture 415/415、Linux 和 macOS 平台作业第一次通过；Windows 失败作业单独重跑后，工作流第二次结果成功。三平台 SDK 10.0.403／runtime 10.0.14，各有阶段 7 普通测试 10/10 和真实磁盘满 2/2；Linux tmpfs、macOS 映像与 Windows VHD 均完成脚本检查的卸载。Windows 第二次工件按 ID `12875778056` 和摘要核对；第一次同名失败工件及日志保留。

**当时状态：PARTIAL（最终判定见 Task 10）。** Windows 第一次的既有 PluginHost 测试 `RunAsync_CancellationDuringBlockedStdinWriteKillsProcess` 在取消异常后发现探针完成标记，重跑通过并不能证明根因消失。该测试及相关 Core 代码自阶段 6 以来未变，具体取消、阻塞写入和进程终止时序未被现有日志证明；用户已选择另立 Core 调查，修复范围另行确认，不在本阶段擅改。不能将成功重跑写成首次通过或断言该缺陷已修复。只读调查已核对 .NET 10.0.14 的 Windows 同步管道和取消实现，调查记录位于上述证据根的 `windows-cancel-investigation/`。后续定向诊断见 Task 10。

## Task 10：Windows 取消诊断与 PluginHost 测试修正

**Files:** `tests/Bukit.PluginHost.Tests/SystemProcessRunnerTests.cs`、`tests/PluginProcessProbe/Program.cs`、主测试计划。

1. 两次授权的 Windows 定向诊断 [#40826824451](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40826824451) 和 [#40828158420](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40828158420) 各 53/53，未复现完成标记。10 个就绪后主动取消样本约 3.8–5 ms 返回；计时器样本有 767／892 ms（信号观察上界）和 1052 ms（回调时间）的延迟。原测试的计时敏感性成立，但首次失败的因果顺序仍未知。
2. 经用户确认只改测试与探针：探针就绪时写出自身 PID；取消测试在就绪后主动取消，超时测试由 Runner 自身 3 s 超时触发；两者都直接确认该进程在有界时间内退出且无完成标记，失败时只清理本测试进程。
3. 反向检查：临时让 Runner 不终止进程，两个测试均失败；Git 还原后 `src/` 无差异。macOS 专项 51/51，闭包无未映射项，GREEN 缓存 HIT。
4. [三平台 CI #40832277705](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40832277705) 第一次即成功：三平台 PluginHost 各 51/51、阶段 7 普通测试各 10/10、真实磁盘满各 2/2。临时证据提交 `92b9237175cbbc8b7a4c752a4f10ebec98e5f095` 与工作区 2415 个文件一致。

```bash
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~Bukit.PluginHost.Tests.SystemProcessRunnerTests|FullyQualifiedName~Bukit.PluginHost.Tests.PluginPathValidatorTests|FullyQualifiedName~Bukit.PluginHost.Tests.PluginProtocolProbeTests' \
  --logger 'trx;LogFileName=pluginhost.trx' --results-directory "$R/green"
```

证据根：`/Users/ali/.local/state/bukit-verification/stage7-pluginhost-kill-20261029/`。主阶段分支未提交、推送或合并。

**最终判定：阶段 7 SUCCESS。** 用户明确接受首次 Windows 失败根因未定的遗留风险不阻塞本阶段；接受风险不等于风险已解决，首次失败记录保留。若该测试再次失败，应作为 Core 取消路径问题单独调查。详见主测试计划“阶段 7 最终判定”。
