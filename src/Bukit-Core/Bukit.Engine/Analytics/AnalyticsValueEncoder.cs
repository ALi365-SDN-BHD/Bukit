using System.Net;
using System.Text.Encodings.Web;

namespace Bukit.Engine.Analytics;

internal static class AnalyticsValueEncoder
{
    internal static string HtmlAttribute(string value)
        => WebUtility.HtmlEncode(value);

    internal static string JavaScriptString(string value)
        => JavaScriptEncoder.Default.Encode(value);
}
