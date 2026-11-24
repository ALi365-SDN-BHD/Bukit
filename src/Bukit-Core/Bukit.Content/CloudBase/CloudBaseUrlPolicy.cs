using System.Text.RegularExpressions;
using Bukit.Notion.Rendering;

namespace Bukit.Content.CloudBase;

/// <summary>
/// Decides which parsed Markdown link and image URLs survive untrusted CloudBase bodies.
/// A URL without a scheme (relative, root-relative, fragment, query) is kept as-is; a URL with a
/// scheme must be allowed by <see cref="RenderingSafeUrl"/>.
/// </summary>
internal static partial class CloudBaseUrlPolicy
{
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9+.-]*:", RegexOptions.CultureInvariant)]
    private static partial Regex SchemePattern();

    internal static string SanitizeLink(string? url)
        => Sanitize(url, RenderingSafeUrl.ForLink);

    internal static string SanitizeMedia(string? url)
        => Sanitize(url, RenderingSafeUrl.ForMedia);

    private static string Sanitize(string? url, Func<string?, string?> allow)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var trimmed = url.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return SchemePattern().IsMatch(trimmed)
            ? allow(trimmed) ?? string.Empty
            : trimmed;
    }
}
