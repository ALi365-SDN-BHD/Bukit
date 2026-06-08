using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PluginOptionsParsingTests
{
    [Fact]
    public void Load_PluginOptionsMapping_ParsesEnabledAndOptions()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var configPath = Path.Combine(root, "site.yaml");
        File.WriteAllText(configPath, """
                                   site:
                                     name: demo
                                     title: Demo
                                     plugins:
                                       path-report:
                                         enabled: true
                                         options:
                                           sourceNames: [posts]
                                           retry:
                                             maxAttempts: 5
                                   content:
                                     sources:
                                       - type: markdown
                                         name: page
                                         collection: page
                                         markdown:
                                           dir: content
                                     markdown:
                                       dir: content
                                   """);

        var config = ConfigLoader.Load(configPath);
        Assert.NotNull(config.Site.Plugins);
        Assert.True(config.Site.Plugins!.TryGetValue("path-report", out var plugin));
        Assert.NotNull(plugin);
        Assert.True(plugin.Enabled);
        Assert.NotNull(plugin.Options);
        Assert.True(plugin.Options!.TryGetValue("sourceNames", out var sourceNamesObj));
        var sourceNames = Assert.IsType<List<object>>(sourceNamesObj);
        Assert.Single(sourceNames);
        Assert.Equal("posts", Assert.IsType<string>(sourceNames[0]));
        Assert.True(plugin.Options.TryGetValue("retry", out var retryObj));
        var retry = Assert.IsType<Dictionary<string, object>>(retryObj);
        Assert.Equal("5", Assert.IsType<string>(retry["maxAttempts"]));
    }

    [Fact]
    public void Load_PluginBooleanStyle_RemainsCompatible()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var configPath = Path.Combine(root, "site.yaml");
        File.WriteAllText(configPath, """
                                   site:
                                     name: demo
                                     title: Demo
                                     plugins:
                                       sitemap: false
                                   content:
                                     sources:
                                       - type: markdown
                                         name: page
                                         collection: page
                                         markdown:
                                           dir: content
                                     markdown:
                                       dir: content
                                   """);

        var config = ConfigLoader.Load(configPath);
        Assert.NotNull(config.Site.Plugins);
        Assert.True(config.Site.Plugins!.TryGetValue("sitemap", out var plugin));
        Assert.NotNull(plugin);
        Assert.False(plugin.Enabled);
        Assert.Null(plugin.Options);
    }

    [Fact]
    public void Load_PluginOptionsScalar_ThrowsConfigException()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var configPath = Path.Combine(root, "site.yaml");
        File.WriteAllText(configPath, """
                                   site:
                                     name: demo
                                     title: Demo
                                     plugins:
                                       path-report:
                                         enabled: true
                                         options: bad
                                   content:
                                     sources:
                                       - type: markdown
                                         name: page
                                         collection: page
                                         markdown:
                                           dir: content
                                     markdown:
                                       dir: content
                                   """);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(configPath));
        Assert.Contains("site.plugins.path-report.options must be a mapping", ex.Message);
    }
}
