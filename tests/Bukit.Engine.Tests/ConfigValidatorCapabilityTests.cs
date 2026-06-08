using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ConfigValidatorCapabilityTests
{
    private static AppConfig ValidConfig(Func<AppConfig, AppConfig>? mutate = null)
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "x",
                Title = "x",
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "./plugin",
                        Hooks = new List<string> { "after-build" },
                        Capabilities = new List<string> { "emit-outputs" }
                    }
                }
            },
            Content = TestContent.Markdown()
        };
        return mutate != null ? mutate(config) : config;
    }

    [Fact]
    public void ValidateExternalPlugins_AcceptValidCapabilities()
    {
        var config = ValidConfig();
        ConfigValidator.Validate(config);
    }

    [Fact]
    public void ValidateExternalPlugins_RejectsInvalidCapability()
    {
        var config = ValidConfig(c => c with
        {
            Site = c.Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["bad"] = new()
                    {
                        Runtime = "process",
                        Entry = "./plugin",
                        Hooks = new List<string> { "after-build" },
                        Capabilities = new List<string> { "read-files" }
                    }
                }
            }
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("capabilities", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateExternalPlugins_NullCapabilities_Throws()
    {
        var config = ValidConfig(c => c with
        {
            Site = c.Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["minimal"] = new()
                    {
                        Runtime = "process",
                        Entry = "./plugin",
                        Hooks = new List<string> { "after-build" }
                    }
                }
            }
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("capabilities is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfigLoader_ReadsCapabilitiesFromYaml()
    {
        var yaml = """
            site:
              name: test-site
              title: Test
              language: en
              baseUrl: /
              externalPlugins:
                sample:
                  runtime: process
                  entry: ./plugin
                  hooks:
                    - after-build
                  capabilities:
                    - emit-outputs
                    - derive-pages
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            """;

        var tmpDir = Path.Combine(Path.GetTempPath(), "bukit-cap-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tmpDir);
            var configPath = Path.Combine(tmpDir, "site.yaml");
            File.WriteAllText(configPath, yaml);

            var config = ConfigLoader.Load(configPath);

            Assert.NotNull(config.Site.ExternalPlugins);
            Assert.True(config.Site.ExternalPlugins.TryGetValue("sample", out var plugin));
            Assert.NotNull(plugin.Capabilities);
            Assert.Equal(2, plugin.Capabilities.Count);
            Assert.Contains("emit-outputs", plugin.Capabilities);
            Assert.Contains("derive-pages", plugin.Capabilities);
        }
        finally
        {
            TestCleanup.DeleteDirectory(tmpDir, recursive: true);
        }
    }
}
