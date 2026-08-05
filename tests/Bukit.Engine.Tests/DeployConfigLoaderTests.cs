using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class DeployConfigLoaderTests
{
    private const string MinimalSiteYaml =
        "site:\n  name: x\n  title: x\ncontent:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\n";

    [Fact]
    public void Load_DeploySectionMissing_ReturnsNull()
    {
        var siteYaml = MinimalSiteYaml;
        var path = WriteTempYaml(siteYaml);
        try
        {
            var config = ConfigLoader.Load(path);
            Assert.Null(config.Deploy);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_DeploySectionEmpty_ThrowsStableNodeKindFailure()
    {
        var siteYaml = MinimalSiteYaml + "deploy:\n";
        var path = WriteTempYaml(siteYaml);
        try
        {
            var exception = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
            Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
            Assert.Contains("deploy", exception.Message, StringComparison.Ordinal);
            Assert.Contains("mapping", exception.Message, StringComparison.Ordinal);
            Assert.Contains("scalar", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_DeployWithOnlyProvider_ReturnsDefaultsForOthers()
    {
        var siteYaml = MinimalSiteYaml + "deploy:\n  provider: github-pages\n";
        var path = WriteTempYaml(siteYaml);
        try
        {
            var config = ConfigLoader.Load(path);
            Assert.NotNull(config.Deploy);
            Assert.Equal("github-pages", config.Deploy!.Provider);
            Assert.Equal("gh-pages", config.Deploy.Branch);
            Assert.Equal("bukit deploy", config.Deploy.Message);
            Assert.Null(config.Deploy.Cname);
            Assert.False(config.Deploy.KeepHistory);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_DeployWithProvider_ReturnsProvider()
    {
        var siteYaml = MinimalSiteYaml + "deploy:\n  provider: github-pages\n";
        var path = WriteTempYaml(siteYaml);
        try
        {
            var config = ConfigLoader.Load(path);
            Assert.NotNull(config.Deploy);
            Assert.Equal("github-pages", config.Deploy!.Provider);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_DeployWithFullConfig_ReturnsAllFields()
    {
        var siteYaml = @"
site:
  name: x
  title: x
content:
  sources:
    - type: markdown
      name: page
      collection: page
      markdown:
        dir: content
deploy:
  provider: github-pages
  branch: pages
  message: custom deploy message
  cname: example.com
  keepHistory: true
";
        var path = WriteTempYaml(siteYaml);
        try
        {
            var config = ConfigLoader.Load(path);
            Assert.NotNull(config.Deploy);
            Assert.Equal("github-pages", config.Deploy!.Provider);
            Assert.Equal("pages", config.Deploy.Branch);
            Assert.Equal("custom deploy message", config.Deploy.Message);
            Assert.Equal("example.com", config.Deploy.Cname);
            Assert.True(config.Deploy.KeepHistory);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_DeployOptions_ThrowsUnknownField()
    {
        var siteYaml = MinimalSiteYaml + """
            deploy:
              provider: github-pages
              options:
                foo: bar
            """;
        var path = WriteTempYaml(siteYaml);
        try
        {
            var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
            Assert.Contains("Unknown config field 'deploy.options'.", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_DeployBranchMissing_UsesDefault()
    {
        var siteYaml = MinimalSiteYaml + "deploy:\n  provider: github-pages\n  message: test\n";
        var path = WriteTempYaml(siteYaml);
        try
        {
            var config = ConfigLoader.Load(path);
            Assert.NotNull(config.Deploy);
            Assert.Equal("gh-pages", config.Deploy!.Branch);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_DeployMessageMissing_UsesDefault()
    {
        var siteYaml = MinimalSiteYaml + "deploy:\n  provider: github-pages\n  branch: pages\n";
        var path = WriteTempYaml(siteYaml);
        try
        {
            var config = ConfigLoader.Load(path);
            Assert.NotNull(config.Deploy);
            Assert.Equal("bukit deploy", config.Deploy!.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempYaml(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-deploy-config", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "site.yaml");
        File.WriteAllText(path, content.TrimStart('\n'));
        return path;
    }
}
