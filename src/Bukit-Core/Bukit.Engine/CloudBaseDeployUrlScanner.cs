using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Content.Media;

namespace Bukit.Engine;

/// <summary>
/// Finds <c>cloud://</c> only in URL positions: HTML attribute values and CSS url(...) for markup,
/// whole string values for JSON, and css url(...) for stylesheets (D11). Text nodes are ignored.
/// Attribute tokenization is the read-only entry of <see cref="HtmlMediaReferenceScanner"/>.
/// </summary>
internal static partial class CloudBaseDeployUrlScanner
{
    internal static bool ContainsCloudReference(string text, string extension)
    {
        if (!text.Contains("cloud://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return extension.ToLowerInvariant() switch
        {
            ".html" or ".htm" or ".svg" or ".xml" or ".rss" => ContainsInMarkup(text),
            ".css" => ContainsInCss(text),
            ".json" or ".jsonld" => ContainsInJson(text),
            _ => false
        };
    }

    private static bool ContainsInMarkup(string html)
    {
        foreach (var value in HtmlMediaReferenceScanner.EnumerateUrlAttributeValues(html))
        {
            if (StartsWithCloud(value) || ContainsInCss(value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsInCss(string css)
    {
        foreach (Match match in CssUrl().Matches(css))
        {
            var value = match.Groups["value"].Value.Trim().Trim('"', '\'');
            if (StartsWithCloud(value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsInJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return ContainsInElement(document.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ContainsInElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (ContainsInElement(property.Value))
                    {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (ContainsInElement(item))
                    {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.String:
                return StartsWithCloud(element.GetString() ?? string.Empty);
            default:
                return false;
        }
    }

    private static bool StartsWithCloud(string value)
        => value.Trim().StartsWith("cloud://", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"(?is)url\(\s*(?<value>[^)]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex CssUrl();
}
