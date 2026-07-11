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

        var alias = NormalizeAlias(configuredName);
        var matches = (fields ?? new Dictionary<string, ContentField>())
            .Where(pair => string.Equals(NormalizeAlias(pair.Key), alias, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count > 1)
        {
            throw new ContentException($"{context} has ambiguous aliases for field '{configuredName}'.");
        }

        if (matches.Count == 1)
        {
            field = matches[0].Value;
            return true;
        }

        field = default!;
        return false;
    }

    private static string NormalizeAlias(string value) =>
        value.Replace("_", string.Empty, StringComparison.Ordinal);
}
