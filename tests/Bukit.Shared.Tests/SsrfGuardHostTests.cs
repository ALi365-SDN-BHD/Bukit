using System.Net;
using System.Net.Sockets;
using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class SsrfGuardHostTests
{
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
