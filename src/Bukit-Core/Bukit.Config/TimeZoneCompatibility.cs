namespace Bukit.Config;

internal static class TimeZoneCompatibility
{
    private static readonly Dictionary<string, string> WindowsFallbacks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Asia/Shanghai"] = "China Standard Time",
        ["Asia/Kuala_Lumpur"] = "Singapore Standard Time",
        ["Asia/Singapore"] = "Singapore Standard Time",
        ["Asia/Tokyo"] = "Tokyo Standard Time",
        ["Asia/Seoul"] = "Korea Standard Time",
        ["Asia/Hong_Kong"] = "China Standard Time",
        ["Asia/Taipei"] = "Taipei Standard Time",
        ["Asia/Bangkok"] = "SE Asia Standard Time",
        ["Asia/Jakarta"] = "SE Asia Standard Time",
        ["Asia/Kolkata"] = "India Standard Time",
        ["Asia/Calcutta"] = "India Standard Time",
        ["Asia/Dubai"] = "Arabian Standard Time",
        ["Asia/Riyadh"] = "Arab Standard Time",
        ["Asia/Jerusalem"] = "Israel Standard Time",
        ["Asia/Tehran"] = "Iran Standard Time",
        ["Asia/Karachi"] = "Pakistan Standard Time",
        ["Asia/Dhaka"] = "Bangladesh Standard Time",
        ["Asia/Yangon"] = "Myanmar Standard Time",
        ["Asia/Ho_Chi_Minh"] = "SE Asia Standard Time",
        ["Europe/London"] = "GMT Standard Time",
        ["Europe/Paris"] = "W. Europe Standard Time",
        ["Europe/Berlin"] = "W. Europe Standard Time",
        ["Europe/Moscow"] = "Russian Standard Time",
        ["Europe/Istanbul"] = "Turkey Standard Time",
        ["Europe/Athens"] = "GTB Standard Time",
        ["Europe/Dublin"] = "GMT Standard Time",
        ["Europe/Amsterdam"] = "W. Europe Standard Time",
        ["Europe/Rome"] = "W. Europe Standard Time",
        ["Europe/Madrid"] = "Romance Standard Time",
        ["Europe/Stockholm"] = "W. Europe Standard Time",
        ["Europe/Zurich"] = "W. Europe Standard Time",
        ["Europe/Warsaw"] = "Central European Standard Time",
        ["America/New_York"] = "Eastern Standard Time",
        ["America/Chicago"] = "Central Standard Time",
        ["America/Denver"] = "Mountain Standard Time",
        ["America/Los_Angeles"] = "Pacific Standard Time",
        ["America/Sao_Paulo"] = "E. South America Standard Time",
        ["America/Mexico_City"] = "Central Standard Time (Mexico)",
        ["America/Toronto"] = "Eastern Standard Time",
        ["America/Vancouver"] = "Pacific Standard Time",
        ["America/Argentina/Buenos_Aires"] = "Argentina Standard Time",
        ["America/Bogota"] = "SA Pacific Standard Time",
        ["America/Lima"] = "SA Pacific Standard Time",
        ["America/Santiago"] = "Pacific SA Standard Time",
        ["Pacific/Auckland"] = "New Zealand Standard Time",
        ["Australia/Sydney"] = "AUS Eastern Standard Time",
        ["Australia/Melbourne"] = "AUS Eastern Standard Time",
        ["Australia/Perth"] = "W. Australia Standard Time",
        ["Australia/Brisbane"] = "E. Australia Standard Time",
        ["Africa/Cairo"] = "Egypt Standard Time",
        ["Africa/Johannesburg"] = "South Africa Standard Time",
        ["Africa/Lagos"] = "W. Central Africa Standard Time",
        ["Africa/Nairobi"] = "E. Africa Standard Time",
        ["UTC"] = "UTC"
    };

    public static bool TryGetWindowsTimeZoneFallback(string timeZoneId, out string windowsTimeZoneId)
    {
        return WindowsFallbacks.TryGetValue(timeZoneId, out windowsTimeZoneId!);
    }
}
