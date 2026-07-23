using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class ContentCollectionContractValidator
{
    private const string UnknownSourceLabel = "unknown";

    public static void Validate(IReadOnlyList<RawContentDocument> documents)
    {
        foreach (var document in documents)
        {
            Validate(document);
        }
    }

    public static void Validate(RawContentDocument document)
    {
        var sourceMode = GetRawText(document, "sourceMode");
        if (string.Equals(sourceMode, "data", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var collection = GetRawText(document, "collection");
        if (!string.IsNullOrWhiteSpace(collection))
        {
            return;
        }

        var sourceKey = GetRawText(document, "sourceKey");
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            sourceKey = document.Source.SourceKey;
        }

        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            sourceKey = UnknownSourceLabel;
        }

        throw new ConfigException(
            $"Content \"{document.Id}\" from source \"{sourceKey}\" is missing required collection. " +
            "Set content.sources[].collection or item collection metadata.",
            DiagnosticCode.ContentCollectionMissing);
    }

    private static string? GetRawText(RawContentDocument document, string key)
    {
        object? value;
        if (ContentFieldReader.TryGetField(document.CustomFields, key, out var field))
        {
            value = field.Value;
        }
        else if (document.Properties is not null &&
                 document.Properties.TryGetValue(key, out var property))
        {
            value = property.Value;
        }
        else
        {
            return null;
        }

        var text = value?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
