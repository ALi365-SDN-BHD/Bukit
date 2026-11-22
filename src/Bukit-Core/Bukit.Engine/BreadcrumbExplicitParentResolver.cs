using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// A page's parent_url: the normalized parent key, the value as the author wrote it (for warnings),
/// and the page's own route URL.
/// </summary>
internal sealed record BreadcrumbExplicitParent(string Key, string Value, string RouteUrl);

/// <summary>
/// Reads the parent_url content field, which names a page's breadcrumb parent by its site path.
/// A parent that is missing, invalid, the page itself or part of a cycle is dropped with a warning,
/// so that page keeps its URL-inferred trail.
/// </summary>
internal static class BreadcrumbExplicitParentResolver
{
    internal const string FieldKey = "parent_url";

    internal static IReadOnlyDictionary<string, BreadcrumbExplicitParent> Resolve(
        IReadOnlyList<RoutedContentDocument> routed,
        Func<string, bool> routeExists,
        ILogger? logger)
    {
        var parents = new Dictionary<string, BreadcrumbExplicitParent>(StringComparer.OrdinalIgnoreCase);
        foreach (var routedDocument in routed)
        {
            var value = ContentFieldReader.GetText(routedDocument.Document.CustomFields, FieldKey)?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            var routeUrl = routedDocument.Route.Url;
            var key = BreadcrumbDescriptorResolver.NormalizeForComparison(routeUrl);
            if (key == "/" || !routeExists(key))
            {
                continue;
            }

            var parentKey = IsSitePath(value) ? BreadcrumbDescriptorResolver.NormalizeForComparison(value) : null;
            if (parentKey is null || !routeExists(parentKey) || string.Equals(parentKey, key, StringComparison.OrdinalIgnoreCase))
            {
                logger?.Warn($"seo.breadcrumb_parent_missing route={routeUrl} parent={value} hint=use-the-site-path-of-a-generated-page");
                continue;
            }

            parents[key] = new BreadcrumbExplicitParent(parentKey, value, routeUrl);
        }

        RemoveCycles(parents, logger);
        return parents;
    }

    // A site path such as /malaysia/routes/; URLs with a scheme or host, queries and fragments are rejected.
    private static bool IsSitePath(string value)
        => value.StartsWith('/') &&
           !value.StartsWith("//", StringComparison.Ordinal) &&
           value.IndexOfAny(['?', '#', '\\']) < 0 &&
           !value.Any(char.IsControl);

    /// <summary>
    /// Drops the explicit parent of every page on a cycle. Pages that merely lead into a cycle keep
    /// theirs, and the result does not depend on the order pages were read in.
    /// </summary>
    private static void RemoveCycles(Dictionary<string, BreadcrumbExplicitParent> parents, ILogger? logger)
    {
        var onCycle = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var settled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in parents.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            var path = new List<string>();
            var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var current = start;
            while (!settled.Contains(current) && parents.ContainsKey(current))
            {
                if (positions.TryGetValue(current, out var cycleStart))
                {
                    onCycle.UnionWith(path.Skip(cycleStart));
                    break;
                }

                positions[current] = path.Count;
                path.Add(current);
                current = parents[current].Key;
            }

            settled.UnionWith(path);
        }

        foreach (var key in onCycle.Order(StringComparer.OrdinalIgnoreCase))
        {
            BreadcrumbTrailRewriter.WarnCycle(logger, parents[key]);
            parents.Remove(key);
        }
    }
}
