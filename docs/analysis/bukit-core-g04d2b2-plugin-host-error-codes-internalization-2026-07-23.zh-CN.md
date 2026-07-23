# Bukit Core G-04D2B2 `PluginHostErrorCodes` 单类型 internalization 决策账本

日期：2026-07-23

基线：`2.0@757fb14976ad7337edc2a6fbf925b986222dea6f`

状态：`qualification-complete`；实现、真实 Native AOT/归档 smoke、published
process-plugin proof、纠正后的精确只读断言与独立复审均已完成

## 决策

只将 `Bukit.PluginHost.PluginHostErrorCodes` 的 containing type 从 public
收窄为 internal。六个 const 成员和值、五个 Host 实际诊断行为及
`plugin.permissionDenied` 保留词汇保持不变。

## 兼容边界

这是 2.0-only source/public-metadata/reflection breaking change。普通已编译
const consumer 可能继续使用内联字符串，但这不构成全面 binary compatibility
承诺。私有消费者继续为 `unknown-until-voluntary-declaration`。

## Governed delta

目标是 14 assemblies / 507 types / 103 candidates。closed 136-entry manifest
必须保持 blob `7b07d6890562387010b52301e9f8716e9bf10ed1`。

## 搜索证据限制

2026-07-22 认证公开搜索未发现目标匹配；2026-07-23 环境没有可校准的治理级
GitHub Code Search，因此没有把本轮连接器结果写成新的认证快照。

## Task 1 实施证据

Task 1 基于 `47ae3fe34d486cc728dc2dc5ec0670242b6ae855` 完成，并提交为
`27492e2077e0174cf36213120b43152ad977177d`：

- 修改前完整 PluginHost 项目为 170 passed / 0 failed，完整 Architecture
  项目为 130 passed / 0 failed。
- 下列 visibility 命令在生产修改前按预期 RED：0 passed / 1 failed，失败点为
  `Assert.False()`（expected `False`，actual `True`）；将唯一目标类型从
  `public` 改为 `internal` 后重跑同一命令 GREEN：1 passed / 0 failed。

  ```text
  dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --nologo --verbosity minimal --tl:off --filter FullyQualifiedName~PluginHostAssembly_KeepsErrorCodeTypeInternalAndDoesNotExportIt
  ```

- 下列 governed-baseline 命令在快照替换前按预期 RED：0 passed / 1 failed，
  expected type count 为 507，actual 为 508。

  ```text
  dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --nologo --verbosity minimal --tl:off --filter FullyQualifiedName~CurrentBaseline_ContainsFourteenAssemblies507TypesAnd103Candidates
  ```
- B2 Architecture class 为 6 passed / 0 failed；选定 current-state 与
  plugin-boundary filter 为 57 passed / 0 failed；完整 PluginHost 项目为
  170 passed / 0 failed；完整 Architecture 项目为 132 passed / 0 failed。
- 唯一一次 `post-change-focused.sh` 退出 0，其中 PluginHost 为
  170 passed / 0 failed、Architecture 为 132 passed / 0 failed。本任务没有重跑
  focused、aggregate、full/release gate、`test-all` 或 `smoke-all`。

## Public API 与治理证据

生成快照与治理断言确认 14 assemblies / 507 types / 103 candidates。旧基线删除
且仅删除
`Bukit.PluginHost	Bukit.PluginHost.PluginHostErrorCodes`
后，与新快照排序后的 JSON 逐字节相同；目标旧记录包含获批的六个 B1 const
成员。closed 136-entry manifest blob 仍为
`7b07d6890562387010b52301e9f8716e9bf10ed1`。

## Task 2 真实发行与进程插件证据

Task 2 在 Darwin arm64、干净的两个临时 proof root 上执行。所有构建、发布及真实
进程执行均使用用户授权的非受限环境。

### Native AOT 与 release-artifact smoke

- `scripts/build/native-aot.sh 2.0.0-g04d2b2 osx-arm64
  /tmp/bukit-g04d2b2-aot Release` 退出 0；归档
  `/tmp/bukit-g04d2b2-aot/bukit-2.0.0-g04d2b2-osx-arm64.tar.gz`
  非空，published CLI 可执行。脚本实际末行使用 macOS 规范化路径
  `/private/tmp/bukit-g04d2b2-aot/bukit-2.0.0-g04d2b2-osx-arm64.tar.gz`，
  原 `/tmp/...` lexical 断言返回 exit 1。控制器随后按独立复审建议将验收纠正为
  `AOT_PROOF_ROOT="$(cd /tmp/bukit-g04d2b2-aot && pwd -P)"` 的物理路径身份；
  使用保留的原始 stdout 重跑该精确比较及归档/可执行断言，exit 0。
- `scripts/smoke/release-artifacts.sh` 对该归档退出 0；观察到
  `Config check passed`、`Build completed:` 与
  `Publish audit: routes=2 errors=0 warnings=22`。

### Published process-plugin

Echo 以 `Release`、`osx-arm64`、self-contained single-file 发布成功；可执行文件
SHA-256 为
`70663ca9d1ab12295bec44105650827edc8b493d15e8e1b8ecb239fd466feb10`。
临时 `plugin.yaml` 与 `.bukit/plugins.yaml` 使用 `apply_patch` 创建，并在任何
CLI 调用前写入该真实值。

- `plugin validate-config` 与 `plugin validate-manifest plugins/echo` 均退出 0、
  stderr 为空；实际成功行中的站点路径是 `/private/tmp/...`，不是 brief 要求的
  原 `/tmp/...` lexical 文本。控制器将验收纠正为
  `SITE_PROOF_ROOT="$(cd /tmp/bukit-g04d2b2-plugin-proof/site && pwd -P)"`；
  两份保留 stdout 与展开后的精确成功行比较均为 exit 0。
- `plugin list` 退出 0、stderr 为空，并输出
  `echo@1.0.0 enabled=true status=ok platform=osx-arm64 commands=echo`。
  `echo@1.0.0` 是成功 handshake identity 的间接证据，`commands=echo` 是成功
  runtime manifest 与 exposed-command selection 的间接证据；CLI 不暴露原始
  handshake/runtime-manifest envelope。
- `.bukit/plugins.lock.yaml` 存在，并包含 `protocol: bukit-plugin-v1`、
  `platform: osx-arm64`、entry
  `plugins/echo/bin/osx-arm64/bukit-plugin-echo`、上述精确 SHA 及
  `sha256Verified: true`。
- published `bukit echo hello` 退出 0，CLI stderr 为空。stdout 为有效 JSON，
  `arguments == ["hello"]`、`options == {}`；两个 context 路径的实际值均为
  `/private/tmp/bukit-g04d2b2-plugin-proof/site`。以 brief 的 `/tmp/...` 精确值运行
  `jq -e` 返回 exit 1；纠正后将两个值与展开的 `SITE_PROOF_ROOT` 精确比较，
  `jq -e` 返回 exit 0。该比较没有使用宽松的 `/tmp|/private/tmp` 正则。
- 只生成一份 `echo-invoke-*.json`。原 brief 中除 `stderr` 精确值外所列
  `pluginId`、version、operation、protocol、platform、command/commandPath、
  entry、两个 exit code、SHA verified、success、timeout/output-limit、非零
  stdout/stderr bytes 及完整 response summary 断言均由 `jq -e` 一次通过。
  报告的原始 `stderr` 是 `"bukit-plugin-echo handled invoke\n"`；原 brief
  遗漏 Echo `WriteLine` 的末尾 LF，因此无 LF 的精确断言返回 exit 1。控制器按
  独立复审建议将验收纠正为 `stderrBytes == 33` 且
  `stderr == "bukit-plugin-echo handled invoke\n"`；对保留的唯一原始报告重跑
  全部合并断言，`jq -e` 返回 exit 0。

## Task 2 复审发现与处置

初次自审发现两类验收契约差异：

1. macOS 将 `/tmp` 规范化为 `/private/tmp`，影响 Native AOT 最后输出行、
   两条 validate 成功行及 invoke context 的精确文本。实际文件身份、命令退出码、
   空 stderr 与 JSON 语义均通过。
2. Echo 使用行输出，execution report 保留末尾换行。除该字段外的全部报告断言
   通过。

原始 lexical/LF 断言的 exit 1 已保留在本账本，没有改写成历史通过。独立只读复审
为 0 Critical，并确认这是验收文本问题，不是产品资格失败；控制器据此只纠正
tracked implementation plan 与 ignored Task 2 brief。随后仅使用保留证据重跑
canonical-path identity 与精确 33-byte stderr 断言，全部 exit 0。没有重新构建
AOT、重新运行 smoke/publish/list/invoke，也没有修改 AOT script、Echo、process
runner、reporter、production、tests、fixtures、CI、release 或 gate。

## 最终资格状态

`qualification-complete`。Native AOT 构建、归档 smoke、process handshake、
runtime manifest、invoke、唯一 execution report、canonical physical-path
identity 与精确 Echo LF/33-byte 报告契约全部通过；原始验收失败、控制器纠正及
独立复审边界均已显式留痕。

## 排除项

不修改 schema、插件协议、配置语义、CLI 行为、错误字符串、权限语义、
`PluginProtocolClient`、其他 PluginHost 类型、CI/release/gate 或 protected
reference areas。
