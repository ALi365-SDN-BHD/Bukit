# Bukit Core G-04D5 CLI Shared 资格审计

> 日期：2026-07-23
>
> 范围：G2 Task 17，只做资格判定，不实施 Task 18/19
>
> 状态：eligibility complete / group-verification-pending

## 1. 执行摘要

Task 17 审计的五项 `Bukit.Cli.Shared` 候选不能批量执行同一种操作：

| 类型 | 资格结论 | 原因 |
|---|---|---|
| `CliBoundCommandFactory` | Task 18 eligible internalize | 生产调用只在同程序集 `CliParser`，没有 public signature 传播 |
| `CliParseResult` | retain-by-design / reclassify | 是 public parser 返回类型和 dispatcher 参数，并允许跨程序集 record 派生 |
| `SimpleParseResult` | 与 `SubcommandParseResult` 成对 eligible internalize | 只由同程序集创建和模式匹配，没有 public signature 直接暴露 concrete identity |
| `SubcommandParseResult` | 与 `SimpleParseResult` 成对 eligible internalize | 与 inner result 构成实现图，但 public base 足以承载跨程序集 parse/dispatch 合同 |
| `CliErrorRenderer.CliErrorPayload` | Task 19 eligible internalize | 仅为 source-generated JSON 内部 DTO，public renderer 只返回 `string` |

因此建议：

- Task 18：internalize factory 与两个 sealed concrete result；保留并重分类
  `CliParseResult`；
- Task 19：只把 nested `CliErrorPayload` 从 public 改为 internal；
- 不新增 `InternalsVisibleTo`，测试改由 public parser/dispatcher 合同驱动；
- 不修改命令树、绑定、诊断、JSON、help、stdout/stderr 或 exit-code 行为。

当前基线是：

```text
14 assemblies / 492 public types / 67 candidates
```

若后续严格按资格结论实施，投影为：

```text
Task 18: 14 / 489 / 63
Task 19: 14 / 488 / 62
```

其中 Task 18 删除三个 public identity，并把一个 retained base 退出 candidate；
Task 19 再删除一个 public identity。以上只是实施目标，不代表测试、public API drift 或
Native AOT 已通过。

## 2. parse/result graph

### 2.1 `CliBoundCommandFactory`

定义位于：

```text
src/Bukit-Core/Bukit.Cli.Shared/Cli/Binding/CliBoundCommandFactory.cs
```

唯一 production caller 是同程序集的：

```text
CliParser.Parse
  -> CliBoundCommandFactory.Create
  -> CliBoundCommand
```

没有 public/protected API 把 factory 类型作为参数、返回值、base、constraint 或
attribute 暴露。Labs 使用 retained public `CliBoundCommand`，但在自己的 `Program.cs`
实现 permissive binder，不调用本 factory。

因此 factory 可以只改 class accessibility。必须冻结：

- option key 使用 `OrdinalIgnoreCase`；
- 非 `-` token 进入 positional arguments；
- 第一个 `=` 分割 inline value，后续 `=` 保留在 value；
- unknown option 被 binder 忽略，由 parser validation 产生 diagnostic；
- flag 写入字符串 `"true"`；
- 非 flag 只在下一 token 不以 `-` 开头时消费 separate value；
- root spec 和 immediate subcommand options 都进入 option map；
- 同名/short-name 后写覆盖语义不变。

现有测试直接调用 factory，不能因此新增 test friend assembly。Task 18 应把这些测试迁为
通过 public `CliParser.Parse(...).BoundCommand` 观察相同行为。

### 2.2 `CliParseResult` 必须保留

`CliParseResult` 是 public abstract record，并进入两个硬公共签名：

```csharp
public static CliParseResult CliParser.Parse(...)
public Task<int> CommandDescriptor.DispatchAsync(CliParseResult result)
```

`Bukit.Cli` 在另一个 assembly 中执行：

```text
CliParser.Parse
  -> IsSuccess / Diagnostics
  -> CommandDescriptor.DispatchAsync
```

此外 `Bukit.Cli.Tests` 作为外部 assembly 定义
`TestParseResult : CliParseResult`，并验证 dispatcher 对未知派生类型返回 exit code 2。
这证明外部 record 派生不是理论上的 metadata 噪声。

Task 18 必须保持：

- exact public abstract record identity；
- primary/protected copy constructor；
- `Command`、`BoundCommand`、`Diagnostics`；
- `IsSuccess == Diagnostics.Count == 0`；
- generated equality operators、`EqualityContract`、`PrintMembers`、clone、
  `ToString` 和外部派生能力；
- public parser 返回和 dispatcher 参数签名。

正确治理分类应为：

```text
cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review
```

若未来要 internalize base，必须先重设计 `CliParser`、`CommandDescriptor` 和外部扩展合同，
不能在 Task 18 顺带完成。

### 2.3 两个 concrete result 必须成对处理

`SimpleParseResult` 与 `SubcommandParseResult` 由同程序集 `CliParser` 创建，并由
`CommandDescriptor` 同程序集模式匹配。没有 public member 直接接收或返回这两个
concrete type。

`SubcommandParseResult` 额外携带：

- `SubcommandName`
- `InnerResult`

两者构成内部递归执行形状。只收窄其中一项会形成不必要的半图，因此 Task 18 应成对
internalize。

这仍是 2.0 source/binary/reflection breaking change：未公开或私人 consumer 若直接
构造、deconstruct、pattern-match concrete records，会受影响。不能把“无 public
signature 传播”写成“无外部消费者”。

record equality 也不得误写成深结构相等：`CliBoundCommand` 是普通 class，
`Diagnostics` 是 list-like reference，concrete record equality 包含这些引用和运行时
concrete identity。

## 3. parse、diagnostic 与 dispatch 行为

Task 18 只治理 CLR visibility，不修改：

- 仅在首 token 非 option 时识别 subcommand；
- subcommand name/alias 使用 `OrdinalIgnoreCase`；
- 对剩余参数递归 parse；
- diagnostic 顺序保持：
  1. token scan；
  2. required arguments；
  3. required/conflicting options；
- integer 和 invariant number validation；
- number 拒绝 `NaN` 与 infinity；
- allowed values 使用 case-insensitive comparison；
- simple handler dispatch；
- subcommand child handler 优先；
- child 无 handler 时 parent handler fallback；
- merge 时 inner option 覆盖 parent option；
- merged arguments 是 `[subcommandName] + inner arguments`；
- unknown command/unknown derived result 的 exit code 2；
- `Unknown command: ...` stderr 文本。

不得借 Task 18 修改 command registry、aliases、help、plugin CLI composition 或 Labs
permissive binder。

## 4. error payload 资格

### 4.1 public signature 与 runtime root

`CliErrorRenderer.CliErrorPayload` 只在 `CliErrorRenderer` 内构造：

```text
RenderJson(...)
  -> new CliErrorPayload(...)
  -> JsonSerializer.Serialize(
       payload,
       CliErrorJsonContext.Default.CliErrorPayload)
  -> string
```

所有 public overload 均返回 `string`。没有 public 参数或返回值暴露 payload。
必须继续 public 的是：

- `CliErrorRenderer`
- `CliErrorRenderer.CliErrorDiagnostic`
- 全部现有 `RenderJson` overload

`CliErrorDiagnostic` 是 public overload 的参数类型，不属于本轮收窄目标。

### 4.2 source generation 与 Native AOT

internal `CliErrorJsonContext` 通过：

```csharp
[JsonSerializable(typeof(CliErrorRenderer.CliErrorPayload))]
[JsonSerializable(typeof(CliErrorRenderer.CliErrorDiagnostic))]
```

静态注册两个类型。序列化调用显式使用 generated `JsonTypeInfo`，没有 reflection
fallback。把 payload 改为 internal 后，source generator 仍应由同程序集访问 nested
type。

这是必须实测的 AOT 风险点：若编译、source generator、trimmer 或 Native AOT 失败，
Task 19 必须停止，不能通过恢复 public、切换反射序列化或移动 DTO 规避。

## 5. 必须冻结的 JSON 合同

当前 property 顺序是：

```text
schema
version
command    (non-null only)
exitCode
errors
usage      (non-null only)
```

每个 error 顺序是：

```text
code
message
showUsage
```

Task 19 必须保持：

- camelCase；
- indented JSON；
- `command == null` 和 `usage == null` 时完全省略；
- exit code 0 仍输出；
- `showUsage == false` 仍输出；
- empty errors 为 `[]`；
- multiple errors 保持输入顺序；
- default schema 为 `https://bukit.dev/schemas/cli-error.v1.json`；
- default version 为 `1.0`；
- default exit code 为 2；
- custom schema/version overload 不变；
- System.Text.Json 默认 escaping；
- serializer string 本身无尾随换行；
- `BukitException` diagnostic code format；
- ordinary exception 使用 `cli-error`；
- exception overload 的 `showUsage=false`。

CLI 层必须继续：

- help 写 stdout、exit 0；
- parse/unknown-command error 写 stderr、exit 2；
- JSON mode 输出所有 diagnostics；
- human mode只显示第一条 diagnostic；
- 任一 diagnostic 要求 usage 时输出 usage；
- argument/config/content error 为 2；
- render error 为 3；
- unexpected error 为 1。

## 6. Labs、Plugin 与仓内消费者

### Labs

`Bukit.Labs.Cli` 直接引用 `Bukit.Cli.Shared`，但本轮五项候选的 direct consumer 为 0。
Labs 使用：

- public `CliBoundCommand`；
- Labs 自有 permissive binder；
- Labs 自有 help、unknown command 和 exception输出。

因此 Task 18/19 不需要修改 Labs source。Task 20 仍要运行 Labs tests，证明共享 assembly
metadata变化没有造成引用或加载回归。

### Plugin

Core plugin CLI composition使用 command descriptors和 retained CLI contract types，没有
直接引用 factory、两个 concrete result或 payload。进程插件协议也不暴露这五个 CLR
identity。

### Tests

当前直接访问包括：

- `CliTestHelper` 调用 factory；
- `CliParserExtendedTests` 调用 factory；
- `CommandDescriptorTests` 直接构造两个 concrete result；
- `CommandDescriptorTests` 外部派生 retained base；
- `CliErrorRendererTests` 通过解析 JSON string 验证 payload；
- `ProgramEntryPointTests` 验证部分 JSON CLI输出。

Task 18 必须迁移前三类 direct internalization consumer，不得新增 IVT。
总计划列出的 `tests/Bukit.Cli.Tests/CliContractTests.cs` 当前不存在；Task 18 应创建该
contract fixture，而不是把它误记为既有测试。

## 7. 外部证据与 private unknown

closed 136-entry manifest 对五项都记录：

- `consumer-declaration-pending`；
- `privateConsumerStatus = unknown-until-voluntary-declaration`；
- authenticated exact full-name search无 public match；
- simple-name结果中的通用名碰撞已经排除；
- historical cohort 与 Git blob
  `7b07d6890562387010b52301e9f8716e9bf10ed1` 不可改写。

因此：

- retained base 是由当前 public contract propagation 决定；
- 四项 eligible narrowing 是在明确 2.0 window 中执行；
- 未观察到公开消费者不等于 private、binary、reflection consumer不存在；
- 新 consumer declaration会触发停止和重新评审。

## 8. 测试缺口与后续矩阵

### Task 18

至少补齐：

- factory不 exported，但 parser仍覆盖其全部 binding行为；
- base public/exported、可外部派生并保持 protected/public shape；
-两个 concrete exact identities存在但 internal/not exported；
- simple与nested subcommand从 public parser产生并由 dispatcher消费；
- diagnostic exact order；
- subcommand aliases、递归 inner result与merge顺序；
- handler selection、fallback、stderr和exit code；
- command tree exact contract不变；
- current baseline精确 `14/489/63`；
- historical manifest/blob不变；
- 不新增 CLI Shared IVT。

### Task 19

现有 JSON semantic tests尚未完全冻结：

- exact property order与indent；
- null command/usage omission；
- empty/multiple errors及顺序；
- exit code 0与boolean false输出；
- quote、backslash、newline、control character、Unicode/HTML-sensitive escaping；
-无尾随换行；
- JSON mode stdout空、stderr envelope与exit code；
- generated context和internal nested metadata；
- current baseline精确 `14/488/62`；
- historical manifest/blob不变。

### Task 20

统一运行 master plan指定的 Content、Content.Notion、Notion、Shared、CLI、Labs CLI、
Engine、Architecture tests、public API drift、唯一 G2 aggregate targeted、Native AOT、
published artifact smoke与一次轻量只读复审。

## 9. 相邻问题，不得顺带修复

审计发现两个现存但不属于 visibility治理的问题：

1. CLI error schema把 `command`列为 required，而 serializer在 null时省略该字段；
2. unexpected exception含 inner exception时，JSON envelope后可能追加一行纯文本，
   使 stderr不再是单一 JSON document。
3. parser支持递归 subcommand result，但 dispatcher当前只消费第一层
   `InnerResult.BoundCommand`；本轮没有证据授权改变深层 dispatch语义。

它们应进入独立 CLI contract/diagnostic任务。Task 19 必须冻结当前行为，不能通过修改
schema、null策略或 exception输出顺带修复；Task 18也不能借机重写递归 dispatch。

## 10. 精确实施边界

### Task 18 允许

- factory `public` → `internal`；
- `SimpleParseResult`、`SubcommandParseResult` `public` → `internal`；
- `CliParseResult`保留 public并重分类；
- 测试改经 public parser/dispatcher；
- current baseline、architecture guard、现行治理文档和决议台账同步。

### Task 18 禁止

- internalize/delete `CliParseResult`；
- 修改 `CliParser.Parse` 或 `CommandDescriptor.DispatchAsync` public signature；
- 新增 IVT；
- 改 command tree、binding、diagnostic、dispatch、help、error或exit行为。

### Task 19 允许

- payload nested accessibility `public` → `internal`；
- 添加 JSON golden、architecture、AOT evidence；
- current baseline和决议台账同步。

### Task 19 禁止

- 修改 payload record shape或加 `sealed`；
- 修改 diagnostic、renderer overload、JsonContext options；
- 修改 schema、encoder、null/default/property order；
- 修改 CLI routing、stdout/stderr、usage或exit codes；
- 修复本报告记录的相邻问题。

## 11. 停止条件

出现任一情况必须停止对应任务：

1. 发现真实 external/private CLR consumer或新 declaration；
2. factory或concrete results进入未发现的 public/protected signature；
3. base record无法保持完整 public/protected shape；
4. Task 18需要新增 IVT或修改 parser/dispatcher public contract；
5. payload internalize导致 source generator、trim或Native AOT失败；
6. 需要改JSON shape、schema、escaping、null或CLI行为才能通过；
7. Task 18不是精确 `14/489/63`，Task 19不是精确 `14/488/62`；
8. historical manifest、136-entry计数或blob漂移；
9. Task 20任一owner test、public API drift、aggregate、AOT或复审finding未关闭。

Task 17 至此只批准后续受控实施资格，不预称 G-04D5 或 G2 已关闭。
