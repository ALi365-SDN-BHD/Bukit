# 默认语言根目录布局：实施方案与任务计划

- 状态：**计划，未实施**。实施属于 Core 任务，需用户另行授权后作为单独的 Core 任务开展。
- 日期：2026-11-12
- 依据：`docs/analysis/bukit-core-i18n-default-language-root-proposal-2026-11-12.zh-CN.md`；规范性提案 `openspec/changes/add-i18n-default-language-root/`（proposal、design、specs、tasks）
- 代码基线：`33c20cd7`（本计划写作时 Core 代码与之相同）
- 范围：只覆盖 Core 实现、测试、文档和契约。commit、push、PR、发布、站点仓库改动、full/release gate 均需另行授权。

---

## 0. 先读这一段

1. **先做"默认值行为不变"的重构，再加新行为。** 阶段 2 把所有拼接语言代码的位置收拢到 `I18nOutputLayout`，此时布局固定为"带前缀"，现有 i18n 测试必须原样通过。之后每个阶段都在这个对照之上增加根目录布局的行为。
2. **默认值下唯一的对外变化是带前缀布局的根目录跳转页**（阶段 4）。它单独成一个阶段、单独测试，便于评审和回退。
3. **根目录布局分四步打开**：输出与 URL（阶段 5）→ 根目录归属与报告（阶段 6）→ 保留前缀（阶段 7）→ Worktree（阶段 8）。在阶段 7 完成之前，根目录布局只在测试中使用，不写进任何用户文档。
4. **任务按严格顺序排列**，同一时刻只有一个实施者写仓库（AGENTS.md 单写者规则）。"依赖"一栏说明为什么排在这里；没有依赖关系的任务也不并行写，只有只读的调查可以并行。
5. 每个任务都应能单独编译、单独通过所列测试，并单独形成一个提交（提交需授权）。

## 1. 约束与执行方式

- 开工前：`python3 scripts/checks/codex-workflow.py closure` 生成验证闭包，处理全部未映射文件（已知 6 个，见任务 T00.2）；`codex-workflow.py queue init` 建立任务队列，实施者取得写者槽位后才写文件。
- 测试：只跑每个任务列出的专项测试（`dotnet test <项目> -c Release --filter <过滤>`）。不跑 `scripts/test-all.sh`、`scripts/smoke-all.sh`、整个解决方案的测试和 full/release gate，除非用户明确授权。
- 证据：GREEN 结果用 `codex-workflow.py cache record` 缓存；闭包、命令、环境、SDK 不变时复用，不重复跑同一 fixture。
- 审查：每个阶段实施并测完后做一次专项审查；只有严重或重要问题才回到实施。全部阶段完成后生成一次 `review-scope`，做一次只看增量的统一审查。
- 记录：各阶段耗时、缓存命中、重跑、写者冲突用 `codex-workflow.py metrics add` 记录。

## 2. 总体顺序

| 阶段 | 内容 | 默认值下对外行为 | 任务 |
|---|---|---|---|
| 0 | 准备：闭包、队列、基线测试 | 不变 | T00.1–T00.3 |
| 1 | 配置项与校验 | 不变 | T01.1–T01.6 |
| 2 | `I18nOutputLayout` 与重构（布局固定为带前缀） | 不变 | T02.1–T02.7 |
| 3 | 跳转页共用实现 | alias、分类跳转页加 `<title>` 和 `noindex` | T03.1–T03.3 |
| 4 | 带前缀布局的根目录跳转页 | **新增根目录 `index.html`** | T04.1–T04.4 |
| 5 | 根目录布局：输出目录与 URL | 不变 | T05.1–T05.6 |
| 6 | 根目录布局：根目录归属、static、报告 | 不变 | T06.1–T06.9 |
| 7 | 根目录布局：路由保留前缀 | 不变 | T07.1–T07.7 |
| 8 | 根目录布局：Worktree 构建 | 不变 | T08.1–T08.5 |
| 9 | 根目录布局：`/<lang>/` 跳转页 | 不变 | T09.1–T09.3 |
| 10 | 布局切换与增量构建 | 不变（哈希不变） | T10.1–T10.4 |
| 11 | 文档与契约 | — | T11.1–T11.8 |
| 12 | 验证与统一审查 | — | T12.1–T12.4 |

## 3. 任务清单

格式：**编号 名称** — 改动 / 做法 / 测试与完成标准 / 依赖。文件路径以 `src/Bukit-Core/` 为根，测试以仓库根的 `tests/` 为根。

### 阶段 0：准备

**T00.1 基线测试取证**
- 做法：在未改动的分支上跑现有 i18n 相关专项测试并缓存证据，作为"行为不变"的对照。
- 测试：`tests/Bukit.Engine.Tests`（过滤 `I18n|SiteEngineIntegration|SitemapPlugin|AliasPlugin|SeoAuditReportWriter|BuildReport|IncrementalEngineIdentity|BuildTransaction`）、`tests/Bukit.Config.Tests`（`ConfigLoader|StrictYamlContract|ConfigPhase2Validator`）、`tests/Bukit.Cli.Tests`（`WorktreeBuildCoordinator`）。
- 完成标准：全部通过，`cache record` 记录。
- 依赖：无。

**T00.2 验证闭包与未映射文件**
- 做法：用计划涉及的文件生成闭包；为新增文件（`I18nOutputLayout.cs`、根目录跳转页 writer、共用跳转页实现、新测试文件）在 `scripts/checks/codex-workflow-policy.v1.json` 中补映射。改动该策略文件后跑它的 owner 自测 `scripts/checks/codex-workflow-self-test.sh`。
- 完成标准：`closure` 无未映射文件。
- 依赖：T00.1。

**T00.3 任务队列**
- 做法：`queue init` 写入 T01.1–T12.4；实施者 acquire 后进入 `writing`。
- 依赖：T00.2。

### 阶段 1：配置项与校验

**T01.1 `SiteConfig` 新增两个属性**
- 改动：`Bukit.Config/AppConfig.cs` 中的 `SiteConfig` 新增 `bool DefaultLanguageInSubdir { get; init; } = true;`、`bool DefaultLanguageRedirect { get; init; } = true;`。
- 测试：编译通过；`tests/Bukit.Config.Tests` 新增默认值断言。
- 依赖：T00.3。

**T01.2 YAML 读取**
- 改动：`Bukit.Config/ConfigLoader.cs`（`defaultLanguage` 附近，约第 70 行）读取 `defaultLanguageInSubdir`、`defaultLanguageRedirect`，缺省为 `true`；非布尔值按现有布尔字段的错误方式报错。
- 测试：`ConfigLoaderTests` 新增读取 true/false/缺省/非法值用例。
- 依赖：T01.1。

**T01.3 严格字段白名单**
- 改动：`Bukit.Config/ConfigStrictFieldValidator.cs` 第 12 行的 site 字段清单加入两个字段。
- 测试：`StrictYamlContractTests` 新增用例：两个字段不再被判为未知字段。
- 依赖：T01.2。

**T01.4 JSON Schema**
- 改动：`Bukit.Config/ConfigJsonSchemaGenerator.cs`（`defaultLanguage` 附近，约第 65 行）加入两个布尔字段及默认值。
- 测试：Schema 相关测试新增断言；若仓库有提交的 schema 产物或文档同步脚本（`scripts/checks/config-docs-contract.sh`），跑其 owner 自测。
- 依赖：T01.3。

**T01.5 配置校验**
- 改动：`Bukit.Config/SiteConfigValidator.cs`（search mode 校验附近，约第 91 行）：`languages` 非空、`DefaultLanguageInSubdir == false` 且 search mode 解析为 `merged` 时抛 `ConfigException`，信息同时点名 `site.defaultLanguageInSubdir` 和 `site.search.mode`；`languages` 为空且任一新字段为 `false` 时记录警告（按现有警告通道）。
- 测试：`ConfigPhase2ValidatorTests` 或 `ConfigValidatorTests` 新增：merged 组合报错、split/index 通过、无 languages 时警告。
- 依赖：T01.4。

**T01.6 增量站点哈希**
- 改动：`Bukit.Engine/Incremental/SiteIdentityAndModesContributor.cs`：两个字段**只在值为 `false` 时**写入哈希。
- 测试：`IncrementalEngineIdentityTests` 新增：未设置与显式 `true` 的哈希和改动前完全相同；任一字段为 `false` 时哈希改变。
- 依赖：T01.5。
- 说明：放在阶段 1，是为了让后续所有阶段的测试都在"默认值哈希不变"的保护下进行。

**阶段 1 审查**：配置读取、白名单、Schema、校验、哈希的一致性。

### 阶段 2：`I18nOutputLayout` 与重构（布局固定为带前缀）

**T02.1 新增 `I18nOutputLayout`**
- 改动：新增 `Bukit.Engine/I18nOutputLayout.cs`（internal）。输入：`SiteConfig`、语言列表、根 baseUrl。按语言提供：`BaseUrl`、`RelativeOutputPrefix`（带前缀时为 `<lang>`，根目录语言为空串）、`OutputDir(root)`、`IsRootLanguage`、`ReportDir(root)`（带前缀时为 `<root>/<lang>/.bukit`，根目录语言为 `<root>/.bukit/variants/<lang>`）、`ReservedPrefixes`（根目录布局下为其他语言代码，带前缀布局为空）。内部复用 `I18nLanguagePolicy.CombineBaseUrlWithLanguage`。
- 测试：新增 `tests/Bukit.Engine.Tests/I18nOutputLayoutTests.cs`：两种布局 × `/`、`/repo` baseUrl × 默认语言与其他语言的全部属性；`languages` 为空时不生效。
- 依赖：T01.6。

**T02.2 baseUrl 的 3 个调用点**
- 改动：`SiteEngine.cs:331`、`I18nWorktreeBuild.cs:186`、`SeoAlternatesService.cs:36` 改用 `I18nOutputLayout.BaseUrl`。`I18nOutputMerger.CombineBaseUrlWithLanguage` 保留给布局内部使用或删除（确认无其他调用方）。
- 测试：T00.1 的 i18n 测试原样通过。
- 依赖：T02.1。

**T02.3 进程内输出目录**
- 改动：`SiteEngine.cs` 约第 342 行 `Path.Combine(outputDir, lang)` 改为 `layout.OutputDir(outputDir, lang)`。
- 测试：同上。
- 依赖：T02.2。

**T02.4 Worktree 输出目录与 manifest 路径**
- 改动：`Bukit.Cli/Commands/WorktreeBuildRun.cs:313`（staging 输出目录）、`Bukit.Cli/Commands/WorktreeBuildPaths.cs:64-65`（`RewriteManifestRoots`）及 `CopyVariantManifest` 改用布局的相对前缀。
- 测试：`tests/Bukit.Cli.Tests` 过滤 `WorktreeBuildCoordinator` 原样通过。
- 依赖：T02.3。

**T02.5 去掉把语言代码当子目录的 3 处**
- 改动：`I18nMergedVariantState.cs:81`、`SeoAuditReportWriter.cs:66,84` 的 `Path.Combine(result.Language, …)` 改为布局相对前缀（根目录语言为空串时不拼接）。
- 测试：`I18nOutputMergerTests`、`SeoAuditReportWriterTests`、`I18nRegressionTests` 原样通过。
- 依赖：T02.4。

**T02.6 `BuildTransaction.RejectGeneratedOutputLinks`**
- 改动：`BuildTransaction.cs:401-408` 的路径清单按布局生成：根目录聚合用 `ProjectionPlan(config, [], root: true)`；每种语言加其相对前缀，空前缀不当作语言目录。
- 测试：`BuildTransactionTests` 原样通过，并新增带前缀布局下清单内容与改动前相同的断言。
- 依赖：T02.5。
- 说明：第 404 行现在对根目录用的是非 root 计划，改动时确认清单只会更准确、不会漏检。

**T02.7 报告目录与 analytics 报告查找**
- 改动：`VariantReportStage` 和 `SeoAuditReportWriter.Write` 的报告目录改由布局提供（带前缀时结果不变）；`BuildReportInventories.cs` 中 `ArtifactManifestInventory.AddVariantAnalyticsReports` 按 `layout.ReportDir` 查找。
- 测试：`BuildReportPipelineTests`、`BuildReporterTests` 原样通过。
- 依赖：T02.6。

**阶段 2 审查**：重点检查是否还有遗漏的 `Path.Combine(…, lang)` 或 `"/" + lang` 拼接（全仓检索 `\.Language\b` 与 `Path.Combine`），以及 T00.1 对照全部通过。

### 阶段 3：跳转页共用实现

**T03.1 共用跳转页生成**
- 改动：新增 `Bukit.Engine/RedirectPageHtml.cs`（internal），输出 meta refresh、canonical、`<title>`、`<meta name="robots" content="noindex">`；对目标地址做 HTML 属性转义。
- 测试：新增 `RedirectPageHtmlTests`：四个标签齐全、转义正确、绝对与相对地址。
- 依赖：阶段 2 完成。

**T03.2 `AliasPlugin` 改用共用实现**
- 改动：`Plugins/BuiltIn/AliasPlugin.cs:86` 的 `BuildRedirectHtml` 删除，改调 `RedirectPageHtml`。
- 测试：`AliasPluginTests` 更新快照或断言（新增两个标签）；增量测试确认派生跳转页因内容哈希变化被重新生成。
- 依赖：T03.1。

**T03.3 `TaxonomyRedirectWriter` 改用共用实现**
- 改动：`Plugins/BuiltIn/TaxonomyRedirectWriter.cs:62` 的 `RenderRedirect` 删除，改调共用实现。
- 测试：分类跳转相关测试更新断言。
- 依赖：T03.2。

### 阶段 4：带前缀布局的根目录跳转页（默认开启）

**T04.1 根目录跳转页 writer**
- 改动：新增 `Bukit.Engine/I18nRootRedirectWriter.cs`，实现 `II18nRootProjectionWriter`：带前缀布局、`DefaultLanguageRedirect == true`、`languages` 非空时写 `<output>/index.html`，目标 `<baseUrl>/<defaultLang>/`；配置了 `site.url` 时用绝对地址。
- 测试：新增 `I18nRootRedirectTests`（writer 级）：绝对/相对地址、`/repo` baseUrl、`false` 时不写。
- 依赖：阶段 3 完成。

**T04.2 注册与根目录计划输出**
- 改动：`I18nRootProjectionWriterRegistry.CreateDefault()` 注册新 writer；`PublicOutputLifecycle.ProjectionPlan(root: true)` 在相同条件下加入 `index.html`，使其进入根目录归属清单（受保护删除、重复检查）。
- 测试：`I18nRegressionTests` 或新增集成测试：默认配置的多语言构建生成 `dist/index.html`；关闭后上次生成的 `index.html` 被作为自有文件删除。
- 依赖：T04.1。

**T04.3 跳转页排除于各类索引**
- 改动：确认根目录跳转页不经过路由，因此不进入 sitemap、搜索、llms、agent-manifest、feed 和 SEO 路由审计；如有聚合按根目录文件扫描，显式排除。
- 测试：集成测试断言上述产物均不含根目录跳转页地址。
- 依赖：T04.2。

**T04.4 升级场景回归**
- 测试：spec 场景 "Upgrade without opting in"：未设置两个新字段时，增量站点哈希、全部已有输出路径不变，唯一新增的公开文件是 `dist/index.html`。
- 依赖：T04.3。

**阶段 4 审查**：默认行为变化是否只有这一个文件；Netlify 场景的说明留到 T11。

### 阶段 5：根目录布局——输出目录与 URL

**T05.1 打开布局开关**
- 改动：`I18nOutputLayout` 读取 `DefaultLanguageInSubdir`：为 `false` 时默认语言 `IsRootLanguage = true`，`BaseUrl` 为根 baseUrl，`OutputDir` 为输出根目录，`ReportDir` 为 `.bukit/variants/<lang>`。
- 测试：`I18nOutputLayoutTests` 的根目录布局用例转为生效。
- 依赖：阶段 4 完成。

**T05.2 进程内构建的根目录输出**
- 测试：新增 `tests/Bukit.Engine.Tests/I18nRootLayoutTests.cs`：`dist/index.html`、`dist/404.html` 为英文页面，`dist/zh-CN/index.html` 为中文页面，`dist/en/` 下没有内容页。
- 依赖：T05.1。

**T05.3 URL、canonical、hreflang、x-default**
- 测试：同文件新增 spec 场景 "hreflang for a root default language"：`en` 与 `x-default` 为 `https://example.com/about/`，`zh-CN` 为 `https://example.com/zh-CN/about/`；模板 `site.base_url` 为根 baseUrl。
- 依赖：T05.2。

**T05.4 项目站点 baseUrl**
- 测试：`site.baseUrl: /repo` 时英文 baseUrl 为 `/repo`、中文为 `/repo/zh-CN`，生成的链接与 canonical 正确。
- 依赖：T05.3。

**T05.5 合并 SEO 审计路径**
- 测试：根目录布局下合并 SEO 审计不报 `seo.output_file_missing`；`--strict` 通过。
- 依赖：T05.4。

**T05.6 `--metrics` 输出**
- 测试：`variants[].outputDir` 对默认语言等于输出根目录（记录为已知差异，不改格式）。
- 依赖：T05.5。

### 阶段 6：根目录布局——根目录归属、static、报告

**T06.1 `ProjectionPlan` 支持根目录语言**
- 改动：`PublicOutputLifecycle.ProjectionPlan` 新增参数 `rootLanguage`：为 true 时不计划 `agent-manifest.json`、`robots.txt`、`llms.txt`、`llms-full.txt`；所有语言的计划输出按 sitemap/feed mode 生成（merged 时不再列出插件本来就不写的文件）。
- 测试：单元测试覆盖三种 sitemap mode × 两种 feed mode × 根目录语言与否的计划清单。
- 依赖：阶段 5 完成。

**T06.2 计划清单变化的安全性**
- 测试：带前缀站点从旧版本 manifest 升级：计划清单变短后不误删任何实际存在的文件，不触发全量重建（对应风险 R4）。
- 依赖：T06.1。

**T06.3 index 模式下默认语言 sitemap**
- 改动：`Plugins/BuiltIn/SitemapPlugin.cs`：根目录语言且 `sitemapMode: index` 时写 `<lang>/sitemap.xml`（条目地址不带前缀）；计划输出同步为 `<lang>/sitemap.xml`。`I18nRootSitemapWriter` 的索引条目引用该地址。
- 测试：spec 场景 "Sitemap index with a root default language"；split 模式默认语言写根目录 `sitemap.xml`；merged 模式只有根目录。
- 依赖：T06.2。

**T06.4 robots 引用实际 sitemap 地址**
- 改动：`I18nRootRobotsWriter`：split 模式下默认语言 sitemap 地址为 `<siteUrl><baseUrl>/sitemap.xml`；index 模式引用根目录索引。
- 测试：`RobotsTxtWriterTests` 或集成测试覆盖两种 mode。
- 依赖：T06.3。

**T06.5 feed 与 search**
- 测试：split feed 下默认语言 feed 在根目录；merged feed 只有根目录合并 feed；search split/index 下默认语言写 `/search.json`，index 模式根目录 `search.index.json` 引用正确路径。
- 依赖：T06.4。

**T06.6 默认语言 static：robots 覆盖与冲突**
- 改动：在 static 资源计划确定后、复制前检查：默认语言 `static/robots.txt` 存在时作为整站 robots，根目录 robots writer 不生成；与 `agent-manifest.json`、`llms.txt`、`llms-full.txt`、merged/index 下的根 `sitemap.xml`、merged feed 文件同名时抛 `i18n-root-output-conflict`，信息含 static 源路径和对应设置。新增报错码到 `Bukit.Shared/DiagnosticCode.cs`（公共枚举，基线在 T11.1 更新）及 `DiagnosticCodeFormatter`。
- 测试：spec 场景 "Static robots override"、"Static llms conflict"；`404.html`、`CNAME` 正常落到根目录。
- 依赖：T06.5。

**T06.7 默认语言报告移到 `.bukit/variants/<lang>/`**
- 改动：`SeoAuditReportWriter.Write`（含 publish-audit、seo-route-map、geo）和 `AnalyticsReportWriter` 使用 `layout.ReportDir`；根目录合并报告路径不变。
- 测试：spec 场景 "SEO reports"：`dist/.bukit/seo-report.json` 为合并报告，`dist/.bukit/variants/en/seo-report.json` 为英文报告；5 种报告逐一断言。
- 依赖：T06.6。

**T06.8 build-report 产物清单**
- 测试：spec 场景 "Analytics report in the build report"：产物清单登记 `.bukit/variants/en/analytics-report.json`（T02.7 已改查找逻辑，这里补根目录布局用例）。
- 依赖：T06.7。

**T06.9 隐私扫描与两份清单的计入范围**
- 做法：确认 `PublicOutputPrivacyCheck`、`AssetsReportInventory.Create`、`ReleaseBundleInventory.Create` 在两种布局下对语言报告的处理（带前缀：`<lang>/.bukit/` 被扫描和计入；根目录布局：默认语言报告在根 `.bukit/` 下被跳过）。与用户确认这是预期行为后，用测试固定；若用户要求两种布局一致，另立任务调整。
- 测试：三处各一条两种布局的断言。
- 依赖：T06.8。

**阶段 6 审查**：根目录每个文件的唯一归属；`AssetOutputPlan.Validate` 仍作为最后防线。

### 阶段 7：根目录布局——路由保留前缀

**T07.1 共用检查实现**
- 改动：新增内部检查（建议放在 `I18nOutputLayout` 或新类 `I18nReservedPrefixGuard`）：取输出相对路径第一个路径段，与 `ReservedPrefixes` 做不区分大小写比较（`StringComparison.OrdinalIgnoreCase`，任何平台一致）；默认语言自身代码不保留。新增报错码 `i18n-route-reserved-language-prefix`，信息含文档 id 或源路径，以及被占用的语言代码。
- 测试：单元测试覆盖大小写、默认语言代码放行、空路径、多级路径。
- 依赖：阶段 6 完成。

**T07.2 路由检查**
- 改动：在 `RoutePipeline` 完成路由后、渲染前检查内容、列表/分页、分类、alias、派生文档路由（可挂在 `RouteInventoryValidator`）。
- 测试：spec 场景 "Taxonomy term named like a language"、"Case-insensitive collision on any platform"、"Legacy alias under the default language code"（`/en/about/` 允许）。
- 依赖：T07.1。

**T07.3 static 与资源计划检查**
- 改动：在 static 和资源计划确定后、复制前检查。
- 测试：默认语言 `static/zh-CN/x.txt` 报错；`static/en/x.txt` 允许。
- 依赖：T07.2。

**T07.4 插件输出检查（进程内）**
- 改动：在 AfterBuild 之后、提交之前检查默认语言插件输出；违规时阻止提交，正式输出不受影响。
- 测试：插件写 `zh-CN/sw.js` 时构建失败，正式输出目录保持构建前状态。
- 依赖：T07.3。

**T07.5 根因优先报告**
- 改动：汇总各语言失败时，若默认语言插件输出越界，把它作为主要错误报告（进程内由构建本身汇总；Worktree 部分在 T08.3 完成）。
- 测试：默认语言插件越界且另一语言同时失败时，首要错误为 `i18n-route-reserved-language-prefix`。
- 依赖：T07.4。

**T07.6 新增语言场景**
- 测试：spec 场景 "Adding a language"：已有英文页面 `/ms/guide/`，加入 `ms` 后报错并点名该页面和 `ms`。
- 依赖：T07.5。

**T07.7 禁止对语言输出目录做裁剪同步**
- 改动：在 `DirectoryCopy.Sync(prune: true)` 的调用点或测试中加断言：目标目录不得是根目录语言的输出目录（否则会删除其他语言目录）。
- 测试：架构测试或单元测试。
- 依赖：T07.6。

**阶段 7 审查**：检查时机是否都在写文件之前（插件输出除外）；报错信息是否可操作。

### 阶段 8：根目录布局——Worktree 构建

**T08.1 默认语言 Worker 输出到 staging 根目录**
- 改动：`WorktreeBuildRun` 已在 T02.4 改用布局；确认根目录语言的 Worker `Output` 为 staging 根目录，`Clean = false`，且 Worker 不会清理或裁剪其他语言目录。
- 测试：`tests/Bukit.Cli.Tests` 新增：默认语言 Worker 输出在 staging 根目录。
- 依赖：阶段 7 完成。

**T08.2 父进程路径校验**
- 改动：`I18nWorktreeProtocol.ValidateOutputPaths`（第 298 行）对根目录语言：结果引用的路径不得位于其他语言目录下（调用 T07.1 的同一检查）；其他语言保持现有"必须在自己目录内"的检查。
- 测试：`I18nWorktreeProtocolTests` 新增：越界结果被拒绝，staging 不提交。
- 依赖：T08.1。

**T08.3 Worktree 根因优先报告**
- 改动：父进程汇总 Worker 失败时，默认语言越界优先。
- 测试：同 T07.5 的场景在 Worktree 模式下成立。
- 依赖：T08.2。

**T08.4 manifest 根路径改写**
- 测试：`RewriteManifestRoots`、`CopyVariantManifest` 在根目录语言下路径正确（空前缀）。
- 依赖：T08.3。

**T08.5 进程内与 Worktree 输出等价**
- 测试：`i18n-worktree-build` 规格的 MODIFIED 场景：同一干净提交用 `bukit build`（Worktree）与 `bukit deploy` 的进程内构建，都设 `defaultLanguageInSubdir: false`，输出文件路径集合一致。
- 依赖：T08.4。

### 阶段 9：根目录布局——`/<lang>/` 跳转页

**T09.1 派生跳转文档**
- 改动：根目录布局、`DefaultLanguageRedirect == true` 时，默认语言生成 `<lang>/index.html` 跳转到 `<baseUrl>/`，用 T03.1 的共用实现，作为派生文档参与归属和增量。
- 测试：spec 场景 "Legacy home redirect for a root default language"。
- 依赖：阶段 8 完成。

**T09.2 用户输出优先**
- 改动：同路径已有用户输出（如 alias `/en/`）时跳过并记录 info 日志。
- 测试：spec 场景 "User content wins over the legacy redirect"。
- 依赖：T09.1。

**T09.3 排除与关闭**
- 测试：跳转页不进 sitemap、搜索、llms、agent-manifest、feed、SEO 路由审计；spec 场景 "Redirects disabled" 两种布局都不生成。
- 依赖：T09.2。

### 阶段 10：布局切换与增量构建

**T10.1 根 manifest 记录布局**
- 改动：`Incremental/BuildManifest.cs` 新增布局字段（读写兼容：旧 manifest 无此字段时视为带前缀）。
- 测试：manifest 读写往返；旧 manifest 读取。
- 依赖：阶段 9 完成。

**T10.2 布局变化时受保护清理**
- 改动：上次布局与本次不同时，走 `OutputDirectoryCleaner` 的受保护清理后再写。
- 测试：spec 场景 "Switching to a root default language"：`dist/en/` 不再存在，`dist/index.html` 为英文首页；反向切换同样干净。
- 依赖：T10.1。

**T10.3 增量删除不越界**
- 测试：spec 场景 "Incremental removal near another language"：删除唯一的英文 `/guides/` 页面后 `dist/guides/` 被删，`dist/zh-CN/guides/` 不变；空目录清理不删除其他语言目录。
- 依赖：T10.2。

**T10.4 跳转页配置切换**
- 测试：`DefaultLanguageRedirect` 由 `true` 改 `false` 时，上次生成的两类跳转页都被作为自有输出删除。
- 依赖：T10.3。

**阶段 10 审查**：清理路径只走受保护通道；哈希只在 `false` 时变化。

### 阶段 11：文档与契约

**T11.1 公共 API 基线**
- 改动：`docs/governance/bukit-core-public-api-baseline.v1.json` 登记 `SiteConfig` 的两个新属性，以及公共枚举 `Bukit.Shared.DiagnosticCode` 新增的两个成员（T06.6、T07.1 引入）。
- 测试：`scripts/checks/public-api-drift.sh` 与其 owner 自测。
- 依赖：阶段 10 完成。

**T11.2 报错码登记**
- 改动：两个报错码进入诊断码清单及相关文档（如有诊断码契约文件）。
- 依赖：T11.1。

**T11.3 i18n skill**
- 改动：`guide/skills/bukit-i18n/SKILL.md`：替换 "add a root redirect at the host or CDN" 的说法；说明两种布局、两个配置项、保留前缀、报告位置。跑 `scripts/checks/skills-schema.sh`。
- 依赖：T11.2。

**T11.4 用户指南与配置文档**
- 改动：`guide/user/11-i18n-seo.md`、`guide/user/04-site-yaml-config.md`、`guide/dev/config-site-yaml.md`、参数速查；跑 `scripts/checks/config-docs-contract.sh`。
- 依赖：T11.3。

**T11.5 站点迁移清单**
- 改动：托管层 301 示例（Netlify/Cloudflare）、删除旧 `/` 跳转规则、清理 CDN 缓存、重新提交 sitemap、重建 `seo diff` 基线、修改 Analytics 路径筛选和写死 `dist/<lang>` 的脚本、`bukit seo audit --report dist/.bukit/variants/<lang>/seo-report.json`、新增语言前的检查。
- 依赖：T11.4。

**T11.6 插件与主题作者说明**
- 改动：`context.OutputDir` 不等于站点根目录；service worker 作用范围；不要手写语言前缀或 `href="/"`。
- 依赖：T11.5。

**T11.7 升级说明与 CHANGELOG**
- 改动：`CHANGELOG.md`、`CHANGELOG.zh-CN.md`、`CHANGELOG.ms.md` 的 Unreleased：新功能、**行为变化**（根目录新增跳转页；已在 Netlify 等配置 `/` 301 的站点应设 `defaultLanguageRedirect: false`）、alias 和分类跳转页新增标签。
- 依赖：T11.6。

**T11.8 验证闭包策略**
- 改动：确认 T00.2 加入的映射覆盖最终文件集合；有新增则补并跑 owner 自测。
- 依赖：T11.7。

### 阶段 12：验证与统一审查

**T12.1 专项测试汇总**
- 测试：Config、Engine、CLI 三个测试项目中本变更涉及的全部过滤集合；与 T00.1 对照全部通过。
- 依赖：T11.8。

**T12.2 格式与契约检查**
- 测试：`scripts/checks/dotnet-format.sh`、`scripts/checks/public-api-drift.sh`、`openspec validate add-i18n-default-language-root --strict`。
- 依赖：T12.1。

**T12.3 AOT 编译**
- 测试：当前平台 AOT 二进制编译通过（新增代码不得引入反射或不兼容 AOT 的序列化）。
- 依赖：T12.2。

**T12.4 统一审查与收尾**
- 做法：`codex-workflow.py review-scope` 生成范围，做一次只看增量的统一审查；勾选 `openspec/.../tasks.md` 中已取得证据的条目；`metrics report` 汇总。
- 完成标准：无严重或重要问题；任何 full/release gate 只在用户授权后执行。
- 依赖：T12.3。

## 4. 风险与回退

| 风险 | 出现位置 | 应对 |
|---|---|---|
| 重构改变了带前缀布局的行为 | 阶段 2 | T00.1 对照测试必须原样通过；不通过即回退该任务 |
| 根目录跳转页与托管层规则冲突 | 阶段 4 | `defaultLanguageRedirect: false`；CHANGELOG 标为行为变化 |
| 计划清单变短导致误删或全量重建 | T06.1–T06.2 | 先写升级场景测试，再改计划 |
| 根目录语言误删其他语言目录 | 阶段 7、8、10 | T07.7 禁止裁剪同步；T10.3 增量删除测试 |
| 插件越界写入 staging 其他语言目录 | T07.4、T08.2 | 提交前拦截，根因优先报告 |
| 隐私扫描与清单的计入范围在两种布局下不同 | T06.9 | 先与用户确认预期，再用测试固定 |

每个阶段结束时代码都可编译、测试通过，且在默认值下对外行为与前一阶段一致（阶段 3、4 的有意变化除外），因此可以在任意阶段边界暂停或回退。

## 5. 与 openspec `tasks.md` 的对应

| tasks.md 小节 | 本计划任务 |
|---|---|
| 1. 布局策略 | T02.1–T02.7、T05.1 |
| 2. 配置 | T01.1–T01.5 |
| 3. 根目录归属、static 和报告 | T06.1–T06.9 |
| 4. 路由保留前缀 | T07.1–T07.7、T08.2–T08.3 |
| 4a. 默认语言跳转页 | T03.1–T03.3、T04.1–T04.4、T09.1–T09.3 |
| 5. 布局切换和增量构建 | T01.6、T10.1–T10.4 |
| 6. 测试 | 分布在各任务中；CLI（Worktree）为 T08.1–T08.5 |
| 7. 文档与契约 | T11.1–T11.8 |
| 8. 验证 | T12.1–T12.4 |
