# Bukit Core G-04 Group 2 组级验证台账

日期：2026-07-23

分支：`codex/g04-group2-content-b-shared-cli`

`GROUP_BASE`：`27dcc456d5f6a614d2a7bc9a35fb93bd938a9766`

状态：`group-verification-complete`；允许合并回 `2.0` 并进入 G3。

## 1. 关闭范围

本组按 master plan 串行完成 Task 11～20 的 Core 范围：

- Content D3B：删除重复 public `Bukit.Content.Notion.NotionClientStats`，直接返回
  canonical `Bukit.Notion.Transport.NotionClientStats`；
- Shared D4：删除三个 legacy tokenizer CLR identity，迁移至 canonical
  `Bukit.Notion.Conversion`；`ValueCoercion` internalized；13 个跨程序集 Notion models
  retained-by-design；
- CLI Shared D5：`CliBoundCommandFactory`、`SimpleParseResult`、
  `SubcommandParseResult` 和 nested `CliErrorPayload` internalized；
  `CliParseResult` retained-by-design。

用户在组级验证阶段明确：所有修复只针对 Bukit Core，Labs 和外部插件不在范围内。
因此本台账只把 Core owner tests、Core AOT 和 Core 发布产物作为关闭证据，不修改或修复
Labs/外部插件。

没有修改配置 schema、插件协议、asset URL、TLS/HTTP、CI/release/gate 脚本或全局路径
工具。

## 2. Core owner tests

所有有效测试均在完成真实 restore 后、Release、非沙箱环境执行。

| 验证 | 结果 |
|---|---|
| `Bukit.Content.Tests` | 464 passed / 0 failed / 0 skipped |
| `Bukit.Content.Notion.Tests` | 6 / 0 / 0 |
| `Bukit.Notion.Tests` | 339 / 0 / 0 |
| `Bukit.Shared.Tests` | 335 / 0 / 0 |
| `Bukit.Cli.Tests` | 618 / 0 / 0 |
| `Bukit.Engine.Tests` | 1594 / 0 / 0；测试进程显式移除宿主环境中的 `NOTION_TOKEN` |
| `Bukit.Architecture.Tests` | 183 / 0 / 0 |
| `public-api-drift.sh check Release` | exit 0；当前程序集导出面与 baseline 精确一致 |

最初五个 `--no-restore` 命令因新 worktree 缺少 `project.assets.json` 而成为无输出
no-op；这些运行没有被记为通过。完成 solution 和三个独立项目 restore 后，上表记录的
才是真实测试结果。

组级测试还发现并关闭三个仅测试层问题：

1. CLI contract 错把历史上不同顺序的 descriptor list 与 registry list 强制同序，
   改为保留 registry exact tree golden，同时验证两者命令集合等价；
2. Architecture test 错误读取不存在的 `JsonSerializableAttribute.Type`，改用
   constructor metadata；
3. 两个 nullable collection assertion 和一个 xUnit collection-size assertion
   不符合当前编译器/analyzer，按原断言语义最小修正。

生产代码未因这些测试问题改变。

## 3. 排除范围证据

`Bukit.Labs.Cli.Tests` 在 G2 分支与 `GROUP_BASE` 均为相同的 147 passed / 5 failed。
根因包括：

- Labs theme YAML 静态反序列化项目缺少直接 generator 引用；
- 非沙箱宿主已有 `NOTION_TOKEN`，一项 Labs 测试未隔离环境变量。

这不是 G2 Core 回归。根据用户明确的 Core-only 修复边界，本组不修改 Labs 项目、Labs
测试或外部插件，也不把这五项写成通过。

## 4. Native AOT 与发布产物

Core-only Darwin arm64 证明：

- `native-aot.sh 2.0.0-g04g2 osx-arm64 ... Release`：exit 0；
- canonical archive：
  `/private/tmp/bukit-g04-g2-aot/bukit-2.0.0-g04g2-osx-arm64.tar.gz`；
- `release-artifacts.sh`：exit 0；
- basic Markdown fixture：config check 和 build 均成功；
- publish audit：`routes=2 errors=0 warnings=22`。

真实发布二进制执行：

```text
bukit missing-command --log-format=json
```

结果：

- exit code `2`；
- stdout `0` bytes；
- stderr `343` bytes，解析为单一 JSON document；
- schema、version、command、exitCode、唯一 error code `unknown-command` 均匹配。

临时 proof 产物不进入仓库。

## 5. 公共面与不可变历史证据

- current baseline：14 assemblies / 488 public types / 62
  `compatibility=2.0-candidate`；
- historical candidate manifest：136 entries；
- historical manifest Git blob：
  `7b07d6890562387010b52301e9f8716e9bf10ed1`；
- manifest 内容未修改。

## 6. 唯一 aggregate targeted gate

组级 aggregate 共发生三次形式调用：

1. 初始调用使用了 macOS Bash 3.2 不支持的 `mapfile`，实际只传入未跟踪台账，并在
   whitespace preflight 发现台账末尾空行后退出；没有运行 owner tests 或后续门禁；
2. 经授权的 replacement 使用完整 50 路径，在 whitespace preflight 发现两份已提交
   文档的尾空格/末尾空行后退出；没有运行 owner tests 或后续门禁；
3. 经再次明确授权的 second replacement 使用 Bash 3.2 兼容数组，覆盖
   `GROUP_BASE..HEAD` 的 49 个已跟踪路径和未跟踪本台账，共 50 路径。

second replacement observed result：exit 0。它依次通过：

- diff 与 untracked whitespace；
- CLI 618/618、Content 464/464、Shared 335/335、Architecture 183/183、
  Notion 339/339；
- docs consistency、active links、absolute path、size、Core command/workflow boundary；
- focused/targeted/format/public API drift self-tests；
- `dotnet format`、code analysis ratchet、public API drift；
- portability、brainstorm server、config/CLI/skills/README contracts；
- Core YAML static context self-test、normalizer self-test和 deterministic drift gate。

aggregate 后仅更新本台账和三份 G2 decision consolidation 的关闭状态；没有修改生产、
测试、baseline、历史 manifest、Labs 或外部插件，也没有再次运行 aggregate。

## 7. 独立轻量复审

独立只读复审基于 `GROUP_BASE..1c8663c4`，结果：

- Critical：0
- Important：0
- Minor：0

复审确认：

- production diff 严格限于 Bukit Core 公共面治理，没有修改 Labs、外部插件、schema、
  协议、路径安全或运行时业务逻辑；
- baseline 精确为 14 assemblies / 488 public types / 62 candidates；
- historical manifest 仍为 136 项且 blob 不变；
- D3B canonical stats、D4 tokenizer/value coercion 与 D5 parser/error payload 的终态、
  owner tests 和 AOT 证据一致；
- Labs 五项基线失败被明确排除，没有伪报为通过。

剩余风险是未声明、私有或未索引的直接 CLR consumer 无法被证明不存在；这是已经记录的
2.0 breaking-change 风险，不是本组回归。

## 8. 关闭判定

Core implementation、owner tests、public API drift、Native AOT、second replacement
aggregate targeted gate 和独立轻量复审均已完成。Group 2 正式关闭，可以合并回
`2.0` 并按 Core-only 边界进入 G3。
