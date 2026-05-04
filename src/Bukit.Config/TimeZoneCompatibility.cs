namespace Bukit.Config;

internal static class TimeZoneCompatibility
{
    private static readonly Dictionary<string, string> WindowsFallbacks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Asia/Shanghai"] = "China Standard Time"
    };

    public static bool TryGetWindowsTimeZoneFallback(string timeZoneId, out string windowsTimeZoneId)
    {
        return WindowsFallbacks.TryGetValue(timeZoneId, out windowsTimeZoneId!);
    }
}
