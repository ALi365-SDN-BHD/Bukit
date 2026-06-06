using Bukit.Cli.Commands;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DoctorSchemaCheckerTests
{
    [Fact]
    public void CheckSchemaFieldCompleteness_UsesContentDocumentCustomFields()
    {
        var ctx = Context(required: true);
        var route = new RouteInfo("/blog/hello/", "blog/hello/index.html", "pages/post.html");
        var document = Document(hasStatus: true);

        var hasErrors = DoctorSchemaChecker.CheckSchemaFieldCompleteness(ctx, new[] { (document, route) });

        Assert.False(hasErrors);
    }

    private static DoctorCommand.DoctorContext Context(bool required)
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post"] = new CollectionConfig
                    {
                        Permalink = "/blog/{slug}/",
                        Template = "pages/post.html",
                        Schema = new[]
                        {
                            new SchemaFieldDefinition
                            {
                                Name = "status",
                                Type = "string",
                                Required = required
                            }
                        }
                    }
                }
            },
            Content = new ContentConfig { Provider = "markdown" }
        };

        return new DoctorCommand.DoctorContext("/", config, "/", Array.Empty<string>());
    }

    private static ContentDocument Document(bool hasStatus)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (hasStatus)
        {
            fields["status"] = new ContentField("text", "published");
        }

        var record = new ContentRecord(
            Identity: new ContentIdentity("post-1", "hello", "post-1", "post", "published"),
            Presentation: new ContentPresentation("Hello", null, null, "en", Array.Empty<string>()),
            Classification: new ContentClassification("post", "post", Array.Empty<string>(), Array.Empty<string>()),
            Ownership: new ContentOwnership(null, null, null, null),
            Lifecycle: new ContentLifecycle(DateTimeOffset.UtcNow, null, null, null),
            Provenance: new ProvenanceRecord(null, null, Array.Empty<string>(), Array.Empty<string>(), null),
            Trust: new TrustMetadata(null, "approved", Array.Empty<string>()),
            Entities: Array.Empty<EntityRecord>(),
            Relations: Array.Empty<ContentRelation>(),
            Media: Array.Empty<MediaAsset>());

        return new ContentDocument(
            record,
            new ContentBodyRef(null, null, null, null),
            new ContentRoutePolicy(null, null, "pages/post.html", null, "post"),
            new ContentPublishPolicy(false, false, false, false, false, false, false),
            fields,
            Array.Empty<ContentDiagnostic>());
    }
}
