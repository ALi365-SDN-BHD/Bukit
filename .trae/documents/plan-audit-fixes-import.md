# Audit Fixes for Bukit Import Module

> 基于 commit `e9098f8` 的审计修复计划

***

## 修复 1：ContentExtractor 中文 slug 生成

**当前问题：**
[ContentExtractor.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/ContentExtractor.cs) 的私有 `Slugify` 方法使用 `Regex.Replace(slug, @"[^a-z0-9\s-]", "")` 直接删除所有非英文字符。中文标题（如"关于我们"）产生空 slug。

`Bukit.Shared.SlugHelper.Slugify` 已存在且通过 Unicode 规范化保留中文字符，但未被使用。

**修改文件：** `src/Bukit.Importing/ContentExtractor.cs`

**实施步骤：**

1. 删除私有 `Slugify` 方法
2. 所有 6 个调用位置改用 `Bukit.Shared.SlugHelper.Slugify`
3. 新增 `GetSlugWithFallback` 辅助方法：若 Slugify 返回空，按类别生成确定性回退：

   * `post-001` / `post-002` ...（PostRecord）

   * `company-001` / `company-002` ...（CompanyRecord）

   * `service-001` / `service-002` ...（ServiceRecord）
4. 回退时输出 warning
5. 新增 `tests/Bukit.Importing.Tests/ContentExtractorChineseTests.cs`：测试中文标题的 posts/companies/services 卡片

***

## 修复 2：返回退出码 2 用于导入用户/配置错误

**当前问题：**
[ImportCommand.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/ImportCommand.cs) 第 139-143 行捕获所有 `Exception` 并统一返回 `1`。以下应返回 `2`（用户输入错误）：

* 缺少 index.html

* 主题已存在但无 `--force`

* 敏感文件检测

* 无效输入

* strict 模式诊断失败

**修改文件：**

* `src/Bukit.Importing/ImportDiagnostics.cs`（新增）

* `src/Bukit.Cli/Commands/ImportCommand.cs`

**实施步骤：**

1. 在 `ImportDiagnostics.cs` 中新增 `ImportException` 类：

   ```csharp
   public sealed class ImportException : Exception
   {
       public ImportErrorKind Kind { get; }
       public ImportException(ImportErrorKind kind, string message) : base(message)
       { Kind = kind; }
   }

   public enum ImportErrorKind
   {
       UserInput,   // 返回退出码 2
       Internal     // 返回退出码 1
   }
   ```
2. 修改 `HtmlDemoImporter.ValidateInput` 抛出 `ImportException`（Kind=UserInput）
3. 修改 `ImportCommand.HtmlDemoAsync` 捕获：

   * `ImportException` 且 `Kind == UserInput` → 返回 `2`

   * `ImportException` 且 `Kind == Internal` → 返回 `1`

   * 其他 `Exception` → 返回 `1`
4. 更新测试中期望 `1` 的敏感文件测试改为期望 `2`

***

## 修复 3：Notion Push 行为文档化

**当前问题：**
`notion push` 命令仅生成计划/报告，不调用 Notion API。但 CLI help 可能暗示真实 Notion 写入。

**修改文件：**

* `src/Bukit.Cli/Cli/BukitCliSpecs.cs`

* `tests/Bukit.Cli.Tests/`（新测试）

**实施步骤：**

1. 更新 `notion push` CLI help 描述为 "生成 Notion push 计划（不执行实际 Notion API 调用）"
2. 更新 `--dry-run` 描述区分"预览计划"与"执行计划"
3. 新增测试 `NotionPush_NonDryRun_WithoutToken_Returns2`
4. 新增测试 `NotionPush_NonDryRun_WithToken_OnlyWritesReport`

***

## 修复 4：添加中文 Demo Fixture

**当前问题：**
不存在包含中文文章卡片、企业卡片、服务卡片的 test fixture。

**修改文件：**

* `tests/fixtures/html-demo-import/chinese-demo/`（新建目录 + 文件）

* `tests/Bukit.Importing.Tests/`（新集成测试）

**实施步骤：**

1. 创建 `tests/fixtures/html-demo-import/chinese-demo/` 目录
2. 创建以下 HTML 文件：

   * `index.html` — 首页，含 SEO title/description、英雄区、CTA

   * `about.html` — 关于页面，中文正文

   * `insights.html` — 文章列表页，3 个中文 article-card（含 SEO title/description）

   * `companies.html` — 企业列表页，3 个中文 company-card

   * `services.html` — 服务列表页，2 个中文 service-card

   * `assets/css/style.css` — 嵌套资源

   * `assets/img/logo.png` — 嵌套图片
3. 创建集成测试 `ChineseDemoIntegrationTests`：

   * 导入成功（退出码 0）

   * 所有 slug 非空

   * `bukit build` 成功

   * 报告已生成

   * content drafts 存在（`sites/<name>/content/` 目录）

***

## 修复 5：Dist 资源路径测试

**当前问题：**
`AssetImporter` 将资源移动到 `static/` 并重写路径为根相对路径。没有测试验证生成 HTML 中的资源引用指向 `dist/` 中实际存在的文件。

**修改文件：**

* `tests/Bukit.Importing.Tests/HtmlDemoImporterTests.cs`（追加）

**实施步骤：**

1. 新增测试 `Import_BuildAndVerifyAssetPaths`：

   * 导入含 `<img src="img/logo.png" />` 和 `<link href="css/style.css" />` 的 demo

   * 执行 `bukit build`

   * 打开生成的 `dist/index.html`

   * 验证 `<img src="...">` 和 `<link href="...">` 引用路径不以 `/assets/` 开头（因为 TransferAssetsToStatic 已移动）

   * 验证引用的文件在 `dist/` 中实际存在

***

## 修复 6：清理 CLI 标志

**当前问题：**
`--preserve-html` 和 `--report` 是正向标志，但默认行为应该是启用它们。应改为 `--no-preserve-html` 和 `--no-report`。

**修改文件：**

* `src/Bukit.Cli/Cli/BukitCliSpecs.cs`

* `src/Bukit.Cli/Commands/ImportCommand.cs`

**实施步骤：**

1. 将 `--preserve-html` 改为 `--no-preserve-html`（默认 preserve）
2. 将 `--report` 改为 `--no-report`（默认 generate report）
3. 更新 `ImportCommand.HtmlDemoAsync` 中对应变量的取反逻辑
4. 更新 `HtmlDemoImportOptions` 默认值（`PreserveHtml = true`，`GenerateReport = true`）
5. 更新测试用例

***

## 修复 7：验证

**验证命令：**

```bash
dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj -c Release
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release
```

**验收标准：**

1. 中文 demo 导入不产生空 slug
2. 导入用户错误返回退出码 2
3. 生成的站点构建成功
4. 生成 HTML 中的 dist 资源引用指向存在的文件
5. Notion push 行为准确文档化为计划/暂存模式

***

## 修改文件汇总

| 操作     | 文件                                                                           |
| ------ | ---------------------------------------------------------------------------- |
| **修改** | `src/Bukit.Importing/ContentExtractor.cs` — Slugify → SlugHelper             |
| **修改** | `src/Bukit.Importing/ImportModels.cs` — ImportException + ImportErrorKind    |
| **修改** | `src/Bukit.Importing/HtmlDemoImporter.cs` — ValidateInput 抛出 ImportException |
| **修改** | `src/Bukit.Cli/Commands/ImportCommand.cs` — 区分退出码 1/2                        |
| **修改** | `src/Bukit.Cli/Cli/BukitCliSpecs.cs` — 更新 CLI 标志 + notion push 描述            |
| **新建** | `tests/fixtures/html-demo-import/chinese-demo/`（5 个 HTML + 资源）               |
| **新建** | `tests/Bukit.Importing.Tests/ContentExtractorChineseTests.cs`                |
| **新建** | `tests/Bukit.Importing.Tests/ChineseDemoIntegrationTests.cs`                 |
| **新建** | `tests/Bukit.Cli.Tests/NotionCommandTests.cs`（追加 2 个测试）                      |
| **修改** | `tests/Bukit.Importing.Tests/HtmlDemoImporterTests.cs` — 追加 dist 资源路径测试      |
| **修改** | `tests/Bukit.Cli.Tests/ImportCommandTests.cs` — 更新退出码预期                      |

