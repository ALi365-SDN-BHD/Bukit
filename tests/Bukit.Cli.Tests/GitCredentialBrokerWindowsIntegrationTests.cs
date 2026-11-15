using System.Runtime.Versioning;
using Xunit;

namespace Bukit.Cli.Tests;

/// <summary>
/// Plan 3.7 tests 10 and 12 on Windows: a real authenticated push through real Git for Windows,
/// its MSYS sh, the real broker on a named pipe and the real <c>__git-credential</c> helper
/// (run as <c>dotnet bukit.dll</c>), against <c>git http-backend</c> behind Basic auth on
/// 127.0.0.1. The peer checks, including the expected shell, are the production ones.
/// </summary>
public sealed class GitCredentialBrokerWindowsIntegrationTests
{
    [SupportedOSPlatform("windows")]
    [WindowsFact]
    public async Task RealPush_WindowsCorrectTokenPushesThroughBroker()
    {
        await using var scope = await BrokerDeployScope.CreateAsync();

        var result = await scope.DeployAsync();

        Assert.True(result.Success, result.Error);
        Assert.Equal("new page", await scope.ReadRemoteFileAsync("index.html"));
        Assert.True(scope.Server.Unauthorized >= 1, "git must first be challenged with 401.");
        Assert.True(scope.Server.Authorized >= 3, "ls-remote, fetch and push must all authenticate.");
        Assert.Empty(scope.Server.WrongPasswords);
    }

    [SupportedOSPlatform("windows")]
    [WindowsFact]
    public async Task RealPush_WindowsWrongTokenReportsAuthenticationFailure()
    {
        await using var scope = await BrokerDeployScope.CreateAsync(token: BrokerDeployScope.WrongToken);

        var result = await scope.DeployAsync();

        Assert.False(result.Success);
        Assert.DoesNotContain(BrokerDeployScope.WrongToken, result.Error, StringComparison.Ordinal);
        // The helper was granted and git sent the wrong token: the broker chain worked.
        Assert.Contains(BrokerDeployScope.WrongToken, scope.Server.WrongPasswords);
        Assert.Equal(0, scope.Server.Authorized);
        Assert.Equal("old page", await scope.ReadRemoteFileAsync("old.html"));
    }
}
