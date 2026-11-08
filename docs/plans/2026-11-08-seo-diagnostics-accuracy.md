# SEO 诊断准确性修复（B1–B10）

- 记录日期：2026-11-08
- 来源：silkroute.cc 全量构建（4709 页）的告警分析
- 分支：`feat/media-localization-controls`

## 1. 问题与定位

| # | 问题 | 源码位置 |
|---|---|---|
| B1 | `<head>` 扫描把标签内任意位置的引号都当作属性值开始；未转义的 `"` 会吞掉后面的 `<title>`，导致"标题缺失"误报 | `Bukit.Engine.Primitives/HtmlHeadScanner.cs` `FindTagEnd` |
| B2 | 审计按内部模型校验 JSON-LD，即使页面根本没有输出 `application/ld+json`，仍报数千条"缺作者／发布者" | `MachineReadabilityTrustAuditBuilder.Core.cs`（`ExtractSchemaTypes` 在读取 HTML 之前） |
| B3 | 渲染阶段（`SeoDiagnostics.AnalyzeHtml`）和发布审计各报一次标题问题，级别还不一致 | `Bukit.Engine/SeoDiagnostics.cs`；`MachineReadabilityTrustAuditBuilder.Rules.cs` |
| B4 | 告警汇总对 `publish.audit …`／`seo.audit …` 只按前缀分组，混在一起看不出是哪类问题 | `Bukit.Engine/BuildDiagnosticLogger.cs` `WarningKind` |
| B5 | 标题、描述长度按字符数计算，没有考虑中日韩字符的显示宽度 | `TitleMaxLength`、`DescriptionMaxLength` 及 `SeoDiagnostics` |
| B6 | 自动摘要默认 200 字，描述审计上限 160，默认配置必然告警 | `SeoModelBuilder.cs` 描述回退链 |
| B7 | 页面标题解码 HTML 实体，期望标题不解码，比较不一致 | `HtmlDocumentTitleInspector`／`SeoDocumentTitleResolver` |
| B8 | `doctor` 不检查 `renderMode: theme` 的模板是否使用 `seo.document_title`、`seo.json_ld` | `Bukit.Cli/Commands/DoctorTemplateChecker.cs` |
| B9 | 封面图缺 alt 时没有回退 | 规范层媒体模型 |
| B10 | 日志中出现重复的 `[INFO]` 前缀；增量构建日志显示暂存目录而非实际缓存路径 | 集合告警输出；`IncrementalBuildEngine` 日志 |

## 2. 方案

| # | 方案 |
|---|---|
| B1 | 只有前一个非空白字符是 `=` 时，引号才开始属性值（HTML5 属性值规则）。`FindTagEnd` 被 8 处共用，改为更正确的行为，不改签名 |
| B2 | 先读 HTML。模型有 JSON-LD 但页面没有 `application/ld+json` 时，报一条 `seo.json_ld_not_rendered`（warning），并跳过对模型 JSON-LD 的结构校验 |
| B3 | 渲染阶段的标题、JSON-LD 检查只在 `site.seo.diagnostics: strict` 下执行（strict 需要在渲染时中断构建）；普通模式由发布审计统一报告 |
| B4 | 规则 ID 格式的消息若带 `code=`，按"前缀:code"分组 |
| B5 | 新增显示宽度计算：东亚宽字符和全角字符计 2，其余计 1。阈值不变（标题 60、描述 160），单位改为显示宽度，更接近搜索结果的像素截断 |
| B6 | 不改自动摘要默认值（它也用于列表页，改了会影响所有站点的输出）。当 SEO 描述来自自动摘要或站点描述回退时，按显示宽度 160 截断并加"…"；显式设置的 `seo_desc` 不截断 |
| B7 | 比较时对期望标题做与页面标题相同的规范化（HTML 解码加空白折叠）；输出用的标题不变，避免二次解码 |
| B8 | `site.seo.enabled` 且 `renderMode` 为 `theme` 时，若所有模板都没有引用 `seo.document_title`（或 `seo.title`）、`seo.json_ld`，`doctor` 给出提示 |
| B9 | **不在 Core 实现**：在模型层用标题回退只会消除告警，页面 HTML 仍然没有 alt，等于掩盖问题。应在主题模板中输出 `alt` |
| B10 | 去掉重复前缀；缓存路径改为输出逻辑路径（`BuildTransaction.Logical`） |

## 3. 方案审计

| 检查项 | 结论 |
|---|---|
| 是否改变站点输出 | B6 会改变未设置 `seo_desc` 的页面在 inject 模式下的 meta description 和 JSON-LD 描述（变短）。这是有意的行为变化，需写入变更说明。其余各项只影响诊断 |
| 是否削弱检查 | B3 在普通模式下由审计接管，检查范围不变；strict 模式行为不变。B2 用一条更准确的问题替代大量误报 |
| 共享代码风险 | B1 影响 8 个调用方，新规则对合法 HTML 结果相同，只在畸形引号时更接近浏览器行为。需跑相关项目的全部测试 |
| B5 对中文更严格 | 是。60 宽度约等于 30 个汉字，与搜索结果实际显示一致。这是准确性提升，不是放宽 |
| 公共 API | 不新增配置项、不改公共类型，不应触发 API 基线变化 |
| B9 拒绝理由 | 回退到标题会隐藏真实的无障碍问题，修复应在主题 |

## 4. 测试要求

每项都有单元测试覆盖：B1 引号场景、B2 未输出与已输出两种情况、B3 普通与 strict 两种模式、B4 分组、B5 宽度计算、B6 截断与显式描述不截断、B7 实体比较、B8 doctor 提示、B10 日志文本。之后跑闭包要求的专项测试与 API 漂移检查。

## 5. 执行结果

- B1–B8、B10 已实施；B9 按审计结论不改 Core，留给主题。
- 显示宽度工具：`Bukit.Engine.Primitives/SeoTextWidth.cs`（B5、B6 共用）。
- 有意的行为变化：
  - B2：页面未输出 JSON-LD 时，审计只报 `seo.json_ld_not_rendered`，不再报模型的 schema 字段问题。
  - B3：非 strict 模式下，渲染阶段不再报标题问题，由发布审计统一报告。
  - B5：标题、描述的长度告警按显示宽度计算，中文页面会更早触发。
  - B6：没有 `seo_desc` 时，meta description 截断到显示宽度 160 并以"…"结尾。
- 测试：Engine 3177、Cli 1274（跳过 5）、Rendering 347 全部通过。
- `public-api-drift` 报告的 `TaxonomyConfig.EmptyTerms` 来自提交 c7aebe0a，与本次改动无关，需要在该提交上更新 API 基线。
