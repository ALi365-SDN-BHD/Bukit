# Source-generate the remaining build-path regular expressions

状态：**草稿**（2026-11-24）。未审计，未获实施批准。本文件只是提案，不授权任何 Core 改动、commit、push 或发布。

## Why

Core 的大部分正则已经使用 `[GeneratedRegex]`（82 处）。但构建路径上还有 14 个静态模式是运行时构造的，其中 12 个带 `RegexOptions.Compiled`：

| 文件 | 数量 | 选项 | 调用频率 |
|---|---|---|---|
| `Bukit.Engine.Audit/PublishAuditRules/SemanticHtmlAuditRules.cs` | 9 | `Compiled` | 发布审计中对每个页面的完整 HTML 执行 |
| `Bukit.Content/Markdown/BasicMarkdownToHtml.cs` | 2 | `Compiled` | 每篇 Markdown 文档 |
| `Bukit.Content/Media/ContentImageRewritePipeline.cs` | 1 | `Compiled` | 每个 `srcset` 值 |
| `Bukit.Engine.Primitives/SeoDocumentTitleResolver.cs` | 1 | 无 `Compiled` | 每个页面标题 |
| `Bukit.Engine.Analytics/AnalyticsManagedBlockFilter.cs` | 1 | 无 `Compiled` | 每个受管分析块标记 |

`RegexOptions.Compiled` 依赖运行时生成 IL。Native AOT 不能动态生成代码，该选项被忽略，这些模式实际由解释器执行，并且在进程启动后首次使用时才解析和构造。`[GeneratedRegex]` 在编译期生成匹配代码，AOT 下是真正编译好的实现，也没有首次构造开销。

`SemanticHtmlAuditRules` 是其中最值得关注的一处：9 个模式（含 `[\s\S]*?` 这类惰性跨行匹配）对每个页面的整份 HTML 运行，位于报告阶段。11-10 计划的实测里，报告阶段在优化后仍约 15 秒。

收益未测量。本变更先测后改：阶段 0 的数据如果显示这些正则在报告阶段中占比很小，变更可以只作为一致性整理保留，或直接关闭。

## What Changes

### 阶段 0：测量（不改仓库文件）

- 用现有的报告阶段分项计时，记录参考站点全量构建中发布审计的耗时。
- 采样确认 `SemanticHtmlAuditRules` 在其中的占比。

### 阶段 1：转换上表的 14 个模式

- 所在类型改为 `partial`，每个模式改为 `[GeneratedRegex]` 的 `partial` 方法或属性，遵循仓库现有写法（例如 `Bukit.Engine/SeoDiagnostics.cs`）。
- **模式文本和 `RegexOptions` 逐字保留**，只去掉 `RegexOptions.Compiled`（对源生成无意义）。不借机改写模式，不新增 `CultureInvariant`。
- 带 `IgnoreCase` 而没有 `CultureInvariant` 的模式（`SemanticHtmlAuditRules` 的 7 个、`ContentImageRewritePipeline` 的 1 个）需要逐个确认源生成后的大小写语义与现状一致。发布的二进制开启了 `InvariantGlobalization`，但测试在 JIT 下运行，两种环境都要覆盖。

### 阶段 2（可选，单独确认）：一次性校验路径上的内联调用

以下位置用字面量模式调用 `Regex.IsMatch` 或 `Regex.Match`，每次构建只执行一到几次，性能收益可以忽略，转换只为统一写法：

- `Bukit.Config/SiteConfigValidator.cs`（4 处）、`Bukit.Config/ProviderValidators.cs`（1 处）
- `Bukit.Cli/Deploy/GitHubPagesDeployProvider.Validation.cs`（1 处）、`GitHubPagesDeployProvider.Git.cs`（1 处）
- `Bukit.Cli/Commands/SeoInsights/SeoInsightsRuleProfileReader.cs`（3 处）
- `Bukit.Notion/Conversion/HtmlToNotionBlockConverter.cs`（1 处）

## Impact

- 受影响代码：阶段 1 的 5 个文件；阶段 2 另外 6 个文件。
- 受影响测试：发布审计语义规则、Markdown 转换、图片改写、SEO 标题、分析块过滤的现有测试；新增一组等价性测试（见 tasks）。
- 公共契约：无公共 API 变化。受影响类型都是 `internal` 或其正则字段为 `private`；`ContentImageRewritePipeline` 与 `BasicMarkdownToHtml` 是 `public` 类型，改为 `partial` 不改变其公共成员，需由 PublicApiDrift 确认。
- 构建输出与报告：逐个比较发布文件的相对路径和字节。报告仅归一化已列明的生成时间、耗时及由这些字段衍生的清单摘要，发布审计的问题列表、计数和其余字段保持一致。
- 跨仓影响：无。
- 用户批准范围：用户 2026-11-24 要求起草本提案。实施、提交与发布需单独确认。

## Non-Goals

- 不转换模式在运行时拼接的正则，它们无法源生成：`Bukit.Engine.Feeds/SitemapPolicy.cs` 的 `ReadAttributeValue`（按属性名拼接）、`Bukit.Engine.Audit/SeoAuditReportWriter.cs`（按路由拼接）、`Bukit.Content/Markdown/MarkdownFolderProvider.cs`（glob 转正则）、`Bukit.Engine.Feeds/RobotsTxtRules.cs`（`NonBacktracking`）、`Bukit.Cli/Commands/DoctorTemplateChecker.cs` 的按标签名拼接。
- 不转换只在开发与诊断命令中使用的正则：`Commands/DocsCheck/*`、`DoctorTemplateChecker`、`DoctorMarkdownChecker`、`DoctorTemplateAnalyzer`、`Bukit.Theme/ThemeDoctor.cs`。
- 不把正则替换成手写扫描器，不改变任何匹配语义。
- 不处理 Labs 与外部插件项目。
