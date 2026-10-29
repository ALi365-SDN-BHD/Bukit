# Core Stage 7 Golden Start Implementation Plan

> 本计划只覆盖阶段 7 的首批 golden；完整阶段出口仍以 `bukit-core-automated-test-plan-2026-10-24.zh-CN.md` 为准。

**Goal:** 对三个现有最小站点建立独立的公共产物字节基准，同时保留每次构建的独立报告校验。

**Architecture:** 复用 `SiteBuilder`、`OutputSnapshot` 和 `ReportAssertions`，不修改 Core、现有 fixture 或公开契约。每个 golden JSON 记录排序后的相对路径及原始文件字节（JSON 中的 base64），比较完整路径集合及每个文件的字节；报告不放入 golden。Linux 与 macOS 实测字节相同，复用 `unix/` 基准；Windows 使用原生采集的 `windows/` 基准，不对字节做换行归一化。

**Tech Stack:** .NET 10、xUnit、`System.Text.Json`；无新依赖。

## 首批步骤

1. 使用新 `Stage7GoldenTests` 为 `basic-markdown-site`、`i18n-site`、`taxonomy-site` 各运行一次真实构建；测试必须在报告校验后因 golden 不存在而 RED。
2. 将该次公共产物候选写至仓库外，核对路径、内容和三站点已有的语义断言，再将候选固定为 `tests/Bukit.Engine.Tests/Snapshots/Stage7/{unix,windows}/<fixture>.golden.json`。运行时不自动更新 golden。
3. 用相同工具链重新执行 `Stage7GoldenTests` 与其直接复用的完整 `TestInfrastructureTests`，核对 TRX 为零失败、零跳过；对 golden 路径和文件字节做负向校验。
4. 将命令、闭包、已执行结果和剩余缺口记录到主计划。稳定性 20 次、增量/全量、并发、聚合及三类故障注入仍需分别完成；首批绿色不等于阶段 7 完成。

本批完整专项命令（从仓库根运行，`R` 指向本批独立证据目录）：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter 'FullyQualifiedName~TestInfrastructureTests|FullyQualifiedName~Stage7GoldenTests' --logger 'trx;LogFileName=golden.trx' --results-directory "$R/golden"
```

三个站点构建独占执行。只固定实际比较所需数据；不追加 CI、全 Core 覆盖率、全解决方案或发布门禁。

## 首批执行记录（2026-10-29）

- macOS arm64：上面的完整专项为 **47 通过、0 失败、0 跳过**，其中 golden 4 项、基础设施 43 项。之后仅将 Windows golden 的外层 JSON 由 CRLF 改为 LF；测试读取的 Unix golden 和 Windows JSON 中编码的产物字节均未改变。
- [Linux / Windows 原生采集 #40809595691](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40809595691)：每端 **46/46**，仅为基础设施和候选采集，不是 golden 复验。
- [Linux / Windows golden 复验 #40811061112](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/40811061112)：证据提交 `b89239113b8ef9cf445a78660ab216a0be4bcc0f`，每端 **4/4** 实际 golden 测试通过；989 项输入指纹与主工作区一致，SDK 10.0.100、runtime 10.0.0。远端只运行 `FullyQualifiedName~Stage7GoldenTests`，确切命令及 TRX 在各平台 artifact 的 `command.json` 和 `stage7.trx`。
- 三站点分别有 12、28、33 个公共文件；Linux 与 macOS 的相同站点字节一致，Windows 分别有 10、23、31 个文件字节不同，但路径集合相同。报告由 `ReportAssertions.Generated` 独立校验，不写入 golden。
- 两次原生运行使用的临时工作流仅存在于证据分支；没有将其引入阶段 7 主工作区。首轮含环境变量值的远端 artifact 已删除，清理后的本地副本与本轮证据保存在仓库外的 `stage7-start-AjtnVD` 证据目录。本轮诊断文件的环境变量段只保留状态提示；命令和工具链诊断仍含运行器路径。

本批不等于阶段 7 出口：增量与全量等价、连续构建确定性、多语言根聚合、并发、三类故障注入及连续 20 次稳定性仍待验证。
