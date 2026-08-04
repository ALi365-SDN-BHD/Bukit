using Bukit.Rendering.Scriban;
using Xunit;
using Xunit.Sdk;

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
    public void GetPath_PrioritizesOverride_WhenChildAndParentExist()
    {
        var parentDir = Path.Combine(_rootDir, "parent");
        var overrideDir = Path.Combine(_rootDir, "override");
        var childTemplate = Path.Combine(_rootDir, "pages", "home.html");
        var parentTemplate = Path.Combine(parentDir, "pages", "home.html");
        var overrideTemplate = Path.Combine(overrideDir, "pages", "home.html");

        Directory.CreateDirectory(Path.GetDirectoryName(childTemplate)!);
        Directory.CreateDirectory(Path.GetDirectoryName(parentTemplate)!);
        Directory.CreateDirectory(Path.GetDirectoryName(overrideTemplate)!);

        File.WriteAllText(childTemplate, "child");
        File.WriteAllText(parentTemplate, "parent");
        File.WriteAllText(overrideTemplate, "override");

        var loader = new FileTemplateLoader(_rootDir, parentDir, overrideDir);
        var result = loader.GetPath(null!, default, "pages/home.html");

        Assert.Equal(Path.GetFullPath(overrideTemplate), result, ignoreCase: true);
    }

    [Fact]
    public void GetPath_FallsBackToChildWhenOverrideMissing()
    {
        var parentDir = Path.Combine(_rootDir, "parent");
        var overrideDir = Path.Combine(_rootDir, "override");
        var childTemplate = Path.Combine(_rootDir, "pages", "home.html");
        var parentTemplate = Path.Combine(parentDir, "pages", "home.html");

        Directory.CreateDirectory(Path.GetDirectoryName(childTemplate)!);
        Directory.CreateDirectory(Path.GetDirectoryName(parentTemplate)!);
        Directory.CreateDirectory(overrideDir);

        File.WriteAllText(childTemplate, "child");
        File.WriteAllText(parentTemplate, "parent");

        var loader = new FileTemplateLoader(_rootDir, parentDir, overrideDir);
        var result = loader.GetPath(null!, default, "pages/home.html");

        Assert.Equal(Path.GetFullPath(childTemplate), result, ignoreCase: true);
    }

    [Fact]
    public void GetPath_FallsBackToParentWhenChildMissing()
    {
        var parentDir = Path.Combine(_rootDir, "parent");
        var overrideDir = Path.Combine(_rootDir, "override");
        var parentTemplate = Path.Combine(parentDir, "pages", "home.html");

        Directory.CreateDirectory(Path.GetDirectoryName(parentTemplate)!);
        Directory.CreateDirectory(overrideDir);
        File.WriteAllText(parentTemplate, "parent");

        var loader = new FileTemplateLoader(_rootDir, parentDir, overrideDir);
        var result = loader.GetPath(null!, default, "pages/home.html");

        var expected = Path.GetFullPath(parentTemplate);
        Assert.Equal(expected, result, ignoreCase: true);
    }

    [Fact]
    public void GetPath_WhenAllLayersAreMissing_ReturnsPrimaryPath()
    {
        var parentDir = Path.Combine(_rootDir, "parent");
        var overrideDir = Path.Combine(_rootDir, "override");
        Directory.CreateDirectory(parentDir);
        Directory.CreateDirectory(overrideDir);

        var loader = new FileTemplateLoader(_rootDir, parentDir, overrideDir);
        var result = loader.GetPath(null!, default, "pages/missing.html");

        var expected = Path.GetFullPath(
            Path.Combine(_rootDir, "pages", "missing.html"));
        Assert.Equal(expected, result, ignoreCase: true);
    }

    [Fact]
    public void GetPath_ForwardSlashNormalization_Works()
    {
        var loader = new FileTemplateLoader(_rootDir);
        var result = loader.GetPath(null!, default, "pages/sub/page.html");
        Assert.Contains("pages", result);
        Assert.Contains("page.html", result);
    }

    [Fact]
    public void Load_LayoutSymlinkOutsideRoot_Throws()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-layout-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        var outsideFile = Path.Combine(outsideDir, "secret.html");
        File.WriteAllText(outsideFile, "secret");
        var linkPath = Path.Combine(_rootDir, "layout.html");
        try
        {
            File.CreateSymbolicLink(linkPath, outsideFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"File symlinks are unavailable: {ex.GetType().Name}");
        }

        try
        {
            var loader = new FileTemplateLoader(_rootDir);
            Assert.Throws<IOException>(() => loader.Load(null!, default, linkPath));
        }
        finally
        {
            File.Delete(outsideFile);
            Directory.Delete(outsideDir);
        }
    }

    [Fact]
    public async Task LoadAsync_LayoutSymlinkOutsideRoot_Throws()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-layout-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        var outsideFile = Path.Combine(outsideDir, "secret-async.html");
        await File.WriteAllTextAsync(outsideFile, "secret");
        var linkPath = Path.Combine(_rootDir, "layout-async.html");
        try
        {
            File.CreateSymbolicLink(linkPath, outsideFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"File symlinks are unavailable: {ex.GetType().Name}");
        }

        try
        {
            var loader = new FileTemplateLoader(_rootDir);
            await Assert.ThrowsAsync<IOException>(async () =>
                await loader.LoadAsync(null!, default, linkPath));
        }
        finally
        {
            File.Delete(outsideFile);
            Directory.Delete(outsideDir);
        }
    }
}
