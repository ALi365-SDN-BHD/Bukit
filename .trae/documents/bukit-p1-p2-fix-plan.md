# Bukit P1/P2 正式版前必须修复计划

## 代码审查确认

| # | 问题 | 严重度 | 确认代码位置 | 根因 |
|---|------|:------:|-------------|------|
| 1 | append blocks 失败未计入失败 | 🔴 | [L208](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/NotionSeedPusher.cs#L208) — `await http.SendAsync()` 无结果检查 | fire-and-forget，blocks 追加失败后 item 仍标记 "updated" |
| 2 | 只支持 append 不支持 replace | 🔴 | [L75](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/NotionSeedPusher.cs#L75) — 仅判断 `"append"` | 重复导入会无限追加正文 (A + A + A ...) |
| 3 | route-map 不生成额外 site.yaml 集合 | 🟡 | [SiteConfigGenerator.cs:L32-L50](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/SiteConfigGenerator.cs#L32-L50) — 固定 4 个 collection | silkroadbiz 需要多企业列表路由 |
| 4 | GetSlugFromRouteMap 不防动态路由 `{slug}` | 🟡 | [L135](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDocumentParser.cs#L135) — `route.Split('/').Last()` | `/insights/{slug}/` → slug=`{slug}` |
| 5 | 缺 route-map 贯通测试 | 🟡 | 无相关测试 | — |
| 6 | 缺 upsert query-failed 测试 | 🟡 | 无相关测试 | — |
| 7 | 缺 schema validate 阻止 push 测试 | 🟡 | 无相关测试 | — |
| 8 | 缺 notion only no-markdown 回归测试 | 🟡 | 无相关测试 | — |
| 9 | RouteMapLoader 轻量 YAML 解析 | 🔵 | [RouteMapLoader.cs:L13-L38](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/RouteMapLoader.cs#L13-L38) — 手写行解析 | 复杂结构静默不完整 |
| 10 | --push-notion 默认校验可能阻塞离线 | 🔵 | — | — |
| 11 | Block converter 缺 table/code/callout | 🔵 | — | — |

---

## 一、P1 — 正式版前必须修复 (Issues 1-8)

### Issue 1: append blocks 失败不计入失败

**当前 (L208):**
```csharp
await http.SendAsync(request, ct);  // fire-and-forget
```

**结果:** 块追加失败后，item 仍标记 `"updated"` 成功 → 报告误导。

**修复:**
- 改 `AppendBlockChildrenAsync` 返回 `Task<(bool Success, string? Error)>`
- 在 `PushAsync` 中检查返回，失败时标记 `"append-failed"` + `continue`

#### Step 1.1: 修改 `AppendBlockChildrenAsync` 签名
- **文件**: [NotionSeedPusher.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/NotionSeedPusher.cs#L198)
- 返回 `Task<(bool Success, string? Error)>`
- 读取 response status 并返回

#### Step 1.2: `PushAsync` 处理 append 失败
- 文件同上 L74-L83
- 在 `await AppendBlockChildrenAsync(...)` 后判断成功/失败
- 失败时: `items.Add(..., "append-failed", false, ...)` → `continue`

#### Step 1.3: `BuildResult` 统计 append-failed
- Failed 计数已包含 `!Success`，无需修改

---

### Issue 2: 只支持 append，不支持 replace

**问题:** 默认 append → 重复导入会无限追加 (A + A + A)。

**修复:** 新增 `--update-content replace` 模式：
1. `GET /blocks/{pageId}/children` 获取已有 blocks
2. `DELETE /blocks/{blockId}` 逐个删除旧 blocks
3. `PATCH /blocks/{pageId}/children` 追加新 blocks

**最终行为矩阵:**

| `--update-content` | 行为 |
|-------------------|------|
| 不传 (none) | 只更新 properties |
| append | 追加正文 blocks（首次补充） |
| replace | 删除旧 blocks → 重新 append（正式迁移） |

#### Step 2.1: `NotionCommand.PushAsync` 校验 `--update-content` 值
- **文件**: [NotionCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/NotionCommand.cs#L52)
- 允许值: `""` / `"append"` / `"replace"`
- 非法值报错返回 2

#### Step 2.2: `GET /blocks/{pageId}/children` — 读取已有 blocks
- 新增 `GetBlockChildrenAsync(http, options, pageId, ct)` → 返回 block IDs
- 使用 `GET /blocks/{pageId}/children?page_size=100`

#### Step 2.3: `DELETE /blocks/{blockId}` — 删除旧 blocks
- 新增 `DeleteBlockAsync(http, options, blockId, ct)`
- 逐个删除（Notion API 没有批量删除）

#### Step 2.4: `PushAsync` 中实现 replace 流程
```csharp
if (success && isUpsert && options.UpdateContent == "replace" && !string.IsNullOrWhiteSpace(record.Content))
{
    // 1. 读取已有 blocks
    var existingBlocks = await GetBlockChildrenAsync(...);
    // 2. 逐个删除
    foreach (var blockId in existingBlocks)
        await DeleteBlockAsync(...);
    // 3. 追加新 blocks
    var blocksJson = HtmlToNotionBlockConverter.ToBlocksJson(record.Content);
    if (blocksJson != "[]")
        await AppendBlockChildrenAsync(...);
}
```

#### Step 2.5: replace 操作失败时标记 replace-failed
- 与 Step 1.2 相同模式

---

### Issue 3: route-map 与 site.yaml 未有深度融合

**问题:** site.yaml 的 collections 固定为 page/post/company/service 四个，不支持 silkroadbiz 的多企业列表路由。

**修复:** `SiteConfigGenerator` 接收 `RouteMapConfig`，当 route-map 中有非默认路由的页面时，生成 `appPages` 节点。

#### Step 3.1: `RouteMapPage` 增加 `Route` 和 `Type` 字段的明确性检查
- **文件**: [RouteMapConfig.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/RouteMapConfig.cs)
- 增加 `public string? Slug { get; init; }` 字段（可选，用于动态路由场景）
- 增加 `public string? Description { get; init; }`

#### Step 3.2: `RouteMapLoader` 支持 `slug:` 和 `description:` 字段
- **文件**: [RouteMapLoader.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/RouteMapLoader.cs)
- 增加对 `slug:` 和 `description:` 的解析

#### Step 3.3: `SiteConfigGenerator.Generate` 接收 routeMap 参数
- **文件**: [SiteConfigGenerator.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/SiteConfigGenerator.cs)
- 签名: `Generate(HtmlDemoImportOptions options, RouteMapConfig? routeMap = null)`

#### Step 3.4: 根据 routeMap 生成额外 appPages 节点
- 遍历 routeMap.Pages
- 对每个非默认路由的 page（排除 index/insights/companies/services 等四个默认集合），生成:
  ```yaml
  appPages:
    {slug}:
      route: {route}
      template: pages/{template}.html
      type: {type}
  ```
- 例: china-companies → `/china-companies/` → `pages/china-companies.html`

#### Step 3.5: `HtmlDemoImporter.Import` 传递 routeMap 到 SiteConfigGenerator
- **文件**: [HtmlDemoImporter.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDemoImporter.cs)
- `SiteConfigGenerator.Generate(options, routeMap)`

---

### Issue 4: GetSlugFromRouteMap 动态路由边界问题

**问题:** `/insights/{slug}/` → slug 被提取为 `{slug}`

**修复:** 两步：
1. 检查 route 是否包含 `{` → 是则跳过 slug 提取
2. RouteMapPage 增加显式 `slug:` 字段 → 优先使用

#### Step 4.1: 防止动态路由模板被当 slug
- **文件**: [HtmlDocumentParser.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/HtmlDocumentParser.cs#L124)
- 在 `GetSlugFromRouteMap` 中增加:
  ```csharp
  if (route.Contains('{'))
      return null;  // 动态路由不反推 slug
  ```

#### Step 4.2: RouteMapPage 的 `slug:` 字段优先
- 如果 `RouteMapPage.Slug` 非空，直接返回 `match.Slug`（Step 3.1 已加字段）
- 否则按原逻辑提取

---

### Issue 5: route-map 贯通测试 (3 个)

#### Step 5.1: 测试 route-map 改变 PageType
- **文件**: `tests/Bukit.Importing.Tests/HtmlDemoImporterTests.cs` (或新文件)
- 准备: `china-companies.html` + route-map `type: CompanyList`
- 断言: `DiscoveredPage.Type == CompanyList`

#### Step 5.2: 测试 route-map 改变模板文件名
- 准备: route-map `template: china-companies`
- 断言: 生成 `themes/.../layouts/pages/china-companies.html`

#### Step 5.3: 测试 route-map 改变报告 Route / Template
- 断言: `import-report.md` 中出现 `/china-companies/` 和 `china-companies`

---

### Issue 6: upsert query-failed 测试

#### Step 6.1: 模拟 Notion query 返回 400
- Mock handler 对 `POST /databases/{id}/query` 返回 400
- 断言: 不请求 `POST /pages`
- 断言: report 中 `failed = 1`，action = `query-failed`

---

### Issue 7: schema validate 阻止 push 测试

#### Step 7.1: 模拟 schema 校验失败
- Mock `GET /databases/{id}` 返回缺字段 schema
- 执行 `import --push-notion`（不传 `--no-validate-notion-schema`）
- 断言: 返回 2，不执行 notion push

---

### Issue 8: Notion-only 不生成 Markdown 回归测试

#### Step 8.1: 测试 notion 模式不生成 content/
- 执行 `import html-demo --content-source notion`
- 断言: `sites/<theme>/content` 不存在
- 断言: `sites/<theme>/notion-seed` 存在
- 断言: `site.yaml` 包含 `provider: notion`

---

## 二、P2 — 体验与健壮性优化 (Issues 9-11)

### Issue 9: RouteMapLoader 轻量 YAML 解析

**短期:** RouteMapLoader 解析失败时打印具体行号和错误内容。

#### Step 9.1: RouteMapLoader 增加行号错误报告
- 解析每行时维护行号
- 未知字段打印 warning: `Route map line N: unknown field ...`
- 解析异常时打印 context

#### Step 9.2: 支持多层缩进（顶部 `pages:` 节点）
- 当前强制要求从 `- source:` 开始
- 增加对 `pages:` 顶层 key 的跳过支持

---

### Issue 10: --push-notion 默认 schema validate 阻塞

**修复:** 在文档/提示中明确说明。

#### Step 10.1: CLI 帮助文本明确
- **文件**: [BukitCliSpecs.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs)
- `--no-validate-notion-schema` 增加说明: "用于本地 smoke test 或 mock 环境"

#### Step 10.2: `--push-notion` 帮助文本提及 schema 校验
- 提示: "推送前默认校验 Notion database schema"

---

### Issue 11: Notion block converter HTML 保真度

**当前覆盖:** heading、paragraph、list、quote、image、toggle、link、bold、italic  
**缺失:** table、code、callout、nested list depth、local image URL

#### Step 11.1: 新增 `CodeBlock` 类型
- **文件**: [NotionBlockTypes.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Shared/Notion/NotionBlockTypes.cs)
- `CodeBlock(string Code, string Language = "plain text")`
- `WriteBlock` 中增加 code 序列化分支

#### Step 11.2: `ParseBlocks` 增加 `<pre><code>` 处理
- **文件**: [HtmlToNotionBlockConverter.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Shared/Notion/HtmlToNotionBlockConverter.cs)
- 遇到 `pre` + `code` 标签 → 提取代码内容 + 语言属性 → `CodeBlock`

#### Step 11.3: 新增 `CalloutBlock` 类型
- `CalloutBlock(string Text, string? Icon = "📝")`
- `<div class="callout">` → callout block

#### Step 11.4: `<table>` 简易支持
- 读取 `<table>` → 提取 `<tr>` `<td>` → 转换为结构化文本 paragraph fallback
- 后续迭代可支持 Notion native table block

#### Step 11.5: 嵌套列表深度
- 当前支持单层 ul/ol
- 增加嵌套识别: `<li>` 内含 `<ul>/<ol>` → 缩进为 Notion indented list

---

## 三、实施顺序

```
Phase 1 (P1 功能修复，并行):
├─ Issue 1: append failed 报告
├─ Issue 2: --update-content replace
├─ Issue 3: route-map → site.yaml appPages
└─ Issue 4: GetSlugFromRouteMap 动态路由防护

Phase 2 (P1 测试补齐):
├─ Issue 5: route-map 贯通测试 (3 tests)
├─ Issue 6: upsert query-failed 测试
├─ Issue 7: schema validate 阻止 push 测试
└─ Issue 8: notion-only no-markdown 回归测试

Phase 3 (P2 健壮性，并行):
├─ Issue 9: RouteMapLoader 错误报告
├─ Issue 10: 文档/提示明确
└─ Issue 11: table/code/callout/nested-list/link
```

## 四、测试验证

1. **Issue 1**: 模拟 blocks append 返回 400 → 输出 "append-failed"，failed=1
2. **Issue 2**: 执行两次 `--update-content replace` → 第二次结果仅含新正文（不含旧正文）
3. **Issue 3**: route-map 含 china-companies → site.yaml 有 `appPages.china-companies`
4. **Issue 4**: route-map 写 `/insights/{slug}/` → slug 不变成 `{slug}`
5. **Issues 5-8**: 新测试全部通过
6. **Issue 11**: `<pre><code>` HTML → Notion code block
