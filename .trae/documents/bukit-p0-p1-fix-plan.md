# Bukit P0/P1 深度修复实施计划

## 总体架构分析

### 当前数据流问题

```
bukit import html-demo <dir> --content-source notion
│
├─ ContentDraftWriter.Write()       ❌ 始终写 .md 到 content/
├─ SeedGenerator.Generate()         ✅ 写 notion-seed/*.json
└─ SiteConfigGenerator.Generate()   ❌ 始终生成 content.provider: markdown
```

**根因**：`ContentDraftWriter` 和 `SiteConfigGenerator` 完全不感知 `ContentSource` 参数。`ContentSource` 只在 `SeedGenerator` 中起作用。

### 相关文件清单

| 文件                           | 路径                                                         | 关键行           |
| ---------------------------- | ---------------------------------------------------------- | ------------- |
| ContentDraftWriter           | `src/Bukit.Importing/ContentDraftWriter.cs`                | L7-L72        |
| SiteConfigGenerator          | `src/Bukit.Importing/SiteConfigGenerator.cs`               | L7-L64        |
| HtmlDemoImporter             | `src/Bukit.Importing/HtmlDemoImporter.cs`                  | L1-L345       |
| NotionSeedPusher             | `src/Bukit.Cli/Commands/NotionSeedPusher.cs`               | L1-L243       |
| NotionCommand                | `src/Bukit.Cli/Commands/NotionCommand.cs`                  | L1-L78        |
| ContentExtractor             | `src/Bukit.Importing/ContentExtractor.cs`                  | L1-L386       |
| TemplateBodyTransformer      | `src/Bukit.Importing/TemplateBodyTransformer.cs`           | L1-L192       |
| HtmlDocumentParser           | `src/Bukit.Importing/HtmlDocumentParser.cs`                | (regex-based) |
| PageClassifier               | `src/Bukit.Importing/PageClassifier.cs`                    | L1-L65        |
| ImportReportWriter           | `src/Bukit.Importing/ImportReportWriter.cs`                | L1-L222       |
| ImportModels                 | `src/Bukit.Importing/ImportModels.cs`                      | L1-L183       |
| BukitCliSpecs                | `src/Bukit.Cli/Cli/BukitCliSpecs.cs`                       | L253-L309     |
| ImportCommand                | `src/Bukit.Cli/Commands/ImportCommand.cs`                  | L81-L238      |
| NotionApiUrls                | `src/Bukit.Shared/Notion/NotionApiUrls.cs`                 | L1-L14        |
| ContentConfig                | `src/Bukit.Config/AppConfig.cs`                            | L205-L274     |
| ProviderValidators           | `src/Bukit.Config/ProviderValidators.cs`                   | L23-L111      |
| NotionDatabaseSchemaResolver | `src/Bukit.Content/Notion/NotionDatabaseSchemaResolver.cs` | L1-L93        |
| ContentProviderFactory       | `src/Bukit.Engine/ContentProviderFactory.cs`               | L1-L223       |
| NotionContentProvider        | `src/Bukit.Content/Notion/NotionContentProvider.cs`        | L1-L342       |

***

## P0-1: `--content-source notion` 修复（禁止写 .md，使用 Notion provider）

### 当前行为

1. `ContentDraftWriter.Write()` 无条件写 `content/*.md` 到 `sites/<theme>/content/`
2. `SiteConfigGenerator.Generate()` 硬编码 `content.provider: markdown`

### 目标行为

当 `--content-source notion` 时：

* **不写** `sites/<theme>/content/*.md`

* `site.yaml` 使用 Notion provider 配置

* notion-seed 目录作为待推送内容

```yaml
content:
  provider: notion
  notion:
    databaseId: ${NOTION_DATABASE_ID}
    tokenEnv: NOTION_TOKEN
```

### 实施步骤

#### Step 1.1: 添加 `--no-markdown-draft` CLI 选项

* **文件**: `src/Bukit.Cli/Cli/BukitCliSpecs.cs`

* **位置**: L253 附近，`--content-source` 后添加

* **内容**:

  ```csharp
  new CliOptionSpec("--no-markdown-draft",
      "跳过生成 content/*.md 草稿（当使用 --content-source notion 时建议开启）",
      CliOptionType.Flag),
  ```

#### Step 1.2: 解析 `--no-markdown-draft` 参数

* **文件**: `src/Bukit.Cli/Commands/ImportCommand.cs`

* **位置**: `HtmlDemoAsync()` 方法，L81 附近

* **改动**:

  ```csharp
  var noMarkdownDraft = command.GetBool("--no-markdown-draft");
  // 当 contentSource 为 notion 时，默认启用 no-markdown-draft
  if (!command.Options.ContainsKey("--no-markdown-draft") && 
      contentSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
      noMarkdownDraft = true;
  ```

#### Step 1.3: `HtmlDemoImportOptions` 增加字段

* **文件**: `src/Bukit.Importing/ImportModels.cs`

* **位置**: `HtmlDemoImportOptions` record

* **改动**: 添加 `public bool NoMarkdownDraft { get; init; }`

#### Step 1.4: `ContentDraftWriter` 支持跳过

* **文件**: `src/Bukit.Importing/ContentDraftWriter.cs`

* **改动**: `Write()` 方法开头检查 `options.NoMarkdownDraft`，若为 true 则跳过所有写入：

  ```csharp
  internal static void Write(HtmlDemoImportOptions options, ExtractedContent content)
  {
      if (options.NoMarkdownDraft) 
      {
          Console.WriteLine("  Content draft 已跳过（--no-markdown-draft / notion mode）");
          return;
      }
      // ... 原有逻辑
  }
  ```

#### Step 1.5: `SiteConfigGenerator` 根据 ContentSource 生成不同 provider

* **文件**: `src/Bukit.Importing/SiteConfigGenerator.cs`

* **改动**: 根据 `options.ContentSource` 动态生成 content 配置：

  ```csharp
  if (options.ContentSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
  {
      sb.AppendLine("content:");
      sb.AppendLine("  provider: notion");
      sb.AppendLine("  notion:");
      sb.AppendLine("    databaseId: ${NOTION_DATABASE_ID}");
      sb.AppendLine("    tokenEnv: NOTION_TOKEN");
      sb.AppendLine("    filterProperty: Published");
      sb.AppendLine("    filterType: checkbox_true");
      sb.AppendLine("    sortProperty: Title");
      sb.AppendLine("    sortDirection: ascending");
  }
  else
  {
      // 原有 markdown provider 逻辑
  }
  ```

#### Step 1.6: `HtmlDemoImporter.Import()` 调用链适配

* **文件**: `src/Bukit.Importing/HtmlDemoImporter.cs`

* **位置**: Import 流程中控制 `ContentDraftWriter` 是否调用

* **改动**: 已通过 Step 1.4 实现（在方法内部判断），无需修改调用链

#### Step 1.7: 添加 `--content-source` 为 notion 时的 `--no-markdown-draft` 自动提示

* **文件**: `src/Bukit.Importing/ImportReportWriter.cs`

* **改动**: 在 `Write()` 末尾增加提示（当 `ContentSource` 为 notion 时）：

  ```
  Console.WriteLine("  提示: content 已配置为 notion provider，使用 bukit notion push 推送内容");
  ```

#### Step 1.8: 更新 CLI 帮助文档和描述

* **文件**: `src/Bukit.Cli/Cli/BukitCliSpecs.cs`

* **改动**: 更新 `--content-source` 的描述文字，移除"不影响默认 markdown build 草稿"

***

## P0-2: Notion push 的 schema 预检和幂等 upsert

### 当前行为

* `NotionSeedPusher.PushAsync()` 对每条 record 执行 `POST /v1/pages`

* 无 schema 校验，无重复检测，无更新能力

* 失败后重跑会产生重复页面

### 目标行为

1. `bukit notion validate-schema` — 校验 Notion database schema 是否包含必要字段
2. `bukit notion push --mode create|upsert` — 支持创建和幂等 upsert
3. `bukit notion push --unique-field Slug` — 以 Slug 为唯一键查询并更新

推送流程：

```
读取 database schema → 校验字段 → 对每条 record:
  → 查询 Slug 是否存在
  → 存在 → PATCH page (update)
  → 不存在 → POST page (create)
→ 输出 created/updated/skipped/failed 报告
```

### 实施步骤

#### Step 2.1: 新增 `bukit notion validate-schema` 子命令

* **文件**: `src/Bukit.Cli/Cli/BukitCliSpecs.cs`

* **位置**: notion 子命令列表中，`push` 之后

* **内容**:

  ```csharp
  new CliCommandSpec(
      Name: "validate-schema",
      Description: "校验 Notion database schema 是否包含 Bukit 所需字段",
      Options: new[]
      {
          new CliOptionSpec("--database-id", "...", CliOptionType.String, ValueName: "id", Required: true),
          new CliOptionSpec("--token-env", "...", CliOptionType.String, ValueName: "name"),
          new CliOptionSpec("--report", "报告输出路径", CliOptionType.String, ValueName: "file"),
      })
  ```

#### Step 2.2: 实现 `NotionSchemaValidator` 类

* **新文件**: `src/Bukit.Cli/Commands/NotionSchemaValidator.cs`

* **功能**:

  * `ValidateAsync(http, databaseId, token)` → 调用 `GET /databases/{id}` 获取 schema

  * 校验必要字段存在：`Title` (title), `Slug` (rich\_text), `Type` (select), `Summary` (rich\_text), `Content` (rich\_text), `Language` (select), `Published` (checkbox), `SeoTitle` (rich\_text), `SeoDescription` (rich\_text)

  * 输出每个字段的校验结果（OK/Missing/Type Mismatch）

  * 输出 JSON 校验报告

#### Step 2.3: `NotionCommand` 增加 `validate-schema` 路由

* **文件**: `src/Bukit.Cli/Commands/NotionCommand.cs`

* **改动**: `RunAsync()` 中添加 `"validate-schema" => ValidateSchemaAsync(command)`

#### Step 2.4: 扩展 `NotionPushOptions` — 增加 mode 和 uniqueField

* **文件**: `src/Bukit.Cli/Commands/NotionSeedPusher.cs` (L8-L13)

* **改动**: `NotionPushOptions` record 增加：

  ```csharp
  internal sealed record NotionPushOptions(
      string DatabaseId,
      string Token,
      string ReportPath,
      bool DryRun,
      string Mode = "create",           // create | upsert
      string UniqueField = "Slug");     // 唯一标识字段
  ```

#### Step 2.5: 扩展 `NotionPushResult` — 增加统计维度

* **文件**: `src/Bukit.Cli/Commands/NotionSeedPusher.cs`

* **改动**: `NotionPushResult` record 增加：

  ```csharp
  internal sealed record NotionPushResult(
      int Total,
      int Created,
      int Updated,
      int Skipped,
      int Failed,
      IReadOnlyList<NotionPushItemResult> Items);
  ```

#### Step 2.6: `NotionSeedPusher.PushAsync()` 重构 — 支持 upsert

* **文件**: `src/Bukit.Cli/Commands/NotionSeedPusher.cs`

* **核心改动**:

  1. 增加 `QueryExistingPageBySlug()` 方法：调用 `POST /databases/{id}/query` 按 Slug 属性搜索已有页面
  2. 增加 `UpdatePage()` 方法：对已有页面发送 `PATCH /pages/{pageId}`
  3. `PushAsync()` 内部逻辑：

     ```
     foreach (record):
       if mode == "upsert":
         existingPageId = QueryBySlug(record.Slug)
         if existingPageId:
           PATCH /pages/{existingPageId} → action: "updated"
         else:
           POST /pages → action: "created"
       else (mode == "create"):
         POST /pages → action: "created"
     ```
  4. 增加 `GetDatabaseSchema()` 私有方法：查询 database schema 用于校验

#### Step 2.7: CLI 选项增加 `--mode` 和 `--unique-field`

* **文件**: `src/Bukit.Cli/Cli/BukitCliSpecs.cs`

* **位置**: `notion push` 子命令的 Options 列表

* **内容**:

  ```csharp
  new CliOptionSpec("--mode", "推送模式: create (仅创建) | upsert (创建或更新)", 
      CliOptionType.String, ValueName: "mode"),
  new CliOptionSpec("--unique-field", "用于判断记录是否存在的唯一字段名 (默认 Slug)", 
      CliOptionType.String, ValueName: "name"),
  ```

#### Step 2.8: `NotionCommand.PushAsync()` 解析新选项

* **文件**: `src/Bukit.Cli/Commands/NotionCommand.cs`

* **改动**: 解析 `--mode` (默认 "create") 和 `--unique-field` (默认 "Slug")，传入 `NotionPushOptions`

#### Step 2.9: 报告输出优化

* **改动**: `WriteReport()` 方法输出 created/updated/skipped/failed 四个维度的统计，每个 item 增加 `action` 字段标识行为

#### Step 2.10: `ImportCommand.PushGeneratedSeedToNotionAsync()` 适配

* **文件**: `src/Bukit.Cli/Commands/ImportCommand.cs` (L210-L238)

* **改动**: 传入新参数（mode 和 uniqueField）支持 `--push-notion` 场景

***

## P0-3: HTML → Notion Block 富文本转换

### 当前行为

* `NotionSeedPusher.BuildParagraphBlocks()` 调用 `StripHtml()` 去掉所有 HTML 标签

* 纯文本按 1900 字符分段，全部作为 `paragraph` block

* 丢失：h1/h2/h3、ul/ol/li、blockquote、img、a、表格、FAQ toggle 等

### 目标行为

新增 `HtmlToNotionBlockConverter` 类，将 HTML 语义化转换为 Notion Block API blocks：

| HTML 元素         | Notion Block                         |
| --------------- | ------------------------------------ |
| `<h1>`          | `heading_1`                          |
| `<h2>`          | `heading_2`                          |
| `<h3>`          | `heading_3`                          |
| `<p>`           | `paragraph`                          |
| `<ul>` + `<li>` | `bulleted_list_item`                 |
| `<ol>` + `<li>` | `numbered_list_item`                 |
| `<blockquote>`  | `quote`                              |
| `<img>`         | `image` (external + caption)         |
| `<a>`           | `rich_text` link                     |
| `<table>`       | `table` 或 fallback paragraph         |
| `.faq-item`     | `toggle` 或 `heading_3` + `paragraph` |

### 实施步骤

#### Step 3.1: 创建 `HtmlToNotionBlockConverter` 类

* **新文件**: `src/Bukit.Shared/Notion/HtmlToNotionBlockConverter.cs`

* **依赖**: 使用 System.Text.Json 直接构建 Notion Block API JSON

* **核心方法**:

  ```csharp
  public static class HtmlToNotionBlockConverter
  {
      public static List<NotionBlock> Convert(string html);
      public static string ToBlocksJson(string html);  // 直接输出 JSON children 数组
  }
  ```

#### Step 3.2: 定义 `NotionBlock` 数据模型

* **新文件** 或把 model 放在同一文件

* **包含类型**: `Heading1`, `Heading2`, `Heading3`, `Paragraph`, `BulletedListItem`, `NumberedListItem`, `Quote`, `Image`, `Toggle`, `Table`

* 使用 `Utf8JsonWriter` 序列化为 Notion API 兼容格式

#### Step 3.3: 实现 HTML 解析逻辑

* **核心思路**: 使用简单的 tokenizer/recursive descent parser（不依赖第三方库，与现有正则风格一致但更结构化）

* **处理步骤**:

  1. Tokenize HTML → 识别 tags (opening, closing, self-closing) + text content
  2. Build tree → 将 tokens 组织为层级结构
  3. Convert → 遍历 tree 生成 NotionBlock 列表

* **支持元素**:

  * `h1`/`h2`/`h3`/`h4`/`h5`/`h6` → heading blocks

  * `p` → paragraph block

  * `ul` → 包含 `li` 的 bulleted\_list\_item blocks

  * `ol` → 包含 `li` 的 numbered\_list\_item blocks

  * `blockquote` → quote block

  * `img` → image block (使用 external URL)

  * `a` → inline link parsed as rich\_text link

  * `em`/`i` → italic rich\_text

  * `strong`/`b` → bold rich\_text

  * `.faq-item` 容器 → toggle block (question 为 heading，answer 为内容)

#### Step 3.4: 处理特殊结构化内容

* **FAQ 结构**: 当遇到 `class` 包含 `faq-item` 的元素时，识别内部的 `h3`（question）和 `p`（answer），转为 `toggle` block

* **Company Profile**: 当页面类型为 `CompanyDetail` 时，识别字段如 Country、Industry 等，提取到 properties

* **Article Metadata**: 识别 author、date、category 等 meta 信息

#### Step 3.5: 在 `NotionSeedPusher` 中集成

* **文件**: `src/Bukit.Cli/Commands/NotionSeedPusher.cs`

* **改动**: `BuildCreatePagePayload()` 方法中：

  * `Content` 属性保持不变（plain text summary）

  * `children` blocks 改用 `HtmlToNotionBlockConverter.ToBlocksJson(record.Content)`

  * 移除旧的 `BuildParagraphBlocks()` 和 `WriteParagraphBlock()` 方法

#### Step 3.6: 在 `ImportSeedRecord` 中保留原始 HTML

* **检查**: `ImportSeedRecord` 是否保留了原始 HTML 内容

* **文件**: `src/Bukit.Cli/Commands/ImportSeedRecordReader.cs`（需确认路径）

* **保证**: Content 字段存储的是原始 HTML（不是 strip 后的纯文本），以便 HtmlToNotionBlockConverter 正确处理

#### Step 3.7: 添加降级预览（fallback）

* 对于无法识别的 HTML 元素，降级为 paragraph block

* 对于超出 Notion block 长度限制（2000 字符）的文本，自动分段

* 对于 `<table>` 复杂结构，先尝试简单表格映射，失败则降级为 paragraph

***

## P1-1: 为 silkroadbiz 增加显式导入规则

### 当前行为

* `PageClassifier` 依赖文件名映射字典 + CSS class 猜测

* 路由映射硬编码在 `HtmlDemoImporter.RouteForPage()` / `TemplateForPage()`

* 无外部配置文件支持

### 目标行为

支持 `--route-map demo.routes.yaml` 显式指定页面→路由→类型→模板映射

```yaml
# demo.routes.yaml
pages:
  - source: index.html
    route: /
    type: Home
    template: index
  - source: insights.html
    route: /insights/
    type: PostList
    template: insights
  - source: china-companies.html
    route: /china-companies/
    type: CompanyList
    template: china-companies
  - source: malaysia-companies.html
    route: /malaysia-companies/
    type: CompanyList
    template: malaysia-companies
```

### 实施步骤

#### Step 4.1: 添加 `--route-map` CLI 选项

* **文件**: `src/Bukit.Cli/Cli/BukitCliSpecs.cs`

* **位置**: `import html-demo` 子命令

* **内容**:

  ```csharp
  new CliOptionSpec("--route-map", "显式页面路由映射 YAML 文件路径", 
      CliOptionType.String, ValueName: "file"),
  ```

#### Step 4.2: 定义 `RouteMapConfig` 模型类

* **新文件**: `src/Bukit.Importing/RouteMapConfig.cs`

* **内容**:

  ```csharp
  public sealed class RouteMapConfig
  {
      public List<RouteMapPage> Pages { get; init; } = [];
  }

  public sealed class RouteMapPage
  {
      public string Source { get; init; } = "";
      public string Route { get; init; } = "";
      public string Type { get; init; } = "";
      public string Template { get; init; } = "";
  }
  ```

#### Step 4.3: 实现 `RouteMapLoader` 解析 YAML

* **新文件** 或放在 `RouteMapConfig.cs` 同级

* **方法**: `RouteMapLoader.Load(string filePath)` → `RouteMapConfig`

* **实现**: 使用 YamlDotNet 解析（项目已有依赖，见 `ConfigLoader` 用 `YamlDotNet`）

#### Step 4.4: 修改 `PageClassifier` — 支持 RouteMap

* **文件**: `src/Bukit.Importing/PageClassifier.cs`

* **改动**: 增加重载 `Classify(fileNameWithoutExtension, html, RouteMapConfig?)`:

  ```csharp
  internal static PageType Classify(string fileNameWithoutExtension, string html, 
      RouteMapConfig? routeMap = null)
  {
      // 先查 routeMap
      if (routeMap != null)
      {
          var match = routeMap.Pages.FirstOrDefault(p => 
              string.Equals(p.Source, $"{fileNameWithoutExtension}.html", 
                  StringComparison.OrdinalIgnoreCase) ||
              string.Equals(Path.GetFileNameWithoutExtension(p.Source), 
                  fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase));
          if (match != null)
              return ParsePageType(match.Type);
      }
      // 原有逻辑
  }
  ```

#### Step 4.5: `HtmlDemoImporter` 集成 RouteMap

* **文件**: `src/Bukit.Importing/HtmlDemoImporter.cs`

* **改动**:

  1. `Import()` 开头加载 RouteMap（如果指定）
  2. `RouteForPage()` 优先使用 RouteMap 中的 route
  3. `TemplateForPage()` 优先使用 RouteMap 中的 template
  4. 将 RouteMap 传递到 `PageClassifier.Classify()`、`ContentExtractor.Extract()` 等

#### Step 4.6: `HtmlDemoImportOptions` 增加 RouteMap 字段

* **文件**: `src/Bukit.Importing/ImportModels.cs`

* **改动**: 添加 `public string? RouteMapPath { get; init; }`

#### Step 4.7: `ImportCommand` 解析 RouteMap

* **文件**: `src/Bukit.Cli/Commands/ImportCommand.cs`

* **改动**: 在 `HtmlDemoAsync()` 中解析 `--route-map` 参数并传入 options

***

## P1-2: 使用 DOM parser 替代正则 HTML 解析

### 当前行为

* `ContentExtractor` — 全部使用 Regex（`H1Regex`, `FirstParagraphRegex`, `CardRegex`, `FaqItemRegex` 等）

* `TemplateBodyTransformer` — 使用 Regex（`H1Regex`, `ElementWithClassPattern`）

* `HtmlDocumentParser` — 使用 Regex + 字符串 IndexOf

* `NotionSeedPusher.StripHtml()` — 使用 `Regex.Replace`

### 目标行为

引入 AngleSharp 作为 HTML DOM parser，在 import 模块内部使用，降低复杂 HTML 的解析误判风险。

### 实施步骤

#### Step 5.1: 添加 AngleSharp NuGet 引用

* **项目**: `src/Bukit.Importing/Bukit.Importing.csproj`

* **内容**: `<PackageReference Include="AngleSharp" Version="1.*" />`

#### Step 5.2: 重构 `HtmlDocumentParser` — 使用 AngleSharp

* **文件**: `src/Bukit.Importing/HtmlDocumentParser.cs`

* **改动**:

  * `Parse()` 方法使用 `AngleSharp.Html.Parser.HtmlParser` 替代 regex

  * `Title` 提取：`document.QuerySelector("title")?.TextContent`

  * `BodyContent` 提取：`document.Body?.InnerHtml`

  * `AssetPaths` 提取：`document.QuerySelectorAll("[src],[href]")` → 过滤

  * `SplitBody()` 重构：使用 DOM API 查找 `<main>`, `<article>`, `<!-- content -->` 注释节点

#### Step 5.3: 重构 `ContentExtractor` — 使用 AngleSharp

* **文件**: `src/Bukit.Importing/ContentExtractor.cs`

* **改动范围**: 全部 7 个提取方法

  * `ExtractPageContent()` — 用 `QuerySelector("h1")` 替代 `H1Regex`

  * `ExtractSummary()` — 用 `QuerySelector("p")` 替代 `FirstParagraphRegex`

  * `ExtractPosts/Companies/Services()` — 用 `QuerySelectorAll(".article-card, .company-card, .service-card")` 替代 `CardRegex`

  * `ExtractSections()` — 用 `QuerySelectorAll("section.hero, section.cta, ...")` 替代 SectionRegex

  * `ExtractFaqs()` — 用 `QuerySelectorAll(".faq-item")` 替代 `FaqItemRegex`

  * `StripHtml()` — 用 `element.TextContent` 替代 `Regex.Replace`

  * `ExtractContentBody()` — 用 `QuerySelector("article, main")` 替代 `StripOuterElement`

#### Step 5.4: 重构 `TemplateBodyTransformer` — 使用 AngleSharp

* **文件**: `src/Bukit.Importing/TemplateBodyTransformer.cs`

* **改动**:

  * `ReplaceFirstHeading()` — 用 DOM 精确操作 `<h1>` 文本节点

  * `ReplaceSectionComponents()` — 用 `QuerySelectorAll()` 按 class 查找

  * `ReplaceListCards()` — 同上

  * `ReplaceMainContentAfterHeading()` — 用 DOM 插入 placeholder

  * **关键**：转换后需要保持 Scriban 模板语法（`{{ }}`），确保 DOM parser 不破坏模板标记

#### Step 5.5: 重构 `PageClassifier.ClassifyByContent()` — 使用 AngleSharp

* **文件**: `src/Bukit.Importing/PageClassifier.cs`

* **改动**: `ClassifyByContent()` 使用 `QuerySelectorAll(".article-card, .company-card, .service-card")` + `.Length` 替代 `CountOccurrences` 字符串查找

#### Step 5.6: 重构 `NotionSeedPusher.StripHtml()` — 使用 AngleSharp

* **文件**: `src/Bukit.Cli/Commands/NotionSeedPusher.cs`

* **改动**: `StripHtml()` 使用 `parser.ParseDocument(html).Body?.TextContent` 替代 `Regex.Replace`

#### Step 5.7: 确保 Scriban 模板语法兼容

* **重要**: 模板中包含 `{{ page.title }}`, `{% if ... %}`, `{% for ... %}`, `{{ include '...' }}` 等 Scriban 标记

* **解决方案**:

  * 在解析前用 placeholder 保护 Scriban 标记

  * 操作完成后再还原

  * 或使用正则预处理 + DOM 后处理混合模式

#### Step 5.8: 回归测试保证兼容

* **目录**: `tests/Bukit.Importing.Tests/`

* **验证**: 现有的 `HtmlDocumentParser` 测试、`ContentExtractor` 测试仍然通过

* **新增**: AngleSharp-based 测试用例覆盖边缘情况（嵌套 section、属性顺序变化、换行缩进差异）

***

## P1-3: import report 标记"模板残留内容比例"

### 当前行为

`ImportReportWriter.WriteReportFile()` 的 "Hardcoded Residuals" 章节仅从 warnings 中筛选关键字生成提示文案，无定量分析。

### 目标行为

生成精确的硬编码内容残留分析，包含：

| Metric                  | 说明                 |
| ----------------------- | ------------------ |
| HardcodedContentScore   | 整体硬编码残留分数 (0-100)  |
| RemainingBusinessText   | 模板中仍残留的业务文本条目数     |
| TemplateTextResidue     | 每个模板的残留文本数量 + 严重程度 |
| NotionExtractedCoverage | Notion 抽取覆盖比例      |

### 报告示例

```
## Hardcoded Content Residue

| Template | Residual Text Count | Severity |
|---|---:|---|
| pages/index.html | 42 | high |
| pages/companies.html | 18 | medium |

## Extraction Coverage

| Collection | Extracted | Total Expected | Coverage |
|---|---:|---:|---:|
| Pages | 5 | 5 | 100% |
| Posts | 12 | 12 | 100% |
| Companies | 45 | 45 | 100% |
```

### 实施步骤

#### Step 6.1: 定义残留分析数据模型

* **文件**: `src/Bukit.Importing/ImportModels.cs`

* **新增 Record**:

  ```csharp
  public sealed record TemplateResidueAnalysis
  {
      public string TemplatePath { get; init; } = "";
      public int ResidualTextCount { get; init; }
      public int TotalTextSegments { get; init; }
      public string Severity { get; init; } = "low";  // low | medium | high
      public List<string> ResidualSamples { get; init; } = [];
  }

  public sealed record HardcodedContentReport
  {
      public int OverallScore { get; init; }  // 0-100, lower is better
      public List<TemplateResidueAnalysis> Residues { get; init; } = [];
      public int TotalResidualCount { get; init; }
  }
  ```

#### Step 6.2: 实现 `TemplateResidueAnalyzer` 类

* **新文件**: `src/Bukit.Importing/TemplateResidueAnalyzer.cs`

* **核心方法**:

  ```csharp
  internal static class TemplateResidueAnalyzer
  {
      internal static HardcodedContentReport Analyze(
          string themePath, 
          List<ExtractedContent> contents, 
          RouteMapConfig? routeMap);
      
      internal static TemplateResidueAnalysis AnalyzeTemplate(string templateContent);
  }
  ```

#### Step 6.3: 实现模板文本残留检测算法

* **识别"非模板文本"的规则**:

  1. 不是 Scriban 表达式：`{{ ... }}` 或 `{% ... %}`
  2. 不是 HTML 标签（变体 class/id/data- 属性值不算文本）
  3. 不是模板指令：`{{ include '...' }}`、`{{ for ... }}`、`{{ if ... }}`
  4. 超过 N 个字符（过滤短单词/数字）
  5. 不在已知的 page/collection 变量引用中
  6. 不在 "Email"、"Phone"、"Address" 等通用占位符白名单中

* **算法**:

  ```
  for each template file:
    textSegments = extract text nodes (DOM)
    residualSegments = textSegments.filter(s => 
      s.Length > 3 && 
      !s.Contains("{{") && 
      !s.Contains("{%") && 
      !isInWhitelist(s) &&
      looksLikeBusinessText(s)
    )
    severity = classifySeverity(residualSegments.Count)
  ```

#### Step 6.4: `ImportReportWriter` 集成残留分析

* **文件**: `src/Bukit.Importing/ImportReportWriter.cs`

* **改动**: `WriteReportFile()` 方法：

  1. 在模板生成后调用 `TemplateResidueAnalyzer.Analyze()`
  2. 新增 `## Hardcoded Content Residue` 章节（替换现有简版）
  3. 新增 `## Extraction Coverage` 章节

#### Step 6.5: `ImportResult` 模型扩展

* **文件**: `src/Bukit.Importing/ImportModels.cs`

* **改动**: 增加字段：

  ```csharp
  public HardcodedContentReport? HardcodedContentReport { get; init; }
  ```

#### Step 6.6: `HtmlDemoImporter` 传递残留分析结果

* **文件**: `src/Bukit.Importing/HtmlDemoImporter.cs`

* **改动**: `Import()` 方法在模板生成后调用 `TemplateResidueAnalyzer.Analyze()`，结果保存到 `ImportResult`

***

## 实施顺序建议

按优先级和依赖关系排序：

```
Phase 1 (P0 独立任务，可并行):
├─ P0-1: ContentSource notion 修复 (Step 1.1-1.8)
├─ P0-2: Notion push upsert (Step 2.1-2.10)
└─ P0-3: HTML→Notion Block 转换 (Step 3.1-3.7)

Phase 2 (P1 依赖 Phase 1，可并行):
├─ P1-1: RouteMap 导入规则 (Step 4.1-4.7) — 依赖 P0-1 Step 1.6
├─ P1-2: DOM parser 重构 (Step 5.1-5.8) — 依赖 P0-3 Step 3.3 (共享 HTML 解析思路)
└─ P1-3: 模板残留报告 (Step 6.1-6.6) — 依赖 P0-1 Step 1.6
```

## 测试验证计划

每个 Phase 完成后需要：

1. **P0-1**: 运行 `bukit import html-demo ./demo --content-source notion`，验证：

   * `sites/<theme>/content/` 不生成 .md 文件

   * `sites/<theme>/site.yaml` 使用 `provider: notion`

   * `sites/<theme>/notion-seed/` 正常生成

2. **P0-2**: 运行 `bukit notion validate-schema --database-id <id>` + `bukit notion push --mode upsert`，验证：

   * Schema 校验输出完整

   * 重复执行不产生重复页面

   * 修改 seed 重新 push 正确更新已有页面

3. **P0-3**: 对比新旧 Notion push 结果，验证：

   * 页面包含 H2/H3 结构

   * 列表正确渲染

   * 图片/链接保留

   * FAQ 显示为 toggle

4. **P1-1**: 运行 `bukit import html-demo ./demo --theme silkroadbiz --route-map demo.routes.yaml` 验证映射生效

5. **P1-2**: 运行导入现有复杂 HTML demo，对比重构前后解析结果一致性

6. **P1-3**: 查看生成的 `import-report.md`，"Hardcoded Content Residue" 表格内容准确

***

## 风险与注意事项

1. **P1-2 (DOM parser)**: Scriban 模板中的 `{{ }}` 标记可能被 AngleSharp 当作无效 HTML 实体处理。需要在解析前保护模板标记（用特殊 placeholder 替换，处理完再还原）。

2. **P0-2 (upsert)**: Notion API 的 `PATCH /pages` 需要正确构造 properties 和 children 的更新 JSON。Notion block children 的更新与 create 不同，需要先 clear 再 append 或使用 block-level PATCH。

3. **P0-1 (向后兼容)**: 需要确保 `--no-markdown-draft` 选项在 notion 模式下默认启用，但用户可以通过显式传 `--no-markdown-draft false`（如果 CLI 支持）来回退。推荐方案：添加独立的 `--markdown-draft` flag，在 notion 模式下默认 false。

4. **P1-1 (RouteMap)**: RouteMap 中的 source 路径应该相对于 input dir（`--input <demo-dir>` 参数中的 `<demo-dir>`），以避免路径混淆。

