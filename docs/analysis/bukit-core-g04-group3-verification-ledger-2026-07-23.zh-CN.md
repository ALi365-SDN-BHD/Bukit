# Bukit Core G-04 Group 3 组级验证台账

日期：2026-07-23

分支：`codex/g04-group3-rendering-routing-theme`

`GROUP_BASE`：`b4a60b7ebeef34eda9f53e72a10a76ebc10c8544`

状态：`verification-in-progress / aggregate-pending`

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

G3 aggregate 尚未执行。执行前必须把 `GROUP_BASE..HEAD` 的全部 tracked paths 与本台账
纳入一次 `post-change-targeted.sh --base`；先做独立 whitespace/JSON/manifest 静态
预检，避免用已知格式问题消费唯一 aggregate。

## 6. 独立轻量复审

尚未执行。aggregate 通过后，必须基于完整 `GROUP_BASE..HEAD` 做一次独立只读复审，
并记录 Critical、Important、Minor 结果。

## 7. 当前关闭判定

Implementation、owner tests、public API drift 与 Native AOT 已完成。G3 仍处于
`aggregate-pending`，在唯一 aggregate 与独立复审完成前不得标记关闭或合并回 `2.0`。
