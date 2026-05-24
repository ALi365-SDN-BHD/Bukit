using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class InitCommandTests : IDisposable
{
    private readonly string _tempDir;

    public InitCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-init-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task RunAsync_CreatesSiteYamlInOutputDir()
    {
        var target = Path.Combine(_tempDir, "my-site");
        var reader = new ArgReader(new[] { "init", target });

        var code = await InitCommand.RunAsync(reader);

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(target, "site.yaml")));
        var yaml = File.ReadAllText(Path.Combine(target, "site.yaml"));
        Assert.Contains("site:", yaml);
        Assert.Contains("name: my-site", yaml);
        Assert.Contains("title: My Site", yaml);
    }

    [Fact]
    public async Task RunAsync_CreatesContentDirWithSampleMd()
    {
        var target = Path.Combine(_tempDir, "my-site");
        var reader = new ArgReader(new[] { "init", target });

        await InitCommand.RunAsync(reader);

        var contentDir = Path.Combine(target, "content");
        Assert.True(Directory.Exists(contentDir));
        Assert.True(File.Exists(Path.Combine(contentDir, "hello-world.md")));
        var md = File.ReadAllText(Path.Combine(contentDir, "hello-world.md"));
        Assert.Contains("Hello World", md);
    }

    [Fact]
    public async Task RunAsync_CreatesLayoutsDirWithBaseTemplate()
    {
        var target = Path.Combine(_tempDir, "my-site");
        var reader = new ArgReader(new[] { "init", target });

        await InitCommand.RunAsync(reader);

        var baseLayout = Path.Combine(target, "themes", "starter", "layouts", "layouts", "base.html");
        Assert.True(File.Exists(baseLayout));
        var content = File.ReadAllText(baseLayout);
        Assert.Contains("<!DOCTYPE html>", content);
    }

    [Fact]
    public async Task RunAsync_CreatesAssetsDir()
    {
        var target = Path.Combine(_tempDir, "my-site");
        var reader = new ArgReader(new[] { "init", target });

        await InitCommand.RunAsync(reader);

        var assetsDir = Path.Combine(target, "themes", "starter", "assets");
        Assert.True(Directory.Exists(assetsDir));
        Assert.True(File.Exists(Path.Combine(assetsDir, "style.css")));
    }

    [Fact]
    public async Task RunAsync_CreatesStaticDir()
    {
        var target = Path.Combine(_tempDir, "my-site");
        var reader = new ArgReader(new[] { "init", target });

        await InitCommand.RunAsync(reader);

        Assert.True(Directory.Exists(Path.Combine(target, "themes", "starter", "static")));
    }

    [Fact]
    public async Task RunAsync_CreatesGitignore()
    {
        var target = Path.Combine(_tempDir, "my-site");
        var reader = new ArgReader(new[] { "init", target });

        await InitCommand.RunAsync(reader);

        Assert.True(File.Exists(Path.Combine(target, ".gitignore")));
        var content = File.ReadAllText(Path.Combine(target, ".gitignore"));
        Assert.Contains("dist/", content);
        Assert.Contains(".cache/", content);
    }

    [Fact]
    public async Task RunAsync_CreatesReadme()
    {
        var target = Path.Combine(_tempDir, "my-site");
        var reader = new ArgReader(new[] { "init", target });

        await InitCommand.RunAsync(reader);

        Assert.True(File.Exists(Path.Combine(target, "README.md")));
        var content = File.ReadAllText(Path.Combine(target, "README.md"));
        Assert.Contains("Powered by bukit", content);
    }

    [Fact]
    public async Task RunAsync_MissingTargetDir_ReturnsError()
    {
        var reader = new ArgReader(new[] { "init" });

        var code = await InitCommand.RunAsync(reader);

        Assert.Equal(2, code);
    }

    [Fact]
    public async Task RunAsync_ProviderMarkdownOption()
    {
        var target = Path.Combine(_tempDir, "my-site");
        var reader = new ArgReader(new[] { "init", target, "--provider", "markdown" });

        await InitCommand.RunAsync(reader);

        var yaml = File.ReadAllText(Path.Combine(target, "site.yaml"));
        Assert.Contains("provider: markdown", yaml);
    }

    [Fact]
    public async Task RunAsync_ProviderNotionOption()
    {
        var target = Path.Combine(_tempDir, "my-site");
        var reader = new ArgReader(new[] { "init", target, "--provider", "notion" });

        await InitCommand.RunAsync(reader);

        var yaml = File.ReadAllText(Path.Combine(target, "site.yaml"));
        Assert.Contains("provider: notion", yaml);
        Assert.Contains("databaseId: xxxxx", yaml);
    }

    [Fact]
    public async Task RunAsync_TemplatePreset_GeneratesPresetTheme()
    {
        var target = Path.Combine(_tempDir, "my-site");
        var reader = new ArgReader(new[] { "init", target, "--template", "blog" });

        var code = await InitCommand.RunAsync(reader);

        Assert.Equal(0, code);
        var themeRoot = Path.Combine(target, "themes", "starter");
        var css = await File.ReadAllTextAsync(Path.Combine(themeRoot, "assets", "style.css"));
        var themeYaml = await File.ReadAllTextAsync(Path.Combine(themeRoot, "theme.yaml"));
        var readme = await File.ReadAllTextAsync(Path.Combine(target, "README.md"));
        Assert.Contains("--primary: #2563eb;", css, StringComparison.Ordinal);
        Assert.Contains("dark-mode", themeYaml, StringComparison.Ordinal);
        Assert.Contains("Template: blog", readme, StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path.Combine(themeRoot, "static")));
    }

    [Fact]
    public async Task RunAsync_BlogTemplate_GeneratesBlogContentSkeleton()
    {
        var target = Path.Combine(_tempDir, "my-blog");
        var reader = new ArgReader(new[] { "init", target, "--template", "blog" });

        var code = await InitCommand.RunAsync(reader);

        Assert.Equal(0, code);
        var yaml = await File.ReadAllTextAsync(Path.Combine(target, "site.yaml"));
        var post = await File.ReadAllTextAsync(Path.Combine(target, "content", "posts", "welcome.md"));
        Assert.Contains("defaultType: post", yaml, StringComparison.Ordinal);
        Assert.Contains("permalink: /blog/{year}/{month}/{slug}/", yaml, StringComparison.Ordinal);
        Assert.Contains("pagination:", yaml, StringComparison.Ordinal);
        Assert.Contains("type: post", post, StringComparison.Ordinal);
        Assert.Contains("categories: [news]", post, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_DocsTemplate_GeneratesDocsContentSkeleton()
    {
        var target = Path.Combine(_tempDir, "my-docs");
        var reader = new ArgReader(new[] { "init", target, "--template", "docs" });

        var code = await InitCommand.RunAsync(reader);

        Assert.Equal(0, code);
        var yaml = await File.ReadAllTextAsync(Path.Combine(target, "site.yaml"));
        var doc = await File.ReadAllTextAsync(Path.Combine(target, "content", "docs", "getting-started.md"));
        Assert.Contains("doc:", yaml, StringComparison.Ordinal);
        Assert.Contains("permalink: /docs/{slug}/", yaml, StringComparison.Ordinal);
        Assert.Contains("defaultType: doc", yaml, StringComparison.Ordinal);
        Assert.Contains("type: doc", doc, StringComparison.Ordinal);
        Assert.Contains("Getting Started", doc, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_LandingTemplate_GeneratesLandingContentSkeleton()
    {
        var target = Path.Combine(_tempDir, "my-landing");
        var reader = new ArgReader(new[] { "init", target, "--template", "landing" });

        var code = await InitCommand.RunAsync(reader);

        Assert.Equal(0, code);
        var yaml = await File.ReadAllTextAsync(Path.Combine(target, "site.yaml"));
        var overview = await File.ReadAllTextAsync(Path.Combine(target, "content", "pages", "overview.md"));
        Assert.Contains("permalink: /{slug}/", yaml, StringComparison.Ordinal);
        Assert.Contains("defaultType: page", yaml, StringComparison.Ordinal);
        Assert.Contains("type: page", overview, StringComparison.Ordinal);
        Assert.Contains("Product Overview", overview, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_PortfolioTemplate_GeneratesPortfolioContentSkeleton()
    {
        var target = Path.Combine(_tempDir, "my-portfolio");
        var reader = new ArgReader(new[] { "init", target, "--template", "portfolio" });

        var code = await InitCommand.RunAsync(reader);

        Assert.Equal(0, code);
        var yaml = await File.ReadAllTextAsync(Path.Combine(target, "site.yaml"));
        var work = await File.ReadAllTextAsync(Path.Combine(target, "content", "work", "sample-project.md"));
        Assert.Contains("work:", yaml, StringComparison.Ordinal);
        Assert.Contains("permalink: /work/{slug}/", yaml, StringComparison.Ordinal);
        Assert.Contains("defaultType: work", yaml, StringComparison.Ordinal);
        Assert.Contains("type: work", work, StringComparison.Ordinal);
        Assert.Contains("Sample Project", work, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_TemplatePreset_GeneratesTemplatesThatNormalizeRootBaseUrl()
    {
        var target = Path.Combine(_tempDir, "my-site");
        var reader = new ArgReader(new[] { "init", target, "--template", "blog" });

        var code = await InitCommand.RunAsync(reader);

        Assert.Equal(0, code);
        var layoutsRoot = Path.Combine(target, "themes", "starter", "layouts");
        var baseLayout = await File.ReadAllTextAsync(Path.Combine(layoutsRoot, "layouts", "base.html"));
        var header = await File.ReadAllTextAsync(Path.Combine(layoutsRoot, "partials", "header.html"));
        var listCard = await File.ReadAllTextAsync(Path.Combine(layoutsRoot, "partials", "list-card.html"));
        Assert.Contains("base_url = site.base_url", baseLayout, StringComparison.Ordinal);
        Assert.Contains("if base_url == \"/\"", baseLayout, StringComparison.Ordinal);
        Assert.Contains("href=\"{{ base_url }}/assets/style.css\"", baseLayout, StringComparison.Ordinal);
        Assert.Contains("href=\"{{ base_url }}/blog/\"", header, StringComparison.Ordinal);
        Assert.Contains("href=\"{{ base_url }}{{ item.url }}\"", listCard, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_UnknownTemplate_ReturnsError()
    {
        var target = Path.Combine(_tempDir, "my-site");
        var reader = new ArgReader(new[] { "init", target, "--template", "unknown" });

        var code = await InitCommand.RunAsync(reader);

        Assert.Equal(2, code);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public async Task RunAsync_RelativePath_ResolvedToFullPath()
    {
        var dirName = "my-site";
        var originalDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = _tempDir;
            var reader = new ArgReader(new[] { "init", dirName });

            await InitCommand.RunAsync(reader);

            Assert.True(File.Exists(Path.Combine(_tempDir, dirName, "site.yaml")));
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
        }
    }

    [Fact]
    public async Task RunAsync_ExistingDir_OverwritesFiles()
    {
        var target = Path.Combine(_tempDir, "my-site");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "existing.txt"), "leave me");

        var reader = new ArgReader(new[] { "init", target });

        await InitCommand.RunAsync(reader);

        Assert.True(File.Exists(Path.Combine(target, "site.yaml")));
        Assert.True(File.Exists(Path.Combine(target, "existing.txt")));
    }
}
