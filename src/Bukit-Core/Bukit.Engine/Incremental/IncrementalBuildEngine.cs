using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Media;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Engine.Incremental;

internal static class IncrementalBuildEngine
{
    private const string BodyFingerprintKey = "bodyFingerprint";

    internal static async Task<string> ComputeContentHashAsync(ContentDocument document, IContentBodyStore bodyStore, CancellationToken cancellationToken = default)
    {
        var metadataHash = ComputeMetadataHash(document);
        if (TryComputeStableContentHash(document, bodyStore, metadataHash, out var stableContentHash))
        {
            return stableContentHash;
        }

        var html = await ContentBodyResolver.GetHtmlAsync(document, bodyStore, cancellationToken).ConfigureAwait(false);
        return ComputeContentHash(document, metadataHash, html);
    }

    internal static string ComputeMetadataHash(ContentDocument document)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';

        AppendUtf8(hasher, document.Id);
        hasher.AppendData(newline);
        AppendUtf8(hasher, document.Title);
        hasher.AppendData(newline);
        AppendUtf8(hasher, document.Slug);
        hasher.AppendData(newline);
        AppendUtf8(hasher, document.PublishAt.ToString("O"));
        hasher.AppendData(newline);

        AppendUtf8(hasher, document.Record.Identity.ContentType);
        hasher.AppendData(newline);

        AppendUtf8(hasher, document.Record.Presentation.Summary ?? string.Empty);
        hasher.AppendData(newline);

        AppendFieldsFingerprint(hasher, document.CustomFields);

        var digest = hasher.GetHashAndReset();
        return HashUtil.ToHexLower(digest);
    }

    internal static string ComputeStableContentHash(ContentDocument document, string metadataHash)
    {
        if (!TryGetBodyFingerprint(document, out var bodyFingerprint))
        {
            throw new InvalidOperationException($"No stable body fingerprint is available for document '{document.Id}'.");
        }

        return HashUtil.Sha256Hex(string.Join("\n", metadataHash, bodyFingerprint));
    }

    internal static bool TryComputeStableContentHash(
        ContentDocument document,
        IContentBodyStore bodyStore,
        string metadataHash,
        out string contentHash)
    {
        contentHash = string.Empty;
        if (bodyStore is LocalizedContentBodyStore)
        {
            return false;
        }

        if (!TryGetBodyFingerprint(document, out var bodyFingerprint))
        {
            return false;
        }

        contentHash = HashUtil.Sha256Hex(string.Join("\n", metadataHash, bodyFingerprint));
        return true;
    }

    internal static string ComputeContentHash(ContentDocument document, string metadataHash, string contentHtml)
    {
        return HashUtil.Sha256Hex(string.Join("\n", metadataHash, contentHtml ?? string.Empty));
    }

    internal static string ComputeRouteHash(RouteInfo route)
    {
        var fingerprint = string.Join("\n", route.Url, BuildPathUtils.NormalizeRelPath(route.OutputPath), route.Template);
        return HashUtil.Sha256Hex(fingerprint);
    }

    [Obsolete("Blocking. Use ComputeListContentHashAsync instead to avoid sync-over-async deadlocks.")]
    internal static string ComputeListContentHash(
        string templateHash,
        string template,
        IReadOnlyList<RoutedContentDocument> source,
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

        foreach (var routedDocument in source)
        {
            var document = routedDocument.Document;
            var route = routedDocument.Route;

            hasher.AppendData(newline);
            AppendUtf8(hasher, route.Url);
            hasher.AppendData(newline);

            var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            AppendUtf8(hasher, key);
            hasher.AppendData(newline);

            if (manifest.Entries.TryGetValue(key, out var entry) && entry is not null)
            {
                AppendUtf8(hasher, entry.ContentHash);
                hasher.AppendData(newline);
                AppendUtf8(hasher, entry.RouteHash);
            }
            else
            {
                AppendUtf8(hasher, ComputeListDocumentHash(document, bodyStore, includeContent));
                hasher.AppendData(newline);
                AppendUtf8(hasher, ComputeRouteHash(route));
            }
        }

        var digest = hasher.GetHashAndReset();
        return HashUtil.ToHexLower(digest);
    }

    internal static async Task<string> ComputeListContentHashAsync(
        string templateHash,
        string template,
        IReadOnlyList<RoutedContentDocument> source,
        BuildManifest manifest,
        IContentBodyStore bodyStore,
        bool includeContent,
        CancellationToken cancellationToken)
        => await ComputeListContentHashAsync(
            templateHash,
            template,
            source,
            manifest.Entries,
            bodyStore,
            includeContent,
            cancellationToken).ConfigureAwait(false);

    internal static async Task<string> ComputeListContentHashAsync(
        string templateHash,
        string template,
        IReadOnlyList<RoutedContentDocument> source,
        IReadOnlyDictionary<string, BuildManifestEntry> manifestEntries,
        IContentBodyStore bodyStore,
        bool includeContent,
        CancellationToken cancellationToken)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var newline = new byte[] { (byte)'\n' };

        AppendUtf8(hasher, templateHash);
        hasher.AppendData(newline);
        AppendUtf8(hasher, template);

        foreach (var routedDocument in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = routedDocument.Document;
            var route = routedDocument.Route;

            hasher.AppendData(newline);
            AppendUtf8(hasher, route.Url);
            hasher.AppendData(newline);

            var k = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            AppendUtf8(hasher, k);
            hasher.AppendData(newline);

            if (manifestEntries.TryGetValue(k, out var entry) && entry is not null)
            {
                AppendUtf8(hasher, entry.ContentHash);
                hasher.AppendData(newline);
                AppendUtf8(hasher, entry.RouteHash);
            }
            else
            {
                var itemHash = await ComputeListDocumentHashAsync(document, bodyStore, includeContent, cancellationToken).ConfigureAwait(false);
                AppendUtf8(hasher, itemHash);
                hasher.AppendData(newline);
                AppendUtf8(hasher, ComputeRouteHash(route));
            }
        }

        var digest = hasher.GetHashAndReset();
        return HashUtil.ToHexLower(digest);
    }

    private static async Task<string> ComputeListDocumentHashAsync(
        ContentDocument document,
        IContentBodyStore bodyStore,
        bool includeContent,
        CancellationToken cancellationToken)
    {
        var metadataHash = ComputeMetadataHash(document);

        if (includeContent)
        {
            if (TryComputeStableContentHash(document, bodyStore, metadataHash, out var stableContentHash))
            {
                return stableContentHash;
            }

            var html = await ContentBodyResolver.GetHtmlAsync(document, bodyStore, cancellationToken).ConfigureAwait(false);
            return ComputeContentHash(document, metadataHash, html);
        }

        return metadataHash;
    }

    [Obsolete("Blocking. Use ComputeListDocumentHashAsync instead to avoid sync-over-async deadlocks.")]
    private static string ComputeListDocumentHash(ContentDocument document, IContentBodyStore bodyStore, bool includeContent)
    {
        var metadataHash = ComputeMetadataHash(document);

        if (includeContent)
        {
            if (TryComputeStableContentHash(document, bodyStore, metadataHash, out var stableContentHash))
            {
                return stableContentHash;
            }

            // Fallback: use inline HTML if available to avoid blocking
            var html = !string.IsNullOrEmpty(document.Body.Html) ? document.Body.Html : string.Empty;
            return ComputeContentHash(document, metadataHash, html);
        }

        return metadataHash;
    }

    private static bool TryGetBodyFingerprint(ContentDocument document, out string bodyFingerprint)
    {
        bodyFingerprint = string.Empty;
        var bodyFingerprintValue = ContentFieldReader.GetText(document.CustomFields, BodyFingerprintKey);
        if (!string.IsNullOrWhiteSpace(bodyFingerprintValue))
        {
            bodyFingerprint = bodyFingerprintValue;
            return true;
        }

        if (!string.IsNullOrEmpty(document.Body.Html))
        {
            bodyFingerprint = HashUtil.Sha256Hex(document.Body.Html);
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

        AppendUtf8(hasher, value.ToString() ?? string.Empty);
    }
}
