using System.Text.Json;

namespace Bukit.Theme;

public static class PageComposer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static List<PageSectionDefinition> ParseSections(string? sectionsJson)
    {
        if (string.IsNullOrWhiteSpace(sectionsJson)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<PageSectionDefinition>>(sectionsJson, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
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
