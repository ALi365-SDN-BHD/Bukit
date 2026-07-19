namespace Bukit.Engine;

internal static class OutputDestinationIdentityComparer
{
    internal static StringComparer ForOutputRoot(string outputRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);

        var fullOutputRoot = Path.GetFullPath(outputRoot);
        var token = Guid.NewGuid().ToString("N");
        var lowerCasePath = Path.Combine(fullOutputRoot, $".bukit-output-case-probe-{token}-a");
        var upperCasePath = Path.Combine(fullOutputRoot, $".bukit-output-case-probe-{token}-A");
        var created = false;

        try
        {
            using (new FileStream(
                       lowerCasePath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.ReadWrite | FileShare.Delete))
            {
                created = true;
            }

            return File.Exists(upperCasePath)
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }
        finally
        {
            if (created)
            {
                File.Delete(lowerCasePath);
            }
        }
    }
}
