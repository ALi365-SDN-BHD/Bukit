using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class ImportCommandTests : IDisposable
{
    private readonly string _tempDir;

    public ImportCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-import-cmd-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static CliBoundCommand MakeCommand(Dictionary<string, string?> options, string[] args)
    {
        return new CliBoundCommand(options, args);
    }

    private Dictionary<string, string?> BaseOptions()
    {
        var configPath = Path.Combine(_tempDir, "site.yaml");
        return new Dictionary<string, string?>
        {
            ["--config"] = configPath
        };
    }

    private void CreateDemoHtml(string fileName, string title)
    {
        File.WriteAllText(Path.Combine(_tempDir, fileName),
            $"<html><head><title>{title}</title></head><body><header><nav>Nav</nav></header><main><h1>{title}</h1></main><footer>Footer</footer></body></html>");
    }

    [Fact]
    public async Task NoSubcommand_Returns2()
    {
        var cmd = MakeCommand(new Dictionary<string, string?>(), []);
        var result = await ImportCommand.RunAsync(cmd);
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task MissingDemoDir_Returns2()
    {
        var opts = BaseOptions();
        opts["--theme"] = "test";
        var cmd = MakeCommand(opts, ["html-demo"]);

        var result = await ImportCommand.RunAsync(cmd);
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task DemoDirNotFound_Returns2()
    {
        var opts = BaseOptions();
        opts["--theme"] = "test";
        var cmd = MakeCommand(opts, ["html-demo", Path.Combine(_tempDir, "nonexistent")]);

        var result = await ImportCommand.RunAsync(cmd);
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task MissingTheme_Returns2()
    {
        CreateDemoHtml("index.html", "Test");

        var opts = BaseOptions();
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);
        Assert.Equal(2, result);
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("/root")]
    [InlineData("")]
    public async Task InvalidThemeName_Returns2(string themeName)
    {
        CreateDemoHtml("index.html", "Test");

        var opts = BaseOptions();
        opts["--theme"] = themeName;
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task ExistingTheme_WithoutForce_Returns2()
    {
        CreateDemoHtml("index.html", "Test");
        var themeDir = Path.Combine(_tempDir, "themes", "existing");
        Directory.CreateDirectory(themeDir);

        var opts = BaseOptions();
        opts["--theme"] = "existing";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task ExistingTheme_WithForce_Overwrites()
    {
        CreateDemoHtml("index.html", "Test");
        var themeDir = Path.Combine(_tempDir, "themes", "force-test");
        Directory.CreateDirectory(themeDir);
        File.WriteAllText(Path.Combine(themeDir, "old.txt"), "old");

        var opts = BaseOptions();
        opts["--theme"] = "force-test";
        opts["--force"] = "true";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(0, result);
        Assert.False(File.Exists(Path.Combine(themeDir, "old.txt")));
    }

    [Fact]
    public async Task SingleHtmlFile_GeneratesCompleteStructure()
    {
        CreateDemoHtml("index.html", "Test Site");

        var opts = BaseOptions();
        opts["--theme"] = "single-test";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(0, result);
        var themeDir = Path.Combine(_tempDir, "themes", "single-test");
        Assert.True(File.Exists(Path.Combine(themeDir, "layouts", "layouts", "base.html")));
        Assert.True(File.Exists(Path.Combine(themeDir, "layouts", "pages", "index.html")));
        Assert.True(File.Exists(Path.Combine(themeDir, "layouts", "partials", "header.html")));
        Assert.True(File.Exists(Path.Combine(themeDir, "layouts", "bukit.templates.yaml")));
    }

    [Fact]
    public async Task MultipleHtmlFiles_CorrectlySplits()
    {
        CreateDemoHtml("index.html", "Home");
        CreateDemoHtml("about.html", "About");

        var opts = BaseOptions();
        opts["--theme"] = "multi-test";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(0, result);
        var pagesDir = Path.Combine(_tempDir, "themes", "multi-test", "layouts", "pages");
        Assert.True(File.Exists(Path.Combine(pagesDir, "index.html")));
        Assert.True(File.Exists(Path.Combine(pagesDir, "list.html")));
    }

    [Fact]
    public async Task AssetsCopied()
    {
        CreateDemoHtml("index.html", "Test");
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "logo.png"), "fake");

        var html = File.ReadAllText(Path.Combine(_tempDir, "index.html"));
        html = html.Replace("</main>", "<img src=\"img/logo.png\" /></main>");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"), html);

        var opts = BaseOptions();
        opts["--theme"] = "asset-test";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(0, result);
        Assert.True(
            File.Exists(Path.Combine(_tempDir, "themes", "asset-test", "assets", "img", "logo.png")) ||
            File.Exists(Path.Combine(_tempDir, "themes", "asset-test", "static", "img", "logo.png")));
    }

    [Fact]
    public async Task SiteYamlCreated()
    {
        CreateDemoHtml("index.html", "Test");

        var opts = BaseOptions();
        opts["--theme"] = "yaml-test";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(0, result);
        Assert.True(File.Exists(Path.Combine(_tempDir, "site.yaml")));
    }

    [Fact]
    public async Task PathTraversal_Rejected()
    {
        CreateDemoHtml("index.html", "Test");
        var html = File.ReadAllText(Path.Combine(_tempDir, "index.html"));
        html = html.Replace("</main>", "<img src=\"../etc/passwd\" /></main>");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"), html);

        var opts = BaseOptions();
        opts["--theme"] = "traversal-test";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(0, result);
        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "traversal-test", "assets", "..", "etc", "passwd")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "passwd")));
    }

    [Fact]
    public async Task SensitiveFiles_Excluded()
    {
        CreateDemoHtml("index.html", "Test");
        File.WriteAllText(Path.Combine(_tempDir, ".env"), "SECRET=xxx");

        var opts = BaseOptions();
        opts["--theme"] = "sensitive-test";
        var cmd = MakeCommand(opts, ["html-demo", _tempDir]);

        var result = await ImportCommand.RunAsync(cmd);

        Assert.Equal(1, result);
    }
}
