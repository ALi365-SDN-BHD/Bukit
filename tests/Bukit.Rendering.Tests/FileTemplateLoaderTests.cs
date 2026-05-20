using Bukit.Rendering.Scriban;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class FileTemplateLoaderTests : IDisposable
{
    private readonly string _rootDir;

    public FileTemplateLoaderTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void GetPath_RelativeName_ResolvesUnderRoot()
    {
        var loader = new FileTemplateLoader(_rootDir);
        var result = loader.GetPath(null!, default, "pages/page.html");
        var expected = Path.GetFullPath(Path.Combine(_rootDir, "pages", "page.html"));
        Assert.Equal(expected, result, ignoreCase: true);
    }

    [Fact]
    public void GetPath_RelativeWithDotDot_ThrowsForPathTraversal()
    {
        var loader = new FileTemplateLoader(_rootDir);
        Assert.Throws<InvalidOperationException>(() =>
            loader.GetPath(null!, default, "../../../etc/passwd"));
    }

    [Fact]
    public void GetPath_AbsolutePathOutsideRoot_ThrowsForPathTraversal()
    {
        var loader = new FileTemplateLoader(_rootDir);
        var outsidePath = Path.GetFullPath(Path.Combine(_rootDir, "..", "outside.html"));
        Assert.Throws<InvalidOperationException>(() =>
            loader.GetPath(null!, default, outsidePath));
    }

    [Fact]
    public void GetPath_AbsolutePathInsideRoot_Succeeds()
    {
        var loader = new FileTemplateLoader(_rootDir);
        var insidePath = Path.GetFullPath(Path.Combine(_rootDir, "layouts", "base.html"));
        var result = loader.GetPath(null!, default, insidePath);
        Assert.Equal(insidePath, result, ignoreCase: true);
    }

    [Fact]
    public void Load_NonexistentFile_ReturnsEmpty()
    {
        var loader = new FileTemplateLoader(_rootDir);
        var result = loader.Load(null!, default, Path.Combine(_rootDir, "missing.html"));
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Load_ExistingFile_ReturnsContent()
    {
        var filePath = Path.Combine(_rootDir, "test.html");
        File.WriteAllText(filePath, "<h1>Hello</h1>");

        var loader = new FileTemplateLoader(_rootDir);
        var result = loader.Load(null!, default, filePath);
        Assert.Equal("<h1>Hello</h1>", result);
    }

    [Fact]
    public void Load_OutsideRoot_ThrowsForPathTraversal()
    {
        var outsidePath = Path.GetFullPath(Path.Combine(_rootDir, "..", "outside.html"));
        File.WriteAllText(outsidePath, "outside");

        try
        {
            var loader = new FileTemplateLoader(_rootDir);
            Assert.Throws<InvalidOperationException>(() =>
                loader.Load(null!, default, outsidePath));
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public void Load_CachesResult_ReturnsSameForUnchangedFile()
    {
        var filePath = Path.Combine(_rootDir, "cached.html");
        File.WriteAllText(filePath, "<p>V1</p>");

        var loader = new FileTemplateLoader(_rootDir);
        var r1 = loader.Load(null!, default, filePath);
        var r2 = loader.Load(null!, default, filePath);

        Assert.Equal("<p>V1</p>", r1);
        Assert.Same(r1, r2);
    }

    [Fact]
    public async Task LoadAsync_ExistingFile_ReturnsContent()
    {
        var filePath = Path.Combine(_rootDir, "async.html");
        File.WriteAllText(filePath, "<div>async</div>");

        var loader = new FileTemplateLoader(_rootDir);
        var result = await loader.LoadAsync(null!, default, filePath);
        Assert.Equal("<div>async</div>", result);
    }

    [Fact]
    public async Task LoadAsync_OutsideRoot_ThrowsForPathTraversal()
    {
        var outsidePath = Path.GetFullPath(Path.Combine(_rootDir, "..", "outside-async.html"));
        await File.WriteAllTextAsync(outsidePath, "outside");

        try
        {
            var loader = new FileTemplateLoader(_rootDir);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await loader.LoadAsync(null!, default, outsidePath));
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public async Task LoadAsync_NonexistentFile_ReturnsEmpty()
    {
        var loader = new FileTemplateLoader(_rootDir);
        var result = await loader.LoadAsync(null!, default, Path.Combine(_rootDir, "no-such-async.html"));
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task LoadAsync_CachesResult_ReturnsSameForUnchangedFile()
    {
        var filePath = Path.Combine(_rootDir, "async-cached.html");
        File.WriteAllText(filePath, "<p>V1</p>");

        var loader = new FileTemplateLoader(_rootDir);
        var r1 = await loader.LoadAsync(null!, default, filePath);
        var r2 = await loader.LoadAsync(null!, default, filePath);

        Assert.Equal("<p>V1</p>", r1);
        Assert.Same(r1, r2);
    }

    [Fact]
    public void GetPath_ForwardSlashNormalization_Works()
    {
        var loader = new FileTemplateLoader(_rootDir);
        var result = loader.GetPath(null!, default, "pages/sub/page.html");
        Assert.Contains("pages", result);
        Assert.Contains("page.html", result);
    }
}
