# `site.languages` 作为唯一语言配置：实施方案与任务计划

- 状态：**待实施**。决策 D1–D6 已于 2026-11-14 确认；实施须作为单独的 Core 任务，在用户明确开始后进行。
- 日期：2026-11-14
- 依据：规范性提案 `openspec/changes/unify-site-languages/`（proposal、design、specs、tasks）
- 代码基线：`38b32568`（main，本计划写作时）
- 范围：只覆盖 Core、Labs、Plugins 的实现、测试和文档。commit、push、PR、发布、full/release gate 均需另行授权。

---

## 0. 先读这一段

1. **先让语言推导可靠，再改模式分界。** 阶段 1 让单项 `languages` 的配置总能得到正确的 `Site.Language`（配置加载器推导一次，引擎入口对代码构造的配置再规范化一次），此时 `GetLanguages` 还没改，单项仍是多语言模式，页面输出不变。阶段 2 再把多语言分界改为"至少 2 项"。顺序反过来会让 `Site with { Languages = ["en"] }` 这类配置按 `zh-CN` 过滤，丢掉全部英文页面（design.md 实现方式第 2 条）。
2. **先改写出配置的工具，再拒绝旧字段。** 阶段 3 让 Labs、Importing 生成 `languages: [x]`（阶段 2 之后它已经是单语言站点，可直接构建）；阶段 4 才让 `site.language` 报错，并在同一任务内迁移全部测试站点，保证每次提交测试都是绿的。
3. **D4（`defaultLanguageInSubdir` 默认改为 `false`）放在最后，单独一个阶段、一个提交。** 它改变所有多语言站点的默认布局，影响的测试最多；被否决或需要回退时，只撤这一个阶段，连同规范里随 D4 修改的三条需求。
4. **任务按严格顺序排列**，同一时刻只有一个实施者写仓库（AGENTS.md 单写者规则）。只读调查（调用方检索、测试清单）可以并行。
5. 每个任务都应能单独编译、通过所列测试。提交边界按阶段划分（提交需授权）：阶段 0、1、3、5、7 每个任务可单独提交；**阶段 2（T02.1–T02.6）、阶段 4（T04.1–T04.6）、阶段 6（T06.1–T06.4）各自只能作为一个提交**，因为阶段内前面的任务会让后面任务才修复的测试暂时变红。这三个阶段内仍按任务顺序执行和记录，只在阶段末提交。

## 1. 约束与执行方式

- 开工前：`python3 scripts/checks/codex-workflow.py closure` 生成验证闭包。按本计划的文件试算，测试站点的 `site.yaml`（T04.6、T06.3 要改）是**未映射文件**，需在 T00.2 补映射并跑 `scripts/checks/codex-workflow-self-test.sh`。用 `codex-workflow.py classify` 给闭包路径和命令分类：按现有 `resourceRules`，`dotnet test` 命令都归为 `dotnet-serial`；Stage7 快照和测试站点相关测试仍选择单独运行；`dotnet test` 串行；静态检查可并行。再用 `codex-workflow.py queue init` 建立任务队列，实施者取得写者槽位后才写文件。
- 测试：每个任务只跑列出的专项测试，命令格式为 `dotnet test tests/<项目> -c Release --filter "<过滤>"`，过滤写成 `FullyQualifiedName~A|FullyQualifiedName~B`，下文简写为 `A|B`。每个阶段结束时跑一次该阶段涉及的完整测试项目（闭包给出的专项测试）。过滤条件必须能选中测试：新加过滤先用 `--list-tests` 确认非空，避免 0 个测试也显示通过。不跑 `scripts/test-all.sh`、`scripts/smoke-all.sh`、整个解决方案的测试和 full/release gate，除非用户明确授权。
- 证据：GREEN 结果用 `codex-workflow.py cache record` 缓存；`HEAD`、闭包、命令、环境、SDK 不变时复用。
- 审查：每个阶段实施并测完后做一次专项审查，只有严重或重要问题才回到实施。全部阶段完成后生成一次 `review-scope`，做一次只看增量的统一审查。
- 公共 API：`SiteConfig.Language` 保留为公开 init 属性，只是不再从 YAML 读取；`I18nLanguagePolicy` 是 internal。预计公共 API 基线不变，但阶段 1、2、4、6 结束时都跑 `bash scripts/checks/public-api-drift.sh check` 确认；如有漂移，在引入它的任务内更新 `docs/governance/bukit-core-public-api-baseline.v1.json`。
- 记录：各阶段耗时、缓存命中、重跑、写者冲突用 `codex-workflow.py metrics add` 记录。

## 2. 总体顺序

| 阶段 | 内容 | 对外行为 | 任务 |
|---|---|---|---|
| 0 | 准备：闭包、队列、基线测试、锁定单语言行为 | 不变 | T00.1–T00.4 |
| 1 | 语言推导 | 页面不变（`metrics.json` 的 `site.language` 变为站点的默认语言） | T01.1–T01.3 |
| 2 | 多语言分界改为至少 2 项；SDK 改用 `languages` | **单项 `languages` 改为单语言输出** | T02.1–T02.6 |
| 3 | Labs、Importing 改为写出 `languages` | 生成的 `site.yaml` 改用 `languages` | T03.1–T03.3 |
| 4 | 配置层：删除 `site.language`、默认 `[zh-CN]` | **`site.language` 报错** | T04.1–T04.6 |
| 5 | D5 搜索回退语言 | 单语言记录 `und` → 站点语言 | T05.1 |
| 6 | D4 `defaultLanguageInSubdir` 默认 `false` | **多语言默认根目录布局** | T06.1–T06.4 |
| 7 | 文档与 CHANGELOG | — | T07.1–T07.4 |
| 8 | 验证与统一审查 | — | T08.1–T08.3 |

## 3. 任务清单

格式：**编号 名称** — 改动 / 测试与完成标准 / 依赖。源码路径以 `src/` 为根，测试以 `tests/` 为根。

### 阶段 0：准备

**T00.1 基线测试取证**
- 做法：在未改动的 main 上跑本计划涉及的专项测试并缓存证据，作为对照。
- 测试：
  - `Bukit.Engine.Tests`：`I18n|SearchIndex|MetricsWriter|IncrementalEngineIdentity|RoutePipeline|CanonicalContentValidator|DataFilesPlugin|SiteEngineIntegration|SiteEngineLogger|ConfigValidator|ContentPipeline|ReportSchemaContract|Stage7Golden`（`SiteEngineLifecycleTests.cs` 是 `SiteEngineIntegrationTests` 的 partial 文件，按类名过滤要用 `SiteEngineIntegration`）
  - `Bukit.Config.Tests`：`ConfigLoader|StrictYamlContract|ConfigJsonSchemaGenerator|ConfigPhase2Validator`
  - `Bukit.Content.Tests`：`MarkdownFolderProvider`
  - `Bukit.Cli.Tests`：`WorktreeBuildCoordinator|DoctorCommand`
  - `Bukit.Labs.Cli.Tests`：`Intent|StarterTheme`
  - `Bukit.Importing.Tests`：`HtmlDemoImporter`（`SiteConfigGenerator` 没有独立测试类，由 `HtmlDemoImporterTests` 覆盖）
- 完成标准：全部通过，`cache record` 记录。
- 依赖：无。

**T00.2 验证闭包**
- 做法：用阶段 1–6 的文件生成闭包。为未映射的测试站点 `site.yaml`（`tests/fixtures/*-site/site.yaml`、`tests/Bukit.Engine.Tests/Fixtures/SrbizTask4/sites/wp2-task4-trust-fixture/site.yaml`）、Stage7 快照（`tests/Bukit.Engine.Tests/Snapshots/Stage7/{unix,windows}/*.golden.json`）和新增测试文件在 `scripts/checks/codex-workflow-policy.v1.json` 补映射，跑 owner 自测 `scripts/checks/codex-workflow-self-test.sh`；再用 `classify` 记录哪些命令是 `fixture-exclusive`。
- 完成标准：`closure` 无未映射文件。
- 依赖：T00.1。

**T00.3 任务队列**
- 做法：`queue init` 写入 T00.4–T08.3；实施者 acquire 后进入 `writing`。
- 依赖：T00.2。

**T00.4 锁定现在单语言站点的行为**
- 做法：新增测试（放在 `tests/Bukit.Engine.Tests/SingleLanguageSiteTests.cs`），在**当前代码**上用"不写 `languages`、写 `language: en`、设置 `site.url`"的站点断言：首页写在 `dist/index.html`；没有 `dist/en/index.html` 和跳转页；页面没有 hreflang（前瞻性锁定：`SeoAlternatesService.cs:79` 本来就跳过少于 2 种语言的分组）；模板 `site.language` 为 `en`；`content/en/a/foo.md` 与 `content/en/b/foo.md` 这对**同一语言目录下不同子目录的同名文件**的 slug（现在是 `en/a/foo`、`en/b/foo`；如果把 `Languages = [en]` 传给 `MarkdownFolderProvider` 会变成 `a/foo`、`b/foo`，见 `MarkdownFolderProvider.cs:117-155,215-226`）。这些断言在阶段 2 改用 `languages: [en]` 写法后，结果必须完全相同。
- 测试：新测试在当前代码上通过。
- 依赖：T00.3。

**阶段 0 审查**：T00.4 的断言是否覆盖 proposal 里"产物与现在的单语言模式相同"的全部内容（输出路径、hreflang、跳转页、模板语言、slug）。

### 阶段 1：语言推导（输出不变）

**T01.1 配置加载器推导语言**
- 改动：`Bukit-Core/Bukit.Config/ConfigLoader.cs` 在读完 `site` 后，若写了 `languages`，把 `SiteConfig.Language` 设为站点的默认语言：`defaultLanguage` 有值时取它，否则取清理（去空白、去重）后的第一项；只有 1 项时就是这一项。此时 `language` 字段仍会读取，但写了 `languages` 时以 `languages` 为准（写了 `languages` 的站点现在都是多语言模式，顶层 `language` 本来就不影响页面）。这样 `metrics.json` 的 `site.language` 和合并审计的回退语言都变为站点真实的默认语言，不再是没用的 `zh-CN` 默认值（proposal 的 Why 一节）。这样 `ConfigLoader.Load` 的所有调用方（含 Labs `DataCommand.cs:174-179` 走的公开 `ContentPipeline`）都拿到正确语言。
- 测试：`ConfigLoaderTests`：`languages: [en]` 加载后 `Language == "en"`；同时写 `language: zh-CN` 时仍为 `en`；`[en, fr]` 为 `en`；`[en, fr]` 加 `defaultLanguage: fr` 为 `fr`；不写 `languages` 时 `Language` 仍取 `language`（阶段 4 才删除）。另跑 `I18nRegressionTests`，确认带前缀布局下合并审计的回退语言改为默认语言后结果正确。
- 依赖：T00.4。

**T01.2 引擎规范化代码构造的配置**
- 改动：在 `Bukit-Core/Bukit.Engine/I18nLanguagePolicy.cs` 新增 internal `NormalizeSite(SiteConfig site)`：写了 `languages` 时返回 `site with { Language = 默认语言 }`（规则同 T01.1），否则原样返回。在两个入口调用：`SiteEngine.BuildCoreAsync`（`SiteEngine.cs:142`）开头，先规范化传入的 `config`，之后 `BuildPlanner.Plan` 里 `ConfigApplier.Apply` 产生的 `EffectiveConfig` 自然继承（`ConfigApplier` 不碰语言字段）；两个公开 `BuildAsync` 重载都经过它。以及公开的 `ContentPipeline.ExecuteAsync`（`ContentPipeline.cs:42-56`）。直接调用公开 `DefaultContentProviderFactory` / `IContentProviderFactory` 且未规范化的代码仍会读到原始 `site.Language`，属于已知边界，在 XML 注释中说明。`I18nWorktreeBuild`、`WorktreeBuildRun` 只在 2 种及以上语言时运行，不需要规范化；`DoctorCommand` 读的是 `ConfigLoader` 的结果，已由 T01.1 覆盖。之后读取 `Site.Language` 的位置（`RoutePipeline`、`RouteInventoryValidator`、SEO、sitemap、feed、审计、投影等）都拿到正确值。
- 说明：阶段 2 之后 `GetDefaultLanguage(site, languages)` 收到的列表对单项站点为空，会返回 `site.Language`，依赖的正是这里的规范化，不另读 `site.Languages`。
- 测试：新增 `I18nLanguagePolicyTests`：单项、2 项、2 项加 `defaultLanguage`、空、全空白、重复项的 `NormalizeSite`；`ContentPipeline` 用 `Site with { Languages = ["en"] }`（不经过加载器）构造时按 `en` 过滤。
- 依赖：T01.1。

**T01.3 输出不变确认与阶段测试**
- 做法：此时 `GetLanguages` 仍是"有项就多语言"，单项站点仍按多语言输出，顶层 `Site.Language` 在多语言构建里不影响页面（`SiteEngine.cs:351` 为每种语言重设），所以页面输出不变。写了 `languages` 的站点 `metrics.json` 的 `site.language` 变为默认语言，在 `MetricsWriterTests` 中更新断言并注明原因。
- 测试：`bash scripts/checks/public-api-drift.sh check`；`Bukit.Config.Tests`、`Bukit.Engine.Tests`、`Bukit.Cli.Tests` 完整项目。
- 依赖：T01.2。

**阶段 1 审查**：全仓检索 `ConfigLoader.Load`、`new SiteEngine`、`ContentPipeline`、`new SiteConfig` 的生产调用方，确认没有绕过 T01.1、T01.2 而读到未推导 `Site.Language` 的路径。

### 阶段 2：多语言分界改为至少 2 项

**T02.1 `GetLanguages` 少于 2 项返回空**
- 改动：`I18nLanguagePolicy.GetLanguages` 去重后少于 2 项时返回空列表。`BuildCommand` 的 worktree 判断已是 `Count > 1`，不改。同一任务内改 `Bukit.Engine/ContentProviderFactory.cs:59`：传给 `MarkdownFolderProvider` 的 `Languages` 改为 `GetLanguages` 的结果（单语言时为空，与现在单语言模式传 `null` 等价），这样 `content/<lang>/` 不再参与单语言站点的 slug 区分，slug 与现在一致。同一任务内把 SDK 的 `Bukit-Core/Bukit.Engine/BuildOptionsMapper.cs:15` 写死的 `Language = "en"` 改为 `Languages = ["en"]`，语言由 T01.2 推导；这一步不能放在阶段 1，因为那时单项仍是多语言模式，SDK 输出会搬到 `<out>/en/` 并多出跳转页。
- 测试：`I18nLanguagePolicyTests` 增加单项返回空；T00.4 的测试改用 `languages: [en]` 写法后原样通过（含同名文件 slug）；`BuildOptionsMapperTests.cs:30` 改为断言 `Languages == ["en"]`；新增 SDK 构建测试：`BuildAsync(provider, options)` 的输出仍在根目录、没有跳转页。
- 依赖：T01.3。

**T02.2 逐一核实调用方**
- 做法：逐个确认以下 20 个直接调用 `GetLanguages` / `IsMultilingual` 的文件在单项时走单语言分支且结果正确（经由 `I18nOutputLayout.For/Create` 间接使用的文件，如 `SeoAlternatesService`、`WorktreeBuildPaths`、`BuildPlanner`、`I18nLegacyRedirectDeriver`、`I18nReservedPrefixGuard`、`I18nRootOutputConflictGuard`、`I18nRootLlmsWriter`、`I18nMergedVariantState`、`I18nWorktreeProtocol`、`SearchIndexPlugin`、`SitemapPlugin`、`PublishProjectionContract`、`DoctorCommand`，随布局自动正确，只在核实记录里列出）：`Bukit.Cli/Commands/BuildCommand.cs`、`WorktreeBuildRun.cs`；`Bukit.Engine/` 下的 `BuildReportInventories`、`BuildTransaction`、`CanonicalContentValidator`（改为全站 id 唯一，与现在单语言一致）、`ContentProviderFactory`、`I18nOutputLayout`、`I18nOutputMerger`、`I18nRootRedirectWriter`、`I18nRootRobotsWriter`、`I18nRootSitemapWriter`、`I18nWorktreeBuild`、`Plugins/BuiltIn/DataFilesPlugin`（`data/<lang>/` 不再当语言目录）、`PublicOutputLifecycle`、`PublishAggregateProjectionWriters`、`RouteInventoryValidator`、`RoutePipeline`、`SearchIndexBuilder`、`SiteEngine`、`VariantReportStage`。只改结果不对的调用方。
- 测试：每改一处补对应单测；`Bukit.Engine.Tests` 过滤 `I18n|RoutePipeline|CanonicalContentValidator|DataFilesPlugin|BuildTransaction|BuildReport`。
- 依赖：T02.1。

**T02.3 直接读取 `site.Languages` 的位置**
- 规则（`metrics.json`）：`site.languages` 记录配置里的语言列表原值，`site.language` 记录推导后的站点语言。阶段 4 之前不写 `languages` 时仍为 `null`；T04.1 把默认值写回后变为 `["zh-CN"]`。`docs/schemas/metrics.v2.schema.json` 的该字段本来就是 `array|null`，不改 schema。
- 改动：
  - `Bukit.Engine/MetricsWriter.cs:63,70`：按上面的规则保持读取 `Site.Languages`，不改代码，只在测试中固定单项站点的两个字段。
  - `Bukit.Engine/Incremental/SiteIdentityAndModesContributor.cs:16,21`：已经同时哈希 `site.language` 和 `site.languages` 原值，规范化之后单项与多项、不同单项之间的哈希都不同，不改代码，只补测试。
- 测试：`MetricsWriterTests`、`IncrementalEngineIdentityTests`。
- 依赖：T02.2。

**T02.4 布局类配置在单项时被忽略**
- 改动：确认 `WarnLayoutSettingsWithoutLanguages`、`WarnIgnoredLegacyRedirects` 已经经由 `GetLanguages` 判断，单项时自动视为"没有多语言"，给出现有警告。
- 测试：`SiteEngineLoggerTests` 新增：`languages: [en]` 且 `defaultLanguageInSubdir: false` 时记录 `i18n.config.default_language_layout_ignored`，输出在根目录。
- 依赖：T02.3。

**T02.5 改写单项 `Languages` 的现有测试**
- 改动：以下测试用单项列表构造配置，原意是测多语言，改为 2 项；原意是测单语言的保持并更新预期：`Bukit.Engine.Tests/DataFilesPluginTests.cs:88,119,282`、`SiteEngineLifecycleTests.cs`、`I18nRootLayoutTests.cs:642`、`I18nOutputMergerTests.cs:325`（直接调用 `WarnDocumentLanguageIssues`，单项时重复 key 警告不再出现）；`Bukit.Cli.Tests/WorktreeBuildCoordinatorTests.cs:36-46`（`ExplicitSingleLanguageBuild_DoesNotRequireGit` 用字符串替换写入 `languages: [en]` 并断言 `dist/en/index.html`，改为断言根目录输出）。`Bukit.Content.Tests/MarkdownFolderProviderTests.cs:1178,1225` 直接设置 provider 选项，不受 `GetLanguages` 影响，不改。实施前再检索一次，除 C# `Languages = [...]` 外还要检索 yaml 字符串里的 `languages: [`。
- 测试：上述测试类（`SiteEngineLifecycleTests.cs` 用过滤 `SiteEngineIntegration`）。
- 依赖：T02.4。

**T02.6 阶段测试**
- 测试：`Bukit.Engine.Tests`、`Bukit.Content.Tests`、`Bukit.Cli.Tests` 完整项目；`public-api-drift.sh check`。
- 依赖：T02.5。

**阶段 2 审查**：20 个调用方的核实记录；单项站点与 T00.4 的产物逐项一致；增量哈希在单项与多项之间切换时触发全量重建。注意：阶段 2 到 T04.5 之间，`languages: [en]` 加 `defaultLanguage: en` 的站点每次构建都会记录 `i18n.config.default_language_ignored`；阶段 2 到阶段 6 之间，单项站点设 `defaultLanguageInSubdir: false` 才警告（规范的最终状态是设 `true` 才警告），都属于预期的中间状态。

### 阶段 3：Labs、Importing 改为写出 `languages`

**T03.1 Labs init 与 clone-fidelity**
- 改动：`Bukit-Labs/Bukit.Labs.Cli/Commands/Theme/InitCommand.cs:312,351` 与 `Commands/Clone/CloneFidelityRunner.cs:79` 的 `language: zh-CN` 改为 `languages: [zh-CN]`。
- 测试：`Bukit.Labs.Cli.Tests` 过滤 `StarterTheme|Init|CloneWorkflow`（`CloneFidelity` 没有独立测试类）；生成的 `site.yaml` 能被 `ConfigLoader` 加载并构建为单语言站点。
- 依赖：T02.6。

**T03.2 Labs intent**
- 决定（用户已于 2026-11-14 确认）：与 D2 一致，intent 文件删除 `site.language` 键，单语言 intent 写 `languages.supported: [x]`。
- 改动：
  - `Commands/Intent/IntentLoader.cs:39`：不再读取 `site.language`；出现时报错，提示改为 `languages.supported: [<code>]`。
  - `Commands/Intent/IntentLoader.cs`、`IntentValidator.cs:37-39`：与 D6 一致，`languages.supported` 只有 1 项时 `languages.default` 可以省略，省略时取这一项；写了必须等于这一项（现有"default 必须在 supported 中"规则保证）。2 项及以上时 `default` 仍必填，规则不变。
  - `Commands/Intent/IntentWizard.cs:22-26,91`：单语言分支的提问 `Ask("site.language", ...)` 和写出位置改为写 `languages.supported: [x]`，不再写 `site.language`。
  - `IntentLoader.cs` 约第 45 行 `GetRequiredString(languagesNode, "default")` 改为可选；`IntentValidator.cs:58` 的 `Contains(Default)` 改为对解析后的默认语言判断。
  - `Commands/Intent/IntentApplier.cs:101-110,163-170`：不再写 `Language`，删除 `else if (intent.Site.Language …)` 分支；只写 `languages` 和（多语言时）`defaultLanguage`。intent 没有 `languages` 时不写，站点按 D1 取 `[zh-CN]`。
  - `SiteIntentSite.Language` 属性随之删除（Labs 内部模型，不在 Core 公共 API 基线中，实施时确认）。
- 测试：`IntentApplierTests`、`IntentValidatorTests`、`IntentCommandTests`：旧键报错；单项省略 `default`；单项 `default` 不匹配报错；2 项缺 `default` 报错；生成的 `site.yaml` 只有 `languages`。
- 依赖：T03.1。

**T03.3 Importing**
- 改动：`Bukit-Plugins/Bukit.Importing/SiteConfigGenerator.cs:48` 写 `languages: [<code>]`；`ResolveLanguage`、`ValidateLanguageOption` 保留。
- 测试：`Bukit.Importing.Tests` 过滤 `HtmlDemoImporter`（`HtmlDemoImporterTests.cs:1029` 断言 `language: en`，改为 `languages: [en]`）；`Bukit.Plugin.Import.Tests` 完整项目。
- 依赖：T03.2。

**阶段 3 审查**：所有生成 `site.yaml` 的代码路径都已改写（全仓检索 `"language"`、`language: ` 的生产代码）。WechatSync 只确认内容投影清单的 `language` 照常填写，不改代码。

### 阶段 4：配置层

**T04.1 读取与默认值**
- 改动：`Bukit-Core/Bukit.Config/ConfigLoader.cs:68` 不再读取 `language`。`languages` 缺失或为空列表时为 `[zh-CN]`，写回 `SiteConfig.Languages`（`metrics.json` 按 T02.3 的规则记录）；`SiteConfig.Language` 继续按 T01.1 推导为默认语言（只有 1 项时就是这一项）。
- 测试：`ConfigLoaderTests`：缺失、空列表、单项、多项时的 `Languages` 和 `Language`；`ConfigLoaderFullCoverageTests.cs:63-79`（`Load_SiteLanguages_EmptyList_ReturnsNull`）按新规则改为返回 `["zh-CN"]`。
- 依赖：T03.3。

**T04.2 旧字段报错**
- 改动：`Bukit.Config/ConfigStrictFieldValidator.cs:12` 移除 `language`；遇到 `site.language` 时给出专门报错：`site.language was removed; write languages: [<code>] instead.`（措辞实施时可调整，须点名 `languages: [<code>]`）。
- 测试：`StrictYamlContractTests`：旧字段报错且信息包含替代写法。
- 依赖：T04.1。

**T04.3 校验**
- 改动：`Bukit.Config/SiteConfigValidator.cs:78-81` 删除单语言 `site.language` 规则；`languages` 现有规则不变（全空白报错、格式、重复、`defaultLanguage` 必须在列表中）。
- 测试：`ConfigValidatorTests`：单项时 `defaultLanguage` 不写通过、等于该项通过、不等于该项报错。
- 依赖：T04.2。

**T04.4 JSON Schema**
- 改动：`Bukit.Config/ConfigJsonSchemaGenerator.cs:63` 移除 `language`；`languages` 说明默认 `[zh-CN]`。
- 测试：`ConfigJsonSchemaGeneratorTests`。
- 依赖：T04.3。

**T04.5 删除 `default_language_ignored` 警告**
- 改动：`Bukit.Engine/I18nLanguagePolicy.cs` 的 `WarnDocumentLanguageIssues` 删除 `i18n.config.default_language_ignored` 分支（`languages` 总有值，单项时 `defaultLanguage` 由校验约束）。
- 测试：`I18nOutputMergerTests.cs:294-304`（`WarnDocumentLanguageIssues_WarnsWhenDefaultLanguageHasNoLanguages`）改为断言不再出现该警告。
- 依赖：T04.4。

**T04.6 迁移测试站点和内嵌配置（与 T04.2 同一提交）**
- 改动：10 个 yaml 测试站点（`tests/fixtures/{basic-markdown,component-validation,dotfile-leak,incremental,output-safety,plugin-policy,route-security,safe-url-content,taxonomy}-site/site.yaml`、`tests/Bukit.Engine.Tests/Fixtures/SrbizTask4/sites/wp2-task4-trust-fixture/site.yaml`）和测试代码里内嵌的 `language:`（约 53 处）改为 `languages: [x]`。C# 里约 112 处 `Language = "..."` 逐一区分：配置对象 `SiteConfig` 改为 `Languages = [...]`，渲染模型 `SiteModel` 不变。
- 说明：T04.1 一落地，写 `language: en` 的测试站点会按 `zh-CN` 构建、英文内容被过滤；T04.2 一落地，它们会加载失败。所以 T04.1–T04.6 合成一个提交；任务仍分开执行和记录。
- 测试：`Bukit.Config.Tests`、`Bukit.Engine.Tests`、`Bukit.Content.Tests`、`Bukit.Cli.Tests`、`Bukit.Labs.Cli.Tests`、`Bukit.Importing.Tests` 完整项目；`public-api-drift.sh check`。
- 依赖：T04.5。

**阶段 4 审查**：全仓（含 `guide/`、`docs/` 以外的测试和工具）不再有用户配置层面的 `site.language`；报错信息可操作。

### 阶段 5：D5 搜索回退语言

**T05.1 `RecordLanguageFallback`**
- 改动：`Bukit-Core/Bukit.Engine/SearchIndexBuilder.cs:20` 单语言时返回推导后的站点语言，不再返回 `und`。调用方 `SearchIndexPlugin.cs:119`、`PublishAggregateProjectionWriters.cs:154` 不改。
- 测试：`Bukit.Engine.Tests` 过滤 `SearchIndex|I18nMergedSearchIndex|Stage7Golden`；新增规范场景"Single-language search record language"。
- 快照：`tests/Bukit.Engine.Tests/Snapshots/Stage7/{unix,windows}/` 中 `basic-markdown-site.golden.json`（`search.json` 两条记录为 `"language":"und"`）和 `taxonomy-site.golden.json` 等单语言快照要更新。仓库没有自动重新生成快照的机制：用临时脚本（放在 `$TMPDIR`）解码 base64 条目，只改 `search.json` 的 `language`，再编码写回，unix 和 windows 两份做同样修改；windows 份由 CI 的 Windows job 验证。Stage7 属于 `fixture-exclusive`，单独运行。
- 依赖：T04.6。

**阶段 5 审查**：`search.json` 的消费者（starter theme 搜索页、`bukit-search.html`）对单语言站点的语言过滤是否仍正确。

### 阶段 6：D4 `defaultLanguageInSubdir` 默认改为 `false`（单独提交，可整体回退）

**T06.1 默认值**
- 改动：`Bukit.Config/AppConfig.cs` 中 `DefaultLanguageInSubdir` 默认 `false`；`ConfigLoader` 缺省值、JSON Schema 默认值同步。
- 测试：`ConfigLoaderTests`、`ConfigJsonSchemaGeneratorTests`。
- 依赖：T05.1。

**T06.2 警告与哈希条件**
- 改动：`I18nLanguagePolicy.WarnLayoutSettingsWithoutLanguages` 的"非默认"判断改为 `DefaultLanguageInSubdir == true`（否则每个单语言站点每次构建都警告）；`WarnIgnoredLegacyRedirects` 不变。`SiteIdentityAndModesContributor.cs:23-26` 改为只在多语言且为 `true` 时计入哈希。
- 测试：`SiteEngineLoggerTests`：单语言默认不警告、设 `true` 警告；`SiteEngineLoggerTests.cs:98-114`（`DefaultLanguageLayoutWithoutLanguages_WarnsAndOutputIsUnchanged`，现为 `false` 警告）和 T02.4 新增的用例改为 `true` 警告；`IncrementalEngineIdentityTests`：单语言站点和未设置该项的多语言站点不把它计入哈希。
- 依赖：T06.1。

**T06.3 更新依赖默认带前缀的测试**
- 改动：以下多语言测试没有显式设置 `defaultLanguageInSubdir`，需要逐一判断：要测带前缀布局的显式写 `true`，否则更新为根目录预期。`Bukit.Engine.Tests`：`I18nMergedFeedProjectionTests`、`I18nMergedSitemapCollectionTests`、`I18nOutputMergerTests`、`I18nRootRedirectTests`、`RenderDependencyContributorInvalidationTests`、`RenderDependencyHasherTests`、`DataFilesPluginTests`、`ConfigValidatorTests`、`ReportStageTimingTests`、`BuildTransactionTests`、`SiteEngineLifecycleTests`（过滤 `SiteEngineIntegration`）、`IncrementalRenderReadsTests`、`CanonicalContentValidatorTests`、`SiteEngineIntegrationTests`；`Bukit.Config.Tests/ConfigLoaderTests`；`Bukit.Cli.Tests/DoctorCommandTests`、`WorktreeBuildCoordinatorTests`（第 111、288、870-871、882、1040 行断言默认带前缀）；确认 `ReportSchemaContractTests.cs:27`（`dist/en/agent-manifest.json`）是否依赖默认值；`Bukit.Labs.Cli.Tests/IntentApplierTests`；`Bukit.Content.Tests/MarkdownFolderProviderTests`；测试站点 `tests/fixtures/i18n-site/site.yaml`。实施前再检索一次。
- 快照：`Stage7/{unix,windows}/i18n-site.golden.json` 是带前缀布局。推荐在 `tests/fixtures/i18n-site/site.yaml` 显式写 `defaultLanguageInSubdir: true`，快照保持不变；改为根目录布局会让两份快照整体重写。
- 测试：新增规范场景"Adding a second language keeps default-language URLs"（`[en]` → `[en, zh-CN]`，英文输出路径和 URL 不变）、"Root redirect for a prefixed site"（需显式 `true`）、"Legacy home redirect for a root default language"（默认即生成）。
- 依赖：T06.2。

**T06.4 阶段测试**
- 测试：`Bukit.Config.Tests`、`Bukit.Engine.Tests`、`Bukit.Content.Tests`、`Bukit.Cli.Tests`、`Bukit.Labs.Cli.Tests` 完整项目；`public-api-drift.sh check`。
- 依赖：T06.3。

**阶段 6 审查**：规范里随 D4 修改的三条需求（"Default language prefix is configurable"、"Changing the layout cleans the output safely"、"Default language redirect pages"）与实现逐条对照；Worktree 构建在默认根目录布局下的行为（`WorktreeBuildCoordinatorTests`）。

### 阶段 7：文档与 CHANGELOG

**T07.1 用户指南与配置文档**
- 改动：三份 README（`README.md`、`README.zh-CN.md`、`README.ms.md`）；`guide/user/02-core-concepts.md`、`04-site-yaml-config.md`、`11-i18n-seo.md`、`14-troubleshooting.md`、`16-parameter-cheatsheet.md`；`guide/dev/config-site-yaml.md`、`guide/dev/theme.md`、`guide/dev/plugins.md`（第 165-171 行把根目录 `OutputDir` 写成"设置 `defaultLanguageInSubdir: false` 时"，D4 之后改为默认情况）；`guide/labs/import-html-demo.md`；`docs/seo.md`、`docs/demo_to_bukit_workflow.md`。删除所有 `site.language` 配置写法，改为 `languages: [x]`；说明 1 项 = 单语言、默认 `[zh-CN]`、`defaultLanguageInSubdir` 默认 `false`。
- 测试：`bash scripts/checks/config-docs-contract.sh`；`bash scripts/checks/readme-sync.sh`（改了三份 README）；`bash scripts/checks/docs/active-links.sh`、`bash scripts/checks/docs/size-policy.sh`。`guide/user/05-markdown-content.md:63-68` 的 slug 规则在 T02.1 保持现状后不需要改，T07 审查时确认。
- 依赖：T06.4。

**T07.2 skills**
- 改动：`guide/skills/` 下的 `bukit-i18n`、`bukit-config`（删除"忘写 `site.language`"的常见错误条目）、`bukit-templating`（说明模板 `site.language` 仍可用及来源）、`bukit-content`、`bukit-troubleshooting`、`using-bukit`。
- 测试：`bash scripts/checks/skills-schema.sh`、`bash guide/skills/scripts/validate-skills-strict.sh`。
- 依赖：T07.1。

**T07.3 CHANGELOG**
- 改动：`CHANGELOG.md`、`CHANGELOG.zh-CN.md`、`CHANGELOG.ms.md` 的 Unreleased 写明三项破坏性变更及改法：删除 `site.language`；单项 `languages` 改为单语言输出；`defaultLanguageInSubdir` 默认 `false`。另写 `search.json` 单语言记录的 `language` 从 `und` 变为站点语言；`metrics.json` 的 `site.languages` 在不写时从 `null` 变为 `["zh-CN"]`，`site.language` 为站点的默认语言（单语言时就是那一种语言）；带前缀布局的多语言站点，合并审计找不到路由语言时回退到默认语言。
- 依赖：T07.2。

**T07.4 openspec tasks 勾选**
- 改动：勾选 `openspec/changes/unify-site-languages/tasks.md` 中已取得证据的条目。
- 依赖：T07.3。

### 阶段 8：验证与统一审查

**T08.1 专项测试汇总**
- 测试：闭包给出的专项测试：`Bukit.Architecture.Tests`、`Bukit.Config.Tests`、`Bukit.Engine.Tests`、`Bukit.Importing.Tests`、`Bukit.Labs.Cli.Tests`、`Bukit.Plugin.Import.Tests` 完整项目，加上 `Bukit.Content.Tests`、`Bukit.Cli.Tests`；未变的结果复用缓存。
- 依赖：T07.4。

**T08.2 格式与契约检查**
- 测试：`bash scripts/checks/dotnet-format.sh`、`bash scripts/checks/code-analysis-ratchet.sh check`（PR #151 曾因 CA1506 超基线失败）、`bash scripts/checks/public-api-drift.sh check`、`openspec validate unify-site-languages --strict --no-interactive`，以及闭包给出的其他专项检查（如 `cli-docs-sync.sh`、`openspec validate i18n-output-layout --type spec --strict`）。
- 依赖：T08.1。

**T08.3 统一审查与收尾**
- 做法：`codex-workflow.py review-scope` 生成范围，做一次只看增量的统一审查；`metrics report` 汇总。
- 完成标准：无严重或重要问题。full/release gate、AOT 编译只在用户授权后执行。
- 依赖：T08.2。

## 4. 风险与回退

| 风险 | 出现位置 | 应对 |
|---|---|---|
| 不经过入口的配置读到未推导的 `Site.Language`，按 `zh-CN` 过滤丢页面 | 阶段 1、2 | 加载器（T01.1）和引擎入口（T01.2）两处推导；阶段 1 先于阶段 2；阶段 1 审查检索全部生产入口 |
| 单项站点产物与现在单语言模式不同（slug、数据目录、id 唯一性） | 阶段 2 | T00.4 先锁定现有行为，T02.3 以它为准 |
| 删除旧字段时测试站点未迁移，中间提交变红 | 阶段 4 | T04.1–T04.6 合成一个提交；阶段 2、6 同理各为一个提交 |
| Stage7 快照随 D5、D4 变化，windows 份无法在 macOS 生成 | T05.1、T06.3 | 脚本只改受影响条目，两份同改；`i18n-site` 显式写 `true` 保持快照；Windows 由 CI 验证 |
| 生成工具仍写 `language:`，生成的站点无法构建 | 阶段 3、4 | 阶段 3 先于阶段 4；阶段 3 审查全仓检索 |
| D4 让大量多语言测试改预期，掩盖真实回归 | 阶段 6 | 单独阶段、逐个判断测试原意；新增"加第二种语言 URL 不变"场景 |
| 每个单语言站点都触发布局警告 | T06.2 | 警告条件改为 `true` 才算非默认，测试固定 |
| 代码分析 ratchet 超基线 | 各阶段 | T08.2 运行 ratchet；新增成员避免引入新类型依赖 |

每个阶段结束时代码都可编译、测试通过，可以在任意阶段边界暂停。D4 被否决时撤回阶段 6，并撤回规范中随 D4 修改的三条需求。

## 5. 与 openspec `tasks.md` 的对应

| tasks.md 小节 | 本计划任务 |
|---|---|
| 0. 决策确认 | 已完成（2026-11-14） |
| 1. 配置层 | T04.1–T04.5 |
| 2. 引擎 | T01.1–T01.3、T02.1–T02.4 |
| 3. 写出或读取配置的工具 | T03.1–T03.3 |
| 4. 搜索回退语言（D5） | T05.1 |
| 5. `defaultLanguageInSubdir` 默认值（D4） | T06.1–T06.4 |
| 6. 测试 | T00.4、T02.5、T04.6、T05.1、T06.3，及各任务内新增测试 |
| 7. 文档 | T07.1–T07.3 |


## 6. 实施时与计划不一致之处

| 任务 | 计划 | 实际 | 原因 |
|---|---|---|---|
| T04.3 | 删除 `SiteConfigValidator` 的单语言 `site.language` 规则 | 保留，只对没写 `Languages` 的代码构造配置生效 | YAML 已不能写 `site.language`，但公开的 `SiteConfig.Language` 仍可在代码中赋值。这条规则拦截 `../outside`、`x"><script>`、`en\nadmin` 这类会拼进路径、HTML 和日志的值；引擎在 `BuildPlanner`、`BuildTransaction` 中都会调用 `ConfigValidator.Validate`，由 `Validate_SingleLanguageIsNotSafeTag_Throws` 覆盖 |
| T04.1–T04.6 | 文档改动放在阶段 7 | 部分改动提前到阶段 4 的提交 | `ConfigContractDriftTests` 在 README、`guide/` 出现未知的 `site.yaml` 字段（包括行内的 `site.language`）时会失败；阶段 4 必须是一个全绿的提交。提前的是最小改动：三份 README 的示例，`guide/user` 的 04、11、16，4 个 skill，并删除 11-i18n-seo.md 中 `default_language_ignored` 一行；完整修订仍在阶段 7 |
| T01.2、T02.2 | 只在 `SiteEngine.BuildCoreAsync` 和 `ContentPipeline.ExecuteAsync` 规范化 | 公开的 `RouteInventoryValidator.BuildContentRoutesAsync` 也先规范化站点语言 | 阶段 2 核实调用方时发现，这个入口不经过上面两处。代码构造的 `Languages = ["fr"]` 配置会让 `Language` 停在 `zh-CN`，丢掉法语页面。规范化对已规范化的配置不产生变化，公开 API 签名不变，有专门测试，去掉规范化即失败 |
| T02.6 | 单项时重复 key 警告不再出现 | 单项站点仍会出现重复 key 警告，与旧的单语言站点相同 | `WarnDocumentLanguageIssues` 按语言加 key 分组，旧单语言站点本来就会报警告（与 main 一致）；单项就是单语言站点，所以行为保持一致。`SingleEntryLanguagesWarnsLikeASingleLanguageSite` 断言两者警告相同，它锁定的是现状，改动前后都会通过 |

## 修订记录

| 轮次 | 日期 | 内容 |
|---|---|---|
| 初稿 | 2026-11-14 | 9 个阶段、34 个任务 |
| 确认 | 2026-11-14 | T03.2：intent 删除 `site.language`，单语言写 `languages.supported: [x]`，单项时 `languages.default` 可省略 |
| 审计 1 | 2026-11-14 | 加载器也推导语言（T01.1），引擎入口改为 `BuildCoreAsync` 和 `ContentPipeline.ExecuteAsync`，新增 SDK 任务（T01.3），阶段 1 共 4 个任务；阶段 2、4、6 各为一个提交；T00.4 改用同一语言目录下的同名文件锁定 slug，`ContentProviderFactory` 并入 T02.1；固定 `metrics.json` 规则；补 `WorktreeBuildCoordinatorTests`、`ConfigLoaderFullCoverageTests`、Stage7 快照、`guide/dev/plugins.md`；修正 `SiteEngineLifecycle` 过滤；测试站点 `site.yaml` 补闭包映射，加 `classify` |
| 复审 1 | 2026-11-14 | SDK 改动移入 T02.1（阶段 1 时单项仍是多语言，会改变 SDK 输出），阶段 1 回到 3 个任务，共 34 个任务；`SiteConfig.Language` 统一为默认语言（多语言站点也是）；规范化放在 `BuildCoreAsync` 开头；补 Stage7 快照映射、`ContentPipeline` 行号、`CloneWorkflow` 过滤 |
| 实施记录 | 2026-11-14 | 新增第 6 节，记录实施时与计划不一致的 4 处 |
