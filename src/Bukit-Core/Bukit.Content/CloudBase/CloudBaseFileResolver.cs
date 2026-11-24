using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Bukit.CloudBase.Transport;
using Bukit.Content.Markdown;

namespace Bukit.Content.CloudBase;

/// <summary>
/// Signs CloudBase file objects for the two allowed positions: fileFields values and body image
/// nodes. Body references are collected from the parsed Markdown, so code fences, inline code and
/// raw HTML are never rewritten (D17).
/// </summary>
internal sealed class CloudBaseFileResolver
{
    private readonly CloudBaseClient _client;
    private readonly string _envId;
    private readonly string _bucket;
    private readonly string _placeholder;
    private readonly int _expiresSeconds;
    private readonly Dictionary<string, string?> _resolved = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _bodyObjects = new(StringComparer.Ordinal);
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

    internal void CollectMarkdownReferences(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return;
        }

        var document = BasicMarkdownToHtml.ParseDocument(markdown);
        foreach (var link in document.Descendants<LinkInline>())
        {
            var url = link.Url;
            if (url is null || !url.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!link.IsImage)
            {
                UnsupportedReferences++;
                continue;
            }

            if (TryParseCloudReference(url, out var objectName))
            {
                _bodyObjects[url] = objectName;
                AddPending(objectName);
            }
            else
            {
                ResolveFailures++;
            }
        }

        foreach (var autolink in document.Descendants<AutolinkInline>())
        {
            if (autolink.Url?.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase) == true)
            {
                UnsupportedReferences++;
            }
        }
    }

    /// <summary>Maps each original body cloud:// URL to its signed URL or the media placeholder.</summary>
    internal IReadOnlyDictionary<string, string> BuildBodyUrlMap()
    {
        var map = new Dictionary<string, string>(_bodyObjects.Count, StringComparer.Ordinal);
        foreach (var (url, objectName) in _bodyObjects)
        {
            map[url] = Resolve(objectName);
        }

        return map;
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
}
