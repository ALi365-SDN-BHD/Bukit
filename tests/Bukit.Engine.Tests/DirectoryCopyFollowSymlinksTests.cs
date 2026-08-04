using System.Runtime.InteropServices;
using Bukit.Engine.IO;
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

    [Fact]
    public void PlatformSafeSourceFileOpener_RegularFile_ReadsVerifiedHandle()
    {
        if (!OperatingSystem.IsWindows() && !IsSymlinkPlatform())
        {
            throw SkipException.ForSkip("The platform has no approved safe source opener.");
        }

        var sourceDir = Path.Combine(_root, "source");
        Directory.CreateDirectory(sourceDir);
        var sourceFile = Path.Combine(sourceDir, "regular.txt");
        File.WriteAllText(sourceFile, "verified");
        var physicalRoot = DirectoryCopy
            .EnumerateFilesForSync(sourceDir, new DirectoryCopyOptions())
            .Single()
            .PhysicalSourceRoot;

        using var verified = new PlatformSafeSourceFileOpener().Open(
            Path.Combine(physicalRoot, "regular.txt"),
            physicalRoot);
        using var reader = new StreamReader(
            verified.Stream,
            System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);

        Assert.Equal("verified", reader.ReadToEnd());
    }

    [Fact]
    public void PlatformSafeSourceFileOpener_FinalSymlink_IsRejected()
    {
        if (!OperatingSystem.IsWindows() && !IsSymlinkPlatform())
        {
            throw SkipException.ForSkip("The platform has no approved safe source opener.");
        }

        var sourceDir = Path.Combine(_root, "source");
        Directory.CreateDirectory(sourceDir);
        var target = Path.Combine(sourceDir, "target.txt");
        var link = Path.Combine(sourceDir, "link.txt");
        File.WriteAllText(target, "target");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"File symlinks are unavailable: {ex.GetType().Name}");
        }

        var physicalRoot = DirectoryCopy
            .EnumerateFilesForSync(sourceDir, new DirectoryCopyOptions())
            .Single()
            .PhysicalSourceRoot;

        Assert.Throws<IOException>(() =>
            new PlatformSafeSourceFileOpener().Open(
                Path.Combine(physicalRoot, "link.txt"),
                physicalRoot));
    }
}
