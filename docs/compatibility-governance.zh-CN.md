# Bukit 兼容性治理

本文档用于跟踪 Bukit 当前仍在生效的兼容行为、弃用路径以及未来可移除的遗留兼容项，帮助代码、文档、CLI 提示和版本规划保持一致。

## 目的

使用本文档统一回答以下四个治理问题：

1. 哪些兼容行为是项目明确支持的？
2. 哪些旧行为虽然还能运行，但已经进入迁移期？
3. 哪些项目只是给出警告，并不构成真正的运行时兼容？
4. 哪些旧路径应当在未来 major 版本中移除？

## 状态词表

每个兼容项都应使用以下状态之一。

| 状态 | 含义 |
|---|---|
| `supported` | 正式支持的行为，短期内没有移除计划。 |
| `deprecated-but-working` | 运行时仍然可用，但已经进入迁移阶段。 |
| `warned-only` | 系统会提示旧写法，但不保证运行时兼容。 |
| `rejected` | 已不再支持，会被明确拒绝。 |
| `rejected-with-message` | 已拒绝，同时提供明确的迁移错误提示。 |
| `supported-by-policy` | 这不是兼容层，而是当前平台/产品边界，需要文档说清楚。 |
| `deprecated-behavior` | 旧行为仍存在，但不构成正式兼容承诺，应收窄或移除。 |

## 兼容性治理表

| ID | 兼容项 | 当前状态 | 代码位置 | 风险 | 建议动作 | 目标版本 | 建议负责人 |
|---|---|---|---|---|---|---|---|
| `CG-001` | `content.provider` 与 `content.sources` 双轨加载 | `supported` | [ConfigLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigLoader.cs:82), [ContentProviderFactory.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ContentProviderFactory.cs:15) | 中 | 保留；补齐优先级测试矩阵；文档明确新项目优先使用 `content.sources`。 | `v1.x` | Config / Engine |
| `CG-002` | SEO 审计报告从 `.bukit/seo-report.json` 回退到旧 `dist/seo-report.json` | `deprecated-but-working` | [SeoCommand.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/SeoCommand.cs:8) | 低 | 暂时保留；补“仅新路径”“仅旧路径”“两者同时存在”三类优先级测试；文档标出旧路径 sunset。 | `v2.0` 前评估 | CLI |
| `CG-003` | GEO 审计在 publish audit、新 SEO 报告、旧 SEO 报告之间回退查找 | `deprecated-but-working` | [GeoCommand.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/GeoCommand.cs:26) | 低 | 暂时保留；补查找优先级测试；文档写清查找顺序。 | `v2.0` 前评估 | CLI |
| `CG-004` | 无 `theme.yaml` 的旧主题仍可渲染 | `supported` | [ThemeManifestLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Theme/ThemeManifestLoader.cs:7), [BuildCompatibilityTests.cs](/Users/ali/mydev/Git/Github/Bukit/tests/Bukit.Theme.Tests/BuildCompatibilityTests.cs:41) | 中 | 保留，并作为明确兼容承诺写入文档；补更多旧主题/混合主题 fixture。 | `v1.x` | Theme |
| `CG-005` | 主题模板 `fallbackDir` 与默认首页模板回退链 | `supported` | [FileTemplateLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Rendering/Scriban/FileTemplateLoader.cs:15), [ThemeTemplateResolver.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ThemeTemplateResolver.cs:17) | 中 | 保留；补 override、child、parent 三层优先级测试。 | `v1.x` | Rendering / Theme |
| `CG-006` | taxonomy 新 `kinds[]` 与旧 `tags/categories` 模板配置并存 | `deprecated-but-working` | [TaxonomyTemplateResolver.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/TaxonomyTemplateResolver.cs:16) | 中 | 文档标为旧配置兼容；新示例统一引导到 `taxonomy.kinds[]`；计划 major 清理 legacy 分支。 | `v2.0` | Engine |
| `CG-007` | 外部协议插件 handshake `v2 -> v1` 回退 | `supported` | [ProtocolAfterBuildRunner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/Protocol/ProtocolAfterBuildRunner.cs:92) | 中 | 保留；补超时、坏 JSON、`ok=false`、空 stdout 回退测试。 | `v1.x` | Plugin |
| `CG-008` | 外部插件未声明 `capabilities` 时默认放行 | `deprecated-but-working` | [PluginCapabilityEnforcer.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/PluginCapabilityEnforcer.cs:10) | 高 | 明确其状态；先加 warning；后续 major 版本再考虑收紧为 strict。 | `v1.1` 加 warning，`v2.0` 评估收紧 | Plugin / Security |
| `CG-009` | 旧插件参数键 `options.arguments` | `rejected` | [ProcessArgumentsBuilder.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/Protocol/ProcessArgumentsBuilder.cs:16) | 低 | 保持拒绝；文档不要再把它写成兼容项。 | 当前 | Plugin |
| `CG-010` | `site.rssMode` 仍影响 feed 行为 | `deprecated-but-working` | [ConfigLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigLoader.cs:68), [FeedPlugin.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/FeedPlugin.cs:24) | 中 | 给出 sunset 计划；在替代配置说明完整前继续保留。 | `v2.0` | Config / Engine |
| `CG-011` | `site.plugins.rss` 的弃用警告 | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:36) | 中 | 文档明确这是“仅警告”，不是自动运行兼容。 | `v1.1` 文档清理 | Config |
| `CG-012` | `collections.*.rss` 的弃用警告 | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:63) | 中 | 与 `CG-011` 同处理，避免误写为 supported。 | `v1.1` 文档清理 | Config |
| `CG-013` | `site.collection` 到 `site.collections` 的迁移警告 | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:80) | 中 | 文档改成“迁移提示”而不是“仍支持”；若想真兼容，应显式补解析逻辑。 | `v1.1` 文档清理 | Config |
| `CG-014` | `content.notion.rootPageId` 到 `rootBlockId` 的迁移警告 | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:89), [SiteDefaultsApplier.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/SiteDefaultsApplier.cs:77) | 中 | 明确 warning 不等于运行兼容；如果存量用户多，再评估是否补 alias 解析。 | `v1.2` 决策 | Config / Notion |
| `CG-015` | front matter 顶层 `outputPath` | `rejected-with-message` | [RouteGenerator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Routing/RouteGenerator.cs:41) | 低 | 继续保持拒绝并给出迁移提示；在 routing 文档中列为 breaking rule。 | 当前 | Routing |
| `CG-016` | 旧 SEO 字段名 `seodesc` 兜底 | `deprecated-but-working` | [LlmsTxtPlugin.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs:300) | 低 | 暂时保留；文档和示例统一优先 `summary` 与 `seo_desc`。 | `v2.0` 前评估 | SEO |
| `CG-017` | Windows 时区 fallback 映射表 | `supported` | [ConfigValidator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigValidator.cs:323), [TimeZoneCompatibility.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/TimeZoneCompatibility.cs:3) | 低 | 保留；补参数化测试；定期审查映射表。 | `v1.x` | Config |
| `CG-018` | obsolete 的同步 body resolver API 仍被内部调用 | `deprecated-but-working` | [ContentBodyResolver.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine.Abstractions/ContentBodyResolver.cs:18), [DataModuleBuilder.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/DataModuleBuilder.cs:43), [SearchIndexBuilder.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/SearchIndexBuilder.cs:65) | 高 | 先把内部调用迁移到 async，再评估公开 API 移除。 | `v1.2` 内部清理，`v2.0` 视情况移除 | Engine |
| `CG-019` | AOT 构建禁用动态程序集插件，统一 process protocol | `supported-by-policy` | [PluginRegistry.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/PluginRegistry.cs:1), [Bukit.Engine.csproj](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Bukit.Engine.csproj:17) | 中 | 明确写成产品边界，不要描述成兼容层。 | `v1.1` 文档清理 | Engine / Docs |
| `CG-020` | import 流程在输入缺失时默认启用较宽的 `pageTypes` 集合 | `deprecated-behavior` | [SiteConfigGenerator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/SiteConfigGenerator.cs:28) | 中 | 先评估 fixture 影响，再改为更窄默认或显式策略。 | `v1.3` | Import |

## 当前治理优先级

### P0：先修代码与文档的真相不一致

这些项应优先澄清，因为它们最容易误导用户和维护者：

- `CG-011` `site.plugins.rss`
- `CG-012` `collections.*.rss`
- `CG-013` `site.collection`
- `CG-014` `content.notion.rootPageId`

预期结果：

- 文档不再把 warning-only 项写成运行兼容。
- 迁移说明与解析器真实行为一致。

### P1：补回归测试

优先级最高的兼容测试包括：

1. `content.sources` 与 `content.provider` 优先级矩阵
2. SEO 报告路径回退优先级
3. GEO 报告路径回退优先级
4. 插件 handshake `v2 -> v1` 回退场景
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
4. 如果旧路径仍可运行，文档应同时写出首选新路径和 sunset 计划。

## 建议的 Issue Checklist

- [ ] 将本文档加入维护者文档入口或索引
- [ ] 按本文状态词表统一 config / routing / plugin 文档表述
- [ ] 补 `content.sources` / `content.provider` 优先级测试
- [ ] 补 SEO / GEO 审计路径 fallback 测试
- [ ] 补协议 handshake fallback 测试
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

