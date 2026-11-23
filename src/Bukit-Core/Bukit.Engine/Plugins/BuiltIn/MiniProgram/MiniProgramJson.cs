using System.Globalization;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Content.Media;

namespace Bukit.Engine.Plugins.BuiltIn.MiniProgram;

/// <summary>Deterministic bytes, content hashes and absolute URLs for mini-program files.</summary>
internal static class MiniProgramJson
{
    // The files are data for a mini-program, never inlined into HTML, so non-ASCII text stays readable.
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false
    };

    /// <summary>UTF-8 without BOM, compact, followed by exactly one <c>\n</c> on every operating system.</summary>
    internal static byte[] Serialize(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            node.WriteTo(writer);
        }

        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal static string FormatDate(DateTimeOffset value)
        => value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture);

    /// <summary>Drops the query string so diagnostics never print signature or credential parameters.</summary>
    internal static string RedactQuery(string url)
    {
        var index = url.IndexOf('?', StringComparison.Ordinal);
        return index < 0 ? url : url[..index] + "?…";
    }

    /// <summary>
    /// True when the URL carries an expiring signature, such as a Notion file URL that was not localized.
    /// Covers the schemes media localization recognizes (AWS, Google, CloudFront, Notion) plus bare
    /// <c>Signature</c>, <c>Expires</c> and <c>X-Amz-Credential</c> parameters.
    /// </summary>
    internal static bool IsSignedUrl(string value)
    {
        if (value.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Query))
        {
            return false;
        }

        if (!string.Equals(SignedUrlIdentity.IdentityTarget(uri), SignedUrlIdentity.RequestTarget(uri), StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = Uri.UnescapeDataString(part.Split('=', 2)[0]);
            if (name.Equals("X-Amz-Credential", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Signature", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Expires", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
