namespace Bukit.Shared;

public static class EnvironmentHelper
{
    public const string NotionTokenKey = "NOTION_TOKEN";
    public const string AutoSummaryKey = "BUKIT_AUTO_SUMMARY";
    public const string AutoSummaryMaxLenKey = "BUKIT_AUTO_SUMMARY_MAXLEN";

    private const int DefaultAutoSummaryMaxLength = 200;

    public static string? GetNotionToken()
        => Environment.GetEnvironmentVariable(NotionTokenKey);

    public static bool IsAutoSummaryEnabled()
    {
        var raw = (Environment.GetEnvironmentVariable(AutoSummaryKey) ?? string.Empty).Trim();
        return raw is "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    public static int GetAutoSummaryMaxLength()
    {
        var raw = (Environment.GetEnvironmentVariable(AutoSummaryMaxLenKey) ?? string.Empty).Trim();
        if (int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 0)
        {
            return n;
        }

        return DefaultAutoSummaryMaxLength;
    }
}
