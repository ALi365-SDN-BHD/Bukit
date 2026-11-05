using System.IO.Enumeration;

namespace Bukit.Importing;

/// <summary>
/// Shared import safety patterns used by both HtmlDemoImporter and ImportSafetyScanner.
/// Centralizing these avoids duplication and ensures consistent protection.
/// </summary>
internal static class ImportSafetyPatterns
{
    public static readonly string[] SensitiveFileNames =
    [
        // dist/ and build/ hold built CSS/JS in many templates; they are not secrets.
        ".env", ".npmrc", ".git", "node_modules", ".vscode",
        "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519"
    ];

    public static readonly string[] SensitiveFilePatterns =
    [
        ".env.*"
    ];

    public static readonly string[] SensitiveExtensions =
    [
        ".key", ".pfx", ".p12", ".pem", ".crt", ".cert"
    ];

    public static readonly string[] DangerousProtocols =
    [
        "javascript:", "vbscript:", "file:", "data:"
    ];

    /// <summary>
    /// Built output folders that demos often ship next to their sources. They are not
    /// secrets (pages may load dist/css), but their HTML is a copy, not a source page.
    /// </summary>
    public static readonly string[] BuiltOutputDirectories = ["dist", "build"];

    /// <summary>Whether a path sits inside a built-output folder of the demo.</summary>
    public static bool IsBuiltOutputPath(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 1 &&
               segments[..^1].Any(segment =>
                   BuiltOutputDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Directories that are never entered; they are reported without walking them.</summary>
    public static readonly IReadOnlySet<string> SkipDirectoryNames =
        new HashSet<string>([".git", "node_modules", ".vscode"], StringComparer.OrdinalIgnoreCase);

    public static bool IsSensitiveFileName(string name)
        => SensitiveFileNames.Contains(name, StringComparer.OrdinalIgnoreCase) ||
           SensitiveFilePatterns.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, name, ignoreCase: true)) ||
           SensitiveExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);

    public static bool IsSensitiveDirectoryName(string name)
        => SensitiveFileNames.Contains(name, StringComparer.OrdinalIgnoreCase);
}
