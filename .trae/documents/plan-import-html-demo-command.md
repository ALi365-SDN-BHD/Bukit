# 计划：新增 `bukit import html-demo` 命令

## 概述

新增一等命令 `bukit import html-demo <demo-dir> --theme <name>`，将静态 HTML demo 目录整体迁移为 Bukit 主题。复用现有的 `CloneFidelityGenerator` / `CloneFidelityRunner` 基础设施，但通过更清晰的 `import` 父命令 + `html-demo` 子命令暴露，并增加模板同步和验证步骤。

## 架构

```
bukit import html-demo ./demo --theme silkroadbiz --force --use --verify
       │      │
       │      └── 子命令 (html-demo)
       └── 父命令 (import)
```

### 需要新建的文件

| 文件 | 用途 |
|------|------|
| `src/Bukit.Cli/Commands/ImportCommand.cs` | 父命令分发 + `HtmlDemoAsync` 子命令处理 |
| `tests/Bukit.Cli.Tests/ImportCommandTests.cs` | 解析、验证、生成、安全性测试 |

### 需要修改的文件

| 文件 | 改动 |
|------|------|
| `src/Bukit.Cli/Cli/BukitCliSpecs.cs` | 添加 `import` 命令规范及 `html-demo` 子命令 |
| `src/Bukit.Cli/Program.cs` | 在子命令 switch 块中添加 `"import"` 分发 |

不删除任何现有代码。`bukit clone --fidelity` 继续原样工作。

---

## 步骤 1：创建 `ImportCommand.cs`

**文件：** `src/Bukit.Cli/Commands/ImportCommand.cs`

### 1.1：顶层分发

```csharp
namespace Bukit.Cli.Commands;

public static class ImportCommand
{
    public static Task<int> RunAsync(CliBoundCommand command)
    {
        var sub = command.GetArgument(0);
        return sub switch
        {
            "html-demo" => HtmlDemoAsync(command),
            _ => Task.FromResult(Unknown(sub))
        };
    }

    private static int Unknown(string sub)
    {
        Console.Error.WriteLine($"Unknown import subcommand: {sub}");
        Console.Error.WriteLine("Available: html-demo");
        return 2;
    }
}
```

### 1.2：`HtmlDemoAsync` 方法

签名：`private static async Task<int> HtmlDemoAsync(CliBoundCommand command)`

**参数提取：**
- `--theme` — 必填字符串，目标主题名
- `--force` — 标志位，是否覆盖已有主题
- `--use` — 标志位，创建后是否切换到该主题
- `--verify` — 标志位，生成后是否执行 doctor/build 验证
- `--config` / `--site` — 可选，用于解析根目录

**位置参数：** `command.GetArgument(1)` = demo 目录路径

**验证顺序（退出码 2）：**

1. 缺少 demo 目录参数 → 报错 `"缺少必填参数: <demo-dir>"`
2. demo 目录不存在 → 报错 `"demo 目录不存在: {path}"`
3. 缺少 `--theme` → 报错 `"缺少必填选项: --theme <名称>"`
4. 主题名不合法 → 使用 `CloneModels.IsSafeThemeName(themeName)` → 报错 `"无效的主题名: {name}"`
5. 主题已存在且未指定 `--force` → 报错 `"主题已存在: {name}。使用 --force 覆盖。"`

**根目录解析**（与 CloneCommand.RunAsync 相同的模式）：

```csharp
var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
var rootDir = resolved.RootDir;
var htmlDir = Path.GetFullPath(command.GetArgument(1));
```

### 1.3：生成（复用 CloneFidelity 基础设施）

调用现有 `CloneFidelityGenerator.Generate(rootDir, htmlDir, themeName)`。该方法处理：
- 递归扫描所有 `*.html` 文件
- 通过 `CloneFidelityHtmlParser` 解析为 `FidelityPage` 模型
- 通过 `CloneFidelityCommonBlocks.ExtractCommonBlocks` 提取公共块
- 写入 `layouts/layouts/base.html`
- 为每个页面写入 `layouts/pages/*.html`，外加 `index.html` 和 `list.html`
- 写入 `layouts/partials/header.html`、`nav.html`、`footer.html`
- 复制资源到 `themes/<name>/assets` 和 `themes/<name>/static`

### 1.4：生成 site.yaml

在 `rootDir` 下生成 `site.yaml`（除非已存在）：

```yaml
site:
  name: <themeName>
  title: <themeName>
  baseUrl: /
  language: en
  seo:
    renderMode: 'off'
  collections:
    page:
      permalink: '/{slug}/'
      template: 'pages/page.html'
      listRoute: '/'
content:
  provider: markdown
  contentDir: content
theme:
  name: <themeName>
```

**安全规则：** 如果 `site.yaml` 已存在，打印生成内容作为提示，但不覆盖。

### 1.5：模板同步

生成完成后，调用模板同步逻辑：
1. 定位 `themes/<themeName>/layouts/`
2. 递归扫描所有 `*.html` 文件
3. 分析每个模板的能力标记（needs_page_content、supports_pagination、supports_taxonomy、supports_search_snippets）
4. 写入 `layouts/bukit.templates.yaml`

实现方式：从 `TemplateCommand.SyncAsync` 中提取一个内部辅助方法 `SyncThemeAsync(rootDir, themeName, force)`，直接接受参数而非 `CliBoundCommand`。

### 1.6：将 assets 内容转移至 static

复刻 `CloneFidelityRunner` 中第 104-127 行的 `TransferAssetsToStatic` 逻辑：将 `themes/<name>/assets/` 下的文件移动到 `themes/<name>/static/`。

### 1.7：如果 `--use`，设置主题

```csharp
if (use)
{
    var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
    var useResult = await ThemeCommand.SetThemeAsync(themeName, resolved.FullConfigPath, resolved.RootDir,
        brand: null, primaryColor: null, accentColor: null);
    if (useResult != 0) return useResult;
}
```

### 1.8：如果 `--verify`，运行验证

```csharp
if (verify)
{
    var verifyResult = await CloneVerifier.VerifyCloneAsync(command, rootDir, failOnVisualDiff: false, visualThreshold: 0.03);
    if (verifyResult != 0) return verifyResult;
}
```

复用 `CloneVerifier.VerifyCloneAsync`，内含 doctor + build 检查。

### 1.9：迁移报告

打印结构化报告：

```
迁移完成: <themeName>
  HTML 页面扫描:   N
  模板生成:        N
  局部模板生成:    N
  资源复制:        N
  警告:            N
  site.yaml:       已创建 / 已跳过（已存在）
  bukit.templates.yaml: 已创建
  主题已设置:      是 / 否

后续步骤:
  bukit dev
  bukit build
  bukit doctor
```

### 1.10：安全要求

- **资源路径穿越拒绝：** 现有 `CloneFidelityGenerator.CopyAssets` 使用 `Path.GetFullPath(Path.Combine(htmlDir, asset.TrimStart('/')))`，已验证可拒绝 `../` 穿越。增加守卫：若解析后的资源路径不以 `htmlDir` 开头，跳过并发出警告。
- **敏感文件排除：** 复制资源/静态文件前，跳过匹配以下模式的文件：`.env`、`.git`、`.npmrc`、`.key`、`.pfx`、`.p12`、`.pem`、`.crt`、`.cert`。添加 `IsSensitiveFile(string path)` 辅助方法。
- **不写入项目根目录之外：** 所有主题输出均写入 `rootDir` 下的 `themes/<name>`，已由 `ConfigPathResolver` 保证。
- **跨平台路径规范化：** 统一使用 `Path.Combine`、`Path.GetFullPath`、`Path.GetRelativePath`。
- **LF 输出：** .NET Core 上 `File.WriteAllText` 已输出 LF。现有 `CloneFidelityGenerator` 使用 `StringBuilder.AppendLine()` 在 macOS 上天然产生 LF。
- **不静默覆盖主题：** 仅当指定 `--force` 时删除已有主题目录。

---

## 步骤 2：在 `BukitCliSpecs.cs` 注册命令规范

在 `clone` 规范之后、`theme` 规范之前添加：

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

在 `CreateRegistry()` 底部的注册数组中加入 `importCmd`。

---

## 步骤 3：在 `Program.cs` 中接线分发

在 `SubcommandParseResult` switch 块中添加（`webhook` 之后）：

```csharp
"import" => await ImportCommand.RunAsync(merged),
```

---

## 步骤 4：测试 — `tests/Bukit.Cli.Tests/ImportCommandTests.cs`

### 测试类：`ImportCommandTests : IDisposable`

使用临时目录模式（与 `CloneFidelityGeneratorTests` 相同）。

### 测试方法：

| 测试方法 | 验证内容 |
|----------|----------|
| `RunAsync_无子命令_返回2` | `import` 不带子命令，退出 2 |
| `HtmlDemo_缺少DemoDir_返回2` | 无位置参数 → 报错 |
| `HtmlDemo_DemoDir不存在_返回2` | 路径不存在 → 报错 |
| `HtmlDemo_缺少Theme_返回2` | 无 `--theme` → 报错 |
| `HtmlDemo_无效主题名_返回2` | `../evil`、`/root`、`com1` 被拒绝 |
| `HtmlDemo_主题已存在_无Force_返回2` | 主题目录已存在，未指定 `--force` → 报错 |
| `HtmlDemo_主题已存在_有Force_覆盖` | `--force` 删除并重建 |
| `HtmlDemo_单个Html文件_生成主题` | 验证主题目录结构已创建 |
| `HtmlDemo_多个Html文件_生成模板和局部模板` | 验证 2+ 页面产出正确数量 |
| `HtmlDemo_资源已复制` | 图片资源复制到 `themes/<name>/assets/` |
| `HtmlDemo_SiteYaml已创建` | 根目录下 `site.yaml` 已生成 |
| `HtmlDemo_SiteYaml已存在_跳过` | 已有 `site.yaml` 不被覆盖 |
| `HtmlDemo_带Use_设置主题` | `--use` 调用 `SetThemeAsync`，验证 `site.yaml` 中的 `theme.name` |
| `HtmlDemo_带Verify_运行验证` | `--verify` 触发验证（doctor 检查通过） |
| `HtmlDemo_路径穿越_拒绝` | 包含 `../etc/passwd` 的资源被跳过并发出警告 |
| `HtmlDemo_敏感文件_排除` | `.env`、`.git` 等从资源复制中跳过 |
| `HtmlDemo_迁移报告_已打印` | 控制台输出包含预期的报告段落 |

---

## 步骤 5：验证现有 `clone --fidelity` 仍然正常

运行已有测试：

```bash
dotnet test tests/Bukit.Cli.Tests --filter "FullyQualifiedName~CloneFidelity"
```

所有已有测试必须不变通过。

---

## 步骤 6：构建并运行全部测试

```bash
dotnet build src/Bukit.Cli/Bukit.Cli.csproj
dotnet test tests/Bukit.Cli.Tests/
```

---

## 文件汇总

| 操作 | 文件 |
|------|------|
| **新建** | `src/Bukit.Cli/Commands/ImportCommand.cs` |
| **新建** | `tests/Bukit.Cli.Tests/ImportCommandTests.cs` |
| **修改** | `src/Bukit.Cli/Cli/BukitCliSpecs.cs` — 添加 `import` 命令规范 |
| **修改** | `src/Bukit.Cli/Program.cs` — 添加 `"import"` 分发 |
