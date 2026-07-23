using Bukit.Config;
using Bukit.Engine.Plugins;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PluginExecutionPolicyTests
{
    [Fact]
    public void From_DefaultsToStrictFailAndAllPluginsEnabled()
    {
        var policy = PluginExecutionPolicy.From(CreateSiteConfig());

        Assert.False(policy.WarnOnPluginFailure);
        Assert.Equal("fail", policy.DeriveConflictPolicy);
        Assert.True(policy.IsPluginEnabled("unknown"));
        Assert.True(policy.IsPluginEnabled(""));
        Assert.True(policy.IsPluginEnabled("   "));
        Assert.True(policy.IsPluginEnabled(null!));
    }

    [Theory]
    [InlineData("warn", true)]
    [InlineData("WARN", true)]
    [InlineData("strict", false)]
    [InlineData(" warn ", false)]
    public void From_MapsWarnModeWithExistingCaseInsensitiveExactSemantics(
        string pluginFailMode,
        bool expected)
    {
        var policy = PluginExecutionPolicy.From(
            CreateSiteConfig(pluginFailMode: pluginFailMode));

        Assert.Equal(expected, policy.WarnOnPluginFailure);
    }

    [Theory]
    [InlineData("  WARN  ", "warn")]
    [InlineData("Last-Wins", "last-wins")]
    [InlineData("", "")]
    public void From_TrimsAndCaseNormalizesDeriveConflictPolicy(
        string deriveConflictPolicy,
        string expected)
    {
        var policy = PluginExecutionPolicy.From(
            CreateSiteConfig(deriveConflictPolicy: deriveConflictPolicy));

        Assert.Equal(expected, policy.DeriveConflictPolicy);
    }

    [Fact]
    public void From_NullDeriveConflictPolicyDefaultsToFail()
    {
        var policy = PluginExecutionPolicy.From(
            CreateSiteConfig(deriveConflictPolicy: null!));

        Assert.Equal("fail", policy.DeriveConflictPolicy);
    }

    [Theory]
    [InlineData("disabled-plugin", false)]
    [InlineData("DISABLED-PLUGIN", false)]
    [InlineData("enabled-plugin", true)]
    [InlineData("ENABLED-PLUGIN", true)]
    [InlineData("unknown", true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    public void IsPluginEnabled_UsesCaseInsensitiveConfiguredLookupAndPreservesDefaults(
        string pluginName,
        bool expected)
    {
        var plugins = new Dictionary<string, PluginToggleConfig>
        {
            ["Disabled-Plugin"] = new() { Enabled = false },
            ["Enabled-Plugin"] = new() { Enabled = true }
        };
        var policy = PluginExecutionPolicy.From(
            CreateSiteConfig(plugins: plugins));

        Assert.Equal(expected, policy.IsPluginEnabled(pluginName));
    }

    private static SiteConfig CreateSiteConfig(
        string pluginFailMode = "strict",
        string deriveConflictPolicy = "fail",
        IReadOnlyDictionary<string, PluginToggleConfig>? plugins = null)
        => new()
        {
            Name = "test",
            Title = "test",
            PluginFailMode = pluginFailMode,
            DeriveConflictPolicy = deriveConflictPolicy,
            Plugins = plugins
        };
}
