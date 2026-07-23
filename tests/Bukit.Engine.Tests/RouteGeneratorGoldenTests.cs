using System.Text.Json;
using System.Linq;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RouteGeneratorGoldenTests
{
    [Fact]
    public void GenerateWithSource_MatchesGoldenSnapshot()
    {
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["article"] = new RouteGenerator.CollectionRouteRule("/blog/{slug}/", "pages/post.html")
        };

        var permalinks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["page"] = "/special/{slug}/"
        };

        var documents = new[]
        {
            CreateDocument(
                "full-override",
                "full override",
                new Dictionary<string, object>
                {
                    ["collection"] = "page",
                    ["route"] = new Dictionary<string, object>
                    {
                        ["url"] = "/full/",
                        ["template"] = "pages/full.html"
                    }
                }),
            CreateDocument(
                "partial-override",
                "partial override",
                new Dictionary<string, object>
                {
                    ["collection"] = "article",
                    ["route"] = new Dictionary<string, object>
                    {
                        ["url"] = "/partial/"
                    }
                }),
            CreateDocument(
                "collection-route",
                "collection route",
                new Dictionary<string, object>
                {
                    ["collection"] = "article"
                }),
            CreateDocument(
                "permalink-route",
                "permalink route",
                new Dictionary<string, object>
                {
                    ["collection"] = "special"
                })
        };

        var actual = new List<RouteInventoryItem>();
        foreach (var doc in documents)
        {
            var (route, source) = RouteGenerator.GenerateWithSource(doc, "none", permalinks, collections);
            actual.Add(new RouteInventoryItem(
                route.Url,
                route.OutputPath,
                route.Template,
                source.ToString(),
                ContentFieldReader.GetCollection(doc),
                ContentFieldReader.GetText(doc.CustomFields, "type")));
        }

        actual = actual.OrderBy(x => x.Url, StringComparer.OrdinalIgnoreCase).ToList();

        var snapshotPath = GetRouteGeneratorGoldenPath();
        var expected = JsonSerializer.Deserialize<RouteGeneratorGoldenSnapshot>(File.ReadAllText(snapshotPath), RouteGeneratorJsonOptions)!;

        Assert.Equal("https://bukit.dev/schemas/routes.v1.json", expected.Schema);
        Assert.Equal("1.0", expected.SchemaVersion);
        Assert.Equal(expected.Routes, actual);
    }

    private static ContentDocument CreateDocument(string id, string title, IReadOnlyDictionary<string, object>? fieldValues)
    {
        return ContentDocument.Create(
            id,
            title,
            id,
            DateTimeOffset.UtcNow,
            null,
            ContentFieldReader.ToFieldMap(fieldValues ?? new Dictionary<string, object>()));
    }

    private static string GetRouteGeneratorGoldenPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.EnumerateDirectories("src").Any() && directory.EnumerateDirectories("tests").Any())
            {
                return Path.Combine(directory.FullName, "tests", "Bukit.Engine.Tests", "Snapshots", "RouteGenerator", "route-generator.golden.json");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Failed to locate repository root for route generator golden file.");
    }

    private static readonly JsonSerializerOptions RouteGeneratorJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record RouteGeneratorGoldenSnapshot(string Schema, string SchemaVersion, List<RouteInventoryItem> Routes);

    private sealed record RouteInventoryItem(
        string Url,
        string OutputPath,
        string Template,
        string RouteSource,
        string? Collection,
        string? Type);
}
