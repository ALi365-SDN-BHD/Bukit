# Bukit Core G-04D5B CLI error payload resolution

日期：2026-07-23

状态：`implementation complete / group-verification-pending`

## 1. 决议摘要

G-04D5B 只处理一个 historical `2.0-candidate`：

```text
Bukit.Cli.Shared.Cli.Rendering.CliErrorRenderer.CliErrorPayload
```

Task 19 对 declaration 做一个 token 的 accessibility 变更：

```diff
- public record CliErrorPayload(
+ internal record CliErrorPayload(
```

除此以外：

- primary constructor、properties、record shape和属性顺序不变；
- `CliErrorRenderer` 保持 public；
- `CliErrorRenderer.CliErrorDiagnostic` 保持 public；
- 全部 public `Render`/`RenderJson` overload保持；
- JSON schema id/version、字段、null策略、命名、缩进和error顺序不变；
- `CliErrorJsonContext` 与 source-generation registrations保持；
- CLI stdout/stderr和exit-code规则不变；
- 不修改 `docs/schemas/cli-error.v1.schema.json`；
- 不新增 reflection serializer、IVT、DTO facade或兼容 shim。

当前状态是实现决议完成；Group 2 owner tests、public API drift、aggregate targeted
gate、Native AOT与独立只读复审统一留待 Task 20。

## 2. 为什么 payload 可以 internalize

`CliErrorPayload` 的 production ownership 完全位于
`Bukit.Cli.Shared.Cli.Rendering`：

```text
CliDiagnostic / CliErrorDiagnostic
  -> CliErrorRenderer.RenderJson
  -> new CliErrorPayload(...)
  -> CliErrorJsonContext
  -> JSON string
  -> Core CLI stderr
```

仓库内没有 production member：

- 返回 `CliErrorPayload`；
- 接收 `CliErrorPayload`；
- 由外部程序集直接构造它；
- 通过 reflection 按 identity发现它；
- 将它传播到 Labs、plugin protocol、configuration、theme或report model。

public consumer收到的是 `RenderJson(...)` 返回的 `string`。真正的产品契约是 CLI
error envelope 的 JSON shape、channel和exit behavior，而不是该 nested CLR record。

因此本任务不需要引入新的 public DTO。把相同数据复制到一个新 facade只会制造第二套
CLR contract，而不会提高 CLI JSON 兼容性。

## 3. 保留的 public renderer/diagnostic surface

下列 identity 与 signatures 继续保持 public：

```text
Bukit.Cli.Shared.Cli.Rendering.CliErrorRenderer
Bukit.Cli.Shared.Cli.Rendering.CliErrorRenderer.CliErrorDiagnostic
```

保留的行为入口包括：

- `Render(CliDiagnostic)`；
- diagnostics 使用默认 exit code `2` 的 `RenderJson`；
- diagnostics 使用显式 exit code 的 `RenderJson`；
- public `CliErrorDiagnostic` list overload；
- 显式 schema/schemaVersion overload；
- exception overload。

`CliErrorDiagnostic` 仍是 public input model，因为 Core CLI 会先将
`CliDiagnostic` 映射为其 list，再调用 public renderer。Task 19 没有授权把 input
diagnostic graph一起收窄。

## 4. JSON contract保持

payload accessibility 不得影响序列化结果。Task 19 保持：

| JSON contract | 保持值 |
|---|---|
| `schema` | `https://bukit.dev/schemas/cli-error.v1.json` |
| `version` | `1.0` |
| naming | camelCase |
| formatting | indented JSON |
| null策略 | `JsonIgnoreCondition.WhenWritingNull` |
| errors | 保持输入顺序 |
| error字段 | `code`、`message`、`showUsage` |
| exit code | renderer收到的整数原样写入 |

非 null payload 的字段顺序继续是：

```text
schema
version
command
exitCode
errors
usage
```

`usage` 为 null 时继续省略；`command` 为 null 时也继续按当前 source-generation
options省略。Task 19 不调整 null shape，不借 internalization 修改 schema或创建新
version。

## 5. Source generator 与 Native AOT

`CliErrorJsonContext` 继续是 internal source-generated context，并继续静态注册：

```csharp
[JsonSerializable(typeof(CliErrorRenderer.CliErrorPayload))]
[JsonSerializable(typeof(CliErrorRenderer.CliErrorDiagnostic))]
```

payload 必须是 `internal` 而不是 `private`：context 位于 `CliErrorRenderer` 外部，
仍需在同程序集编译期引用 nested type。Task 19 不移动 context，也不改为 runtime
reflection serialization。

accessibility narrowing 不改变：

- source generator看到的成员；
- generated `JsonTypeInfo`；
- static serializer reachability；
- trimming root；
- property naming与null ignore options。

不过“理论上静态可达”不能替代真实 Native AOT 证明。Task 20 必须使用 published
Core CLI 触发 JSON error path，并解析真实 stderr，而不只依赖普通 managed unit test。

## 6. CLI channel 与 exit behavior

Core CLI 当前 channel contract保持：

### JSON error mode

- `--log-format json` 选择 JSON error rendering；
- JSON envelope写入 stderr；
- stdout不承载 error envelope；
- parser/unknown-command error返回 `2`；
- `ConfigException`、`ContentException`返回 `2`；
- `RenderException`返回 `3`；
- unexpected exception返回 `1`。

### Text error mode

- 首条 diagnostic以文本写入 stderr；
- `ShowUsage` 要求时追加 usage；
- 不把 JSON envelope写到 stdout；
- exit code与 JSON mode保持同一业务分类。

Task 19 只收窄 payload identity，不修改 entry-point routing、log-format parsing、
exception classification、usage选择或 channel。

## 7. 相邻问题一：schema required `command`

当前 schema 将 `command` 同时定义为：

- required property；
- value type允许 `string` 或 `null`。

而 runtime source-generation options 使用
`DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`。因此调用 renderer 并传
`command = null` 时，runtime会省略 `command`，而不是写 `"command": null`。

这是既有 schema/runtime null-shape差异，不是 payload public accessibility 导致的。
G-04D5B 明确不：

- 修改 schema required list；
- 为 `command` 添加 per-property ignore override；
- 强制写 null；
- 升级 `cli-error.v1`；
- 改 renderer overload的nullable signature。

如果需要收敛，必须建立独立 CLI schema-contract任务，先决定 canonical行为、兼容
影响和版本策略。Task 19 的回归测试只冻结当前 null处理。

## 8. 相邻问题二：inner exception 污染 JSON stderr

Core CLI unexpected-exception catch 当前先通过 `PrintError` 写 error envelope；随后若
存在 `InnerException`，又把 inner message作为普通文本写入 stderr。

在 JSON error mode 下，这可能形成：

```text
{ valid JSON envelope }
inner exception text
```

整个 stderr因尾随文本不再是单一可解析 JSON document。这是 entry-point exception
channel contamination，和 `CliErrorPayload` 的 CLR visibility无关。

G-04D5B 明确不：

- 删除 inner exception输出；
- 把 inner message加入 JSON schema；
- 在 JSON mode 改日志channel；
- 改 unexpected exception exit code；
- 修改 exception masking或diagnostic vocabulary。

该问题应作为独立 CLI channel/diagnostic任务处理，必须同时定义安全脱敏、JSON shape、
text mode、stderr/stdout和Native AOT测试。Task 19 不以“顺手修复”方式扩大范围。

## 9. Core、Labs 与 Plugin 边界

### Core

Core CLI 继续通过 public renderer获得 JSON string。entry point不构造、返回或检查
payload CLR type。

### Labs

`Bukit.Labs.Cli` 不引用 `CliErrorPayload`，也不调用 Core 的 JSON error renderer。
Task 19 不修改 Labs error文本、help、command set或exit code。Task 20 仍需运行 Labs
owner tests，以证明 shared assembly metadata变化没有造成编译回归。

### Plugins

`CliErrorPayload` 不是 `bukit-plugin-v1` wire DTO，不进入 handshake、manifest、
invoke response、plugin diagnostic或artifact envelope。Task 19 不修改：

- process-plugin protocol；
- plugin JSON serializer context；
- plugin command metadata；
- plugin error codes；
- output ownership或report schema。

## 10. Public API baseline

Task 18 完成后的 current baseline：

```text
14 assemblies / 489 public types / 63 candidates
```

Task 19 的精确 delta：

| 操作 | Public types | Candidates |
|---|---:|---:|
| internalize `CliErrorPayload` | -1 | -1 |

Task 19 current baseline：

```text
14 assemblies / 488 public types / 62 candidates
```

current baseline 必须满足：

- nested payload不再出现；
- public renderer仍出现；
- public `CliErrorDiagnostic` 及其 members仍出现；
- renderer的 public overload signatures没有 drift；
- 除 payload identity删除外没有 Task 19 无关 CLR drift。

Task 18 的三个 internalized parse identities和 retained/reclassified
`CliParseResult` 也必须继续保持，不能在 Task 19 回退。

closed 136-entry consumer-declaration manifest 是声明窗口关闭时的历史 cohort，必须
保持原内容、136项和 Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`。current baseline 描述当前
2.0 surface，两者不得混写。

## 11. External consumer 与兼容性

2026-07-22 authenticated GitHub search：

- full-name query返回 `0`；
- simple-name query返回20个 truncated同名结果；
- `Bukit CliErrorPayload` resolution query返回 `0`；
- reviewed simple-name matches均不是 Bukit payload direct consumer。

closed manifest 仍保留：

```text
declarationStatus = consumer-declaration-pending
privateConsumerStatus = unknown-until-voluntary-declaration
externalEvidence.searchStatus = no-public-match-found
```

这不能证明 private、unindexed 或 undisclosed consumers 不存在。

兼容性影响：

- 未声明的 direct CLR source consumer在 2.0 重新编译时会 breaking；
- metadata/reflection 按 exact nested identity查找的 consumer会 breaking；
- 已编译且直接引用 public nested payload 的 binary consumer不保证继续加载；
- 只消费 documented CLI JSON、exit code或 `bukit-plugin-v1` 的 consumer不受该 CLR
  accessibility change影响；
- public `CliErrorDiagnostic` 与 renderer调用代码不需要迁移。

不新增 obsolete shim：公开行为 facade 已是 `RenderJson`，且没有 reviewed direct
consumer证据。若后续出现真实消费者，应单独决定 retain、compatibility facade或
migration window，不得重写 closed historical manifest。

## 12. Task 19 测试边界

Task 19 的测试必须覆盖：

| 测试面 | 必须冻结 |
|---|---|
| architecture | payload internal/not exported；renderer/diagnostic public |
| source generation | 两个 `JsonSerializable` registrations保持 |
| JSON shape | schema/version、字段名、字段顺序、缩进、null省略 |
| diagnostics | error顺序、code/message/showUsage、转义 |
| exits | 默认2、显式exit code、BukitException code、generic `cli-error` |
| channels | JSON stderr、stdout空、文本stderr与usage |
| governance | `14/488/62`；closed manifest 136/blob不变 |

architecture test可通过 assembly metadata确认 internal payload，但不得：

- 为测试添加 production IVT；
- 暴露 test-only public payload factory；
- 改用 reflection serializer；
- 直接复制 production payload形成测试专用第二套模型。

JSON行为应经 public `RenderJson` 与真实 CLI entry point验证，不依赖 internal record
construction。

schema-required-command 与 inner-exception contamination 应记录为相邻问题，但本任务
测试不得通过改变现状来“修复”它们。

## 13. Task 20 待验证

按 master plan 的组级规则，Task 19 不单独运行测试、aggregate gate、Native AOT或
只读复审。当前状态保持 `group-verification-pending`。

Task 20 至少必须验证：

- `Bukit.Cli.Tests`；
- `Bukit.Labs.Cli.Tests`；
- `Bukit.Architecture.Tests`；
- master plan 所列 G2 Content、Notion、Shared、Engine owner projects；
- source-generated managed JSON tests；
- unknown command、invalid option与exception exit behavior；
- JSON mode stdout为空、stderr是预期 envelope；
- current public API snapshot与 baseline exact match；
- historical manifest blob与136-entry保持；
- G2 aggregate `post-change-targeted.sh --base <GROUP_BASE>` 只运行一次；
- 真实 Native AOT publish；
- published CLI 执行 `missing-command --log-format=json`，stderr可按本任务正常路径解析；
- release-artifact smoke；
- 一次 G2 轻量独立只读复审。

Task 20 对普通 unknown-command JSON 的验证不等于 inner-exception contamination 已解决。
后者保持独立 open adjacent issue，不能在关闭台账中误写为已修复。

## 14. 停止条件

出现下列任一情况必须停止关闭，不得扩大 Task 19：

1. 发现真实 public/private direct CLR consumer需要 payload identity；
2. source generator无法在 payload internal后静态生成或 Native AOT可达；
3. 需要把 payload改为 private并搬移 context，或引入 reflection serializer；
4. public renderer、`CliErrorDiagnostic` 或 overload signatures发生 drift；
5. JSON schema/version、字段、顺序、null处理、缩进或diagnostic顺序改变；
6. stdout/stderr、usage、exception classification或exit code改变；
7. Labs、Core CLI或dynamic plugin command出现编译/行为回归；
8. public API baseline不是精确 `14/488/62`；
9. closed manifest内容、136-entry计数或 blob发生变化；
10. Task 20 owner tests、aggregate gate、Native AOT或独立复审存在未关闭 failure；
11. 修复要求进入 schema-required-command、inner-exception contamination、plugin
    protocol、parse graph或其他相邻范围。

停止后应选择 retain、独立 facade/migration任务或独立 CLI contract修复；不得通过
扩大 Task 19 来换取候选数下降。

## 15. 正式关闭台账

| Identity | Task 19状态 | Current baseline终态 | Group 2状态 |
|---|---|---|---|
| `CliErrorRenderer.CliErrorPayload` | one-token internalized | removed from public baseline | Task 20 verification pending |
| `CliErrorRenderer` | retained public | unchanged | Task 20 verification pending |
| `CliErrorRenderer.CliErrorDiagnostic` | retained public | unchanged | Task 20 verification pending |

Task 19 的实现决策已经完成；JSON/source-gen/CLI behavior、public API drift、G2
aggregate gate、Native AOT与独立只读复审统一留待 Task 20。schema required
`command` 与 inner-exception JSON stderr contamination均保持独立相邻问题，不属于本
任务关闭范围。
