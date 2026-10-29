using System.Security.Cryptography;
using System.Text;
using Bukit.PluginHost;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class PluginHashVerifierTests
{
    [Fact]
    public async Task VerifySha256Async_ReturnsMatchForExpectedHash()
    {
        using var directory = TestDirectory.Create();
        string path = directory.Write("plugins/echo/bin/osx-arm64/bukit-plugin-echo", "hello");
        string expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hello"))).ToLowerInvariant();
        var verifier = new PluginHashVerifier();

        PluginHashVerificationResult result = await verifier.VerifySha256Async(path, expected, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expected, result.ActualSha256);
    }

    [Fact]
    public async Task VerifySha256Async_ReturnsFailureForHashMismatch()
    {
        using var directory = TestDirectory.Create();
        string path = directory.Write("plugins/echo/bin/osx-arm64/bukit-plugin-echo", "hello");
        var verifier = new PluginHashVerifier();

        PluginHashVerificationResult result = await verifier.VerifySha256Async(path, new string('0', 64), CancellationToken.None);

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("", "expected")]
    [InlineData(" ", "expected")]
    public async Task VerifySha256Async_RequiresFilePath(string path, string expected)
    {
        var verifier = new PluginHashVerifier();

        PluginHashVerificationResult result = await verifier.VerifySha256Async(path, expected, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(result.ActualSha256);
        Assert.Equal("File path is required.", result.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task VerifySha256Async_RequiresExpectedSha256(string expected)
    {
        using var directory = TestDirectory.Create();
        string path = directory.Write("plugins/echo/bin/osx-arm64/bukit-plugin-echo", "hello");
        var verifier = new PluginHashVerifier();

        PluginHashVerificationResult result = await verifier.VerifySha256Async(path, expected, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Expected sha256 is required.", result.Message);
    }

    [Fact]
    public async Task VerifySha256Async_ComparesDigestCaseInsensitively()
    {
        using var directory = TestDirectory.Create();
        string path = directory.Write("plugins/echo/bin/osx-arm64/bukit-plugin-echo", "hello");
        string expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hello")));
        var verifier = new PluginHashVerifier();

        PluginHashVerificationResult result = await verifier.VerifySha256Async(path, expected, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expected, result.ExpectedSha256);
    }
}
