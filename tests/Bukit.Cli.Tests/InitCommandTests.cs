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
