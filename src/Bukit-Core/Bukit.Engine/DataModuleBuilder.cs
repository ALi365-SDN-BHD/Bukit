using Bukit.Content;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Shared;
using Bukit.Engine.Content;
using System.Globalization;
using System.Net.Mail;

namespace Bukit.Engine;

internal static class DataModuleBuilder
{
    internal static async Task<IReadOnlyDictionary<string, IReadOnlyList<ModuleInfo>>?> BuildModulesAsync(IReadOnlyList<ContentDocument> dataDocuments, string language, IContentBodyStore bodyStore, CancellationToken cancellationToken = default)
    {
        if (dataDocuments.Count == 0)
        {
            return null;
        }

        var map = new Dictionary<string, List<ModuleInfo>>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in dataDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enabled = ContentFieldReader.GetBool(document.CustomFields, "enabled");
            if (enabled is false)
            {
                continue;
            }

            var type = ContentFieldReader.GetContentType(document).Trim();
            if (string.IsNullOrWhiteSpace(type))
            {
                type = ContentFieldReader.GetText(document.CustomFields, "sourceKey") ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                type = "module";
            }

            if (!map.TryGetValue(type, out var list))
            {
                list = new List<ModuleInfo>();
                map[type] = list;
            }

            var html = await ContentBodyResolver.GetHtmlAsync(document, bodyStore, cancellationToken).ConfigureAwait(false);
            list.Add(new ModuleInfo
            {
                Id = document.Id,
                Title = document.Title,
                Slug = document.Slug,
                Content = html,
                Fields = document.CustomFields
            });
        }

        var result = new Dictionary<string, IReadOnlyList<ModuleInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in map)
        {
            var ordered = kv.Value
                .OrderBy(x => ContentFieldReader.GetNumber(x.Fields, "order") ?? 0d)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result[kv.Key] = ordered;
        }

        return result;
    }

    internal static async Task<IReadOnlyDictionary<string, object>?> BuildDataBySourceAsync(IReadOnlyList<ContentDocument> dataDocuments, IContentBodyStore bodyStore, CancellationToken cancellationToken = default)
    {
        if (dataDocuments.Count == 0)
        {
            return null;
        }

        var map = new Dictionary<string, List<ModuleInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in dataDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceKey = ContentFieldReader.GetText(document.CustomFields, "sourceKey") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sourceKey))
            {
                continue;
            }

            var enabled = ContentFieldReader.GetBool(document.CustomFields, "enabled");
            if (enabled is false)
            {
                continue;
            }

            if (!map.TryGetValue(sourceKey, out var list))
            {
                list = new List<ModuleInfo>();
                map[sourceKey] = list;
            }

            var html = await ContentBodyResolver.GetHtmlAsync(document, bodyStore, cancellationToken).ConfigureAwait(false);
            list.Add(new ModuleInfo
            {
                Id = document.Id,
                Title = document.Title,
                Slug = document.Slug,
                Content = html,
                Fields = document.CustomFields
            });
        }

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in map)
        {
            result[kv.Key] = kv.Value
                .OrderBy(x => ContentFieldReader.GetNumber(x.Fields, "order") ?? 0d)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return result.Count == 0 ? null : result;
    }

    internal static IReadOnlyDictionary<string, object>? BuildDataIndex(
        IReadOnlyList<ContentDocument> dataDocuments,
        IReadOnlyList<ContentSourceConfig>? sources)
    {
        var indexedSources = (sources ?? Array.Empty<ContentSourceConfig>())
            .Where(source => source.DataIndex is not null && !string.IsNullOrWhiteSpace(source.Name))
            .ToDictionary(source => source.Name!.Trim(), StringComparer.OrdinalIgnoreCase);
        if (indexedSources.Count == 0)
        {
            return null;
        }

        var valuesBySource = indexedSources.Keys.ToDictionary(
            sourceName => sourceName,
            _ => new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal),
            StringComparer.OrdinalIgnoreCase);

        foreach (var document in dataDocuments)
        {
            var sourceName = ContentFieldReader.GetText(document.CustomFields, "sourceKey");
            if (string.IsNullOrWhiteSpace(sourceName) || !indexedSources.TryGetValue(sourceName, out var source))
            {
                continue;
            }

            if (ContentFieldReader.GetBool(document.CustomFields, "enabled") is false)
            {
                continue;
            }

            var config = source.DataIndex!;
            var scope = RequireIndexField(document, config.ScopeField, sourceName);
            var key = RequireIndexField(document, config.KeyField, sourceName);
            var valueType = RequireIndexField(document, config.ValueTypeField, sourceName).ToLowerInvariant();
            if (!IsIndexIdentifier(scope) || !IsIndexIdentifier(key))
            {
                throw new ContentException($"Data index source '{sourceName}' requires scope and key values to match ^[a-z][a-z0-9_]*$; content '{document.Id}' has '{scope}.{key}'.");
            }

            var context = $"Data index source '{sourceName}' content '{document.Id}'";
            var raw = ConfiguredContentFieldReader.TryGetField(document.CustomFields, config.ValueField, context, out var valueField)
                ? valueField.Value
                : null;
            // CloudBase structured fields are parsed JSON: their strings stay strings.
            var jsonTyped = string.Equals(document.Source.Provider, "cloudbase", StringComparison.OrdinalIgnoreCase);
            var value = ReadIndexValue($"{context} key '{scope}.{key}'", valueType, raw, jsonTyped);

            var scopes = valuesBySource[sourceName];
            if (!scopes.TryGetValue(scope, out var keys))
            {
                keys = new Dictionary<string, object>(StringComparer.Ordinal);
                scopes[scope] = keys;
            }

            if (!keys.TryAdd(key, value))
            {
                throw new ContentException($"Data index source '{sourceName}' contains duplicate key '{scope}.{key}'.");
            }
        }

        foreach (var (sourceName, source) in indexedSources)
        {
            var scopes = valuesBySource[sourceName];
            foreach (var requiredKey in source.DataIndex!.RequiredKeys ?? Array.Empty<string>())
            {
                var parts = requiredKey.Split('.', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2 ||
                    !scopes.TryGetValue(parts[0], out var keys) ||
                    !keys.TryGetValue(parts[1], out var value) ||
                    DataIndexObjectValue.IsEmpty(value))
                {
                    throw new ContentException($"Data index source '{sourceName}' required key '{requiredKey}' is missing or empty.");
                }
            }
        }

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sourceName, scopes) in valuesBySource)
        {
            var sourceObject = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var (scope, keys) in scopes)
            {
                sourceObject[scope] = keys;
            }

            result[sourceName] = sourceObject;
        }

        return result;
    }

    private static string RequireIndexField(ContentDocument document, string fieldName, string sourceName)
    {
        var context = $"Data index source '{sourceName}' content '{document.Id}'";
        var value = ConfiguredContentFieldReader.TryGetField(document.CustomFields, fieldName, context, out var field)
            ? field.Value?.ToString()?.Trim()
            : null;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ContentException($"Data index source '{sourceName}' content '{document.Id}' is missing field '{fieldName}'.");
        }

        return value;
    }

    /// <param name="context">Names the source, content id and <c>scope.key</c>.</param>
    private static object ReadIndexValue(string context, string valueType, object? raw, bool jsonTyped)
    {
        if (valueType is DataIndexObjectValue.ObjectType or DataIndexObjectValue.ObjectArrayType)
        {
            return DataIndexObjectValue.Read(raw, valueType == DataIndexObjectValue.ObjectArrayType, context, jsonTyped);
        }

        if (valueType is not ("text" or "multiline" or "email" or "phone" or "url"))
        {
            throw new ContentException($"{context} has unsupported value type '{valueType}'; use text, multiline, email, phone, url, object or object[].");
        }

        // A map or list has no text form; its ToString() is a .NET type name.
        if (raw is System.Collections.IDictionary or (System.Collections.IEnumerable and not string))
        {
            var shape = raw is System.Collections.IDictionary or IEnumerable<KeyValuePair<string, object?>> ? "an object" : "a list";
            throw new ContentException($"{context} has value type '{valueType}' but its value is {shape}; use value type 'object' or 'object[]'.");
        }

        // Invariant, so a number or date builds the same text on every machine.
        var value = (raw is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : raw?.ToString())?.Trim() ?? string.Empty;
        if (valueType == "email" && value.Length > 0 && !MailAddress.TryCreate(value, out _))
        {
            throw new ContentException($"{context} has invalid email value.");
        }

        if (valueType == "url" && value.Length > 0 && !IsAllowedIndexUrl(value))
        {
            throw new ContentException($"{context} has invalid URL value.");
        }

        return value;
    }

    private static bool IsAllowedIndexUrl(string value)
    {
        if (value.StartsWith("/", StringComparison.Ordinal))
        {
            return !value.StartsWith("//", StringComparison.Ordinal) &&
                   !value.Contains('\\') &&
                   !value.Any(char.IsWhiteSpace) &&
                   Uri.TryCreate(value, UriKind.Relative, out _);
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               uri.Scheme is "http" or "https";
    }

    private static bool IsIndexIdentifier(string value)
    {
        if (value.Length == 0 || value[0] is < 'a' or > 'z')
        {
            return false;
        }

        return value.Skip(1).All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
    }

}
