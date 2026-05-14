using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class DeployConfigTests
{
    private static AppConfig ValidConfig(Func<AppConfig, AppConfig>? mutate = null)
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig() }
        };
        return mutate != null ? mutate(config) : config;
    }

    [Fact]
    public void Validate_DefaultDeployConfig_Passes()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig() });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_DeployWithGithubPagesProvider_Passes()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Provider = "github-pages" } });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_DeployWithFullConfig_Passes()
    {
        var config = ValidConfig(c => c with
        {
            Deploy = new DeployConfig
            {
                Provider = "github-pages",
                Branch = "pages",
                Message = "deploy v2",
                Cname = "example.com"
            }
        });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_DeployWithUnknownProvider_Throws()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Provider = "ftp" } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("deploy.provider", ex.Message);
    }

    [Fact]
    public void Validate_DeployWithBranchSlash_Throws()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Branch = "feature/pages" } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("deploy.branch", ex.Message);
    }

    [Fact]
    public void Validate_DeployWithInvalidDomain_Throws()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Cname = "not a domain!" } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("deploy.cname", ex.Message);
    }

    [Fact]
    public void Validate_DeployWithLongMessage_Throws()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Message = new string('x', 5000) } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("deploy.message", ex.Message);
    }

    [Fact]
    public void Validate_DeployBranchEmpty_Passes()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Branch = "" } });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_DeployBranchLetters_Passes()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Branch = "gh-pages" } });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_DeployCnameCustomDomain_Passes()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Cname = "blog.example.co.uk" } });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_DeployCnameSubdomain_Passes()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Cname = "www.example.com" } });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }
}
