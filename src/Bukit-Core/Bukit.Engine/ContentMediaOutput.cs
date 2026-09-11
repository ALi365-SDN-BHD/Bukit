using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Bukit.Content.Media;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

// Resolves this variant's published bodies before asset preflight, retaining exactly those results.
internal sealed partial class ContentMediaOutput(BuildVariantContext context) : IContentBodyStore
{
    [GeneratedRegex(@"(^|,)\s*(?<url>[^\s,]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SrcsetUrl();
    private readonly ConcurrentDictionary<(string Id, ContentBodyRef Body), Lazy<Task<ContentBody>>> _bodies = new();
    private readonly ConcurrentDictionary<string, byte> _references = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _fallbacks = new(StringComparer.Ordinal);
    private readonly string _mediaPrefix = ContentProviderFactory.BuildEffectiveMediaConfig(context.Config.Content.Media, context.RootDir, context.MediaDownloadDir).UrlBase.TrimEnd('/');
    private readonly string _baseUrl = BuildPathUtils.NormalizeBaseUrl(context.BaseUrl).TrimEnd('/');
    private readonly string _rootBaseUrl = BuildPathUtils.NormalizeBaseUrl(context.RootBaseUrl ?? context.BaseUrl).TrimEnd('/');

    internal ContentDocument NormalizeDocument(ContentDocument document, bool collect = false)
    {
        var fields = document.CustomFields?.ToDictionary(pair => pair.Key, pair => pair.Value with
        {
            Value = pair.Value.Value switch
            {
                string value => RewriteUrl(value, collect),
                IReadOnlyList<string> values => values.Select(value => RewriteUrl(value, collect)).ToArray(),
                _ => pair.Value.Value
            }
        }, StringComparer.OrdinalIgnoreCase);
        var html = document.Body.Html is null ? null : RewriteHtml(document.Body.Html, collect);
        return document with
        {
            Body = document.Body with { Html = html },
            CustomFields = fields,
            Record = document.Record with
            {
                Media = [.. document.Record.Media.Select(media => media with { Url = RewriteUrl(media.Url, collect) })],
                Presentation = document.Record.Presentation with { Body = html }
            }
        };
    }

    public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
        => _bodies.GetOrAdd((document.Id, document.Body), _ => new Lazy<Task<ContentBody>>(async () =>
        {
            var html = await ContentBodyResolver.GetHtmlAsync(document, context.BodyStore, cancellationToken);
            return new ContentBody(RewriteHtml(html, collect: true));
        })).Value;

    internal async Task ResolveAsync(IEnumerable<ContentDocument> documents, CancellationToken cancellationToken)
    {
        foreach (var document in documents)
        {
            NormalizeDocument(document, collect: true);
            await GetAsync(document, cancellationToken);
        }
    }

    internal IReadOnlyList<AssetOutputItem> Plan(CancellationToken cancellationToken)
    {
        var effective = ContentProviderFactory.BuildEffectiveMediaConfig(context.Config.Content.Media, context.RootDir, context.MediaDownloadDir);
        var options = new DirectoryCopyOptions { IgnoreDotPrefixedFiles = true, FollowSymlinks = false };
        var candidates = Directory.Exists(effective.DownloadDir)
            ? DirectoryCopy.EnumerateFilesForSync(effective.DownloadDir, options, cancellationToken).ToDictionary(item => BuildPathUtils.NormalizeRelPath(item.RelativePath), StringComparer.Ordinal)
            : new Dictionary<string, DirectoryCopyItem>(StringComparer.Ordinal);
        var items = new List<AssetOutputItem>();
        foreach (var relative in _references.Keys.OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!candidates.TryGetValue(relative, out var source))
                throw new IOException($"Referenced localized media is missing or unsafe: {_mediaPrefix}/{relative}");
            items.Add(new AssetOutputItem(source.SourcePath, _mediaPrefix.TrimStart('/') + "/" + relative,
                AssetOutputCategory.Media, source.PhysicalSourceRoot, options));
        }
        return items;
    }

    internal void ValidateOutputs(IEnumerable<AssetOutputItem> items)
    {
        var planned = items.Select(item => item.Destination).ToHashSet(OutputDestinationIdentityComparer.ForOutputRoot(context.OutputDir));
        // Include current rendered HTML (including incremental skips), not stale pages in the output tree.
        foreach (var item in items.Where(item => item.Operation == AssetOutputOperation.Render && item.Destination.EndsWith(".html", StringComparison.OrdinalIgnoreCase)))
        {
            var path = FileWriter.GetSafeFullPath(context.OutputDir, item.Destination);
            if (File.Exists(path)) RewriteHtml(File.ReadAllText(path), collect: true);
        }
        foreach (var relative in _references.Keys.Select(path => _mediaPrefix.TrimStart('/') + "/" + path).Concat(_fallbacks.Keys))
        {
            if (!planned.Contains(relative) || !File.Exists(FileWriter.GetSafeFullPath(context.OutputDir, relative)))
                throw new IOException($"Referenced localized media output is missing: {relative}");
        }
    }

    private string RewriteHtml(string html, bool collect)
    {
        // Localized image links no longer have the remote href shape recognized by the source scanner.
        var references = HtmlMediaReferenceScanner.Find(html, includeAllAnchorHrefs: true);
        var result = new StringBuilder(html.Length);
        var last = 0;
        foreach (var reference in references)
        {
            var value = WebUtility.HtmlDecode(reference.Value);
            var rewritten = reference.Kind == HtmlMediaReferenceKind.Srcset
                ? SrcsetUrl().Replace(value, match => match.Value[..(match.Groups["url"].Index - match.Index)] + RewriteUrl(match.Groups["url"].Value, collect, requireFallback: true))
                : RewriteUrl(value, collect, requireFallback: true);
            result.Append(html, last, reference.ValueStart - last);
            result.Append(WebUtility.HtmlEncode(rewritten));
            last = reference.ValueStart + reference.ValueLength;
        }
        result.Append(html, last, html.Length - last);
        return result.ToString();
    }

    private string RewriteUrl(string value, bool collect, bool requireFallback = false)
    {
        var path = value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https")
        {
            if (!Uri.TryCreate(context.Config.Site.Url, UriKind.Absolute, out var site) ||
                !string.Equals(site.GetLeftPart(UriPartial.Authority), absolute.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)) return value;
            path = absolute.PathAndQuery + absolute.Fragment;
        }
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal)) return value;
        var suffixAt = path.IndexOfAny(['?', '#']);
        var suffix = suffixAt < 0 ? string.Empty : path[suffixAt..];
        path = Uri.UnescapeDataString(suffixAt < 0 ? path : path[..suffixAt]);
        foreach (var prefix in new[] { _baseUrl + _mediaPrefix, _rootBaseUrl + _mediaPrefix, _mediaPrefix }.Distinct(StringComparer.Ordinal))
        {
            if (!path.StartsWith(prefix + "/", StringComparison.Ordinal)) continue;
            var relative = path[(prefix.Length + 1)..];
            FileWriter.GetSafeFullPath(context.MediaDownloadDir, relative);
            if (relative.Contains('\\')) throw new IOException("Localized media URL contains an invalid path separator.");
            if (collect) _references.TryAdd(relative, 0);
            return _baseUrl + _mediaPrefix + "/" + string.Join('/', relative.Split('/').Select(Uri.EscapeDataString)) + suffix;
        }
        var fallback = ContentProviderFactory.BuildEffectiveMediaConfig(context.Config.Content.Media, context.RootDir, context.MediaDownloadDir).DefaultImageUrl;
        if (path == fallback || path == _baseUrl + fallback || path == _rootBaseUrl + fallback)
        {
            if (collect && requireFallback) _fallbacks.TryAdd(fallback.TrimStart('/'), 0);
            return _baseUrl + fallback + suffix;
        }
        return value;
    }
}
