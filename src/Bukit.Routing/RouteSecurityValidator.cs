namespace Bukit.Routing;

public static class RouteSecurityValidator
{
    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static void ValidateInternalUrl(string url, string? source = null)
    {
        var value = (url ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            Fail("URL must not be empty", url, source);
        }

        if (ContainsControlCharacter(value))
        {
            Fail("URL must not contain control characters", value, source);
        }

        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            Fail("URL must not be protocol-relative", value, source);
        }

        if (!value.StartsWith("/", StringComparison.Ordinal) && Uri.TryCreate(value, UriKind.Absolute, out var absolute) && !string.IsNullOrWhiteSpace(absolute.Scheme))
        {
            Fail("URL must be an internal path", value, source);
        }
    }

    public static void ValidateOutputPath(string outputPath, string? source = null)
    {
        var value = (outputPath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            Fail("Output path must not be empty", outputPath, source);
        }

        if (ContainsControlCharacter(value))
        {
            Fail("Output path must not contain control characters", value, source);
        }

        if (Path.IsPathRooted(value) || value.StartsWith("/", StringComparison.Ordinal) || value.StartsWith("\\", StringComparison.Ordinal))
        {
            Fail("Output path must be relative", value, source);
        }

        if (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':')
        {
            Fail("Output path must not be drive-qualified", value, source);
        }

        var normalized = value.Replace('\\', '/');
        foreach (var segment in normalized.Split('/'))
        {
            ValidatePathSegment(segment, value, source, allowFileExtension: true);
        }
    }

    public static void ValidateSlugSegment(string segment, string? source = null)
    {
        ValidatePathSegment(segment, segment, source, allowFileExtension: false);
    }

    private static void ValidatePathSegment(string segment, string fullValue, string? source, bool allowFileExtension)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            Fail("Path segment must not be empty", fullValue, source);
        }

        if (segment is "." or "..")
        {
            Fail("Path segment must not traverse directories", fullValue, source);
        }

        if (ContainsControlCharacter(segment))
        {
            Fail("Path segment must not contain control characters", fullValue, source);
        }

        var reservedCandidate = allowFileExtension ? Path.GetFileNameWithoutExtension(segment) : segment;
        if (ReservedWindowsNames.Contains(reservedCandidate))
        {
            Fail("Path segment uses a reserved device name", fullValue, source);
        }
    }

    private static bool ContainsControlCharacter(string value)
        => value.Any(char.IsControl);

    private static void Fail(string reason, string? value, string? source)
    {
        var sourceText = string.IsNullOrWhiteSpace(source) ? "route" : source;
        throw new InvalidOperationException($"Invalid {sourceText}: {reason}. Value: '{value ?? string.Empty}'");
    }
}
