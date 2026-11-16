namespace Bukit.Shared;

internal static class YamlDocumentText
{
    // YamlStream.Save ends the stream with "..." plus Environment.NewLine.
    internal static string StripTrailingDocumentEndMarker(string yaml)
    {
        if (yaml.EndsWith("\n...\r\n", StringComparison.Ordinal))
        {
            return yaml[..^5];
        }

        return yaml.EndsWith("\n...\n", StringComparison.Ordinal) ? yaml[..^4] : yaml;
    }
}
