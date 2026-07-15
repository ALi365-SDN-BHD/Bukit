using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Analytics;
using Xunit;

namespace Bukit.Engine.Tests.Analytics;

public sealed class AnalyticsReportWriterTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(
        Path.GetTempPath(), "bukit-analytics-report-tests", Guid.NewGuid().ToString("N"));

    public AnalyticsReportWriterTests() => Directory.CreateDirectory(_outputDir);

    public void Dispose() => TestCleanup.DeleteDirectory(_outputDir, recursive: true);

    [Fact]
    public void WriteIfEnabled_WritesExactContractWithoutProviderSecrets()
    {
        var config = CreateConfig(reportEnabled: true);
        var state = AnalyticsBuildState.Create(config, BuildExecutionMode.Production);
        state.RecordProcessed();
        state.RecordInjected();
        state.RecordSkipped(AnalyticsSkipReason.HeadMissing);

        AnalyticsReportWriter.WriteIfEnabled(config, _outputDir, state.Snapshot());

        var json = File.ReadAllText(Path.Combine(_outputDir, ".bukit", "analytics-report.json"));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(
            new[]
            {
                "schema", "schemaVersion", "pluginEnabled", "analyticsEnabled", "productionOnly",
                "executionMode", "providerTypes", "processedHtml", "injectedHtml", "skippedByReason"
            },
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("https://bukit.dev/schemas/analytics-report.v1.json", root.GetProperty("schema").GetString());
        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("production", root.GetProperty("executionMode").GetString());
        Assert.Equal("google-analytics", Assert.Single(root.GetProperty("providerTypes").EnumerateArray()).GetString());
        Assert.Equal(1, root.GetProperty("processedHtml").GetInt32());
        Assert.Equal(1, root.GetProperty("injectedHtml").GetInt32());
        Assert.DoesNotContain("G-SECRET123", json, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", json, StringComparison.Ordinal);
        Assert.DoesNotContain("script.js", json, StringComparison.Ordinal);
        AssertMatchesSchema(root, LoadSchema().RootElement);
    }

    [Fact]
    public void WriteIfEnabled_WhenBuildReportDisabled_DoesNotWrite()
    {
        var config = CreateConfig(reportEnabled: false);
        var state = AnalyticsBuildState.Create(config, BuildExecutionMode.Development);

        AnalyticsReportWriter.WriteIfEnabled(config, _outputDir, state.Snapshot());

        Assert.False(File.Exists(Path.Combine(_outputDir, ".bukit", "analytics-report.json")));
    }

    [Fact]
    public void WriteIfEnabled_WhenBuildReportIsDisabled_RemovesAStaleReport()
    {
        var enabled = CreateConfig(reportEnabled: true);
        var disabled = CreateConfig(reportEnabled: false);
        var reportPath = Path.Combine(_outputDir, ".bukit", "analytics-report.json");
        AnalyticsReportWriter.WriteIfEnabled(
            enabled,
            _outputDir,
            AnalyticsBuildState.Create(enabled, BuildExecutionMode.Production).Snapshot());
        Assert.True(File.Exists(reportPath));

        AnalyticsReportWriter.WriteIfEnabled(
            disabled,
            _outputDir,
            AnalyticsBuildState.Create(disabled, BuildExecutionMode.Production).Snapshot());

        Assert.False(File.Exists(reportPath));
    }

    [Fact]
    public void Schema_IsStrictAndMatchesWriterContract()
    {
        using var schema = LoadSchema();
        var root = schema.RootElement;
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("https://bukit.dev/schemas/analytics-report.v1.json", root.GetProperty("$id").GetString());
        Assert.Equal(10, root.GetProperty("required").GetArrayLength());
        var reasons = root.GetProperty("properties").GetProperty("skippedByReason")
            .GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(AnalyticsSkipReason.All, reasons);
    }

    private static JsonDocument LoadSchema()
    {
        var repoRoot = FindRepositoryRoot();
        return JsonDocument.Parse(File.ReadAllText(
            Path.Combine(repoRoot, "docs", "schemas", "analytics-report.v1.schema.json")));
    }

    private static void AssertMatchesSchema(JsonElement instance, JsonElement schema)
    {
        if (schema.TryGetProperty("const", out var constant))
        {
            Assert.Equal(constant.GetRawText(), instance.GetRawText());
        }

        if (schema.TryGetProperty("enum", out var allowed))
        {
            Assert.Contains(allowed.EnumerateArray(), value => value.GetRawText() == instance.GetRawText());
        }

        if (schema.TryGetProperty("type", out var type))
        {
            Assert.Equal(type.GetString(), instance.ValueKind switch
            {
                JsonValueKind.Object => "object",
                JsonValueKind.Array => "array",
                JsonValueKind.String => "string",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Number => "integer",
                _ => instance.ValueKind.ToString().ToLowerInvariant()
            });
        }

        if (schema.TryGetProperty("minimum", out var minimum))
        {
            Assert.True(instance.GetInt64() >= minimum.GetInt64());
        }

        if (instance.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var items))
        {
            foreach (var item in instance.EnumerateArray())
            {
                AssertMatchesSchema(item, items);
            }
        }

        if (instance.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var properties = schema.TryGetProperty("properties", out var propertySchemas)
            ? propertySchemas
            : default;
        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var requiredName in required.EnumerateArray().Select(value => value.GetString()!))
            {
                Assert.True(instance.TryGetProperty(requiredName, out _), $"Missing required property '{requiredName}'.");
            }
        }

        foreach (var property in instance.EnumerateObject())
        {
            var propertySchema = default(JsonElement);
            var isDeclared = properties.ValueKind == JsonValueKind.Object &&
                             properties.TryGetProperty(property.Name, out propertySchema);
            if (!isDeclared)
            {
                Assert.False(
                    schema.TryGetProperty("additionalProperties", out var additional) && !additional.GetBoolean(),
                    $"Unexpected property '{property.Name}'.");
                continue;
            }

            AssertMatchesSchema(property.Value, propertySchema);
        }
    }

    private static AppConfig CreateConfig(bool reportEnabled)
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Analytics = new AnalyticsConfig
                {
                    Providers =
                    [
                        new AnalyticsProviderConfig
                        {
                            Type = "google-analytics",
                            MeasurementId = "G-SECRET123"
                        }
                    ]
                }
            },
            Content = TestContent.Markdown(),
            Build = new BuildConfig { Report = new BuildReportConfig { Enabled = reportEnabled } }
        };

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "bukit-core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
