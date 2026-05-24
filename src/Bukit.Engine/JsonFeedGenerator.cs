using System.Text.Json;

namespace Bukit.Engine;

public static class JsonFeedGenerator
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

        var sorted = posts
            .OrderByDescending(x => x.PublishAt)
            .GroupBy(x => x.AbsoluteUrl, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(maxItems)
            .ToList();

        var feedUrl = RssGenerator.BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/" + feedFileName);
        var homeUrl = RssGenerator.BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/");
        var channelDescription = string.IsNullOrWhiteSpace(siteDescription) ? siteTitle : siteDescription.Trim();

        using var stream = File.Create(Path.Combine(outputDir, feedFileName));
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteString("version", "https://jsonfeed.org/version/1.1");
        writer.WriteString("title", siteTitle);
        writer.WriteString("home_page_url", homeUrl);
        writer.WriteString("feed_url", feedUrl);
        if (!string.IsNullOrWhiteSpace(channelDescription))
        {
            writer.WriteString("description", channelDescription);
        }

        writer.WriteStartArray("items");
        foreach (var post in sorted)
        {
            writer.WriteStartObject();
            writer.WriteString("id", post.AbsoluteUrl);
            writer.WriteString("url", post.AbsoluteUrl);
            writer.WriteString("title", post.Title);
            writer.WriteString("date_published", post.PublishAt.ToString("O"));

            if (!string.IsNullOrWhiteSpace(post.Description))
            {
                writer.WriteString("summary", post.Description);
            }

            if (post.Categories is { Count: > 0 })
            {
                writer.WriteStartArray("tags");
                foreach (var c in post.Categories)
                {
                    writer.WriteStringValue(c);
                }
                writer.WriteEndArray();
            }

            if (!string.IsNullOrWhiteSpace(post.ContentHtml))
            {
                writer.WriteString("content_html", post.ContentHtml);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
    }
}
