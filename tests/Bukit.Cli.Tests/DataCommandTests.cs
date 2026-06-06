using System.Text.Json;
using Bukit.Cli.Commands;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DataCommandTests : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly StringWriter _stdout;

    public DataCommandTests()
    {
        _originalOut = Console.Out;
        _stdout = new StringWriter();
        Console.SetOut(_stdout);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        _stdout.Dispose();
    }

    [Fact]
    public void PrintModuleSummary_UsesStructuredTypeAndLanguage()
    {
        var item = new ContentItem(
            Id: "module-1",
            Title: "Hero Block",
            Slug: "hero-block",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceMode"] = new("text", "data"),
                ["sourceKey"] = new("text", "homepage"),
                ["type"] = new("text", "hero"),
                ["language"] = new("text", "ms-MY")
            });

        DataCommand.PrintModuleSummary(new[] { item });

        var output = _stdout.ToString();
        Assert.Contains("hero", output, StringComparison.Ordinal);
        Assert.Contains("homepage", output, StringComparison.Ordinal);
        Assert.Contains("ms-MY", output, StringComparison.Ordinal);
    }

    [Fact]
    public void DumpModulesJson_UsesStructuredTypeAsModuleKey()
    {
        var item = new ContentItem(
            Id: "module-1",
            Title: "Hero Block",
            Slug: "hero-block",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceMode"] = new("text", "data"),
                ["type"] = new("text", "hero"),
                ["headline"] = new("text", "Welcome")
            });

        var json = DataCommand.DumpModulesJson(new[] { item });
        using var doc = JsonDocument.Parse(json);

        var hero = doc.RootElement.GetProperty("modules").GetProperty("hero");
        Assert.Equal("module-1", hero[0].GetProperty("id").GetString());
        Assert.Equal("Welcome", hero[0].GetProperty("fields").GetProperty("headline").GetString());
    }

    [Fact]
    public void PrintModuleSummary_UsesContentDocumentsAsDataModuleSource()
    {
        var document = Document(isDataModule: true);

        DataCommand.PrintModuleSummary(new[] { document });

        var output = _stdout.ToString();
        Assert.Contains("hero", output, StringComparison.Ordinal);
        Assert.Contains("homepage", output, StringComparison.Ordinal);
        Assert.Contains("ms-MY", output, StringComparison.Ordinal);
    }

    [Fact]
    public void DumpModulesJson_UsesContentDocumentsAsDataModuleSource()
    {
        var document = Document(isDataModule: true);

        var json = DataCommand.DumpModulesJson(new[] { document });
        using var doc = JsonDocument.Parse(json);

        var hero = doc.RootElement.GetProperty("modules").GetProperty("hero");
        Assert.Equal("module-1", hero[0].GetProperty("id").GetString());
        Assert.Equal("Welcome", hero[0].GetProperty("fields").GetProperty("headline").GetString());
    }

    private static ContentDocument Document(bool isDataModule)
    {
        var record = new ContentRecord(
            Identity: new ContentIdentity("module-1", "hero-block", "module-1", "hero", "published"),
            Presentation: new ContentPresentation("Hero Block", null, null, "ms-MY", Array.Empty<string>()),
            Classification: new ContentClassification("hero", "homepage", Array.Empty<string>(), Array.Empty<string>()),
            Ownership: new ContentOwnership(null, null, null, null),
            Lifecycle: new ContentLifecycle(DateTimeOffset.UtcNow, null, null, null),
            Provenance: new ProvenanceRecord("homepage", null, Array.Empty<string>(), Array.Empty<string>(), null),
            Trust: new TrustMetadata(null, "approved", Array.Empty<string>()),
            Entities: Array.Empty<EntityRecord>(),
            Relations: Array.Empty<ContentRelation>(),
            Media: Array.Empty<MediaAsset>());

        return new ContentDocument(
            Record: record,
            Body: new ContentBodyRef(null, null, null, null),
            Route: new ContentRoutePolicy(null, null, null, null, "homepage"),
            Publish: new ContentPublishPolicy(false, false, false, false, false, false, isDataModule),
            CustomFields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["headline"] = new("text", "Welcome")
            },
            Diagnostics: Array.Empty<ContentDiagnostic>());
    }
}
