using System.Runtime.InteropServices;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class DirectoryCopyFollowSymlinksTests : IDisposable
{
    private readonly string _root;

    public DirectoryCopyFollowSymlinksTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "bukit-symlink-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static bool IsSymlinkPlatform()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [Fact]
    public void Sync_WithFollowSymlinks_InternalSymlink_IsCopied()
    {
        if (!IsSymlinkPlatform()) return;

        var sourceDir = Path.Combine(_root, "source");
        var destDir = Path.Combine(_root, "dest");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "real.txt"), "content");

        var linkPath = Path.Combine(sourceDir, "link.txt");
        File.CreateSymbolicLink(linkPath, Path.Combine(sourceDir, "real.txt"));

        DirectoryCopy.Sync(sourceDir, destDir, new DirectoryCopyOptions { FollowSymlinks = true });

        Assert.True(File.Exists(Path.Combine(destDir, "real.txt")));
        Assert.True(File.Exists(Path.Combine(destDir, "link.txt")));
        Assert.Equal("content", File.ReadAllText(Path.Combine(destDir, "link.txt")));
    }

    [Fact]
    public void Sync_WithFollowSymlinks_ExternalSymlink_IsSkipped()
    {
        if (!IsSymlinkPlatform()) return;

        var sourceDir = Path.Combine(_root, "source");
        var destDir = Path.Combine(_root, "dest");
        Directory.CreateDirectory(sourceDir);

        var outsideDir = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outsideDir);
        File.WriteAllText(Path.Combine(outsideDir, "secret.txt"), "secret");

        var linkPath = Path.Combine(sourceDir, "evil.txt");
        File.CreateSymbolicLink(linkPath, Path.Combine(outsideDir, "secret.txt"));

        DirectoryCopy.Sync(sourceDir, destDir, new DirectoryCopyOptions { FollowSymlinks = true });

        Assert.False(File.Exists(Path.Combine(destDir, "evil.txt")));
    }

    [Fact]
    public void Sync_WithoutFollowSymlinks_SymlinkIsSkipped()
    {
        if (!IsSymlinkPlatform()) return;

        var sourceDir = Path.Combine(_root, "source");
        var destDir = Path.Combine(_root, "dest");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "real.txt"), "content");

        var linkPath = Path.Combine(sourceDir, "link.txt");
        File.CreateSymbolicLink(linkPath, Path.Combine(sourceDir, "real.txt"));

        DirectoryCopy.Sync(sourceDir, destDir, new DirectoryCopyOptions { FollowSymlinks = false });

        Assert.True(File.Exists(Path.Combine(destDir, "real.txt")));
        Assert.False(File.Exists(Path.Combine(destDir, "link.txt")));
    }

    [Fact]
    public void Sync_WithFollowSymlinks_SymlinkChain_ResolvesFinalTarget()
    {
        if (!IsSymlinkPlatform()) return;

        var sourceDir = Path.Combine(_root, "source");
        var destDir = Path.Combine(_root, "dest");
        Directory.CreateDirectory(sourceDir);

        var outsideDir = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outsideDir);
        File.WriteAllText(Path.Combine(outsideDir, "target.txt"), "chain");

        var chain1 = Path.Combine(sourceDir, "chain1.txt");
        File.CreateSymbolicLink(chain1, Path.Combine(sourceDir, "chain2.txt"));

        var chain2 = Path.Combine(sourceDir, "chain2.txt");
        File.CreateSymbolicLink(chain2, Path.Combine(outsideDir, "target.txt"));

        DirectoryCopy.Sync(sourceDir, destDir, new DirectoryCopyOptions { FollowSymlinks = true });

        Assert.False(File.Exists(Path.Combine(destDir, "chain1.txt")));
    }
}
