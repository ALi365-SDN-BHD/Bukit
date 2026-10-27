using Bukit.Shared;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class OutputDirectoryCleanerTests : IDisposable
{
    private readonly string _root;

    public OutputDirectoryCleanerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "bukit-output-cleaner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => TestCleanup.DeleteDirectory(_root, recursive: true);

    [Fact]
    public void CleanIfExists_MissingOutput_DoesNothing()
    {
        OutputDirectoryCleaner.CleanIfExists(_root, Path.Combine(_root, "dist"));

        Assert.False(Directory.Exists(Path.Combine(_root, "dist")));
    }

    [Fact]
    public void CleanIfExists_WithMarker_DeletesOutput()
    {
        var output = Path.Combine(_root, "dist");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, ".bukit-output-marker"), "bukit-output\n");
        File.WriteAllText(Path.Combine(output, "index.html"), "old");

        OutputDirectoryCleaner.CleanIfExists(_root, output);

        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void CleanIfExists_NonEmptyWithoutMarker_Throws()
    {
        var output = Path.Combine(_root, "dist");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "user.txt"), "do not delete");

        var ex = Assert.Throws<ConfigException>(() => OutputDirectoryCleaner.CleanIfExists(_root, output));

        Assert.Equal(DiagnosticCode.BuildOutputNoMarker, ex.Code);
        Assert.True(File.Exists(Path.Combine(output, "user.txt")));
    }

    [Fact]
    public void EnsureCanClean_EmptyOutput_Allows()
    {
        var output = Path.Combine(_root, "dist");
        Directory.CreateDirectory(output);

        OutputDirectoryCleaner.EnsureCanClean(_root, output);
    }

    [Fact]
    public void EnsureCanClean_RequireMarkerFalse_AllowsNonEmpty()
    {
        var output = Path.Combine(_root, "dist");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "user.txt"), "allowed when explicitly requested");

        OutputDirectoryCleaner.EnsureCanClean(_root, output, requireMarker: false);
    }

    [Fact]
    public void EnsureCanClean_Root_Throws()
    {
        var ex = Assert.Throws<ConfigException>(() => OutputDirectoryCleaner.EnsureCanClean(_root, _root));

        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, ex.Code);
    }

    [Fact]
    public void EnsureCanClean_FilesystemRoot_Throws()
    {
        var root = Path.GetPathRoot(_root)!;

        var ex = Assert.Throws<ConfigException>(() => OutputDirectoryCleaner.EnsureCanClean(_root, root));

        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, ex.Code);
    }

    [Fact]
    public void EnsureCanClean_GitSegment_Throws()
    {
        var output = Path.Combine(_root, ".git", "dist");
        Directory.CreateDirectory(output);

        var ex = Assert.Throws<ConfigException>(() => OutputDirectoryCleaner.EnsureCanClean(_root, output));

        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, ex.Code);
    }

    [Fact]
    public void EnsureCanClean_MissingOutputSegment_Returns()
    {
        OutputDirectoryCleaner.EnsureCanClean(_root, Path.Combine(_root, "missing", "dist"));
    }

    [Fact, Trait("Category", "Integration")]
    public void EnsureCanClean_UserHome_Throws()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            throw SkipException.ForSkip("User profile is unavailable.");
        }

        var fullHome = Path.GetFullPath(home).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(fullHome);
        if (string.IsNullOrWhiteSpace(parent) ||
            string.Equals(fullHome, Path.GetPathRoot(fullHome), PlatformPathHelper.PathComparison))
        {
            throw SkipException.ForSkip("User profile is a filesystem root.");
        }

        var ex = Assert.Throws<ConfigException>(() =>
            OutputDirectoryCleaner.EnsureCanClean(parent, fullHome));

        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, ex.Code);
    }

    [Fact]
    public void EnsureCanClean_UserHomeCaseVariant_FollowsPlatformComparison()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.False(string.IsNullOrWhiteSpace(home), "User profile is unavailable.");

        var fullHome = Path.GetFullPath(home).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var variant = fullHome.ToUpperInvariant();
        if (variant == fullHome)
        {
            variant = fullHome.ToLowerInvariant();
        }
        Assert.NotEqual(fullHome, variant);
        var parent = Path.GetDirectoryName(variant);
        Assert.False(string.IsNullOrWhiteSpace(parent), "User profile has no parent directory.");

        var failure = Record.Exception(() =>
            OutputDirectoryCleaner.EnsureCanClean(parent, variant));

        if (OperatingSystem.IsLinux())
        {
            Assert.Null(failure);
            return;
        }

        Assert.IsType<ConfigException>(failure);
        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, ((ConfigException)failure).Code);
    }

    [Fact]
    public void EnsureCanClean_ExistingDirectoryWithoutReparsePoint_Returns()
    {
        var output = Path.Combine(_root, "dist");
        Directory.CreateDirectory(output);

        OutputDirectoryCleaner.EnsureCanClean(_root, output);
    }

    [Fact]
    public void EnsureCanClean_SymlinkBelowRoot_Throws()
    {
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        var link = Path.Combine(_root, "link");
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception linkException) when (linkException is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"Symbolic links are unavailable: {linkException.GetType().Name}");
        }

        Assert.NotNull(new DirectoryInfo(link).LinkTarget);

        var failure = Assert.Throws<ConfigException>(() =>
            OutputDirectoryCleaner.EnsureCanClean(_root, Path.Combine(link, "dist")));

        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, failure.Code);
    }
}
