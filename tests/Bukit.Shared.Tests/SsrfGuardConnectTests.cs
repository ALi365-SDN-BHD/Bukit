using System.Net;
using System.Net.Sockets;
using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class SsrfGuardConnectTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SafeConnect_EmptyOrPrivateResolution_NeverConnects(bool hasAddresses)
    {
        var attempts = 0;
        var addresses = hasAddresses
            ? new[] { IPAddress.Loopback, IPAddress.Parse("::ffff:10.0.0.1"), IPAddress.IPv6Loopback }
            : Array.Empty<IPAddress>();

        var error = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await SsrfGuard.SsrfSafeConnectAsync("private.example", 443,
                (_, _) => Task.FromResult(addresses),
                (_, _, _) =>
                {
                    attempts++;
                    throw new InvalidOperationException("Must not connect.");
                }, CancellationToken.None));

        Assert.Equal(0, attempts);
        Assert.Contains("SSRF blocked", error.Message);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task SafeConnect_CanceledBeforeResolution_DoesNotResolveOrConnect()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await SsrfGuard.SsrfSafeConnectAsync("public.example", 443,
                (_, _) => throw new InvalidOperationException("Must not resolve."),
                (_, _, _) => throw new InvalidOperationException("Must not connect."),
                cancellation.Token));
    }

    [Fact]
    public async Task SafeConnect_ResolverFailure_PropagatesWithoutConnecting()
    {
        var expected = new SocketException((int)SocketError.HostNotFound);
        var error = await Assert.ThrowsAsync<SocketException>(async () =>
            await SsrfGuard.SsrfSafeConnectAsync("missing.example", 443,
                (_, _) => Task.FromException<IPAddress[]>(expected),
                (_, _, _) => throw new InvalidOperationException("Must not connect."),
                CancellationToken.None));

        Assert.Same(expected, error);
    }

    [Fact]
    public async Task SafeConnect_EachConnectionResolvesAgain_RejectsReboundPrivateAddress()
    {
        using var cancellation = new CancellationTokenSource();
        using var expected = new MemoryStream([1]);
        var resolutions = 0;
        var connections = 0;
        Task<IPAddress[]> Resolve(string host, CancellationToken token)
        {
            Assert.Equal("rebound.example", host);
            Assert.Equal(cancellation.Token, token);
            return Task.FromResult(new[] { ++resolutions == 1 ? IPAddress.Parse("8.8.8.8") : IPAddress.Loopback });
        }
        ValueTask<Stream> Connect(IPAddress address, int port, CancellationToken token)
        {
            connections++;
            Assert.Equal(IPAddress.Parse("8.8.8.8"), address);
            Assert.Equal(8443, port);
            Assert.Equal(cancellation.Token, token);
            return ValueTask.FromResult<Stream>(expected);
        }

        Assert.Same(expected, await SsrfGuard.SsrfSafeConnectAsync(
            "rebound.example", 8443, Resolve, Connect, cancellation.Token));
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await SsrfGuard.SsrfSafeConnectAsync("rebound.example", 8443, Resolve, Connect, cancellation.Token));

        Assert.Equal(2, resolutions);
        Assert.Equal(1, connections);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("http")]
    [InlineData("unexpected")]
    public async Task SafeConnect_OnlyExpectedNetworkFailuresTryNextAddress(string failureKind)
    {
        Exception failure = failureKind switch
        {
            "io" => new IOException("connection failed"),
            "http" => new HttpRequestException("connection failed"),
            _ => new InvalidOperationException("connector invariant failed")
        };
        using var expected = new MemoryStream();
        var attempts = 0;
        async Task<Stream> ConnectAsync() => await SsrfGuard.SsrfSafeConnectAsync("public.example", 443,
            (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("1.1.1.1") }),
            (_, _, _) => ++attempts == 1
                ? ValueTask.FromException<Stream>(failure)
                : ValueTask.FromResult<Stream>(expected),
            CancellationToken.None);

        if (failureKind == "unexpected")
        {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(ConnectAsync));
            Assert.Equal(1, attempts);
        }
        else
        {
            Assert.Same(expected, await ConnectAsync());
            Assert.Equal(2, attempts);
        }
    }

    [Fact]
    public async Task SafeConnect_FirstPublicAddressFails_TriesNextPublicAddress()
    {
        var first = IPAddress.Parse("8.8.8.8");
        var second = IPAddress.Parse("1.1.1.1");
        var attempted = new List<IPAddress>();
        using var expected = new MemoryStream([1, 2, 3]);

        var stream = await SsrfGuard.SsrfSafeConnectAsync(
            "example.com",
            443,
            (_, _) => Task.FromResult(new[] { first, second }),
            (address, _, _) =>
            {
                attempted.Add(address);
                return attempted.Count == 1
                    ? ValueTask.FromException<Stream>(
                        new SocketException((int)SocketError.HostUnreachable))
                    : ValueTask.FromResult<Stream>(expected);
            },
            CancellationToken.None);

        Assert.Same(expected, stream);
        Assert.Equal(new[] { first, second }, attempted);
    }

    [Fact]
    public async Task SafeConnect_MixedAddresses_NeverAttemptsPrivateAddress()
    {
        var privateAddress = IPAddress.Loopback;
        var publicAddress = IPAddress.Parse("8.8.8.8");
        var attempted = new List<IPAddress>();
        using var expected = new MemoryStream([1]);

        var stream = await SsrfGuard.SsrfSafeConnectAsync(
            "example.com",
            443,
            (_, _) => Task.FromResult(new[] { privateAddress, publicAddress }),
            (address, _, _) =>
            {
                attempted.Add(address);
                return ValueTask.FromResult<Stream>(expected);
            },
            CancellationToken.None);

        Assert.Same(expected, stream);
        Assert.Equal(new[] { publicAddress }, attempted);
    }

    [Fact]
    public async Task SafeConnect_CancellationDuringAttempt_DoesNotTryLaterAddress()
    {
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await SsrfGuard.SsrfSafeConnectAsync(
                "example.com",
                443,
                (_, _) => Task.FromResult(new[]
                {
                    IPAddress.Parse("8.8.8.8"),
                    IPAddress.Parse("1.1.1.1")
                }),
                (_, _, token) =>
                {
                    attempts++;
                    cancellation.Cancel();
                    return ValueTask.FromException<Stream>(new OperationCanceledException(token));
                },
                cancellation.Token));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task SafeConnect_AllPublicAddressesFail_PreservesLastFailure()
    {
        var firstFailure = new SocketException((int)SocketError.HostUnreachable);
        var lastFailure = new IOException("second address failed");
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await SsrfGuard.SsrfSafeConnectAsync(
                "example.com",
                443,
                (_, _) => Task.FromResult(new[]
                {
                    IPAddress.Parse("8.8.8.8"),
                    IPAddress.Parse("1.1.1.1")
                }),
                (_, _, _) => ++attempts == 1
                    ? ValueTask.FromException<Stream>(firstFailure)
                    : ValueTask.FromException<Stream>(lastFailure),
                CancellationToken.None));

        Assert.Equal(2, attempts);
        Assert.Same(lastFailure, exception.InnerException);
    }
}
