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
    private const string ReferenceMismatch = "reference-mismatch";
    private const string InvalidObjectName = "invalid-object-name";

    private readonly CloudBaseClient _client;
    private readonly string _envId;
    private readonly string _bucket;
    private readonly string _placeholder;
    private readonly int _expiresSeconds;
    private readonly Dictionary<string, string?> _resolved = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _bodyObjects = new(StringComparer.Ordinal);
    private readonly List<string> _pending = [];
    private readonly Dictionary<string, List<string>> _entriesByObject = new(StringComparer.Ordinal);
    private readonly List<string> _failures = [];

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

    /// <summary>One line per failed entry: <c>{documentId}/{field or body}: {reason}</c>.</summary>
    internal IReadOnlyList<string> Failures => _failures;

    internal int UnsupportedReferences { get; private set; }

    internal string Placeholder => _placeholder;

    /// <summary>
    /// A fileFields value is an object name or a matching cloud:// reference. <paramref name="entry"/>
    /// names the position (<c>{documentId}/{field}</c>) for failure diagnostics.
    /// </summary>
    internal bool TryAddFieldValue(string? value, string entry, out string objectName)
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
                Fail(entry, ReferenceMismatch);
                return false;
            }

            objectName = parsed;
            AddPending(objectName, entry);
            return true;
        }

        if (!CloudBaseEndpoints.IsValidObjectName(value))
        {
            Fail(entry, InvalidObjectName);
            return false;
        }

        objectName = value;
        AddPending(objectName, entry);
        return true;
    }

    internal string Resolve(string objectName)
        => _resolved.TryGetValue(objectName, out var url) && url is not null ? url : _placeholder;

    internal void CollectMarkdownReferences(string markdown, string entry)
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
                AddPending(objectName, entry);
            }
            else
            {
                Fail(entry, ReferenceMismatch);
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
            results = await _client.SignObjectsAsync(_bucket, _pending, _expiresSeconds, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CloudBaseApiException ex)
        {
            // A whole-request failure (often 401/403 from a missing storage SELECT policy) names
            // its kind and status so the cause is visible, not just a placeholder count.
            var reason = ex.StatusCode is { } status
                ? $"sign-request-failed {ex.Kind} {status}"
                : $"sign-request-failed {ex.Kind}";
            foreach (var objectName in _pending)
            {
                FailObject(objectName, reason);
            }

            return;
        }

        foreach (var result in results)
        {
            if (result.Url is null)
            {
                FailObject(result.ObjectName, DescribeItemError(result.Error));
            }

            _resolved[result.ObjectName] = result.Url;
        }
    }

    /// <summary>Keeps transport-defined reasons; gateway free text is reduced to a fixed word.</summary>
    private static string DescribeItemError(string? error)
        => error is InvalidObjectName or "invalid-signed-url" or CloudBaseClient.SignMismatchError
            ? error
            : "object-unavailable";

    private void Fail(string entry, string reason) => _failures.Add($"{entry}: {reason}");

    private void FailObject(string objectName, string reason)
    {
        foreach (var entry in _entriesByObject[objectName])
        {
            Fail(entry, reason);
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

        // An empty bucket never matches, even when storageBucket is unset (no fileFields).
        if (bucket.Length == 0 ||
            !envId.Equals(_envId, StringComparison.Ordinal) ||
            !bucket.Equals(_bucket, StringComparison.Ordinal) ||
            !CloudBaseEndpoints.IsValidObjectName(path))
        {
            return false;
        }

        objectName = path;
        return true;
    }

    private void AddPending(string objectName, string entry)
    {
        if (!_resolved.ContainsKey(objectName))
        {
            _pending.Add(objectName);
            _resolved[objectName] = null;
            _entriesByObject[objectName] = [];
        }

        if (!_entriesByObject[objectName].Contains(entry, StringComparer.Ordinal))
        {
            _entriesByObject[objectName].Add(entry);
        }
    }
}
