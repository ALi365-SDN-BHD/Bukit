using Bukit.Cli.Commands;
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

    [Fact]
    public void Scaffold_RollsBackWhenResourceMissing()
    {
        var target = Path.Combine(_root, "rollback");
        var entries = new[]
        {
            new StarterFile("HeaderPartial.html", "themes/starter/layouts/partials/header.html"),
            new StarterFile("missing.html", "missing.txt")
        };

        Assert.Throws<InvalidOperationException>(
            () => StarterSiteScaffold.Create(target, "My Site", "my-site", "en-US", entries));

        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Scaffold_ExistingEmptyTargetStaysEmptyOnFailure()
    {
        var target = Path.Combine(_root, "empty");
        Directory.CreateDirectory(target);
        var entries = new[]
        {
            new StarterFile("HeaderPartial.html", "themes/starter/layouts/partials/header.html"),
            new StarterFile("missing.html", "missing.txt")
        };

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
}
