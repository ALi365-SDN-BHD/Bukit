using System.Text.Json;
using System.Linq;
using Bukit.Cli.Commands;
using Bukit.Cli.Tests;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class RouteCommandTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _configPath;
    private readonly string _contentDir;

    public RouteCommandTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-route-command-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
        _contentDir = Path.Combine(_rootDir, "content");
        Directory.CreateDirectory(_contentDir);

        _configPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                         collections:
                                           article:
                                             permalink: /articles/{slug}/
                                             template: pages/article.html
                                         permalinks:
                                           special: /special/{slug}/
                                       content:
                                         sources:
                                           - type: markdown
                                             name: article
                                             collection: article
                                             markdown:
                                               dir: content
                                       build:
                                         output: dist
                                       """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public async Task InspectJson_ReturnsRouteSourceByOverridePriority()
    {
        WriteContent("full-override.md", """
                                 ---
                                 title: Full Override
                                 slug: full-override
                                 collection: article
                                 route:
                                   url: /full/
                                   template: pages/full.html
                                 ---

                                 # Full Override
                                 """);
        WriteContent("partial-override.md", """
                                 ---
                                 title: Partial Override
                                 slug: partial-override
                                 collection: article
                                 route:
                                   url: /partial/
                                   template: pages/partial.html
                                 ---

                                 # Partial Override
                                 """);
        WriteContent("collection-route.md", """
                                 ---
                                 title: Collection Route
                                 slug: collection-route
                                 collection: article
                                 ---

                                 # Collection Route
                                 """);
        using var writer = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await RouteCommand.RunAsync(CliTestHelper.CreateCommand("route", new[]
            {
                "route",
                "inspect",
                "--config",
                _configPath,
                "--json"
            }));

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        using var doc = JsonDocument.Parse(writer.ToString());
        var routes = doc.RootElement.EnumerateArray().ToDictionary(
            e => e.GetProperty("url").GetString()!,
            e => e);

        var full = routes["/full/"];
        Assert.Equal("FullOverride", full.GetProperty("routeSource").GetString());
        Assert.Equal("full/index.html", full.GetProperty("outputPath").GetString());
        Assert.Equal("pages/full.html", full.GetProperty("template").GetString());

        var partial = routes["/partial/"];
        Assert.Equal("FullOverride", partial.GetProperty("routeSource").GetString());
        Assert.Equal("partial/index.html", partial.GetProperty("outputPath").GetString());
        Assert.Equal("pages/partial.html", partial.GetProperty("template").GetString());

        var collection = routes["/articles/collection-route/"];
        Assert.Equal("Collection", collection.GetProperty("routeSource").GetString());
        Assert.Equal("articles/collection-route/index.html", collection.GetProperty("outputPath").GetString());
        Assert.Equal("pages/article.html", collection.GetProperty("template").GetString());

    }

    [Fact]
    public async Task InspectJson_FiltersByCollection()
    {
        WriteContent("full-override.md", """
                                 ---
                                 title: Full Override
                                 slug: full-override
                                 collection: article
                                 route:
                                   url: /full/
                                   template: pages/full.html
                                 ---

                                 # Full Override
                                 """);
        WriteContent("collection-route.md", """
                                 ---
                                 title: Collection Route
                                 slug: collection-route
                                 collection: article
                                 ---

                                 # Collection Route
                                 """);
        using var writer = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await RouteCommand.RunAsync(CliTestHelper.CreateCommand("route", new[]
            {
                "route",
                "inspect",
                "--config",
                _configPath,
                "--json",
                "--collection",
                "article"
            }));

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        using var doc = JsonDocument.Parse(writer.ToString());
        var routes = doc.RootElement.EnumerateArray().ToArray();

        Assert.Equal(2, routes.Length);
        Assert.All(routes, item => Assert.Equal("article", item.GetProperty("collection").GetString()));
    }

    [Fact]
    public async Task Inspect_RejectsTopLevelOutputPathWithoutNestedRouteMap()
    {
        WriteContent("invalid-outputpath.md", """
                                 ---
                                 title: Rejected Top-Level OutputPath
                                 slug: rejected-outputpath
                                 collection: article
                                 outputPath: blocked/index.html
                                 ---

                                 # Rejected Top-Level OutputPath
                                 """);

        var ex = await Assert.ThrowsAsync<ConfigException>(
            () => RouteCommand.RunAsync(CliTestHelper.CreateCommand("route", new[]
            {
                "route",
                "inspect",
                "--config",
                _configPath,
                "--json"
            })));

        Assert.Contains("Top-level outputPath is removed in Bukit 1.0", ex.Message);
    }

    private void WriteContent(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(_contentDir, fileName), content);
    }
}
