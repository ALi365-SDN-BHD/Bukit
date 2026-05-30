using Bukit.Engine.Output;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SafeOutputFileSystemTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-safe-output-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("/tmp/evil.txt")]
    [InlineData("C:/evil.txt")]
    [InlineData("CON")]
    public void GetSafeFullPath_RejectsUnsafeRelativePath(string relativePath)
    {
        var fs = new SafeOutputFileSystem(_root);

        Assert.Throws<ConfigException>(() => fs.GetSafeFullPath(relativePath));
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("/tmp/evil.txt")]
    public async Task DeleteFileAsync_RejectsUnsafeRelativePath(string relativePath)
    {
        var fs = new SafeOutputFileSystem(_root);

        await Assert.ThrowsAsync<ConfigException>(() => fs.DeleteFileAsync(relativePath, CancellationToken.None));
    }

    [Fact]
    public async Task CopyFileAsync_CopiesInsideOutputRoot()
    {
        var source = Path.Combine(_root, "source.txt");
        Directory.CreateDirectory(_root);
        File.WriteAllText(source, "copied");
        var fs = new SafeOutputFileSystem(Path.Combine(_root, "dist"));

        await fs.CopyFileAsync(source, "assets/source.txt", CancellationToken.None);

        Assert.Equal("copied", File.ReadAllText(Path.Combine(_root, "dist", "assets", "source.txt")));
    }

    [Fact]
    public async Task DeleteFileAsync_DeletesInsideOutputRoot()
    {
        var target = Path.Combine(_root, "dist", "old.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "old");
        var fs = new SafeOutputFileSystem(Path.Combine(_root, "dist"));

        await fs.DeleteFileAsync("old.txt", CancellationToken.None);

        Assert.False(File.Exists(target));
    }

    [Fact]
    public async Task WriteTextAsync_WritesInsideOutputRoot()
    {
        var fs = new SafeOutputFileSystem(_root);

        await fs.WriteTextAsync("nested/file.txt", "ok", CancellationToken.None);

        Assert.Equal("ok", File.ReadAllText(Path.Combine(_root, "nested", "file.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
