using System.Text.RegularExpressions;
using Bukit.Config;

namespace Bukit.Content.Media;

public sealed class ContentImageRewritePipeline
{
    // Matches <img ... src="url" ...>
    private static readonly Regex ImgSrcRegex = new(
        "<img\\b(?<before>[^>]*?)\\bsrc\\s*=\\s*(?<q>[\"'])(?<url>.*?)(\\k<q>)(?<after>[^>]*?)>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Matches data-src="url" on any element
    private static readonly Regex DataSrcRegex = new(
        @"\bdata-src\s*=\s*(?<q>[""'])(?<url>.*?)(\k<q>)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Matches <video ... poster="url" ...>
    private static readonly Regex VideoPosterRegex = new(
        @"<video\b[^>]*?\bposter\s*=\s*(?<q>[""'])(?<url>.*?)(\k<q>)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Matches <video ... src="url" ...>
    private static readonly Regex VideoSrcRegex = new(
        @"<video\b[^>]*?\bsrc\s*=\s*(?<q>[""'])(?<url>.*?)(\k<q>)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Matches <a ... href="url" ...> where URL points to a known media host (S3/CDN)
    private static readonly Regex AnchorHrefRegex = new(
        @"<a\b[^>]*?\bhref\s*=\s*(?<q>[""'])(?<url>https?://[^""']*?\.(?:jpg|jpeg|png|gif|webp|svg|avif|bmp|ico|tiff|tif)(?:\?[^""']*)?)(\k<q>)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Matches srcset="url1 1x, url2 2x" or srcset="url1 300w, url2 600w"
    private static readonly Regex SrcsetAttrRegex = new(
        @"\bsrcset\s*=\s*(?<q>[""'])(?<value>.*?)(\k<q>)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SrcsetEntryRegex = new(
        @"(?<url>https?://[^\s,]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly MediaConfig _config;
    private readonly IImageAssetLocalizer _localizer;
    private readonly HashSet<string> _fieldKeys;

    public ContentImageRewritePipeline(MediaConfig config, IImageAssetLocalizer localizer)
    {
        _config = config;
        _localizer = localizer;
        _fieldKeys = BuildFieldKeySet(config.FieldKeys);
    }

    public async Task<IReadOnlyList<ContentItem>> RewriteAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var results = new ContentItem[items.Count];
        var concurrency = _config.MaxConcurrency is > 0 ? _config.MaxConcurrency.Value : 4;
        using var sem = new SemaphoreSlim(concurrency, concurrency);
        var tasks = new Task[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            var idx = i;
            tasks[idx] = RewriteOneAsync(items[idx], idx);
        }

        await Task.WhenAll(tasks);
        return results;

        async Task RewriteOneAsync(ContentItem item, int idx)
        {
            await sem.WaitAsync(cancellationToken);
            try
            {
                var localizeMemo = new Dictionary<string, string>(StringComparer.Ordinal);
                var html = await RewriteHtmlAsync(item.ContentHtml, localizeMemo, cancellationToken);
                var fields = await RewriteFieldsAsync(item.Fields, localizeMemo, cancellationToken);
                results[idx] = item with
                {
                    ContentHtml = html,
                    Fields = fields
                };
            }
            finally
            {
                sem.Release();
            }
        }
    }

    public Task<string?> RewriteBodyHtmlAsync(string? html, CancellationToken cancellationToken)
        => RewriteHtmlAsync(html, new Dictionary<string, string>(StringComparer.Ordinal), cancellationToken);

    private async Task<string?> RewriteHtmlAsync(
        string? html,
        Dictionary<string, string> localizeMemo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        // Pass 1: <img src="...">
        html = await RewriteByRegexAsync(html, ImgSrcRegex, "url", localizeMemo, cancellationToken);

        // Pass 2: data-src="..." (lazy loading)
        html = await RewriteByRegexAsync(html, DataSrcRegex, "url", localizeMemo, cancellationToken);

        // Pass 3: <video poster="...">
        html = await RewriteByRegexAsync(html, VideoPosterRegex, "url", localizeMemo, cancellationToken);

        // Pass 4: <video src="...">
        html = await RewriteByRegexAsync(html, VideoSrcRegex, "url", localizeMemo, cancellationToken);

        // Pass 5: <a href="...image_url..."> (file blocks with image extensions)
        html = await RewriteByRegexAsync(html, AnchorHrefRegex, "url", localizeMemo, cancellationToken);

        // Pass 6: srcset="url1 1x, url2 2x"
        html = await RewriteSrcsetAsync(html, localizeMemo, cancellationToken);

        return html;
    }

    private async Task<string> RewriteByRegexAsync(
        string html,
        Regex regex,
        string urlGroupName,
        Dictionary<string, string> localizeMemo,
        CancellationToken cancellationToken)
    {
        var matches = regex.Matches(html);
        if (matches.Count == 0)
        {
            return html;
        }

        var sb = new System.Text.StringBuilder();
        var last = 0;
        foreach (Match m in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var urlGroup = m.Groups[urlGroupName];
            var url = System.Net.WebUtility.HtmlDecode(urlGroup.Value);
            var localized = await LocalizeMemoizedAsync(url, localizeMemo, cancellationToken);
            var safe = System.Net.WebUtility.HtmlEncode(localized);

            sb.Append(html, last, urlGroup.Index - last);
            sb.Append(safe);
            last = urlGroup.Index + urlGroup.Length;
        }

        sb.Append(html, last, html.Length - last);
        return sb.ToString();
    }

    private async Task<string> RewriteSrcsetAsync(
        string html,
        Dictionary<string, string> localizeMemo,
        CancellationToken cancellationToken)
    {
        var matches = SrcsetAttrRegex.Matches(html);
        if (matches.Count == 0)
        {
            return html;
        }

        var sb = new System.Text.StringBuilder();
        var last = 0;
        foreach (Match m in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var valueGroup = m.Groups["value"];
            var srcsetValue = valueGroup.Value;
            var rewritten = await RewriteSrcsetValueAsync(srcsetValue, localizeMemo, cancellationToken);

            sb.Append(html, last, valueGroup.Index - last);
            sb.Append(rewritten);
            last = valueGroup.Index + valueGroup.Length;
        }

        sb.Append(html, last, html.Length - last);
        return sb.ToString();
    }

    private async Task<string> RewriteSrcsetValueAsync(
        string srcsetValue,
        Dictionary<string, string> localizeMemo,
        CancellationToken cancellationToken)
    {
        var matches = SrcsetEntryRegex.Matches(srcsetValue);
        if (matches.Count == 0)
        {
            return srcsetValue;
        }

        var sb = new System.Text.StringBuilder();
        var last = 0;
        foreach (Match m in matches)
        {
            var url = System.Net.WebUtility.HtmlDecode(m.Groups["url"].Value);
            var localized = await LocalizeMemoizedAsync(url, localizeMemo, cancellationToken);
            var safe = System.Net.WebUtility.HtmlEncode(localized);

            sb.Append(srcsetValue, last, m.Index - last);
            sb.Append(safe);
            last = m.Index + m.Length;
        }

        sb.Append(srcsetValue, last, srcsetValue.Length - last);
        return sb.ToString();
    }

    private async Task<IReadOnlyDictionary<string, ContentField>?> RewriteFieldsAsync(
        IReadOnlyDictionary<string, ContentField>? fields,
        Dictionary<string, string> localizeMemo,
        CancellationToken cancellationToken)
    {
        if (fields is null || fields.Count == 0)
        {
            return fields;
        }

        var changed = false;
        var copy = new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase);

        foreach (var kv in fields)
        {
            var key = kv.Key;
            var field = kv.Value;

            // Process field if it's in the FieldKeys whitelist OR has a file/files type
            var isFileType = string.Equals(field.Type, "file", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(field.Type, "files", StringComparison.OrdinalIgnoreCase);

            if (!isFileType && !_fieldKeys.Contains(key))
            {
                continue;
            }

            // Single string URL
            if (field.Value is string s)
            {
                var localized = await LocalizeMemoizedAsync(s, localizeMemo, cancellationToken);
                if (!string.Equals(localized, s, StringComparison.Ordinal))
                {
                    copy[key] = field with { Value = localized };
                    changed = true;
                }

                continue;
            }

            // List of string URLs (e.g. Notion "files" property with multiple entries)
            if (field.Value is IReadOnlyList<string> urls && urls.Count > 0)
            {
                var rewritten = new List<string>(urls.Count);
                var listChanged = false;
                foreach (var url in urls)
                {
                    var localized = await LocalizeMemoizedAsync(url, localizeMemo, cancellationToken);
                    rewritten.Add(localized);
                    if (!string.Equals(localized, url, StringComparison.Ordinal))
                    {
                        listChanged = true;
                    }
                }

                if (listChanged)
                {
                    copy[key] = field with { Value = rewritten.AsReadOnly() };
                    changed = true;
                }
            }
        }

        return changed ? copy : fields;
    }

    private async Task<string> LocalizeMemoizedAsync(
        string? sourceUrl,
        Dictionary<string, string> localizeMemo,
        CancellationToken cancellationToken)
    {
        var key = sourceUrl ?? string.Empty;
        if (localizeMemo.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var localized = await _localizer.LocalizeAsync(sourceUrl, cancellationToken);
        localizeMemo[key] = localized;
        return localized;
    }

    private static HashSet<string> BuildFieldKeySet(IReadOnlyList<string>? keys)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (keys is null)
        {
            return set;
        }

        foreach (var key in keys)
        {
            var text = (key ?? string.Empty).Trim();
            if (text.Length > 0)
            {
                set.Add(text);
            }
        }

        return set;
    }
}
