using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class FilteredListMatcherTests
{
    [Fact]
    public void Matches_Equals_UsesCaseInsensitiveAndSlugComparison()
    {
        var fields = FieldMap("industry", new ContentField("text", "Financial Services"));

        Assert.True(FilteredListMatcher.Matches(fields, Filter("industry", "financial-services")));
        Assert.True(FilteredListMatcher.Matches(fields, Filter("industry", "FINANCIAL SERVICES")));
    }

    [Fact]
    public void Matches_Contains_UsesTextAndSlugComparison()
    {
        var fields = FieldMap("title", new ContentField("text", "Malaysia Investment Hub"));

        Assert.True(FilteredListMatcher.Matches(fields, Filter("title", "investment", filterOperator: "contains")));
        Assert.True(FilteredListMatcher.Matches(fields, Filter("title", "malaysia-investment", filterOperator: "contains")));
        Assert.False(FilteredListMatcher.Matches(fields, Filter("title", "singapore", filterOperator: "contains")));
    }

    [Fact]
    public void Matches_Contains_MatchesAnyListValue()
    {
        var fields = FieldMap("industry", new ContentField("list", new List<object> { "Malaysia Logistics", "Manufacturing" }));

        Assert.True(FilteredListMatcher.Matches(fields, Filter("industry", "logistics", filterOperator: "contains")));
        Assert.True(FilteredListMatcher.Matches(fields, Filter("industry", "malaysia-logistics", filterOperator: "contains")));
        Assert.False(FilteredListMatcher.Matches(fields, Filter("industry", "finance", filterOperator: "contains")));
    }

    [Fact]
    public void Matches_In_MatchesAnyListValue()
    {
        var fields = FieldMap("category", new ContentField("list", new List<object> { "市场观察", "公司动态" }));
        var filter = new FilteredListConfig
        {
            Field = "category",
            Operator = "in",
            Values = new[] { "政策动态", "市场观察" },
            ListRoute = "/market/"
        };

        Assert.True(FilteredListMatcher.Matches(fields, filter));
    }

    [Fact]
    public void Matches_DoesNotMatchObjectListsByAccidentalToString()
    {
        var fields = FieldMap("category", new ContentField("list", new List<object>
        {
            new Dictionary<string, object?> { ["title"] = "市场观察" }
        }));
        var filter = new FilteredListConfig
        {
            Field = "category",
            Operator = "in",
            Values = new[] { "市场观察" },
            ListRoute = "/market/"
        };

        Assert.False(FilteredListMatcher.Matches(fields, filter));
    }

    [Fact]
    public void Matches_Equals_MatchesDateFieldsByCalendarDate()
    {
        var fields = FieldMap("publishDate", new ContentField("date", new DateTimeOffset(2026, 1, 2, 15, 30, 0, TimeSpan.Zero)));

        Assert.True(FilteredListMatcher.Matches(fields, Filter("publishDate", "2026-01-02")));
        Assert.False(FilteredListMatcher.Matches(fields, Filter("publishDate", "2026-01-03")));
    }

    private static FilteredListConfig Filter(string field, string value, string filterOperator = "equals")
        => new()
        {
            Field = field,
            Operator = filterOperator,
            Value = value,
            ListRoute = "/filtered/"
        };

    private static IReadOnlyDictionary<string, ContentField> FieldMap(string key, ContentField field)
        => new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            [key] = field
        };
}
