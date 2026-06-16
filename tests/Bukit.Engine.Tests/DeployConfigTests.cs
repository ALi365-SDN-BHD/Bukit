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
            Content = TestContent.Markdown()
        };
        return mutate != null ? mutate(config) : config;
    }

    [Fact]
    public void Validate_DeployWithoutProvider_Throws()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig() });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("deploy.provider is required when deploy section is present.", ex.Message, StringComparison.Ordinal);
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
    public void Validate_DeployWithBranchLeadingDash_Throws()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Provider = "github-pages", Branch = "-feature" } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("deploy.branch", ex.Message);
    }

    [Fact]
    public void Validate_DeployWithBranchSlashPath_Throws()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Provider = "github-pages", Branch = "/feature" } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("deploy.branch", ex.Message);
    }

    [Fact]
    public void Validate_DeployWithBranchAllowsSlashPath()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Provider = "github-pages", Branch = "feature/pages" } });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_DeployWithInvalidDomain_Throws()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Provider = "github-pages", Cname = "not a domain!" } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("deploy.cname", ex.Message);
    }

    [Fact]
    public void Validate_DeployWithLongMessage_Throws()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Provider = "github-pages", Message = new string('x', 5000) } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("deploy.message", ex.Message);
    }

    [Fact]
    public void Validate_DeployBranchEmpty_Passes()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Provider = "github-pages", Branch = "" } });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_DeployBranchLetters_Passes()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Provider = "github-pages", Branch = "gh-pages" } });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_DeployCnameCustomDomain_Passes()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Provider = "github-pages", Cname = "blog.example.co.uk" } });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_DeployCnameSubdomain_Passes()
    {
        var config = ValidConfig(c => c with { Deploy = new DeployConfig { Provider = "github-pages", Cname = "www.example.com" } });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }
}
