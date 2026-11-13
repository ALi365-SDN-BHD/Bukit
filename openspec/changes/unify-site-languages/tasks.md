# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。执行顺序、提交边界和专项测试命令见 `docs/plans/2026-11-14-unify-site-languages-implementation-plan.zh-CN.md`。实施须在用户确认本提案后，作为单独的 Core 任务开展。

## 0. 决策确认

D1–D6 均采用推荐选项，用户已于 2026-11-14 确认。

- [x] D1：不写 `site.languages` 时默认为 `[zh-CN]`。
- [x] D2：删除 `site.language`，出现时报错，不保留别名。
- [x] D3：只有 1 项时不能保留语言前缀。
- [x] D4：`site.defaultLanguageInSubdir` 默认改为 `false`。
- [x] D5：单语言搜索记录的回退语言改为站点语言。
- [x] D6：只有 1 项时默认语言就是这一项（`site.defaultLanguage` 不写时自动取这一项，写了必须等于它）；删除警告 `i18n.config.default_language_ignored`。

## 1. 配置层

- [ ] `ConfigLoader`：`languages` 缺失或为空列表时取默认值；不再从 YAML 读取 `language`；明确默认值是否写回 `SiteConfig.Languages`（影响 `metrics.json`、增量哈希和语言目录）。
- [ ] `ConfigStrictFieldValidator`：从 `site` 允许字段中移除 `language`，出现时给出迁移提示。
- [ ] `SiteConfigValidator`：删除单语言 `site.language` 规则；`defaultLanguage` 只有 1 项时的检查用测试固定。
- [ ] `ConfigJsonSchemaGenerator` 和生成的 JSON Schema 去掉 `site.language`，更新 `defaultLanguageInSubdir` 默认值说明。

## 2. 引擎

- [ ] 语言推导放在 `I18nLanguagePolicy.GetSiteLanguage` 或引擎入口规范化（二选一），不经过加载器构造的单项配置也要得到正确的 `Site.Language`。
- [ ] `I18nLanguagePolicy.GetLanguages` 在少于 2 项时返回空列表。
- [ ] 逐一检查 `GetLanguages` / `IsMultilingual` 的约 20 个源文件调用方，确认只有 1 项时都走单语言分支。
- [ ] `MetricsWriter`、`SiteIdentityAndModesContributor` 改为读取推导后的语言或 `GetLanguages` 结果。
- [ ] `ContentProviderFactory`：先用测试锁定现在单语言站点 `content/<lang>/` 的 slug，再决定只有 1 项时是否传语言目录，确保结果不变。
- [ ] `defaultLanguageInSubdir`、`defaultLanguageRedirect`、`defaultLanguageLegacyRedirects` 只有 1 项时被忽略，并按现有规则给出警告。

## 3. 写出或读取配置的工具

- [ ] Labs：`InitCommand`、`IntentApplier`、`CloneFidelityRunner` 改为写 `languages`；intent 文件的 `site.language` 键（`IntentWizard`、`IntentLoader`、`IntentValidator`）按 D2 的结论映射或删除。
- [ ] Importing：`SiteConfigGenerator` 写 `languages: [<code>]`，`--language` 校验保留。
- [ ] WechatSync：不改代码，确认内容投影清单里每篇文档的 `language` 在单语言站点照常填写。

## 4. 搜索回退语言（D5）

- [ ] `SearchIndexBuilder.RecordLanguageFallback` 单语言时返回站点语言；更新相关测试和搜索文档。

## 5. `defaultLanguageInSubdir` 默认值（D4，单独提交）

- [ ] 默认值改为 `false`；只有 1 项时设为 `true` 记录 `i18n.config.default_language_layout_ignored`。
- [ ] `WarnLayoutSettingsWithoutLanguages` 改为以 `true` 为非默认值；`SiteIdentityAndModesContributor` 只在多语言且为 `true` 时把该设置计入哈希。
- [ ] 规范中随 D4 修改的三条需求、"加第二种语言后默认语言 URL 不变"的测试和本节一起提交。
- [ ] 更新所有依赖默认带前缀的多语言测试和测试站点，需要带前缀的显式写 `true`。

## 6. 测试

- [ ] 10 个 yaml 测试站点和 53 处内嵌 yaml 改为 `languages`。
- [ ] 用单项 `Languages` 构造配置的测试（`I18nRootLayoutTests`、`I18nOutputMergerTests`、`DataFilesPluginTests`、`SiteEngineLifecycleTests`、`MarkdownFolderProviderTests` 等）按新语义改写：要测多语言的改为 2 项。
- [ ] 逐一区分约 112 处 C# `Language = "..."`：配置对象改为 `Languages`，渲染模型 `SiteModel` 保持不变。
- [ ] 新增测试：只有 1 项时输出在根目录且模板 `site.language` 正确；不写 `languages` 时为 `zh-CN`；旧字段报错；只有 1 项且不写 `defaultLanguage` 时默认语言为这一项；只有 1 项时 `defaultLanguage` 不匹配报错；加第二种语言后默认语言 URL 不变。
- [ ] 只运行受影响范围的专项测试；full/release gate 需另行授权。

## 7. 文档

- [ ] 三份 README，`guide/user/02、04、11、14、16`，`guide/labs/import-html-demo.md`，`guide/dev/config-site-yaml.md`、`guide/dev/theme.md`，`docs/seo.md`，`docs/demo_to_bukit_workflow.md`。
- [ ] skills：`bukit-i18n`、`bukit-config`（删除"忘写 `site.language`"的常见错误条目）、`bukit-templating`、`bukit-content`、`bukit-troubleshooting`、`using-bukit`。
- [ ] 中英马三份 CHANGELOG 写明破坏性变更和改法。
