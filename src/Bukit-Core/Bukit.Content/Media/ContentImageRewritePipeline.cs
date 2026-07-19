using Bukit.Engine.Abstractions.Content;
using System.Text.RegularExpressions;
using Bukit.Config;

namespace Bukit.Content.Media;

public sealed class ContentImageRewritePipeline
{
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

    public async Task<IReadOnlyList<ContentDocument>> RewriteAsync(IReadOnlyList<ContentDocument> documents, CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return documents;
        }

        var results = new ContentDocument[documents.Count];
        var concurrency = _config.MaxConcurrency is > 0 ? _config.MaxConcurrency.Value : 4;
        using var documentGate = new SemaphoreSlim(concurrency, concurrency);
        using var downloadGate = new SemaphoreSlim(concurrency, concurrency);
        var tasks = new Task[documents.Count];
        for (var i = 0; i < documents.Count; i++)
        {
            var idx = i;
            tasks[idx] = RewriteOneAsync(documents[idx], idx);
        }

        await Task.WhenAll(tasks);
        return results;

        async Task RewriteOneAsync(ContentDocument document, int idx)
        {
            await documentGate.WaitAsync(cancellationToken);
            try
            {
                var localizeMemo = new Dictionary<string, string>(StringComparer.Ordinal);
                var html = await RewriteHtmlAsync(document.Body.Html, localizeMemo, downloadGate, cancellationToken);
                var fields = await RewriteFieldsAsync(document.CustomFields, localizeMemo, downloadGate, cancellationToken);
                results[idx] = document with
                {
                    Body = document.Body with { Html = html },
                    CustomFields = fields,
                    Route = ContentRoutePolicy.FromFields(fields),
                    Publish = ContentPublishPolicy.FromFields(fields)
                };
            }
            finally
            {
                documentGate.Release();
            }
        }
    }

    public async Task<string?> RewriteBodyHtmlAsync(string? html, CancellationToken cancellationToken)
    {
        using var downloadGate = CreateDownloadGate();
        return await RewriteBodyHtmlAsync(html, downloadGate, cancellationToken);
    }

    internal SemaphoreSlim CreateDownloadGate()
    {
        var concurrency = _config.MaxConcurrency is > 0 ? _config.MaxConcurrency.Value : 4;
        return new SemaphoreSlim(concurrency, concurrency);
    }

    internal async Task<string?> RewriteBodyHtmlAsync(
        string? html,
        SemaphoreSlim downloadGate,
        CancellationToken cancellationToken)
    {
        return await RewriteHtmlAsync(
            html,
            new Dictionary<string, string>(StringComparer.Ordinal),
            downloadGate,
            cancellationToken);
    }

    private async Task<string?> RewriteHtmlAsync(
        string? html,
        Dictionary<string, string> localizeMemo,
        SemaphoreSlim downloadGate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        var references = HtmlMediaReferenceScanner.Find(html);
        if (references.Count == 0)
        {
            return html;
        }

        var urls = new List<string>(references.Count);
        foreach (var reference in references)
        {
            if (reference.Kind == HtmlMediaReferenceKind.Srcset)
            {
                CollectSrcsetValueUrls(reference.Value, urls);
            }
            else
            {
                urls.Add(MaybeHtmlDecode(reference.Value));
            }
        }

        var localizedMap = await LocalizeDistinctUrlsAsync(urls, localizeMemo, downloadGate, cancellationToken);
        var sb = new System.Text.StringBuilder();
        var last = 0;
        foreach (var reference in references)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rewritten = reference.Kind == HtmlMediaReferenceKind.Srcset
                ? await RewriteSrcsetValueAsync(reference.Value, localizedMap, localizeMemo, downloadGate, cancellationToken)
                : await RewriteUrlValueAsync(reference.Value, localizedMap, localizeMemo, downloadGate, cancellationToken);

            sb.Append(html, last, reference.ValueStart - last);
            sb.Append(rewritten);
            last = reference.ValueStart + reference.ValueLength;
        }

        sb.Append(html, last, html.Length - last);
        return sb.ToString();
    }

    /// <summary>
    /// Fast-path HTML decode: returns the original string if it contains no '&',
    /// avoiding the StringBuilder allocation inside <see cref="System.Net.WebUtility.HtmlDecode(string)"/>.
    /// </summary>
    private static string MaybeHtmlDecode(string value)
        => value.IndexOf('&') < 0 ? value : System.Net.WebUtility.HtmlDecode(value);

    private async Task<string> RewriteUrlValueAsync(
        string value,
        IReadOnlyDictionary<string, string> localizedMap,
        Dictionary<string, string> localizeMemo,
        SemaphoreSlim downloadGate,
        CancellationToken cancellationToken)
    {
        var url = MaybeHtmlDecode(value);
        var localized = localizedMap.TryGetValue(url, out var mapped)
            ? mapped
            : await LocalizeMemoizedAsync(url, localizeMemo, downloadGate, cancellationToken);
        return System.Net.WebUtility.HtmlEncode(localized);
    }

    private async Task<string> RewriteSrcsetValueAsync(
        string srcsetValue,
        IReadOnlyDictionary<string, string> localizedMap,
        Dictionary<string, string> localizeMemo,
        SemaphoreSlim downloadGate,
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
            var url = MaybeHtmlDecode(m.Groups["url"].Value);
            var localized = localizedMap.TryGetValue(url, out var mapped)
                ? mapped
                : await LocalizeMemoizedAsync(url, localizeMemo, downloadGate, cancellationToken);
            var safe = System.Net.WebUtility.HtmlEncode(localized);

            sb.Append(srcsetValue, last, m.Index - last);
            sb.Append(safe);
            last = m.Index + m.Length;
        }

        sb.Append(srcsetValue, last, srcsetValue.Length - last);
        return sb.ToString();
    }

    private static void CollectSrcsetValueUrls(string srcsetValue, List<string> urls)
    {
        var entries = SrcsetEntryRegex.Matches(srcsetValue);
        foreach (Match entry in entries)
        {
            urls.Add(MaybeHtmlDecode(entry.Groups["url"].Value));
        }
    }

    private async Task<IReadOnlyDictionary<string, ContentField>?> RewriteFieldsAsync(
        IReadOnlyDictionary<string, ContentField>? fields,
        Dictionary<string, string> localizeMemo,
        SemaphoreSlim downloadGate,
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
                var localized = await LocalizeMemoizedAsync(s, localizeMemo, downloadGate, cancellationToken);
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
                var localizedMap = await LocalizeDistinctUrlsAsync(urls, localizeMemo, downloadGate, cancellationToken);
                var rewritten = new List<string>(urls.Count);
                var listChanged = false;
                foreach (var url in urls)
                {
                    var localized = localizedMap.TryGetValue(url ?? string.Empty, out var mapped)
                        ? mapped
                        : await LocalizeMemoizedAsync(url, localizeMemo, downloadGate, cancellationToken);
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
        SemaphoreSlim downloadGate,
        CancellationToken cancellationToken)
    {
        var key = sourceUrl ?? string.Empty;
        if (localizeMemo.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var localized = await LocalizeWithGateAsync(sourceUrl, downloadGate, cancellationToken);
        localizeMemo[key] = localized;
        return localized;
    }

    private async Task<IReadOnlyDictionary<string, string>> LocalizeDistinctUrlsAsync(
        IReadOnlyList<string> urls,
        Dictionary<string, string> localizeMemo,
        SemaphoreSlim downloadGate,
        CancellationToken cancellationToken)
    {
        var distinctKeys = new HashSet<string>(StringComparer.Ordinal);
        var pending = new List<(string Key, Task<string> Task)>();

        foreach (var url in urls)
        {
            var key = url ?? string.Empty;
            if (!distinctKeys.Add(key) || localizeMemo.ContainsKey(key))
            {
                continue;
            }

            pending.Add((key, LocalizeWithGateAsync(url, downloadGate, cancellationToken)));
        }

        if (pending.Count == 0)
        {
            return localizeMemo;
        }

        await Task.WhenAll(pending.Select(x => x.Task));
        foreach (var entry in pending)
        {
            localizeMemo[entry.Key] = await entry.Task;
        }

        return localizeMemo;
    }

    private async Task<string> LocalizeWithGateAsync(
        string? sourceUrl,
        SemaphoreSlim downloadGate,
        CancellationToken cancellationToken)
    {
        await downloadGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _localizer.LocalizeAsync(sourceUrl, cancellationToken);
        }
        finally
        {
            downloadGate.Release();
        }
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
