# Bukit 深度修复计划 — 5 个未贯通缺口

## 代码审查确认

| 问题 | 严重度 | 确认代码位置 | 根因 |
|------|:------:|-------------|------|
| P0-1: routeMap 未贯通扫描/生成 | 🔴 | [HtmlDemoImporter:L20](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDemoImporter.cs#L20), [HtmlDemoScanner:L5](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDemoScanner.cs#L5), [HtmlDocumentParser:L28](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDocumentParser.cs#L28), [ThemeGenerator:L12](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ThemeGenerator.cs#L12) | routeMap 被加载但从未传入 Scan/Parse/Classify/ThemeGenerator |
| P0-2: upsert 查询失败→创建 | 🔴 | [NotionSeedPusher:L95](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/NotionSeedPusher.cs#L95) | `!IsSuccessStatusCode → return null` → `PushAsync` 走 create 分支 |
| P1-1: upsert 不更新 blocks | 🟡 | [NotionSeedPusher:L208-L218](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/NotionSeedPusher.cs#L208-L218) | `BuildUpdatePagePayload` 只有 properties，无 children |
| P1-2: validate-schema 未接入 --push-notion | 🟡 | [ImportCommand:L203-L213](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ImportCommand.cs#L203-L213) | `pushNotion` 流程从不调用 schema 校验 |
| P1-3: Notion provider 端到端验证 | 🟡 | [ContentProviderFactory:L128](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ContentProviderFactory.cs#L128), [VerifyImportAsync:L257](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ImportCommand.cs#L257) | build 引擎已支持 notion，但 --verify 需真实 NOTION_TOKEN |

---

## P0-1: routeMap 贯通扫描和生成全链路

### 现状
```
HtmlDemoImporter.Import()
  routeMap = Load(...)            ✅ 已加载
  pages = Scanner.Scan(inputPath) ❌ 未传 routeMap
    → Parser.Parse(file, baseDir) ❌ 未传 routeMap
      → Classifier.Classify(name, html) ❌ 未传 routeMap (用的是无参重载)
  ThemeGenerator.Generate(options, pages, ...) ❌ 未传 routeMap
```

routeMap 只在 `BuildReportPages` → `RouteForPage(page, routeMap)` / `TemplateForPage(page, routeMap)` 中使用（仅用于报告展示）。

### 修复步骤

#### Step 1.1: `HtmlDocumentParser.Parse` 增加 routeMap 参数
- **文件**: [HtmlDocumentParser.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDocumentParser.cs#L15)
- **改动**:
  ```csharp
  internal static DiscoveredPage Parse(string filePath, string baseDir, RouteMapConfig? routeMap = null)
  {
      ...
      var pageType = PageClassifier.Classify(fileNameWithoutExtension, html, routeMap);
      ...
      // 同时：如果 routeMap 中指定了 slug，优先使用
      var routeMapPage = routeMap?.Pages.FirstOrDefault(p => ...);
      var slug = routeMapPage?.Route != null 
          ? SanitizeRouteAsSlug(routeMapPage.Route) 
          : ...;
  }
  ```

#### Step 1.2: `HtmlDemoScanner.Scan` 增加 routeMap 参数并下传
- **文件**: [HtmlDemoScanner.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDemoScanner.cs#L5)
- **改动**:
  ```csharp
  internal static List<DiscoveredPage> Scan(string inputPath, RouteMapConfig? routeMap = null)
  {
      ...
      return htmlFiles.Select(f => HtmlDocumentParser.Parse(f, inputPath, routeMap)).ToList();
  }
  ```

#### Step 1.3: `HtmlDemoImporter.Import` 传递 routeMap 到 Scan
- **文件**: [HtmlDemoImporter.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDemoImporter.cs#L20)
- **改动**:
  ```csharp
  var pages = HtmlDemoScanner.Scan(options.InputPath, routeMap);
  ```

#### Step 1.4: `ThemeGenerator.Generate` 增加 routeMap 参数，模板名优先 routeMap
- **文件**: [ThemeGenerator.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ThemeGenerator.cs#L12)
- **改动**:
  ```csharp
  internal static ImportResult Generate(
      HtmlDemoImportOptions options,
      List<DiscoveredPage> pages,
      LayoutExtractor.LayoutInfo layout,
      List<string> warnings,
      Dictionary<string, string> pathMappings,
      RouteMapConfig? routeMap = null)
  ```
- **模板选择逻辑**（[L139-L154](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ThemeGenerator.cs#L139)）改为：
  ```csharp
  var fileName = Path.GetFileNameWithoutExtension(page.RelativePath);
  var routeTemplate = PageClassifier.GetTemplate(routeMap, fileName);
  var templateName = routeTemplate ?? (page.Type switch { ... });
  ```

#### Step 1.5: `HtmlDemoImporter.Import` 传递 routeMap 到 ThemeGenerator
- **文件**: [HtmlDemoImporter.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDemoImporter.cs#L63)
- **改动**:
  ```csharp
  var result = ThemeGenerator.Generate(options, pages, layout, warnings, 
      assetResult.PathMappings, routeMap);
  ```

#### Step 1.6: routeMap 中 Source 匹配时，page.Slug 也优先 routeMap route
- **逻辑**: 当 `routeMap.Pages[].Route` 为 `/china-companies/` 时，slug 应为 `china-companies`，覆盖文件名 slug
- **实现位置**: `HtmlDocumentParser.Parse()` 中通过 `GetSlugFromRouteMap(routeMap, fileName)` 提取

#### Step 1.7: 确保 back-compat — 所有无 RouteMap 的旧行为不变
- routeMap 默认 `null`，所有新增参数都是可选的（`= null`）
- 无 RouteMap 时行为与重构前完全一致

---

## P0-2: upsert 查询失败不能当作"不存在"

### 现状
```csharp
// QueryExistingPageAsync L94-102
using var response = await http.SendAsync(request, ct);
if (!response.IsSuccessStatusCode) return null;  // ❌ 网络错误/API错误 → null → create
```

当下游 `PushAsync` 拿到 `null` 时：
```csharp
if (existingPageId != null)
    // update
else
    // create ← 错误时走这个分支
```

### 修复步骤

#### Step 2.1: 新增 `QueryExistingPageResult` 记录类型
- **文件**: [NotionSeedPusher.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/NotionSeedPusher.cs)
- **内容**:
  ```csharp
  internal sealed record QueryExistingPageResult(
      bool QuerySucceeded,
      string? PageId,
      string? Error);
  ```
  - `QuerySucceeded=true, PageId="xxx"` → 页面存在，可 update
  - `QuerySucceeded=true, PageId=null` → 页面不存在，可 create
  - `QuerySucceeded=false` → 查询失败，标记 failed（不可 create）

#### Step 2.2: `QueryExistingPageAsync` 返回 `QueryExistingPageResult`
- **改动**: 返回类型从 `Task<string?>` 改为 `Task<QueryExistingPageResult>`
  ```csharp
  if (!response.IsSuccessStatusCode)
      return new QueryExistingPageResult(false, null, body ?? response.ReasonPhrase);
  
  var body = ...;
  var results = doc.RootElement.GetProperty("results");
  if (results.GetArrayLength() > 0)
      return new QueryExistingPageResult(true, results[0].GetProperty("id").GetString(), null);
  return new QueryExistingPageResult(true, null, null);
  ```

#### Step 2.3: `PushAsync` 根据 `QuerySucceeded` 决定行为
- **改动**:
  ```csharp
  if (isUpsert)
  {
      var queryResult = await QueryExistingPageAsync(http, options, record, ct);
      if (!queryResult.QuerySucceeded)
      {
          items.Add(new NotionPushItemResult(record, "failed", false, null, 
              $"Schema query failed: {queryResult.Error}"));
          continue;  // ← 不 create
      }
      existingPageId = queryResult.PageId;
  }
  ```

#### Step 2.4: 报告输出区分 "query-failed" 和 "api-failed"
- `NotionPushItemResult.Action` 增加 `"query-failed"`、`"create-failed"`、`"update-failed"` 三种失败类型

---

## P1-1: upsert 支持更新正文 blocks

### 现状
`BuildUpdatePagePayload` 只有 `WriteProperties(writer, record)`，无 children 字段。Notion PATCH /pages 只更新 properties metadata，页面正文 blocks 不更新。

### 修复步骤

#### Step 3.1: 新增 `--update-content` CLI 选项
- **文件**: [BukitCliSpecs.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs) — `notion push` 子命令 Options
- **内容**:
  ```csharp
  new CliOptionSpec("--update-content", 
      "upsert 时更新页面正文 blocks: append (追加) | replace (替换)", 
      CliOptionType.String, ValueName: "strategy"),
  ```
- **默认行为**: 不传时不更新 blocks（保持保守策略）

#### Step 3.2: `NotionPushOptions` 增加 `UpdateContent` 字段
- **文件**: [NotionSeedPusher.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/NotionSeedPusher.cs)
- **改动**:
  ```csharp
  internal sealed record NotionPushOptions(
      ...
      string Mode = "create",
      string UniqueField = "Slug",
      string UpdateContent = "");  // "" | "append" | "replace"
  ```

#### Step 3.3: `updatePageAsync` 增加 blocks 处理逻辑
- **实施策略**: 
  - 当前阶段实现 **append** 模式（最简单、最安全）
  - replace 模式标记为后续迭代（需要 `GET /blocks/{pageId}/children` → 删除 → 重新 append）

- **append 实现**:
  ```csharp
  // 在 UpdatePageAsync 之后，如果 options.UpdateContent == "append"：
  if (options.Mode == "upsert" && options.UpdateContent == "append" && !string.IsNullOrWhiteSpace(record.Content))
  {
      var blocksJson = HtmlToNotionBlockConverter.ToBlocksJson(record.Content);
      if (blocksJson != "[]")
      {
          await AppendBlocksAsync(http, options, pageId, blocksJson, ct);
      }
  }
  ```

#### Step 3.4: 实现 `AppendBlockChildrenAsync`
- `PATCH /blocks/{blockId}/children`（Notion block children API）
- 将 blocks JSON append 到页面顶部或底部

#### Step 3.5: `NotionCommand.PushAsync` 解析 `--update-content`
- 校验值：空字符串 / "append" / "replace"
- 传入 `NotionPushOptions.UpdateContent`

---

## P1-2: validate-schema 接入 --push-notion 流程

### 现状
```
ImportCommand (pushNotion=true)
  → PushGeneratedSeedToNotionAsync()
    → NotionCommand.RunAsync(["push", ...])    ← 直接 push，无 schema 校验
```

### 修复步骤

#### Step 4.1: 新增 `--no-validate-notion-schema` CLI 选项
- **文件**: [BukitCliSpecs.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs)
- **位置**: import html-demo 子命令 Options
- **内容**:
  ```csharp
  new CliOptionSpec("--no-validate-notion-schema", 
      "--push-notion 时跳过 schema 校验", CliOptionType.Flag),
  ```
- **默认**: 当 `--push-notion` 时自动校验（`--no-validate-notion-schema` 不传且 `--push-notion` 为 true 时默认校验）

#### Step 4.2: ImportCommand 解析 `--no-validate-notion-schema`
- **文件**: [ImportCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ImportCommand.cs)
- **改动**:
  ```csharp
  var validateNotionSchema = !command.GetBool("--no-validate-notion-schema");
  ```

#### Step 4.3: `PushGeneratedSeedToNotionAsync` 增加 schema 校验步骤
- **文件**: [ImportCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ImportCommand.cs)
- **改动**:
  ```csharp
  if (validateNotionSchema)
  {
      Console.WriteLine("校验 Notion schema...");
      var token = Environment.GetEnvironmentVariable(tokenEnv);
      using var http = NotionCommand.CreateHttpClient();
      var validationReport = await NotionSchemaValidator.ValidateAsync(
          http, databaseId, token!, null);
      
      if (!validationReport.Success)
      {
          Console.Error.WriteLine("Notion schema validation failed:");
          foreach (var f in validationReport.FieldResults.Where(r => r.Result != "OK"))
              Console.Error.WriteLine($"  {f.Name}: {f.Result} - {f.Message}");
          return 2;
      }
      Console.WriteLine("  Schema validation passed.");
  }
  
  // 继续原有 push 逻辑...
  ```

#### Step 4.4: 更新 `PushGeneratedSeedToNotionAsync` 签名
- 增加 `bool validateSchema` 参数

---

## P1-3: Notion provider 构建端到端验证

### 现状

site.yaml 可以正确生成 `provider: notion` 配置，且构建引擎已支持 notion provider（`ContentProviderFactory` → `NotionContentProvider`）。但 `--verify` 流程（ImportCommand:L257-L274）会实际调用 `bukit build`，此时需要真实的 NOTION_TOKEN。

### 解决方案

采用**保守注入**策略：

#### Step 5.1: `HtmlDemoImportOptions` 增加 `NotionProviderMode` 字段
- **文件**: [ImportModels.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ImportModels.cs)
- **内容**:
  ```csharp
  public string? NotionDatabaseId { get; init; }
  public string? NotionTokenEnv { get; init; }
  ```

#### Step 5.2: `SiteConfigGenerator` 使用真实值（如果提供）替代占位符
- **文件**: [SiteConfigGenerator.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/SiteConfigGenerator.cs#L52)
- **当前**:
  ```yaml
  databaseId: ${NOTION_DATABASE_ID}
  tokenEnv: NOTION_TOKEN
  ```
- **改进**: 如果 `options.NotionDatabaseId` 非空，直接写入真实值：
  ```csharp
  var dbId = !string.IsNullOrWhiteSpace(options.NotionDatabaseId)
      ? options.NotionDatabaseId
      : "${NOTION_DATABASE_ID}";
  sb.AppendLine($"    databaseId: {dbId}");
  ```

#### Step 5.3: `ImportCommand` 将 `--notion-database-id` 和 `--notion-token-env` 传入 options
- **文件**: [ImportCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ImportCommand.cs)
- **改动**: 在 `HtmlDemoImportOptions` 中增加 `NotionDatabaseId` 和 `NotionTokenEnv`

#### Step 5.4: 添加单元测试覆盖 Notion provider build 流程
- **新测试**: `NotionProviderIntegrationTests`
- Mock Notion API 响应（或使用测试环境变量）
- 执行 `import → build` 端到端验证

#### Step 5.5: 报告增加 Notion provider 配置状态说明
- **文件**: [ImportReportWriter.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ImportReportWriter.cs)
- 在 `Extraction Coverage` 或 `Next Steps` 章节说明：
  ```
  ## Notion Provider Status
  - provider: notion ✓
  - databaseId: configured / ${NOTION_DATABASE_ID}
  - bukit build requires valid NOTION_TOKEN environment variable
  ```

---

## 实施顺序

```
Phase 1 (独立 P0):
├─ P0-1: routeMap 贯通链路 (Step 1.1-1.7)
└─ P0-2: upsert 查询失败安全 (Step 2.1-2.4)

Phase 2 (P1):
├─ P1-1: upsert blocks 更新 (Step 3.1-3.5)
├─ P1-2: validate-schema 接入 (Step 4.1-4.4)
└─ P1-3: Notion provider 端到端 (Step 5.1-5.5)
```

## 测试验证

1. **P0-1**: `--route-map demo.routes.yaml` → `Scan` 调用了带 routeMap 的 `Parse` → `PageType` 正确 → `ThemeGenerator` 模板名正确
2. **P0-2**: 模拟 Notion API 查询失败（401/429/500）→ 该 record 标记为 failed（不创建）
3. **P1-1**: `--mode upsert --update-content append` → 页面 blocks 追加了新内容
4. **P1-2**: `--push-notion --notion-database-id X`（不传 `--no-validate-notion-schema`）→ 先输出 schema validation 再 push
5. **P1-3**: `import --content-source notion --notion-database-id real-id` → site.yaml 写入了真实 databaseId → `--verify` 可正常 build
