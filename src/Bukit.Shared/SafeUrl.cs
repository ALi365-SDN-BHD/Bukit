namespace Bukit.Shared;

internal static class SafeUrl
{
    private static readonly HashSet<string> LinkSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "mailto", "tel"
    };

    private static readonly HashSet<string> MediaSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https"
    };

    public static string? ForLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return null;
        if (trimmed.StartsWith('/')) return trimmed;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        return LinkSchemes.Contains(uri.Scheme) ? trimmed : null;
    }

    public static string? ForMedia(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return null;
        if (trimmed.StartsWith('/')) return trimmed;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        return MediaSchemes.Contains(uri.Scheme) ? trimmed : null;
    }

    public static string? ForEmbed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return null;
        if (trimmed.StartsWith('/')) return trimmed;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)) return null;
        return trimmed;
    }

    public static bool IsExternal(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return true;
        return !trimmed.StartsWith('/');
    }
}
