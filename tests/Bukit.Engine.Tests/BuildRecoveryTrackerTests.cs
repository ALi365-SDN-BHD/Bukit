using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildRecoveryTrackerTests : IDisposable
{
    private readonly string _dir;

    public BuildRecoveryTrackerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bukit-recovery-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void DetectIncompleteBuild_ReturnsFalseAfterCompletedBuild()
    {
        BuildRecoveryTracker.MarkStarted(_dir);
        BuildRecoveryTracker.MarkCompleted(_dir);

        Assert.False(BuildRecoveryTracker.HasIncompleteBuild(_dir));
    }

    [Fact]
    public void DetectIncompleteBuild_ReturnsTrueWhenPreviousBuildDidNotComplete()
    {
        BuildRecoveryTracker.MarkStarted(_dir);

        Assert.True(BuildRecoveryTracker.HasIncompleteBuild(_dir));
    }

    [Fact]
    public void HasIncompleteBuild_ReturnsFalseWhenOutputDirDoesNotExist()
    {
        var missingDir = Path.Combine(_dir, "missing");

        Assert.False(BuildRecoveryTracker.HasIncompleteBuild(missingDir));
    }

    [Fact]
    public void HasIncompleteBuild_ReturnsFalseWhenNoStateFile()
    {
        var emptyDir = Path.Combine(_dir, "empty");
        Directory.CreateDirectory(emptyDir);

        Assert.False(BuildRecoveryTracker.HasIncompleteBuild(emptyDir));
    }
}
