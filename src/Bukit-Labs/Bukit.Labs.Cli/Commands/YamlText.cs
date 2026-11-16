namespace Bukit.Labs.Cli.Commands;

// shortcut: duplicated from Bukit.Shared.YamlDocumentText because the Architecture
// IVT allowlist forbids sharing with bukit-labs; merge if that boundary opens.
internal static class YamlText
{
    internal static string StripTrailingDocumentEndMarker(string yaml)
    {
        if (yaml.EndsWith("\n...\r\n", StringComparison.Ordinal))
        {
            return yaml[..^5];
        }

        return yaml.EndsWith("\n...\n", StringComparison.Ordinal) ? yaml[..^4] : yaml;
    }
}
