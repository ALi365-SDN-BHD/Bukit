using System.Reflection;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class ProgramEntryPointTests : IDisposable
{
    private readonly string _tempDir;

    public ProgramEntryPointTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-program-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Main_NoArgs_PrintsHelpAndReturnsZero()
    {
        var result = await InvokeEntryPointAsync([]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: bukit <command> [options]", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Main_Version_PrintsVersionAndRuntime()
    {
        var result = await InvokeEntryPointAsync(["version"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("bukit ", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("runtime: native-aot", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_Version_IgnoresMalformedPluginConfig()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteMalformedPluginConfig();

        var result = await InvokeEntryPointAsync(["version"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("bukit ", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Main_VersionHelp_PrintsCommandUsage()
    {
        var result = await InvokeEntryPointAsync(["version", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("bukit version", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_BuildHelp_IgnoresInvalidPluginManifest()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WritePluginConfigWithInvalidManifest();

        var result = await InvokeEntryPointAsync(["build", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("bukit build", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Main_PluginCommandWithMalformedConfig_ReturnsCliError()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteMalformedPluginConfig();

        var result = await InvokeEntryPointAsync(["echo"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid YAML syntax in plugin config file", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_DevHelp_PrintsLiveReloadDescription()
    {
        var result = await InvokeEntryPointAsync(["dev", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("bukit dev", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("LiveReload", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("HMR", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Main_Clean_PreservesProjectMetadataAndCleansOutputAndCache()
    {
        var outputDir = Path.Combine(_tempDir, "dist");
        var cacheDir = Path.Combine(_tempDir, ".cache");
        var pluginDir = Path.Combine(_tempDir, ".bukit");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(cacheDir);
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(outputDir, ".bukit-output-marker"), "Bukit output directory");
        File.WriteAllText(Path.Combine(cacheDir, "cached.txt"), "disposable");
        var pluginConfig = Path.Combine(pluginDir, "plugins.yaml");
        var pluginLock = Path.Combine(pluginDir, "plugins.lock.yaml");
        var unknown = Path.Combine(pluginDir, "custom.bin");
        byte[] bytes = [0, 255, 128, 10];
        File.WriteAllText(pluginConfig, "version: 1\r\nplugins: {}\r\n");
        File.WriteAllText(pluginLock, "version: 1\nresolved: {}\n");
        File.WriteAllBytes(unknown, bytes);
        using var cwd = new CurrentDirectoryScope(_tempDir);

        var result = await InvokeEntryPointAsync(["clean", "--dir", "dist"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Cleaned:", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
        Assert.False(Directory.Exists(outputDir));
        Assert.False(Directory.Exists(cacheDir));
        Assert.True(File.Exists(pluginConfig), "CLI clean deleted user plugin metadata.");
        Assert.Equal("version: 1\r\nplugins: {}\r\n", File.ReadAllText(pluginConfig));
        Assert.Equal("version: 1\nresolved: {}\n", File.ReadAllText(pluginLock));
        Assert.Equal(bytes, File.ReadAllBytes(unknown));
    }

    [Fact]
    public async Task Main_UnknownCommand_WithJsonLogFormat_PrintsJsonDiagnostic()
    {
        var result = await InvokeEntryPointAsync(["missing-command", "--log-format=json"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.StdOut);

        var envelope = result.StdErr.TrimEnd('\r', '\n');
        ReportContractAssertions.Schema(envelope, "cli-error.v1.schema.json", "exitCode");
        using var payload = System.Text.Json.JsonDocument.Parse(envelope);
        var root = payload.RootElement;
        var error = Assert.Single(root.GetProperty("errors").EnumerateArray().ToArray());

        Assert.Equal("missing-command", root.GetProperty("command").GetString());
        Assert.Equal(2, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("unknown-command", error.GetProperty("code").GetString());
        Assert.Equal(envelope + Environment.NewLine, result.StdErr);
    }

    [Fact]
    public void UnhandledError_WithJsonLogFormat_WritesOneJsonObjectAndDoesNotLeakInnerMessage()
    {
        var programType = typeof(VersionCommand).Assembly.EntryPoint?.DeclaringType
            ?? throw new InvalidOperationException("Missing Bukit.Cli entry point type.");
        var render = programType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(method => method.Name.Contains("PrintUnhandledError", StringComparison.Ordinal));
        Assert.NotNull(render);
        using var stderr = new StringWriter();
        var originalErr = Console.Error;

        try
        {
            Console.SetError(stderr);
            var exitCode = Assert.IsType<int>(render.Invoke(null,
            [
                "build",
                new InvalidOperationException("build failed", new Exception("token=do-not-leak")),
                true
            ]));

            Assert.Equal(1, exitCode);
        }
        finally
        {
            Console.SetError(originalErr);
        }

        var envelope = stderr.ToString().TrimEnd('\r', '\n');
        using var payload = System.Text.Json.JsonDocument.Parse(envelope);
        Assert.Equal("build failed", payload.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
        Assert.DoesNotContain("do-not-leak", stderr.ToString(), StringComparison.Ordinal);
        Assert.Equal(envelope + Environment.NewLine, stderr.ToString());
    }

    [Fact]
    public async Task Main_BuildWithInvalidIntegerOption_ReturnsTwoAndPrintsUsage()
    {
        var result = await InvokeEntryPointAsync(["build", "--jobs", "NaN"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage:", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("bukit build", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_PublishAudit_WithoutReport_ReturnsOne()
    {
        var outputDir = Path.Combine(_tempDir, "dist");
        Directory.CreateDirectory(outputDir);

        var result = await InvokeEntryPointAsync(["publish", "audit", "--dir", outputDir]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Publish report not found under", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_PublishDiff_WithoutBaselineOrCurrent_ReturnsTwo()
    {
        var result = await InvokeEntryPointAsync(["publish", "diff"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage: bukit publish diff --baseline old-report.json --current new-report.json", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_SeoAudit_WithValidReport_ReturnsZero()
    {
        var outputDir = Path.Combine(_tempDir, "seo-dist");
        var reportDir = Path.Combine(outputDir, ".bukit");
        Directory.CreateDirectory(reportDir);
        File.WriteAllText(Path.Combine(reportDir, "seo-report.json"), ValidSeoReportJson());

        var result = await InvokeEntryPointAsync(["seo", "audit", "--dir", outputDir]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("SEO audit: routes=1 errors=0 warnings=0", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_GeoAudit_WithValidReport_ReturnsZero()
    {
        var outputDir = Path.Combine(_tempDir, "geo-dist");
        var reportDir = Path.Combine(outputDir, ".bukit");
        Directory.CreateDirectory(reportDir);
        File.WriteAllText(Path.Combine(reportDir, "geo-report.json"), ValidGeoReportJson());

        var result = await InvokeEntryPointAsync(["geo", "audit", "--dir", outputDir]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("=== GEO Audit ===", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("Schema types: Article", result.StdOut, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("json", false)]
    [InlineData("JSON", true)]
    public async Task LoggerStartup_BuildPreservesWarningFormatAndErrorEnvelope(string format, bool split)
    {
        string path = WriteWarningConfig();
        string[] option = split ? ["--log-format", format] : ["--log-format=" + format];
        var result = await InvokeEntryPointAsync(["build", "--config", path, "--jobs", "0", .. option]);
        Assert.Equal(2, result.ExitCode);
        int end = result.StdErr.IndexOf(Environment.NewLine, StringComparison.Ordinal);
        Assert.StartsWith("{", result.StdErr[..end]);
        using var warning = System.Text.Json.JsonDocument.Parse(result.StdErr[..end]);
        Assert.Equal("Warn", warning.RootElement.GetProperty("level").GetString());
        Assert.StartsWith("build.followSymlinks", warning.RootElement.GetProperty("msg").GetString());
        using var error = System.Text.Json.JsonDocument.Parse(result.StdErr[(end + Environment.NewLine.Length)..]);
        Assert.Equal(2, error.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Contains("jobs", error.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("json", false)]
    [InlineData("JSON", true)]
    public async Task LoggerStartup_NonBuildRetainsGlobalFormat(string format, bool split)
    {
        string path = WriteWarningConfig();
        string[] option = split ? ["--log-format", format] : ["--log-format=" + format];
        var result = await InvokeEntryPointAsync(["config", "check", "--config", path, .. option]);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Config check passed", result.StdOut, StringComparison.Ordinal);
        Assert.StartsWith("{", result.StdErr);
        using var warning = System.Text.Json.JsonDocument.Parse(result.StdErr);
        Assert.Equal("Warn", warning.RootElement.GetProperty("level").GetString());
        Assert.StartsWith("build.followSymlinks", warning.RootElement.GetProperty("msg").GetString());
    }

    [Fact]
    public async Task LoggerStartup_ErrorFiltersWarningAndNextInvocationHasNoState()
    {
        string path = WriteWarningConfig("error");
        var filtered = await InvokeEntryPointAsync(["config", "check", "--config", path, "--log-format=json"]);
        Assert.Equal(0, filtered.ExitCode);
        Assert.Empty(filtered.StdErr);
        var next = await InvokeEntryPointAsync(["version"]);
        Assert.Empty(next.StdErr);
    }

    [Fact]
    public async Task LoggerStartup_LateLoadFailureReplaysBeforeOriginalError()
    {
        string path = WriteWarningConfig("error", lateFailure: true);
        var result = await InvokeEntryPointAsync(["build", "--config", path, "--log-format=JSON"]);
        Assert.Equal(2, result.ExitCode);
        int end = result.StdErr.IndexOf(Environment.NewLine, StringComparison.Ordinal);
        Assert.StartsWith("{", result.StdErr[..end]);
        using var warning = System.Text.Json.JsonDocument.Parse(result.StdErr[..end]);
        Assert.Equal("Warn", warning.RootElement.GetProperty("level").GetString());
        using var error = System.Text.Json.JsonDocument.Parse(result.StdErr[(end + Environment.NewLine.Length)..]);
        Assert.Contains("pageSize", error.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
        Assert.Empty(result.StdOut);
    }

    [Fact]
    public async Task LoggerStartup_PluginDiscoveryUsesRawJsonWithoutSiteConfig()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteMalformedPluginConfig();
        Assert.False(File.Exists(Path.Combine(_tempDir, "site.yaml")));
        var result = await InvokeEntryPointAsync(["echo", "--log-format=JSON"]);
        Assert.Equal(2, result.ExitCode);
        using var error = System.Text.Json.JsonDocument.Parse(result.StdErr);
        Assert.Contains("plugin config", error.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
    }

    private string WriteWarningConfig(string level = "warn", bool lateFailure = false)
    {
        string path = Path.Combine(_tempDir, "site.yaml");
        File.WriteAllText(path, $$"""
            site:
              name: test
              title: Test
              url: https://example.com
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            build:
              followSymlinks: true
            logging:
              level: {{level}}
            {{(lateFailure ? "taxonomy:\n  pageSize: not-an-integer" : "")}}
            """);
        return path;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoggerStartup_BuildErrorLevelAndCiOverride(bool ci)
    {
        string path = WriteWarningConfig("error");
        string[] ciOption = ci ? ["--ci"] : [];
        var result = await InvokeEntryPointAsync(["build", "--config", path, "--jobs", "0", "--log-format=json", .. ciOption]);
        Assert.Equal(2, result.ExitCode);
        string errorText = result.StdErr;
        if (ci)
        {
            int end = errorText.IndexOf(Environment.NewLine, StringComparison.Ordinal);
            using var warning = System.Text.Json.JsonDocument.Parse(errorText[..end]);
            Assert.Equal("Warn", warning.RootElement.GetProperty("level").GetString());
            errorText = errorText[(end + Environment.NewLine.Length)..];
        }
        using var error = System.Text.Json.JsonDocument.Parse(errorText);
        Assert.Contains("jobs", error.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task LoggerStartup_ValidationFailureAfterSuccessfulLoadDoesNotReplayBootstrap()
    {
        string path = WriteWarningConfig("error");
        File.WriteAllText(path, File.ReadAllText(path).Replace("url: https://example.com", "baseUrl: relative", StringComparison.Ordinal));
        var result = await InvokeEntryPointAsync(["config", "check", "--config", path, "--log-format=json"]);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Config error", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task LoggerStartup_PreviewImplicitLateFailureUsesNearestConfigAndNeverStartsServer()
    {
        string path = WriteWarningConfig(lateFailure: true);
        string previewDir = Path.Combine(_tempDir, "dist", "nested");
        Directory.CreateDirectory(previewDir);
        var result = await InvokeEntryPointAsync(["preview", "--dir", previewDir, "--port", "0", "--log-format=json"]);
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.StdOut);
        int end = result.StdErr.IndexOf(Environment.NewLine, StringComparison.Ordinal);
        using var warning = System.Text.Json.JsonDocument.Parse(result.StdErr[..end]);
        Assert.Equal("Warn", warning.RootElement.GetProperty("level").GetString());
        string policy = result.StdErr[(end + Environment.NewLine.Length)..];
        Assert.StartsWith("Preview analytics policy error:", policy);
        Assert.Contains(path, policy, StringComparison.Ordinal);
        Assert.Contains("pageSize", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoggerStartup_PreviewExplicitConfigKeepsPriorityAndSelectedFormat()
    {
        string path = WriteWarningConfig();
        string preview = Path.Combine(_tempDir, "dist");
        Directory.CreateDirectory(preview);
        File.WriteAllText(Path.Combine(preview, "site.yaml"), "site: [");
        var result = await InvokeEntryPointAsync(["preview", "--config", path, "--dir", Path.Combine(preview, "missing"), "--log-format=json"]);
        Assert.Equal(2, result.ExitCode);
        int end = result.StdErr.IndexOf(Environment.NewLine, StringComparison.Ordinal);
        using var warning = System.Text.Json.JsonDocument.Parse(result.StdErr[..end]);
        Assert.Equal("Warn", warning.RootElement.GetProperty("level").GetString());
        Assert.StartsWith("Directory not found:", result.StdErr[(end + Environment.NewLine.Length)..]);
        Assert.Empty(result.StdOut);
    }

    [Fact]
    public async Task LoggerStartup_DevLanRefusalPrecedesMissingConfigAndUsesJsonBootstrap()
    {
        var result = await InvokeEntryPointAsync(["dev", "--config", Path.Combine(_tempDir, "missing.yaml"), "--host", "0.0.0.0", "--log-format=JSON"]);
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.StdOut);
        using var json = System.Text.Json.JsonDocument.Parse(result.StdErr);
        Assert.Equal("Error", json.RootElement.GetProperty("level").GetString());
        Assert.Contains("refused", json.RootElement.GetProperty("msg").GetString());
    }

    [Fact]
    public async Task LoggerStartup_InvalidBuildFormatStopsBeforeConfigurationLoad()
    {
        string path = WriteWarningConfig();
        var result = await InvokeEntryPointAsync(["build", "--config", path, "--log-format=invalid"]);
        Assert.Equal(2, result.ExitCode);
        Assert.DoesNotContain("followSymlinks", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("--log-format", result.StdErr, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeEntryPointAsync(string[] args)
    {
        var entryPoint = typeof(VersionCommand).Assembly.EntryPoint ?? throw new InvalidOperationException("Missing Bukit.Cli entry point.");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var result = entryPoint.Invoke(null, [args]);
            var exitCode = result switch
            {
                Task<int> task => await task,
                Task task => await AwaitAndReturnZeroAsync(task),
                int code => code,
                _ => throw new InvalidOperationException($"Unsupported entry point return type: {result?.GetType().FullName ?? "null"}")
            };

            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static async Task<int> AwaitAndReturnZeroAsync(Task task)
    {
        await task;
        return 0;
    }

    private void WriteMalformedPluginConfig()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".bukit"));
        File.WriteAllText(Path.Combine(_tempDir, ".bukit", "plugins.yaml"), "version: [");
    }

    private void WritePluginConfigWithInvalidManifest()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".bukit"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "plugins", "broken"));
        File.WriteAllText(Path.Combine(_tempDir, ".bukit", "plugins.yaml"),
            """
            version: 1
            plugins:
              broken:
                enabled: true
                source: plugins/broken
                allowInCi: true
                permissions:
                  network: false
            """);
        File.WriteAllText(Path.Combine(_tempDir, "plugins", "broken", "plugin.yaml"), "id: broken");
    }

    private static string ValidSeoReportJson() => """
        {
          "schema": "https://bukit.dev/schemas/seo-report.v1.json",
          "schemaVersion": "1.0",
          "generatedAt": "2026-01-01T00:00:00Z",
          "siteName": "test",
          "baseUrl": "https://example.com",
          "routes": [
            {
              "url": "/",
              "outputPath": "index.html",
              "title": "Home",
              "description": "Welcome",
              "canonical": "https://example.com/",
              "indexable": true,
              "lastModified": "2026-01-01",
              "sitemapIncluded": true,
              "searchIncluded": true,
              "rssIncluded": false,
              "alternates": [],
              "schemaTypes": ["WebPage"]
            }
          ],
          "issues": [],
          "summary": {
            "routeCount": 1,
            "indexableCount": 1,
            "nonIndexableCount": 0,
            "errorCount": 0,
            "warningCount": 0
          }
        }
        """;

    private static string ValidGeoReportJson() => """
        {
          "schema": "https://bukit.dev/schemas/geo-report.v1.json",
          "schemaVersion": "1.0",
          "generatedAt": "2026-01-01T00:00:00Z",
          "geoScore": 80,
          "llmsTxtGenerated": true,
          "llmsFullTxtGenerated": false,
          "geoEnhancedCount": 1,
          "geoEnhancedRoutes": [
            {
              "url": "/post/",
              "schemaTypes": ["Article"]
            }
          ]
        }
        """;
}
