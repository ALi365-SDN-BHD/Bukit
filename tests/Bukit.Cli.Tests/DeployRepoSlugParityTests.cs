using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Cli.Deploy;
using Bukit.Config;
using Xunit;

namespace Bukit.Cli.Tests;

// deploy --repo keeps its own owner/name validator because Bukit.Cli may not link Bukit.Config sources.
// This keeps it in step with deploy.repo through the public config schema pattern.
public sealed class DeployRepoSlugParityTests
{
    private static readonly Lazy<string> SchemaPattern = new(() =>
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        return schema.RootElement
            .GetProperty("properties").GetProperty("deploy")
            .GetProperty("properties").GetProperty("repo")
            .GetProperty("pattern").GetString()!;
    });

    public static TheoryData<string> Samples() => new()
    {
        "ALi365-SDN-BHD/site",
        "ali/docs.v2",
        "ali/.github",
        "a/b",
        "ali/site.github.io",
        "ali/site.gitx",
        "ali/site.git",
        "ali/site.GIT",
        "ali/.",
        "ali/..",
        "ali/...",
        "ali/bad name",
        "ali",
        "ali/site/extra",
        "-ali/site",
        "/site",
        "ali/",
        "ali_x/site",
        "https://github.com/ali/site",
        "git@github.com:ali/site.git",
        new string('a', 39) + "/site",
        new string('a', 40) + "/site",
        "ali/" + new string('n', 100),
        "ali/" + new string('n', 101),
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void CliRepoValidator_MatchesConfigSchemaPattern(string value)
    {
        var cliAccepts = GitHubPagesDeployProvider.TryNormalizeRepo(value, out _, out _);

        Assert.Equal(Regex.IsMatch(value, SchemaPattern.Value), cliAccepts);
    }
}
