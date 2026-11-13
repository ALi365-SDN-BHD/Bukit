## Context

已在 `main`（`3ba02c9f`）上核实：

- 模式分界：`I18nLanguagePolicy.GetLanguages(site)` 在 `site.Languages` 为空时返回空列表；`I18nOutputLayout.IsMultilingual => _languages.Count > 0`。约 20 个源文件通过 `GetLanguages` 或 `IsMultilingual` 区分单语言和多语言，例如 `SiteEngine`、`RoutePipeline`、`RouteInventoryValidator`、`I18nWorktreeBuild`、`PublicOutputLifecycle`、`I18nRootRedirectWriter`、`SearchIndexBuilder`、`DataFilesPlugin`、`CanonicalContentValidator`（`DoctorCommand` 经由 `RouteInventoryValidator` 间接使用）。只有 `BuildCommand` 的 worktree 隔离判断用的是 `Count > 1`。
- 单语言模式的语言来源：`RoutePipeline` 用 `config.Site.Language` 过滤文档；`I18nLanguagePolicy.GetDefaultLanguage` 在没有多语言时返回 `site.Language`；模板变量 `site.language` 来自 `ScribanSiteModelMapper` 的 `model.Language`。
- 多语言模式下，`SiteEngine` 为每种语言复制一份配置，并把 `Site.Language` 设为该语言（`SiteEngine.cs:351`），所以模板和语言构建流程只认 `Site.Language`。
- 直接读取 `site.Languages`、没有经过 `GetLanguages` 的位置：`SiteConfigValidator`、`ContentProviderFactory`（作为 `MarkdownFolderProvider` 的语言目录，只用于同名文件冲突时的 slug 区分）、`MetricsWriter`、`Incremental/SiteIdentityAndModesContributor`。Labs 的 `IntentApplier` 同时写 `Languages`、`DefaultLanguage` 和 `Language`。
- 会写出 `site.yaml` 的 `language` 字段的工具：Labs `InitCommand`、`IntentApplier`、`CloneFidelityRunner`，Importing `SiteConfigGenerator`。Labs intent 文件另有自己的 `site.language` 键（`IntentWizard`、`IntentLoader`）。`WechatSyncInputLoader` 读取的是内容投影清单里每篇文档的 `language`，与 `site.yaml` 无关。
- `SiteConfig.Language` 是公开的 init 属性，默认 `zh-CN`（`AppConfig.cs:26`），大量配置不经过 `ConfigLoader` 构造（`with` 表达式、测试、SDK 调用方）。
- `defaultLanguageInSubdir`、`defaultLanguageRedirect`、`defaultLanguageLegacyRedirects` 在没有多语言时都被忽略，并分别记录警告（见 `i18n-output-layout` 规范）。
- 搜索记录的回退语言：`SearchIndexBuilder.RecordLanguageFallback` 在多语言时取语言变体的语言，单语言时为 `und`。

## Goals / Non-Goals

目标：

- 站点语言只有一个来源 `site.languages`，"一种语言"只有一种写法。
- 多语言站点里不再有用户能写、但不起作用的语言字段。
- 尽量复用现有的单语言和多语言两条路径，不重写引擎流程，不要求主题修改。

非目标：

- 不改变 2 种及以上语言站点的路由、输出和报告结构（`defaultLanguageInSubdir` 默认值变化除外，见 D4）。
- 不引入新的翻译或 UI 字符串机制。
- 不为旧字段提供兼容别名（没有存量站点）。

## Decisions

### D1. 不写 `site.languages` 时默认为 `[zh-CN]`（待确认）

- 推荐：默认 `[zh-CN]`，与现在 `site.language` 的默认值一致，零配置也能构建，现有文档和示例的默认行为不变。
- 备选 A：改为必填，缺失时校验失败。能根除"非中文站点忘写语言导致内容被过滤"的问题，但最简配置要多写一行。
- 备选 B：默认改为 `[en]`。更符合国际默认，但会改变现有文档里"默认中文"的约定。

### D2. 删除 `site.language`，出现时报错（待确认）

- 推荐：`ConfigStrictFieldValidator` 把 `language` 从 `site` 的允许字段中移除，加一条专门的报错，提示改成 `languages: [<code>]`。不保留别名。
- 备选：保留为 `languages: [<value>]` 的别名，并给出弃用警告。这样两个字段又会并存一段时间，与本变更的目的相反。

### D3. 只有 1 项时不能保留语言前缀（待确认）

- 现在 `languages: [en]` 会得到 `dist/en/`。统一后只有 1 项就是单语言，输出写在 `dist/`。
- 推荐：放弃"单语言但带语言前缀"的能力。需要前缀的站点通常就是多语言站点。
- 备选：在只有 1 项时，允许显式写 `defaultLanguageInSubdir: true` 来保留前缀。这样 D4 就必须用"是否显式设置"区分，规则更复杂。

### D4. `site.defaultLanguageInSubdir` 默认改为 `false`（待确认）

- 原因：站点从 1 种语言加到 2 种时，如果默认值还是 `true`，默认语言的页面会从 `/about/` 搬到 `/en/about/`，所有已有链接都会失效；默认 `false` 时默认语言留在根目录，加语言不改变已有链接。没有存量站点，现在改默认值的成本最低。主流工具（Hugo、Astro、Docusaurus 等）默认也是根目录布局，见归档变更 `2026-11-12-add-i18n-default-language-root` 的 Prior Art。
- 影响：多语言站点默认改为根目录布局，带前缀布局需要显式写 `true`。所有依赖默认带前缀的测试和文档示例需要修改。
- 备选：保持 `true`，只在文档里提醒"以后会加语言的站点建议设为 `false`"。

### D5. 单语言搜索记录的回退语言改为站点语言（待确认）

- 推荐：`RecordLanguageFallback` 在单语言时也返回站点语言，不再返回 `und`。单语言站点的语言总是已知的，前端过滤逻辑在两种模式下一致。
- 备选：保持 `und`，`search.json` 不变。

### D6. 只有 1 项时 `site.defaultLanguage` 只能不写或等于那一项（待确认）

- 推荐：沿用现有规则"`defaultLanguage` 必须在 `languages` 中"，不另加规则。只有 1 项时这条规则自然只接受那一项。
- 现有警告 `i18n.config.default_language_ignored`（写了 `defaultLanguage` 但没有 `languages`）随之删除，因为 `languages` 总会有值（D1）。

### 实现方式：集中在一个判断和一次推导

1. 配置加载：读取 `site.languages`（缺失或为空列表时按 D1 取默认值），按规则清理（去空白、去重）。`SiteConfig.Language` 不再从 YAML 读取。
2. 语言推导不能只放在加载器里：只靠加载器时，测试或 SDK 用 `Site with { Languages = ["en"] }` 构造的配置，`Language` 仍是默认的 `zh-CN`，单语言路径会按 `zh-CN` 过滤并丢掉所有英文页面，正是本变更要消除的问题。做法二选一，实施时定：
   - 新增 `I18nLanguagePolicy.GetSiteLanguage(site)`（只有 1 项时取那一项，否则取 `site.Language`），`GetDefaultLanguage` 和各单语言分支（`RoutePipeline`、`RouteInventoryValidator` 等）都改用它；
   - 或在引擎入口（`SiteEngine`、`WorktreeBuildRun`、`DoctorCommand`）统一规范化一次 `SiteConfig`，之后所有读取 `Site.Language` 的位置（SEO、sitemap、feed、审计、投影等）都拿到正确值。
3. `I18nLanguagePolicy.GetLanguages` 在清理后少于 2 项时返回空列表。这样所有通过它判断模式的位置，只有 1 项时都会走现有的单语言分支。
4. 逐一核实直接读取 `site.Languages` 的 4 个位置：
   - `MetricsWriter`、`SiteIdentityAndModesContributor`：改为读取 `GetLanguages` 的结果或推导出的语言。
   - `ContentProviderFactory`：只有 1 项时是否仍把那一项传给 `MarkdownFolderProvider`，取决于单语言站点的 `content/<lang>/` 目录是否参与同名文件的 slug 区分。实施时用测试锁定现在单语言站点的 slug 结果，确保不变。
   - `SiteConfigValidator`：`languages` 规则保持不变，删除单语言 `site.language` 的规则。
5. `BuildCommand` 的 worktree 判断已是 `Count > 1`，不用改。
6. D4 落地时，`I18nLanguagePolicy.WarnLayoutSettingsWithoutLanguages` 里写死的旧默认值判断要改为以 `true` 为非默认，否则每个单语言站点每次构建都会警告；`SiteIdentityAndModesContributor` 的哈希条件改为"多语言且为 `true`"。D4 拆成单独提交前，`DefaultLanguageInSubdir` 是不可空 bool，无法区分显式 `true` 和默认值，所以只有 1 项时"设为 `true` 给出警告"的场景属于 D4 那次提交。

## Risks / Trade-offs

- `GetLanguages` 的含义从"配置了哪些语言"变为"多语言站点有哪些语言"。审查已逐一检查约 20 个源文件调用方：只有 1 项时它们都只需要 `site.Language`，在推导可靠（见实现方式 2）的前提下不需要新增 `GetConfiguredLanguages`。其中 `CanonicalContentValidator` 会改为全站 id 唯一（比现在 `[en]` 多语言更严格），`DataFilesPlugin` 不再把 `data/<lang>/` 当语言目录，都与现在的单语言模式一致。
- 本变更里"不写 = 少于 2 种"的约定只覆盖 `i18n-output-layout`。在途变更 `add-i18n-shared-assets` 的规范用同样的空/非空措辞，落地顺序在后的一方需要同步改写。
- D4 改默认值后，所有多语言测试的布局预期都要更新，改动量大。可以分两步：先完成字段统一（默认值不变），再单独改默认值，各自提交、各自测试。规范里"Default language prefix is configurable"、"Changing the layout cleans the output safely"、"Default language redirect pages"三条修改和"加第二种语言后默认语言 URL 不变"的测试都属于 D4 那次提交；D4 被否决时这些修改一并撤回。
- 删除 `site.language` 后，Labs 和 Importing 生成的配置、文档里的示例必须同步修改，否则生成的站点无法构建。

## Migration Plan

没有存量站点，不需要迁移工具。实施顺序：

1. 配置层：字段删除、默认值、推导、校验和报错。
2. 引擎：`GetLanguages` 的判断改为 2 项，核实直接读取 `Languages` 的位置。
3. 写出配置的工具：Labs、Importing（WechatSync 只需确认，不改代码）。
4. D5 搜索回退语言。
5. D4 默认值变更（可单独提交）。
6. 测试、文档、CHANGELOG。

## Open Questions

- D1–D6 待用户确认。
- 是否需要在 `bukit doctor` 中对旧字段 `site.language` 给出更友好的迁移提示（配置校验已经会报错）。
