using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Analytics;
using Bukit.Shared;
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
                "executionMode", "providerTypes", "googleConsent", "csp", "processedHtml",
                "injectedHtml", "skippedByReason"
            },
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("https://bukit.dev/schemas/analytics-report.v2.json", root.GetProperty("schema").GetString());
        Assert.Equal("2.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("production", root.GetProperty("executionMode").GetString());
        Assert.Equal("google-analytics", Assert.Single(root.GetProperty("providerTypes").EnumerateArray()).GetString());
        var consent = root.GetProperty("googleConsent");
        Assert.Equal("advanced", consent.GetProperty("mode").GetString());
        Assert.Equal("denied", consent.GetProperty("defaults").GetProperty("analyticsStorage").GetString());
        Assert.Equal(500, consent.GetProperty("waitForUpdateMs").GetInt32());
        var csp = root.GetProperty("csp");
        Assert.Equal("requirements-report", csp.GetProperty("mode").GetString());
        Assert.False(csp.GetProperty("completePolicy").GetBoolean());
        Assert.Equal(
            ["https://www.googletagmanager.com"],
            csp.GetProperty("scriptSrcOrigins").EnumerateArray().Select(value => value.GetString()));
        Assert.Empty(csp.GetProperty("frameSrcOrigins").EnumerateArray());
        Assert.False(csp.GetProperty("dynamicContainerDestinationsUnknown").GetBoolean());
        var hashes = csp.GetProperty("inlineScriptSha256")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Equal(2, hashes.Length);
        Assert.All(hashes, hash => Assert.Matches("^sha256-[A-Za-z0-9+/]{43}=$", hash));
        Assert.Equal(1, root.GetProperty("processedHtml").GetInt32());
        Assert.Equal(1, root.GetProperty("injectedHtml").GetInt32());
        Assert.DoesNotContain("G-SECRET123", json, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", json, StringComparison.Ordinal);
        Assert.DoesNotContain("script.js", json, StringComparison.Ordinal);
        AssertMatchesSchema(root, LoadSchema().RootElement);
    }

    [Fact]
    public void WriteIfEnabled_WhenSerializationFails_PreservesPreviousReportAndLeavesNoTempFiles()
    {
        var config = CreateConfig(reportEnabled: true);
        var snapshot = AnalyticsBuildState.Create(config, BuildExecutionMode.Production).Snapshot();
        var reportDir = Path.Combine(_outputDir, ".bukit");
        var reportPath = Path.Combine(reportDir, "analytics-report.json");
        AnalyticsReportWriter.WriteIfEnabled(config, _outputDir, snapshot);
        var previousBytes = File.ReadAllBytes(reportPath);
        var invalidSnapshot = snapshot with
        {
            ProviderTypes = new ThrowingProviderTypes()
        };

        Assert.Throws<IOException>(() =>
            AnalyticsReportWriter.WriteIfEnabled(config, _outputDir, invalidSnapshot));

        Assert.Equal(previousBytes, File.ReadAllBytes(reportPath));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(reportDir),
            path => Path.GetFileName(path).EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteIfEnabled_CspHashesMatchExactGeneratedInlineScriptBodiesWithoutRenderingPagesFirst()
    {
        var config = CreateConfig(reportEnabled: true);
        var snapshot = AnalyticsBuildState.Create(config, BuildExecutionMode.Production).Snapshot();

        AnalyticsReportWriter.WriteIfEnabled(config, _outputDir, snapshot);

        var resolved = AnalyticsConfigNormalizer.Normalize(config.Site.Analytics);
        var transformed = new AnalyticsHtmlTransform(
            resolved,
            AnalyticsProviderRegistry.CreateDefault()).Transform(
                new HtmlTransformContext(
                    "/", "index.html", HtmlDocumentKind.Content,
                    BuildExecutionMode.Production, new ConsoleLogger(LogLevel.Error)),
                "<html><head></head><body></body></html>");
        var expected = ExtractInlineScriptBodies(transformed)
            .Select(body => "sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body))))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        using var report = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(_outputDir, ".bukit", "analytics-report.json")));
        var actual = report.RootElement.GetProperty("csp").GetProperty("inlineScriptSha256")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WriteIfEnabled_CspRequirementsCoverAllProviderOriginsAndFlagDynamicGtmDestinations()
    {
        var baseConfig = CreateConfig(reportEnabled: true);
        var config = baseConfig with
        {
            Site = baseConfig.Site with
            {
                Analytics = baseConfig.Site.Analytics with
                {
                    Providers =
                    [
                        new AnalyticsProviderConfig { Type = "google-analytics", MeasurementId = "G-PRIVATE1" },
                        new AnalyticsProviderConfig { Type = "google-tag-manager", ContainerId = "GTM-PRIVATE2" },
                        new AnalyticsProviderConfig
                        {
                            Type = "plausible",
                            Domain = "private.example",
                            SnippetMode = "site-specific",
                            ScriptUrl = "https://plausible.io/js/pa-PRIVATE3.js"
                        },
                        new AnalyticsProviderConfig
                        {
                            Type = "umami",
                            WebsiteId = "00000000-0000-0000-0000-000000000004",
                            ScriptUrl = "https://metrics.example.net/private/script.js"
                        }
                    ]
                }
            }
        };

        AnalyticsReportWriter.WriteIfEnabled(
            config,
            _outputDir,
            AnalyticsBuildState.Create(config, BuildExecutionMode.Production).Snapshot());

        var json = File.ReadAllText(Path.Combine(_outputDir, ".bukit", "analytics-report.json"));
        using var report = JsonDocument.Parse(json);
        var csp = report.RootElement.GetProperty("csp");
        Assert.Equal(
            ["https://metrics.example.net", "https://plausible.io", "https://www.googletagmanager.com"],
            csp.GetProperty("scriptSrcOrigins").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            ["https://www.googletagmanager.com"],
            csp.GetProperty("frameSrcOrigins").EnumerateArray().Select(value => value.GetString()));
        Assert.True(csp.GetProperty("dynamicContainerDestinationsUnknown").GetBoolean());
        Assert.DoesNotContain("G-PRIVATE1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("GTM-PRIVATE2", json, StringComparison.Ordinal);
        Assert.DoesNotContain("pa-PRIVATE3.js", json, StringComparison.Ordinal);
        Assert.DoesNotContain("00000000-0000-0000-0000-000000000004", json, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteIfEnabled_WhenConsentAndCspAreNotConfigured_WritesExplicitNulls()
    {
        var baseConfig = CreateConfig(reportEnabled: true);
        var config = baseConfig with
        {
            Site = baseConfig.Site with
            {
                Analytics = new AnalyticsConfig
                {
                    Providers =
                    [
                        new AnalyticsProviderConfig
                        {
                            Type = "plausible",
                            Domain = "example.com",
                            SnippetMode = "legacy",
                            ScriptUrl = "https://plausible.io/js/script.js"
                        }
                    ]
                }
            }
        };

        AnalyticsReportWriter.WriteIfEnabled(
            config,
            _outputDir,
            AnalyticsBuildState.Create(config, BuildExecutionMode.Production).Snapshot());

        using var report = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(_outputDir, ".bukit", "analytics-report.json")));
        Assert.Equal(JsonValueKind.Null, report.RootElement.GetProperty("googleConsent").ValueKind);
        Assert.Equal(JsonValueKind.Null, report.RootElement.GetProperty("csp").ValueKind);
        AssertMatchesSchema(report.RootElement, LoadSchema().RootElement);
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
        Assert.Equal("https://bukit.dev/schemas/analytics-report.v2.json", root.GetProperty("$id").GetString());
        Assert.Equal(12, root.GetProperty("required").GetArrayLength());
        var reasons = root.GetProperty("properties").GetProperty("skippedByReason")
            .GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(AnalyticsSkipReason.All, reasons);
    }

    private static JsonDocument LoadSchema()
    {
        var repoRoot = FindRepositoryRoot();
        return JsonDocument.Parse(File.ReadAllText(
            Path.Combine(repoRoot, "docs", "schemas", "analytics-report.v2.schema.json")));
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
            var actualType = instance.ValueKind switch
            {
                JsonValueKind.Object => "object",
                JsonValueKind.Array => "array",
                JsonValueKind.String => "string",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Number => "integer",
                JsonValueKind.Null => "null",
                _ => instance.ValueKind.ToString().ToLowerInvariant()
            };
            if (type.ValueKind == JsonValueKind.Array)
            {
                Assert.Contains(type.EnumerateArray(), value => value.GetString() == actualType);
            }
            else
            {
                Assert.Equal(type.GetString(), actualType);
            }
        }

        if (instance.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (schema.TryGetProperty("minimum", out var minimum))
        {
            Assert.True(instance.GetInt64() >= minimum.GetInt64());
        }

        if (schema.TryGetProperty("maximum", out var maximum))
        {
            Assert.True(instance.GetInt64() <= maximum.GetInt64());
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
                    Consent = new AnalyticsConsentConfig
                    {
                        Google = new AnalyticsGoogleConsentConfig
                        {
                            Mode = "advanced",
                            Defaults = new AnalyticsGoogleConsentDefaultsConfig
                            {
                                AdStorage = "denied",
                                AnalyticsStorage = "denied",
                                AdUserData = "denied",
                                AdPersonalization = "denied"
                            },
                            WaitForUpdateMs = 500
                        }
                    },
                    Csp = new AnalyticsCspConfig { Mode = "requirements-report" },
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

    private static IEnumerable<string> ExtractInlineScriptBodies(string html)
    {
        var index = 0;
        while ((index = html.IndexOf("<script", index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var tagEnd = html.IndexOf('>', index);
            var close = html.IndexOf("</script>", tagEnd + 1, StringComparison.OrdinalIgnoreCase);
            Assert.True(tagEnd >= 0 && close >= 0);
            var openingTag = html[index..(tagEnd + 1)];
            if (!openingTag.Contains(" src=", StringComparison.OrdinalIgnoreCase))
            {
                yield return html[(tagEnd + 1)..close];
            }

            index = close + "</script>".Length;
        }
    }

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

    private sealed class ThrowingProviderTypes : IReadOnlyList<string>
    {
        public int Count => 2;

        public string this[int index] => index switch
        {
            0 => "google-analytics",
            1 => throw new IOException("Injected provider enumeration failure."),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        public IEnumerator<string> GetEnumerator()
        {
            yield return this[0];
            yield return this[1];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}
