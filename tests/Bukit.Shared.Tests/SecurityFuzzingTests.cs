using System.Net;
using Xunit;
using Bukit.Shared;

namespace Bukit.Shared.Tests;

/// <summary>
/// Deterministic SSRF address and factory regression cases; no public DNS or sockets.
/// </summary>
public sealed class SecurityFuzzingTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]  // AWS metadata
    [InlineData("0.0.0.0")]
    [InlineData("fe80::1")]           // IPv6 link-local
    [InlineData("fc00::1")]           // IPv6 unique-local
    public async Task SsrfGuard_PrivateAddresses_Detected(string address)
    {
        var result = await SsrfGuard.IsPrivateHostAsync(address, CancellationToken.None);
        Assert.True(result, $"Expected {address} to be detected as private");
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("2001:4860:4860::8888")]
    [InlineData("2606:4700:4700::1111")]
    public async Task SsrfGuard_PublicAddresses_Allowed(string address)
    {
        var result = await SsrfGuard.IsPrivateHostAsync(address, CancellationToken.None);
        Assert.False(result, $"Expected {address} to be allowed as public");
    }

    // ── SSRF payload in HTTP requests ───────────────────────────────

    [Theory]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://localhost/secret")]
    [InlineData("http://[::1]/admin")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://10.0.0.1/internal")]
    [InlineData("http://192.168.1.1/router")]
    public async Task SsrfGuard_PrivateUrls_IdentifiedAsPrivate(string url)
    {
        var uri = new Uri(url, UriKind.Absolute);
        var result = await SsrfGuard.IsPrivateHostAsync(uri.Host, (host, _) =>
        {
            Assert.Equal("localhost", host);
            return Task.FromResult(new[] { IPAddress.Loopback });
        }, CancellationToken.None);
        Assert.True(result, $"Expected {url} to be detected as private");
    }

    // ── Private IP address range tests ──────────────────────────────

    [Theory]
    [InlineData("10.0.0.0")]
    [InlineData("10.255.255.255")]
    [InlineData("172.16.0.0")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.0")]
    [InlineData("192.168.255.255")]
    [InlineData("100.64.0.0")]     // Carrier-grade NAT
    [InlineData("100.127.255.255")]
    [InlineData("198.18.0.0")]     // Benchmark testing
    [InlineData("198.19.255.255")]
    public async Task SsrfGuard_PrivateRanges_AllDetected(string address)
    {
        var result = await SsrfGuard.IsPrivateHostAsync(address, CancellationToken.None);
        Assert.True(result, $"Expected {address} in private range");
    }

    // ── IPv6 private ranges ─────────────────────────────────────────

    [Theory]
    [InlineData("fe80::1")]
    [InlineData("fe80::abcd:1234")]
    [InlineData("fc00::1")]
    [InlineData("fd00::1")]
    [InlineData("ff00::1")]         // multicast
    [InlineData("::1")]             // loopback
    [InlineData("::")]              // unspecified
    public async Task SsrfGuard_IPv6Private_AllDetected(string address)
    {
        var result = await SsrfGuard.IsPrivateHostAsync(address, CancellationToken.None);
        Assert.True(result, $"Expected IPv6 {address} to be private");
    }

    // ── Safe HttpClient factory ─────────────────────────────────────

    [Fact]
    public void SsrfGuard_CreateSafeHttpClient_ReturnsClient()
    {
        using var client = SsrfGuard.CreateSafeHttpClient();
        Assert.NotNull(client);
    }

    [Fact]
    public void SsrfGuard_CreateSafeHandler_ReturnsHandler()
    {
        using var handler = SsrfGuard.CreateSafeHandler();
        Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> expected = SsrfGuard.SsrfSafeConnectAsync;
        Assert.Equal(expected, handler.ConnectCallback);
    }

    [Fact]
    public void SsrfGuard_CreateSafeHttpClient_WithTimeout()
    {
        using var client = SsrfGuard.CreateSafeHttpClient(timeout: TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(5), client.Timeout);
    }

    [Fact]
    public void SsrfGuard_CreateSafeHttpClient_WithUserAgent()
    {
        using var client = SsrfGuard.CreateSafeHttpClient(userAgent: "BukitTest/1.0");
        Assert.Contains("BukitTest/1.0", client.DefaultRequestHeaders.UserAgent.ToString());
    }

    // ── CIDR boundary tests ─────────────────────────────────────────

    [Theory]
    [InlineData("127.0.0.1", true)]   // loopback
    [InlineData("127.255.255.255", true)]
    [InlineData("8.8.8.8", false)]    // Google DNS (public)
    [InlineData("1.1.1.1", false)]    // Cloudflare (public)
    [InlineData("192.0.2.1", true)]   // TEST-NET-1 (documentation)
    [InlineData("198.51.100.1", true)] // TEST-NET-2
    [InlineData("203.0.113.1", true)]  // TEST-NET-3
    public async Task SsrfGuard_BoundaryAddresses_CorrectlyClassified(string address, bool expectedPrivate)
    {
        var result = await SsrfGuard.IsPrivateHostAsync(address, CancellationToken.None);
        Assert.Equal(expectedPrivate, result);
    }

    [Theory]
    [InlineData("10.0.0.0", "10.255.255.255", "9.255.255.255", "11.0.0.0")]
    [InlineData("100.64.0.0", "100.127.255.255", "100.63.255.255", "100.128.0.0")]
    [InlineData("127.0.0.0", "127.255.255.255", "126.255.255.255", "128.0.0.0")]
    [InlineData("169.254.0.0", "169.254.255.255", "169.253.255.255", "169.255.0.0")]
    [InlineData("172.16.0.0", "172.31.255.255", "172.15.255.255", "172.32.0.0")]
    [InlineData("192.0.0.0", "192.0.0.255", "191.255.255.255", "192.0.1.0")]
    [InlineData("192.0.2.0", "192.0.2.255", "192.0.1.255", "192.0.3.0")]
    [InlineData("192.88.99.0", "192.88.99.255", "192.88.98.255", "192.88.100.0")]
    [InlineData("192.168.0.0", "192.168.255.255", "192.167.255.255", "192.169.0.0")]
    [InlineData("198.18.0.0", "198.19.255.255", "198.17.255.255", "198.20.0.0")]
    [InlineData("198.51.100.0", "198.51.100.255", "198.51.99.255", "198.51.101.0")]
    [InlineData("203.0.113.0", "203.0.113.255", "203.0.112.255", "203.0.114.0")]
    public void IsPrivateAddress_ReservedIpv4Ranges_IncludeEndpointsButNotNeighbors(
        string first, string last, string before, string after)
    {
        Assert.True(SsrfGuard.IsPrivateAddress(IPAddress.Parse(first)), first);
        Assert.True(SsrfGuard.IsPrivateAddress(IPAddress.Parse(last)), last);
        Assert.False(SsrfGuard.IsPrivateAddress(IPAddress.Parse(before)), before);
        Assert.False(SsrfGuard.IsPrivateAddress(IPAddress.Parse(after)), after);
    }

    [Theory]
    [InlineData("0.255.255.255", true)]
    [InlineData("1.0.0.0", false)]
    [InlineData("223.255.255.255", false)]
    [InlineData("224.0.0.0", true)]
    [InlineData("239.255.255.255", true)]
    [InlineData("240.0.0.0", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("fec0::1", true)]
    [InlineData("fe80::", true)]
    [InlineData("febf:ffff:ffff:ffff:ffff:ffff:ffff:ffff", true)]
    [InlineData("fc00::", true)]
    [InlineData("fdff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", true)]
    [InlineData("2001:db8::", true)]
    [InlineData("2001:db8:ffff:ffff:ffff:ffff:ffff:ffff", true)]
    [InlineData("2001:db7:ffff:ffff:ffff:ffff:ffff:ffff", false)]
    [InlineData("2001:db9::", false)]
    [InlineData("::ffff:127.0.0.1", true)]
    [InlineData("::ffff:192.168.1.1", true)]
    [InlineData("::ffff:192.0.2.1", true)]
    [InlineData("::ffff:8.8.8.8", false)]
    public void IsPrivateAddress_ReservedAndMappedAddresses_ReturnExpected(string address, bool expected)
    {
        Assert.Equal(expected, SsrfGuard.IsPrivateAddress(IPAddress.Parse(address)));
    }
}
