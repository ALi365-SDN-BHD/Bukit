using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
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
            Fail("URL must not be empty", url, source, DiagnosticCode.RouteInvalidInternalUrl);
        }

        if (ContainsControlCharacter(value))
        {
            Fail("URL must not contain control characters", value, source, DiagnosticCode.RouteInvalidInternalUrl);
        }

        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            Fail("URL must not be protocol-relative", value, source, DiagnosticCode.RouteInvalidInternalUrl);
        }

        if (value.Contains('?') || value.Contains('#'))
        {
            Fail("URL must not contain query or fragment components", value, source, DiagnosticCode.RouteInvalidInternalUrl);
        }

        if (!value.StartsWith("/", StringComparison.Ordinal))
        {
            Fail("URL must start with '/' and be an internal path", value, source, DiagnosticCode.RouteInvalidInternalUrl);
        }

        ValidateUrlPathSegments(value, source);
    }

    private static void ValidateUrlPathSegments(string url, string? source)
    {
        foreach (var segment in url.Split('/'))
        {
            if (segment.Length == 0)
            {
                continue;
            }

            if (segment is "." or "..")
            {
                Fail("Path segment must not traverse directories", url, source, DiagnosticCode.RouteInvalidInternalUrl);
            }

            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(segment);
            }
            catch
            {
                Fail("Path segment must contain valid percent-encoding", url, source, DiagnosticCode.RouteInvalidInternalUrl);
                return;
            }

            if (decoded is "." or "..")
            {
                Fail("Path segment must not traverse directories", url, source, DiagnosticCode.RouteInvalidInternalUrl);
            }

            if (decoded.Contains('/') || decoded.Contains('\\'))
            {
                Fail("Path segment must not contain encoded slashes", url, source, DiagnosticCode.RouteEncodedSlashInPath);
            }

            if (decoded.EndsWith('.') || decoded.EndsWith(' '))
            {
                Fail("Path segment must not end with a dot or space", url, source, DiagnosticCode.RouteInvalidInternalUrl);
            }

            var dotIndex = decoded.IndexOf('.');
            var reservedCandidate = dotIndex < 0 ? decoded : decoded[..dotIndex];
            if (ReservedWindowsNames.Contains(reservedCandidate))
            {
                Fail("Path segment uses a reserved device name", url, source, DiagnosticCode.RouteReservedWindowsPath);
            }
        }
    }

    public static void ValidateOutputPath(string outputPath, string? source = null)
    {
        var value = (outputPath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            Fail("Output path must not be empty", outputPath, source, DiagnosticCode.RouteUnsafeOutputPath);
        }

        if (ContainsControlCharacter(value))
        {
            Fail("Output path must not contain control characters", value, source, DiagnosticCode.RouteUnsafeOutputPath);
        }

        if (Path.IsPathRooted(value) || value.StartsWith("/", StringComparison.Ordinal) || value.StartsWith("\\", StringComparison.Ordinal))
        {
            Fail("Output path must be relative", value, source, DiagnosticCode.RouteUnsafeOutputPath);
        }

        if (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':')
        {
            Fail("Output path must not be drive-qualified", value, source, DiagnosticCode.RouteUnsafeOutputPath);
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
            Fail("Path segment must not be empty", fullValue, source, DiagnosticCode.RouteUnsafeOutputPath);
        }

        if (segment is "." or "..")
        {
            Fail("Path segment must not traverse directories", fullValue, source, DiagnosticCode.RouteUnsafeOutputPath);
        }

        if (ContainsControlCharacter(segment))
        {
            Fail("Path segment must not contain control characters", fullValue, source, DiagnosticCode.RouteUnsafeOutputPath);
        }

        var reservedCandidate = allowFileExtension ? Path.GetFileNameWithoutExtension(segment) : segment;
        if (ReservedWindowsNames.Contains(reservedCandidate))
        {
            Fail("Path segment uses a reserved device name", fullValue, source, DiagnosticCode.RouteReservedWindowsPath);
        }
    }

    private static bool ContainsControlCharacter(string value)
        => value.Any(char.IsControl);

    private static void Fail(string reason, string? value, string? source, DiagnosticCode code)
    {
        var sourceText = string.IsNullOrWhiteSpace(source) ? "route" : source;
        throw new ConfigException(
            $"Invalid {sourceText}: {reason}. Value: '{value ?? string.Empty}'",
            code);
    }
}
