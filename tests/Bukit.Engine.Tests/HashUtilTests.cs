using System.Text;
using Bukit.Engine.Incremental;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class HashUtilTests
{
    [Fact]
    public void Sha256Hex_SameInput_ReturnsSameOutput()
    {
        var input = "hello world";

        var result1 = HashUtil.Sha256Hex(input);
        var result2 = HashUtil.Sha256Hex(input);

        Assert.Equal(result1, result2);
        Assert.Equal(64, result1.Length);
    }

    [Fact]
    public void Sha256Hex_DifferentInputs_ReturnDifferentOutputs()
    {
        var result1 = HashUtil.Sha256Hex("hello");
        var result2 = HashUtil.Sha256Hex("world");

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Sha256Hex_EmptyString_ReturnsValidHash()
    {
        var result = HashUtil.Sha256Hex(string.Empty);

        Assert.NotNull(result);
        Assert.Equal(64, result.Length);
    }

    [Fact]
    public void Sha256Hex_UnicodeInput_ReturnsValidHash()
    {
        var result = HashUtil.Sha256Hex("\u4f60\u597d\u4e16\u754c");

        Assert.NotNull(result);
        Assert.Equal(64, result.Length);

        var result2 = HashUtil.Sha256Hex("\u4f60\u597d\u4e16\u754c");
        Assert.Equal(result, result2);

        var result3 = HashUtil.Sha256Hex("\u4f60\u597d\u4e16\u754c!");
        Assert.NotEqual(result, result3);
    }

    [Fact]
    public void Sha256Hex_LongInput_ReturnsValidHash()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < 10000; i++)
        {
            sb.Append("abcdefghij");
        }
        var longInput = sb.ToString();

        var result = HashUtil.Sha256Hex(longInput);

        Assert.NotNull(result);
        Assert.Equal(64, result.Length);
    }

    [Fact]
    public void ToHexLower_ReturnsConsistentLowercase()
    {
        var hash = HashUtil.Sha256Hex("test");

        Assert.NotNull(hash);
        Assert.Equal(hash, hash.ToLowerInvariant());
    }

    [Fact]
    public void Sha256Hex_SpecialCharacters_ReturnsValidHash()
    {
        var input = "!@#$%^&*()_+-=[]{}|;:',.<>?/~`";

        var result = HashUtil.Sha256Hex(input);

        Assert.NotNull(result);
        Assert.Equal(64, result.Length);
    }

    [Fact]
    public void Sha256HexForDirectory_WithLargeFileCount_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-hash-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            for (var i = 0; i < 10; i++)
            {
                File.WriteAllText(Path.Combine(tempDir, $"file-{i:D4}.txt"), $"content-{i}");
            }

            var result = HashUtil.Sha256HexForDirectory(tempDir, maxFiles: 5, maxTotalSize: 1024);
            Assert.NotNull(result);
            Assert.Equal(64, result.Length);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Sha256HexForDirectory_StableHash_WithLimits()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-hash-stable-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            for (var i = 0; i < 5; i++)
            {
                File.WriteAllText(Path.Combine(tempDir, $"file-{i:D4}.txt"), $"content-{i}");
            }

            var result1 = HashUtil.Sha256HexForDirectory(tempDir, maxFiles: 10, maxTotalSize: 10 * 1024 * 1024);
            var result2 = HashUtil.Sha256HexForDirectory(tempDir, maxFiles: 10, maxTotalSize: 10 * 1024 * 1024);

            Assert.Equal(result1, result2);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
