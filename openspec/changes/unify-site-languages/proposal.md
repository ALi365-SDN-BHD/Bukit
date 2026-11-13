# Make `site.languages` the only language setting

## Why

站点语言现在由两个字段决定，分界点也不直观：

- 没写 `site.languages` 时是单语言模式，语言来自 `site.language`（默认 `zh-CN`），输出写在 `dist/`。
- 写了 `site.languages` 时是多语言模式，**哪怕只有一项**（`languages: [en]`）也一样。此时输出写到 `dist/en/`，还会生成首页跳转页和语言变体，`site.language` 不再影响页面输出（`I18nOutputLayout.IsMultilingual => _languages.Count > 0`）。

结果是"只有一种语言"有两种写法，产物不同；多语言站点里还留着一个用户能写、但不起作用的 `site.language`。这个残留值已经造成过问题：合并审计取错译文（PR #151），`metrics.json` 记录的也是这个值而不是站点的真实语言。skills 文档还专门列出一个常见错误：非中文的单语言站点忘了写 `site.language`，结果内容全被过滤掉。

目前没有存量站点，这是改配置模型成本最低的时候。

## What Changes

- **BREAKING**：删除用户配置字段 `site.language`。站点语言只由 `site.languages` 决定：
  - 1 项是单语言站点，产物与现在的单语言模式相同：输出到 `dist/`，没有语言目录、hreflang 和跳转页。
  - 2 项及以上是多语言站点，行为与现在相同。
  - `site.yaml` 里出现 `site.language` 时校验失败，并提示改成 `languages: [<code>]`。
- 不写 `site.languages` 时默认为 `[zh-CN]`，与现在 `site.language` 的默认值一致（决策 D1）。
- 内部模型 `SiteConfig.Language` 和模板变量 `site.language` 保留：单语言时取唯一那一项，多语言时取当前构建的那种语言。推导放在 `I18nLanguagePolicy`（或引擎入口统一规范化一次），不只靠配置加载器，这样不经过加载器构造的配置也能得到正确语言。主题和模板不用改。
- 多语言判断改为"有 2 种及以上语言"，集中在 `I18nLanguagePolicy.GetLanguages()` 这一处：只有 1 项时返回空，现有约 20 个源文件里的单语言分支原样复用。`languages: []` 视同未写；全部为空白的列表仍然报错。
- `site.defaultLanguage` 在只有 1 项时只能不写或等于那一项。
- `site.defaultLanguageInSubdir` 的默认值改为 `false`（默认语言放在根目录），这样单语言站点以后加第二种语言时，已有链接不会变（决策 D4）。只有 1 项时这个字段不起作用。`defaultLanguageRedirect` 和 `defaultLanguageLegacyRedirects` 在只有 1 项时也不起作用，沿用现在"没有多语言时忽略并给出警告"的规则。
- 单语言站点里没有语言的搜索记录，`language` 从 `und` 改为站点语言，与多语言模式一致（决策 D5）。
- 今天加的单语言 `site.language` 格式校验并入 `site.languages` 的已有规则，不再单独存在。
- 会写出 `site.yaml` 的工具改为写 `languages`：Labs 的 `init`、intent、clone-fidelity，Importing 的 `SiteConfigGenerator`。Labs intent 文件自己的 `site.language` 键如何处理（映射为 `languages: [x]` 或删除）在实施时随 D2 一起定。

## Impact

- 受影响代码：
  - `Bukit.Config`：`SiteConfig`、`ConfigLoader`、`ConfigStrictFieldValidator`、`SiteConfigValidator`、`ConfigJsonSchemaGenerator`。
  - `Bukit.Engine`：`I18nLanguagePolicy`、`I18nOutputLayout`；直接读取 `site.Languages` 的 `ContentProviderFactory`（传给 `MarkdownFolderProvider` 的语言目录）、`MetricsWriter`、`Incremental/SiteIdentityAndModesContributor`；`SearchIndexBuilder` 的回退语言。
  - `Bukit.Cli`：`BuildCommand`、`DoctorCommand`、`WorktreeBuildRun` 经由 `GetLanguages` 判断模式，需要逐一核实。
  - Labs：`InitCommand`、`IntentApplier`、`IntentValidator`、`CloneFidelityRunner`。
  - Plugins：`Bukit.Importing/SiteConfigGenerator`。（WechatSync 读取的是内容投影清单里每篇文档的 `language`，不读 `site.yaml`，不受影响，只需确认清单字段照常填写。）
- 规范：`i18n-output-layout` 新增"Site languages are the only language setting"；修改"Default language prefix is configurable"、"Changing the layout cleans the output safely"、"Default language redirect pages"、"Merged search with a root default language"（后三条随 D4、D5 调整）。
- 公共契约：`site.yaml` 删除 `language` 字段（破坏性）；`site.defaultLanguageInSubdir` 默认值从 `true` 改为 `false`（破坏性）；`SiteConfig.Language` 不再从 YAML 读取；单语言 `search.json` 记录的 `language` 从 `und` 变为站点语言；`metrics.json` 的 `language`/`languages` 字段取值随之变化（未写时 `languages` 从 null 变为 `[zh-CN]`，取决于加载器是否把默认值写回）。公共 API 基线可能随之变化，以实施时 api-drift 的结果为准。
- 测试：10 个 yaml 测试站点，53 处测试里内嵌的 `language:`，约 112 处 C# 里的 `Language = "..."`（大部分是渲染模型 `SiteModel`，需要逐一区分是否要改）。现有用 `Languages = ["en"]` 这类单项列表构造配置的测试（如 `I18nRootLayoutTests`、`I18nOutputMergerTests`、`DataFilesPluginTests`、`SiteEngineLifecycleTests`、`MarkdownFolderProviderTests`）含义会从多语言变为单语言，需要逐一改写。所有断言 `dist/<lang>/` 的多语言用例，在默认值改为 `false` 后都要重新确认布局预期。
- 文档：三份 README，`guide/user/02、04、11、14、16`，skills（`bukit-i18n`、`bukit-config`、`bukit-templating`、`bukit-troubleshooting`、`using-bukit`），`guide/labs/import-html-demo.md`，`guide/dev/theme.md`，`docs/seo.md`，`docs/demo_to_bukit_workflow.md`，中英马三份 CHANGELOG。
- 跨仓：没有存量站点，网站仓库不受影响。

## Approval Scope

- 本提案只覆盖 Core、Labs、Plugins 的实现、测试和文档。commit、push、PR、发布和 full/release gate 均需另行授权。
- design.md 中的决策 D1–D6 已于 2026-11-14 由用户确认，均采用推荐选项，规范和任务清单按此编写。
- 实施属于 Core 任务，须作为单独的 Core 任务开展。确认决策不等于授权开始实施。
