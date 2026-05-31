# 计划：`bukit import html-demo` 完整开发实施方案

> 基于 `/docs/demo_to_bukit_workflow.md` 第 18 节「统一 Demo 导入方案」

---

## 一、总体架构

### 1.1 命令设计

```
bukit import html-demo <demo-dir> --theme <name> [options]
```

`import` 为父命令，`html-demo` 为子命令（遵循现有 `seo audit`、`theme create` 模式）。

### 1.2 导入流水线

```
Phase 0: 输入验证
  ↓
Phase 1: 页面发现与分类
  ↓
Phase 2: 公共结构识别 (header/nav/footer/layout)
  ↓
Phase 3: 组件识别 (hero/card/faq/cta...)
  ↓
Phase 4: 内容抽取与数据化 (pages/sections/posts/companies/faqs)
  ↓
Phase 5: 主题模板生成
  ↓
Phase 6: 种子数据生成 (notion-seed/*.json)
  ↓
Phase 7: site.yaml 生成
  ↓
Phase 8: 资源处理 (CSS/JS/images)
  ↓
Phase 9: 安全扫描
  ↓
Phase 10: 导入报告生成
```

### 1.3 涉及文件

| 操作 | 文件 | 层级 |
|------|------|------|
| **新建** | `src/Bukit.Cli/Commands/ImportCommand.cs` | CLI 入口 |
| **新建** | `src/Bukit.Importing/HtmlDemoImporter.cs` | 核心导入器 |
| **新建** | `src/Bukit.Importing/HtmlDemoScanner.cs` | 页面扫描 |
| **新建** | `src/Bukit.Importing/HtmlDocumentParser.cs` | HTML 解析 |
| **新建** | `src/Bukit.Importing/PageClassifier.cs` | 页面分类 |
| **新建** | `src/Bukit.Importing/LayoutExtractor.cs` | 公共结构提取 |
| **新建** | `src/Bukit.Importing/ComponentExtractor.cs` | 组件识别 |
| **新建** | `src/Bukit.Importing/ContentExtractor.cs` | 内容抽取 |
| **新建** | `src/Bukit.Importing/AssetImporter.cs` | 资源导入 |
| **新建** | `src/Bukit.Importing/ThemeGenerator.cs` | 主题模板生成 |
| **新建** | `src/Bukit.Importing/SeedGenerator.cs` | 种子数据生成 |
| **新建** | `src/Bukit.Importing/SiteConfigGenerator.cs` | site.yaml 生成 |
| **新建** | `src/Bukit.Importing/ImportReportWriter.cs` | 报告写入 |
| **新建** | `src/Bukit.Importing/ImportSafetyScanner.cs` | 安全扫描 |
| **新建** | `src/Bukit.Importing/ImportDiagnostics.cs` | 诊断数据类型 |
| **新建** | `src/Bukit.Importing/ImportModels.cs` | 数据模型 |
| **新建** | `src/Bukit.Importing/Bukit.Importing.csproj` | 项目文件 |
| **新建** | `tests/Bukit.Importing.Tests/*` | 测试 |
| **新建** | `tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj` | 测试项目 |
| **修改** | `src/Bukit.Cli/Bukit.Cli.csproj` | 添加项目引用 |
| **修改** | `src/Bukit.Cli/Cli/BukitCliSpecs.cs` | 注册命令规范 |
| **修改** | `src/Bukit.Cli/Program.cs` | 添加分发 |

### 1.4 模块职责

```text
Bukit.Importing/                    ← 新项目，核心逻辑
  ├── ImportModels.cs               ← 数据模型 (Phase 0)
  ├── ImportDiagnostics.cs          ← 诊断数据 (Phase 0)
  ├── HtmlDocumentParser.cs         ← 单个 HTML 解析 (Phase 1)
  ├── HtmlDemoScanner.cs            ← 目录扫描 (Phase 1)
  ├── PageClassifier.cs             ← 页面分类 (Phase 1)
  ├── LayoutExtractor.cs            ← 公共结构提取 (Phase 2)
  ├── ComponentExtractor.cs         ← 组件识别 (Phase 3)
  ├── ContentExtractor.cs           ← 内容抽取 (Phase 4)
  ├── AssetImporter.cs              ← 资源处理 (Phase 8)
  ├── ThemeGenerator.cs             ← 主题模板生成 (Phase 5)
  ├── SeedGenerator.cs              ← 种子数据生成 (Phase 6)
  ├── SiteConfigGenerator.cs        ← site.yaml 生成 (Phase 7)
  ├── ImportSafetyScanner.cs        ← 安全扫描 (Phase 9)
  ├── ImportReportWriter.cs         ← 报告写入 (Phase 10)
  └── HtmlDemoImporter.cs           ← 编排器

Bukit.Cli/Commands/
  └── ImportCommand.cs              ← CLI 入口，参数解析+分发
```

---

## 二、分阶段实施计划

### 阶段 A：MVP（最小可行产品）

**目标：** 实现 `bukit import html-demo ./demo --theme silkroadbiz` 即可运行，覆盖 Phase 0/1/2/5/7/8/9/10。V1 复用现有 CloneFidelity 基础设施做 layout 拆分和模板生成。

**不包含：**
- 组件识别（Phase 3）
- 内容抽取与种子数据（Phase 4/6）
- 多内容源支持（仅 markdown）
- `--dry-run`、`--strict` 模式
- 完整报告（仅打印摘要）

**命令：**
```bash
bukit import html-demo ./demo --theme silkroadbiz [--force] [--use] [--verify]
```

### 阶段 B：内容数据化

**目标：** 实现 Phase 3/4/6——组件识别、内容抽取、种子数据生成。

**新增选项：**
- `--extract-content`（默认 true）
- `--generate-seed`（默认 true）
- `--content-source notion|json`（默认 notion）

### 阶段 C：完整功能

**目标：** 实现 Phase 0 全量验证、`--dry-run`、`--strict`、`--overwrite`、`--preserve-html`、`--report`、`--language`、`--base-url`。

---

## 三、阶段 A 详细实施方案

### A-1：创建 `Bukit.Importing` 项目

在 solution 中添加新项目：

**文件：** `src/Bukit.Importing/Bukit.Importing.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Bukit.Shared\Bukit.Shared.csproj" />
  </ItemGroup>
</Project>
```

更新 solution 文件，添加 `Bukit.Importing` 项目。

**文件：** `src/Bukit.Cli/Bukit.Cli.csproj` — 添加：

```xml
<ProjectReference Include="..\Bukit.Importing\Bukit.Importing.csproj" />
```

---

### A-2：Phase 0 — 输入验证 + 数据模型

#### A-2.1：`ImportModels.cs`

```csharp
namespace Bukit.Importing;

public sealed record HtmlDemoImportOptions
{
    public required string InputPath { get; init; }
    public required string ThemeName { get; init; }
    public required string RootDir { get; init; }
    public bool Force { get; init; }
    public bool Use { get; init; }
    public bool Verify { get; init; }
    public string Language { get; init; } = "en";
}

public sealed record DiscoveredPage
{
    public required string FilePath { get; init; }
    public required string RelativePath { get; init; }
    public required string Slug { get; init; }
    public required PageType Type { get; init; }
    public string? Title { get; init; }
    public string? HeadContent { get; init; }
    public string? BodyContent { get; init; }
}

public enum PageType
{
    Home,           // index.html
    Page,           // about, contact, etc.
    PostList,       // insights, blog, news
    PostDetail,     // article, post
    CompanyList,    // companies
    CompanyDetail,  // company, company-detail
    ServiceList,    // services
    ServiceDetail,  // service-detail
    Unknown
}

public sealed record ImportResult
{
    public required string ThemePath { get; init; }
    public int PagesFound { get; init; }
    public int TemplatesGenerated { get; init; }
    public int PartialsGenerated { get; init; }
    public int AssetsCopied { get; init; }
    public bool SiteYamlCreated { get; init; }
    public bool TemplatesSynced { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
```

#### A-2.2：`ImportDiagnostics.cs`

```csharp
namespace Bukit.Importing;

public sealed record ImportDiagnostic(
    ImportDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? FilePath = null,
    int? LineNumber = null);

public enum ImportDiagnosticSeverity
{
    Info,
    Warning,
    Error
}
```

#### A-2.3：`HtmlDemoImporter.cs` 初始骨架

```csharp
namespace Bukit.Importing;

public static class HtmlDemoImporter
{
    public static ImportResult Import(HtmlDemoImportOptions options)
    {
        // Phase 0: 验证输入
        ValidateInput(options);

        // Phase 1: 页面发现
        var pages = HtmlDemoScanner.Scan(options.InputPath);

        // Phase 2: 公共结构识别
        var layout = LayoutExtractor.Extract(pages);

        // Phase 5: 主题生成
        var result = ThemeGenerator.Generate(options, pages, layout);

        // Phase 7: site.yaml
        SiteConfigGenerator.Generate(options, result);

        // Phase 8: 资源处理
        AssetImporter.Import(options, pages);

        // Phase 9: 安全扫描
        var diagnostics = ImportSafetyScanner.Scan(options, pages);

        // Phase 10: 报告
        ImportReportWriter.Write(options, result, diagnostics);

        return result;
    }

    private static void ValidateInput(HtmlDemoImportOptions options)
    {
        // 1. 输入目录必须存在
        // 2. 至少包含一个 index.html
        // 3. 主题名必须安全 (CloneModels.IsSafeThemeName / ThemeNameSanitizer.TrySanitize)
        // 4. 如果主题已存在且未 --force，报错
        // 5. 拒绝危险文件 (.env, *.key, *.pem, .git/, node_modules/)
    }
}
```

---

### A-3：Phase 1 — 页面发现与分类

#### A-3.1：`HtmlDemoScanner.cs`

**职责：** 递归扫描 `*.html` 文件，解析基本结构。

```csharp
namespace Bukit.Importing;

internal static class HtmlDemoScanner
{
    internal static List<DiscoveredPage> Scan(string inputPath)
    {
        var htmlFiles = Directory.GetFiles(inputPath, "*.html", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return htmlFiles.Select(f => ParseFile(f, inputPath)).ToList();
    }

    private static DiscoveredPage ParseFile(string filePath, string baseDir)
    {
        // 委托给 HtmlDocumentParser
        return HtmlDocumentParser.Parse(filePath, baseDir);
    }
}
```

#### A-3.2：`HtmlDocumentParser.cs`

**职责：** 解析单个 HTML 文件，提取 head/body/title。

```csharp
namespace Bukit.Importing;

internal static class HtmlDocumentParser
{
    internal static DiscoveredPage Parse(string filePath, string baseDir)
    {
        var html = File.ReadAllText(filePath);
        var relativePath = Path.GetRelativePath(baseDir, filePath);
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var slug = fileName.ToLowerInvariant() == "index" ? "" : fileName.ToLowerInvariant();

        var title = ExtractTitle(html);
        var headContent = ExtractBetween(html, "<head>", "</head>"); // 简化版
        var bodyContent = ExtractBetween(html, "<body", "</body>");  // 简化版
        var pageType = PageClassifier.Classify(fileName, html);

        return new DiscoveredPage
        {
            FilePath = filePath,
            RelativePath = relativePath,
            Slug = slug,
            Type = pageType,
            Title = title,
            HeadContent = headContent,
            BodyContent = bodyContent
        };
    }
}
```

#### A-3.3：`PageClassifier.cs`

**职责：** 根据文件名和 HTML 内容特征判断页面类型。

```csharp
namespace Bukit.Importing;

internal static class PageClassifier
{
    private static readonly Dictionary<string, PageType> FileNameMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["index"] = PageType.Home,
        ["about"] = PageType.Page,
        ["contact"] = PageType.Page,
        ["privacy"] = PageType.Page,
        ["terms"] = PageType.Page,
        ["insights"] = PageType.PostList,
        ["blog"] = PageType.PostList,
        ["news"] = PageType.PostList,
        ["article"] = PageType.PostDetail,
        ["article-detail"] = PageType.PostDetail,
        ["post"] = PageType.PostDetail,
        ["companies"] = PageType.CompanyList,
        ["company"] = PageType.CompanyDetail,
        ["company-detail"] = PageType.CompanyDetail,
        ["services"] = PageType.ServiceList,
        ["service-detail"] = PageType.ServiceDetail,
    };

    internal static PageType Classify(string fileNameWithoutExtension, string html)
    {
        if (FileNameMapping.TryGetValue(fileNameWithoutExtension, out var type))
            return type;

        // 根据 HTML 内容特征进一步判断
        // 例如：包含多个 .article-card → PostList
        // 包含 .company-card → CompanyList
        return PageType.Unknown;
    }
}
```

---

### A-4：Phase 2 — 公共结构识别

#### A-4.1：`LayoutExtractor.cs`

**职责：** 识别所有页面的公共 header/nav/footer。

**V1 策略：** 直接复用现有的 `CloneFidelityCommonBlocks.ExtractCommonBlocks`，它已经实现了：
1. 多页面间的最长公共前缀（header）
2. `<nav>` 标签提取
3. 最长公共后缀（footer）

```csharp
namespace Bukit.Importing;

internal static class LayoutExtractor
{
    internal sealed record LayoutInfo(
        string Header,
        string Nav,
        string Footer,
        string HeadCommon);

    internal static LayoutInfo Extract(List<DiscoveredPage> pages)
    {
        // V1: 复用 CloneFidelityCommonBlocks.ExtractCommonBlocks
        // 将 DiscoveredPage 适配为 FidelityPage
        // ...
    }
}
```

**注意：** `CloneFidelityCommonBlocks` 是 `internal` 且位于 `Bukit.Cli` 项目中。V1 可以直接在 `Bukit.Importing` 中复制其核心逻辑，或者在 CLI 层调用后传入结果。

**决策：** V1 在 `LayoutExtractor` 中内联实现最长公共前缀/后缀提取（与 `CloneFidelityCommonBlocks` 相同的算法但独立），避免跨项目 internal 依赖。

---

### A-5：Phase 5 — 主题模板生成

#### A-5.1：`ThemeGenerator.cs`

**职责：** 生成主题目录结构和模板文件。

```csharp
namespace Bukit.Importing;

internal static class ThemeGenerator
{
    internal static ImportResult Generate(
        HtmlDemoImportOptions options,
        List<DiscoveredPage> pages,
        LayoutExtractor.LayoutInfo layout)
    {
        var themeDir = Path.Combine(options.RootDir, "themes", options.ThemeName);

        // 强制时删除已有主题
        if (Directory.Exists(themeDir) && options.Force)
            Directory.Delete(themeDir, recursive: true);

        // 创建目录结构
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "partials"));
        Directory.CreateDirectory(Path.Combine(themeDir, "assets"));
        Directory.CreateDirectory(Path.Combine(themeDir, "static"));

        // 写入 partials: header.html, nav.html, footer.html
        WritePartial(themeDir, "header.html", layout.Header);
        WritePartial(themeDir, "nav.html", layout.Nav);
        WritePartial(themeDir, "footer.html", layout.Footer);

        // 写入 base.html layout
        WriteBaseLayout(themeDir, layout);

        // 为每个页面生成模板
        var templateCount = 0;
        foreach (var page in pages)
        {
            var templatePath = Path.Combine(themeDir, "layouts", "pages",
                GetTemplateFileName(page));
            WritePageTemplate(templatePath, page);
            templateCount++;
        }

        // 写入 index.html 和 list.html（如果不需要覆盖已有页面模板）
        WriteIndexTemplate(themeDir, pages);
        WriteListTemplate(themeDir);
        templateCount += 2;

        return new ImportResult
        {
            ThemePath = themeDir,
            PagesFound = pages.Count,
            TemplatesGenerated = templateCount,
            PartialsGenerated = CountPartials(layout),
            AssetsCopied = 0, // 由 AssetImporter 填充
            SiteYamlCreated = false,
            TemplatesSynced = false
        };
    }

    private static string GetTemplateFileName(DiscoveredPage page)
    {
        var baseName = page.Slug switch
        {
            "" => "index",
            _ => page.Slug
        };

        return page.Type switch
        {
            PageType.Home => "index.html",
            PageType.Page => "page.html",
            PageType.PostList => $"{baseName}.html",
            PageType.PostDetail => "article.html",
            PageType.CompanyList => "companies.html",
            PageType.CompanyDetail => "company.html",
            _ => "page.html"
        };
    }
}
```

---

### A-6：Phase 7 — site.yaml 生成

#### `SiteConfigGenerator.cs`

**职责：** 在 `rootDir` 生成基础 `site.yaml`。

```csharp
namespace Bukit.Importing;

internal static class SiteConfigGenerator
{
    internal static bool Generate(HtmlDemoImportOptions options, ImportResult result)
    {
        var yamlPath = Path.Combine(options.RootDir, "site.yaml");
        if (File.Exists(yamlPath))
            return false; // 已存在，不覆盖

        var sb = new StringBuilder();
        sb.AppendLine("site:");
        sb.AppendLine($"  name: {options.ThemeName}");
        sb.AppendLine($"  title: {options.ThemeName}");
        sb.AppendLine("  baseUrl: /");
        sb.AppendLine($"  language: {options.Language}");
        sb.AppendLine("  seo:");
        sb.AppendLine("    renderMode: 'off'");
        sb.AppendLine("  collections:");
        sb.AppendLine("    page:");
        sb.AppendLine("      permalink: '/{slug}/'");
        sb.AppendLine("      template: 'pages/page.html'");
        sb.AppendLine("      listRoute: '/'");
        sb.AppendLine("content:");
        sb.AppendLine("  provider: markdown");
        sb.AppendLine("  contentDir: content");
        sb.AppendLine("theme:");
        sb.AppendLine($"  name: {options.ThemeName}");

        File.WriteAllText(yamlPath, sb.ToString());
        return true;
    }
}
```

---

### A-7：Phase 8 — 资源处理

#### `AssetImporter.cs`

**职责：** 复制 CSS/JS/images 到主题目录。

```csharp
namespace Bukit.Importing;

internal static class AssetImporter
{
    private static readonly HashSet<string> SensitiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env", ".key", ".pem", ".pfx", ".p12", ".crt", ".cert"
    };

    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", ".vscode", "dist", "build"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".ico", ".bmp"
    };

    private static readonly HashSet<string> DangerousProtocols = new(StringComparer.OrdinalIgnoreCase)
    {
        "javascript:", "vbscript:", "file:"
    };

    internal static int Import(HtmlDemoImportOptions options, List<DiscoveredPage> pages)
    {
        var themeDir = Path.Combine(options.RootDir, "themes", options.ThemeName);
        var count = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in pages)
        {
            foreach (var asset in ExtractAssetRefs(page))
            {
                if (!seen.Add(asset.Path)) continue;
                if (!IsSafeAsset(asset, options.InputPath)) continue;

                var sourcePath = Path.GetFullPath(
                    Path.Combine(options.InputPath, asset.Path.TrimStart('/')));

                if (!sourcePath.StartsWith(Path.GetFullPath(options.InputPath),
                        StringComparison.OrdinalIgnoreCase))
                    continue; // 路径穿越拒绝

                if (!File.Exists(sourcePath)) continue;

                var isImage = ImageExtensions.Contains(
                    Path.GetExtension(asset.Path).ToLowerInvariant());
                var destSubDir = isImage ? "assets" : "static";
                var destPath = Path.Combine(themeDir, destSubDir,
                    asset.Path.TrimStart('/'));

                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(sourcePath, destPath, overwrite: true);
                count++;
            }
        }

        return count;
    }

    private static bool IsSafeAsset(AssetRef asset, string baseDir)
    {
        // 拒绝危险协议
        if (DangerousProtocols.Any(p => asset.Path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return false;

        // 拒绝敏感文件
        var fileName = Path.GetFileName(asset.Path);
        var ext = Path.GetExtension(fileName);
        if (SensitiveExtensions.Contains(ext)) return false;
        if (SensitiveNames.Contains(fileName)) return false;

        return true;
    }

    private sealed record AssetRef(string Path, string AttrType);
    // 从 HTML 中提取 src= 和 href=
}
```

---

### A-8：Phase 9 — 安全扫描

#### `ImportSafetyScanner.cs`

```csharp
namespace Bukit.Importing;

internal static class ImportSafetyScanner
{
    private static readonly string[] SensitiveFilePatterns =
    [
        ".env", ".git", ".npmrc", "*.key", "*.pfx", "*.p12",
        "*.pem", "*.crt", "*.cert", "node_modules", ".vscode", "dist", "build"
    ];

    internal static List<ImportDiagnostic> Scan(
        HtmlDemoImportOptions options, List<DiscoveredPage> pages)
    {
        var diagnostics = new List<ImportDiagnostic>();

        // 检查输入目录中的敏感文件
        ScanSensitiveFiles(options.InputPath, diagnostics);

        // 检查 HTML 中的危险内容
        foreach (var page in pages)
            ScanHtmlContent(page, diagnostics);

        return diagnostics;
    }

    private static void ScanSensitiveFiles(string inputPath,
        List<ImportDiagnostic> diagnostics)
    {
        // 遍历目录，发现敏感文件时添加 Error 诊断
    }

    private static void ScanHtmlContent(DiscoveredPage page,
        List<ImportDiagnostic> diagnostics)
    {
        // 检查 inline script、iframe、javascript: URL、onclick 等
    }
}
```

---

### A-9：Phase 10 — 导入报告

#### `ImportReportWriter.cs`

**职责：** 打印迁移报告到控制台。

```csharp
namespace Bukit.Importing;

internal static class ImportReportWriter
{
    internal static void Write(HtmlDemoImportOptions options,
        ImportResult result, List<ImportDiagnostic> diagnostics)
    {
        var errors = diagnostics.Count(d => d.Severity == ImportDiagnosticSeverity.Error);
        var warnings = diagnostics.Count(d => d.Severity == ImportDiagnosticSeverity.Warning);

        Console.WriteLine($"迁移完成: {options.ThemeName}");
        Console.WriteLine($"  HTML 页面扫描:   {result.PagesFound}");
        Console.WriteLine($"  模板生成:        {result.TemplatesGenerated}");
        Console.WriteLine($"  局部模板生成:    {result.PartialsGenerated}");
        Console.WriteLine($"  资源复制:        {result.AssetsCopied}");
        Console.WriteLine($"  错误:            {errors}");
        Console.WriteLine($"  警告:            {warnings}");
        Console.WriteLine($"  site.yaml:        {(result.SiteYamlCreated ? "已创建" : "已跳过（已存在）")}");
        Console.WriteLine($"  bukit.templates.yaml: {(result.TemplatesSynced ? "已创建" : "已跳过")}");

        foreach (var w in result.Warnings)
            Console.WriteLine($"  Warning: {w}");

        if (diagnostics.Count > 0)
        {
            Console.WriteLine();
            foreach (var d in diagnostics.Where(d => d.Severity >= ImportDiagnosticSeverity.Warning))
                Console.WriteLine($"  [{d.Severity}] {d.Code}: {d.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("后续步骤:");
        Console.WriteLine("  bukit dev");
        Console.WriteLine("  bukit build");
        Console.WriteLine("  bukit doctor");
    }
}
```

---

### A-10：CLI 入口 — `ImportCommand.cs`

**文件：** `src/Bukit.Cli/Commands/ImportCommand.cs`

```csharp
using Bukit.Cli.Cli.Binding;
using Bukit.Importing;

namespace Bukit.Cli.Commands;

public static class ImportCommand
{
    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var sub = command.GetArgument(0);
        return sub switch
        {
            "html-demo" => await HtmlDemoAsync(command),
            _ => Unknown(sub)
        };
    }

    private static async Task<int> HtmlDemoAsync(CliBoundCommand command)
    {
        // 1. 验证 demo 目录参数
        var demoDirArg = command.GetArgument(1);
        if (string.IsNullOrWhiteSpace(demoDirArg))
        {
            Console.Error.WriteLine("缺少必填参数: <demo-dir>");
            return 2;
        }
        var demoDir = Path.GetFullPath(demoDirArg);
        if (!Directory.Exists(demoDir))
        {
            Console.Error.WriteLine($"demo 目录不存在: {demoDir}");
            return 2;
        }

        // 2. 验证 --theme
        var themeName = command.GetString("--theme");
        if (string.IsNullOrWhiteSpace(themeName))
        {
            Console.Error.WriteLine("缺少必填选项: --theme <名称>");
            return 2;
        }
        if (!CloneModels.IsSafeThemeName(themeName))
        {
            Console.Error.WriteLine($"无效的主题名: {themeName}");
            return 2;
        }

        // 3. 解析选项
        var force = command.GetBool("--force");
        var use = command.GetBool("--use");
        var verify = command.GetBool("--verify");

        // 4. 根目录解析
        var resolved = ConfigPathResolver.Resolve(
            command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;

        // 5. 检查主题是否已存在
        var themeDir = Path.Combine(rootDir, "themes", themeName);
        if (Directory.Exists(themeDir) && !force)
        {
            Console.Error.WriteLine($"主题已存在: {themeName}。使用 --force 覆盖。");
            return 2;
        }

        // 6. 执行导入
        var options = new HtmlDemoImportOptions
        {
            InputPath = demoDir,
            ThemeName = themeName,
            RootDir = rootDir,
            Force = force,
            Use = use,
            Verify = verify
        };

        ImportResult result;
        try
        {
            result = HtmlDemoImporter.Import(options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"导入失败: {ex.Message}");
            return 1;
        }

        // 7. 模板同步
        await SyncTemplatesAsync(rootDir, themeName);
        // result.TemplatesSynced = true; // 需要可变的 ImportResult 或重建

        // 8. 如果 --use，设置主题
        if (use)
        {
            var resolved2 = ConfigPathResolver.Resolve(
                command.GetString("--config"), command.GetString("--site"));
            var useResult = await ThemeCommand.SetThemeAsync(themeName,
                resolved2.FullConfigPath, resolved2.RootDir,
                brand: null, primaryColor: null, accentColor: null);
            if (useResult != 0) return useResult;
        }

        // 9. 如果 --verify，运行验证
        if (verify)
        {
            var verifyResult = await CloneVerifier.VerifyCloneAsync(
                command, rootDir, failOnVisualDiff: false, visualThreshold: 0.03);
            if (verifyResult != 0) return verifyResult;
        }

        return 0;
    }

    private static async Task SyncTemplatesAsync(string rootDir, string themeName)
    {
        // 复用 TemplateCommand 的同步逻辑
        // V1: 构造一个模拟的 CliBoundCommand 调用 TemplateCommand.SyncAsync
        // 或提取 SyncThemeAsync 内部方法
    }

    private static int Unknown(string sub)
    {
        Console.Error.WriteLine($"未知的 import 子命令: {sub}");
        Console.Error.WriteLine("可用: html-demo");
        return 2;
    }
}
```

---

### A-11：注册命令规范 + 分发

#### `BukitCliSpecs.cs` 新增：

```csharp
var importCmd = new CliCommandSpec(
    Name: "import",
    Description: "导入外部资源以创建 Bukit 主题或内容",
    Options: new[]
    {
        new CliOptionSpec("--config", "配置文件路径"),
        new CliOptionSpec("--site", "多站点名")
    },
    Subcommands: new[]
    {
        new CliCommandSpec(
            Name: "html-demo",
            Description: "将静态 HTML demo 目录迁移为 Bukit 主题",
            Arguments: new[] { new CliArgumentSpec("demo-dir", "HTML demo 目录路径", Required: true) },
            Options: new[]
            {
                new CliOptionSpec("--theme", "目标主题名", CliOptionType.String, ValueName: "name", Required: true),
                new CliOptionSpec("--force", "覆盖已有主题", CliOptionType.Flag),
                new CliOptionSpec("--use", "创建后切换到该主题", CliOptionType.Flag),
                new CliOptionSpec("--verify", "生成后执行 doctor/build 验证", CliOptionType.Flag),
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名")
            })
    });
```

在 `CreateRegistry()` 返回数组中添加 `importCmd`。

#### `Program.cs` 新增分发：

在 `SubcommandParseResult` switch 块中添加：
```csharp
"import" => await ImportCommand.RunAsync(merged),
```

---

## 四、阶段 A 测试计划

### 4.1 测试项目

**文件：** `tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj`
**文件：** `tests/Bukit.Importing.Tests/ImportModelsTests.cs`（数据模型测试）
**文件：** `tests/Bukit.Importing.Tests/HtmlDemoScannerTests.cs`
**文件：** `tests/Bukit.Importing.Tests/PageClassifierTests.cs`
**文件：** `tests/Bukit.Importing.Tests/LayoutExtractorTests.cs`
**文件：** `tests/Bukit.Importing.Tests/AssetImporterTests.cs`
**文件：** `tests/Bukit.Importing.Tests/ImportSafetyScannerTests.cs`
**文件：** `tests/Bukit.Importing.Tests/HtmlDemoImporterTests.cs`（集成测试）

### 4.2 CLI 层测试

**文件：** `tests/Bukit.Cli.Tests/ImportCommandTests.cs`

| 测试方法 | 验证内容 |
|----------|----------|
| `无子命令_返回2` | `import` 不带子命令 |
| `缺少DemoDir_返回2` | 无位置参数 |
| `DemoDir不存在_返回2` | 路径无效 |
| `缺少Theme_返回2` | 无 `--theme` |
| `无效主题名_返回2` | `../evil`、`/root` 被拒绝 |
| `主题已存在_无Force_返回2` | 目录已存在 |
| `主题已存在_有Force_覆盖` | `--force` 重建 |
| `单个Html文件_生成完整结构` | 验证 base.html、partials、pages |
| `多个Html文件_正确拆分` | 验证公共块提取 |
| `资源已复制` | 验证图片/资源到位 |
| `SiteYaml已创建` | `site.yaml` 生成 |
| `路径穿越_拒绝` | `../etc/passwd` 被跳过 |
| `敏感文件_排除` | `.env`、`.key` 不复制 |

### 4.3 导入器核心测试

| 测试类 | 关键测试 |
|--------|----------|
| `PageClassifierTests` | index→Home, about→Page, insights→PostList, article→PostDetail, companies→CompanyList, unknown→Unknown |
| `LayoutExtractorTests` | 单页面: header=bodyOpening, footer=bodyClosing; 多页面: 公共前缀=header, 公共后缀=footer |
| `AssetImporterTests` | 图片→assets, CSS→static; 敏感扩展被拒绝; 路径穿越被拒绝 |
| `ImportSafetyScannerTests` | 检测到 .env→Error; 检测到 inline script→Warning |

---

## 五、阶段 B 预览（内容数据化）

阶段 B 将在阶段 A 基础上添加：

### B-1：`ComponentExtractor.cs`

基于 CSS 类名和 HTML 结构识别可复用组件：

```csharp
namespace Bukit.Importing;

internal static class ComponentExtractor
{
    private static readonly Dictionary<string, string> ClassToComponent = new(StringComparer.OrdinalIgnoreCase)
    {
        [".hero"] = "hero",
        [".article-card"] = "article-card",
        [".company-card"] = "company-card",
        [".service-card"] = "service-card",
        [".faq-item"] = "faq",
        [".cta"] = "cta",
        [".pagination"] = "pagination",
        // ...
    };

    internal static List<DiscoveredComponent> Extract(List<DiscoveredPage> pages);
}

public sealed record DiscoveredComponent(
    string Name,
    string HtmlFragment,
    List<DiscoveredPage> UsedBy);
```

### B-2：`ContentExtractor.cs`

从 HTML 中抽取结构化内容：

- 页面级：H1、摘要 → pages.json
- 区块级：hero/cta/faq → sections.json
- 集合型：文章卡片 → posts.json、企业卡片 → companies.json

### B-3：`SeedGenerator.cs`

生成 `notion-seed/*.json` 文件。

### B-4：新增 CLI 选项

- `--extract-content`（默认 true）
- `--generate-seed`（默认 true）
- `--content-source notion|json`（默认 notion）
- `--site`（目标站点目录）

---

## 六、阶段 C 预览（完整功能）

- `--dry-run`：只分析不写入
- `--strict`：遇到硬编码残留/空 slug/重复 slug 直接失败
- `--overwrite`：覆盖已有文件
- `--preserve-html`：保留原始 HTML 快照
- `--report`：生成 `import-report.md`
- `--language`：设置默认语言
- `--base-url`：设置默认 URL

---

## 七、构建集成

### 7.1 Solution 更新

在 `Bukit.sln` 中添加：
```
src/Bukit.Importing/Bukit.Importing.csproj
tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj
```

### 7.2 构建验证

```bash
dotnet build Bukit.sln
dotnet test tests/Bukit.Importing.Tests/
dotnet test tests/Bukit.Cli.Tests/ --filter "FullyQualifiedName~Import"
dotnet test tests/Bukit.Cli.Tests/ --filter "FullyQualifiedName~CloneFidelity"
```

---

## 八、验收标准（阶段 A）

| 项目 | 标准 |
|------|------|
| 命令可用 | `bukit import html-demo ./demo --theme test` 可执行 |
| 主题结构 | 生成 `themes/<name>/layouts/layouts/`, `layouts/pages/`, `layouts/partials/`, `assets/`, `static/` |
| base.html | 包含 `{{ content }}` 和 partial includes |
| 页面模板 | 每个 HTML 页面对应一个模板，包含 `{% layout %}` 指令 |
| 公共结构 | header/nav/footer 提取为 partials |
| 资源复制 | 图片→assets，其他→static |
| site.yaml | 若不存在则生成 |
| 安全 | .env/.key 等敏感文件不复制，路径穿越被拒绝 |
| 现有功能 | `bukit clone --fidelity` 不受影响 |
| 测试 | 所有新增 + 已有测试通过 |

---

## 九、文件汇总

| 操作 | 文件 |
|------|------|
| **新建** | `src/Bukit.Importing/Bukit.Importing.csproj` |
| **新建** | `src/Bukit.Importing/ImportModels.cs` |
| **新建** | `src/Bukit.Importing/ImportDiagnostics.cs` |
| **新建** | `src/Bukit.Importing/HtmlDocumentParser.cs` |
| **新建** | `src/Bukit.Importing/HtmlDemoScanner.cs` |
| **新建** | `src/Bukit.Importing/PageClassifier.cs` |
| **新建** | `src/Bukit.Importing/LayoutExtractor.cs` |
| **新建** | `src/Bukit.Importing/ThemeGenerator.cs` |
| **新建** | `src/Bukit.Importing/SiteConfigGenerator.cs` |
| **新建** | `src/Bukit.Importing/AssetImporter.cs` |
| **新建** | `src/Bukit.Importing/ImportSafetyScanner.cs` |
| **新建** | `src/Bukit.Importing/ImportReportWriter.cs` |
| **新建** | `src/Bukit.Importing/HtmlDemoImporter.cs` |
| **新建** | `src/Bukit.Cli/Commands/ImportCommand.cs` |
| **新建** | `tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj` |
| **新建** | `tests/Bukit.Importing.Tests/*.cs`（6个测试文件） |
| **新建** | `tests/Bukit.Cli.Tests/ImportCommandTests.cs` |
| **修改** | `Bukit.sln` — 添加 2 个项目 |
| **修改** | `src/Bukit.Cli/Bukit.Cli.csproj` — 添加项目引用 |
| **修改** | `src/Bukit.Cli/Cli/BukitCliSpecs.cs` — 注册 import 命令 |
| **修改** | `src/Bukit.Cli/Program.cs` — 添加分发 |
