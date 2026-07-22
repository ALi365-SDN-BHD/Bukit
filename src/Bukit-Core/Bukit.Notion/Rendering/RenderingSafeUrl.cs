namespace Bukit.Notion.Rendering;

internal static class RenderingSafeUrl
{
    private static readonly HashSet<string> LinkSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "mailto", "tel"
    };

    private static readonly HashSet<string> MediaSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https"
    };

    internal static string? ForLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return null;
        if (trimmed.StartsWith('/')) return trimmed;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        return LinkSchemes.Contains(uri.Scheme) ? trimmed : null;
    }

    internal static string? ForMedia(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return null;
        if (trimmed.StartsWith('/')) return trimmed;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        return MediaSchemes.Contains(uri.Scheme) ? trimmed : null;
    }

    internal static string? ForEmbed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return null;
        if (trimmed.StartsWith('/')) return trimmed;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)) return null;
        return trimmed;
    }

    internal static bool IsExternal(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return true;
        return !trimmed.StartsWith('/');
    }
}
