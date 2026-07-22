using System.Text.Json;
using Canonical = Bukit.Notion.Rendering.BlockRenderers;

namespace Bukit.Content.Notion.BlockRenderers;

internal static class NotionBlockHelpers
{
    internal static string? GetString(JsonElement obj, string name)
        => Canonical.NotionBlockHelpers.GetString(obj, name);

    internal static string ExtractPlainText(JsonElement richTextArray)
        => Canonical.NotionBlockHelpers.ExtractPlainText(richTextArray);

    internal static string GetBlockColorClass(JsonElement typeContainer)
        => Canonical.NotionBlockHelpers.GetBlockColorClass(typeContainer);

    internal static string? GetBlockColor(JsonElement typeContainer)
        => Canonical.NotionBlockHelpers.GetBlockColor(typeContainer);

    internal static string? ExtractFileUrl(JsonElement container)
        => Canonical.NotionBlockHelpers.ExtractFileUrl(container);

    internal static string NotionBlockColorToCssBackground(string notionColor)
        => Canonical.NotionBlockHelpers.NotionBlockColorToCssBackground(notionColor);

    internal static bool IsYouTubeUrl(string url, out string embedUrl)
        => Canonical.NotionBlockHelpers.IsYouTubeUrl(url, out embedUrl);

    internal static string? ExtractQueryParam(string url, string paramName)
        => Canonical.NotionBlockHelpers.ExtractQueryParam(url, paramName);
}
