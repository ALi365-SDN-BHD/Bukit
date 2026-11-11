using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine.Abstractions;

/// <summary>
/// The custom fields one page shows of another page (the <c>pages</c> list and section
/// <c>source</c> items). The <c>bodyFingerprint</c> field is left out: it identifies the body,
/// which is not part of the <c>pages</c> dependency hash, so a page that showed it could keep
/// stale text after another page's body edit.
/// </summary>
internal static class CrossPageFields
{
    internal const string BodyFingerprintKey = "bodyFingerprint";

    internal static IReadOnlyDictionary<string, ContentField>? Visible(IReadOnlyDictionary<string, ContentField>? fields)
    {
        if (fields is null || !fields.Keys.Any(IsBodyFingerprint))
        {
            return fields;
        }

        var comparer = fields is Dictionary<string, ContentField> dictionary ? dictionary.Comparer : StringComparer.Ordinal;
        var visible = new Dictionary<string, ContentField>(fields.Count, comparer);
        foreach (var (key, value) in fields)
        {
            if (!IsBodyFingerprint(key))
            {
                visible[key] = value;
            }
        }

        return visible;
    }

    // Matches the metadata hash, which leaves the key out ignoring case.
    private static bool IsBodyFingerprint(string key) =>
        string.Equals(key, BodyFingerprintKey, StringComparison.OrdinalIgnoreCase);
}
