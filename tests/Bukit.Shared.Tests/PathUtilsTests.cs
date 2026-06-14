using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class PathUtilsTests : IDisposable
{
    private readonly string _rootDir;

    public PathUtilsTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-path-utils-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public void IsSubPathOf_NestedPath_ReturnsTrue()
    {
        var parent = Path.Combine(_rootDir, "parent");
        var child = Path.Combine(parent, "child", "page.md");
        Directory.CreateDirectory(Path.GetDirectoryName(child)!);
        File.WriteAllText(child, "content");

        Assert.True(PathUtils.IsSubPathOf(child, parent));
    }

    [Fact]
    public void IsSubPathOf_SamePath_ReturnsFalse()
    {
        var parent = Path.Combine(_rootDir, "same");
        Directory.CreateDirectory(parent);

        Assert.False(PathUtils.IsSubPathOf(parent, parent));
    }

    [Fact]
    public void IsSameOrSubPathOf_SamePathAndNestedMissingTail_ReturnExpectedResults()
    {
        var parent = Path.Combine(_rootDir, "content");
        var nested = Path.Combine(parent, "posts", "hello.md");
        Directory.CreateDirectory(parent);

        Assert.True(PathUtils.IsSameOrSubPathOf(parent, parent));
        Assert.True(PathUtils.IsSameOrSubPathOf(nested, parent));
    }

    [Fact]
    public void IsSameOrSubPathOf_SiblingPath_ReturnsFalse()
    {
        var parent = Path.Combine(_rootDir, "content");
        var sibling = Path.Combine(_rootDir, "assets", "logo.png");
        Directory.CreateDirectory(parent);

        Assert.False(PathUtils.IsSameOrSubPathOf(sibling, parent));
    }

    [Fact]
    public void IsSubPathOf_WhitespacePath_Throws()
    {
        var parent = Path.Combine(_rootDir, "content");
        Directory.CreateDirectory(parent);

        Assert.Throws<ArgumentException>(() => PathUtils.IsSubPathOf(" ", parent));
    }
}
