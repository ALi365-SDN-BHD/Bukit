using BenchmarkDotNet.Attributes;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Theme;

namespace Bukit.Theme.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class SectionDataResolverBenchmarks
{
    private IReadOnlyList<(ContentDocument Item, RouteInfo? Route)> _allPages = null!;
    private PageSectionDefinition _sectionWithSource = null!;
    private PageSectionDefinition _sectionWithFilter = null!;
    private PageSectionDefinition _sectionWithSort = null!;

    [Params(100, 1000, 5000)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var items = new List<(ContentDocument, RouteInfo?)>();
        for (int i = 0; i < ItemCount; i++)
        {
            var isFeature = i % 3 == 0;
            var item = ContentDocument.Create(
                $"post-{i}",
                $"Post Title {i}",
                $"post-title-{i}",
                DateTimeOffset.UtcNow.AddDays(-i),
                null,
                ContentFieldReader.WithValues(
                    new Dictionary<string, ContentField>
                    {
                        ["featured"] = new("boolean", isFeature)
                    },
                    new Dictionary<string, object>
                    {
                        ["type"] = "posts",
                        ["collections"] = new List<object> { "posts", "blog" }
                    })
            );
            items.Add((item, (RouteInfo?)null));
        }

        _allPages = items;

        _sectionWithSource = new PageSectionDefinition
        {
            Type = "cardGrid",
            Source = "posts",
            Limit = 10
        };

        _sectionWithFilter = new PageSectionDefinition
        {
            Type = "cardGrid",
            Source = "posts",
            Filter = new Dictionary<string, object?> { ["featured"] = true },
            Limit = 5
        };

        _sectionWithSort = new PageSectionDefinition
        {
            Type = "cardGrid",
            Source = "posts",
            Sort = "title",
            Limit = 20
        };
    }

    [Benchmark]
    public IReadOnlyList<(ContentDocument, string?)> Resolve_WithSourceOnly()
    {
        return SectionDataResolver.Resolve(_sectionWithSource, _allPages);
    }

    [Benchmark]
    public IReadOnlyList<(ContentDocument, string?)> Resolve_WithSourceAndFilter()
    {
        return SectionDataResolver.Resolve(_sectionWithFilter, _allPages);
    }

    [Benchmark]
    public IReadOnlyList<(ContentDocument, string?)> Resolve_WithSourceAndSort()
    {
        return SectionDataResolver.Resolve(_sectionWithSort, _allPages);
    }

    [Benchmark]
    public IReadOnlyList<(ContentDocument, string?)> Resolve_AllPages()
    {
        var section = new PageSectionDefinition { Type = "cardGrid", Source = "*" };
        return SectionDataResolver.Resolve(section, _allPages);
    }
}
