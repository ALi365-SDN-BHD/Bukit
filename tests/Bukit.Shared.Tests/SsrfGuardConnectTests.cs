using System.Net;
using System.Net.Sockets;
using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class SsrfGuardConnectTests
{
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
            CancellationToken.None,
            (_, _) => Task.FromResult(new[] { first, second }),
            (address, _, _) =>
            {
                attempted.Add(address);
                return attempted.Count == 1
                    ? ValueTask.FromException<Stream>(
                        new SocketException((int)SocketError.HostUnreachable))
                    : ValueTask.FromResult<Stream>(expected);
            });

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
            CancellationToken.None,
            (_, _) => Task.FromResult(new[] { privateAddress, publicAddress }),
            (address, _, _) =>
            {
                attempted.Add(address);
                return ValueTask.FromResult<Stream>(expected);
            });

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
                cancellation.Token,
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
                }));

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
                CancellationToken.None,
                (_, _) => Task.FromResult(new[]
                {
                    IPAddress.Parse("8.8.8.8"),
                    IPAddress.Parse("1.1.1.1")
                }),
                (_, _, _) => ++attempts == 1
                    ? ValueTask.FromException<Stream>(firstFailure)
                    : ValueTask.FromException<Stream>(lastFailure)));

        Assert.Equal(2, attempts);
        Assert.Same(lastFailure, exception.InnerException);
    }
}
