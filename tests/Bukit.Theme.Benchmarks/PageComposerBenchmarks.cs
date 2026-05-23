using BenchmarkDotNet.Attributes;
using Bukit.Theme;

namespace Bukit.Theme.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class PageComposerBenchmarks
{
    private string _sectionsJson = null!;
    private IReadOnlyDictionary<string, ThemeSectionDefinition> _themeSections = null!;

    [Params(1, 5, 10)]
    public int SectionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var sections = new List<object>();
        for (int i = 0; i < SectionCount; i++)
        {
            sections.Add(new
            {
                type = i % 2 == 0 ? "hero" : "cardGrid",
                props = new Dictionary<string, object?>
                {
                    ["title"] = $"Section {i}",
                    ["subtitle"] = $"Subtitle {i}"
                },
                source = i % 2 == 0 ? null : "posts",
                limit = i % 2 == 0 ? (int?)null : 6,
                sort = i % 2 == 0 ? null : "publishAt desc"
            });
        }

        _sectionsJson = System.Text.Json.JsonSerializer.Serialize(sections);

        _themeSections = new Dictionary<string, ThemeSectionDefinition>
        {
            ["hero"] = new() { Template = "sections/hero/hero.html", Description = "Hero" },
            ["cardGrid"] = new()
            {
                Template = "sections/card-grid/card-grid.html",
                Description = "Card grid",
                Data = new ThemeDataBindingDefinition { Source = "posts", Limit = 6, Sort = "publishAt desc" }
            }
        };
    }

    [Benchmark]
    public List<PageSectionDefinition> ParseAndCompose()
    {
        var parsed = PageComposer.ParseSections(_sectionsJson);
        return PageComposer.Compose(parsed, _themeSections);
    }
}
