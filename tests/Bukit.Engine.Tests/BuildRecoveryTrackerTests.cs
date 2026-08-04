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

    [Fact]
    public void HasIncompleteBuild_MalformedExistingState_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_dir, ".bukit-build-state.json"), "{not-json");

        Assert.True(BuildRecoveryTracker.HasIncompleteBuild(_dir));
    }

    [Fact]
    public void HasIncompleteBuild_UnknownState_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_dir, ".bukit-build-state.json"), """{"status":"paused"}""");

        Assert.True(BuildRecoveryTracker.HasIncompleteBuild(_dir));
    }

    [Fact]
    public void MarkStarted_WriteFailure_PreservesPreviousState()
    {
        BuildRecoveryTracker.MarkCompleted(_dir);
        var statePath = Path.Combine(_dir, ".bukit-build-state.json");
        var previous = File.ReadAllText(statePath);
        File.SetAttributes(statePath, FileAttributes.ReadOnly);
        try
        {
            var exception = Record.Exception(() => BuildRecoveryTracker.MarkStarted(_dir));
            if (exception is null)
            {
                // POSIX rename can replace a read-only file; the replacement must be atomic.
                Assert.Contains("started", File.ReadAllText(statePath), StringComparison.Ordinal);
            }
            else
            {
                // A failed replacement must leave the previous complete document intact.
                Assert.Equal(previous, File.ReadAllText(statePath));
                Assert.False(BuildRecoveryTracker.HasIncompleteBuild(_dir));
            }

            // No torn or half-written state document may remain.
            var content = File.ReadAllText(statePath);
            Assert.True(content.Contains("started", StringComparison.Ordinal) || content.Contains("completed", StringComparison.Ordinal));
        }
        finally
        {
            try
            {
                File.SetAttributes(statePath, FileAttributes.Normal);
            }
            catch
            {
                // Attribute restore is best effort on read-only targets.
            }
        }
    }
}
