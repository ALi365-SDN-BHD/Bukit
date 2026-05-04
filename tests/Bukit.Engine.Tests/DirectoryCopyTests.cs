using Xunit;

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

        DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: true);

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

        DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: true);

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
}
