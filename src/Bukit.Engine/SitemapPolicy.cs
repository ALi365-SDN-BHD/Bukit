using System.Text.RegularExpressions;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine;

internal static partial class SitemapPolicy
{
    internal static DateTimeOffset ResolveLastModified(ContentItem item)
    {
        if (item.Fields is not null && item.Fields.TryGetValue("update_time", out var field) && field is not null)
        {
            if (TryReadDate(field.Value, out var dt))
            {
                return dt;
            }
        }

        return item.PublishAt;
    }

    internal static bool ShouldExcludeFromSitemap(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        foreach (Match match in MetaTagRegex().Matches(html))
        {
            var tag = match.Value;
            var name = ReadAttributeValue(tag, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var content = ReadAttributeValue(tag, "content") ?? string.Empty;
            if (name.Equals("robots", StringComparison.OrdinalIgnoreCase))
            {
                if (ContainsToken(content, "noindex") || ContainsToken(content, "none"))
                {
                    return true;
                }
            }
            else if (name.Equals("sitemap", StringComparison.OrdinalIgnoreCase))
            {
                if (ContainsToken(content, "exclude") || ContainsToken(content, "noindex") || EqualsToken(content, "false") || EqualsToken(content, "0"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool ShouldExcludeFromSitemapFile(string absoluteHtmlPath, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(absoluteHtmlPath))
        {
            return false;
        }

        if (!File.Exists(absoluteHtmlPath))
        {
            return false;
        }

        try
        {
            var html = File.ReadAllText(absoluteHtmlPath);
            return ShouldExcludeFromSitemap(html);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Warn($"event=sitemap.meta_read_failed path={absoluteHtmlPath} error={ex.Message}");
            return false;
        }
    }

    private static bool TryReadDate(object? value, out DateTimeOffset date)
    {
        if (value is DateTimeOffset dto)
        {
            date = dto;
            return true;
        }

        if (value is DateTime dt)
        {
            date = dt.Kind == DateTimeKind.Unspecified
                ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Local)).ToUniversalTime()
                : new DateTimeOffset(dt).ToUniversalTime();
            return true;
        }

        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            date = default;
            return false;
        }

        return DateTimeOffset.TryParse(text.Trim(), out date);
    }

    private static string? ReadAttributeValue(string tag, string key)
    {
        if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var m = Regex.Match(tag, $@"\b{Regex.Escape(key)}\s*=\s*(?:""(?<q>[^""]*)""|'(?<q>[^']*)'|(?<u>[^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
        {
            return null;
        }

        var q = m.Groups["q"].Value;
        if (!string.IsNullOrEmpty(q))
        {
            return q.Trim();
        }

        var u = m.Groups["u"].Value;
        return string.IsNullOrWhiteSpace(u) ? null : u.Trim();
    }

    private static bool ContainsToken(string content, string token)
    {
        return content.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EqualsToken(string content, string token)
    {
        return string.Equals(content.Trim(), token, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("<meta\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaTagRegex();
}
