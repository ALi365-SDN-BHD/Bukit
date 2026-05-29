using Bukit.Cli.Commands.Dev;
using Xunit;

namespace Bukit.Cli.Tests.Dev;

public class DevPathGuardTests
{
    [Fact]
    public void TryResolveWithinRoot_NormalFilePath_ReturnsNonNull()
    {
        var root = Path.GetTempPath();
        var result = DevPathGuard.TryResolveWithinRoot(root, "index.html");

        Assert.NotNull(result);
        Assert.Contains("index.html", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryResolveWithinRoot_DotDotEscape_ReturnsNull()
    {
        var root = Path.GetTempPath();
        var result = DevPathGuard.TryResolveWithinRoot(root, "../etc/passwd");

        Assert.Null(result);
    }

    [Fact]
    public void TryResolveWithinRoot_NullOrEmptyRoot_ReturnsNull()
    {
        Assert.Null(DevPathGuard.TryResolveWithinRoot("", "index.html"));
        Assert.Null(DevPathGuard.TryResolveWithinRoot(null!, "index.html"));
    }

    [Fact]
    public void TryResolveWithinRoot_RootDirectoryItself_ReturnsRoot()
    {
        var root = Path.GetTempPath();
        var result = DevPathGuard.TryResolveWithinRoot(root, "/");

        Assert.NotNull(result);
        var expected = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryResolveWithinRoot_PathSeparatorInsensitive_WorksWithForwardSlash()
    {
        var root = Path.GetTempPath();
        var result = DevPathGuard.TryResolveWithinRoot(root, "/foo/bar");

        Assert.NotNull(result);
        var expected = Path.Combine(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), "foo", "bar");
        Assert.Equal(expected, result);
    }
}
