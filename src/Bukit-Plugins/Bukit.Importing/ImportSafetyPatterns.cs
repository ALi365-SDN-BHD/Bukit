namespace Bukit.Importing;

/// <summary>
/// Shared import safety patterns used by both HtmlDemoImporter and ImportSafetyScanner.
/// Centralizing these avoids duplication and ensures consistent protection.
/// </summary>
internal static class ImportSafetyPatterns
{
    public static readonly string[] SensitiveFileNames =
    [
        ".env", ".npmrc", ".git", "node_modules", ".vscode", "dist", "build",
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
    /// Combined flat list used by HtmlDemoImporter for quick scan: names + patterns + extensions.
    /// </summary>
    public static readonly string[] DangerousInputPatterns =
    [
        ..SensitiveFileNames,
        ..SensitiveFilePatterns,
        ..SensitiveExtensions
    ];
}
