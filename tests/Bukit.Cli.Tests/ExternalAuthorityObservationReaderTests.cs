using Bukit.Cli.Commands.SeoAuthorityInsights;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class ExternalAuthorityObservationReaderTests : IDisposable
{
    private const string QuestionKey = "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string TopicKey = "topic:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string EntityKey = "entity:sha256:fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
    private const string ContextHash = "context:sha256:9876543210abcdef9876543210abcdef9876543210abcdef9876543210abcdef";

    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "bukit-external-authority-reader-tests-" + Guid.NewGuid().ToString("N"));

    public ExternalAuthorityObservationReaderTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => TestCleanup.DeleteDirectory(_tempDir, recursive: true);

    [Fact]
    public void Read_ValidDataset_ReturnsRowsAndPreservesLifecycle()
    {
        var path = WriteJson(DatasetJson(
            RowJson(status: "active"),
            RowJson(status: "deleted", observedAt: "2026-08-02T00:00:00Z")));

        var dataset = ExternalAuthorityObservationReader.Read(path);

        Assert.Equal("https://bukit.dev/schemas/external-authority-observation.v1.json", dataset.Schema);
        Assert.Equal("1.0", dataset.SchemaVersion);
        Assert.Equal("approved-provider", dataset.Provider);
        Assert.Equal("api", dataset.CollectionMethod);
        Assert.Equal(2, dataset.Rows.Count);
        Assert.Equal("active", dataset.Rows[0].Status);
        Assert.Equal("deleted", dataset.Rows[1].Status);
        Assert.Equal(QuestionKey, dataset.Rows[0].QuestionKey);
        Assert.Null(dataset.Rows[0].TopicKey);
        Assert.Equal(ContextHash, dataset.Rows[0].ContextHash);
        Assert.Equal(["https://example.com/guide/"], dataset.Rows[0].CitedUrls);
    }

    [Fact]
    public void Read_RemotePath_ThrowsBeforeAnyNetworkAccess()
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => ExternalAuthorityObservationReader.Read("https://attacker.example/authority.json"));

        Assert.StartsWith("external_authority_observation.path_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_OversizeFile_ThrowsWithoutParsing()
    {
        var path = Path.Combine(_tempDir, "oversize.json");
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        {
            stream.SetLength(ExternalAuthorityObservationReader.MaximumFileBytes + 1);
        }

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.file_too_large", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MalformedJson_ThrowsStableCode()
    {
        var path = Path.Combine(_tempDir, "malformed.json");
        File.WriteAllText(path, "{ not json");

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.json_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_UnknownField_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson(extraField: "\"score\": 5,")));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.unknown_field", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_DuplicateField_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson(extraField: "\"status\": \"active\",")));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.duplicate_field", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_InvalidSourceType_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson(sourceType: "influencer")));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.source_type_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_InvalidStatus_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson(status: "archived")));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.status_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MissingAllIdentityKeys_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson(
            questionKey: "null", topicKey: "null", entityKey: "null")));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.identity_keys_missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_InvalidIdentityKeyFormat_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson(questionKey: "\"question:sha256:tooshort\"")));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.identity_key_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_NonHttpSourceUrl_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson(sourceUrl: "\"ftp://source.example/discussion\"")));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.source_url_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_CredentialBearingSourceUrl_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson(
            sourceUrl: "\"https://source-user:source-secret@source.example/discussion\"")));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.source_url_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_InvalidContextHash_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson(contextHash: "\"context:sha256:xyz\"")));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.context_hash_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_NonHttpCitedUrl_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson(citedUrls: "[\"ftp://example.com/file\"]")));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.cited_urls_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_CredentialBearingCitedUrl_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson(
            citedUrls: "[\"https://citation-user:citation-secret@example.com/guide/\"]")));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.cited_urls_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_DuplicateCitedUrl_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson(
            citedUrls: "[\"https://example.com/guide/\", \"https://example.com/guide/\"]")));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.cited_url_duplicate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_ObservedAfterCollected_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson(observedAt: "2026-08-06T00:00:00Z")));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.observed_at_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RowLimitExceeded_ThrowsStableCode()
    {
        var rows = string.Join(
            "," + Environment.NewLine,
            Enumerable.Range(0, ExternalAuthorityObservationReader.MaximumRows + 1).Select(_ => RowJson()));
        var path = WriteJson(DatasetJsonText(rows));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.row_limit_exceeded", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_InvalidCollectionMethod_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJsonWithMethod(RowJson(), collectionMethod: "scrape"));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.collection_method_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MissingField_ThrowsStableCode()
    {
        var path = WriteJson(DatasetJson(RowJson()).Replace("\"contextHash\": \"" + ContextHash + "\",", string.Empty));

        var exception = Assert.Throws<InvalidDataException>(() => ExternalAuthorityObservationReader.Read(path));

        Assert.StartsWith("external_authority_observation.field_required", exception.Message, StringComparison.Ordinal);
    }

    private string WriteJson(string json)
    {
        var path = Path.Combine(_tempDir, $"dataset-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string DatasetJson(params string[] rows)
        => DatasetJsonText(string.Join("," + Environment.NewLine, rows));

    private static string DatasetJsonWithMethod(string rowsCsv, string collectionMethod)
        => DatasetJsonText(rowsCsv, collectionMethod);

    private static string DatasetJsonText(string rowsCsv, string collectionMethod = "api") => $$"""
        {
          "schema": "https://bukit.dev/schemas/external-authority-observation.v1.json",
          "schemaVersion": "1.0",
          "provider": "approved-provider",
          "collectedAt": "2026-08-05T00:00:00Z",
          "collectionMethod": "{{collectionMethod}}",
          "rows": [
            {{rowsCsv}}
          ]
        }
        """;

    private static string RowJson(
        string sourceUrl = "\"https://source.example/discussion/1\"",
        string sourceType = "forum",
        string observedAt = "2026-08-05T00:00:00Z",
        string status = "active",
        string questionKey = "\"" + QuestionKey + "\"",
        string topicKey = "null",
        string entityKey = "null",
        string contextHash = "\"" + ContextHash + "\"",
        string citedUrls = "[\"https://example.com/guide/\"]",
        string extraField = "") => $$"""
        {
          {{extraField}}
          "sourceUrl": {{sourceUrl}},
          "sourceType": "{{sourceType}}",
          "observedAt": "{{observedAt}}",
          "status": "{{status}}",
          "questionKey": {{questionKey}},
          "topicKey": {{topicKey}},
          "entityKey": {{entityKey}},
          "contextHash": {{contextHash}},
          "citedUrls": {{citedUrls}}
        }
        """;
}
