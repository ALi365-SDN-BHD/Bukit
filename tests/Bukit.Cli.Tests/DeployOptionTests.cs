using Bukit.Cli.Cli.Binding;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DeployOptionTests
{
    [Fact]
    public void DryRunOption_Set_ReturnsTrue()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--dry-run"] = "true"
            },
            Array.Empty<string>());

        Assert.True(command.GetBool("--dry-run"));
    }

    [Fact]
    public void DryRunOption_NotSet_ReturnsFalse()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        Assert.False(command.GetBool("--dry-run"));
    }

    [Fact]
    public void SkipBuildOption_Set_ReturnsTrue()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--skip-build"] = "true"
            },
            Array.Empty<string>());

        Assert.True(command.GetBool("--skip-build"));
    }

    [Fact]
    public void SkipBuildOption_NotSet_ReturnsFalse()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        Assert.False(command.GetBool("--skip-build"));
    }

    [Fact]
    public void BranchOption_Set_ReturnsValue()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--branch"] = "my-pages"
            },
            Array.Empty<string>());

        Assert.Equal("my-pages", command.GetString("--branch"));
    }

    [Fact]
    public void BranchOption_NotSet_ReturnsNull()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        Assert.Null(command.GetString("--branch"));
    }

    [Fact]
    public void MessageOption_Set_ReturnsValue()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--message"] = "release v1.0"
            },
            Array.Empty<string>());

        Assert.Equal("release v1.0", command.GetString("--message"));
    }

    [Fact]
    public void MessageOption_NotSet_ReturnsNull()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        Assert.Null(command.GetString("--message"));
    }

    [Fact]
    public void CiOption_Set_ReturnsTrue()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--ci"] = "true"
            },
            Array.Empty<string>());

        Assert.True(command.GetBool("--ci"));
    }

    [Fact]
    public void CiOption_NotSet_ReturnsFalse()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        Assert.False(command.GetBool("--ci"));
    }

    [Fact]
    public void OutputOption_Set_ReturnsValue()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--output"] = "_site"
            },
            Array.Empty<string>());

        Assert.Equal("_site", command.GetString("--output"));
    }

    [Fact]
    public void BaseUrlOption_Set_ReturnsValue()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--base-url"] = "/myrepo"
            },
            Array.Empty<string>());

        Assert.Equal("/myrepo", command.GetString("--base-url"));
    }

    [Fact]
    public void SiteUrlOption_Set_ReturnsValue()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--site-url"] = "https://example.com"
            },
            Array.Empty<string>());

        Assert.Equal("https://example.com", command.GetString("--site-url"));
    }
}
