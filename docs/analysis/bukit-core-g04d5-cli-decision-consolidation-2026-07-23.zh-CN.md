# Bukit Core G-04D5 CLI Shared 决策汇总

日期：2026-07-23

任务：G-04D5 / master plan Task 17～20

状态：`implementation-complete / group-verification-pending`

## 1. 范围

本汇总只处理 `Bukit.Cli.Shared` 的五个原始候选身份：

1. `CliBoundCommandFactory`
2. `CliParseResult`
3. `SimpleParseResult`
4. `SubcommandParseResult`
5. `CliErrorRenderer.CliErrorPayload`

根据用户在 Task 20 验证阶段明确的范围约束，所有修复只针对 Bukit Core；
Labs 和外部插件不属于修复范围。本任务不修改 CLI command tree、参数、help、退出码、
错误 JSON schema、插件协议或任何 Labs/外部插件源码。

## 2. 最终决策

| 身份 | 最终可见性 | 决策 | 依据 |
|---|---|---|---|
| `CliBoundCommandFactory` | `internal` | internalize | 仅为 Core parser/binding 实现；公共 parser 可覆盖受支持入口 |
| `CliParseResult` | `public abstract record` | retained-by-design | 公共 `CliParser.Parse` 返回该类型，公共 `CommandDescriptor.DispatchAsync` 消费该类型，并保留外部派生契约 |
| `SimpleParseResult` | `internal sealed record` | internalize | 具体实现只由 Core parser/dispatcher 创建和识别 |
| `SubcommandParseResult` | `internal sealed record` | internalize | 具体实现只由 Core parser/dispatcher 创建和识别 |
| `CliErrorRenderer.CliErrorPayload` | `internal sealed record` | internalize | CLR identity 仅供 renderer 与 source-generated JSON context 使用；持久机器契约是 JSON v1，而不是 DTO 可见性 |

没有新增 production `InternalsVisibleTo`、test-only production facade、source-link
compilation 或 public wrapper。

## 3. 行为契约

Task 18/19 固定并验证：

- registry command tree 的名称、subcommand 和顺序；
- descriptor 覆盖与 registry 命令集合一致，但不伪造两者历史列表顺序相同；
- 大小写不敏感 option binding、flag、`=` 值和 unknown option diagnostic；
- nested subcommand 当前 diagnostic 传播行为；
- parser diagnostic code 与顺序；
- dispatcher、help 和 exit code；
- CLI error JSON 的 schema、字段顺序、null omission、escaping、stderr/stdout 和 exit code；
- `CliErrorJsonContext` 的两个 AOT root 与 source-generated type info 使用。

相邻但未在本任务修改的问题：

1. error schema 将 `command` 标为 required，而 runtime 的低层 renderer 允许省略；
2. 意外 inner exception 可能在 JSON 后追加 plaintext；
3. parser 可递归产生 nested subcommand result，而 dispatcher 当前只处理 immediate child。

这些问题需要独立行为任务，不能借公共面治理顺带修复。

## 4. 公共面与历史证据

当前 public API baseline：

- 14 assemblies；
- 488 public types；
- 62 个 `compatibility=2.0-candidate`。

历史消费者声明 manifest：

- 136 entries；
- Git blob
  `7b07d6890562387010b52301e9f8716e9bf10ed1`；
- 文件内容未修改。

`CliParseResult` 在当前 baseline 中保留：

- `classification=cross-assembly-implementation`
- `compatibility=1.x-do-not-narrow`
- `migrationHorizon=2.0-review`

其余四个 internalized identity 不再出现在当前 public API baseline。

## 5. Task 20 验证边界

Task 20 的 Core owner tests、public API drift、真实 Native AOT、发布产物 smoke、唯一一次
aggregate targeted gate 和独立轻量复审完成后，才能把本汇总改为
`group-verification-complete`。

Labs CLI 的既有 YAML static-context 缺陷已在 G2 base 原样复现；由于用户明确排除 Labs
修复，它记录为 out-of-scope baseline evidence，不授权修改 Labs 项目或测试。

