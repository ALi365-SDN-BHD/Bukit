using System.Net;
using System.Net.Sockets;
using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class SsrfGuardHostTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("2130706433", true)]
    [InlineData("0x7f000001", true)]
    [InlineData("::ffff:10.0.0.1", true)]
    [InlineData("8.8.8.8", false)]
    public async Task IsPrivateHostAsync_IpLiteral_DoesNotResolveDns(string host, bool expected)
    {
        var result = await SsrfGuard.IsPrivateHostAsync(host,
            (_, _) => throw new InvalidOperationException("Literal IP must not use DNS."),
            CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task IsPrivateHostAsync_PublicDns_PassesHostAndTokenAndAllowsAllPublicResults()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var result = await SsrfGuard.IsPrivateHostAsync("public.example", (host, token) =>
        {
            calls++;
            Assert.Equal("public.example", host);
            Assert.Equal(cancellation.Token, token);
            return Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("2606:4700:4700::1111") });
        }, cancellation.Token);

        Assert.False(result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task IsPrivateHostAsync_InvalidHostResolverArgument_FailsClosed()
    {
        Assert.True(await SsrfGuard.IsPrivateHostAsync("invalid/host",
            (_, _) => Task.FromException<IPAddress[]>(new ArgumentException("Invalid host.")),
            CancellationToken.None));
    }

    [Fact]
    public async Task IsPrivateHostAsync_CanceledBeforeHostnameResolution_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SsrfGuard.IsPrivateHostAsync("example.com", cancellation.Token));
    }

    [Fact]
    public async Task IsPrivateHostAsync_CanceledBeforeLiteralClassification_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SsrfGuard.IsPrivateHostAsync("127.0.0.1", cancellation.Token));
    }

    [Fact]
    public async Task IsPrivateHostAsync_MixedAddresses_TreatsHostAsPrivate()
    {
        var result = await SsrfGuard.IsPrivateHostAsync(
            "example.com",
            (_, _) => Task.FromResult(new[]
            {
                IPAddress.Parse("8.8.8.8"),
                IPAddress.Loopback
            }),
            CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task IsPrivateHostAsync_ResolverFailure_TreatsHostAsUnsafe()
    {
        var result = await SsrfGuard.IsPrivateHostAsync(
            "unresolvable.example",
            (_, _) => Task.FromException<IPAddress[]>(
                new SocketException((int)SocketError.HostNotFound)),
            CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task IsPrivateHostAsync_EmptyResolution_TreatsHostAsUnsafe()
    {
        var result = await SsrfGuard.IsPrivateHostAsync(
            "empty.example",
            (_, _) => Task.FromResult(Array.Empty<IPAddress>()),
            CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task IsPrivateHostAsync_CancellationDuringResolution_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SsrfGuard.IsPrivateHostAsync(
                "example.com",
                (_, token) =>
                {
                    cancellation.Cancel();
                    return Task.FromException<IPAddress[]>(new OperationCanceledException(token));
                },
                cancellation.Token));
    }

    [Fact]
    public async Task IsPrivateHostAsync_UnexpectedResolverFailure_PropagatesFailure()
    {
        var expected = new InvalidOperationException("resolver invariant failed");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SsrfGuard.IsPrivateHostAsync(
                "example.com",
                (_, _) => Task.FromException<IPAddress[]>(expected),
                CancellationToken.None));

        Assert.Same(expected, exception);
    }
}
