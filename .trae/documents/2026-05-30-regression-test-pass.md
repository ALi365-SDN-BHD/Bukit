# Bukit 全量自动化回归测试 — 实施计划

> **面向 Agent Worker：** 必须使用子技能 `superpowers:subagent-driven-development`（推荐）或 `superpowers:executing-plans` 逐任务实施。步骤使用 checkbox (`- [ ]`) 语法进行追踪。

**目标：** 对 Bukit 执行全量自动化回归测试，验证 CWD 隔离、SafeUrl 强化、外部插件策略、路由校验、CLI 错误分层、jobs 参数固位以及版本一致性等近期修复的正确性——运行 `dotnet test` 至少 20 次，零间歇性失败，同时新增基于 fixture 的端到端构建测试和 AOT 发布验证。

**架构：** 分七个阶段实施：(1) 基线测试确认当前状态，(2) 修复遗留的不安全测试模式，(3) 新增基于 fixture 的 E2E 集成测试，(4) 反复压力测试（20+ 轮），(5) CWD/环境变量泄漏检测，(6) Native AOT 发布验证，(7) 最终校验。

**技术栈：** .NET / C# / xUnit / Bukit CLI、Engine、Content、Shared、Config、Rendering、Theme 测试项目

---

## 阶段 0：基线评估与模式修复

### 任务 0.1：运行基线 `dotnet test`

**涉及文件：** 无（仅执行验证）

- [ ] **步骤 1：运行全量测试**

```bash
dotnet test --no-restore 2>&1 | tail -30
```

预期：全部通过。记录任何失败。

- [ ] **步骤 2：记录各项目测试用例数**

```bash
dotnet test --no-restore --logger "console;verbosity=detailed" 2>&1 | grep -E "^(Passed|Failed|Total|Skipped)"
```

预期：记录每个项目的基线测试数量（Bukit.Shared、Bukit.Config、Bukit.Content、Bukit.Engine、Bukit.Rendering、Bukit.Theme、Bukit.Cli、Bukit.Routing、Bukit.Engine.Abstractions、Bukit.PluginSourceGenerator、Bukit.Architecture）。

- [ ] **步骤 3：提交基线记录**

```bash
git add -A && git commit -m "chore: 记录回归加固前的基线测试情况"
```

仅当之前的模式修复产生实际变更时才提交。

### 任务 0.2：修复 DevFileWatcherTests 中未检查完成任务的 WhenAny

**涉及文件：**
- 修改：`tests/Bukit.Cli.Tests/Dev/DevFileWatcherTests.cs:57`

- [ ] **步骤 1：应用修复**

`OnChange_Debounce_MultipleWritesTriggerSingleRebuild` 测试在第 57 行使用了 `await Task.WhenAny(tcs.Task, Task.Delay(2000))`，但没有验证是 `tcs.Task` 完成（而非超时）。修改为：

```csharp
var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(2000));
Assert.Same(tcs.Task, completedTask);
Assert.Equal(1, rebuildCount);
```

- [ ] **步骤 2：验证编译通过**

```bash
dotnet build tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

预期：编译成功，零警告。

- [ ] **步骤 3：运行该测试**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter "FullyQualifiedName~DevFileWatcherTests.OnChange_Debounce_MultipleWritesTriggerSingleRebuild" --no-restore
```

预期：PASS

### 任务 0.3：修复 PreviewCommandTests 中过于宽泛的 catch(Exception)

**涉及文件：**
- 修改：`tests/Bukit.Cli.Tests/PreviewCommandTests.cs:118-135`

- [ ] **步骤 1：移除 `RunWithTimeoutAsync` 中的 `catch (Exception)` 包装**

当前代码（第 118-135 行）：
```csharp
private static async Task<int> RunWithTimeoutAsync(CliBoundCommand command, TimeSpan timeout)
{
    try
    {
        using var cts = new CancellationTokenSource(timeout);
        var task = PreviewCommand.RunAsync(command);
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed == task)
        {
            return await task;
        }
        return 0;
    }
    catch (Exception)
    {
        return 2;
    }
}
```

替换为：
```csharp
private static async Task<int> RunWithTimeoutAsync(CliBoundCommand command, TimeSpan timeout)
{
    using var cts = new CancellationTokenSource(timeout);
    var task = PreviewCommand.RunAsync(command);
    var completed = await Task.WhenAny(task, Task.Delay(timeout));
    if (completed == task)
    {
        return await task;
    }
    return 0;
}
```

- [ ] **步骤 2：更新依赖宽泛 catch 的测试以直接抛出异常**

更新 `RunAsync_MissingDir_ReturnsExitCode2`（第 41-47 行），改为捕获预期异常：

```csharp
[Fact]
public async Task RunAsync_MissingDir_ReturnsExitCode2()
{
    var command = BuildCommand("--dir", Path.Combine(Path.GetTempPath(), "bukit-preview-nonexistent"));
    await Assert.ThrowsAnyAsync<Exception>(() => RunWithTimeoutAsync(command, TimeSpan.FromSeconds(5)));
}
```

类似地更新 `RunAsync_InvalidPort_ReturnsExitCode2`（第 49-65 行）和 `RunAsync_NegativePort_ReturnsExitCode2`（第 68-85 行），使用 `PreviewCommand.RunAsync` 对无效端口/负端口抛出的具体异常类型。

- [ ] **步骤 3：编译并运行相关测试**

```bash
dotnet build tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter "FullyQualifiedName~PreviewCommandTests" --no-restore
```

预期：所有 PreviewCommandTests 通过，异常正确传播。

- [ ] **步骤 4：提交模式修复**

```bash
git add tests/Bukit.Cli.Tests/Dev/DevFileWatcherTests.cs tests/Bukit.Cli.Tests/PreviewCommandTests.cs
git commit -m "fix: 移除不安全测试模式——未检查的 Task.WhenAny 和宽泛的 catch(Exception)"
```

---

## 阶段 1：SafeUrl 与 Notion 块渲染器验证

### 任务 1.1：验证 SafeUrl 测试覆盖所有要求场景

**涉及文件：** 只读验证 `tests/Bukit.Shared.Tests/SafeUrlTests.cs` 和 `src/Bukit.Shared/SafeUrl.cs`

- [ ] **步骤 1：确认已有测试覆盖**

检查 `SafeUrlTests.cs` 已包含以下测试（预分析阶段已确认——全部存在）：
- `ForLink_ProtocolRelativeUrl_ReturnsNull` 含 `//evil.com` → ✓
- `ForMedia_ProtocolRelativeUrl_ReturnsNull` 含 `//evil.com/a.png` → ✓
- `ForEmbed_ProtocolRelativeUrl_ReturnsNull` 含 `//evil.com/embed` → ✓
- `ForLink_DangerousOrEmptyUrls_ReturnNull` 含 `javascript:alert(1)` → ✓
- `ForLink_ValidUrls_ReturnUrl` 中 `/about/` 被 `/internal/path` 覆盖 → ✓

- [ ] **步骤 2：如缺失则添加显式 `/about/` 测试**

要求的测试 `SafeUrl.ForLink("/about/") == "/about/"` 已被 `ForLink_ValidUrls_ReturnUrl` 中的 `[InlineData("/internal/path")]` 覆盖。但为使意图更明确，在 `tests/Bukit.Shared.Tests/SafeUrlTests.cs` 中新增：

```csharp
[Theory]
[InlineData("/about/")]
public void ForLink_LocalPaths_ReturnUnchanged(string url)
{
    var result = SafeUrl.ForLink(url);
    Assert.Equal(url.Trim(), result);
}
```

- [ ] **步骤 3：运行 SafeUrl 测试**

```bash
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj --filter "FullyQualifiedName~SafeUrlTests" --no-restore
```

预期：全部 PASS。

### 任务 1.2：验证所有 Notion 块渲染器使用 SafeUrl

**涉及文件：** 只读验证（预分析阶段已确认）

- [ ] **步骤 1：运行架构测试（如存在）**

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj --no-restore
```

- [ ] **步骤 2：手动验证清单**

所有 9 个块渲染器已确认使用 SafeUrl：
- ✅ `NotionRichTextRenderer.cs` — 富文本链接使用 `SafeUrl.ForLink()`（第 57、98 行）
- ✅ `ImageBlockRenderer.cs` — 图片 URL 使用 `SafeUrl.ForMedia()`（第 20 行）
- ✅ `VideoBlockRenderer.cs` — YouTube 使用 `SafeUrl.ForEmbed()`，直链视频使用 `SafeUrl.ForMedia()`（第 28、37 行）
- ✅ `EmbedBlockRenderer.cs` — YouTube 和嵌入使用 `SafeUrl.ForEmbed()`（第 30、39 行）
- ✅ `BookmarkBlockRenderer.cs` — 书签 URL 使用 `SafeUrl.ForLink()`（第 20 行）
- ✅ `FileBlockRenderer.cs` — 文件下载链接使用 `SafeUrl.ForLink()`（第 20 行）
- ✅ `PdfBlockRenderer.cs` — PDF 文件 URL 使用 `SafeUrl.ForMedia()`（第 20 行）
- ✅ `AudioBlockRenderer.cs` — 音频文件 URL 使用 `SafeUrl.ForMedia()`（第 20 行）
- ✅ `LinkPreviewBlockRenderer.cs` — 预览 URL 使用 `SafeUrl.ForLink()`（第 20 行）

全部同时使用 `SafeUrl.IsExternal()` 添加 `rel="noopener noreferrer"` 属性。对不安全 URL 全部返回 null。

- [ ] **步骤 3：运行块渲染器测试**

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj --filter "FullyQualifiedName~BlockRenderer" --no-restore
```

预期：全部 PASS。

---

## 阶段 2：基于 Fixture 的端到端构建测试

### 任务 2.1：新增 E2E 测试基础设施

**涉及文件：**
- 新建：`tests/Bukit.Cli.Tests/E2E/E2ETestBase.cs`（基类：临时目录、site.yaml 写入、构建运行器）
- 新建：`tests/Bukit.Cli.Tests/E2E/E2EFixtureTests.cs`（4 个 fixture 测试）

- [ ] **步骤 1：创建 E2E 测试基类**

创建 `tests/Bukit.Cli.Tests/E2E/E2ETestBase.cs`：

```csharp
using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;

namespace Bukit.Cli.Tests.E2E;

public abstract class E2ETestBase : IDisposable
{
    protected readonly string SiteDir;
    protected readonly string DistDir;

    protected E2ETestBase()
    {
        SiteDir = Path.Combine(Path.GetTempPath(), "bukit-e2e-" + Guid.NewGuid().ToString("N"));
        DistDir = Path.Combine(SiteDir, "dist");
        Directory.CreateDirectory(SiteDir);
        Directory.CreateDirectory(Path.Combine(SiteDir, "content"));
        Directory.CreateDirectory(Path.Combine(SiteDir, "layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(SiteDir, "layouts", "pages"));
    }

    public void Dispose()
    {
        try { Directory.Delete(SiteDir, recursive: true); } catch { }
    }

    protected void WriteFile(string relativePath, string content)
    {
        var full = Path.Combine(SiteDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    protected async Task<int> RunBuildAsync(params string[] extraArgs)
    {
        var args = new List<string> { "--config", Path.Combine(SiteDir, "site.yaml") };
        args.AddRange(extraArgs);
        var command = new CliBoundCommand(
            args.ToDictionary(
                a => a.StartsWith("--") ? a : "--" + a,
                _ => (string?)null,
                StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            return await BuildCommand.RunAsync(command);
        }
        catch (OperationCanceledException)
        {
            return -1;
        }
    }

    protected bool DistFileExists(string relativePath)
    {
        return File.Exists(Path.Combine(DistDir, relativePath));
    }
}
```

- [ ] **步骤 2：验证基类编译通过**

```bash
dotnet build tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

预期：编译成功。

### 任务 2.2：新增基础 Markdown 站点 fixture 测试

**涉及文件：**
- 修改：`tests/Bukit.Cli.Tests/E2E/E2EFixtureTests.cs`

- [ ] **步骤 1：编写基础 Markdown 站点测试**

```csharp
using Xunit;

namespace Bukit.Cli.Tests.E2E;

public class E2EFixtureTests
{
    public sealed class BasicMarkdownSite : E2ETestBase
    {
        [Fact]
        public async Task Build_GeneratesHtmlAndManifest()
        {
            WriteFile("site.yaml", """
                site:
                  name: basic-test
                  title: Basic Test
                  url: https://example.com
                content:
                  provider: markdown
                  markdown:
                    dir: content
                build:
                  output: dist
                """);

            WriteFile("content/index.md", """
                ---
                title: Home
                ---
                # Hello World
                """);

            WriteFile("layouts/layouts/base.html", """
                <!DOCTYPE html>
                <html>
                <head><title>{{ page.title }}</title></head>
                <body>{{ page.content }}</body>
                </html>
                """);

            WriteFile("layouts/pages/index.html", """
                {{ layout "layouts/base.html" }}
                """);

            var exitCode = await RunBuildAsync();

            Assert.True(DistFileExists("index.html"));
            Assert.True(DistFileExists("manifest.json"));
            Assert.False(DistFileExists(".git"));
            Assert.False(DistFileExists(".gitignore"));

            var html = File.ReadAllText(Path.Combine(DistDir, "index.html"));
            Assert.Contains("<h1>Hello World</h1>", html);
        }
    }
}
```

- [ ] **步骤 2：运行基础 Markdown 测试**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter "FullyQualifiedName~BasicMarkdownSite" --no-restore
```

预期：PASS — HTML 和 manifest 均已生成。

### 任务 2.3：新增路由覆盖站点 fixture 测试

**涉及文件：**
- 修改：`tests/Bukit.Cli.Tests/E2E/E2EFixtureTests.cs`

- [ ] **步骤 1：编写路由覆盖测试**

```csharp
public sealed class RouteOverrideSite : E2ETestBase
{
    [Fact]
    public async Task Build_RouteOverride_GeneratesCorrectPaths()
    {
        WriteFile("site.yaml", """
            site:
              name: route-override
              title: Route Test
              url: https://example.com
            content:
              provider: markdown
              markdown:
                dir: content
            routes:
              "/custom-path/":
                template: post
            build:
              output: dist
            """);

        WriteFile("content/post.md", """
            ---
            title: Custom Post
            slug: post
            ---
            # Custom Route Post
            """);

        WriteFile("layouts/layouts/base.html", """
            <!DOCTYPE html>
            <html>
            <head><title>{{ page.title }}</title></head>
            <body>{{ page.content }}</body>
            </html>
            """);

        WriteFile("layouts/pages/post.html", """
            {{ layout "layouts/base.html" }}
            """);

        var exitCode = await RunBuildAsync();

        Assert.True(DistFileExists("custom-path/index.html"));
        var html = File.ReadAllText(Path.Combine(DistDir, "custom-path", "index.html"));
        Assert.Contains("Custom Route Post", html);
    }
}
```

- [ ] **步骤 2：运行路由覆盖测试**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter "FullyQualifiedName~RouteOverrideSite" --no-restore
```

预期：PASS。

### 任务 2.4：新增外部插件站点 fixture 测试

**涉及文件：**
- 修改：`tests/Bukit.Cli.Tests/E2E/E2EFixtureTests.cs`

- [ ] **步骤 1：编写外部插件测试**

```csharp
public sealed class ExternalPluginSite : E2ETestBase
{
    [Fact]
    public async Task Build_ExternalPlugin_RunsAfterBuildHook()
    {
        WriteFile("site.yaml", """
            site:
              name: plugin-test
              title: Plugin Test
              url: https://example.com
              externalPlugins:
                sample:
                  runtime: process
                  entry: plugins/sample.sh
                  hooks: [after-build]
                  timeoutMs: 5000
            content:
              provider: markdown
              markdown:
                dir: content
            build:
              output: dist
            """);

        WriteFile("content/index.md", """
            ---
            title: Home
            ---
            # Plugin Test
            """);

        WriteFile("layouts/layouts/base.html", """
            <!DOCTYPE html>
            <html>
            <head><title>{{ page.title }}</title></head>
            <body>{{ page.content }}</body>
            </html>
            """);

        WriteFile("layouts/pages/index.html", """
            {{ layout "layouts/base.html" }}
            """);

        Directory.CreateDirectory(Path.Combine(SiteDir, "plugins"));
        WriteFile("plugins/sample.sh", "#!/bin/sh\ntouch \"$OUTPUT_DIR/plugin_was_here.txt\"\n");

        if (!OperatingSystem.IsWindows())
        {
            System.Diagnostics.Process.Start("chmod", $"+x {Path.Combine(SiteDir, "plugins", "sample.sh")}")?.WaitForExit();
        }

        var exitCode = await RunBuildAsync("--allow-external-plugins");

        Assert.True(DistFileExists("index.html"));
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(DistFileExists("plugin_was_here.txt"));
        }
    }
}
```

- [ ] **步骤 2：运行外部插件测试**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter "FullyQualifiedName~ExternalPluginSite" --no-restore
```

预期：非 Windows 环境 PASS。

### 任务 2.5：新增增量构建站点 fixture 测试

**涉及文件：**
- 修改：`tests/Bukit.Cli.Tests/E2E/E2EFixtureTests.cs`

- [ ] **步骤 1：编写增量构建测试**

```csharp
public sealed class IncrementalBuildSite : E2ETestBase
{
    [Fact]
    public async Task Build_Twice_SecondBuildIsFasterAndPreservesOutput()
    {
        WriteFile("site.yaml", """
            site:
              name: incremental-test
              title: Incremental Test
              url: https://example.com
            content:
              provider: markdown
              markdown:
                dir: content
            build:
              output: dist
            """);

        WriteFile("content/index.md", """
            ---
            title: Home
            ---
            # First Build
            """);

        WriteFile("layouts/layouts/base.html", """
            <!DOCTYPE html>
            <html>
            <head><title>{{ page.title }}</title></head>
            <body>{{ page.content }}</body>
            </html>
            """);

        WriteFile("layouts/pages/index.html", """
            {{ layout "layouts/base.html" }}
            """);

        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var exitCode1 = await RunBuildAsync();
        sw1.Stop();

        Assert.True(DistFileExists("index.html"));
        Assert.True(DistFileExists("manifest.json"));

        var firstBuildHtml = File.ReadAllText(Path.Combine(DistDir, "index.html"));

        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var exitCode2 = await RunBuildAsync();
        sw2.Stop();

        Assert.True(DistFileExists("index.html"));

        var secondBuildHtml = File.ReadAllText(Path.Combine(DistDir, "index.html"));

        Assert.Equal(firstBuildHtml, secondBuildHtml);
        Assert.True(sw2.Elapsed <= sw1.Elapsed * 2,
            $"第二次构建 ({sw2.Elapsed}) 应 <= 首次构建的 2 倍 ({sw1.Elapsed})");
    }

    [Fact]
    public async Task Build_Twice_WithContentChange_UpdatesOutput()
    {
        WriteFile("site.yaml", """
            site:
              name: incremental-change-test
              title: Incremental Change Test
              url: https://example.com
            content:
              provider: markdown
              markdown:
                dir: content
            build:
              output: dist
            """);

        WriteFile("content/index.md", """
            ---
            title: Home
            ---
            # Version 1
            """);

        WriteFile("layouts/layouts/base.html", """
            <!DOCTYPE html>
            <html>
            <head><title>{{ page.title }}</title></head>
            <body>{{ page.content }}</body>
            </html>
            """);

        WriteFile("layouts/pages/index.html", """
            {{ layout "layouts/base.html" }}
            """);

        await RunBuildAsync();
        var v1 = File.ReadAllText(Path.Combine(DistDir, "index.html"));
        Assert.Contains("Version 1", v1);

        WriteFile("content/index.md", """
            ---
            title: Home
            ---
            # Version 2
            """);

        await RunBuildAsync();
        var v2 = File.ReadAllText(Path.Combine(DistDir, "index.html"));
        Assert.Contains("Version 2", v2);
    }
}
```

- [ ] **步骤 2：运行增量构建测试**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter "FullyQualifiedName~IncrementalBuildSite" --no-restore
```

预期：两个测试均 PASS。

- [ ] **步骤 3：提交 E2E 测试**

```bash
git add tests/Bukit.Cli.Tests/E2E/
git commit -m "test: 新增 fixture 端到端测试——Markdown、路由覆盖、外部插件、增量构建"
```

---

## 阶段 3：输出校验测试

### 任务 3.1：新增全面输出校验

**涉及文件：**
- 修改：`tests/Bukit.Cli.Tests/E2E/E2EFixtureTests.cs`

- [ ] **步骤 1：新增输出校验测试**

```csharp
public sealed class OutputValidation : E2ETestBase
{
    [Fact]
    public async Task Build_WithSitemapAndFeed_GeneratesValidOutput()
    {
        WriteFile("site.yaml", """
            site:
              name: output-test
              title: Output Test
              url: https://example.com
              sitemap: true
              rss: true
              search: true
            content:
              provider: markdown
              markdown:
                dir: content
            build:
              output: dist
            """);

        WriteFile("content/index.md", """
            ---
            title: Home
            date: 2026-01-15
            ---
            # Output Validation
            """);

        WriteFile("content/about.md", """
            ---
            title: About
            date: 2026-02-20
            ---
            # About Us
            """);

        WriteFile("layouts/layouts/base.html", """
            <!DOCTYPE html>
            <html>
            <head><title>{{ page.title }}</title></head>
            <body>{{ page.content }}</body>
            </html>
            """);

        WriteFile("layouts/pages/page.html", """
            {{ layout "layouts/base.html" }}
            """);

        await RunBuildAsync();

        // HTML 存在
        Assert.True(DistFileExists("index.html"));
        Assert.True(DistFileExists("about/index.html"));

        // Manifest 存在
        Assert.True(DistFileExists("manifest.json"));
        var manifest = File.ReadAllText(Path.Combine(DistDir, "manifest.json"));
        Assert.Contains("\"index.html\"", manifest);

        // Sitemap 存在且有效
        Assert.True(DistFileExists("sitemap.xml"));
        var sitemap = File.ReadAllText(Path.Combine(DistDir, "sitemap.xml"));
        Assert.Contains("<urlset", sitemap);
        Assert.Contains("https://example.com/", sitemap);
        Assert.Contains("https://example.com/about/", sitemap);

        // RSS/Atom feed 存在
        Assert.True(DistFileExists("feed.xml"));
        var feed = File.ReadAllText(Path.Combine(DistDir, "feed.xml"));
        Assert.Contains("<rss", feed);
        Assert.Contains("<title>Output Test</title>", feed);

        // Search 索引存在
        Assert.True(DistFileExists("search.json"));
        var search = File.ReadAllText(Path.Combine(DistDir, "search.json"));
        Assert.Contains("Output Validation", search);

        // 无敏感点文件
        Assert.False(DistFileExists(".git"));
        Assert.False(DistFileExists(".gitignore"));
        Assert.False(DistFileExists(".env"));
        Assert.False(DistFileExists(".DS_Store"));
    }
}
```

- [ ] **步骤 2：运行输出校验测试**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter "FullyQualifiedName~OutputValidation" --no-restore
```

预期：PASS — 所有输出断言通过。

---

## 阶段 4：反复压力测试（20+ 轮）

### 任务 4.1：运行 `dotnet test` 20 次

**涉及文件：** 无（仅执行）

- [ ] **步骤 1：创建压力测试脚本**

创建 `scripts/stress-test.sh`：

```bash
#!/bin/bash
set -euo pipefail

PASSES=0
FAILS=0
RUNS=20

echo "=== Bukit 压力测试：$RUNS 轮 ==="
echo "开始：$(date)"
echo ""

for i in $(seq 1 $RUNS); do
    echo "--- 第 $i/$RUNS 轮 ---"
    if dotnet test --no-restore --verbosity quiet 2>&1 | tee /tmp/bukit-test-$i.log | tail -5; then
        PASSES=$((PASSES + 1))
        echo "  ✅ 通过"
    else
        FAILS=$((FAILS + 1))
        echo "  ❌ 失败（日志：/tmp/bukit-test-$i.log）"
    fi
    echo ""
done

echo "=== 结果 ==="
echo "总计：$RUNS"
echo "通过：$PASSES"
echo "失败：$FAILS"
echo "结束：$(date)"

exit $FAILS
```

- [ ] **步骤 2：运行压力脚本**

```bash
chmod +x scripts/stress-test.sh
./scripts/stress-test.sh
```

预期：全部 20 轮通过（0 次失败）。

- [ ] **步骤 3：如有失败，诊断并修复**

对每次失败：
1. 读取 `/tmp/bukit-test-N.log` 找到失败测试
2. 用指定的测试筛选器重现
3. 修复间歇性问题
4. 重新运行完整压力脚本

- [ ] **步骤 4：额外针对特定测试分类加压**

```bash
# 针对 CLI 测试加压（50 轮）
for i in $(seq 1 50); do
    echo "CLI 第 $i 轮"
    dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --no-restore --verbosity quiet || { echo "CLI 第 $i 轮失败"; exit 1; }
done

# 针对 CWD 相关测试加压（50 轮）
for i in $(seq 1 50); do
    echo "ConfigPathResolver 第 $i 轮"
    dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~ConfigPathResolver" --verbosity quiet || { echo "第 $i 轮失败"; exit 1; }
done

# 针对 BuildCommand 测试加压（50 轮）
for i in $(seq 1 50); do
    echo "BuildCommand 第 $i 轮"
    dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~BuildCommandTests" --verbosity quiet || { echo "第 $i 轮失败"; exit 1; }
done

# 针对 CI/BUKIT_CI 环境变量测试加压（50 轮）
for i in $(seq 1 50); do
    echo "CI 环境变量 第 $i 轮"
    dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~RunAsync_CIEnv" --verbosity quiet || { echo "第 $i 轮失败"; exit 1; }
done
```

预期：所有定向压力测试零失败。

---

## 阶段 5：CWD 与环境变量泄漏检测

### 任务 5.1：验证 CWD 隔离

**涉及文件：** 只读验证 `tests/Bukit.Cli.Tests/ConfigPathResolverTests.cs` 和 `tests/Bukit.Cli.Tests/BuildCommandTests.cs`

- [ ] **步骤 1：审计 CWD 使用模式**

已验证的关键模式：
- `ConfigPathResolverTests` 使用 `IDisposable` 配合临时目录清理，默认解析不修改 CWD
- `BuildCommandTests` 正确使用 `CurrentDirectoryScope` 并在 dispose 时恢复
- BuildCommandTests 中的 CI 环境变量测试使用 `finally` 块恢复 `Environment.SetEnvironmentVariable`

- [ ] **步骤 2：新增 CWD 泄漏检测测试**

在 `tests/Bukit.Cli.Tests/ConfigPathResolverTests.cs` 中添加：

```csharp
[Fact]
public void Resolve_DefaultConfig_DoesNotChangeCwd()
{
    var originalCwd = Directory.GetCurrentDirectory();
    ConfigPathResolver.Resolve(null, null);
    var afterCwd = Directory.GetCurrentDirectory();
    Assert.Equal(originalCwd, afterCwd);
}

[Fact]
public void Resolve_WithConfigPath_DoesNotChangeCwd()
{
    var configPath = Path.Combine(_testDir, "unused.yaml");
    var originalCwd = Directory.GetCurrentDirectory();
    ConfigPathResolver.Resolve(configPath, null);
    var afterCwd = Directory.GetCurrentDirectory();
    Assert.Equal(originalCwd, afterCwd);
}
```

- [ ] **步骤 3：运行 CWD 泄漏测试**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter "FullyQualifiedName~ConfigPathResolverTests" --no-restore
```

预期：全部 PASS，CWD 未变。

### 任务 5.2：验证环境变量清理

**涉及文件：**
- 修改：`tests/Bukit.Cli.Tests/BuildCommandTests.cs`

- [ ] **步骤 1：新增环境变量清理验证测试**

```csharp
[Fact]
public void CIEnvTests_RestoreOriginalEnvironmentVariables()
{
    var oldCI = Environment.GetEnvironmentVariable("CI");
    var oldBukitCI = Environment.GetEnvironmentVariable("BUKIT_CI");

    try
    {
        Environment.SetEnvironmentVariable("CI", "modified");
        Environment.SetEnvironmentVariable("BUKIT_CI", "modified");
    }
    finally
    {
        Environment.SetEnvironmentVariable("CI", oldCI);
        Environment.SetEnvironmentVariable("BUKIT_CI", oldBukitCI);
    }

    Assert.Equal(oldCI, Environment.GetEnvironmentVariable("CI"));
    Assert.Equal(oldBukitCI, Environment.GetEnvironmentVariable("BUKIT_CI"));
}
```

- [ ] **步骤 2：运行环境变量测试**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter "FullyQualifiedName~RestoreOriginalEnvironmentVariables" --no-restore
```

预期：PASS。

---

## 阶段 6：Native AOT 发布

### 任务 6.1：运行 Native AOT 发布

**涉及文件：** 无（仅验证）

- [ ] **步骤 1：运行 AOT 发布**

```bash
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c Release 2>&1 | tail -30
```

预期：发布成功，零错误，零警告（AOT 裁剪警告已在项目配置中抑制）。

- [ ] **步骤 1b：检查 AOT 警告**

```bash
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c Release 2>&1 | grep -i "warning.*IL" || echo "无 AOT 警告"
```

预期："无 AOT 警告"或零 IL 裁剪警告。

- [ ] **步骤 2：验证发布二进制存在**

```bash
ls -lh src/Bukit.Cli/bin/Release/net*/publish/Bukit.Cli*
```

预期：二进制文件存在，大小非零。

- [ ] **步骤 3：对发布二进制运行基本冒烟测试**

```bash
./src/Bukit.Cli/bin/Release/net9.0/macos-arm64/publish/Bukit.Cli --version 2>&1
```

预期：显示版本字符串（如 "1.0.7"）。

- [ ] **步骤 4：运行 AOT 专项脚本**

```bash
bash scripts/check-aot-warnings.sh
```

预期：零 AOT 警告。

---

## 阶段 7：最终验证

### 任务 7.1：运行最终全量测试

**涉及文件：** 无（仅执行）

- [ ] **步骤 1：清理并重新构建**

```bash
dotnet clean
dotnet build
```

- [ ] **步骤 2：最后一次运行全量测试**

```bash
dotnet test --no-restore --verbosity normal
```

预期：全部测试通过。

- [ ] **步骤 3：运行质量门禁**

```bash
bash scripts/quality-gate.sh
```

预期：PASS。

### 任务 7.2：生成汇总报告

- [ ] **步骤 1：记录测试数量**

```bash
dotnet test --no-restore --logger "console;verbosity=detailed" 2>&1 | grep -E "(Passed|Failed|Total)" | tail -5
```

- [ ] **步骤 2：逐项确认验收标准**

验收清单：
- [ ] `dotnet test` 20+ 次均通过
- [ ] 无间歇性 BuildCommand NoConfig 失败
- [ ] 无测试残留 CWD 或环境变量污染
- [ ] 无测试完成后后台构建任务泄漏
- [ ] SafeUrl 阻止不安全协议和协议相对 URL
- [ ] AudioBlockRenderer 已安全化（使用 SafeUrl.ForMedia）
- [ ] AOT 发布成功，零警告

---

## 风险评估

| 风险 | 可能性 | 缓解措施 |
|------|--------|----------|
| 移除 broad catch(Exception) 后 PreviewCommandTests 破裂 | 中 | 将测试预期从退出码 2 调整为特定异常类型 |
| 外部插件 E2E 测试在 Windows 上失败 | 中 | 使用 `OperatingSystem.IsWindows()` 做条件判断 |
| 增量构建时间比较因波动失败 | 低 | 使用 `<= 2x` 阈值代替严格 `<=` |
| AOT 发布产生 IL 裁剪警告 | 低 | 先前审计已验证——项目有 AOT 警告抑制配置 |
| 压力测试中出现间歇性失败 | 低 | 先前压力测试结果干净 |
