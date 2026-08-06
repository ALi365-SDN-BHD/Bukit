using Bukit.Engine.Output;
using Bukit.Shared.IO;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class DirectoryCopyTests : IDisposable
{
    private readonly List<string> _tempRoots = new();

    [Fact]
    public void SyncFiles_NoOps_WhenSourceDirectoryDoesNotExist()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "missing");
        var destinationDir = Path.Combine(root, "output");

        DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: true);

        Assert.False(Directory.Exists(destinationDir));
    }

    [Fact]
    public void SyncFiles_CopiesNewFile_AndPreservesTimestamp()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);

        var sourceFile = Path.Combine(sourceDir, "photo.jpg");
        File.WriteAllText(sourceFile, "v1");
        var sourceTimestamp = new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourceFile, sourceTimestamp);

        DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: false);

        var destinationFile = Path.Combine(destinationDir, "photo.jpg");
        Assert.True(File.Exists(destinationFile));
        Assert.Equal("v1", File.ReadAllText(destinationFile));
        Assert.Equal(sourceTimestamp, File.GetLastWriteTimeUtc(destinationFile));
    }

    [Fact]
    public void SyncFiles_SkipsUnchangedDestinationFile()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destinationDir);

        var sourceFile = Path.Combine(sourceDir, "photo.jpg");
        var destinationFile = Path.Combine(destinationDir, "photo.jpg");
        File.WriteAllText(sourceFile, "same-content");
        var sharedTimestamp = new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourceFile, sharedTimestamp);
        File.WriteAllText(destinationFile, "same-content");
        File.SetLastWriteTimeUtc(destinationFile, sharedTimestamp);
        File.SetAttributes(destinationFile, File.GetAttributes(destinationFile) | FileAttributes.ReadOnly);

        try
        {
            DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: true);
        }
        finally
        {
            File.SetAttributes(destinationFile, FileAttributes.Normal);
        }

        Assert.Equal("same-content", File.ReadAllText(destinationFile));
        Assert.Equal(sharedTimestamp, File.GetLastWriteTimeUtc(destinationFile));
    }

    [Fact]
    public void SyncFiles_OverwritesDestination_WhenLengthOrTimestampDiffers()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destinationDir);

        var sourceFile = Path.Combine(sourceDir, "photo.jpg");
        var destinationFile = Path.Combine(destinationDir, "photo.jpg");
        File.WriteAllText(sourceFile, "new-content");
        var sourceTimestamp = new DateTime(2024, 05, 06, 07, 08, 09, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourceFile, sourceTimestamp);
        File.WriteAllText(destinationFile, "old");
        File.SetLastWriteTimeUtc(destinationFile, sourceTimestamp.AddMinutes(-10));

        DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: false);

        Assert.Equal("new-content", File.ReadAllText(destinationFile));
        Assert.Equal(sourceTimestamp, File.GetLastWriteTimeUtc(destinationFile));
    }

    [Fact]
    public void SyncFiles_IgnoresDotPrefixedFiles_WhenOptionEnabled()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);

        File.WriteAllText(Path.Combine(sourceDir, ".hidden.jpg"), "skip-me");

        DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: true);

        Assert.False(File.Exists(Path.Combine(destinationDir, ".hidden.jpg")));
    }

    [Fact]
    public void Sync_Sha256ModeCopiesWhenContentChangedButSizeAndTimeSame()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destinationDir);
        var sourceFile = Path.Combine(sourceDir, "main.css");
        var destinationFile = Path.Combine(destinationDir, "main.css");
        var timestamp = DateTime.UtcNow.AddMinutes(-10);

        File.WriteAllText(sourceFile, "bbbb");
        File.WriteAllText(destinationFile, "aaaa");
        File.SetLastWriteTimeUtc(sourceFile, timestamp);
        File.SetLastWriteTimeUtc(destinationFile, timestamp);

        DirectoryCopy.Sync(sourceDir, destinationDir, new DirectoryCopyOptions { HashMode = "sha256", IgnoreDotPrefixedFiles = false });

        Assert.Equal("bbbb", File.ReadAllText(destinationFile));
    }

    [Fact]
    public void Sync_PruneDeletesFilesRemovedFromSource()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destinationDir);
        File.WriteAllText(Path.Combine(sourceDir, "main.css"), "current");
        File.WriteAllText(Path.Combine(destinationDir, "main.css"), "old");
        File.WriteAllText(Path.Combine(destinationDir, "removed.css"), "stale");

        DirectoryCopy.Sync(sourceDir, destinationDir, prune: true);

        Assert.True(File.Exists(Path.Combine(destinationDir, "main.css")));
        Assert.False(File.Exists(Path.Combine(destinationDir, "removed.css")));
    }

    [Fact]
    public void SyncFiles_DoesNotCopySubdirectoryFiles()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var nestedDir = Path.Combine(sourceDir, "nested");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(nestedDir);

        File.WriteAllText(Path.Combine(nestedDir, "nested.jpg"), "nested");

        DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: true);

        Assert.False(File.Exists(Path.Combine(destinationDir, "nested.jpg")));
        Assert.False(Directory.Exists(Path.Combine(destinationDir, "nested")));
    }

    [Fact]
    public void SyncFilesRecursive_CopiesNestedFilesAndSkipsDotPrefixedFiles()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var nestedDir = Path.Combine(sourceDir, "posts", "2026");
        Directory.CreateDirectory(nestedDir);
        File.WriteAllText(Path.Combine(sourceDir, "cover.png"), "cover");
        File.WriteAllText(Path.Combine(nestedDir, "article-cover.png"), "article");
        File.WriteAllText(Path.Combine(nestedDir, ".tmp"), "skip-me");

        var destinationDir = Path.Combine(root, "output");
        DirectoryCopy.SyncFilesRecursive(sourceDir, destinationDir, ignoreDotPrefixedFiles: false);

        Assert.True(File.Exists(Path.Combine(destinationDir, "cover.png")));
        Assert.Equal("cover", File.ReadAllText(Path.Combine(destinationDir, "cover.png")));
        Assert.True(File.Exists(Path.Combine(destinationDir, "posts", "2026", "article-cover.png")));
        Assert.Equal("article", File.ReadAllText(Path.Combine(destinationDir, "posts", "2026", "article-cover.png")));
    }

    [Fact]
    public void SyncFilesRecursive_SkipsDirectorySymlinkToExternalRoot()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var nestedDir = Path.Combine(sourceDir, "nested");
        var externalDir = Path.Combine(root, "external");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(nestedDir);
        Directory.CreateDirectory(externalDir);
        File.WriteAllText(Path.Combine(nestedDir, "local.txt"), "local");
        File.WriteAllText(Path.Combine(externalDir, "secret.txt"), "secret");

        try
        {
            Directory.CreateSymbolicLink(Path.Combine(sourceDir, "linked-external"), externalDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"Directory symlinks are unavailable: {ex.GetType().Name}");
        }

        DirectoryCopy.SyncFilesRecursive(sourceDir, destinationDir, ignoreDotPrefixedFiles: false);

        Assert.Equal("local", File.ReadAllText(Path.Combine(destinationDir, "nested", "local.txt")));
        Assert.False(File.Exists(Path.Combine(destinationDir, "linked-external", "secret.txt")));
    }

    [Fact]
    public void SyncPlannedFile_WhenValidatedSourceIsRetargetedOutsideRoot_ThrowsBeforeWriting()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var externalDir = Path.Combine(root, "external");
        var outputDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(externalDir);
        var sourceFile = Path.Combine(sourceDir, "public.txt");
        var externalFile = Path.Combine(externalDir, "secret.txt");
        File.WriteAllText(sourceFile, "safe");
        File.WriteAllText(externalFile, "secret");
        var planned = Assert.Single(DirectoryCopy.EnumerateFilesForSync(
            sourceDir,
            new DirectoryCopyOptions { FollowSymlinks = true }));
        File.Delete(sourceFile);
        try
        {
            File.CreateSymbolicLink(sourceFile, externalFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"File symlinks are unavailable: {ex.GetType().Name}");
        }

        var exception = Assert.Throws<IOException>(() => DirectoryCopy.SyncPlannedFile(
            planned.SourcePath,
            Path.Combine(outputDir, "public.txt"),
            "size-time",
            outputDir,
            planned.PhysicalSourceRoot,
            new DirectoryCopyOptions { FollowSymlinks = true }));

        Assert.Contains("changed after validation", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(outputDir));
    }

    [Fact]
    public void SyncPlannedFile_WhenValidatedSourceRootIsRetargeted_ThrowsBeforeWriting()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var externalDir = Path.Combine(root, "external");
        var outputDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(externalDir);
        File.WriteAllText(Path.Combine(sourceDir, "public.txt"), "safe");
        File.WriteAllText(Path.Combine(externalDir, "public.txt"), "secret");
        var options = new DirectoryCopyOptions { FollowSymlinks = true };
        var planned = Assert.Single(DirectoryCopy.EnumerateFilesForSync(sourceDir, options));

        Directory.Delete(sourceDir, recursive: true);
        try
        {
            Directory.CreateSymbolicLink(sourceDir, externalDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"Directory symlinks are unavailable: {ex.GetType().Name}");
        }

        var exception = Assert.Throws<IOException>(() => DirectoryCopy.SyncPlannedFile(
            planned.SourcePath,
            Path.Combine(outputDir, "public.txt"),
            "size-time",
            outputDir,
            planned.PhysicalSourceRoot,
            options));

        Assert.Contains("root changed after validation", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(outputDir));
    }

    [Fact]
    public void Sync_DotfilesAllowed_SensitiveDotfilesStillDenied()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);

        File.WriteAllText(Path.Combine(sourceDir, ".env"), "secret");
        File.WriteAllText(Path.Combine(sourceDir, ".env.local"), "local-secret");
        File.WriteAllText(Path.Combine(sourceDir, "server.pem"), "private-key");
        File.WriteAllText(Path.Combine(sourceDir, "cert.key"), "cert-key");
        File.WriteAllText(Path.Combine(sourceDir, "prod.pfx"), "pfx-data");
        File.WriteAllText(Path.Combine(sourceDir, "ca.p12"), "p12-data");
        File.WriteAllText(Path.Combine(sourceDir, ".git"), "git-dir");
        File.WriteAllText(Path.Combine(sourceDir, ".github"), "github-dir");
        File.WriteAllText(Path.Combine(sourceDir, ".npmrc"), "npmrc-data");
        File.WriteAllText(Path.Combine(sourceDir, ".htaccess"), "htaccess-data");
        File.WriteAllText(Path.Combine(sourceDir, "regular.txt"), "regular");

        DirectoryCopy.Sync(sourceDir, destinationDir, new DirectoryCopyOptions { IgnoreDotPrefixedFiles = false });

        Assert.False(File.Exists(Path.Combine(destinationDir, ".env")));
        Assert.False(File.Exists(Path.Combine(destinationDir, ".env.local")));
        Assert.False(File.Exists(Path.Combine(destinationDir, "server.pem")));
        Assert.False(File.Exists(Path.Combine(destinationDir, "cert.key")));
        Assert.False(File.Exists(Path.Combine(destinationDir, "prod.pfx")));
        Assert.False(File.Exists(Path.Combine(destinationDir, "ca.p12")));
        Assert.False(File.Exists(Path.Combine(destinationDir, ".git")));
        Assert.False(File.Exists(Path.Combine(destinationDir, ".github")));
        Assert.False(File.Exists(Path.Combine(destinationDir, ".npmrc")));
        Assert.True(File.Exists(Path.Combine(destinationDir, ".htaccess")));
        Assert.Equal("htaccess-data", File.ReadAllText(Path.Combine(destinationDir, ".htaccess")));
        Assert.True(File.Exists(Path.Combine(destinationDir, "regular.txt")));
    }

    [Fact]
    public void Sync_DotfilesAllowed_WellKnownAlwaysAllowed()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var wellKnownDir = Path.Combine(sourceDir, ".well-known");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(wellKnownDir);
        File.WriteAllText(Path.Combine(wellKnownDir, "security.txt"), "security-content");
        File.WriteAllText(Path.Combine(sourceDir, "index.html"), "home");

        DirectoryCopy.Sync(sourceDir, destinationDir, new DirectoryCopyOptions { IgnoreDotPrefixedFiles = false });

        Assert.True(File.Exists(Path.Combine(destinationDir, ".well-known", "security.txt")));
        Assert.Equal("security-content", File.ReadAllText(Path.Combine(destinationDir, ".well-known", "security.txt")));
        Assert.True(File.Exists(Path.Combine(destinationDir, "index.html")));
    }

    [Fact]
    public void Sync_DefaultOptions_DotPrefixedFilesStillSkipped()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);

        File.WriteAllText(Path.Combine(sourceDir, ".hidden"), "hidden");
        File.WriteAllText(Path.Combine(sourceDir, ".config"), "config");
        File.WriteAllText(Path.Combine(sourceDir, "visible.txt"), "visible");

        DirectoryCopy.Sync(sourceDir, destinationDir, new DirectoryCopyOptions());

        Assert.False(File.Exists(Path.Combine(destinationDir, ".hidden")));
        Assert.False(File.Exists(Path.Combine(destinationDir, ".config")));
        Assert.True(File.Exists(Path.Combine(destinationDir, "visible.txt")));
    }

    [Fact]
    public void Sync_AlwaysDenySensitiveDotfilesFalse_AllowsSensitiveFiles()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);

        File.WriteAllText(Path.Combine(sourceDir, ".env"), "secret");
        File.WriteAllText(Path.Combine(sourceDir, ".htaccess"), "htaccess-data");

        DirectoryCopy.Sync(sourceDir, destinationDir,
            new DirectoryCopyOptions { IgnoreDotPrefixedFiles = false, AlwaysDenySensitiveDotfiles = false });

        Assert.True(File.Exists(Path.Combine(destinationDir, ".env")));
        Assert.True(File.Exists(Path.Combine(destinationDir, ".htaccess")));
    }

    [Fact]
    public void Sync_SkipsSymlinkFile_CopiesRegularFile()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);

        var regularFile = Path.Combine(sourceDir, "photo.jpg");
        File.WriteAllText(regularFile, "regular");

        var symlinkFile = Path.Combine(sourceDir, "link.jpg");
        try
        {
            File.CreateSymbolicLink(symlinkFile, regularFile);
        }
        catch
        {
            return;
        }

        DirectoryCopy.Sync(sourceDir, destinationDir, prune: false);

        Assert.True(File.Exists(Path.Combine(destinationDir, "photo.jpg")));
        Assert.Equal("regular", File.ReadAllText(Path.Combine(destinationDir, "photo.jpg")));
        Assert.False(File.Exists(Path.Combine(destinationDir, "link.jpg")));
    }

    [Fact]
    public void Sync_SkipsSymlinkDirectory()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);

        var realSubDir = Path.Combine(sourceDir, "real");
        Directory.CreateDirectory(realSubDir);
        File.WriteAllText(Path.Combine(realSubDir, "inside.txt"), "inside");

        var symlinkDir = Path.Combine(sourceDir, "linkdir");
        try
        {
            Directory.CreateSymbolicLink(symlinkDir, realSubDir);
        }
        catch
        {
            return;
        }

        DirectoryCopy.Sync(sourceDir, destinationDir, prune: false);

        Assert.True(File.Exists(Path.Combine(destinationDir, "real", "inside.txt")));
        Assert.False(Directory.Exists(Path.Combine(destinationDir, "linkdir")));
    }

    [Fact]
    public void Sync_CopiesSymlink_WhenFollowSymlinksTrue()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);

        var regularFile = Path.Combine(sourceDir, "photo.jpg");
        File.WriteAllText(regularFile, "regular");

        var symlinkFile = Path.Combine(sourceDir, "link.jpg");
        try
        {
            File.CreateSymbolicLink(symlinkFile, regularFile);
        }
        catch
        {
            return;
        }

        var options = new DirectoryCopyOptions { FollowSymlinks = true, IgnoreDotPrefixedFiles = false };
        DirectoryCopy.Sync(sourceDir, destinationDir, options);

        Assert.True(File.Exists(Path.Combine(destinationDir, "photo.jpg")));
        Assert.True(File.Exists(Path.Combine(destinationDir, "link.jpg")));
    }

    [Fact]
    public void SyncFilesRecursive_SkipsSymlink()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var nestedDir = Path.Combine(sourceDir, "nested");
        Directory.CreateDirectory(nestedDir);

        var regularFile = Path.Combine(nestedDir, "real.txt");
        File.WriteAllText(regularFile, "real");

        var symlinkFile = Path.Combine(nestedDir, "link.txt");
        try
        {
            File.CreateSymbolicLink(symlinkFile, regularFile);
        }
        catch
        {
            return;
        }

        var destinationDir = Path.Combine(root, "output");
        DirectoryCopy.SyncFilesRecursive(sourceDir, destinationDir, ignoreDotPrefixedFiles: false);

        Assert.True(File.Exists(Path.Combine(destinationDir, "nested", "real.txt")));
        Assert.Equal("real", File.ReadAllText(Path.Combine(destinationDir, "nested", "real.txt")));
        Assert.False(File.Exists(Path.Combine(destinationDir, "nested", "link.txt")));
    }

    [Fact]
    public void Copy_SkipsSymlink()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);

        var regularFile = Path.Combine(sourceDir, "photo.jpg");
        File.WriteAllText(regularFile, "regular");

        var symlinkFile = Path.Combine(sourceDir, "link.jpg");
        try
        {
            File.CreateSymbolicLink(symlinkFile, regularFile);
        }
        catch
        {
            return;
        }

        DirectoryCopy.Copy(sourceDir, destinationDir);

        Assert.True(File.Exists(Path.Combine(destinationDir, "photo.jpg")));
        Assert.False(File.Exists(Path.Combine(destinationDir, "link.jpg")));
    }

    public void Dispose()
    {
        foreach (var root in _tempRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }
            }

            Directory.Delete(root, recursive: true);
        }
    }

    private string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        _tempRoots.Add(root);
        return root;
    }

    // ── Copy method ──────────────────────────────────────────────────

    [Fact]
    public void Copy_NoOps_WhenSourceDirectoryDoesNotExist()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "missing");
        var destinationDir = Path.Combine(root, "output");

        DirectoryCopy.Copy(sourceDir, destinationDir);

        Assert.False(Directory.Exists(destinationDir));
    }

    [Fact]
    public void Copy_CopiesFilesAndSubdirectories()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(Path.Combine(sourceDir, "sub"));
        File.WriteAllText(Path.Combine(sourceDir, "a.txt"), "a");
        File.WriteAllText(Path.Combine(sourceDir, "sub", "b.txt"), "b");

        DirectoryCopy.Copy(sourceDir, destinationDir);

        Assert.True(File.Exists(Path.Combine(destinationDir, "a.txt")));
        Assert.True(File.Exists(Path.Combine(destinationDir, "sub", "b.txt")));
        Assert.Equal("a", File.ReadAllText(Path.Combine(destinationDir, "a.txt")));
    }

    [Fact]
    public void Copy_SkipsDotPrefixedFilesAndDirectories()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(Path.Combine(sourceDir, ".hidden-dir"));
        File.WriteAllText(Path.Combine(sourceDir, ".hidden.txt"), "skip");
        File.WriteAllText(Path.Combine(sourceDir, "visible.txt"), "keep");

        DirectoryCopy.Copy(sourceDir, destinationDir);

        Assert.False(File.Exists(Path.Combine(destinationDir, ".hidden.txt")));
        Assert.False(Directory.Exists(Path.Combine(destinationDir, ".hidden-dir")));
        Assert.True(File.Exists(Path.Combine(destinationDir, "visible.txt")));
    }

    [Fact]
    public void Copy_OverwritesExistingDestinationFile()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destinationDir);
        File.WriteAllText(Path.Combine(sourceDir, "a.txt"), "new");
        File.WriteAllText(Path.Combine(destinationDir, "a.txt"), "old");

        DirectoryCopy.Copy(sourceDir, destinationDir);

        Assert.Equal("new", File.ReadAllText(Path.Combine(destinationDir, "a.txt")));
    }

    [Fact]
    public void Copy_UsesPathReturnedByOutputPolicy()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        var redirectedDir = Path.Combine(root, "redirected");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "asset.txt"), "content");

        DirectoryCopy.Copy(
            sourceDir,
            destinationDir,
            destinationDir,
            new RedirectingOutputPathPolicy(redirectedDir));

        Assert.Equal("content", File.ReadAllText(Path.Combine(redirectedDir, "asset.txt")));
        Assert.False(File.Exists(Path.Combine(destinationDir, "asset.txt")));
    }

    [Fact]
    public void SyncFiles_UsesPathReturnedByOutputPolicy()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        var redirectedDir = Path.Combine(root, "redirected");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "asset.txt"), "content");

        DirectoryCopy.SyncFiles(
            sourceDir,
            destinationDir,
            ignoreDotPrefixedFiles: false,
            outputRoot: destinationDir,
            pathPolicy: new RedirectingOutputPathPolicy(redirectedDir));

        Assert.Equal("content", File.ReadAllText(Path.Combine(redirectedDir, "asset.txt")));
        Assert.False(File.Exists(Path.Combine(destinationDir, "asset.txt")));
    }

    [Fact]
    public void Sync_NonExistentSource_NoOps()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "missing");
        var destinationDir = Path.Combine(root, "output");

        DirectoryCopy.Sync(sourceDir, destinationDir);

        Assert.False(Directory.Exists(destinationDir));
    }

    [Fact]
    public void Sync_WithPrune_RemovesStaleDestinationFiles()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destinationDir);
        File.WriteAllText(Path.Combine(sourceDir, "keep.txt"), "keep");
        File.WriteAllText(Path.Combine(destinationDir, "keep.txt"), "old");
        File.WriteAllText(Path.Combine(destinationDir, "stale.txt"), "stale");
        Directory.CreateDirectory(Path.Combine(destinationDir, "stale-dir"));

        DirectoryCopy.Sync(sourceDir, destinationDir, prune: true);

        Assert.True(File.Exists(Path.Combine(destinationDir, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(destinationDir, "stale.txt")));
        Assert.False(Directory.Exists(Path.Combine(destinationDir, "stale-dir")));
    }

    [Fact]
    public void SyncFilesRecursive_CopiesNestedStructure()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(Path.Combine(sourceDir, "deep", "nested"));
        File.WriteAllText(Path.Combine(sourceDir, "top.txt"), "top");
        File.WriteAllText(Path.Combine(sourceDir, "deep", "nested", "bottom.txt"), "bottom");

        DirectoryCopy.SyncFilesRecursive(sourceDir, destinationDir, ignoreDotPrefixedFiles: false);

        Assert.True(File.Exists(Path.Combine(destinationDir, "top.txt")));
        Assert.True(File.Exists(Path.Combine(destinationDir, "deep", "nested", "bottom.txt")));
    }

    [Fact]
    public void EnumerateFilesForSync_ReturnsSortedRelativePaths()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        Directory.CreateDirectory(Path.Combine(sourceDir, "sub"));
        File.WriteAllText(Path.Combine(sourceDir, "b.txt"), "b");
        File.WriteAllText(Path.Combine(sourceDir, "a.txt"), "a");
        File.WriteAllText(Path.Combine(sourceDir, "sub", "c.txt"), "c");

        var items = DirectoryCopy.EnumerateFilesForSync(sourceDir, new DirectoryCopyOptions());

        Assert.Equal(3, items.Count);
        Assert.Equal("a.txt", items[0].RelativePath);
        Assert.Equal("b.txt", items[1].RelativePath);
        Assert.Equal(Path.Combine("sub", "c.txt"), items[2].RelativePath);
    }

    [Fact]
    public void EnumerateFilesForSync_NonExistentSource_ReturnsEmpty()
    {
        var root = CreateTempRoot();
        var items = DirectoryCopy.EnumerateFilesForSync(Path.Combine(root, "missing"), new DirectoryCopyOptions());
        Assert.Empty(items);
    }

    [Fact]
    public void SyncPlannedFile_CopiesFileWhenSourceUnchanged()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destinationDir);
        File.WriteAllText(Path.Combine(sourceDir, "asset.txt"), "content");

        // Obtain the planned item (source + physical root) exactly as AssetPipeline does
        var planned = DirectoryCopy.EnumerateFilesForSync(sourceDir, new DirectoryCopyOptions()).Single();
        DirectoryCopy.SyncPlannedFile(
            planned.SourcePath,
            Path.Combine(destinationDir, "asset.txt"),
            "size-time",
            destinationDir,
            planned.PhysicalSourceRoot,
            new DirectoryCopyOptions());

        Assert.True(File.Exists(Path.Combine(destinationDir, "asset.txt")));
    }

    [Fact]
    public void SyncPlannedFile_UsesPathReturnedByOutputPolicy()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        var redirectedDir = Path.Combine(root, "redirected");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "asset.txt"), "content");
        var options = new DirectoryCopyOptions();
        var planned = DirectoryCopy.EnumerateFilesForSync(sourceDir, options).Single();

        DirectoryCopy.SyncPlannedFile(
            planned.SourcePath,
            Path.Combine(destinationDir, "asset.txt"),
            "size-time",
            destinationDir,
            planned.PhysicalSourceRoot,
            options,
            new RedirectingOutputPathPolicy(redirectedDir));

        Assert.Equal("content", File.ReadAllText(Path.Combine(redirectedDir, "asset.txt")));
        Assert.False(File.Exists(Path.Combine(destinationDir, "asset.txt")));
    }

    [Fact]
    public void SyncPlannedFile_WhenPathChangesAfterVerifiedOpen_CopiesFromVerifiedHandle()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);
        var sourceFile = Path.Combine(sourceDir, "asset.txt");
        var destinationFile = Path.Combine(destinationDir, "asset.txt");
        File.WriteAllText(sourceFile, "safe");
        var planned = DirectoryCopy.EnumerateFilesForSync(sourceDir, new DirectoryCopyOptions()).Single();
        var opener = new ReplacingSourceOpener("safe", "evil");

        DirectoryCopy.SyncPlannedFile(
            planned.SourcePath,
            destinationFile,
            "size-time",
            destinationDir,
            planned.PhysicalSourceRoot,
            new DirectoryCopyOptions(),
            opener: opener);

        Assert.Equal("safe", File.ReadAllText(destinationFile));
    }

    [Fact]
    public void SyncPlannedFile_WhenVerifiedOpenIsRejected_PreservesExistingDestination()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destinationDir);
        File.WriteAllText(Path.Combine(sourceDir, "asset.txt"), "source");
        var destinationFile = Path.Combine(destinationDir, "asset.txt");
        File.WriteAllText(destinationFile, "existing");
        var planned = DirectoryCopy.EnumerateFilesForSync(sourceDir, new DirectoryCopyOptions()).Single();

        Assert.Throws<IOException>(() => DirectoryCopy.SyncPlannedFile(
            planned.SourcePath,
            destinationFile,
            "size-time",
            destinationDir,
            planned.PhysicalSourceRoot,
            new DirectoryCopyOptions(),
            opener: new RejectingSourceOpener()));

        Assert.Equal("existing", File.ReadAllText(destinationFile));
    }

    private sealed class RedirectingOutputPathPolicy(string redirectedRoot) : IOutputPathPolicy
    {
        public string ResolveSafePath(string outputRoot, string relativePath)
            => Path.GetFullPath(Path.Combine(redirectedRoot, relativePath));
    }

    private sealed class ReplacingSourceOpener : ISafeSourceFileOpener
    {
        private readonly string _verifiedContent;
        private readonly string _replacementContent;

        public ReplacingSourceOpener(string verifiedContent, string replacementContent)
        {
            _verifiedContent = verifiedContent;
            _replacementContent = replacementContent;
        }

        public VerifiedSourceFile Open(string path, string sourceRoot)
        {
            Assert.Equal(_verifiedContent, File.ReadAllText(path));
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var displacedPath = path + ".displaced";
            File.Move(path, displacedPath);
            File.WriteAllText(path, _replacementContent);
            return new VerifiedSourceFile(
                stream.SafeFileHandle,
                stream,
                displacedPath);
        }
    }

    private sealed class RejectingSourceOpener : ISafeSourceFileOpener
    {
        public VerifiedSourceFile Open(string path, string sourceRoot)
            => throw new IOException("rejected");
    }

}
