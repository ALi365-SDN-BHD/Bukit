using Bukit.PluginHost;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class PluginSecretMaskerTests
{
    [Theory]
    [InlineData("NOTION_TOKEN")]
    [InlineData("MY_API_KEY")]
    [InlineData("DB_PASSWORD")]
    [InlineData("token")]
    [InlineData("CLIENT_SECRET")]
    [InlineData("COOKIE")]
    [InlineData("HTTP_COOKIE")]
    [InlineData("AUTHORIZATION")]
    [InlineData("HTTP_AUTHORIZATION")]
    [InlineData("cookie")]
    public void MaskValue_MasksSecretKeyFragments(string key)
    {
        Assert.Equal("***", PluginSecretMasker.MaskValue(key, "secret-value"));
    }

    [Theory]
    [InlineData("PUBLIC_VALUE")]
    [InlineData("PLUGIN_NAME")]
    public void MaskValue_LeavesNonSecretKeysUnchanged(string key)
    {
        Assert.Equal("visible", PluginSecretMasker.MaskValue(key, "visible"));
    }

    [Fact]
    public void MaskValue_ReturnsEmptyForNullSecret()
    {
        Assert.Equal(string.Empty, PluginSecretMasker.MaskValue("NOTION_TOKEN", null));
        Assert.Equal(string.Empty, PluginSecretMasker.MaskValue("NOTION_TOKEN", string.Empty));
    }

    [Fact]
    public void MaskEnvironment_MasksOnlySecretKeys()
    {
        IReadOnlyDictionary<string, string> masked = PluginSecretMasker.MaskEnvironment(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["NOTION_TOKEN"] = "secret-token",
                ["API_KEY"] = "secret-key",
                ["COOKIE"] = "session-cookie",
                ["HTTP_AUTHORIZATION"] = "Bearer secret",
                ["PUBLIC_VALUE"] = "visible"
            });

        Assert.Equal("***", masked["NOTION_TOKEN"]);
        Assert.Equal("***", masked["API_KEY"]);
        Assert.Equal("***", masked["COOKIE"]);
        Assert.Equal("***", masked["HTTP_AUTHORIZATION"]);
        Assert.Equal("visible", masked["PUBLIC_VALUE"]);
    }

    [Fact]
    public void MaskText_ReplacesLongestSecretFirst()
    {
        string masked = PluginSecretMasker.MaskText(
            "secret-token and secret",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["NOTION_TOKEN"] = "secret-token",
                ["API_KEY"] = "secret"
            });

        Assert.Equal("*** and ***", masked);
    }

    [Fact]
    public void MaskText_LeavesTextWhenEnvironmentIsEmpty()
    {
        Assert.Equal("no secrets", PluginSecretMasker.MaskText("no secrets", new Dictionary<string, string>()));
    }

    [Fact]
    public void MaskText_MasksAuthorizedValueRegardlessOfKey()
    {
        string masked = PluginSecretMasker.MaskText(
            "PATH=/usr/local/bin",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PATH"] = "/usr/local/bin"
            });

        Assert.Equal("PATH=***", masked);
    }

    [Fact]
    public void MaskText_MasksShortAuthorizedValue()
    {
        string masked = PluginSecretMasker.MaskText(
            "LANG=C",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["LANG"] = "C"
            });

        Assert.Equal("LANG=***", masked);
    }

    [Fact]
    public void MaskText_LeavesValuesNotPresentInAuthorizedEnvironment()
    {
        string masked = PluginSecretMasker.MaskText(
            "public=/usr/local/bin",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["NOTION_TOKEN"] = "secret-token"
            });

        Assert.Equal("public=/usr/local/bin", masked);
    }
}
