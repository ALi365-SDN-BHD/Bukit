using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Config;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class GitHubRepoSlugTests
{
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
    public void SchemaPattern_MatchesRuntimeValidator(string value)
    {
        var schemaMatch = Regex.IsMatch(value, GitHubRepoSlug.Pattern);

        Assert.Equal(GitHubRepoSlug.IsValid(value), schemaMatch);
    }

    [Fact]
    public void GeneratedSchema_UsesSharedPattern()
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var pattern = schema.RootElement
            .GetProperty("properties").GetProperty("deploy")
            .GetProperty("properties").GetProperty("repo")
            .GetProperty("pattern").GetString();

        Assert.Equal(GitHubRepoSlug.Pattern, pattern);
    }
}
