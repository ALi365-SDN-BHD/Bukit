using Bukit.Cli.Commands.New;
using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class StarterSiteScaffoldTests : IDisposable
{
    private readonly string _root;

    public StarterSiteScaffoldTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "bukit-starter-site-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_root, recursive: true);
    }

    [Fact]
    public void Resources_LoadTextAndBinary()
    {
        Assert.Contains("<!DOCTYPE html>", StarterSiteScaffold.GetThemeText("BaseLayout.html"));
        Assert.NotEmpty(StarterSiteScaffold.GetThemeBytes("og-default.png"));
        Assert.Throws<InvalidOperationException>(
            () => StarterSiteScaffold.GetThemeText("missing.html"));
    }

    [Fact]
    public void Scaffold_CopiesThemeContentAndGitignore()
    {
        var target = Path.Combine(_root, "site");

        StarterSiteScaffold.Create(target, "My Site", "my-site", "en-US");

        Assert.True(File.Exists(Path.Combine(target, "themes/starter/theme.yaml")));
        Assert.True(File.Exists(Path.Combine(target, "themes/starter/LICENSE")));
        Assert.True(File.Exists(Path.Combine(target, "themes/starter/assets/og-default.png")));
        Assert.True(File.Exists(Path.Combine(target, "themes/starter/static/robots.txt")));
        Assert.True(File.Exists(Path.Combine(target, "content/posts/welcome.md")));
        Assert.True(File.Exists(Path.Combine(target, "content/pages/about.md")));
        Assert.True(File.Exists(Path.Combine(target, "data/features-10.md")));
        Assert.True(File.Exists(Path.Combine(target, "site.yaml")));
    }

    [Theory]
    [InlineData("a: 1\n...\n", "a: 1\n")]
    [InlineData("a: 1\r\n...\r\n", "a: 1\r\n")]
    [InlineData("a: one...\n", "a: one...\n")]
    [InlineData("a: 1\n", "a: 1\n")]
    public void StripTrailingDocumentEndMarker_RemovesOnlyTheTrailingLine(string input, string expected)
    {
        Assert.Equal(expected, YamlDocumentText.StripTrailingDocumentEndMarker(input));
    }

    [Fact]
    public void Scaffold_SiteYamlIsOneDocumentAndAppendedSectionsAreValidated()
    {
        var target = Path.Combine(_root, "site-yaml");

        StarterSiteScaffold.Create(target, "My Site", "my-site", "en-US");

        var path = Path.Combine(target, "site.yaml");
        var yaml = File.ReadAllText(path).ReplaceLineEndings("\n");
        Assert.DoesNotContain("\n...\n", yaml, StringComparison.Ordinal);

        File.AppendAllText(path, "deploy:\n  provider: github-pages\n  repo: someone/blog\n");
        var config = ConfigLoader.Load(path);
        Assert.Equal("someone/blog", config.Deploy!.Repo);

        File.AppendAllText(path, "notABukitKey: 1\n");
        var exception = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
    }

    [Fact]
    public void Scaffold_RollsBackWhenResourceMissing()
    {
        var target = Path.Combine(_root, "rollback");
        StarterFile[] entries =
        [
            new("HeaderPartial.html", "themes/starter/layouts/partials/header.html"),
            new("missing.html", "missing.txt")
        ];

        Assert.Throws<InvalidOperationException>(
            () => StarterSiteScaffold.Create(target, "My Site", "my-site", "en-US", entries));

        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Scaffold_ExistingEmptyTargetStaysEmptyOnFailure()
    {
        var target = Path.Combine(_root, "empty");
        Directory.CreateDirectory(target);
        StarterFile[] entries =
        [
            new("HeaderPartial.html", "themes/starter/layouts/partials/header.html"),
            new("missing.html", "missing.txt")
        ];

        Assert.Throws<InvalidOperationException>(
            () => StarterSiteScaffold.Create(target, "My Site", "my-site", "en-US", entries));

        Assert.True(Directory.Exists(target));
        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

    [Fact]
    public void Scaffold_RefusesNonEmptyTarget()
    {
        var target = Path.Combine(_root, "non-empty");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.txt"), "keep");

        Assert.Throws<CommandArgumentException>(
            () => StarterSiteScaffold.Create(target, "My Site", "my-site", "en-US"));

        Assert.Equal("keep", File.ReadAllText(Path.Combine(target, "keep.txt")));
    }

    [Theory]
    [InlineData("zh", true)]
    [InlineData("zh-CN", true)]
    [InlineData("ZH-hans-sg", true)]
    [InlineData("zh-TW", false)]
    [InlineData("zh-Hant", false)]
    [InlineData("zh-HK", false)]
    [InlineData("zh-MO", false)]
    [InlineData("zh-SG", true)]
    [InlineData("zh-Hans", true)]
    [InlineData("zh-Hans-HK", true)]
    [InlineData("zh-Hant-CN", false)]
    [InlineData("en-US", false)]
    [InlineData("zha", false)]
    public void IsSimplifiedChinese_MatchesPrimaryAndScriptSubtags(string language, bool expected)
    {
        Assert.Equal(expected, StarterSiteScaffold.IsSimplifiedChinese(language));
    }
}
