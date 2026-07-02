using System.Text.Json;

namespace Bukit.Theme;

public static class PageComposer
{
    public static List<PageSectionDefinition> ParseSections(string? sectionsJson)
    {
        if (string.IsNullOrWhiteSpace(sectionsJson)) return [];

        try
        {
            using var doc = JsonDocument.Parse(sectionsJson);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                var single = ParseSectionElement(root);
                return single is not null ? [single] : [];
            }

            if (root.ValueKind != JsonValueKind.Array) return [];

            var result = new List<PageSectionDefinition>();
            foreach (var element in root.EnumerateArray())
            {
                var section = ParseSectionElement(element);
                if (section is not null) result.Add(section);
            }
            return result;
        }
        catch
        {
            return [];
        }
    }

    private static PageSectionDefinition? ParseSectionElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        if (!element.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
            return null;

        var section = new PageSectionDefinition { Type = typeProp.GetString()! };

        if (element.TryGetProperty("variant", out var v) && v.ValueKind == JsonValueKind.String)
            section.Variant = v.GetString();

        if (element.TryGetProperty("props", out var propsElement) && propsElement.ValueKind == JsonValueKind.Object)
            section.Props = ParseProps(propsElement);

        if (element.TryGetProperty("source", out var src) && src.ValueKind == JsonValueKind.String)
            section.Source = src.GetString();

        if (element.TryGetProperty("filter", out var filterElement) && filterElement.ValueKind == JsonValueKind.Object)
            section.Filter = ParseFilter(filterElement);

        if (element.TryGetProperty("limit", out var limitProp) && limitProp.TryGetInt32(out var limit))
            section.Limit = limit;

        if (element.TryGetProperty("sort", out var sortProp) && sortProp.ValueKind == JsonValueKind.String)
            section.Sort = sortProp.GetString();

        return section;
    }

    private static Dictionary<string, object?> ParseProps(JsonElement props)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in props.EnumerateObject())
        {
            dict[prop.Name] = JsonElementToObject(prop.Value);
        }
        return dict;
    }

    private static Dictionary<string, object?> ParseFilter(JsonElement filter)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in filter.EnumerateObject())
        {
            dict[prop.Name] = JsonElementToObject(prop.Value);
        }
        return dict;
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    public static List<PageSectionDefinition> Compose(List<PageSectionDefinition> pageSections, IReadOnlyDictionary<string, ThemeSectionDefinition> themeSections)
    {
        var result = new List<PageSectionDefinition>(pageSections.Count);

        foreach (var pageSection in pageSections)
        {
            if (!themeSections.TryGetValue(pageSection.Type, out var themeSectionDef))
            {
                result.Add(pageSection);
                continue;
            }

            var mergedProps = MergeProps(themeSectionDef, pageSection);
            var dataBinding = MergeDataBinding(themeSectionDef.Data, pageSection);

            result.Add(new PageSectionDefinition
            {
                Type = pageSection.Type,
                Variant = pageSection.Variant,
                Props = mergedProps,
                Source = dataBinding?.Source ?? pageSection.Source,
                Filter = dataBinding?.Filters ?? pageSection.Filter,
                Limit = dataBinding?.Limit ?? pageSection.Limit,
                Sort = dataBinding?.Sort ?? pageSection.Sort
            });
        }

        return result;
    }

    private static Dictionary<string, object?>? MergeProps(ThemeSectionDefinition themeSection, PageSectionDefinition pageSection)
    {
        var merged = new Dictionary<string, object?>();

        if (pageSection.Props is not null)
        {
            foreach (var kv in pageSection.Props)
            {
                merged[kv.Key] = kv.Value;
            }
        }

        return merged.Count > 0 ? merged : null;
    }

    private static ThemeDataBindingDefinition? MergeDataBinding(ThemeDataBindingDefinition? themeData, PageSectionDefinition pageSection)
    {
        if (themeData is null && pageSection.Filter is null && pageSection.Limit is null && string.IsNullOrEmpty(pageSection.Sort))
        {
            return themeData;
        }

        return new ThemeDataBindingDefinition
        {
            Source = pageSection.Source ?? themeData?.Source,
            Mode = themeData?.Mode,
            Limit = pageSection.Limit ?? themeData?.Limit,
            Sort = pageSection.Sort ?? themeData?.Sort,
            Filters = pageSection.Filter is not null
                ? new Dictionary<string, object?>(pageSection.Filter)
                : themeData?.Filters
        };
    }
}
