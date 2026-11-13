# `site.languages` 作为唯一语言配置：实施方案与任务计划

- 状态：**待实施**。决策 D1–D6 已于 2026-11-14 确认；实施须作为单独的 Core 任务，在用户明确开始后进行。
- 日期：2026-11-14
- 依据：规范性提案 `openspec/changes/unify-site-languages/`（proposal、design、specs、tasks）
- 代码基线：`38b32568`（main，本计划写作时）
- 范围：只覆盖 Core、Labs、Plugins 的实现、测试和文档。commit、push、PR、发布、full/release gate 均需另行授权。

---

## 0. 先读这一段

1. **先让语言推导可靠，再改模式分界。** 阶段 1 让单项 `languages` 的配置总能得到正确的 `Site.Language`，此时 `GetLanguages` 还没改，单项仍是多语言模式，页面输出不变。阶段 2 再把多语言分界改为"至少 2 项"。顺序反过来会让 `Site with { Languages = ["en"] }` 这类配置按 `zh-CN` 过滤，丢掉全部英文页面（design.md 实现方式第 2 条）。
2. **先改写出配置的工具，再拒绝旧字段。** 阶段 3 让 Labs、Importing 生成 `languages: [x]`（阶段 2 之后它已经是单语言站点，可直接构建）；阶段 4 才让 `site.language` 报错，并在同一任务内迁移全部测试站点，保证每次提交测试都是绿的。
3. **D4（`defaultLanguageInSubdir` 默认改为 `false`）放在最后，单独一个阶段。** 它改变所有多语言站点的默认布局，影响的测试最多；被否决或需要回退时，只撤这一个阶段，连同规范里随 D4 修改的三条需求。
4. **任务按严格顺序排列**，同一时刻只有一个实施者写仓库（AGENTS.md 单写者规则）。只读调查（调用方检索、测试清单）可以并行。
5. 每个任务都应能单独编译、单独通过所列测试，单独形成一个提交（提交需授权）。

## 1. 约束与执行方式

- 开工前：`python3 scripts/checks/codex-workflow.py closure` 生成验证闭包。按本计划的主要文件试算，当前**没有未映射文件**；新增测试文件若出现未映射，在 T00.2 补映射并跑 `scripts/checks/codex-workflow-self-test.sh`。再用 `codex-workflow.py queue init` 建立任务队列，实施者取得写者槽位后才写文件。
- 测试：每个任务只跑列出的专项测试，命令格式为 `dotnet test tests/<项目> -c Release --filter "<过滤>"`，过滤写成 `FullyQualifiedName~A|FullyQualifiedName~B`，下文简写为 `A|B`。每个阶段结束时跑一次该阶段涉及的完整测试项目（闭包给出的专项测试）。不跑 `scripts/test-all.sh`、`scripts/smoke-all.sh`、整个解决方案的测试和 full/release gate，除非用户明确授权。
- 证据：GREEN 结果用 `codex-workflow.py cache record` 缓存；`HEAD`、闭包、命令、环境、SDK 不变时复用。
- 审查：每个阶段实施并测完后做一次专项审查，只有严重或重要问题才回到实施。全部阶段完成后生成一次 `review-scope`，做一次只看增量的统一审查。
- 公共 API：`SiteConfig.Language` 保留为公开 init 属性，只是不再从 YAML 读取；`I18nLanguagePolicy` 是 internal。预计公共 API 基线不变，但阶段 1、2、4、6 结束时都跑 `bash scripts/checks/public-api-drift.sh check` 确认；如有漂移，在引入它的任务内更新 `docs/governance/bukit-core-public-api-baseline.v1.json`。
- 记录：各阶段耗时、缓存命中、重跑、写者冲突用 `codex-workflow.py metrics add` 记录。

## 2. 总体顺序

| 阶段 | 内容 | 对外行为 | 任务 |
|---|---|---|---|
| 0 | 准备：闭包、队列、基线测试、锁定单语言行为 | 不变 | T00.1–T00.4 |
| 1 | 语言推导 | 不变（`metrics.json` 的 `language` 可能变为真实语言） | T01.1–T01.3 |
| 2 | 多语言分界改为至少 2 项 | **单项 `languages` 改为单语言输出** | T02.1–T02.6 |
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
  - `Bukit.Engine.Tests`：`I18n|SearchIndex|MetricsWriter|IncrementalEngineIdentity|RoutePipeline|CanonicalContentValidator|DataFilesPlugin|SiteEngineLifecycle|SiteEngineLogger|ConfigValidator`
  - `Bukit.Config.Tests`：`ConfigLoader|StrictYamlContract|ConfigJsonSchemaGenerator`
  - `Bukit.Content.Tests`：`MarkdownFolderProvider`
  - `Bukit.Cli.Tests`：`WorktreeBuildCoordinator|DoctorCommand`
  - `Bukit.Labs.Cli.Tests`：`Intent|StarterTheme`
  - `Bukit.Importing.Tests`：`HtmlDemoImporter`
- 完成标准：全部通过，`cache record` 记录。
- 依赖：无。

**T00.2 验证闭包**
- 做法：用阶段 1–6 的文件生成闭包；新增测试文件如未映射，在 `scripts/checks/codex-workflow-policy.v1.json` 补映射并跑 owner 自测。
- 完成标准：`closure` 无未映射文件。
- 依赖：T00.1。

**T00.3 任务队列**
- 做法：`queue init` 写入 T00.4–T08.3；实施者 acquire 后进入 `writing`。
- 依赖：T00.2。

**T00.4 锁定现在单语言站点的行为**
- 做法：新增测试（放在 `tests/Bukit.Engine.Tests/SingleLanguageSiteTests.cs`），在**当前代码**上用"不写 `languages`、写 `language: en`"的站点断言：首页写在 `dist/index.html`；没有 `dist/en/`、hreflang 和跳转页；模板 `site.language` 为 `en`；`content/en/` 下与其他目录同名的文件的 slug 结果。这些断言在阶段 2 之后改用 `languages: [en]` 写法，结果必须完全相同。
- 测试：新测试在当前代码上通过。
- 依赖：T00.3。

**阶段 0 审查**：T00.4 的断言是否覆盖 proposal 里"产物与现在的单语言模式相同"的全部内容（输出路径、hreflang、跳转页、模板语言、slug）。

### 阶段 1：语言推导（输出不变）

**T01.1 推导入口**
- 改动：在 `Bukit-Core/Bukit.Engine/I18nLanguagePolicy.cs` 新增 internal `NormalizeSite(SiteConfig site)`：清理后的 `languages` 只有 1 项时，返回 `site with { Language = 该项 }`，否则原样返回。`GetDefaultLanguage` 在 `languages` 清理后只有 1 项时返回该项。
- 推荐做法：在引擎入口统一调用一次（`SiteEngine` 构建开始处、`Bukit.Cli/Commands/WorktreeBuildRun.cs`、`DoctorCommand` 读取配置后），之后所有读取 `Site.Language` 的位置（`RoutePipeline`、`RouteInventoryValidator`、SEO、sitemap、feed、审计、投影等，design.md Context 已列出）都拿到正确值，不用逐个改。如果审查发现入口不止这三处，改为 design.md 的另一方案：`GetSiteLanguage(site)` 由各单语言分支调用。
- 测试：新增 `I18nLanguagePolicyTests`：单项、2 项、空、全空白、重复项的 `NormalizeSite` 和 `GetDefaultLanguage`。
- 依赖：T00.4。

**T01.2 入口接入与输出不变**
- 改动：在 T01.1 列出的入口接入 `NormalizeSite`。此时 `GetLanguages` 仍是"有项就多语言"，单项站点仍按多语言输出，顶层 `Site.Language` 在多语言构建里不影响页面（`SiteEngine.cs:351` 为每种语言重设），所以页面输出不变。
- 测试：T00.1 的 Engine、Cli 过滤原样通过。`MetricsWriter` 的 `language` 字段对单项站点会变为该项，在 `MetricsWriterTests` 中更新断言并注明原因。
- 依赖：T01.1。

**T01.3 公共 API 与阶段测试**
- 测试：`bash scripts/checks/public-api-drift.sh check`；`Bukit.Engine.Tests`、`Bukit.Cli.Tests` 完整项目。
- 依赖：T01.2。

**阶段 1 审查**：入口是否齐全（全仓检索 `ConfigLoader.Load` 和 `new SiteEngine` 的生产调用方）；不经过入口的生产代码是否还会读到未推导的 `Site.Language`。

### 阶段 2：多语言分界改为至少 2 项

**T02.1 `GetLanguages` 少于 2 项返回空**
- 改动：`I18nLanguagePolicy.GetLanguages` 去重后少于 2 项时返回空列表。`BuildCommand` 的 worktree 判断已是 `Count > 1`，不改。
- 测试：`I18nLanguagePolicyTests` 增加单项返回空；T00.4 的测试改用 `languages: [en]` 写法后原样通过。
- 依赖：T01.3。

**T02.2 逐一核实调用方**
- 做法：逐个确认以下 20 个文件在单项时走单语言分支且结果正确：`Bukit.Cli/Commands/BuildCommand.cs`、`WorktreeBuildRun.cs`；`Bukit.Engine/` 下的 `BuildReportInventories`、`BuildTransaction`、`CanonicalContentValidator`（改为全站 id 唯一，与现在单语言一致）、`ContentProviderFactory`、`I18nOutputLayout`、`I18nOutputMerger`、`I18nRootRedirectWriter`、`I18nRootRobotsWriter`、`I18nRootSitemapWriter`、`I18nWorktreeBuild`、`Plugins/BuiltIn/DataFilesPlugin`（`data/<lang>/` 不再当语言目录）、`PublicOutputLifecycle`、`PublishAggregateProjectionWriters`、`RouteInventoryValidator`、`RoutePipeline`、`SearchIndexBuilder`、`SiteEngine`、`VariantReportStage`。只改结果不对的调用方。
- 测试：每改一处补对应单测；`Bukit.Engine.Tests` 过滤 `I18n|RoutePipeline|CanonicalContentValidator|DataFilesPlugin|BuildTransaction|BuildReport`。
- 依赖：T02.1。

**T02.3 直接读取 `site.Languages` 的位置**
- 改动：
  - `Bukit.Engine/MetricsWriter.cs:63,70`：改为读取 `GetLanguages` 的结果；单语言时 `languages` 字段的取值在测试中固定。
  - `Bukit.Engine/Incremental/SiteIdentityAndModesContributor.cs:21`：改为哈希 `GetLanguages` 结果加推导后的语言：同一配置的哈希稳定，单语言与多语言、不同单语言之间的哈希都不同。
  - `Bukit.Engine/ContentProviderFactory.cs:59`：按 T00.4 的 slug 断言决定单项时是否传 `Languages`，结果必须与 T00.4 一致。
- 测试：`MetricsWriterTests`、`IncrementalEngineIdentityTests`、`MarkdownFolderProviderTests`、T00.4。
- 依赖：T02.2。

**T02.4 布局类配置在单项时被忽略**
- 改动：确认 `WarnLayoutSettingsWithoutLanguages`、`WarnIgnoredLegacyRedirects` 已经经由 `GetLanguages` 判断，单项时自动视为"没有多语言"，给出现有警告。
- 测试：`SiteEngineLoggerTests` 新增：`languages: [en]` 且 `defaultLanguageInSubdir: false` 时记录 `i18n.config.default_language_layout_ignored`，输出在根目录。
- 依赖：T02.3。

**T02.5 改写单项 `Languages` 的现有测试**
- 改动：以下测试用单项列表构造配置，原意是测多语言，改为 2 项；原意是测单语言的保持并更新预期：`Bukit.Engine.Tests/DataFilesPluginTests.cs`（3 处）、`Bukit.Content.Tests/MarkdownFolderProviderTests.cs`（2 处）、`Bukit.Engine.Tests/SiteEngineLifecycleTests.cs`、`I18nRootLayoutTests.cs`、`I18nOutputMergerTests.cs`（各 1 处）。实施前再检索一次，以实际数量为准。
- 测试：上述测试类。
- 依赖：T02.4。

**T02.6 阶段测试**
- 测试：`Bukit.Engine.Tests`、`Bukit.Content.Tests`、`Bukit.Cli.Tests` 完整项目；`public-api-drift.sh check`。
- 依赖：T02.5。

**阶段 2 审查**：20 个调用方的核实记录；单项站点与 T00.4 的产物逐项一致；增量哈希在单项与多项之间切换时触发全量重建。

### 阶段 3：Labs、Importing 改为写出 `languages`

**T03.1 Labs init 与 clone-fidelity**
- 改动：`Bukit-Labs/Bukit.Labs.Cli/Commands/Theme/InitCommand.cs:312,351` 与 `Commands/Clone/CloneFidelityRunner.cs:79` 的 `language: zh-CN` 改为 `languages: [zh-CN]`。
- 测试：`Bukit.Labs.Cli.Tests` 过滤 `StarterTheme|Init|CloneFidelity`；生成的 `site.yaml` 能被 `ConfigLoader` 加载并构建为单语言站点。
- 依赖：T02.6。

**T03.2 Labs intent**
- 改动：`Commands/Intent/IntentApplier.cs:101-103,163-170` 不再写 `Language`，只写 `languages` 和（多语言时）`defaultLanguage`。intent 文件自身的 `site.language` 键（`IntentLoader.cs:39`、`IntentWizard.cs:91`）的处理：**开工时先请用户确认**，推荐与 D2 一致直接删除，单语言 intent 写 `languages.supported: [x]`。
- 测试：`IntentApplierTests`、`IntentValidatorTests`、`IntentCommandTests`。
- 依赖：T03.1，以及上面的用户确认。

**T03.3 Importing**
- 改动：`Bukit-Plugins/Bukit.Importing/SiteConfigGenerator.cs:48` 写 `languages: [<code>]`；`ResolveLanguage`、`ValidateLanguageOption` 保留。
- 测试：`Bukit.Importing.Tests` 过滤 `HtmlDemoImporter|SiteConfigGenerator`；`Bukit.Plugin.Import.Tests` 完整项目。
- 依赖：T03.2。

**阶段 3 审查**：所有生成 `site.yaml` 的代码路径都已改写（全仓检索 `"language"`、`language: ` 的生产代码）。WechatSync 只确认内容投影清单的 `language` 照常填写，不改代码。

### 阶段 4：配置层

**T04.1 读取与默认值**
- 改动：`Bukit-Core/Bukit.Config/ConfigLoader.cs:68` 不再读取 `language`。`languages` 缺失或为空列表时为 `[zh-CN]`，并写回 `SiteConfig.Languages`，让 `metrics.json`、增量哈希和语言目录看到同一个值。
- 测试：`ConfigLoaderTests`：缺失、空列表、单项、多项。
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
- 测试：`SiteEngineLoggerTests` 中相关用例改为断言不再出现。
- 依赖：T04.4。

**T04.6 迁移测试站点和内嵌配置（与 T04.2 同一提交）**
- 改动：10 个 yaml 测试站点（`tests/fixtures/{basic-markdown,component-validation,dotfile-leak,incremental,output-safety,plugin-policy,route-security,safe-url-content,taxonomy}-site/site.yaml`、`tests/Bukit.Engine.Tests/Fixtures/SrbizTask4/sites/wp2-task4-trust-fixture/site.yaml`）和测试代码里内嵌的 `language:`（约 53 处）改为 `languages: [x]`。C# 里约 112 处 `Language = "..."` 逐一区分：配置对象 `SiteConfig` 改为 `Languages = [...]`，渲染模型 `SiteModel` 不变。
- 说明：T04.2 一落地，没迁移的测试站点就会加载失败，所以 T04.2–T04.6 合成一个提交；任务仍分开执行和记录。
- 测试：`Bukit.Config.Tests`、`Bukit.Engine.Tests`、`Bukit.Content.Tests`、`Bukit.Cli.Tests`、`Bukit.Labs.Cli.Tests`、`Bukit.Importing.Tests` 完整项目；`public-api-drift.sh check`。
- 依赖：T04.5。

**阶段 4 审查**：全仓（含 `guide/`、`docs/` 以外的测试和工具）不再有用户配置层面的 `site.language`；报错信息可操作。

### 阶段 5：D5 搜索回退语言

**T05.1 `RecordLanguageFallback`**
- 改动：`Bukit-Core/Bukit.Engine/SearchIndexBuilder.cs:20` 单语言时返回推导后的站点语言，不再返回 `und`。调用方 `SearchIndexPlugin.cs:119`、`PublishAggregateProjectionWriters.cs:154` 不改。
- 测试：`Bukit.Engine.Tests` 过滤 `SearchIndex|I18nMergedSearchIndex`；新增规范场景"Single-language search record language"。
- 依赖：T04.6。

**阶段 5 审查**：`search.json` 的消费者（starter theme 搜索页、`bukit-search.html`）对单语言站点的语言过滤是否仍正确。

### 阶段 6：D4 `defaultLanguageInSubdir` 默认改为 `false`（单独提交，可整体回退）

**T06.1 默认值**
- 改动：`Bukit.Config/AppConfig.cs` 中 `DefaultLanguageInSubdir` 默认 `false`；`ConfigLoader` 缺省值、JSON Schema 默认值同步。
- 测试：`ConfigLoaderTests`、`ConfigJsonSchemaGeneratorTests`。
- 依赖：T05.1。

**T06.2 警告与哈希条件**
- 改动：`I18nLanguagePolicy.WarnLayoutSettingsWithoutLanguages` 的"非默认"判断改为 `DefaultLanguageInSubdir == true`（否则每个单语言站点每次构建都警告）；`WarnIgnoredLegacyRedirects` 不变。`SiteIdentityAndModesContributor.cs:22-25` 改为只在多语言且为 `true` 时计入哈希。
- 测试：`SiteEngineLoggerTests`：单语言默认不警告、设 `true` 警告；`IncrementalEngineIdentityTests`：单语言站点和未设置该项的多语言站点不把它计入哈希。
- 依赖：T06.1。

**T06.3 更新依赖默认带前缀的测试**
- 改动：以下多语言测试没有显式设置 `defaultLanguageInSubdir`，需要逐一判断：要测带前缀布局的显式写 `true`，否则更新为根目录预期。`Bukit.Engine.Tests`：`I18nMergedFeedProjectionTests`、`I18nMergedSitemapCollectionTests`、`I18nOutputMergerTests`、`I18nRootRedirectTests`、`RenderDependencyContributorInvalidationTests`、`RenderDependencyHasherTests`、`DataFilesPluginTests`、`ConfigValidatorTests`、`ReportStageTimingTests`、`BuildTransactionTests`、`SiteEngineLifecycleTests`、`IncrementalRenderReadsTests`、`CanonicalContentValidatorTests`、`SiteEngineIntegrationTests`；`Bukit.Config.Tests/ConfigLoaderTests`；`Bukit.Cli.Tests/DoctorCommandTests`；`Bukit.Labs.Cli.Tests/IntentApplierTests`；`Bukit.Content.Tests/MarkdownFolderProviderTests`；测试站点 `tests/fixtures/i18n-site/site.yaml`。实施前再检索一次。
- 测试：新增规范场景"Adding a second language keeps default-language URLs"（`[en]` → `[en, zh-CN]`，英文输出路径和 URL 不变）、"Root redirect for a prefixed site"（需显式 `true`）、"Legacy home redirect for a root default language"（默认即生成）。
- 依赖：T06.2。

**T06.4 阶段测试**
- 测试：`Bukit.Config.Tests`、`Bukit.Engine.Tests`、`Bukit.Content.Tests`、`Bukit.Cli.Tests`、`Bukit.Labs.Cli.Tests` 完整项目；`public-api-drift.sh check`。
- 依赖：T06.3。

**阶段 6 审查**：规范里随 D4 修改的三条需求（"Default language prefix is configurable"、"Changing the layout cleans the output safely"、"Default language redirect pages"）与实现逐条对照；Worktree 构建在默认根目录布局下的行为（`WorktreeBuildCoordinatorTests`）。

### 阶段 7：文档与 CHANGELOG

**T07.1 用户指南与配置文档**
- 改动：三份 README（`README.md`、`README.zh-CN.md`、`README.ms.md`）；`guide/user/02-core-concepts.md`、`04-site-yaml-config.md`、`11-i18n-seo.md`、`14-troubleshooting.md`、`16-parameter-cheatsheet.md`；`guide/dev/config-site-yaml.md`、`guide/dev/theme.md`；`guide/labs/import-html-demo.md`；`docs/seo.md`、`docs/demo_to_bukit_workflow.md`。删除所有 `site.language` 配置写法，改为 `languages: [x]`；说明 1 项 = 单语言、默认 `[zh-CN]`、`defaultLanguageInSubdir` 默认 `false`。
- 测试：`bash scripts/checks/config-docs-contract.sh`；`bash scripts/checks/docs/active-links.sh`、`bash scripts/checks/docs/size-policy.sh`。
- 依赖：T06.4。

**T07.2 skills**
- 改动：`guide/skills/` 下的 `bukit-i18n`、`bukit-config`（删除"忘写 `site.language`"的常见错误条目）、`bukit-templating`（说明模板 `site.language` 仍可用及来源）、`bukit-content`、`bukit-troubleshooting`、`using-bukit`。
- 测试：`bash scripts/checks/skills-schema.sh`。
- 依赖：T07.1。

**T07.3 CHANGELOG**
- 改动：`CHANGELOG.md`、`CHANGELOG.zh-CN.md`、`CHANGELOG.ms.md` 的 Unreleased 写明三项破坏性变更及改法：删除 `site.language`；单项 `languages` 改为单语言输出；`defaultLanguageInSubdir` 默认 `false`。另写 `search.json` 单语言记录语言变化、`metrics.json` 字段变化。
- 依赖：T07.2。

**T07.4 openspec tasks 勾选**
- 改动：勾选 `openspec/changes/unify-site-languages/tasks.md` 中已取得证据的条目。
- 依赖：T07.3。

### 阶段 8：验证与统一审查

**T08.1 专项测试汇总**
- 测试：闭包给出的专项测试：`Bukit.Architecture.Tests`、`Bukit.Config.Tests`、`Bukit.Engine.Tests`、`Bukit.Importing.Tests`、`Bukit.Labs.Cli.Tests`、`Bukit.Plugin.Import.Tests` 完整项目，加上 `Bukit.Content.Tests`、`Bukit.Cli.Tests`；未变的结果复用缓存。
- 依赖：T07.4。

**T08.2 格式与契约检查**
- 测试：`bash scripts/checks/dotnet-format.sh`、`bash scripts/checks/code-analysis-ratchet.sh check`（PR #151 曾因 CA1506 超基线失败）、`bash scripts/checks/public-api-drift.sh check`、`openspec validate unify-site-languages --strict --no-interactive`。
- 依赖：T08.1。

**T08.3 统一审查与收尾**
- 做法：`codex-workflow.py review-scope` 生成范围，做一次只看增量的统一审查；`metrics report` 汇总。
- 完成标准：无严重或重要问题。full/release gate、AOT 编译只在用户授权后执行。
- 依赖：T08.2。

## 4. 风险与回退

| 风险 | 出现位置 | 应对 |
|---|---|---|
| 不经过入口的配置读到未推导的 `Site.Language`，按 `zh-CN` 过滤丢页面 | 阶段 1、2 | 阶段 1 先于阶段 2；阶段 1 审查检索全部生产入口 |
| 单项站点产物与现在单语言模式不同（slug、数据目录、id 唯一性） | 阶段 2 | T00.4 先锁定现有行为，T02.3 以它为准 |
| 删除旧字段时测试站点未迁移，中间提交变红 | 阶段 4 | T04.2–T04.6 合成一个提交 |
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
| 6. 测试 | T00.4、T02.5、T04.6、T06.3，及各任务内新增测试 |
| 7. 文档 | T07.1–T07.3 |

## 待用户确认

- T03.2：Labs intent 文件里的 `site.language` 键直接删除（推荐），还是映射为 `languages.supported: [x]`。

## 修订记录

| 轮次 | 日期 | 内容 |
|---|---|---|
| 初稿 | 2026-11-14 | 9 个阶段、34 个任务 |
