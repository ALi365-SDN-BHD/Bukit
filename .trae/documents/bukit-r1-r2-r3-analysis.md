# 3 个已知限制 — 深度分析与修复计划

***

## R1: Report Build/Data Source Relationship 在 notion 模式下误导

### 问题分析

**当前代码 ([L166](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ImportReportWriter.cs#L166)):**

```
- Build uses the generated Markdown draft under `content/`
  so `bukit build` and `--verify` do not require external credentials.
```

**矛盾:** 当 `--content-source notion` 时：

* `content/` 目录不存在（Notion 模式不生成 `.md`）

* `site.yaml` 使用 `provider: notion`（需 NOTION\_TOKEN）

* "Notion Provider Status" 章节（L215-222）正确说明 `bukit build requires valid NOTION_TOKEN`

* 但 "Build/Data Source Relationship" 章节（L166）仍说 "Markdown draft" — **两个章节互相矛盾**

**危险场景:** 用户看到 L166 的文字，以为 `--verify` 可以在无 Notion 凭据下运行，但实际 `bukit build` 会因为 `provider: notion` 而请求 NOTION\_TOKEN。

### 修复方案

**策略:** 根据 ContentSource 动态生成正确的文字。

#### Step R1.1

* **文件**: [ImportReportWriter.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ImportReportWriter.cs#L163-L167)

* **改动**:

  ```csharp
  sb.AppendLine("## Build/Data Source Relationship");
  sb.AppendLine();
  if (options.ContentSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
  {
      sb.AppendLine("- Build uses the Notion API (`provider: notion`). Ensure `NOTION_TOKEN` is set before running `bukit build` or `--verify`.");
      sb.AppendLine("- Seed files in `notion-seed/` are for push only and do not serve as a build source.");
  }
  else
  {
      sb.AppendLine("- Build uses the generated Markdown draft under `content/` so `bukit build` and `--verify` do not require external credentials.");
  }
  sb.AppendLine($"- `{options.ContentSource}` seed files are generated for review/import and are not treated as a live build provider in this step.");
  ```

#### Step R1.2

* 更新现有测试 `Import_GenerateReport_WritesReportFile` 中的断言，使其匹配新的 notion 模式输出

***

## R2: GetBlockChildrenIdsAsync 不分页 — 超过 100 blocks 时静默遗漏

### 问题分析

**当前代码 ([L227-L246](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/NotionSeedPusher.cs#L227-L246)):**

```csharp
var url = $".../blocks/{pageId}/children?page_size=100";
using var response = await http.SendAsync(request, ct);
if (!response.IsSuccessStatusCode) return ids;

var body = await response.Content.ReadAsStringAsync(ct);
using var doc = JsonDocument.Parse(body);
foreach (var block in doc.RootElement.GetProperty("results").EnumerateArray())
{
    var id = block.GetProperty("id").GetString();
    if (!string.IsNullOrWhiteSpace(id))
        ids.Add(id);
}
return ids;
```

**问题:** Notion API 的 block children 端点返回如下结构：

```json
{
  "results": [...],
  "has_more": true,
  "next_cursor": "abc123..."
}
```

`page_size=100` 但最多返回 100 个 block。如果页面有 >100 个 blocks，`has_more == true`，其余 blocks 的 ID 不会被获取 → `replace` 模式不会删除它们 → 旧 blocks 残留。

**评估影响面:**

* 典型导入页面：heading + 段落 + 列表 + 图片 ≈ 10-30 blocks

* 极端长文档：>100 blocks 时 → 删除不完整

* `replace` 模式是新功能，用户主要在迁移时使用

* 迁移场景下 HTML 正文 block 数量通常 <50

**结论:** 影响面低但应修复以保正确性。

### 修复方案

**策略:** 标准 Notion API cursor-based 分页循环。

#### Step R2.1: 实现分页循环

* **文件**: [NotionSeedPusher.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/NotionSeedPusher.cs#L227-L246)

* **改动**:

  ```csharp
  private static async Task<List<string>> GetBlockChildrenIdsAsync(
      HttpClient http, NotionPushOptions options, string pageId, CancellationToken ct)
  {
      var ids = new List<string>();
      string? startCursor = null;
      var hasMore = true;

      while (hasMore)
      {
          var url = $"{NotionApiUrls.Base}/{NotionApiUrls.ApiVersion}/blocks/{pageId}/children?page_size=100";
          if (startCursor != null)
              url += $"&start_cursor={Uri.EscapeDataString(startCursor)}";

          using var request = new HttpRequestMessage(HttpMethod.Get, url);
          BuildCommonRequestHeaders(request, options.Token);
          using var response = await http.SendAsync(request, ct);
          if (!response.IsSuccessStatusCode) return ids;

          var body = await response.Content.ReadAsStringAsync(ct);
          using var doc = JsonDocument.Parse(body);
          foreach (var block in doc.RootElement.GetProperty("results").EnumerateArray())
          {
              var id = block.GetProperty("id").GetString();
              if (!string.IsNullOrWhiteSpace(id))
                  ids.Add(id);
          }

          hasMore = doc.RootElement.TryGetProperty("has_more", out var hm) && hm.GetBoolean();
          startCursor = hasMore && doc.RootElement.TryGetProperty("next_cursor", out var nc)
              ? nc.GetString() : null;
      }
      return ids;
  }
  ```

***

## R3: RouteMapLoader 不支持多行 YAML 值 → 采用 YamlDotNet

### 问题分析

**当前代码 ([L63-L71](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/RouteMapLoader.cs#L63-L71)):**

```csharp
private static string ExtractYamlValue(string line, string prefix)
{
    var value = line[prefix.Length..].Trim();
    if (value.StartsWith('"') && value.EndsWith('"'))
        value = value[1..^1];
    else if (value.StartsWith('\'') && value.EndsWith('\''))
        value = value[1..^1];
    return value;
}
```

**问题:** 仅支持单行值。YAML block scalars 会静默失败：

```yaml
# ❌ 多行 description 无法解析
pages:
  - source: china-companies.html
    route: /china-companies/
    type: CompanyList
    template: china-companies
    description: |
      中国地区企业列表页面
      包含所有中国区业务
```

**决定:** 用户要求采用 YamlDotNet 替代手写解析器。

### 环境确认

| 条件                                             |                                                                                            状态                                                                                            |
| ---------------------------------------------- | :--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------: |
| YamlDotNet 16.3.0 在 Directory.Packages.props 中 |                                                                                           ✅ 已存在                                                                                          |
| Bukit.Importing 当前引用 YamlDotNet                |                                                                                           ❌ 需添加                                                                                          |
| 代码库现有 YAML 解析模式                                | `YamlStream.Load()` + `YamlSequenceNode`/`YamlMappingNode` (如 [ImportSeedRecords:L75-L96](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ImportSeedRecords.cs#L75-L96)) |

### 修复方案

**策略:** 用 `YamlStream` + `YamlSequenceNode` → `YamlMappingNode` 重写 `RouteMapLoader.Load()`，同时保留强健壮的错误处理。

#### Step R3.1: 添加 YamlDotNet 引用到 Bukit.Importing

* **文件**: [Bukit.Importing.csproj](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/Bukit.Importing.csproj)

* **改动**: 添加 `<PackageReference Include="YamlDotNet" />`（版本由 Directory.Packages.props 管理）

#### Step R3.2: 用 YamlDotNet 重写 `RouteMapLoader.Load`

* **文件**: [RouteMapLoader.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/RouteMapLoader.cs)

* **改动**: 完整替换实现:

  ```csharp
  using YamlDotNet.RepresentationModel;

  internal static class RouteMapLoader
  {
      internal static RouteMapConfig? Load(string? path)
      {
          if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
              return null;

          try
          {
              var yaml = new YamlStream();
              using var reader = File.OpenText(path);
              yaml.Load(reader);

              if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode == null)
              {
                  Console.Error.WriteLine($"Route map '{path}' is empty.");
                  return null;
              }

              var root = yaml.Documents[0].RootNode;
              YamlSequenceNode pagesSeq;

              if (root is YamlMappingNode mapping)
              {
                  if (!mapping.Children.TryGetValue("pages", out var pagesNode) ||
                      pagesNode is not YamlSequenceNode seq)
                  {
                      Console.Error.WriteLine($"Route map '{path}' is missing the 'pages' sequence.");
                      return null;
                  }
                  pagesSeq = seq;
              }
              else if (root is YamlSequenceNode directSeq)
              {
                  pagesSeq = directSeq;
              }
              else
              {
                  Console.Error.WriteLine($"Route map '{path}' has unsupported structure.");
                  return null;
              }

              var config = new RouteMapConfig();
              foreach (var node in pagesSeq.Children)
              {
                  if (node is not YamlMappingNode item)
                      continue;

                  var page = new RouteMapPage
                  {
                      Source   = ReadString(item, "source"),
                      Route    = ReadString(item, "route"),
                      Type     = ReadString(item, "type"),
                      Template = ReadString(item, "template"),
                      Slug     = ReadOptionalString(item, "slug"),
                      Description = ReadOptionalString(item, "description")
                  };

                  if (string.IsNullOrWhiteSpace(page.Source))
                  {
                      Console.Error.WriteLine("Route map entry missing required 'source' field.");
                      continue;
                  }

                  config.Pages.Add(page);
              }

              return config;
          }
          catch (YamlDotNet.Core.YamlException ex)
          {
              Console.Error.WriteLine($"Failed to parse route map '{path}': {ex.Message}");
              return null;
          }
          catch (Exception ex)
          {
              Console.Error.WriteLine($"Failed to load route map '{path}': {ex.Message}");
              return null;
          }
      }

      private static string ReadString(YamlMappingNode node, string key)
      {
          if (!node.Children.TryGetValue(key, out var valueNode))
              return "";
          return ((YamlScalarNode)valueNode).Value ?? "";
      }

      private static string? ReadOptionalString(YamlMappingNode node, string key)
      {
          if (!node.Children.TryGetValue(key, out var valueNode))
              return null;
          var val = ((YamlScalarNode)valueNode).Value;
          return string.IsNullOrWhiteSpace(val) ? null : val;
      }
  }
  ```

#### Step R3.3: 删除旧手写解析代码

* 移除 `ExtractYamlValue()` 方法（不再需要）

* 移除 `inPagesBlock` 状态机

* 移除行号循环

#### Step R3.4: 验证 back-compat

* 旧格式 `- source: foo.html`（无 `pages:` 顶层 key）→ `YamlSequenceNode` 根节点 ✅

* 新格式 `pages:\n  - source: foo.html` → `YamlMappingNode` + `pages` key ✅

* 带引号的值 `source: "foo.html"` → YamlDotNet 自动处理 ✅

* 多行 `description: |\n  text` → YamlDotNet 自动处理 ✅

### 优势总结

| 维度   | 旧实现 (手写行解析) | 新实现 (YamlDotNet)         |
| ---- | ----------- | ------------------------ |
| 多行值  | ❌ 不支持       | ✅ 原生支持                   |
| 引号转义 | ❌ 仅简单 strip | ✅ 完整 YAML 语义             |
| 嵌套结构 | ❌ 不支持       | ✅ 原生支持                   |
| 错误定位 | ⚠️ 行号       | ✅ 带 context              |
| 代码量  | 72 行        | \~80 行（含错误处理）            |
| 依赖   | 0           | 1 (已有 centrally managed) |

***

## 实施顺序

```
Phase 1 (独立，并行):
├─ R1: Report 文字修正 (Step R1.1 + R1.2)
├─ R2: 分页循环 (Step R2.1)
└─ R3: YamlDotNet 重写 RouteMapLoader (Step R3.1-R3.4)

Phase 2:
└─ dotnet test: 验证现有测试不受影响 + 更新 R1 相关断言
```

## 测试验证

1. **R1**: `--content-source notion` → report 中 "Build/Data Source" 显示 Notion 相关文字
2. **R1**: `--content-source markdown` → report 中 "Build/Data Source" 显示原有 Markdown 文字
3. **R2**: 单元测试模拟 150 个 blocks → 返回 150 个 IDs（需分两页）
4. **R3**: route-map 使用 `description: |` 多行 → YamlDotNet 正确解析多行内容
5. **R3**: 旧格式 `- source: foo.html`（无 `pages:` 顶层 key）→ 仍然可用（sequence root 模式）
6. **R3**: 新格式 `pages:\n  - source: foo.html` → 正确解析
7. **R3**: 单行 description → 行为不变

