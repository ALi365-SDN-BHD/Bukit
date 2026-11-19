using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine.Content;

internal static class ConfiguredContentFieldReader
{
    internal static bool TryGetField(
        IReadOnlyDictionary<string, ContentField>? fields,
        string configuredName,
        string context,
        out ContentField field)
    {
        if (ContentFieldReader.TryGetField(fields, configuredName, out field))
        {
            return true;
        }

        var matches = AliasMatches(fields, configuredName).ToList();
        if (matches.Count > 1)
        {
            throw new ContentException($"{context} has ambiguous aliases for field '{configuredName}'.");
        }

        if (matches.Count == 1)
        {
            field = matches[0];
            return true;
        }

        field = default!;
        return false;
    }

    /// <summary>
    /// Every field the configured name can refer to through its alias, the exact name included,
    /// without the ambiguity check, for callers that only ask whether any of them holds a value.
    /// </summary>
    internal static IEnumerable<ContentField> AliasMatches(IReadOnlyDictionary<string, ContentField>? fields, string configuredName)
    {
        var alias = NormalizeAlias(configuredName);
        return (fields ?? new Dictionary<string, ContentField>())
            .Where(pair => string.Equals(NormalizeAlias(pair.Key), alias, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value);
    }

    private static string NormalizeAlias(string value) =>
        value.Replace("_", string.Empty, StringComparison.Ordinal);
}
