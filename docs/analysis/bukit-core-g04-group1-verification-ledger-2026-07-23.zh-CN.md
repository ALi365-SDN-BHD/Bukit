# Bukit Core G-04 Group 1 组级验证台账

日期：2026-07-23

分支：`codex/g04-group1-pluginhost-content-a`

`GROUP_BASE`：`10bfead3f28b8a9f82a9b5fc008a16d49e290cae`

状态：`group-verification-complete`；允许合并回 `2.0` 并进入 G2。

## 1. 关闭范围

本组串行完成 master plan Task 1～10：

- PluginHost D2：16 个原始候选中 8 个 internalized、8 个 retained-by-design；
- execution report：持久化 JSON v1 是受支持契约，三个 writer/DTO CLR identity
  internalized；
- Content D3A：四个 Body/Markdown implementation/helper identity internalized；
- `Bukit.Content.Notion.NotionClientStats` 明确保留给 G2 Task 11；
- current public API baseline 为 14 assemblies / 497 public types /
  85 `2.0-candidate`；
- 历史 136 项 candidate manifest 未改，Git blob 仍为
  `7b07d6890562387010b52301e9f8716e9bf10ed1`。

没有修改配置 schema、插件协议、asset URL、媒体/SEO、TLS、CI/release/gate 脚本或全局
路径工具。

## 2. 组级测试证据

所有命令均在 Release、非沙箱环境执行；restore 仅用于缺失的本地依赖准备，并显式设置
`NuGetAudit=false`。

| 验证 | 结果 |
|---|---|
| `Bukit.PluginHost.Tests` | 171 passed / 0 failed / 0 skipped |
| `Bukit.Content.Tests` | 464 / 0 / 0 |
| `Bukit.Cli.Tests` | 610 / 0 / 0 |
| `Bukit.Architecture.Tests` | 155 / 0 / 0 |
| `SiteEngineBodyStoreLifetimeTests` | 3 / 0 / 0；成功、异常、取消均保持 exactly-once disposal |
| `public-api-drift.sh check Release` | exit 0；baseline 与当前导出面一致 |

首次组级运行暴露并关闭了三个局部验证阻断：

1. public xUnit theory signature 暴露 internal `PluginRuntimeOnlyContext`，改为 public
   `string` 数据输入并在测试体内解析；生产代码未改；
2. 四处 `Where(...)+Assert.Single(...)` 触发 `xUnit2031`，按 analyzer 建议改为
   predicate overload；断言语义未改；
3. `PluginPermissionPathNormalizer` 的隐式无参构造器在 metadata 中仍为 public，补显式
   empty internal ctor，完成已批准的 D2D constructor boundary；路径行为未改。

修复后重新运行受影响 owner tests，PluginHost 171/171、Architecture 155/155 均通过。

## 3. Native AOT 与发布产物

在全新 `/tmp` proof roots 上验证 Darwin arm64：

- `native-aot.sh 2.0.0-g04g1 osx-arm64 ... Release` exit 0；
- canonical 归档：
  `/private/tmp/bukit-g04-g1-aot/bukit-2.0.0-g04g1-osx-arm64.tar.gz`；
- archive 非空，published CLI 可执行；
- `release-artifacts.sh` exit 0；
- basic Markdown fixture 输出 `Config check passed`、`Build completed:`；
- publish audit 为 `routes=2 errors=0 warnings=22`。

这些产物只用于本地 proof，不进入仓库。

## 4. 真实 Echo 进程插件与 execution report v1

Echo 以 `osx-arm64` self-contained single-file 发布到独立临时站点，真实 SHA-256 为
`b75b9dced7355384ce79721eff4addf7247eee9db4ce469ff5791f7dc422a3b2`。

发布的 Native AOT CLI 完成：

- `plugin validate-config`：exit 0、stderr 为空、canonical path 精确匹配；
- `plugin validate-manifest plugins/echo`：exit 0、stderr 为空；
- `plugin list`：`echo@1.0.0 enabled=true status=ok platform=osx-arm64 commands=echo`；
- lock：protocol、platform、entry、真实 SHA 和 `sha256Verified=true` 全部匹配；
- `echo hello`：exit 0、CLI stderr 为空，arguments、options、rootDir、workingDir 全匹配；
- execution report 数量严格为 1。

唯一 report 的 identity、operation、protocol、platform、command、commandPath、entry、两
个 exit code、SHA、success、timeout、output-limit、response summary 均通过 `jq -e`。
实测 `stdoutBytes=460`、`stderrBytes=33`，stderr 精确为
`bukit-plugin-echo handled invoke\n`。这同时证明 internal report writer/DTO 在 Native
AOT published CLI 的真实 process-plugin 路径仍可达。

## 5. 独立轻量复审

独立只读复审基于 `GROUP_BASE..7e0fe66c`，结果：

- Critical：0
- Important：0
- Minor：0

复审确认：

- D2 的 8 internalized + 8 retained 终态完整；
- D2F 八项 process seam 仍 public/exported；
- persisted JSON v1 的 schema、golden、writer 25 字段一致，未新增
  `schemaVersion` 或 raw `stdout`；
- D3A production diff 仅四个 type accessibility 变化；
- `NotionClientStats` 仍 public；
- PluginHost 与 Content 的 production friendship 未扩张；PluginHost 仅新增 D2E
  批准的 `Bukit.PluginHost.Tests` 与 `Bukit.Cli.Tests` 两个精确 test-only friends；
- historical manifest blob、baseline 和禁止漂移边界均满足。

## 6. 唯一 aggregate targeted gate

本组只允许执行一次：

```bash
bash scripts/checks/post-change-targeted.sh \
  --base 10bfead3f28b8a9f82a9b5fc008a16d49e290cae \
  -- <GROUP_BASE..HEAD 的全部变更路径，含本台账>
```

observed result：exit 0。该唯一执行消费了 `GROUP_BASE..7e0fe66c` 的全部 45 个已跟踪
变更路径及当时尚未跟踪的本台账，依次通过：

- diff/untracked whitespace；
- Content 464/464、PluginHost 171/171、Architecture 155/155；
- docs consistency、active links、absolute-path、size、command/workflow boundaries；
- focused/targeted/format/public-API-drift self-tests；
- `dotnet format`、code-analysis ratchet、public API drift；
- portability、brainstorm server、config/CLI/skills/README contracts；
- YAML static context deterministic drift check。

aggregate 后只把本节与顶部状态改为 observed result，并同步两个现行 closure ledger；
没有修改生产、测试、schema、fixture、baseline 或历史 manifest，也没有再次运行
aggregate。

## 7. 关闭判定

G1 的 implementation、owner tests、API drift、唯一 aggregate targeted gate、AOT、
真实插件路径及独立复审均已满足；Group 1 正式关闭。

G2 仍必须建立独立分支，并从 Task 11 `NotionClientStats` 开始；本台账不授权提前处理
Shared、CLI Shared、Rendering、Routing、Theme 或 Engine 候选。
