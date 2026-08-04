using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        var writer = new CanonicalFingerprintWriter(hasher);
        writer.BeginRecord("ListContentMetadata", 2);
        AppendContentRecord(writer, document.Record);
        AppendFieldsFingerprint(writer, document.CustomFields);

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

            // Always compute from current inputs, never backfill from prior manifest
            AppendUtf8(hasher, ComputeListDocumentHash(document, bodyStore, includeContent));
            hasher.AppendData(newline);
            AppendUtf8(hasher, ComputeRouteHash(route));
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

            // Always compute from current inputs, never backfill from prior manifest
            var itemHash = await ComputeListDocumentHashAsync(document, bodyStore, includeContent, cancellationToken).ConfigureAwait(false);
            AppendUtf8(hasher, itemHash);
            hasher.AppendData(newline);
            AppendUtf8(hasher, ComputeRouteHash(route));
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

    private static void AppendContentRecord(CanonicalFingerprintWriter writer, ContentRecord record)
    {
        writer.BeginRecord("ContentRecord", 10);
        AppendIdentity(writer, record.Identity);
        AppendPresentation(writer, record.Presentation);
        AppendClassification(writer, record.Classification);
        AppendOwnership(writer, record.Ownership);
        AppendLifecycle(writer, record.Lifecycle);
        AppendProvenance(writer, record.Provenance);
        AppendTrust(writer, record.Trust);
        writer.WriteSequence(record.Entities, value => AppendEntity(writer, value));
        writer.WriteSequence(record.Relations, value => AppendRelation(writer, value));
        writer.WriteSequence(record.Media, value => AppendMedia(writer, value));
    }

    private static void AppendIdentity(CanonicalFingerprintWriter writer, ContentIdentity value)
    {
        writer.BeginRecord("ContentIdentity", 5);
        writer.WriteString(value.Id);
        writer.WriteString(value.Slug);
        writer.WriteString(value.CanonicalUrlKey);
        writer.WriteString(value.ContentType);
        writer.WriteString(value.Status);
    }

    private static void AppendPresentation(CanonicalFingerprintWriter writer, ContentPresentation value)
    {
        writer.BeginRecord("ContentPresentation", 5);
        writer.WriteString(value.Title);
        writer.WriteString(value.Summary);
        writer.WriteString(value.Body);
        writer.WriteString(value.Language);
        writer.WriteSequence(value.Translations, writer.WriteString);
    }

    private static void AppendClassification(CanonicalFingerprintWriter writer, ContentClassification value)
    {
        writer.BeginRecord("ContentClassification", 4);
        writer.WriteString(value.Type);
        writer.WriteString(value.Collection);
        writer.WriteSequence(value.Sections, writer.WriteString);
        writer.WriteSequence(value.Tags, writer.WriteString);
    }

    private static void AppendOwnership(CanonicalFingerprintWriter writer, ContentOwnership value)
    {
        writer.BeginRecord("ContentOwnership", 5);
        writer.WriteString(value.Author);
        writer.WriteString(value.Organization);
        writer.WriteString(value.Owner);
        writer.WriteString(value.Reviewer);
        writer.WriteString(value.AuthorType);
    }

    private static void AppendLifecycle(CanonicalFingerprintWriter writer, ContentLifecycle value)
    {
        writer.BeginRecord("ContentLifecycle", 5);
        writer.WriteDateTimeOffset(value.PublishedAt);
        writer.WriteNullableDateTimeOffset(value.UpdatedAt);
        writer.WriteNullableDateTimeOffset(value.ExpiresAt);
        writer.WriteNullableDateTimeOffset(value.ReviewedAt);
        writer.WriteBoolean(value.Evergreen);
    }

    private static void AppendProvenance(CanonicalFingerprintWriter writer, ProvenanceRecord value)
    {
        writer.BeginRecord("ProvenanceRecord", 5);
        writer.WriteString(value.Source);
        writer.WriteString(value.OriginalSource);
        writer.WriteSequence(value.Citations, writer.WriteString);
        writer.WriteSequence(value.References, writer.WriteString);
        writer.WriteString(value.SyncStatus);
    }

    private static void AppendTrust(CanonicalFingerprintWriter writer, TrustMetadata value)
    {
        writer.BeginRecord("TrustMetadata", 3);
        if (value.CredibilityScore is { } credibilityScore)
        {
            writer.WriteDouble(credibilityScore);
        }
        else
        {
            writer.WriteNull();
        }

        writer.WriteString(value.ReviewStatus);
        writer.WriteSequence(value.QualityFlags, writer.WriteString);
    }

    private static void AppendEntity(CanonicalFingerprintWriter writer, EntityRecord value)
    {
        writer.BeginRecord("EntityRecord", 6);
        writer.WriteString(value.Type);
        writer.WriteString(value.Name);
        writer.WriteString(value.Description);
        writer.WriteString(value.Id);
        writer.WriteString(value.Url);
        writer.WriteSequence(value.SameAs, writer.WriteString);
    }

    private static void AppendRelation(CanonicalFingerprintWriter writer, ContentRelation value)
    {
        writer.BeginRecord("ContentRelation", 4);
        writer.WriteString(value.Type);
        writer.WriteString(value.Target);
        writer.WriteString(value.TargetType);
        writer.WriteString(value.TargetId);
    }

    private static void AppendMedia(CanonicalFingerprintWriter writer, MediaAsset value)
    {
        writer.BeginRecord("MediaAsset", 6);
        writer.WriteString(value.Kind);
        writer.WriteString(value.Url);
        writer.WriteString(value.Alt);
        writer.WriteString(value.Caption);
        writer.WriteString(value.Description);
        writer.WriteString(value.License);
    }

    private static void AppendFieldsFingerprint(
        CanonicalFingerprintWriter writer,
        IReadOnlyDictionary<string, ContentField>? fields)
    {
        if (fields is null)
        {
            writer.WriteNull();
            return;
        }

        writer.BeginMap(fields.Count);
        foreach (var field in fields.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteString(field.Key);
            writer.BeginRecord("ContentField", 2);
            writer.WriteString(field.Value.Type);
            writer.WriteValue(field.Value.Value);
        }
    }

    private sealed class CanonicalFingerprintWriter
    {
        private const string UnsupportedValueMessage = "Unsupported content field value in incremental fingerprint.";
        private readonly HashSet<object> _activeCompounds = new(ReferenceEqualityComparer.Instance);
        private readonly IncrementalHash _hasher;

        internal CanonicalFingerprintWriter(IncrementalHash hasher) => _hasher = hasher;

        internal void BeginRecord(string typeName, int memberCount)
        {
            AppendTag(ValueTag.Record);
            AppendLength(memberCount);
            AppendTextPayload(typeName);
        }

        internal void BeginMap(int count)
        {
            AppendTag(ValueTag.Map);
            AppendLength(count);
        }

        internal void WriteNull() => AppendTag(ValueTag.Null);

        internal void WriteString(string? value)
        {
            if (value is null)
            {
                WriteNull();
                return;
            }

            AppendTag(ValueTag.String);
            AppendTextPayload(value);
        }

        internal void WriteBoolean(bool value)
        {
            AppendTag(ValueTag.Boolean);
            Span<byte> data = stackalloc byte[1];
            data[0] = value ? (byte)1 : (byte)0;
            _hasher.AppendData(data);
        }

        internal void WriteDouble(double value)
            => AppendInvariant(ValueTag.Double, value.ToString("R", CultureInfo.InvariantCulture));

        internal void WriteDateTimeOffset(DateTimeOffset value)
            => AppendInvariant(ValueTag.DateTimeOffset, value.ToString("O", CultureInfo.InvariantCulture));

        internal void WriteNullableDateTimeOffset(DateTimeOffset? value)
        {
            if (value is { } actual)
            {
                WriteDateTimeOffset(actual);
            }
            else
            {
                WriteNull();
            }
        }

        internal void WriteSequence<T>(IReadOnlyList<T>? values, Action<T> writeValue)
        {
            if (values is null)
            {
                WriteNull();
                return;
            }

            AppendTag(ValueTag.Sequence);
            AppendLength(values.Count);
            foreach (var value in values)
            {
                writeValue(value);
            }
        }

        internal void WriteValue(object? value)
        {
            switch (value)
            {
                case null:
                    WriteNull();
                    return;
                case string text:
                    WriteString(text);
                    return;
                case bool boolean:
                    WriteBoolean(boolean);
                    return;
                case char character:
                    AppendInvariant(ValueTag.Character, character.ToString());
                    return;
                case sbyte number:
                    AppendInvariant(ValueTag.SByte, number.ToString(CultureInfo.InvariantCulture));
                    return;
                case byte number:
                    AppendInvariant(ValueTag.Byte, number.ToString(CultureInfo.InvariantCulture));
                    return;
                case short number:
                    AppendInvariant(ValueTag.Int16, number.ToString(CultureInfo.InvariantCulture));
                    return;
                case ushort number:
                    AppendInvariant(ValueTag.UInt16, number.ToString(CultureInfo.InvariantCulture));
                    return;
                case int number:
                    AppendInvariant(ValueTag.Int32, number.ToString(CultureInfo.InvariantCulture));
                    return;
                case uint number:
                    AppendInvariant(ValueTag.UInt32, number.ToString(CultureInfo.InvariantCulture));
                    return;
                case long number:
                    AppendInvariant(ValueTag.Int64, number.ToString(CultureInfo.InvariantCulture));
                    return;
                case ulong number:
                    AppendInvariant(ValueTag.UInt64, number.ToString(CultureInfo.InvariantCulture));
                    return;
                case float number:
                    AppendInvariant(ValueTag.Single, number.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case double number:
                    WriteDouble(number);
                    return;
                case decimal number:
                    AppendInvariant(ValueTag.Decimal, number.ToString("G29", CultureInfo.InvariantCulture));
                    return;
                case DateTime dateTime:
                    AppendInvariant(ValueTag.DateTime, dateTime.ToString("O", CultureInfo.InvariantCulture));
                    return;
                case DateTimeOffset dateTimeOffset:
                    WriteDateTimeOffset(dateTimeOffset);
                    return;
                case DateOnly date:
                    AppendInvariant(ValueTag.DateOnly, date.ToString("O", CultureInfo.InvariantCulture));
                    return;
                case TimeOnly time:
                    AppendInvariant(ValueTag.TimeOnly, time.ToString("O", CultureInfo.InvariantCulture));
                    return;
                case TimeSpan duration:
                    AppendInvariant(ValueTag.TimeSpan, duration.ToString("c", CultureInfo.InvariantCulture));
                    return;
                case Guid guid:
                    AppendInvariant(ValueTag.Guid, guid.ToString("D"));
                    return;
                case Uri uri:
                    AppendInvariant(ValueTag.Uri, uri.OriginalString);
                    return;
                case byte[] bytes:
                    AppendTag(ValueTag.Bytes);
                    AppendLength(bytes.Length);
                    _hasher.AppendData(bytes);
                    return;
                case JsonElement json:
                    WriteJsonElement(json);
                    return;
                case TableOfContentsEntry entry:
                    BeginRecord("TableOfContentsEntry", 3);
                    WriteValue(entry.Level);
                    WriteString(entry.Text);
                    WriteString(entry.Id);
                    return;
                case IDictionary dictionary:
                    WriteDictionary(dictionary);
                    return;
                case IEnumerable<KeyValuePair<string, object?>> dictionary:
                    WriteDictionary(dictionary);
                    return;
                case IEnumerable sequence:
                    WriteEnumerable(sequence);
                    return;
                default:
                    throw new InvalidOperationException(UnsupportedValueMessage);
            }
        }

        private void WriteJsonElement(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    WriteNull();
                    return;
                case JsonValueKind.String:
                    WriteString(value.GetString());
                    return;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    WriteBoolean(value.GetBoolean());
                    return;
                case JsonValueKind.Number when value.TryGetInt64(out var signed):
                    AppendInvariant(ValueTag.Int64, signed.ToString(CultureInfo.InvariantCulture));
                    return;
                case JsonValueKind.Number when value.TryGetUInt64(out var unsigned):
                    AppendInvariant(ValueTag.UInt64, unsigned.ToString(CultureInfo.InvariantCulture));
                    return;
                case JsonValueKind.Number when value.TryGetDecimal(out var decimalValue):
                    AppendInvariant(ValueTag.Decimal, decimalValue.ToString("G29", CultureInfo.InvariantCulture));
                    return;
                case JsonValueKind.Number:
                    WriteDouble(value.GetDouble());
                    return;
                case JsonValueKind.Array:
                    {
                        var items = value.EnumerateArray().ToArray();
                        AppendTag(ValueTag.Sequence);
                        AppendLength(items.Length);
                        foreach (var item in items)
                        {
                            WriteJsonElement(item);
                        }

                        return;
                    }
                case JsonValueKind.Object:
                    {
                        var properties = value.EnumerateObject()
                            .OrderBy(static property => property.Name, StringComparer.Ordinal)
                            .ToArray();
                        BeginMap(properties.Length);
                        foreach (var property in properties)
                        {
                            WriteString(property.Name);
                            WriteJsonElement(property.Value);
                        }

                        return;
                    }
                default:
                    throw new InvalidOperationException(UnsupportedValueMessage);
            }
        }

        private void WriteDictionary(IDictionary dictionary)
        {
            EnterCompound(dictionary);
            try
            {
                var entries = new List<KeyValuePair<string, object?>>(dictionary.Count);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not string key)
                    {
                        throw new InvalidOperationException(UnsupportedValueMessage);
                    }

                    entries.Add(new KeyValuePair<string, object?>(key, entry.Value));
                }

                WriteDictionaryEntries(entries);
            }
            finally
            {
                ExitCompound(dictionary);
            }
        }

        private void WriteDictionary(IEnumerable<KeyValuePair<string, object?>> dictionary)
        {
            EnterCompound(dictionary);
            try
            {
                WriteDictionaryEntries(dictionary.ToList());
            }
            finally
            {
                ExitCompound(dictionary);
            }
        }

        private void WriteDictionaryEntries(List<KeyValuePair<string, object?>> entries)
        {
            entries.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
            for (var index = 1; index < entries.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(entries[index - 1].Key, entries[index].Key))
                {
                    throw new InvalidOperationException(UnsupportedValueMessage);
                }
            }

            BeginMap(entries.Count);
            foreach (var entry in entries)
            {
                WriteString(entry.Key);
                WriteValue(entry.Value);
            }
        }

        private void WriteEnumerable(IEnumerable sequence)
        {
            EnterCompound(sequence);
            try
            {
                var values = sequence.Cast<object?>().ToList();
                AppendTag(ValueTag.Sequence);
                AppendLength(values.Count);
                foreach (var value in values)
                {
                    WriteValue(value);
                }
            }
            finally
            {
                ExitCompound(sequence);
            }
        }

        private void EnterCompound(object value)
        {
            if (!_activeCompounds.Add(value))
            {
                throw new InvalidOperationException("Cyclic content field value in incremental fingerprint.");
            }
        }

        private void ExitCompound(object value) => _activeCompounds.Remove(value);

        private void AppendInvariant(ValueTag tag, string value)
        {
            AppendTag(tag);
            AppendTextPayload(value);
        }

        private void AppendTextPayload(string value)
        {
            AppendLength(Encoding.UTF8.GetByteCount(value));
            AppendUtf8(_hasher, value);
        }

        private void AppendTag(ValueTag tag)
        {
            Span<byte> data = stackalloc byte[1];
            data[0] = (byte)tag;
            _hasher.AppendData(data);
        }

        private void AppendLength(int value)
        {
            Span<byte> data = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(data, value);
            _hasher.AppendData(data);
        }

        private enum ValueTag : byte
        {
            Null,
            String,
            Boolean,
            Character,
            SByte,
            Byte,
            Int16,
            UInt16,
            Int32,
            UInt32,
            Int64,
            UInt64,
            Single,
            Double,
            Decimal,
            DateTime,
            DateTimeOffset,
            DateOnly,
            TimeOnly,
            TimeSpan,
            Guid,
            Uri,
            Bytes,
            Sequence,
            Map,
            Record
        }
    }
}
