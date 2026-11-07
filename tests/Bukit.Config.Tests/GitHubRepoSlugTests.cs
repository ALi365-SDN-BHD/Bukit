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
        "ali/site\n",
        "ali/site\r\n",
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void SchemaPattern_MatchesRuntimeValidator(string value)
    {
        // Full-length match: in .NET (unlike ECMA-262) '$' also matches before a trailing '\n'.
        var match = Regex.Match(value, GitHubRepoSlug.Pattern);
        var schemaMatch = match.Success && match.Length == value.Length;

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
