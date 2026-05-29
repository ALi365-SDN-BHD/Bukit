using System.Net.Http;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public class SsrfGuardIntegrationTests
{
    [Fact]
    public async Task HttpClient_WithSsrfGuard_RejectsPrivateNetworkConnection()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = SsrfGuard.SsrfSafeConnectAsync
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await client.GetAsync("http://127.0.0.1:1/");
        });
    }
}
