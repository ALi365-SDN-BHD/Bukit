using Bukit.Cli.Commands;
using Bukit.Cli.Deploy;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DeployRepoOptionTests
{
    [Theory]
    [InlineData("ALi365-SDN-BHD/site")]
    [InlineData("ali/docs.v2")]
    [InlineData("  ali/docs  ")]
    public void TryNormalizeRepo_AcceptsOwnerSlashName(string value)
    {
        Assert.True(GitHubPagesDeployProvider.TryNormalizeRepo(value, out var normalized, out var error));
        Assert.Null(error);
        Assert.Equal(value.Trim(), normalized);
    }

    [Fact]
    public void TryNormalizeRepo_Null_MeansUseOrigin()
    {
        Assert.True(GitHubPagesDeployProvider.TryNormalizeRepo(null, out var normalized, out var error));
        Assert.Null(normalized);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("https://github.com/ali/site")]
    [InlineData("git@github.com:ali/site.git")]
    [InlineData("ali/site.git")]
    [InlineData("ali/bad name")]
    [InlineData("ali/..")]
    [InlineData("ali")]
    [InlineData("ali/site/extra")]
    [InlineData("   ")]
    public void TryNormalizeRepo_RejectsInvalidValues_WithoutEchoingValue(string value)
    {
        Assert.False(GitHubPagesDeployProvider.TryNormalizeRepo(value, out var normalized, out var error));
        Assert.Null(normalized);
        Assert.NotNull(error);
        if (value.Trim().Length > 0)
        {
            Assert.DoesNotContain(value.Trim(), error, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ResolveDeployRepo_CliBeatsConfig()
    {
        var (repo, source) = DeployCommand.ResolveDeployRepo("cli/owner", "config/owner");

        Assert.Equal("cli/owner", repo);
        Assert.Equal("--repo", source);
    }

    [Fact]
    public void ResolveDeployRepo_FallsBackToConfig_ThenOrigin()
    {
        var fromConfig = DeployCommand.ResolveDeployRepo(null, "config/owner");
        Assert.Equal(("config/owner", "deploy.repo"), fromConfig);

        var fromOrigin = DeployCommand.ResolveDeployRepo(null, null);
        Assert.Equal((null, null), fromOrigin);
    }

}
