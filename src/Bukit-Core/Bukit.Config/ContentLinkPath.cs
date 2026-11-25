using System.Text.RegularExpressions;

namespace Bukit.Config;

/// <summary>
/// A parsed <c>content.links[].field</c>: dot-separated names, where <c>name[]</c> walks every item
/// of a list of objects and plain <c>name</c> enters an object. The last name holds the keys.
/// </summary>
internal sealed partial record ContentLinkPath(IReadOnlyList<ContentLinkPath.Segment> Segments)
{
    /// <summary>The most segments a path may have: the nesting limit of structured fields.</summary>
    internal const int MaxSegments = 8;

    internal readonly record struct Segment(string Name, bool IsList);

    internal bool IsNested => Segments.Count > 1;

    /// <summary>The field written next to the key field: <c>attractions_links</c>.</summary>
    internal string GroupedName => Segments[^1].Name + "_links";

    /// <summary>The top-level field with every link of the page: <c>days_attractions_links</c>.</summary>
    internal string FlatName => string.Join('_', Segments.Select(segment => segment.Name)) + "_links";

    internal static bool TryParse(string? text, out ContentLinkPath path)
    {
        path = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('.');
        if (parts.Length > MaxSegments)
        {
            return false;
        }

        var segments = new Segment[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            var isList = part.EndsWith("[]", StringComparison.Ordinal);
            var name = isList ? part[..^2] : part;
            // The last name holds the keys; a list of keys needs no [] marker.
            if (!IsName(name) || (isList && index == parts.Length - 1))
            {
                return false;
            }

            segments[index] = new Segment(name, isList);
        }

        path = new ContentLinkPath(segments);
        return true;
    }

    /// <summary>A field name a link may read or write: letters, digits, <c>_</c> and <c>-</c>, not starting with a digit or <c>-</c>.</summary>
    internal static bool IsName(string? value) => value is not null && Name().IsMatch(value);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Name();
}
