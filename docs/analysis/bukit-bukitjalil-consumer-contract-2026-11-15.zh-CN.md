# BukitJalil 行为契约测试：分析与建议（修订版）

- 日期：2026-11-15
- 状态：提案，未实施
- 范围：只改 Bukit 仓库；不改 BukitJalil，不新增 CI 门禁
- 修订：吸收 2026-11-15 复核意见，修正 BukitJalil 版本、CI 频率、SEO/发布审计/配置 schema 的读法、`--base-url` 的主题路径、BKT 行首范围和验证命令。

## 1. 结论

1. **Bukit 已有相当多防护**。`--secrets-stdin`、离线快照、BKT 码表、SEO schema、凭据代理在 Bukit 侧都有测试，其中一些是真实子进程/真实 push 测试。
2. **真正的问题是契约归属不清，加上 8 个端到端缺口**：原来的 6 个（version、render=3、`--base-url`、BKT 行首、`content.source.loaded`、SEO/GEO），加上复核发现的 `publish audit` 和 `config schema`。
3. **最小完整方案**：6 个新增测试 + 3 处现有断言强化 + 11 项契约映射。不新建测试项目、不新增 CI job；macOS/Windows 平台矩阵后置。

## 2. 现状核对

- BukitJalil 当前版本是 **0.1.0-beta.4**（`package.json`）；**2.2.3** 是它要求的最低 Bukit 版本 `MIN_BUKIT_VERSION`（`packages/server/src/bukit/install.ts:21`），也是它随包发布的 `BUKIT_VERSION`。下文不再把 2.2.3 当成 BukitJalil 的版本。
- BukitJalil 的 `ci.yml` 每次 push 都用锁定版本的真实 Bukit 在 ubuntu 和 macOS 上跑测试（`BUKIT_BIN` 由 fetch-bukit 设置，`bukit-behavior.test.ts` 里的 `describe.skipIf(!REAL_BIN)` 因此会执行）；`bukit-latest.yml` 只是每天额外对最新 release 跑一次。Windows 的 server 还没有移植（`ci.yml` 注释写明）。
- 版本检查的真实正则是 `^bukit\s+(\d+\.\d+\.\d+)`（`runner.ts:363-365`）；`locate.ts:43-52` 把同 minor 线以外的版本视为 `unsupported`，同 major 的旧 minor 才是 `old`。契约测试要钉格式；minor 线策略写进映射表，不由测试替代消费者决策。

Bukit 侧已有的相关测试：

| 行为 | 现有 Bukit 测试 | 性质 |
|---|---|---|
| 版本 | `ProgramEntryPointTests.Main_Version_PrintsVersionAndRuntime` | 只断言包含 `bukit `，没有 pin 格式 |
| 退出码 / cli-error | `ProgramEntryPointTests` 多处 0/1/2；`CliErrorRendererTests.RenderJson_Diagnostics_UsesDefaultSchemaAndExitCode` | cli-error 的 `{`/`}` 分行、schema 前缀、无尾随换行已由完整字符串比对钉住；没有 build render error = 3 的端到端测试 |
| `--base-url` | `SiteEngineLifecycleTests`、`I18nRegressionTests` 已有 `/docs/` 的端到端构建；`BuildPathUtilsTests` 有 `NormalizeBaseUrl` | Engine 层覆盖较强；缺口只在 CLI 参数到配置的绑定（`BuildCommand.cs:102`）和主题 `static/` 放在输出根 |
| `--secrets-stdin` | `SecretsStdinCliTests` | 真实 `dotnet bukit.dll` 子进程；非法 JSON、超限、不回显、多语言 worker 都有；`Program.cs` 在分派前统一处理，不分命令 |
| 离线快照 | `NotionOfflineSnapshotCommandTests`、`NotionOfflineSnapshotMultilingualTests`、`SiteEngineNotionSnapshotTests` | 成功路径、0B10/0B11/0B14、0B18/0B19、无 token、无请求、无路径泄漏 |
| BKT 码 | `DiagnosticCodeFormatterTests` 全部 18 个 Notion 码；CLI 侧 0B10/0B11/0B14/0B18/0B19 | 映射完整；check/doctor 的“行首”形状只用 `Contains` 断言过 |
| `content.source.loaded` | `SiteEngineNotionSnapshotTests` 只断言 `TestLogger.Infos` | 没有 CLI JSON 日志的流、`msg` 格式、`level`、count 测试 |
| SEO/GEO 报告 | `SeoAuditReportWriterTests`、`docs/schemas/seo-report.v1.schema.json`、`SiteEngineIntegrationTests` | schema 和字段很强；没有一条按 BukitJalil 的直接读法验证 starter 无 `site.url` 路径的测试 |
| publish audit | `PublishCommandTests`、`SeoReportValidatorTests`、`publish-audit-report.v1.schema.json` | 命令级有测试；没有真实 build 后按 deploy 消费方式读 `documents`/`issues` 的测试 |
| config schema | `ConfigCommandTests.Schema_WritesJsonSchemaToOutputFile` | 命令级断言 `$schema`/`site`/`content` 子串；没有顶层 CLI 后 `JSON.parse` 的测试 |
| 凭据代理 | `GitCredentialBrokerIntegrationTests`、`GitCredentialBrokerWindowsIntegrationTests`、`GitHubPagesDeployProviderTests` | 真实 push、真实进程链、环境无 token、helper 重试都有 |

本机用现有 Release 产物（报告 2.2.2）复核了关键形状：`{{ 1 + }}` 的模板解析错误在 JSON/文本模式都退出 3，这次样例的 JSON 码是 `BKT-0302`（换一种语法错误会是 `BKT-0399`，所以测试只 pin `BKT-03` 前缀）；命名主题下 `build --base-url /docs/` 输出 `/docs/assets/style.css`、`/docs/blog/`，`robots.txt` 在输出根且逐字节一致；`--log-format json` 的 `content.source.loaded` 在 stderr，stdout 为空；`config check`/`doctor` 的 0B10/0B11 在行首；`publish audit --dir` 在带 warning 的站点上退出 0；`.bukit/seo-report.json` 在 starter 无 `site.url` 时 `siteUrl` 为 null。

## 3. 契约清单（11 项）

| # | 外部契约 | BukitJalil 消费点 | 现有覆盖 | 缺口 / 动作 |
|---|---|---|---|---|
| 1 | `version` 输出匹配 `(?m)^bukit\s+\d+\.\d+\.\d+`；同 minor 线才默认可迁移 | `locate.ts`、`runner.ts:363` | 只测 `Contains("bukit ")` | 强化现有断言；映射表写 minor 线策略 |
| 2 | build 退出码 0/2/3/1；cli-error 是最后一份、`{`/`}` 各占一行、schema 以 `https://bukit.dev/schemas/cli-error` 开头 | `runner.ts:341-360`、`report-errors.ts` | 0/1/2 有测试；`CliErrorRendererTests` 完整输出比对 | 新增 render=3 测试；映射表登记 cli-error 格式 |
| 3 | `--base-url` 进入 `site.base_url`，`/docs/` 规范化为 `/docs`；命名主题的 `assets/`、`static/` 仍在输出根 | `bukit-behavior.test.ts:159-176`；starter 的 `base.html:1-11` | Engine 层 `/docs/` E2E、`NormalizeBaseUrl` | 新增命名主题 CLI 测试；只补绑定和主题 static |
| 4 | `--secrets-stdin` 的 JSON、退出 2、不回显；deploy 的 `--token-env` 先读 stdin | `runner.ts` 的 `STDIN_COMMANDS`；behavior 145-156 | `SecretsStdinCliTests`、`IsSupported`、provider 的 `SecretStore` 优先级 | 登记映射；完整 deploy 组合见 §7 残余风险 |
| 5 | 离线快照 + `BKT-0B10/0B11` + build 2 / check 1 / doctor 1；check/doctor 的码在行首 | `notion-snapshot.ts`；behavior 89-141 | `NotionOfflineSnapshotCommandTests`、`...Multilingual` | 强化 0B10/0B11 为精确行首断言 |
| 6 | `BKT-0Bxx` 码表与输出位置：build 用 cli-error `code`，check/doctor 用 `^(?:✖ )?\[BKT-0B..\]`，允许行尾 `\r` | `notion-errors.ts:20-70` | 码表 18 个；CLI 侧 `Contains` | 强化 0B10/0B11、0B18/0B19 为精确行首断言 |
| 7 | `event=content.source.loaded source=<name> count=<n>` 出现在 JSON 日志 `msg`；`level` 是大写 `Info`/`Warn`，消费端转小写比较；默认 `logging.level: info`（或更低）才输出 | `builds/service.ts:583-640` | 只有 Engine `TestLogger` | 新增 CLI JSON 日志测试；写明 info 前提 |
| 8 | 直接读 `.bukit/seo-report.json` / `geo-report.json`：`issues[]` 必填，`severity`/`code` 字符串，`route`/`message` 可空；`geoScore` 有限数字；starter 无 `site.url` 时 `siteUrl` 为 null，8 个 `SITE_URL_CODES` 告警被折叠 | `audit.ts:16-19,42-60` | schema、writer、Engine 集成 | 新增 SEO 测试：不跑 `seo audit`，不加 `site.url`，直接读文件 |
| 9 | `publish audit --dir` 退出 0；`.bukit/publish-audit-report.json` 的 `documents`、`issues` 可供 deploy 读取 | `deploys/service.ts:446-447`、`audit.ts:269-274` | `PublishCommandTests`、schema | 新增真实 build 后的消费者读法测试 |
| 10 | `config schema --output` 退出 0，输出文件可 `JSON.parse` | `ai-bukit.ts:297` | `ConfigCommandTests` 命令级 | 新增顶层 CLI + 有效 JSON 测试 |
| 11 | deploy token 走 stdin，git 子进程环境/命令行无 token，凭据代理可用 | BukitJalil 的 `docs/git-credential-broker.md`；provider/broker 测试 | `GitCredentialBrokerIntegrationTests`、Windows 测试 | 登记映射；文档漂移单列任务 |

## 4. 建议方案

### 4.1 放置位置与进程方式

新增 `tests/Bukit.Cli.Tests/Consumers/BukitJalilContractTests.cs`，不新建测试项目。

- **新测试用进程内入口调用**，复用/抽取 `ProgramEntryPointTests.InvokeEntryPointAsync` 的模式；它已经覆盖真实 `Program` 分派、配置加载、输出和退出码，比每个测试起子进程更省时间。子进程只保留给已有的 `SecretsStdinCliTests`（stdin）和 broker 集成测试。
- 新测试类标 `[Collection("Console")]`，因为它捕获 `Console.Out`/`Console.Error`，与 `ProgramEntryPointTests` 保持一致。`tests/Bukit.Cli.Tests/AssemblyInfo.cs` 已整体关闭并行，所以不会和同程序集测试竞争；仍不要依赖当前目录，`--config`、`--dir`、`config schema --output` 用绝对路径，build 的 `--output` 用相对路径 `out`（相对配置根），不要用绝对路径。
- **不新增 fixture 文件**：`--base-url` 的命名主题在临时目录里用现有 `basic-markdown-site` 的 source 文件搭建；这样 `codex-workflow.py closure` 不会因为 `tests/fixtures/` 新目录出现未映射文件。新 `.cs` 文件在 `tests/Bukit.Cli.Tests/Consumers/` 下，已被现有 `bukit-cli-tests` 规则（`tests/Bukit.Cli.Tests/**/*.cs`）覆盖。
- 文件头注明：契约对象是 BukitJalil 0.1.0-beta.4、`MIN_BUKIT_VERSION = 2.2.3`；改动这些行为要同时改 BukitJalil 的 `bukit-behavior.test.ts` 和 `MIN_BUKIT_VERSION`。

### 4.2 6 个新增测试

1. **`Build_ParseFailure_ReturnsExitCodeThree`**（新增）
   - 用 `{{ 1 + }}` 触发 Scriban 解析错误；
   - 断言退出 3，JSON 模式 stderr 的 cli-error `exitCode = 3`，`errors[0].code` 以 `BKT-03` 开头，且 `message` 非空。不要 pin `BKT-0302`：换一种语法错误会得到 `BKT-0399`，而 BukitJalil 只看退出码和 message，不解析 03xx 码。

2. **`Build_BaseUrl_NamedThemeNormalizesAndKeepsStaticAtRoot`**（新增）
   - 临时目录里复制 `basic-markdown-site` 的 `site.yaml`、`content/`、`layouts/`；
   - 搭一个命名主题：`theme.name: contract`、`theme.layouts: layouts`、`themes/contract/theme.yaml`、`themes/contract/static/robots.txt`、`themes/contract/assets/style.css`；
   - 模板模仿 starter：`{{ base_url = site.base_url }}`，链接 `{{ base_url }}/assets/style.css` 和 `{{ base_url }}/blog/`；
   - `build --base-url /docs/` 后断言：`href="/docs/assets/style.css"`、`href="/docs/blog/"`、不出现 `href="/assets/`；`out/robots.txt` 与主题源文件逐字节一致且在根。

3. **`Build_JsonLog_EmitsContentSourceLoadedLinePerSource`**（新增）
   - 同一站点，`build --log-format json`，不设置 `logging.level`（用默认 `info`）；
   - 逐行解析 stderr，找到 `msg` 匹配 `^event=content\.source\.loaded source=(.+?) count=(\d+)$`；
   - 断言 `source=pages`、`count=1`、`level` 是字符串且小写为 `info`；stdout 为空。

4. **`Build_SeoAndGeoReportsMatchConsumerSchema`**（新增）
   - **不要加 `site.url`**，也不要调用 `seo audit`/`geo audit`；BukitJalil 直接读 `.bukit/*.json`；
   - 直接读 `out/.bukit/seo-report.json`：`siteUrl` 为 null 或缺失；`issues` 是数组；每项 `severity`/`code` 是字符串，`route`/`message` 是字符串或 null；至少包含 `seo.canonical_not_absolute` 和 `publish.sitemap_missing_route` 两条 warning，证明 8 个 `SITE_URL_CODES` 的折叠分支有真实数据；
   - 直接读 `out/.bukit/geo-report.json`：`geoScore` 是有限数字；`llmsTxtGenerated`/`llmsFullTxtGenerated` 是布尔（若存在），`geoEnhancedCount` 是非负整数（若存在）。

5. **`PublishAudit_RealBuild_ReportMatchesDeployConsumerShape`**（新增）
   - 正常 build 后跑 `publish audit --dir out`，断言退出 0；
   - 直接读 `out/.bukit/publish-audit-report.json`：`documents` 是数组，`issues` 是数组；每项 issue 的 `severity`/`code` 是字符串，`route`/`message` 可空。BukitJalil 把 `documents` 当作 `routes` 传给 `summarizeSeo`，所以这两个字段是 deploy 的前置条件。

6. **`ConfigSchema_OutputIsJsonAtTopLevel`**（新增）
   - 跑 `config schema --output <tmp>/schema.json`，断言退出 0；
   - `JsonDocument.Parse` 成功，且包含 `$schema` 与 `properties`（或 `site`/`content`）。BukitJalil 只要求可解析、大小受限的 JSON。

### 4.3 强化 3 处现有断言

- `ProgramEntryPointTests.Main_Version_PrintsVersionAndRuntime`：`Assert.Matches(@"(?m)^bukit\s+\d+\.\d+\.\d+", result.StdOut)`。
- `NotionOfflineSnapshotCommandTests.ChangedDatabaseId_FailsConfigCheckDoctorAndBuildOffline` 和 `OptionMisuse_IsAnArgumentError`：把 `Contains("[BKT-0B11]")` / `Contains("[BKT-0B10]")` 换成 `Regex.IsMatch(..., @"(?m)^(?:✖ )?\[BKT-0B11\]")` / `...BKT-0B10...`。
- `NotionOfflineSnapshotCommandTests.Pin_MissingSourceOrInvalidFile_FailsWithItsCode`：把 0B18/0B19 的 `Contains` 换成精确行首断言。BukitJalil 也会按这两个码分类。

### 4.4 契约映射文档

在 `guide/dev/cli.md` 增加 “BukitJalil consumer contract” 一节，列出上面 11 项、对应测试、BukitJalil 0.1.0-beta.4 / `MIN_BUKIT_VERSION = 2.2.3`、以及变更规则：

> 修改表中任一行为，必须同时更新 BukitJalil 的 `bukit-behavior.test.ts`、`MIN_BUKIT_VERSION` 和本表；这不是内部重构。版本只在同 minor 线内默认可迁移，跨 minor 由 BukitJalil 决定。

映射表里同时注明两条已由现有测试钉住的细节：cli-error 的分行/前缀/最后一份规则（`CliErrorRendererTests`），以及 JSON 日志 `level` 的 `Info`/`Warn` 取值（消费端转小写）。

## 5. 为什么不采用其他方案

- **在 Bukit CI 里直接跑 BukitJalil 的 vitest**：跨仓库、Node/pnpm、release 下载、版本 pin 都会耦合进来；BukitJalil 自己改测试也会让 Bukit 红。拒绝。
- **新建 `Bukit.Contracts.Tests` 项目 + 新 CI job**：要改 slnx、coverage 矩阵和 `ci.yaml`，收益只是“名字更响”。先用现有项目，等确实需要 macOS/Windows 独立矩阵时再升级。推迟。
- **整段 stdout/stderr golden**：会把无关文案也 pin 住，PR 改一句提示就红。只 pin 消费者实际解析的字段和形状。
- **共享 JSON 契约文件 + 两边生成测试**：当前只有一个消费者，协调成本大于收益。拒绝。

## 6. 验证与验收

**验证以 `codex-workflow.py closure` 的输出为准**；带过滤的 `dotnet test` 只是开发时的快速反馈，不是最终 gate。

### 6.1 契约测试任务

显式列出本次改动文件，避免把工作区里其他任务的未跟踪文件带进来：

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed tests/Bukit.Cli.Tests/Consumers/BukitJalilContractTests.cs \
  --changed tests/Bukit.Cli.Tests/ProgramEntryPointTests.cs \
  --changed tests/Bukit.Cli.Tests/NotionOfflineSnapshotCommandTests.cs \
  --changed guide/dev/cli.md
```

2026-11-15 实测 closure 输出：

- `unmappedFiles: []`；`tests/Bukit.Cli.Tests/Consumers/*.cs` 命中 `bukit-cli-tests`。
- `publicContractFiles: ["guide/dev/cli.md"]`。`guide/dev/cli.md` 是公共契约文件，所以完整 CLI 测试和 `cli-docs-sync.sh` 是必须的。
- `specialtyTests`（按 closure 的分类执行）：
  - `bash guide/skills/scripts/validate-skills-strict.sh`（`static-parallel`）
  - `bash scripts/checks/cli-docs-sync.sh`（`static-parallel`）
  - `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj`（`dotnet-serial`）

注意最后一条是**不带过滤的完整 `Bukit.Cli.Tests`**。只跑 `--filter "FullyQualifiedName~BukitJalilContractTests"` 不会运行两个被强化的旧测试，不能作为完成证据。开发时可以先用：

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release \
  --filter "FullyQualifiedName~BukitJalilContractTests|FullyQualifiedName~ProgramEntryPointTests|FullyQualifiedName~NotionOfflineSnapshotCommandTests"
```

但它只是快速反馈，最终仍要跑 closure 的完整 `Bukit.Cli.Tests`。`docs-consistency.sh` 不是这个 closure 的强制项，除非任务另行要求。

如果确实要新增 fixture 文件，先补 policy 映射，否则不要建新 fixture 目录。这份分析文档本身没有 policy 映射（`docs/analysis/*` 不在规则里），应作为提案单独提交，不要列进实现 closure。

### 6.2 文档漂移任务

显式列出 4 处文档：

```bash
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed guide/dev/cli.md \
  --changed guide/user/12-cli-reference.md \
  --changed guide/user/13-deploy-github-pages.md \
  --changed guide/skills/bukit-cli-reference/SKILL.md
```

2026-11-15 实测 closure 输出：

- `unmappedFiles: []`。
- `publicContractFiles`：`guide/dev/cli.md`、`guide/skills/bukit-cli-reference/SKILL.md`、`guide/user/12-cli-reference.md`。
- `contractConsumers`：`tests/Bukit.Architecture.Tests/SeoGeoDocumentationContractTests.cs`。
- `specialtyTests`：`validate-skills-strict.sh`、`cli-docs-sync.sh`、完整 `Bukit.Architecture.Tests`、完整 `Bukit.Cli.Tests`、完整 `Bukit.Engine.Tests`。

`CliContractTests.CliReference_DocumentsExactlyTheRegisteredCommandTree` 已包含在完整 `Bukit.Cli.Tests` 里；`python3 scripts/checks/cli-docs/validate_docs.py` 是额外的快速检查，不是 closure 的替代。这个任务单独提交，不要和契约测试混在一起。

### 6.3 故意改坏检查与验收

每个新测试做一个“故意改坏”检查：临时把 `site.base_url` 改坏、把 `content.source.loaded` 改成纯文本、把 render 退出码改成 1、把 `.bukit/seo-report.json` 改名、把 `publish-audit-report.json` 的 `documents` 改成非数组、让 `config schema` 输出非 JSON，确认对应测试真的红，再恢复。

验收标准：

- 改坏 version 格式、render 退出码、`--base-url` 的主题路径、`content.source.loaded` 的 `msg`/`level`、SEO/GEO 报告字段、publish audit 的 `documents`/`issues`、config schema 的 JSON 输出，`Bukit.Cli.Tests` 在 PR 上失败；
- closure 的 `unmappedFiles` 为空，且 `specialtyTests` 全部通过；
- 不新增 CI job、不新增测试项目、不新增未映射 fixture；
- `--secrets-stdin`、离线快照、BKT 码表、凭据代理仍由现有测试保护，并在 11 项映射表中可见。

## 7. 风险与边界

- **PR 上只跑 Linux**：`coverage-projects` 在 ubuntu 上跑 `Bukit.Cli.Tests`；`platform-tests` 只跑写死的类列表（`ci.yaml:170`），不改 CI 时新类不会进 macOS/Windows。平台矩阵后置是有依据的：BukitJalil 每次 push 都在 macOS 上用锁定版本跑 behavior 测试；Windows 的 server 还没有移植。
- **测试时间**：新测试全部进程内，预计总共增加几秒；不要把子进程当成默认方式。真正需要 stdin 的 `--secrets-stdin` 和 broker 已有子进程测试。
- **契约会故意变红**：这是设计目标。BukitJalil 升级 `BUKIT_VERSION` 时，先让 Bukit 的契约测试通过，再改自己的 `MIN_BUKIT_VERSION`。
- **不是打包测试**：这些测试跑普通 `bukit.dll`，不覆盖 Native AOT/单文件发布的裁剪问题；发布包完整性仍由 release smoke 负责。
- **一个低风险的组合缺口**：`deploy --skip-build --secrets-stdin --token-env NAME` 这条完整 CLI 组合没有单独测试。两端都有覆盖：`SecretsStdinCliTests.IsSupported_LimitsFlagToTokenConsumingCommands` 证明 deploy 接受该参数，`GitHubPagesDeployProviderTests.DeployAsync_CustomTokenEnv_ResolvedFromSecretsStdinBeforeEnvironment` 证明 provider 先读 `SecretStore`。要钉死可加一个 Unix fake-git 子进程测试，但成本高于这 6 个，建议后置。

## 8. 附带发现：文档漂移（单列任务）

`v2.2.3` 已包含凭据代理（`GitHubPagesDeployProvider.cs:113` 和 `GitCredentialBroker` 代码），但以下文档仍写“token 通过 `BUKIT_GITHUB_TOKEN` + `GIT_ASKPASS` 进入 git 子进程环境”：

- `guide/dev/cli.md:54`
- `guide/user/12-cli-reference.md:102-105`
- `guide/user/13-deploy-github-pages.md:129-133`
- `guide/skills/bukit-cli-reference/SKILL.md:168`

另：`docs/git-credential-broker.md` 在 **BukitJalil 仓库**，不在 Bukit 仓库；Labs 的 `WebhookCommand` 里也有 `BUKIT_GITHUB_TOKEN`，但用途无关，不要动。

这个修复的验证范围不同，按 §6.2 的 closure 执行：4 个文档文件会被归为 `publicContractFiles`，closure 要求完整的 `Bukit.Architecture.Tests`、`Bukit.Cli.Tests`、`Bukit.Engine.Tests`，加上 `validate-skills-strict.sh` 和 `cli-docs-sync.sh`。`CliContractTests.CliReference_DocumentsExactlyTheRegisteredCommandTree` 已包含在完整 CLI 测试里；`validate_docs.py` 只是额外的快速检查，不能替代 closure。建议单独提交、单独任务。

## 9. 需要确认的决策

1. 是否同意“6 个新增测试 + 3 处强化 + 11 项映射”，而不是新建完整契约项目。
2. 是否同意契约测试放在 `Bukit.Cli.Tests`、用进程内入口，PR 上 Linux 运行；macOS/Windows 平台矩阵后置。
3. 是否同意把 4 处凭据代理文档漂移单列成任务、单独提交。
