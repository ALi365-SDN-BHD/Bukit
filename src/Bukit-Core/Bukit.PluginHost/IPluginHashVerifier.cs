namespace Bukit.PluginHost;

public interface IPluginHashVerifier
{
    Task<PluginHashVerificationResult> VerifySha256Async(
        string filePath,
        string expectedSha256,
        CancellationToken cancellationToken);
}
