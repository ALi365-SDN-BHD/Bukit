namespace Bukit.Engine;

internal static class ThemeNameSanitizer
{
    private static readonly System.Buffers.SearchValues<char> AllowedChars =
        System.Buffers.SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-.");

    internal static bool TrySanitize(string? raw, out string sanitized, out string? error)
    {
        sanitized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "theme name is null or whitespace.";
            return false;
        }

        var value = raw.Trim();

        if (Path.IsPathRooted(value))
        {
            error = $"theme name '{raw}' must not be an absolute path.";
            return false;
        }

        if (value == ".." || value.Contains(".."))
        {
            error = $"theme name '{raw}' must not contain '..' segments.";
            return false;
        }

        if (value.Contains('/') || value.Contains('\\'))
        {
            error = $"theme name '{raw}' must not contain path separators.";
            return false;
        }

        foreach (var ch in value)
        {
            if (ch < 32)
            {
                error = $"theme name '{raw}' contains control characters.";
                return false;
            }
        }

        if (BuildPathUtils.IsWindowsDeviceName(value))
        {
            error = $"theme name '{raw}' is a reserved Windows device name.";
            return false;
        }

        foreach (var ch in value)
        {
            if (!AllowedChars.Contains(ch))
            {
                error = $"theme name '{raw}' contains invalid character '{ch}'. Only [A-Za-z0-9_-.] are allowed.";
                return false;
            }
        }

        sanitized = value;
        return true;
    }
}
