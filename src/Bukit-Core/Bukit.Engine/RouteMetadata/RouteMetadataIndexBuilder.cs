using System.Collections;
using System.Collections.ObjectModel;
using Bukit.Config;
using Bukit.Engine.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine.RouteMetadata;

internal static class RouteMetadataIndexBuilder
{
    internal static IReadOnlyDictionary<string, RouteMetadataEntry> Build(
        RouteMetadataConfig config,
        IReadOnlyDictionary<string, object>? sourceData)
    {
        if (sourceData is null || !sourceData.TryGetValue(config.Source, out var source) ||
            source is not IEnumerable rows || source is string)
        {
            throw new ContentException($"Route metadata source '{config.Source}' is unavailable.");
        }

        var entries = new Dictionary<string, RouteMetadataEntry>(StringComparer.Ordinal);
        var rowIndex = 0;
        foreach (var row in rows)
        {
            if (row is not ModuleInfo module)
            {
                throw new ContentException(
                    $"Route metadata source '{config.Source}' row {rowIndex} must be an object row.");
            }

            var rowContext = $"Route metadata source '{config.Source}' row '{module.Id}' (index {rowIndex})";
            var routeValue = RequireText(module.Fields, config.RouteField, rowContext, null);
            ValidateRoute(routeValue, rowContext);
            var route = RoutePathBuilder.NormalizeUrl(routeValue);
            var routeContext = $"{rowContext} route '{route}'";
            var canonicalTitle = string.Equals(config.TitleField, "title", StringComparison.Ordinal)
                ? module.Title
                : null;
            var title = RequireText(module.Fields, config.TitleField, routeContext, canonicalTitle);
            var summary = RequireText(module.Fields, config.SummaryField, routeContext, null);
            var seoTitle = OptionalText(module.Fields, config.SeoTitleField, routeContext);
            var seoDescription = OptionalText(module.Fields, config.SeoDescriptionField, routeContext);

            if (!entries.TryAdd(route, new RouteMetadataEntry(route, title, summary, seoTitle, seoDescription)))
            {
                throw new ContentException($"{routeContext} duplicates normalized route '{route}'.");
            }

            rowIndex++;
        }

        foreach (var requiredRoute in config.RequiredRoutes ?? Array.Empty<string>())
        {
            ValidateRoute(requiredRoute, $"Route metadata source '{config.Source}' required");
            var normalized = RoutePathBuilder.NormalizeUrl(requiredRoute);
            if (!entries.ContainsKey(normalized))
            {
                throw new ContentException(
                    $"Route metadata source '{config.Source}' required route '{normalized}' is missing.");
            }
        }

        return new ReadOnlyDictionary<string, RouteMetadataEntry>(entries);
    }

    private static void ValidateRoute(string route, string context)
    {
        if (route.Contains('?', StringComparison.Ordinal) ||
            route.Contains('#', StringComparison.Ordinal) ||
            route.Contains('\\', StringComparison.Ordinal) ||
            route.Contains("//", StringComparison.Ordinal))
        {
            throw new ContentException(
                $"{context} route '{route}' is invalid; route paths cannot contain query, fragment, backslash, or empty non-root segments.");
        }
    }

    private static string RequireText(
        IReadOnlyDictionary<string, ContentField>? fields,
        string fieldName,
        string context,
        string? fallback)
    {
        var hasField = ConfiguredContentFieldReader.TryGetField(fields, fieldName, context, out var field);
        var value = hasField ? field.Value?.ToString()?.Trim() : fallback?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ContentException($"{context} requires non-empty field '{fieldName}'.");
        }

        return value;
    }

    private static string? OptionalText(
        IReadOnlyDictionary<string, ContentField>? fields,
        string fieldName,
        string context)
    {
        if (!ConfiguredContentFieldReader.TryGetField(fields, fieldName, context, out var field))
        {
            return null;
        }

        var value = field.Value?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
