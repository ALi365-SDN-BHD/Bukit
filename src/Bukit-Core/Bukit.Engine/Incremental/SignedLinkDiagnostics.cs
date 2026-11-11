using System.Collections;
using System.Text.RegularExpressions;
using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Media;
using Bukit.Rendering;
using Bukit.Shared;

namespace Bukit.Engine.Incremental;

/// <summary>
/// Finds links of a recognized signing scheme left in the shared render data, including the
/// route metadata source and page custom fields (read through <c>pages</c>). Their signature may
/// change on every fetch, so each build can see a changed collection and render again every
/// route that read it. Diagnostic only: the hash keeps the full links, because skipping such a
/// page would keep an expired link online.
/// </summary>
internal static partial class SignedLinkDiagnostics
{
    internal static void Report(
        RenderDependencyContext context,
        IEnumerable<ContentDocument>? pageDocuments,
        string? language,
        ILogger logger)
    {
        var found = Find(context, pageDocuments);
        if (found.Count == 0)
        {
            return;
        }

        var keys = string.Join(',', found.Select(item => $"{item.Key}:{item.Count}"));
        var languagePart = string.IsNullOrWhiteSpace(language) ? string.Empty : $" language={language}";
        logger.Warn(
            $"event=incremental.signed_links{languagePart} keys={keys} links={found.Sum(item => item.Count)} " +
            "message=Shared data contains signed links that may change on every fetch; routes that read these keys may render again on every build. " +
            "hint=localize_these_files_with_content_media");
    }

    internal static IReadOnlyList<(string Key, int Count)> Find(
        RenderDependencyContext context,
        IEnumerable<ContentDocument>? pageDocuments = null)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        void Add(string key, object? value)
        {
            var count = Count(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
            if (count > 0)
            {
                counts[key] = counts.GetValueOrDefault(key) + count;
            }
        }

        foreach (var (key, _, value, _) in SiteModelDataContributor.EnumerateCollectionValues(context))
        {
            Add(key, value);
        }

        foreach (var (key, _, value, _) in SiteModelDataContributor.EnumerateRouteMetadataSourceValues(context))
        {
            Add(key, value);
        }

        foreach (var document in pageDocuments ?? [])
        {
            Add("pages", document.CustomFields);
        }

        return [.. counts.Select(item => (item.Key, item.Value))];
    }

    internal static int CountInText(string? text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("signature", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var count = 0;
        foreach (Match match in UrlPattern().Matches(text))
        {
            // Links inside HTML escape their query separators.
            var candidate = match.Value
                .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase)
                .Replace("&#38;", "&", StringComparison.Ordinal)
                .Replace("&#x26;", "&", StringComparison.OrdinalIgnoreCase);
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
                !string.Equals(SignedUrlIdentity.IdentityTarget(uri), SignedUrlIdentity.RequestTarget(uri), StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static int Count(object? value, HashSet<object> visited)
    {
        switch (value)
        {
            case null:
                return 0;
            case string text:
                return CountInText(text);
            case ContentField field:
                return Count(field.Value, visited);
            case ModuleInfo module:
                return CountInText(module.Content) + Count(module.Fields, visited);
            case PageInfo page:
                return CountInText(page.Content) + CountInText(page.Summary) + Count(page.Fields, visited);
            case IDictionary dictionary:
                return visited.Add(dictionary) ? SumValues(dictionary.Values, visited) : 0;
            case IEnumerable sequence:
                if (!visited.Add(sequence))
                {
                    return 0;
                }

                var total = 0;
                foreach (var element in sequence)
                {
                    total += element switch
                    {
                        KeyValuePair<string, object> pair => Count(pair.Value, visited),
                        KeyValuePair<string, ContentField> pair => Count(pair.Value, visited),
                        _ => Count(element, visited)
                    };
                }

                return total;
            default:
                return 0;
        }
    }

    private static int SumValues(ICollection values, HashSet<object> visited)
    {
        var total = 0;
        foreach (var item in values)
        {
            total += Count(item, visited);
        }

        return total;
    }

    [GeneratedRegex(@"https?://[^\s""'<>()\\]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();
}
