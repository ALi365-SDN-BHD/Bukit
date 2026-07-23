using System.Text;

namespace Bukit.Engine;

internal static class AtomFeedGenerator
{
    public static void Generate(
        string outputDir,
        string siteUrl,
        string baseUrl,
        string siteTitle,
        IReadOnlyList<RssGenerator.Post> posts,
        string feedFileName,
        int maxItems = 20,
        string? siteDescription = null)
    {
        var normalizedSiteUrl = RssGenerator.InternalNormalizeSiteUrl(siteUrl);
        var normalizedBaseUrl = RssGenerator.InternalNormalizeBaseUrl(baseUrl);

        var sorted = FeedWindowSelector.Select(posts, x => x.PublishAt, x => x.AbsoluteUrl, maxItems);

        var feedUrl = RssGenerator.BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/" + feedFileName);
        var homeUrl = RssGenerator.BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/");
        var updated = sorted.Count > 0 ? sorted[0].PublishAt : DateTimeOffset.UtcNow;
        var channelDescription = string.IsNullOrWhiteSpace(siteDescription) ? siteTitle : siteDescription.Trim();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<feed xmlns=\"http://www.w3.org/2005/Atom\">");
        sb.AppendLine($"  <title>{EscapeXml(siteTitle)}</title>");
        if (!string.IsNullOrWhiteSpace(channelDescription))
        {
            sb.AppendLine($"  <subtitle>{EscapeXml(channelDescription)}</subtitle>");
        }
        sb.AppendLine($"  <link href=\"{EscapeXml(homeUrl)}\" />");
        sb.AppendLine($"  <link href=\"{EscapeXml(feedUrl)}\" rel=\"self\" />");
        sb.AppendLine($"  <updated>{updated:yyyy-MM-ddTHH:mm:ssZ}</updated>");
        sb.AppendLine($"  <id>{EscapeXml(homeUrl)}</id>");
        sb.AppendLine("  <generator>bukit</generator>");

        foreach (var post in sorted)
        {
            sb.AppendLine("  <entry>");
            sb.AppendLine($"    <title>{EscapeXml(post.Title)}</title>");
            sb.AppendLine($"    <link href=\"{EscapeXml(post.AbsoluteUrl)}\" />");
            sb.AppendLine($"    <id>{EscapeXml(post.AbsoluteUrl)}</id>");
            sb.AppendLine($"    <published>{post.PublishAt:yyyy-MM-ddTHH:mm:ssZ}</published>");
            sb.AppendLine($"    <updated>{post.PublishAt:yyyy-MM-ddTHH:mm:ssZ}</updated>");

            if (post.Categories is { Count: > 0 } cats)
            {
                foreach (var c in cats)
                {
                    sb.AppendLine($"    <category term=\"{EscapeXml(c)}\" />");
                }
            }

            if (!string.IsNullOrWhiteSpace(post.Description))
            {
                sb.AppendLine($"    <summary>{EscapeXml(post.Description!)}</summary>");
            }

            if (!string.IsNullOrWhiteSpace(post.ContentHtml))
            {
                sb.AppendLine($"    <content type=\"html\"><![CDATA[{ToCData(post.ContentHtml!)}]]></content>");
            }

            sb.AppendLine("  </entry>");
        }

        sb.AppendLine("</feed>");
        FileWriter.WriteUtf8(outputDir, feedFileName, sb.ToString());
    }

    private static string ToCData(string value)
    {
        return value.Replace("]]>", "]]]]><![CDATA[>", StringComparison.Ordinal);
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }
}
