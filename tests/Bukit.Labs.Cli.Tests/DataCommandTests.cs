using System.Text.Json;
using Bukit.Engine.Abstractions.Content;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class DataCommandTests
{
    [Fact]
    public async Task PrintModuleSummary_WhenNoDataDocuments_PrintsNone()
    {
        var result = await CommandTestSupport.CaptureAsync(() =>
        {
            DataCommand.PrintModuleSummary([]);
            return Task.FromResult(0);
        });

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Data modules: (none)", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrintModuleSummary_GroupsModulesAndShowsFields()
    {
        var documents = new[]
        {
            CreateDataDocument(
                id: "hero-1",
                title: "Hero",
                slug: "hero",
                type: "hero",
                sourceKey: "homepage",
                sourceMode: "data",
                language: "zh",
                fields: new Dictionary<string, object>
                {
                    ["headline"] = "Hello",
                    ["enabled"] = true
                }),
            CreateDataDocument(
                id: "hero-2",
                title: "Hero Secondary",
                slug: "hero-secondary",
                type: "hero",
                sourceKey: "homepage",
                sourceMode: "data",
                language: "en",
                fields: new Dictionary<string, object>
                {
                    ["headline"] = "World"
                })
        };

        var result = await CommandTestSupport.CaptureAsync(() =>
        {
            DataCommand.PrintModuleSummary(documents);
            return Task.FromResult(0);
        });

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Data modules:", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("hero", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("source=homepage", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("lang=mixed", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("[enabled, headline, language, sourceKey, sourceMode, type]", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrintModuleDetail_WhenModuleMissing_PrintsMessage()
    {
        var documents = new[]
        {
            CreateDataDocument(
                id: "hero-1",
                title: "Hero",
                slug: "hero",
                type: "hero",
                sourceKey: "homepage",
                sourceMode: "data",
                language: "zh")
        };

        var result = await CommandTestSupport.CaptureAsync(() =>
        {
            DataCommand.PrintModuleDetail(documents, "pricing");
            return Task.FromResult(0);
        });

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Module 'pricing' not found.", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void DumpModulesJson_WritesTypedFieldValues()
    {
        var documents = new[]
        {
            CreateDataDocument(
                id: "pricing-1",
                title: "Pricing",
                slug: "pricing",
                type: "pricing",
                sourceKey: "homepage",
                sourceMode: "data",
                language: "zh",
                fields: new Dictionary<string, object>
                {
                    ["headline"] = "Starter",
                    ["featured"] = true,
                    ["plans"] = 3
                })
        };

        var json = DataCommand.DumpModulesJson(documents);

        using var document = JsonDocument.Parse(json);
        var pricing = document.RootElement.GetProperty("modules").GetProperty("pricing");
        Assert.Equal("pricing-1", pricing[0].GetProperty("id").GetString());
        Assert.True(pricing[0].GetProperty("fields").GetProperty("featured").GetBoolean());
        Assert.Equal(3, pricing[0].GetProperty("fields").GetProperty("plans").GetInt32());
    }

    private static ContentDocument CreateDataDocument(
        string id,
        string title,
        string slug,
        string type,
        string sourceKey,
        string sourceMode,
        string language,
        Dictionary<string, object>? fields = null)
    {
        fields ??= [];
        fields["type"] = type;
        fields["sourceKey"] = sourceKey;
        fields["sourceMode"] = sourceMode;
        fields["language"] = language;

        return new ContentDocument(
            new ContentRecord(
                new ContentIdentity(id, slug, slug, type, "published"),
                new ContentPresentation(title, null, null, language, Array.Empty<string>()),
                new ContentClassification(type, "module", Array.Empty<string>(), Array.Empty<string>()),
                new ContentOwnership(null, null, null, null),
                new ContentLifecycle(DateTimeOffset.UtcNow, null, null, null),
                new ProvenanceRecord(null, null, Array.Empty<string>(), Array.Empty<string>(), null),
                new TrustMetadata(null, "published", Array.Empty<string>()),
                Array.Empty<EntityRecord>(),
                Array.Empty<ContentRelation>(),
                Array.Empty<MediaAsset>()),
            new ContentBodyRef(),
            customFields: ContentFieldReader.ToFieldMap(fields));
    }
}
