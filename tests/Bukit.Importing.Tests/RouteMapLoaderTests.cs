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
    public void Load_MissingPagesSequence_ThrowsUserInputError()
    {
        var path = WriteRouteMap("title: invalid");

        var ex = Assert.Throws<ImportException>(() => RouteMapLoader.Load(path));

        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.Contains("missing the 'pages' sequence", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MissingFile_ThrowsUserInputError()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-missing-route-map-{Guid.NewGuid():N}.yaml");

        var ex = Assert.Throws<ImportException>(() => RouteMapLoader.Load(path));

        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.Contains("was not found", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_NoPath_ReturnsNull()
    {
        Assert.Null(RouteMapLoader.Load(null));
        Assert.Null(RouteMapLoader.Load(" "));
    }

    [Fact]
    public void Load_InvalidYaml_ThrowsUserInputError()
    {
        var path = WriteRouteMap("pages: [");

        var ex = Assert.Throws<ImportException>(() => RouteMapLoader.Load(path));

        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.Contains("Failed to parse route map", ex.Message, StringComparison.Ordinal);
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
        }
        finally
        {
            Console.SetError(original);
        }
    }

    [Fact]
    public void Load_TemplateWithPathTraversal_ThrowsUserInputError()
    {
        var path = WriteRouteMap(
            """
            pages:
              - source: about
                route: /about/
                type: Page
                template: ../../outside
            """);

        var ex = Assert.Throws<ImportException>(() => RouteMapLoader.Load(path));

        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.Contains("invalid 'template'", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../../escaped")]
    [InlineData("nested/slug")]
    [InlineData("..")]
    [InlineData(" padded ")]
    public void Load_SlugWithPathSemantics_ThrowsUserInputError(string slug)
    {
        var path = WriteRouteMap(
            $"""
            pages:
              - source: about
                route: /about/
                type: Page
                template: about
                slug: "{slug}"
            """);

        var ex = Assert.Throws<ImportException>(() => RouteMapLoader.Load(path));

        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.Contains("invalid 'slug'", ex.Message, StringComparison.Ordinal);
    }

    private string WriteRouteMap(string yaml)
    {
        var path = Path.Combine(_tempDir, "routes.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }
}
