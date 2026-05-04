using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Bukit.Content;
using Bukit.Content.Media;
using Bukit.Routing;

namespace Bukit.Engine.Incremental;

internal static class IncrementalBuildEngine
{
    private const string BodyFingerprintKey = "bodyFingerprint";

    internal static string ComputeContentHash(ContentItem item, IContentBodyStore bodyStore)
    {
        var metadataHash = ComputeMetadataHash(item);
        if (TryComputeStableContentHash(item, bodyStore, metadataHash, out var stableContentHash))
        {
            return stableContentHash;
        }

        return ComputeContentHash(item, metadataHash, ContentBodyResolver.GetHtml(item, bodyStore));
    }

    internal static string ComputeMetadataHash(ContentItem item)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';

        AppendUtf8(hasher, item.Id);
        hasher.AppendData(newline);
        AppendUtf8(hasher, item.Title);
        hasher.AppendData(newline);
        AppendUtf8(hasher, item.Slug);
        hasher.AppendData(newline);
        AppendUtf8(hasher, item.PublishAt.ToString("O"));
        hasher.AppendData(newline);

        var type = item.Meta.TryGetValue("type", out var typeObj) && typeObj is not null ? typeObj.ToString() : string.Empty;
        AppendUtf8(hasher, type);
        hasher.AppendData(newline);

        var summary = item.Meta.TryGetValue("summary", out var summaryObj) && summaryObj is not null ? summaryObj.ToString() : string.Empty;
        AppendUtf8(hasher, summary);
        hasher.AppendData(newline);

        AppendFieldsFingerprint(hasher, item.Fields);

        var digest = hasher.GetHashAndReset();
        return HashUtil.ToHexLower(digest);
    }

    internal static string ComputeStableContentHash(ContentItem item, string metadataHash)
    {
        if (!TryGetBodyFingerprint(item, out var bodyFingerprint))
        {
            throw new InvalidOperationException($"No stable body fingerprint is available for item '{item.Id}'.");
        }

        return HashUtil.Sha256Hex(string.Join("\n", metadataHash, bodyFingerprint));
    }

    internal static bool TryComputeStableContentHash(
        ContentItem item,
        IContentBodyStore bodyStore,
        string metadataHash,
        out string contentHash)
    {
        contentHash = string.Empty;
        if (bodyStore is LocalizedContentBodyStore)
        {
            return false;
        }

        if (!TryGetBodyFingerprint(item, out var bodyFingerprint))
        {
            return false;
        }

        contentHash = HashUtil.Sha256Hex(string.Join("\n", metadataHash, bodyFingerprint));
        return true;
    }

    internal static string ComputeContentHash(ContentItem item, string metadataHash, string contentHtml)
    {
        return HashUtil.Sha256Hex(string.Join("\n", metadataHash, contentHtml ?? string.Empty));
    }

    internal static string ComputeRouteHash(RouteInfo route)
    {
        var fingerprint = string.Join("\n", route.Url, BuildPathUtils.NormalizeRelPath(route.OutputPath), route.Template);
        return HashUtil.Sha256Hex(fingerprint);
    }

    internal static string ComputeListContentHash(
        string templateHash,
        string template,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> source,
        BuildManifest manifest,
        IContentBodyStore bodyStore,
        bool includeContent)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';

        AppendUtf8(hasher, templateHash);
        hasher.AppendData(newline);
        AppendUtf8(hasher, template);

        foreach (var (item, route) in source)
        {
            hasher.AppendData(newline);
            AppendUtf8(hasher, route.Url);
            hasher.AppendData(newline);

            var k = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            AppendUtf8(hasher, k);
            hasher.AppendData(newline);

            if (manifest.Entries.TryGetValue(k, out var entry) && entry is not null)
            {
                AppendUtf8(hasher, entry.ContentHash);
                hasher.AppendData(newline);
                AppendUtf8(hasher, entry.RouteHash);
            }
            else
            {
                AppendUtf8(hasher, ComputeListItemHash(item, bodyStore, includeContent));
                hasher.AppendData(newline);
                AppendUtf8(hasher, ComputeRouteHash(route));
            }
        }

        var digest = hasher.GetHashAndReset();
        return HashUtil.ToHexLower(digest);
    }

    private static string ComputeListItemHash(ContentItem item, IContentBodyStore bodyStore, bool includeContent)
    {
        var metadataHash = ComputeMetadataHash(item);

        if (includeContent)
        {
            if (TryComputeStableContentHash(item, bodyStore, metadataHash, out var stableContentHash))
            {
                return stableContentHash;
            }

            return ComputeContentHash(item, metadataHash, ContentBodyResolver.GetHtml(item, bodyStore));
        }

        return metadataHash;
    }

    private static bool TryGetBodyFingerprint(ContentItem item, out string bodyFingerprint)
    {
        bodyFingerprint = string.Empty;
        if (item.Meta.TryGetValue(BodyFingerprintKey, out var bodyFingerprintObj) &&
            bodyFingerprintObj is not null &&
            !string.IsNullOrWhiteSpace(bodyFingerprintObj.ToString()))
        {
            bodyFingerprint = bodyFingerprintObj.ToString()!.Trim();
            return true;
        }

        if (!string.IsNullOrEmpty(item.ContentHtml))
        {
            bodyFingerprint = HashUtil.Sha256Hex(item.ContentHtml);
            return true;
        }

        return false;
    }

    internal static void AppendUtf8(IncrementalHash hasher, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var maxByteCount = Encoding.UTF8.GetMaxByteCount(text.Length);
        var buffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
        try
        {
            var written = Encoding.UTF8.GetBytes(text, 0, text.Length, buffer, 0);
            hasher.AppendData(buffer.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void AppendFieldsFingerprint(IncrementalHash hasher, IReadOnlyDictionary<string, ContentField>? fields)
    {
        if (fields is null || fields.Count == 0)
        {
            return;
        }

        var keys = fields.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';
        var wroteAny = false;

        foreach (var k in keys)
        {
            if (!fields.TryGetValue(k, out var f))
            {
                continue;
            }

            if (wroteAny)
            {
                hasher.AppendData(newline);
            }

            AppendUtf8(hasher, k);
            hasher.AppendData(newline);
            AppendUtf8(hasher, f.Type ?? string.Empty);
            hasher.AppendData(newline);
            AppendFieldValue(hasher, f.Value);
            wroteAny = true;
        }
    }

    private static void AppendFieldValue(IncrementalHash hasher, object? value)
    {
        if (value is null)
        {
            return;
        }

        if (value is DateTimeOffset dto)
        {
            AppendUtf8(hasher, dto.ToString("O"));
            return;
        }

        if (value is DateTime dt)
        {
            AppendUtf8(hasher, dt.ToUniversalTime().ToString("O"));
            return;
        }

        if (value is string s)
        {
            AppendUtf8(hasher, s);
            return;
        }

        if (value is IEnumerable<object> seq)
        {
            var first = true;
            Span<byte> comma = stackalloc byte[1];
            comma[0] = (byte)',';
            foreach (var v in seq)
            {
                if (!first)
                {
                    hasher.AppendData(comma);
                }

                AppendFieldValue(hasher, v);
                first = false;
            }

            return;
        }

        if (value is IEnumerable<string> sseq)
        {
            var first = true;
            Span<byte> comma = stackalloc byte[1];
            comma[0] = (byte)',';
            foreach (var v in sseq)
            {
                if (!first)
                {
                    hasher.AppendData(comma);
                }

                AppendUtf8(hasher, v);
                first = false;
            }

            return;
        }

        AppendUtf8(hasher, value.ToString() ?? string.Empty);
    }
}
