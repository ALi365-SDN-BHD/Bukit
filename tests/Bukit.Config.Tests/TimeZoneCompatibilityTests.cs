using Bukit.Config;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class TimeZoneCompatibilityTests
{
    public static IEnumerable<object[]> AllMappings()
    {
        yield return new object[] { "Asia/Shanghai", "China Standard Time" };
        yield return new object[] { "Asia/Kuala_Lumpur", "Singapore Standard Time" };
        yield return new object[] { "Asia/Singapore", "Singapore Standard Time" };
        yield return new object[] { "Asia/Tokyo", "Tokyo Standard Time" };
        yield return new object[] { "Asia/Seoul", "Korea Standard Time" };
        yield return new object[] { "Asia/Hong_Kong", "China Standard Time" };
        yield return new object[] { "Asia/Taipei", "Taipei Standard Time" };
        yield return new object[] { "Asia/Bangkok", "SE Asia Standard Time" };
        yield return new object[] { "Asia/Jakarta", "SE Asia Standard Time" };
        yield return new object[] { "Asia/Kolkata", "India Standard Time" };
        yield return new object[] { "Asia/Calcutta", "India Standard Time" };
        yield return new object[] { "Asia/Dubai", "Arabian Standard Time" };
        yield return new object[] { "Asia/Riyadh", "Arab Standard Time" };
        yield return new object[] { "Asia/Jerusalem", "Israel Standard Time" };
        yield return new object[] { "Asia/Tehran", "Iran Standard Time" };
        yield return new object[] { "Asia/Karachi", "Pakistan Standard Time" };
        yield return new object[] { "Asia/Dhaka", "Bangladesh Standard Time" };
        yield return new object[] { "Asia/Yangon", "Myanmar Standard Time" };
        yield return new object[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" };
        yield return new object[] { "Europe/London", "GMT Standard Time" };
        yield return new object[] { "Europe/Paris", "W. Europe Standard Time" };
        yield return new object[] { "Europe/Berlin", "W. Europe Standard Time" };
        yield return new object[] { "Europe/Moscow", "Russian Standard Time" };
        yield return new object[] { "Europe/Istanbul", "Turkey Standard Time" };
        yield return new object[] { "Europe/Athens", "GTB Standard Time" };
        yield return new object[] { "Europe/Dublin", "GMT Standard Time" };
        yield return new object[] { "Europe/Amsterdam", "W. Europe Standard Time" };
        yield return new object[] { "Europe/Rome", "W. Europe Standard Time" };
        yield return new object[] { "Europe/Madrid", "Romance Standard Time" };
        yield return new object[] { "Europe/Stockholm", "W. Europe Standard Time" };
        yield return new object[] { "Europe/Zurich", "W. Europe Standard Time" };
        yield return new object[] { "Europe/Warsaw", "Central European Standard Time" };
        yield return new object[] { "America/New_York", "Eastern Standard Time" };
        yield return new object[] { "America/Chicago", "Central Standard Time" };
        yield return new object[] { "America/Denver", "Mountain Standard Time" };
        yield return new object[] { "America/Los_Angeles", "Pacific Standard Time" };
        yield return new object[] { "America/Sao_Paulo", "E. South America Standard Time" };
        yield return new object[] { "America/Mexico_City", "Central Standard Time (Mexico)" };
        yield return new object[] { "America/Toronto", "Eastern Standard Time" };
        yield return new object[] { "America/Vancouver", "Pacific Standard Time" };
        yield return new object[] { "America/Argentina/Buenos_Aires", "Argentina Standard Time" };
        yield return new object[] { "America/Bogota", "SA Pacific Standard Time" };
        yield return new object[] { "America/Lima", "SA Pacific Standard Time" };
        yield return new object[] { "America/Santiago", "Pacific SA Standard Time" };
        yield return new object[] { "Pacific/Auckland", "New Zealand Standard Time" };
        yield return new object[] { "Australia/Sydney", "AUS Eastern Standard Time" };
        yield return new object[] { "Australia/Melbourne", "AUS Eastern Standard Time" };
        yield return new object[] { "Australia/Perth", "W. Australia Standard Time" };
        yield return new object[] { "Australia/Brisbane", "E. Australia Standard Time" };
        yield return new object[] { "Africa/Cairo", "Egypt Standard Time" };
        yield return new object[] { "Africa/Johannesburg", "South Africa Standard Time" };
        yield return new object[] { "Africa/Lagos", "W. Central Africa Standard Time" };
        yield return new object[] { "Africa/Nairobi", "E. Africa Standard Time" };
        yield return new object[] { "UTC", "UTC" };
    }

    [Theory]
    [MemberData(nameof(AllMappings))]
    public void TryGetWindowsTimeZoneFallback_ReturnsCorrectWindowsTimeZone(string ianaId, string expectedWindowsId)
    {
        var result = TimeZoneCompatibility.TryGetWindowsTimeZoneFallback(ianaId, out var windowsId);

        Assert.True(result);
        Assert.Equal(expectedWindowsId, windowsId);
    }

    [Fact]
    public void TryGetWindowsTimeZoneFallback_UnknownId_ReturnsFalse()
    {
        var result = TimeZoneCompatibility.TryGetWindowsTimeZoneFallback("Mars/Olympus", out var windowsId);

        Assert.False(result);
        Assert.Null(windowsId);
    }
}
