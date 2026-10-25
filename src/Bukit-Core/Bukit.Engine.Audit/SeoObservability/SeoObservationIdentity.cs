using System.Security.Cryptography;
using System.Text;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static class SeoObservationIdentity
{
    internal static string CreateRouteKey(string route, string canonical)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonical);

        return CreateKey("route", $"route\0{route}\0{canonical}");
    }

    internal static string? CreateContentKey(ContentRecord? record, string language)
    {
        if (record is null)
        {
            return null;
        }

        return CreateKey(
            "content",
            $"content\0{record.Identity.ContentType}\0{record.Identity.Id}\0{language}");
    }

    private static string CreateKey(string kind, string material)
        => $"{kind}:sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)))}";
}
