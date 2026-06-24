namespace Bukit.PluginHost;

public static class PluginSecretMasker
{
    private static readonly string[] SecretKeyFragments =
    [
        "NOTION_TOKEN",
        "API_KEY",
        "PASSWORD",
        "TOKEN",
        "SECRET"
    ];

    public static string MaskValue(string key, string? value)
        => IsSecretKey(key) && !string.IsNullOrEmpty(value) ? "***" : value ?? string.Empty;

    public static IReadOnlyDictionary<string, string> MaskEnvironment(IReadOnlyDictionary<string, string> environment)
    {
        var masked = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in environment)
        {
            masked[key] = MaskValue(key, value);
        }

        return masked;
    }

    private static bool IsSecretKey(string key)
        => SecretKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
