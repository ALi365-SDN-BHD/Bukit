# Bukit 兼容性治理

本文档用于跟踪 Bukit 当前仍在生效的兼容行为、弃用路径以及未来可移除的遗留兼容项，帮助代码、文档、CLI 提示和版本规划保持一致。

## 目的

使用本文档统一回答以下四个治理问题：

1. 哪些兼容行为是项目明确支持的？
2. 哪些旧行为在 1.0 下被明确拒绝（含或不含迁移提示）？
3. 哪些项目只是给出警告，并不构成真正的运行时兼容？
4. 哪些旧路径应当在未来 major 版本中移除？

## 状态词表

每个兼容项都应使用以下状态之一。

| 状态 | 含义 |
|---|---|
| `supported` | 正式支持的行为，短期内没有移除计划。 |
| `removed` | 不属于 1.0 公开契约。默认不承诺运行时支持，除非文档明确记为例外迁移路径。 |
| `warned-only` | 系统会提示旧写法，但不保证运行时兼容。 |
| `rejected` | 已不再支持，会被明确拒绝。 |
| `rejected-with-message` | 已拒绝，同时提供明确的迁移错误提示。 |
| `supported-by-policy` | 这不是兼容层，而是当前平台/产品边界，需要文档说清楚。 |
| `deprecated-behavior` | 旧行为仍存在，但不构成正式用户-facing 兼容承诺，需逐步收窄或移除。 |

## 正确性与安全收紧（2026-07-19）

以下修复落实既有 1.0 意图，不新增兼容 alias、schema 版本、插件协议字段或持久化迁移。

| Finding | 可观察变化 | 兼容性分类 |
|---|---|---|
| F-01 | 危险、项目外、symlink、`.git` 和无 marker 非空 clean target 会被一致拒绝。 | 有意安全收紧；不保留旧的不安全删除行为。 |
| F-02 | 默认 search title/snippet 中的 HTML 会按文本显示。 | 安全修复；生成 DOM 的内部构造方式不是公开 ABI。 |
| F-03 | 跨类别及 file/descendant 输出冲突会在发布写入前确定失败。 | 正确性收紧；不支持旧的时序依赖覆盖。 |
| F-04 | 默认发布 walker 一致跳过目录 symlink/reparse point。 | 落实既有 `followSymlinks: false` 策略；不新增全局 follow 能力。 |
| F-05 | 同进程模板决策读取当前 manifest/root/include/layout 内容。 | 缓存正确性修复；manifest 形状与公开 capability model 不变。 |
| F-06 | 既有 `site.search.maxContentLength` 对所有 Core search representation 生效。 | 落实既有字段语义；默认 `8000` 与 schema minimum `1` 不变。 |
| F-07 | 既有 `content.media.maxConcurrency` 在 operation/store 范围限制实际本地化下载。 | 落实既有字段语义；默认值与 YAML 形状不变。 |
| F-08 | 既有 build-report 字段包含当前诊断计数和 public output inventory。 | 值正确性修复；冻结的 `build-report.v1` 形状不变。 |

这些收紧属于 patch-compatible bug fix。依赖危险删除、非确定性覆盖、默认 false 下跟随目录 link、陈旧模板决策或配置不生效的站点，依赖的是文档契约之外的行为。

## 兼容性治理表

| ID | 兼容项 | 当前状态 | 代码位置 | 风险 | 建议动作 | 目标版本 | 建议负责人 |
|---|---|---|---|---|---|---|---|
| `CG-001` | `content.provider` 已移除，`content.sources[]` 是唯一内容源入口 | `rejected-with-message` | [ConfigLoader.cs](../src/Bukit-Core/Bukit.Config/ConfigLoader.cs), [ContentProviderFactory.cs](../src/Bukit-Core/Bukit.Engine/ContentProviderFactory.cs) | 中 | 保持拒绝；文档和 AI prompt 只能生成 `content.sources[]`；测试必须覆盖 `content.provider` fail fast 与迁移提示。 | `current` | Config / Engine |
| `CG-002` | SEO 审计不再发现根路径 `dist/seo-report.json` | `rejected-with-message` | [SeoCommand.cs](../src/Bukit-Core/Bukit.Cli/Commands/SeoCommand.cs) | 低 | 默认只发现 `.bukit/seo-report.json`，并以 `.bukit/publish-audit-report.json` 作为次级兼容输入。不要依赖根路径输出，需重新 build。 | `current` | CLI |
| `CG-003` | GEO 审计不再发现根路径 `dist/seo-report.json` | `rejected-with-message` | [GeoCommand.cs](../src/Bukit-Core/Bukit.Cli/Commands/GeoCommand.cs) | 低 | 默认只发现 `.bukit/seo-report.json`，并以 `.bukit/publish-audit-report.json` 作为次级兼容输入。不要依赖根路径输出，需重新 build。 | `current` | CLI |
| `CG-004` | 无 `theme.yaml` 的旧主题仍可渲染 | `rejected-with-message` | [ThemeManifestLoader.cs](../src/Bukit-Core/Bukit.Theme/ThemeManifestLoader.cs), [ThemeBootstrapper.cs](../src/Bukit-Core/Bukit.Engine/ThemeBootstrapper.cs), [BuildCompatibilityTests.cs](../tests/Bukit.Theme.Tests/BuildCompatibilityTests.cs) | 高 | 构建与 doctor 阶段要求 `theme.yaml`，否则返回明确错误提示；保留迁移指引用于生成或补齐清单。 | `current` | Theme |
| `CG-005` | 主题模板 `fallbackDir` 与默认首页模板回退链 | `supported` | [FileTemplateLoader.cs](../src/Bukit-Core/Bukit.Rendering/Scriban/FileTemplateLoader.cs), [ThemeTemplateResolver.cs](../src/Bukit-Core/Bukit.Engine/ThemeTemplateResolver.cs) | 中 | 保留；`FileTemplateLoaderTests` 已补齐 override/child/parent 三层优先级回退验证。 | `v1.x` | Rendering / Theme |
| `CG-006` | taxonomy 新 `kinds[]` 与旧 `tags/categories` 模板配置并存 | `removed` | [TaxonomyTemplateResolver.cs](../src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/TaxonomyTemplateResolver.cs) | 中 | 1.0 文档与 starter 仅记录 `taxonomy.kinds[]` 为可宣告路径，保留 legacy 仅作迁移背景说明。 | `current` | Engine |
| `CG-007` | 外部协议插件 `v1` handshake 回退 | `rejected-with-message` | [PluginProtocolClient.cs](../src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs), [PluginProtocolConstants.cs](../src/Bukit-Core/Bukit.Plugin.Abstractions/Protocol/PluginProtocolConstants.cs) | 中 | 强制 `bukit-plugin-v1` 协议消息，拒绝不支持的协议响应并给迁移指引。 | `current` | Plugin |
| `CG-008` | 外部插件命令元数据未在 manifest 中声明 | `rejected-with-message` | [PluginCommandManifestValidator.cs](../src/Bukit-Core/Bukit.PluginHost/PluginCommandManifestValidator.cs), [PluginSchemaContractTests.cs](../tests/Bukit.PluginHost.Tests/PluginSchemaContractTests.cs) | 高 | 运行时命令元数据必须在 `plugin.yaml` 中声明；未声明的运行时命令、alias、argument 和 option 均会验证失败。 | `current` | Plugin / Security |
| `CG-009` | 旧插件参数键 `options.arguments` | `rejected` | [PluginManifestLoader.cs](../src/Bukit-Core/Bukit.PluginHost/PluginManifestLoader.cs), [PluginCommandManifestValidator.cs](../src/Bukit-Core/Bukit.PluginHost/PluginCommandManifestValidator.cs) | 低 | 保持拒绝；文档应记录 `commands[].arguments` 和 `commands[].options`，不要再把 `options.arguments` 写成兼容项。 | `current` | Plugin |
| `CG-010` | `site.rssMode` 仍影响 feed 行为 | `rejected-with-message` | [ConfigLoader.cs](../src/Bukit-Core/Bukit.Config/ConfigLoader.cs), [FeedPlugin.cs](../src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/FeedPlugin.cs) | 中 | 1.0 下保持拒绝；迁移指引改为 `site.feed.formats`。 | `current` | Config / Engine |
| `CG-011` | `site.plugins.<name>` 仍是 Core 内置插件开关 | `supported` | [SiteDefaultsApplier.Theme.cs](../src/Bukit-Core/Bukit.Config/SiteDefaultsApplier.Theme.cs), [built-in-plugins.md](../guide/dev/built-in-plugins.md) | 中 | 只把它记录为 Core 内置插件开关。不要描述成外部进程插件配置；外部插件配置属于 `.bukit/plugins.yaml`。 | `current` | Config / Engine |
| `CG-012` | 旧 `site.collections.*.rss` 快捷写法 | `rejected` | [ConfigStrictFieldValidator.cs](../src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs), [ConfigLoaderTests.cs](../tests/Bukit.Config.Tests/ConfigLoaderTests.cs) | 中 | 保持由严格配置字段校验拒绝。集合 feed 输出使用 `site.collections.*.output.rss`。 | `current` | Config |
| `CG-013` | 单数 `site.collection` 配置 | `rejected` | [ConfigStrictFieldValidator.cs](../src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs), [ConfigLoader.cs](../src/Bukit-Core/Bukit.Config/ConfigLoader.cs) | 中 | 保持由严格配置字段校验拒绝。使用 `site.collections`。 | `current` | Config |
| `CG-014` | `rootPageId`/`rootBlockId` 等旧 Notion 页面根字段 | `rejected` | [ConfigStrictFieldValidator.cs](../src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs), [SiteDefaultsApplier.Content.cs](../src/Bukit-Core/Bukit.Config/SiteDefaultsApplier.Content.cs), [ProviderValidators.cs](../src/Bukit-Core/Bukit.Config/ProviderValidators.cs) | 中 | 保持当前 `content.sources[].notion.databaseId` 契约。不要把页面根 alias 写成 warning-only 兼容。 | `current` | Config / Notion |
| `CG-015` | front matter 顶层 `outputPath` | `rejected-with-message` | [RouteGenerator.cs](../src/Bukit-Core/Bukit.Routing/RouteGenerator.cs) | 低 | 继续保持拒绝并给出迁移提示；在 routing 文档中列为 breaking rule。 | `current` | Routing |
| `CG-016` | 旧 SEO 字段名 `seodesc` 兜底 | `removed` | [LlmsTxtPlugin.cs](../src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs) | 低 | 文档和示例统一使用 `summary` 与 `seo_desc` 作为 1.0 主字段。 | `current` | SEO |
| `CG-017` | Windows 时区 fallback 映射表 | `supported` | [ConfigValidator.cs](../src/Bukit-Core/Bukit.Config/ConfigValidator.cs), [TimeZoneCompatibility.cs](../src/Bukit-Core/Bukit.Config/TimeZoneCompatibility.cs) | 低 | 保留；补参数化测试；定期审查映射表。 | `v1.x` | Config |
| `CG-018` | obsolete 的同步 body resolver API 仍被内部调用 | `deprecated-behavior` | [ContentBodyResolver.cs](../src/Bukit-Core/Bukit.Engine.Abstractions/ContentBodyResolver.cs), [DataModuleBuilder.cs](../src/Bukit-Core/Bukit.Engine/DataModuleBuilder.cs), [SearchIndexBuilder.cs](../src/Bukit-Core/Bukit.Engine/SearchIndexBuilder.cs) | 高 | 先把内部调用迁移到 async，再评估公开 API 移除。 | `v1.2` 内部清理，`v2.0` 视情况移除 | Engine |
| `CG-019` | AOT 构建禁用动态程序集插件，统一 process protocol | `supported-by-policy` | [PluginRegistry.cs](../src/Bukit-Core/Bukit.Engine/Plugins/PluginRegistry.cs), [Bukit.Engine.csproj](../src/Bukit-Core/Bukit.Engine/Bukit.Engine.csproj) | 中 | 明确写成产品边界，不要描述成兼容层。 | `v1.1` 文档清理 | Engine / Docs |
| `CG-020` | import 流程在输入缺失时默认启用较宽的 `pageTypes` 集合 | `deprecated-behavior` | [SiteConfigGenerator.cs](../src/Bukit-Plugins/Bukit.Importing/SiteConfigGenerator.cs) | 中 | 先评估 fixture 影响，再改为更窄默认或显式策略。 | `v1.3` | Import |
| `CG-021` | CLR public 可见性不等于通用 SDK 支持承诺 | `supported-by-policy` | [Public API Governance](../guide/dev/public-api-governance.md), [public/protected baseline](governance/bukit-core-public-api-baseline.v1.json) | 中 | 接受任何 public/protected drift 前，必须复审 baseline，并为每个变更 type/member 指定 owner、classification、compatibility、migration horizon 和 reason。 | `current` | Core / Docs |

## 当前治理优先级

### P0：先修代码与文档的真相不一致

这些项应优先澄清，因为它们最容易误导用户和维护者：

- `CG-004` 无 `theme.yaml` 的旧主题
- `CG-007` 外部协议 `v1` 回退
- `CG-008` 未声明 `capabilities` 的外部插件
- `CG-012` 旧 `site.collections.*.rss`
- `CG-013` 单数 `site.collection`
- `CG-014` 旧 Notion 页面根字段

预期结果：

- 文档不再把 warning-only 项写成运行兼容。
- 迁移说明与解析器真实行为一致。
- `site.plugins.<name>` 明确只属于 Core 内置插件开关，不属于外部进程插件配置。

### P1：补回归测试

优先级最高的兼容测试包括：

1. `content.provider` 拒绝矩阵与 `content.sources[]` 接受矩阵
2. SEO 报告路径发现，且不再回退根目录报告
3. GEO 报告路径发现，且不再回退根目录旧报告
4. 插件 handshake `v1` 拒绝场景
5. 未声明 `capabilities` 的行为
6. Windows 时区 fallback 映射

### P2：准备移除计划

以下项目应进入明确的 sunset 规划：

- `CG-006` taxonomy legacy 模板配置
- `CG-010` `site.rssMode`
- `CG-018` obsolete 同步 body API
- `CG-020` import 宽默认行为

## 文档更新规则

更新 Bukit 文档时请遵循以下规则：

1. 只有在运行时真实兼容时，才能把某项写成“兼容”。
2. 如果代码只是发出警告，应标记为 `warned-only`。
3. 如果代码拒绝旧写法但给出迁移提示，应标记为 `rejected-with-message`。
4. 如果旧路径仅保留为迁移语境，说明迁移边界并明确 1.0 用户指引，不把它当作可依赖路径。

## 建议的 Issue Checklist

- [ ] 将本文档加入维护者文档入口或索引
- [ ] 按本文状态词表统一 config / routing / plugin 文档表述
- [ ] 补 `content.provider` 拒绝测试与 `content.sources[]` 接受测试
- [ ] 补 SEO / GEO 审计路径发现测试
- [ ] 补协议 handshake 拒绝测试（`version` not `2`、`ok=false`、无效 JSON、空 stdout）
- [ ] 补插件 `capabilities` 缺省行为测试
- [ ] 补 Windows 时区 fallback 参数化测试
- [ ] 确认旧集合 feed 快捷写法继续由严格字段校验拒绝
- [ ] 确认单数 `site.collection` 继续由严格字段校验拒绝
- [ ] 确认旧 Notion 页面根字段继续被拒绝，并以 `databaseId` 为准
- [ ] 为 `site.rssMode` 发布 sunset 版本计划
- [ ] 替换内部同步 `ContentBodyResolver.GetHtml()` 调用

## 复审时机

出现以下情况时，应重新检查本治理表：

- 新增或移除了某个弃用警告
- 某个解析器开始接受或拒绝旧字段
- 开始制定新的 major 版本计划
- 更新了 config、theme、routing、plugin 或 import 相关文档
