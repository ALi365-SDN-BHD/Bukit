# Bukit Core G-04D5A CLI parse/result graph resolution

日期：2026-07-23

状态：`implementation complete / group-verification-pending`

## 1. 决议摘要

G-04D5A 只处理 `Bukit.Cli.Shared` 中 factory 与 parse-result graph 的四个
历史 `2.0-candidate` identity：

| Identity | Task 18 终态 | 当前 owner |
|---|---|---|
| `Bukit.Cli.Shared.Cli.Binding.CliBoundCommandFactory` | internalized | `CliParser` 的同程序集 binding implementation |
| `Bukit.Cli.Shared.Cli.Parsing.CliParseResult` | retained public / reclassified | public parser/dispatch companion contract |
| `Bukit.Cli.Shared.Cli.Parsing.SimpleParseResult` | internalized | `CliParser` 与 `CommandDescriptor` 的同程序集实现 |
| `Bukit.Cli.Shared.Cli.Parsing.SubcommandParseResult` | internalized | `CliParser` 与 `CommandDescriptor` 的同程序集实现 |

本决议采取最小兼容边界：

- factory、simple result 和 subcommand result 不再作为 exported CLR types；
- abstract base `CliParseResult` 继续 public，public/protected record inheritance shape
  保持；
- `CliParser.Parse(...)` 继续返回 `CliParseResult`；
- `CommandDescriptor.DispatchAsync(...)` 继续接收 `CliParseResult`；
- 不修改 CLI command tree、参数、诊断、help、退出码或 dispatch 规则；
- 不新增 production `InternalsVisibleTo`。

该决议是 2.0-only CLR surface narrowing。它不改变 1.x visibility，也不把
`Bukit.Cli.Shared` 宣布为通用 CLR SDK。

## 2. 为什么三个 identity 可以 internalize

### 2.1 `CliBoundCommandFactory`

生产代码中 factory 的 canonical consumer 是同程序集 `CliParser`。
Core CLI、Labs CLI 和官方进程插件均不需要直接构造该 helper：

```text
CLI args
  -> CliParser.Parse
  -> CliBoundCommandFactory.Create
  -> CliBoundCommand
  -> CliParseResult
```

public behavior owner 仍是：

- `CliParser.Parse(CliCommandSpec, IReadOnlyList<string>)`；
- `CliBoundCommand` 的读取方法；
- CLI command metadata、diagnostic 和 entry-point 行为。

因此收窄 factory 不需要增加替代 public helper，也不需要把 Labs 的
permissive binder 合并到 Core parser。

Labs 的 binder 会接受未知选项并按邻接 token 推断值；Core factory 只绑定 spec
认识的 option。两者语义不同，本任务禁止借 surface governance 建立新的全局 binding
abstraction。

### 2.2 `SimpleParseResult` 与 `SubcommandParseResult`

两个 sealed records 只负责表达 parser 与 dispatcher 之间的内部分支：

- `CliParser` 构造；
- `CommandDescriptor` 在同程序集模式匹配；
- public consumer 通过 base properties 读取 command、bound command、diagnostics
  和 `IsSuccess`；
- public consumer通过 `CommandDescriptor.DispatchAsync` 完成 dispatch。

测试原先直接 `new SimpleParseResult(...)` 或
`new SubcommandParseResult(...)`，属于测试绑定内部实现，而不是需要保留的产品契约。
Task 18 将相关测试迁移到真实 `CliParser.Parse(...)` 路径，避免为了测试方便引入
friend assembly。

## 3. 为什么 `CliParseResult` 必须保留

`CliParseResult` 不能与两个 concrete records 一起机械 internalize。它仍被两个
非候选 public members 精确传播：

```csharp
public static CliParseResult CliParser.Parse(
    CliCommandSpec command,
    IReadOnlyList<string> args)

public Task<int> CommandDescriptor.DispatchAsync(
    CliParseResult result)
```

base 还是 public abstract record，并继续提供：

- `Command`；
- `BoundCommand`；
- `Diagnostics`；
- `IsSuccess`；
- record equality/deconstruction；
- protected primary/copy constructor 与 record inheritance metadata。

仓库测试还包含外部测试程序集派生的 custom parse result，用来证明未知 result 的
dispatch exit behavior。删除 base、改为 sealed DTO 或改变 parser/dispatcher signature
都会扩大为新的 public facade migration；均不属于 G-04D5A。

因此 current baseline 中该类型从：

```text
implementation-public / 2.0-candidate / 2.0-review
```

重分类为：

```text
cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review
```

这表示它是 retained public parser/dispatcher companion，不表示 Bukit 新增了受支持的
通用 CLR extension SDK。

## 4. 行为保持边界

### 4.1 Argument binding

Task 18 不改变：

- long option 与 short alias 映射；
- option name 的 ordinal-ignore-case 行为；
- flag 被绑定为 `"true"`；
- `--option=value` inline value；
- option 后的独立 value；
- option 后紧邻另一 option 时不误吞下一 token；
- positional argument 顺序；
- 未知 option 的 validation 与 binding 分工；
- required argument、required option 与 conflict 判断。

### 4.2 Diagnostic

不改变：

- diagnostic code 与 message；
- validation traversal 顺序；
- nested subcommand diagnostic 传播；
- `IsSuccess` 只由 `Diagnostics.Count == 0` 决定；
- CLI entry point 对首条文本诊断、usage 和 JSON error 的处理。

Task 18 的测试迁移需要冻结 exact diagnostic order，不能因不再直接构造 concrete
records 而把测试降级为只验证“存在某条诊断”。

### 4.3 Dispatch

不改变：

- simple result 调用当前 descriptor handler；
- subcommand result 优先调用匹配 child handler；
- child 无 handler 时回退 parent handler；
- parent/inner options 与 arguments 的现有 merge 规则；
- 未知 command/result 返回 exit code `2`；
- stderr 中现有 unknown-command 文本。

## 5. 明确不修：相邻的深层 subcommand dispatch

当前 parser 能递归生成 nested `SubcommandParseResult`，但 dispatcher 的现有逻辑只在
当前层消费 `InnerResult.BoundCommand`，没有递归调度任意深度的 descriptor graph。
现有测试主要覆盖一层 subcommand dispatch；这与 type accessibility 无关。

G-04D5A 不得顺带：

- 重写 recursive dispatch；
- 改变多层 argument/option merge；
- 改 command path；
- 调整 plugin subcommand ownership；
- 修改 unknown-command 或 exit-code 行为。

若要修复或扩展深层 dispatch，必须建立独立行为任务，先定义兼容语义和回归矩阵。
Task 18 只冻结当前行为，不能把 line count 或更“优雅”的 recursion 当作验收条件。

## 6. Core、Labs 与 Plugin 边界

### Core

Core CLI 继续沿用：

```text
Program
  -> CliParser.Parse
  -> CliParseResult public view
  -> CommandDescriptor.DispatchAsync
  -> command handler
```

`CliParser` 与 `CommandDescriptor` 都位于 `Bukit.Cli.Shared`，所以其内部 concrete
result 协作不需要 IVT。

### Labs

`Bukit.Labs.Cli` 引用 `Bukit.Cli.Shared`，但直接消费的是 retained
`CliBoundCommand`。对本任务四项 identity 没有直接调用。Task 18 不修改 Labs 的
command set、binder 或 help；Task 20 必须通过 Labs owner tests 证明 shared assembly
收窄没有造成编译或运行回归。

### Plugins

官方插件和 `bukit-plugin-v1` process protocol 不传播四项 CLR identity。动态 plugin
command 由 Core 映射为 `CliCommandSpec`/`CommandDescriptor`，再走 retained parser
facade。Task 18 不修改 plugin manifest、wire DTO、process protocol、command JSON
或 plugin output ownership。

## 7. 测试迁移边界

Task 18 的测试改动只允许把 direct implementation construction 迁移到 canonical
public behavior：

| 测试面 | 必须保持的断言 |
|---|---|
| parser/binder | long/short option、case、flag、inline/separate value、positionals |
| validation | missing/invalid/unknown/required/conflict 与 exact diagnostic order |
| result graph | simple、subcommand、nested parse、inner diagnostic 传播 |
| descriptor | child preference、parent fallback、merge、unknown result exit `2` |
| CLI contract | command tree、command path、help/usage、exit code不变 |
| plugin CLI | dynamic descriptor 仍可 parse、dispatch并生成原 command invocation |
| architecture | 三项 internal/not exported；base public/reclassified；无新 production IVT |

测试不得通过以下方式取得 internal access：

- 给 `Bukit.Cli.Shared` 增加 `InternalsVisibleTo("Bukit.Cli.Tests")`；
- source-link 编译 production file；
- reflection 构造 concrete parse records 来替代真实 parser 路径；
- 添加 test-only public facade 到 production assembly。

架构测试可以只读反射 exact type identity，以确认 internal/not exported；行为测试必须走
public parser/dispatcher。

## 8. Public API baseline

Task 18 之前的 current baseline：

```text
14 assemblies / 492 public types / 67 candidates
```

本任务产生的精确 delta：

| 操作 | Public types | Candidates |
|---|---:|---:|
| internalize factory | -1 | -1 |
| internalize simple result | -1 | -1 |
| internalize subcommand result | -1 | -1 |
| retain/reclassify base result | 0 | -1 |
| 合计 | -3 | -4 |

Task 18 current baseline：

```text
14 assemblies / 489 public types / 63 candidates
```

current baseline 必须满足：

- 三个 internalized identities不再出现；
- `CliParseResult` exact public/protected member shape仍出现；
- base 分类精确为
  `cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review`；
- `CliParser.Parse` 和 `CommandDescriptor.DispatchAsync` 的 signatures不变；
- 除上述三删一重分类外不存在无关 drift。

closed 136-entry consumer-declaration manifest 是声明窗口关闭时的历史 cohort，必须保持
原内容、136 项和 Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`。current baseline 描述当前 2.0
终态，两者不得混写。

## 9. External consumer 与兼容性

2026-07-22 authenticated GitHub search 对四项均未发现 reviewed Bukit public match。
simple-name 命中中的同名类型已通过 Bukit-scoped resolution query 排除。

closed historical manifest 仍保留：

```text
declarationStatus = consumer-declaration-pending
privateConsumerStatus = unknown-until-voluntary-declaration
externalEvidence.searchStatus = no-public-match-found
```

`no-public-match-found` 不能证明 private、unindexed 或 undisclosed consumer 不存在。

兼容性影响：

- 任何未声明、直接引用 factory、simple result 或 subcommand result 的 CLR source
  consumer，在 2.0 重新编译时会 breaking；
- 任何对三个 identities 做 metadata/reflection lookup 的 consumer 会 breaking；
- 已编译且直接引用它们的 binary consumer 不保证可在 2.0 继续加载；
- 只使用 documented CLI、JSON error、configuration、theme 或
  `bukit-plugin-v1` 的消费者不受该 CLR accessibility 变更影响；
- 依赖 `CliParseResult`、`CliParser.Parse` 或
  `CommandDescriptor.DispatchAsync` 的现有调用形状继续保留。

本任务不新增 obsolete shim，因为 canonical parser/dispatcher 已存在，且 public
search 没有提供需要迁移期的直接消费者证据。若后续出现真实消费者，必须单独评估
retain、facade 或 migration window，不能重写历史 manifest。

## 10. Native AOT

四项 parse identities：

- 不由 JSON serializer/source generator 构造；
- 不依赖 dynamic assembly loading；
- 通过 Core CLI static parser/descriptor path 可达；
- accessibility narrowing 不改变 static reachability。

但静态分析不能替代真实 publish。Task 20 必须统一证明：

1. Core CLI Native AOT publish成功；
2. published binary能执行普通 command；
3. invalid option仍产生相同 diagnostic、usage和exit code；
4. subcommand/plugin descriptor parse-dispatch path仍可达；
5. release-artifact smoke通过；
6. 没有新 trimming、AOT、reflection或source-generation warning。

Labs CLI 本身不是本任务的 Native AOT product target，但必须完成真实项目编译和 owner
tests，证明其 retained `CliBoundCommand` dependency未受影响。

## 11. Task 20 待验证

按 master plan 的组级规则，Task 18 不单独运行测试、aggregate gate、Native AOT或
只读复审。当前状态保持 `group-verification-pending`。

Task 20 至少必须验证：

- `Bukit.Cli.Tests`；
- `Bukit.Labs.Cli.Tests`；
- `Bukit.Architecture.Tests`；
- master plan 所列 G2 的 Content、Notion、Shared、Engine owner projects；
- current public API snapshot 与 baseline exact match；
- historical manifest blob/136-entry不变；
- G2 aggregate `post-change-targeted.sh --base <GROUP_BASE>` 只运行一次；
- 真实 Native AOT publish 与 release-artifact smoke；
- CLI command tree、parse/bind、diagnostic order、dispatch和exit behavior；
- 一次 G2 轻量独立只读复审。

在 Task 20 证据完成前，不得把 G-04D5A 或 Group 2 标为 fully closed。

## 12. 停止条件

出现下列任一情况必须停止关闭，不得扩大 Task 18：

1. 发现真实 public/private consumer 直接引用三个 internalized identities；
2. `CliParseResult` 无法在不改变 public parser/dispatcher signatures 的情况下保留；
3. record base 的 public/protected inheritance shape发生 drift；
4. 需要新增 production IVT、test-only production facade或 source-link compilation；
5. argument binding、diagnostic code/message/order、command tree、dispatch或exit code改变；
6. Labs、Core CLI 或 dynamic plugin command 编译/行为回归；
7. public API baseline不是精确 `14/489/63`；
8. closed manifest内容、136-entry计数或 blob发生变化；
9. Task 20 owner tests、aggregate gate、Native AOT或独立复审存在未关闭 failure；
10. 修复要求进入深层 subcommand dispatch、plugin protocol、error JSON、schema或其他
    G-04D5B/相邻模块范围。

停止后应选择 retain/reclassify、独立 facade migration 或新的行为修复任务；不得用
一次性扩大修改范围来追求候选数下降。

## 13. 正式关闭台账

| Identity graph | Task 18状态 | Current baseline终态 | Group 2状态 |
|---|---|---|---|
| factory | internalized | removed from public baseline | Task 20 verification pending |
| simple result | internalized | removed from public baseline | Task 20 verification pending |
| subcommand result | internalized | removed from public baseline | Task 20 verification pending |
| abstract base result | retained/reclassified | public companion；退出 candidate | Task 20 verification pending |

Task 18 的实现决策已经完成；完整 owner tests、public API drift、aggregate targeted gate、
Native AOT与独立只读复审统一留待 Task 20。
