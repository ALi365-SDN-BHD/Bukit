using System.Security.Cryptography;

namespace Bukit.PluginHost;

public sealed class PluginHashVerifier : IPluginHashVerifier
{
    public async Task<PluginHashVerificationResult> VerifySha256Async(
        string filePath,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new PluginHashVerificationResult(false, string.Empty, expectedSha256, "File path is required.");
        }

        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            return new PluginHashVerificationResult(false, string.Empty, expectedSha256, "Expected sha256 is required.");
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
            string actual = Convert.ToHexString(hash).ToLowerInvariant();
            bool success = StringComparer.OrdinalIgnoreCase.Equals(actual, expectedSha256);
            return new PluginHashVerificationResult(
                success,
                actual,
                expectedSha256,
                success ? null : "Plugin executable sha256 mismatch.");
        }
        catch (FileNotFoundException)
        {
            return NotFound(expectedSha256);
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound(expectedSha256);
        }
        catch (UnauthorizedAccessException)
        {
            return new PluginHashVerificationResult(
                false,
                string.Empty,
                expectedSha256,
                "Plugin executable could not be read: access denied.");
        }
        catch (IOException)
        {
            return new PluginHashVerificationResult(
                false,
                string.Empty,
                expectedSha256,
                "Plugin executable could not be read.");
        }
    }

    private static PluginHashVerificationResult NotFound(string expectedSha256)
        => new(false, string.Empty, expectedSha256, "Plugin executable was not found.");
}
