# Tune the Native AOT publish profile and GC settings

状态：**草稿**（2026-11-24）。未审计，未获实施批准。本文件只是提案，不授权任何 Core 改动、commit、push 或发布。

测量结论（2026-11-25）：阶段 0 与阶段 1 的候选已在 macOS 与 Linux 上实测。阶段 2（GC 设置）无一达标，关闭；阶段 1 只有 `BukitStripSymbols` 默认值改为 `true` 达标，实施需用户另行确认。数据见 `docs/plans/2026-11-24-core-performance-optimization-strategy.zh-CN.md` 第 5.1 节。

## Why

`bukit` 以 Native AOT 单文件发布，但发布配置只做了最基本的设置，运行时内存行为完全使用默认值：

- `src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj` 的 AOT 相关属性只有 `PublishAot`、`PublishSingleFile`、`InvariantGlobalization`、`StripSymbols`（由 `BukitStripSymbols` 控制，默认 `false`）和 `TrimmerSingleWarn`。
- 全仓库没有任何 GC 配置：没有 `runtimeconfig.template.json`，没有 `ConcurrentGarbageCollection`、`ServerGarbageCollector`、`System.GC.*` 设置，也没有 `GCSettings` 调用。
- 实测基线（`docs/plans/2026-11-10-build-time-incremental-remediation-plan.zh-CN.md`）：4651 页站点全量构建峰值内存 651 MB。
- 本机产物：`artifacts/publish/osx-arm64/bukit` 为 29.9 MB，旁边的 `libSkiaSharp.dylib` 为 15.9 MB。

需要先说明两点已有事实，避免高估收益：

1. **发布包已经剥离符号**。`scripts/build/package-native-aot.sh` 传入 `-p:BukitStripSymbols=true` 和 `-p:NativeDebugSymbols=false`，并删除 `.dSYM`、`.pdb`、`.dbg`。csproj 里的默认值 `false` 只影响不经过该脚本的本地发布。所以"开启符号剥离"对正式发布包没有收益。
2. **部分特性开关在 AOT 下可能已是默认关闭**（例如 `EventSourceSupport`）。这些项只有实测有差别才保留。

因此本变更的定位是：**用实测决定一组发布属性和 GC 设置，只保留有证据的项**，而不是预设一份参数表。

## What Changes

### 阶段 0：基线与实验（不改仓库文件）

对同一台机器、同一份站点副本、同一个二进制，记录：

| 指标 | 采集方式 |
|---|---|
| 二进制体积 | `ls -l`，分别记录 `bukit` 与原生库 |
| 冷启动 | `bukit version` 连续运行 20 次取中位数 |
| 全量构建耗时 | `bukit build --clean`，取构建报告中的阶段计时与总耗时 |
| 峰值内存 | 脚本读取构建子进程的 `wait4` 峰值 RSS，并按操作系统换算为 MiB |
| 输出一致性 | 按相对路径比较发布文件字节，并比较仅归一化已列明易变字段的内部报告 |

GC 实验先用环境变量在**现有二进制**上做，不需要重新编译：

| 实验 | 环境变量 | 预期方向 |
|---|---|---|
| E0 | 无（基线） | - |
| E1 | `DOTNET_gcConcurrent=0` | 减少后台 GC 线程及其内存 |
| E2 | `DOTNET_GCConserveMemory=5` | 更积极压缩，降低峰值，增加 GC 时间 |
| E3 | E1 + E2 | 组合 |
| E4a / E4b | E3 + `DOTNET_GCgen0size=1000000` / `4000000`（无前缀十六进制） | 比较 16 / 64 MiB 最小 gen0 预算 |

阶段 0 先用 `scripts/benchmarks/run-gc-experiments.py` 跑两次 E0，确认同配置的输出等价性和波动范围。每组在统一的清洁环境下运行。候选组采样前，使用 Native AOT 探针读取 GC 有效配置（例如 `GC.GetConfigurationVariables()`），并记录 SDK、运行时及 RID。无法确认有效值的组不进入采纳判断。

### 阶段 1：发布属性（改 `Bukit.Cli.csproj`）

逐项加入、逐项测量。每一项只在满足采纳条件时保留：

| 候选 | 说明 | 已知代价 |
|---|---|---|
| `IlcFoldIdenticalMethodBodies=true` | 合并机器码相同的方法体 | 堆栈中的方法名可能指向被合并的另一个方法 |
| `OptimizationPreference`（`Size` 与 `Speed` 各测一次） | 默认是折中 | `Size` 可能拖慢渲染，`Speed` 可能增大体积 |
| `EventSourceSupport=false`、`DebuggerSupport=false` | 特性开关 | 可能已是默认值，无差别则不写入 |
| `BukitStripSymbols` 默认值改为 `true` | 只影响本地发布 | Linux 上需要 `objcopy`（已有 `ValidateStripSymbolsTooling` 校验） |

### 阶段 2：GC 设置（改 `Bukit.Cli.csproj`）

把阶段 0 中胜出且有受支持项目配置键的设置固化到项目文件。并发 GC 使用 `ConcurrentGarbageCollection`，保守内存设置使用 `RuntimeHostConfigurationOption` 的 `System.GC.ConserveMemory`，再验证相应的 `DOTNET_*` 环境变量可以覆盖。`GCgen0size` 没有公开的 `System.GC.*` 键，E4 仅作为环境变量实验。若收益依赖 E4，须重新评估可部署方案。

### 采纳条件

一项设置只有同时满足以下条件才进入仓库：

- 目标指标改善不少于 10%（体积项看体积，GC 项看峰值内存）；
- 全量构建总耗时变差不超过 5%；
- 发布产物按相对路径逐文件字节一致，归一化后的内部报告保持一致；
- 发布包的可复现性自检仍通过。

阈值是提案值，由用户确认或修改。不满足条件的候选记录测量结果后放弃。

## Impact

- 受影响代码：仅 `src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj`。不改任何 `.cs` 文件。
- 受影响脚本：`scripts/build/package-native-aot.sh` 预计不需要改动；若阶段 1 的属性需要由脚本传入，则同时更新 `scripts/build/native-aot-self-test.sh` 的断言。
- 公共契约：无。CLI 命令、输出、报告模式、`site.yaml` 都不变。
- 构建输出：发布产物逐文件字节一致。报告仅归一化已列明的时间、耗时及依赖这些字段的清单摘要，问题、计数和其余契约字段保持一致。
- 可观测性：`IlcFoldIdenticalMethodBodies` 会让崩溃堆栈的方法名不够精确。Bukit 的诊断以 `DiagnosticCode` 为主，影响有限，但需要用户明确接受。
- 跨仓影响：无。BukitJalil 和站点仓库消费的是 CLI 行为，不受影响。
- 用户批准范围：用户 2026-11-24 要求起草本提案。实施、提交与发布需单独确认。

## Non-Goals

- 不设置 `UseSystemResourceKeys`。它会把 BCL 异常消息替换成资源键，而 Bukit 在 doctor、插件加载等多处把 `ex.Message` 直接呈现给用户，会损害诊断信息的真实性。
- 不设置 `StackTraceSupport=false`，不关闭堆栈信息。
- 不设置 `InvariantTimezone`，日期处理语义不变。
- 不针对特定 CPU 指令集编译（`IlcInstructionSet`），保持二进制可移植。
- 不替换或移除 SkiaSharp、AngleSharp 等依赖（体积上更大的一块，属于独立变更）。
- 不改变渲染并行度、输出管线或任何算法。
