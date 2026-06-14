using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class RouteMapLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public RouteMapLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-route-map-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_MappingRoot_ReadsPagesAndOptionalFields()
    {
        var path = WriteRouteMap(
            """
            pages:
              - source: home
                route: /
                type: Home
                template: index
                slug: home
                description: Landing page
            """);

        var config = RouteMapLoader.Load(path);

        var page = Assert.Single(config!.Pages);
        Assert.Equal("home", page.Source);
        Assert.Equal("/", page.Route);
        Assert.Equal("Home", page.Type);
        Assert.Equal("index", page.Template);
        Assert.Equal("home", page.Slug);
        Assert.Equal("Landing page", page.Description);
    }

    [Fact]
    public void Load_DirectSequenceRoot_ReadsPages()
    {
        var path = WriteRouteMap(
            """
            - source: about
              route: /about/
              type: Page
              template: page
            """);

        var config = RouteMapLoader.Load(path);

        var page = Assert.Single(config!.Pages);
        Assert.Equal("about", page.Source);
        Assert.Equal("/about/", page.Route);
    }

    [Fact]
    public void Load_MissingPagesSequence_ReturnsNull()
    {
        var path = WriteRouteMap("title: invalid");

        var stderr = new StringWriter();
        var original = Console.Error;

        try
        {
            Console.SetError(stderr);

            var config = RouteMapLoader.Load(path);

            Assert.Null(config);
            Assert.Contains("missing the 'pages' sequence", stderr.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    [Fact]
    public void Load_InvalidYaml_ReturnsNull()
    {
        var path = WriteRouteMap("pages: [");

        var stderr = new StringWriter();
        var original = Console.Error;

        try
        {
            Console.SetError(stderr);

            var config = RouteMapLoader.Load(path);

            Assert.Null(config);
            Assert.Contains("Failed to parse route map", stderr.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    [Fact]
    public void Load_EntryMissingSource_IsSkipped()
    {
        var path = WriteRouteMap(
            """
            pages:
              - route: /missing-source/
                type: Page
                template: page
              - source: contact
                route: /contact/
                type: Page
                template: page
            """);

        var stderr = new StringWriter();
        var original = Console.Error;

        try
        {
            Console.SetError(stderr);

            var config = RouteMapLoader.Load(path);

            var page = Assert.Single(config!.Pages);
            Assert.Equal("contact", page.Source);
            Assert.Contains("missing required 'source' field", stderr.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    private string WriteRouteMap(string yaml)
    {
        var path = Path.Combine(_tempDir, "routes.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }
}
