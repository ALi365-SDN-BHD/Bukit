using System.Security.Cryptography;
using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Content.CloudBase;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Engine.Stages;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class CloudBaseStructuredFieldTests
{
    // The composite provider prefixes the source key to the CloudBase row id.
    private const string RowId = "row-7f3e";
    private const string RowDocumentId = "routes:" + RowId;

    [Fact]
    public async Task RejectedField_StrictCollection_BlocksBuild()
    {
        var logger = new RecordingLogger();

        var exception = await Assert.ThrowsAsync<ConfigException>(() =>
            new ContentGraphValidateStage().ExecuteAsync(Input("strict", logger), CancellationToken.None));

        Assert.Equal(DiagnosticCode.SchemaStrictModeBlocked, exception.Code);
        Assert.Contains(logger.Warnings, warning =>
            warning.Contains(CloudBaseContentSource.StructuredFieldInvalidCode, StringComparison.Ordinal) &&
            warning.Contains("field=deep", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RejectedField_Log_NamesRowOnlyByHash()
    {
        var logger = new RecordingLogger();

        await new ContentGraphValidateStage().ExecuteAsync(Input("warn", logger), CancellationToken.None);

        var warning = Assert.Single(logger.Warnings, warning =>
            warning.Contains(CloudBaseContentSource.StructuredFieldInvalidCode, StringComparison.Ordinal));
        Assert.Contains($"source={CloudBaseContentSource.LogId(RowDocumentId)} ", warning, StringComparison.Ordinal);
        Assert.DoesNotContain(RowId, warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectedField_WarnCollection_LogsWithoutBlocking()
    {
        var logger = new RecordingLogger();

        var output = await new ContentGraphValidateStage().ExecuteAsync(Input("warn", logger), CancellationToken.None);

        Assert.Contains(output.SchemaErrors!, issue => issue.Code == CloudBaseContentSource.StructuredFieldInvalidCode);
        Assert.Contains(logger.Warnings, warning => warning.Contains("maximum nesting depth of 8", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RejectedField_OffCollection_IsIgnored()
    {
        var logger = new RecordingLogger();

        var output = await new ContentGraphValidateStage().ExecuteAsync(Input("off", logger), CancellationToken.None);

        Assert.Empty(output.SchemaErrors!);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void StructuredField_SurvivesNormalizationAndHashesStably()
    {
        var first = Normalize("""{"days":[{"title":"D1","nights":1,"price":12.50}],"months":[3,4,5]}""");
        var second = Normalize("""{"months":[3,4,5],"days":[{"price":12.50,"nights":1,"title":"D1"}]}""");

        Assert.Equal("list", first.CustomFields!["days"].Type);
        Assert.IsType<List<object?>>(first.CustomFields["months"].Value);
        Assert.Equal(Hash(first.CustomFields), Hash(second.CustomFields!));
        Assert.NotEqual(Hash(first.CustomFields), Hash(Normalize("""{"days":[{"title":"D1","nights":2,"price":12.50}],"months":[3,4,5]}""").CustomFields!));
    }

    private static ContentDocument Normalize(string row)
    {
        var fields = CloudBaseFieldProjector.ProjectWithSet(
            JsonDocument.Parse(row).RootElement,
            "all",
            CloudBaseFieldProjector.BuildAllowedSet("all", null),
            "id");
        var raw = new RawContentDocument("a1", "cloudbase", "A", "a", null, new RawBody(), CustomFields: fields);
        return Assert.Single(ContentDocumentNormalizer.ToDocuments([raw]));
    }

    private static string Hash(IReadOnlyDictionary<string, ContentField> fields)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        new RenderDependencyHashWriter(hasher).AppendCanonicalValue(fields);
        return Convert.ToHexString(hasher.GetHashAndReset());
    }

    private static ContentStageInput Input(string collectionMode, ILogger logger)
    {
        var raw = new RawContentDocument(
            RowDocumentId,
            "cloudbase",
            "A",
            "a",
            null,
            new RawBody(),
            Source: new ContentSourceInfo("cloudbase", SourceKey: "routes", SourcePath: "routes", ExternalId: RowId),
            CustomFields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = new("text", "routes"),
                ["type"] = new("text", "routes")
            })
        {
            Diagnostics =
            [
                new ContentDiagnostic(
                    CloudBaseContentSource.StructuredFieldInvalidCode,
                    "error",
                    "CloudBase field 'deep' value at 'a[0]' exceeds the maximum nesting depth of 8. The field was dropped.",
                    "deep")
            ]
        };
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["routes"] = new() { Permalink = "/routes/{slug}/", SchemaFailMode = collectionMode }
                }
            },
            Content = TestContent.Markdown(),
            Build = new BuildConfig { SchemaFailMode = "warn" }
        };
        return new ContentStageInput(
            ContentDocumentNormalizer.ToDocuments([raw]),
            EmptyContentBodyStore.Instance,
            config,
            new ConfigOverrides(),
            "/root",
            "/cache",
            logger);
    }

    private sealed class RecordingLogger : ILogger
    {
        internal List<string> Warnings { get; } = new();

        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) { }
    }
}
