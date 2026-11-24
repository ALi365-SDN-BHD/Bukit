using System.Text.RegularExpressions;
using Bukit.CloudBase.Transport;

namespace Bukit.Content.CloudBase;

/// <summary>
/// Signs CloudBase file objects and rewrites the two allowed positions: fileFields values and
/// body image positions. Everything else that carries <c>cloud://</c> is dropped or degraded
/// (D17); failures fall back to the media placeholder instead of publishing raw references.
/// </summary>
internal sealed partial class CloudBaseFileResolver
{
    private readonly CloudBaseClient _client;
    private readonly string _envId;
    private readonly string _bucket;
    private readonly string _placeholder;
    private readonly int _expiresSeconds;
    private readonly Dictionary<string, string?> _resolved = new(StringComparer.Ordinal);
    private readonly List<string> _pending = [];

    internal CloudBaseFileResolver(
        CloudBaseClient client,
        string envId,
        string bucket,
        string placeholder,
        int expiresSeconds)
    {
        _client = client;
        _envId = envId;
        _bucket = bucket;
        _placeholder = placeholder;
        _expiresSeconds = expiresSeconds;
    }

    internal int ResolveFailures { get; private set; }

    internal int UnsupportedReferences { get; private set; }

    internal string Placeholder => _placeholder;

    internal void MarkUnsupported() => UnsupportedReferences++;

    /// <summary>A fileFields value is an object name or a matching cloud:// reference.</summary>
    internal bool TryAddFieldValue(string? value, out string objectName)
    {
        objectName = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseCloudReference(value, out var parsed))
            {
                ResolveFailures++;
                return false;
            }

            objectName = parsed;
            AddPending(objectName);
            return true;
        }

        if (!IsValidObjectName(value))
        {
            ResolveFailures++;
            return false;
        }

        objectName = value;
        AddPending(objectName);
        return true;
    }

    internal string Resolve(string objectName)
        => _resolved.TryGetValue(objectName, out var url) && url is not null ? url : _placeholder;

    internal string RewriteMarkdown(string markdown)
    {
        if (!markdown.Contains("cloud://", StringComparison.OrdinalIgnoreCase))
        {
            return markdown;
        }

        markdown = InlineImage().Replace(markdown, match =>
        {
            var url = match.Groups["url"].Value;
            if (!url.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            return TryParseCloudReference(url, out var objectName)
                ? match.Value.Replace(url, Resolve(objectName), StringComparison.Ordinal)
                : match.Value.Replace(url, _placeholder, StringComparison.Ordinal);
        });

        markdown = InlineLink().Replace(markdown, match =>
        {
            var url = match.Groups["url"].Value;
            if (!url.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            return match.Groups["text"].Value;
        });

        var usedImageLabels = ImageReferenceUse().Matches(markdown)
            .Select(match => match.Groups["label"].Value)
            .ToHashSet(StringComparer.Ordinal);
        var definitions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in ReferenceDefinition().Matches(markdown))
        {
            definitions[match.Groups["label"].Value] = match.Groups["url"].Value;
        }
        var linkOnlyLabels = definitions
            .Where(pair => pair.Value.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase) &&
                !usedImageLabels.Contains(pair.Key))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
        markdown = ReferenceDefinition().Replace(markdown, match =>
        {
            var url = match.Groups["url"].Value;
            if (linkOnlyLabels.Contains(match.Groups["label"].Value))
            {
                return string.Empty;
            }

            if (!usedImageLabels.Contains(match.Groups["label"].Value))
            {
                return match.Value;
            }

            if (!url.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            return TryParseCloudReference(url, out var objectName)
                ? match.Value.Replace(url, Resolve(objectName), StringComparison.Ordinal)
                : match.Value.Replace(url, _placeholder, StringComparison.Ordinal);
        });

        markdown = LinkReferenceUse().Replace(markdown, match =>
            definitions.TryGetValue(match.Groups["label"].Value, out var url) &&
            url.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase) &&
            !usedImageLabels.Contains(match.Groups["label"].Value)
                ? match.Groups["text"].Value
                : match.Value);

        markdown = CloudBaseAutolink().Replace(markdown, string.Empty);
        return markdown;
    }

    internal void CollectMarkdownReferences(string markdown)
    {
        if (!markdown.Contains("cloud://", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (Match match in InlineImage().Matches(markdown))
        {
            CollectReference(match.Groups["url"].Value);
        }

        foreach (Match match in InlineLink().Matches(markdown))
        {
            if (match.Groups["url"].Value.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase))
            {
                UnsupportedReferences++;
            }
        }

        var usedImageLabels = ImageReferenceUse().Matches(markdown)
            .Select(match => match.Groups["label"].Value)
            .ToHashSet(StringComparer.Ordinal);
        var definitions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in ReferenceDefinition().Matches(markdown))
        {
            definitions[match.Groups["label"].Value] = match.Groups["url"].Value;
        }
        foreach (Match match in LinkReferenceUse().Matches(markdown))
        {
            if (definitions.TryGetValue(match.Groups["label"].Value, out var url) &&
                url.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase) &&
                !usedImageLabels.Contains(match.Groups["label"].Value))
            {
                UnsupportedReferences++;
            }
        }

        UnsupportedReferences += CloudBaseAutolink().Matches(markdown).Count;
        foreach (Match match in ReferenceDefinition().Matches(markdown))
        {
            if (usedImageLabels.Contains(match.Groups["label"].Value))
            {
                CollectReference(match.Groups["url"].Value);
            }
        }
    }

    private void CollectReference(string url)
    {
        if (!url.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (TryParseCloudReference(url, out var objectName))
        {
            AddPending(objectName);
        }
        else
        {
            ResolveFailures++;
        }
    }

    internal async Task SignAsync(CancellationToken cancellationToken)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        IReadOnlyList<Bukit.CloudBase.Models.CloudBaseSignedObject> results;
        try
        {
            results = await _client.SignObjectsAsync(_bucket, [.. _pending.Distinct(StringComparer.Ordinal)], _expiresSeconds, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CloudBaseApiException)
        {
            ResolveFailures += _pending.Count;
            return;
        }

        foreach (var result in results)
        {
            if (result.Url is null)
            {
                ResolveFailures++;
            }

            _resolved[result.ObjectName] = result.Url;
        }
    }

    private bool TryParseCloudReference(string value, out string objectName)
    {
        objectName = string.Empty;
        var rest = value["cloud://".Length..];
        var dot = rest.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0)
        {
            return false;
        }

        var envId = rest[..dot];
        var slash = rest.IndexOf('/', dot + 1);
        if (slash < 0)
        {
            return false;
        }

        var bucket = rest[(dot + 1)..slash];
        var path = rest[(slash + 1)..];
        if (!envId.Equals(_envId, StringComparison.Ordinal) ||
            !bucket.Equals(_bucket, StringComparison.Ordinal) ||
            !IsValidObjectName(path))
        {
            return false;
        }

        objectName = path;
        return true;
    }

    private void AddPending(string objectName)
    {
        if (!_resolved.ContainsKey(objectName))
        {
            _pending.Add(objectName);
            _resolved[objectName] = null;
        }
    }

    private static bool IsValidObjectName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('/') || name.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        return !name.Split('/').Any(segment => segment == "..");
    }

    [GeneratedRegex(@"!\[[^\]]*\]\[(?<label>[^\]]+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex ImageReferenceUse();

    [GeneratedRegex(@"!\[(?<alt>[^\]]*)\]\((?<url>[^)\s]+)(?<title>\s+""[^""]*"")?\)", RegexOptions.CultureInvariant)]
    private static partial Regex InlineImage();

    [GeneratedRegex(@"(?<!!)\[(?<text>[^\]]*)\]\((?<url>[^)\s]+)(?<title>\s+""[^""]*"")?\)", RegexOptions.CultureInvariant)]
    private static partial Regex InlineLink();

    [GeneratedRegex(@"(?m)^(?<prefix>\s*\[(?<label>[^\]]+)\]:\s*)(?<url>\S+)(?<rest>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceDefinition();

    [GeneratedRegex(@"(?<!!)\[(?<text>[^\]]*)\]\[(?<label>[^\]]+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex LinkReferenceUse();

    [GeneratedRegex(@"<(?<url>cloud://[^>]+)>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CloudBaseAutolink();
}
