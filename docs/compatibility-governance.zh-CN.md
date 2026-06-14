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

## 兼容性治理表

| ID | 兼容项 | 当前状态 | 代码位置 | 风险 | 建议动作 | 目标版本 | 建议负责人 |
|---|---|---|---|---|---|---|---|
| `CG-001` | `content.provider` 已移除，`content.sources[]` 是唯一内容源入口 | `rejected-with-message` | [ConfigLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigLoader.cs:82), [ContentProviderFactory.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ContentProviderFactory.cs:15) | 中 | 保持拒绝；文档和 AI prompt 只能生成 `content.sources[]`；测试必须覆盖 `content.provider` fail fast 与迁移提示。 | `current` | Config / Engine |
| `CG-002` | SEO 审计不再发现根路径 `dist/seo-report.json` | `rejected-with-message` | [SeoCommand.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/SeoCommand.cs:8) | 低 | 默认只发现 `.bukit/seo-report.json`，并以 `.bukit/publish-audit-report.json` 作为次级兼容输入。不要依赖根路径输出，需重新 build。 | `current` | CLI |
| `CG-003` | GEO 审计不再发现根路径 `dist/seo-report.json` | `rejected-with-message` | [GeoCommand.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/GeoCommand.cs:26) | 低 | 默认只发现 `.bukit/seo-report.json`，并以 `.bukit/publish-audit-report.json` 作为次级兼容输入。不要依赖根路径输出，需重新 build。 | `current` | CLI |
| `CG-004` | 无 `theme.yaml` 的旧主题仍可渲染 | `rejected-with-message` | [ThemeManifestLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Theme/ThemeManifestLoader.cs:7), [ThemeBootstrapper.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ThemeBootstrapper.cs:11), [BuildCompatibilityTests.cs](/Users/ali/mydev/Git/Github/Bukit/tests/Bukit.Theme.Tests/BuildCompatibilityTests.cs:121) | 高 | 构建与 doctor 阶段要求 `theme.yaml`，否则返回明确错误提示；保留迁移指引用于生成或补齐清单。 | `current` | Theme |
| `CG-005` | 主题模板 `fallbackDir` 与默认首页模板回退链 | `supported` | [FileTemplateLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Rendering/Scriban/FileTemplateLoader.cs:15), [ThemeTemplateResolver.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ThemeTemplateResolver.cs:17) | 中 | 保留；`FileTemplateLoaderTests` 已补齐 override/child/parent 三层优先级回退验证。 | `v1.x` | Rendering / Theme |
| `CG-006` | taxonomy 新 `kinds[]` 与旧 `tags/categories` 模板配置并存 | `removed` | [TaxonomyTemplateResolver.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/TaxonomyTemplateResolver.cs:16) | 中 | 1.0 文档与 starter 仅记录 `taxonomy.kinds[]` 为可宣告路径，保留 legacy 仅作迁移背景说明。 | `current` | Engine |
| `CG-007` | 外部协议插件 handshake `v2 -> v1` 回退 | `rejected-with-message` | [ProtocolAfterBuildRunner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/Protocol/ProtocolAfterBuildRunner.cs:92), [ProtocolHandshakeNegotiator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/Protocol/ProtocolHandshakeNegotiator.cs:23) | 中 | 强制 v2 握手 schema，拒绝 v1 响应并给迁移指引。 | `current` | Plugin |
| `CG-008` | 外部插件未声明 `capabilities` 时默认放行 | `rejected-with-message` | [PluginCapabilityEnforcer.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/PluginCapabilityEnforcer.cs:10) | 高 | 缺少 `capabilities` 直接拒绝并给迁移提示：按 hook 补齐 `derive-pages` 或 `emit-outputs`。 | `current` | Plugin / Security |
| `CG-009` | 旧插件参数键 `options.arguments` | `rejected` | [ProcessArgumentsBuilder.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/Protocol/ProcessArgumentsBuilder.cs:16) | 低 | 保持拒绝；文档不要再把它写成兼容项。 | `current` | Plugin |
| `CG-010` | `site.rssMode` 仍影响 feed 行为 | `rejected-with-message` | [ConfigLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigLoader.cs:68), [FeedPlugin.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/FeedPlugin.cs:24) | 中 | 1.0 下保持拒绝；迁移指引改为 `site.feed.formats`。 | `current` | Config / Engine |
| `CG-011` | `site.plugins.rss` 的弃用警告 | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:36) | 中 | 文档明确这是“仅警告”，不是自动运行兼容。 | `v1.1` 文档清理 | Config |
| `CG-012` | `collections.*.rss` 的弃用警告 | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:63) | 中 | 与 `CG-011` 同处理，避免误写为 supported。 | `v1.1` 文档清理 | Config |
| `CG-013` | `site.collection` 到 `site.collections` 的迁移警告 | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:80) | 中 | 文档改成“迁移提示”而不是“仍支持”；若想真兼容，应显式补解析逻辑。 | `v1.1` 文档清理 | Config |
| `CG-014` | `content.notion.rootPageId` 到 `rootBlockId` 的迁移警告 | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:89), [SiteDefaultsApplier.Content.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/SiteDefaultsApplier.Content.cs:7) | 中 | 明确 warning 不等于运行兼容；如果存量用户多，再评估是否补 alias 解析。 | `v1.2` 决策 | Config / Notion |
| `CG-015` | front matter 顶层 `outputPath` | `rejected-with-message` | [RouteGenerator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Routing/RouteGenerator.cs:41) | 低 | 继续保持拒绝并给出迁移提示；在 routing 文档中列为 breaking rule。 | `current` | Routing |
| `CG-016` | 旧 SEO 字段名 `seodesc` 兜底 | `removed` | [LlmsTxtPlugin.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs:300) | 低 | 文档和示例统一使用 `summary` 与 `seo_desc` 作为 1.0 主字段。 | `current` | SEO |
| `CG-017` | Windows 时区 fallback 映射表 | `supported` | [ConfigValidator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigValidator.cs:323), [TimeZoneCompatibility.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/TimeZoneCompatibility.cs:3) | 低 | 保留；补参数化测试；定期审查映射表。 | `v1.x` | Config |
| `CG-018` | obsolete 的同步 body resolver API 仍被内部调用 | `deprecated-behavior` | [ContentBodyResolver.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine.Abstractions/ContentBodyResolver.cs:18), [DataModuleBuilder.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/DataModuleBuilder.cs:43), [SearchIndexBuilder.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/SearchIndexBuilder.cs:65) | 高 | 先把内部调用迁移到 async，再评估公开 API 移除。 | `v1.2` 内部清理，`v2.0` 视情况移除 | Engine |
| `CG-019` | AOT 构建禁用动态程序集插件，统一 process protocol | `supported-by-policy` | [PluginRegistry.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/PluginRegistry.cs:1), [Bukit.Engine.csproj](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Bukit.Engine.csproj:17) | 中 | 明确写成产品边界，不要描述成兼容层。 | `v1.1` 文档清理 | Engine / Docs |
| `CG-020` | import 流程在输入缺失时默认启用较宽的 `pageTypes` 集合 | `deprecated-behavior` | [SiteConfigGenerator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/SiteConfigGenerator.cs:28) | 中 | 先评估 fixture 影响，再改为更窄默认或显式策略。 | `v1.3` | Import |

## 当前治理优先级

### P0：先修代码与文档的真相不一致

这些项应优先澄清，因为它们最容易误导用户和维护者：

- `CG-004` 无 `theme.yaml` 的旧主题
- `CG-007` 外部协议 `v1` 回退
- `CG-008` 未声明 `capabilities` 的外部插件
- `CG-011` `site.plugins.rss`
- `CG-012` `collections.*.rss`
- `CG-013` `site.collection`
- `CG-014` `content.notion.rootPageId`

预期结果：

- 文档不再把 warning-only 项写成运行兼容。
- 迁移说明与解析器真实行为一致。

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
- [ ] 决定 `rootPageId` 保持 warning-only 还是新增 alias 解析
- [ ] 为 `site.rssMode` 发布 sunset 版本计划
- [ ] 替换内部同步 `ContentBodyResolver.GetHtml()` 调用

## 复审时机

出现以下情况时，应重新检查本治理表：

- 新增或移除了某个弃用警告
- 某个解析器开始接受或拒绝旧字段
- 开始制定新的 major 版本计划
- 更新了 config、theme、routing、plugin 或 import 相关文档
