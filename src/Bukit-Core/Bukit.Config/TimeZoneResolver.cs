namespace Bukit.Config;

public static class TimeZoneResolver
{
    public static TimeZoneInfo ResolveOrUtc(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        if (TryResolve(timeZoneId, out var timeZone))
        {
            return timeZone!;
        }

        throw new TimeZoneNotFoundException($"The time zone ID '{timeZoneId}' was not found on the local computer.");
    }

    public static bool TryResolve(string timeZoneId, out TimeZoneInfo? timeZone)
    {
        if (TryFind(timeZoneId, out timeZone))
        {
            return true;
        }

        if (OperatingSystem.IsWindows()
            && TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsTimeZoneId)
            && TryFind(windowsTimeZoneId, out timeZone))
        {
            return true;
        }

        if (OperatingSystem.IsWindows()
            && TimeZoneCompatibility.TryGetWindowsTimeZoneFallback(timeZoneId, out var fallbackWindowsTimeZoneId)
            && TryFind(fallbackWindowsTimeZoneId, out timeZone))
        {
            return true;
        }

        timeZone = null;
        return false;
    }

    private static bool TryFind(string timeZoneId, out TimeZoneInfo? timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = null;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = null;
            return false;
        }
    }
}
