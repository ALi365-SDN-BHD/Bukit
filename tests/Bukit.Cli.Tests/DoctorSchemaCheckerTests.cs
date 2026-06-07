using System.Text;
using Bukit.Cli.Commands;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DoctorSchemaCheckerTests
{
    [Fact]
    public void CheckSchemaFieldCompleteness_UsesContentModelProjectionDiagnostics()
    {
        var ctx = CreateContext(ConfigWithCollectionSchemas());
        var routed = new[]
        {
            RoutedDocument("post-1", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "post",
                ["type"] = "post"
            })
        };

        var output = CaptureConsoleOutput(() =>
        {
            var hasErrors = DoctorSchemaChecker.CheckSchemaFieldCompleteness(ctx, routed);
            Assert.True(hasErrors);
        });

        Assert.Contains("[content.custom_field_required_missing]", output);
        Assert.DoesNotContain("(collection:", output);
        Assert.DoesNotContain("[required]", output);
    }

    [Fact]
    public void CheckExtraContentFields_UsesCollectionScopedProjection()
    {
        var ctx = CreateContext(ConfigWithCollectionSchemas());
        var routed = new[]
        {
            RoutedDocument("post-1", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "post",
                ["type"] = "post",
                ["postOnly"] = "ok",
                ["pageOnly"] = "wrong collection"
            })
        };

        var output = CaptureConsoleOutput(() =>
            DoctorSchemaChecker.CheckExtraContentFields(ctx, routed));

        Assert.Contains("pageOnly", output);
        Assert.DoesNotContain("postOnly", output);
    }

    private static DoctorCommand.DoctorContext CreateContext(AppConfig config)
        => new(
            Path.Combine(Path.GetTempPath(), "bukit-doctor-schema-tests"),
            config,
            Path.Combine(Path.GetTempPath(), "bukit-doctor-schema-tests", "layouts"),
            Array.Empty<string>());

    private static AppConfig ConfigWithCollectionSchemas()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post"] = new()
                    {
                        Permalink = "/posts/{slug}/",
                        Template = "pages/post.html"
                    },
                    ["page"] = new()
                    {
                        Permalink = "/pages/{slug}/",
                        Template = "pages/page.html"
                    }
                }
            },
            Content = new ContentConfig
            {
                Provider = "markdown",
                ModelSchema = new ContentModelSchemaConfig
                {
                    FieldScopes = new Dictionary<string, IReadOnlyList<CustomFieldDefinitionConfig>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["post"] = new[]
                        {
                            new CustomFieldDefinitionConfig { Name = "postOnly", FieldType = "string", Required = true }
                        },
                        ["page"] = new[]
                        {
                            new CustomFieldDefinitionConfig { Name = "pageOnly", FieldType = "string", Required = true }
                        }
                    }
                }
            }
        };

    private static RoutedContentDocument RoutedDocument(string id, IReadOnlyDictionary<string, object> fields)
    {
        var fieldMap = ContentFieldReader.ToFieldMap(fields);
        var document = ContentDocument.Create(
            id,
            id,
            id,
            DateTimeOffset.UnixEpoch,
            $"<p>{id}</p>",
            fieldMap);
        return new RoutedContentDocument(document, new RouteInfo($"/{id}/", $"{id}/index.html", "pages/post.html"));
    }

    private static string CaptureConsoleOutput(Action action)
    {
        var original = Console.Out;
        using var sw = new StringWriter(new StringBuilder());
        Console.SetOut(sw);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(original);
        }

        return sw.ToString();
    }
}
