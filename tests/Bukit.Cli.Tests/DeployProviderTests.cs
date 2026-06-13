using Bukit.Cli.Deploy;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DeployProviderTests
{
    [Theory]
    [InlineData("! [rejected] gh-pages -> gh-pages (non-fast-forward)")]
    [InlineData("Updates were rejected because the remote contains work that you do not have locally.")]
    [InlineData("hint: Updates were rejected. You may want to first integrate the remote changes.")]
    [InlineData("error: failed to push some refs to origin. fetch first")]
    public void IsNonFastForwardPush_KnownGitMessages_ReturnsTrue(string message)
    {
        Assert.True(GitHubPagesDeployProvider.IsNonFastForwardPush(message));
    }

    [Fact]
    public void IsNonFastForwardPush_UnrelatedMessage_ReturnsFalse()
    {
        Assert.False(GitHubPagesDeployProvider.IsNonFastForwardPush("fatal: Authentication failed"));
    }
}
