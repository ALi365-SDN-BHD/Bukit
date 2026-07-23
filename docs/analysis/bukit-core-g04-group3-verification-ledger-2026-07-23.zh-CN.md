# Bukit Core G-04 Group 3 组级验证台账

日期：2026-07-23

分支：`codex/g04-group3-rendering-routing-theme`

`GROUP_BASE`：`b4a60b7ebeef34eda9f53e72a10a76ebc10c8544`

状态：`group-verification-complete`

## 1. 关闭范围

本组按 master plan Task 21～30 只治理 Bukit Core：

- Rendering：`FileTemplateLoader` 与 `ScribanModelBinder` 仅收窄 type
  accessibility，loader fallback 与模板模型投影保持；
- Routing：删除 nested `RouteGenerationResult`，把 public
  `GenerateWithSource` 原子迁移为命名
  `(RouteInfo Route, RouteSource Source)`；
- Theme：`SchemaValidationException` 收窄为 internal；
  `SchemaValidationError` 与 `ThemeDoctorCommand.DoctorResult` 因 public
  facade 传播而保留 public，并重分类为
  `cross-assembly-implementation / 1.x-do-not-narrow / 2.0-review`。

没有修改 Labs、外部插件、配置 schema、插件协议、asset URL、HTTP/TLS、全局路径工具、
CI、release 或 gate 脚本。Architecture project 构建其既有 Labs/插件依赖不代表扩大修复
范围。

## 2. Core owner tests

新 worktree 最初缺少六个测试项目的 `project.assets.json`。先完成 Core solution 与六个
测试项目 restore；缺资产状态未被误记为测试通过。

| 验证 | 结果 |
|---|---|
| `Bukit.Rendering.Tests` | 169 passed / 0 failed / 0 skipped |
| `Bukit.Routing.Tests` | 27 / 0 / 0 |
| `Bukit.Theme.Tests` | 74 / 0 / 0 |
| `Bukit.Cli.Tests` | 618 / 0 / 0 |
| `Bukit.Engine.Tests` | 1595 / 0 / 0；测试进程显式移除宿主 `NOTION_TOKEN` |
| `Bukit.Architecture.Tests` | 215 / 0 / 0 |
| `public-api-drift.sh check Release` | exit 0 |

直接测试发现并最小关闭四个仅测试/治理证据问题：

1. Rendering `ExpandoObject` fixture 使用错误的非空字典注解，改为
   `IDictionary<string, object?>`；
2. Routing permalink fixture 只在 custom fields 写 type，却把 canonical
   `ContentIdentity.ContentType` 固定成 `post`，改为由 fixture 输入建立 canonical
   type/collection；
3. D7A Architecture guard 的两个 `Where + Assert.Single` 与 nullable tuple-name
   assertion 不符合现行 xUnit/analyzer，保持断言语义改为 predicate overload 与
   `Assert.Collection`；
4. `RouteGenerator` 返回类型改成 `System.ValueTuple` 后，baseline 的三个
   `publicMembers` 需要按 ordinal 稳定顺序重排；签名和分类未改变。

上述修正均未修改 production。

## 3. Native AOT 与真实发布二进制

Core-only Darwin arm64 证明：

- `package-native-aot.sh 2.0.0-g04g3 osx-arm64 ... Release`：exit 0；
- archive：
  `/private/tmp/bukit-g04-g3-aot/bukit-2.0.0-g04g3-osx-arm64.tar.gz`；
- `release-artifacts.sh`：exit 0；
- basic Markdown fixture config/build 成功；
- publish audit：`routes=2 errors=0 warnings=22`。

真实 AOT 二进制 doctor：

- 有效站点：exit `0`，包含 `Doctor passed`；
- 无效 `theme.yaml`：exit `1`，包含 `Theme manifest invalid` 与 `unknown`；
- 两条路径均不输出 `Theme Doctor Report` 或 `DoctorResult` JSON 字段。

这证明现有 Core CLI doctor 与 Theme doctor 保持隔离；没有为了测试
`DoctorResult` 而新增 CLI wiring、JSON root 或 trimming 标注。

## 4. 公共面与历史证据

- current baseline：14 assemblies / 484 public types / 56
  `compatibility=2.0-candidate`；
- Routing current candidate：0；
- retained Theme types：`SchemaValidationError`、`DoctorResult`；
- historical candidate manifest：136 entries；
- historical manifest Git blob：
  `7b07d6890562387010b52301e9f8716e9bf10ed1`；
- historical manifest 内容未修改。

## 5. 唯一 aggregate targeted gate

G3 aggregate 调用记录：

1. 初始 wrapper 使用 zsh 特殊变量名 `path` 收集 48 个路径，覆盖了 shell `PATH`，
   因而在 `/bin/bash` 启动前以 `command not found` 退出；没有运行任何 gate；
2. 首个实际 aggregate 覆盖 48 个路径，在 `Bukit.Engine.Tests` 因宿主
   `NOTION_TOKEN` 污染而得到 1594 passed / 1 failed；同一项目此前以
   `env -u NOTION_TOKEN` 得到 1595/1595；
3. 经用户批准的 replacement aggregate 以命令级 `env -u NOTION_TOKEN` 执行，
   六个 owner projects、docs consistency 和 `dotnet format` 均通过，随后
   code-analysis ratchet 报告本组新增一个 `IDE0042`：
   `RouteInventoryValidator` 对 D7 新 named tuple 仍使用临时 result；
4. 只把该消费点改为 `(route, source)` 解构，不改返回、source 文本或路由行为。
   修正后直接 code-analysis ratchet 为 style `586/593`、analyzers `323/326`，
   Engine tests 再次为 1595/1595；
5. 用户明确批准第二次 replacement，并允许在非沙箱环境执行。最终调用覆盖 49 个
   G3 changed paths，以命令级 `env -u NOTION_TOKEN` 隔离宿主凭据，exit `0`。六个
   owner projects、docs consistency、`dotnet format`、code-analysis ratchet、
   public API drift/self-test、portability、brainstorm server self-test 与其余
   `ci-fast` contracts 全部通过。

因此，wrapper 失败不计为实际 aggregate；两个实际失败均有明确分类与最小修复/环境
隔离；最终 replacement 获得单独授权并完整通过。本台账不把局部 owner proof 替代为
aggregate proof，也不隐藏调用历史。

## 6. 独立轻量复审

已基于完整 `GROUP_BASE..b22e32cd` 完成一次独立只读轻量复审：

- Critical：0；
- Important：0；
- Minor：0。

复审确认 Rendering 仅改变两个 type accessibility；Routing 为原子 named tuple
迁移并同步 Engine consumer；Theme exception internal，而 `SchemaValidationError` 与
`DoctorResult` 正确保留 public。current baseline `14/484/56`、136-entry manifest 与
Git blob 均一致；没有 Labs、插件、schema、protocol、config、security/path、Core CLI
production、JSON/AOT root 漂移。剩余风险仅是 private/unindexed consumer 无法被公开
证据排除，以及两项 2.0 breaking change 本身需要发布说明。

## 7. 当前关闭判定

Implementation、owner tests、public API drift、Native AOT、release artifact smoke、
published doctor、最终 replacement aggregate 与独立复审均已完成。G3 正式判定为
`group-verification-complete`，可以申请本地合并回 `2.0`，随后按 master plan 进入
G4；本任务不自行执行合并。
