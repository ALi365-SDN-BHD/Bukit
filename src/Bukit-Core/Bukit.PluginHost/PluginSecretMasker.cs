namespace Bukit.PluginHost;

internal static class PluginSecretMasker
{
    private static readonly string[] SecretKeyFragments =
    [
        "NOTION_TOKEN",
        "API_KEY",
        "PASSWORD",
        "COOKIE",
        "AUTHORIZATION",
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

    // Value-based on purpose: callers pass only plugin-authorized environment names, and any
    // authorized value may be a secret regardless of its key. Over-masking common authorized
    // values is accepted; do not switch to IsSecretKey here without a contract change.
    public static string MaskText(string value, IReadOnlyDictionary<string, string> environment)
    {
        if (string.IsNullOrEmpty(value) || environment.Count == 0)
        {
            return value;
        }

        string masked = value;
        foreach (string secretValue in environment.Values
                     .Where(secretValue => !string.IsNullOrEmpty(secretValue))
                     .Distinct(StringComparer.Ordinal)
                     .OrderByDescending(secretValue => secretValue.Length))
        {
            masked = masked.Replace(secretValue, "***", StringComparison.Ordinal);
        }

        return masked;
    }

    private static bool IsSecretKey(string key)
        => SecretKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
