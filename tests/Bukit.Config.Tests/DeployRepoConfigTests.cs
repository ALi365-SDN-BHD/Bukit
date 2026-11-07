using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class DeployRepoConfigTests
{
    private static string Yaml(string repoLine) => $$"""
        site:
          name: myblog
          title: My Blog
        content:
          sources:
            - type: markdown
              markdown:
                dir: content
        deploy:
          provider: github-pages
        {{repoLine}}
        """;

    private static string WriteTemp(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-deploy-repo-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    [Fact]
    public void Load_DeployRepo_ParsesOwnerAndName()
    {
        var path = WriteTemp(Yaml("  repo: ALi365-SDN-BHD/site"));
        try
        {
            var config = ConfigLoader.Load(path);

            Assert.Equal("ALi365-SDN-BHD/site", config.Deploy?.Repo);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_DeployWithoutRepo_LeavesRepoUnset()
    {
        var path = WriteTemp(Yaml(string.Empty));
        try
        {
            var config = ConfigLoader.Load(path);

            Assert.Null(config.Deploy?.Repo);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("https://github.com/ali/site")]
    [InlineData("git@github.com:ali/site.git")]
    [InlineData("ali/site.git")]
    [InlineData("ali/bad name")]
    [InlineData("ali/..")]
    [InlineData("ali/.")]
    [InlineData("ali")]
    [InlineData("ali/site/extra")]
    [InlineData("-ali/site")]
    [InlineData("/site")]
    public void Load_DeployRepo_RejectsNonOwnerNameValues(string value)
    {
        var path = WriteTemp(Yaml($"  repo: \"{value}\""));
        try
        {
            var config = ConfigLoader.Load(path);

            var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
            Assert.Contains("deploy.repo must be 'owner/name'", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("  repo: \"\"")]
    [InlineData("  repo: \"   \"")]
    [InlineData("  repo:")]
    public void Validate_EmptyDeployRepo_FailsInsteadOfFallingBackToOrigin(string repoLine)
    {
        var path = WriteTemp(Yaml(repoLine));
        try
        {
            var config = ConfigLoader.Load(path);

            var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
            Assert.Contains("deploy.repo must not be empty", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
