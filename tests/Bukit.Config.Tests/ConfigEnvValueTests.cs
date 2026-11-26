using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigEnvValueTests
{
    [Theory]
    [InlineData("${env:RELEASE_ID}", "r42")]
    [InlineData("release-${env:RELEASE_ID}", "release-r42")]
    [InlineData("published", "published")]
    public void Resolve_ReplacesReferences(string value, string expected)
        => Assert.Equal(expected, ConfigEnvValue.Resolve(value, "x", name => name == "RELEASE_ID" ? " r42 " : null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_UnsetOrEmptyVariable_Throws(string? variable)
    {
        var ex = Assert.Throws<ConfigException>(() => ConfigEnvValue.Resolve("${env:RELEASE_ID}", "cloudbase.filter.value", _ => variable));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
        Assert.Equal("cloudbase.filter.value reads environment variable RELEASE_ID, which is not set or is empty.", ex.Message);
    }

    [Theory]
    [InlineData("${env:RELEASE_ID}", false)]
    [InlineData("a${env:A}b${env:B_2}", false)]
    [InlineData("plain", false)]
    [InlineData("${RELEASE_ID}", true)]
    [InlineData("${env:1BAD}", true)]
    [InlineData("${env:RELEASE_ID", true)]
    public void FindSyntaxError_FlagsMalformedReferences(string value, bool invalid)
        => Assert.Equal(invalid, ConfigEnvValue.FindSyntaxError(value) is not null);
}
