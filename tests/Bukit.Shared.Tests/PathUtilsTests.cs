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
    public void IsSameOrSubPathOf_LinkTargetTraversingAnotherLink_ResolvesToPhysicalTarget()
    {
        var real = Path.Combine(_rootDir, "real");
        Directory.CreateDirectory(real);
        var dataPath = Path.Combine(real, "data.txt");
        File.WriteAllText(dataPath, "content");
        var dirAlias = Path.Combine(_rootDir, "alias1");
        var fileAlias = Path.Combine(_rootDir, "alias2.txt");
        Directory.CreateSymbolicLink(dirAlias, real);
        // The file link target itself routes through the directory link, so the
        // verbatim target string still contains an unresolved link component.
        File.CreateSymbolicLink(fileAlias, Path.Combine(dirAlias, "data.txt"));

        Assert.True(PathUtils.IsSameOrSubPathOf(fileAlias, real));
    }

    [Fact]
    public void IsSubPathOf_WhitespacePath_Throws()
    {
        var parent = Path.Combine(_rootDir, "content");
        Directory.CreateDirectory(parent);

        Assert.Throws<ArgumentException>(() => PathUtils.IsSubPathOf(" ", parent));
    }

    [Theory]
    [InlineData("content-other/page.md")]
    [InlineData("content/../outside/page.md")]
    public void PathRelations_SiblingPrefixOrDotSegmentEscape_ReturnFalse(string relative)
    {
        var parent = Path.Combine(_rootDir, "content");
        var child = Path.Combine(_rootDir, relative.Replace('/', Path.DirectorySeparatorChar));

        Assert.False(PathUtils.IsSubPathOf(child, parent));
        Assert.False(PathUtils.IsSameOrSubPathOf(child, parent));
    }

    [Fact]
    public void PathRelations_TrailingSeparatorsAndMissingTail_PreserveContainment()
    {
        var parent = Path.Combine(_rootDir, "content");
        var child = Path.Combine(parent, "posts", "..", "new", "page.md");

        Assert.True(PathUtils.IsSubPathOf(child, parent + Path.DirectorySeparatorChar));
        Assert.True(PathUtils.IsSameOrSubPathOf(parent + Path.DirectorySeparatorChar, parent));
        Assert.False(PathUtils.IsSubPathOf(parent + Path.DirectorySeparatorChar, parent));
        Assert.True(PathUtils.IsSubPathOf(_rootDir, Path.GetPathRoot(_rootDir)!));
    }

    [Fact]
    public void IsSubPathOf_VolumeRootItself_ReturnsFalse()
    {
        var root = Path.GetPathRoot(_rootDir)!;

        Assert.False(PathUtils.IsSubPathOf(root, root));
        Assert.False(PathUtils.IsSubPathOf(Path.Combine(root, "."), root));
        Assert.True(PathUtils.IsSameOrSubPathOf(root, root));
    }

    [Fact]
    public void PathRelations_MissingCaseDifferentPath_UsePlatformComparison()
    {
        var parent = Path.Combine(_rootDir, "Content");
        var child = Path.Combine(_rootDir, "content", "page.md");

        Assert.Equal(OperatingSystem.IsWindows(), PathUtils.IsSubPathOf(child, parent));
        Assert.Equal(OperatingSystem.IsWindows(), PathUtils.IsSameOrSubPathOf(child, parent));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void PathRelations_InvalidChildOrParent_Throws(string? invalid)
    {
        Assert.ThrowsAny<ArgumentException>(() => PathUtils.IsSubPathOf(invalid!, _rootDir));
        Assert.ThrowsAny<ArgumentException>(() => PathUtils.IsSubPathOf(_rootDir, invalid!));
        Assert.ThrowsAny<ArgumentException>(() => PathUtils.IsSameOrSubPathOf(invalid!, _rootDir));
        Assert.ThrowsAny<ArgumentException>(() => PathUtils.IsSameOrSubPathOf(_rootDir, invalid!));
    }

    [Fact]
    public void PathRelations_OutsideLinksAndMissingTail_DoNotEscape()
    {
        var parent = Directory.CreateDirectory(Path.Combine(_rootDir, "content")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(_rootDir, "outside")).FullName;
        var secret = Path.Combine(outside, "secret.txt");
        File.WriteAllText(secret, "secret");
        var directoryLink = Path.Combine(parent, "external");
        var fileLink = Path.Combine(parent, "secret.txt");
        Directory.CreateSymbolicLink(directoryLink, outside);
        File.CreateSymbolicLink(fileLink, secret);

        foreach (var candidate in new[] { fileLink, Path.Combine(directoryLink, "missing", "page.md") })
        {
            Assert.False(PathUtils.IsSubPathOf(candidate, parent));
            Assert.False(PathUtils.IsSameOrSubPathOf(candidate, parent));
        }
    }

    [Fact]
    public void PathRelations_ParentAliasAndInternalLink_ResolveSamePhysicalRoot()
    {
        var parent = Directory.CreateDirectory(Path.Combine(_rootDir, "content")).FullName;
        var alias = Path.Combine(_rootDir, "alias");
        Directory.CreateSymbolicLink(alias, parent);
        var target = Path.Combine(parent, "page.md");
        File.WriteAllText(target, "page");
        var link = Path.Combine(parent, "linked.md");
        File.CreateSymbolicLink(link, "page.md");

        Assert.True(PathUtils.IsSameOrSubPathOf(parent, alias));
        Assert.False(PathUtils.IsSubPathOf(parent, alias));
        Assert.True(PathUtils.IsSubPathOf(link, alias));
    }

    [Fact]
    public void EnumerateFiles_RecursionPatternAndHiddenFiles_ReturnExactSets()
    {
        var deep = _rootDir;
        for (var i = 0; i < 8; i++)
        {
            deep = Path.Combine(deep, "nested");
        }
        Directory.CreateDirectory(deep);
        var hidden = Path.Combine(_rootDir, ".hidden.md");
        var top = Path.Combine(_rootDir, "top.txt");
        var nested = Path.Combine(deep, "page.md");
        File.WriteAllText(hidden, "hidden");
        File.WriteAllText(top, "top");
        File.WriteAllText(nested, "nested");
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(hidden, FileAttributes.Hidden);
        }

        Assert.Equal(new[] { hidden, nested, top }.Order(StringComparer.Ordinal),
            SafeFileEnumerator.EnumerateFiles(_rootDir).Order(StringComparer.Ordinal));
        Assert.Equal(new[] { hidden, top }.Order(StringComparer.Ordinal),
            SafeFileEnumerator.EnumerateFiles(_rootDir, recurse: false).Order(StringComparer.Ordinal));
        Assert.Equal(new[] { hidden, nested }.Order(StringComparer.Ordinal),
            SafeFileEnumerator.EnumerateFiles(_rootDir, "*.md").Order(StringComparer.Ordinal));
        Assert.Empty(SafeFileEnumerator.EnumerateFiles(_rootDir, "*.html"));
    }

    [Fact]
    public void EnumerateFiles_FileDirectoryDanglingAndLoopLinks_AreSkipped()
    {
        var content = Directory.CreateDirectory(Path.Combine(_rootDir, "content")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(_rootDir, "outside")).FullName;
        var secret = Path.Combine(outside, "secret.txt");
        var regular = Path.Combine(content, "page.md");
        File.WriteAllText(secret, "secret");
        File.WriteAllText(regular, "page");
        File.CreateSymbolicLink(Path.Combine(content, "secret.txt"), secret);
        File.CreateSymbolicLink(Path.Combine(content, "dangling.txt"), Path.Combine(outside, "missing.txt"));
        Directory.CreateSymbolicLink(Path.Combine(content, "external"), outside);
        Directory.CreateSymbolicLink(Path.Combine(content, "loop"), content);

        Assert.Equal(new[] { regular }, SafeFileEnumerator.EnumerateFiles(content));
    }

    [Fact]
    public void EnumerateFiles_EmptyAndMissingDirectory_DistinguishEmptyFromFailure()
    {
        Assert.Empty(SafeFileEnumerator.EnumerateFiles(_rootDir));
        Assert.Throws<DirectoryNotFoundException>(() =>
            SafeFileEnumerator.EnumerateFiles(Path.Combine(_rootDir, "missing")).ToArray());
    }
}
