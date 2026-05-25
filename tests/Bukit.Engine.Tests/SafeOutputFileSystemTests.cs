using Bukit.Engine.Output;
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

        Assert.Throws<InvalidOperationException>(() => fs.GetSafeFullPath(relativePath));
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
