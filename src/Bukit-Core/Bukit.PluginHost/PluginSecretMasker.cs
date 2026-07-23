namespace Bukit.PluginHost;

internal static class PluginSecretMasker
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
