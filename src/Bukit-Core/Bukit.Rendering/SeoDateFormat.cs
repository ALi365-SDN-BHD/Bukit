using System.Globalization;

namespace Bukit.Rendering;

/// <summary>Date-time text for SEO output (JSON-LD, Open Graph article tags and their template values).</summary>
internal static class SeoDateFormat
{
    /// <summary>Second precision with the value's offset, such as <c>2026-06-05T18:00:00+08:00</c>.</summary>
    internal static string Format(DateTimeOffset value)
        => value.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
}
