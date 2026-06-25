using System.Text.Json;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Xunit;

namespace Bukit.Plugin.Import.Tests;

public sealed class ImportHtmlDemoDryRunTests : IDisposable
{
    private readonly string _projectRoot;

    public ImportHtmlDemoDryRunTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-import-plugin-html-dry-run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectRoot))
        {
            Directory.Delete(_projectRoot, recursive: true);
        }
    }

    [Fact]
    public void App_InvokeHtmlDemoDryRun_ReturnsScanArtifactAndDoesNotWriteThemeOrContent()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html><head><title>Home</title></head><body><main>Home</main></body></html>
        """);
        PluginInvokeRequest request = CreateRequest(
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = JsonString("dry-theme"),
                ["--dry-run"] = JsonBool(true)
            });

        string json = ImportPluginApp.Handle(JsonSerializer.Serialize(
            request,
            PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(0, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("scan-report", root.GetProperty("artifacts")[0].GetProperty("type").GetString());
        Assert.Equal("reports/import/html-demo-dry-run.json", root.GetProperty("artifacts")[0].GetProperty("path").GetString());
        Assert.False(Directory.Exists(Path.Combine(_projectRoot, "themes", "dry-theme")));
        Assert.False(Directory.Exists(Path.Combine(_projectRoot, "content")));
    }

    [Theory]
    [InlineData("missing-demo-dir")]
    [InlineData("missing-theme")]
    [InlineData("wrong-command-path")]
    public void App_InvokeHtmlDemoDryRunInvalidRequest_ReturnsDiagnostics(string caseName)
    {
        PluginInvokeRequest request = caseName switch
        {
            "missing-demo-dir" => CreateRequest(
                arguments: [],
                options: new Dictionary<string, JsonElement>
                {
                    ["--theme"] = JsonString("dry-theme"),
                    ["--dry-run"] = JsonBool(true)
                }),
            "missing-theme" => CreateRequest(
                arguments: ["demo"],
                options: new Dictionary<string, JsonElement> { ["--dry-run"] = JsonBool(true) }),
            "wrong-command-path" => CreateRequest(
                path: ["import", "seed"],
                arguments: ["demo"],
                options: new Dictionary<string, JsonElement>
                {
                    ["--theme"] = JsonString("dry-theme"),
                    ["--dry-run"] = JsonBool(true)
                }),
            _ => throw new InvalidOperationException(caseName)
        };

        string json = ImportPluginApp.Handle(JsonSerializer.Serialize(
            request,
            PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(2, root.GetProperty("exitCode").GetInt32());
        Assert.True(root.GetProperty("diagnostics").GetArrayLength() > 0);
    }

    [Fact]
    public void App_InvokeHtmlDemoDryRunMissingDirectory_ReturnsDomainDiagnostic()
    {
        PluginInvokeRequest request = CreateRequest(
            arguments: ["missing-demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = JsonString("dry-theme"),
                ["--dry-run"] = JsonBool(true)
            });

        string json = ImportPluginApp.Handle(JsonSerializer.Serialize(
            request,
            PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("import.htmlDemoDirNotFound", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void App_InvokeHtmlDemoLocalImport_WritesThemeAndSiteAndKeepsStdoutJsonOnly()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html>
          <head><title>Local Import</title></head>
          <body><main><h1>Local Import</h1><p>Plugin local import body.</p></main></body>
        </html>
        """);
        PluginInvokeRequest request = CreateRequest(
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = JsonString("local-theme"),
                ["--force"] = JsonBool(true)
            });
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;

        string json;
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            json = ImportPluginApp.Handle(JsonSerializer.Serialize(
                request,
                PluginJsonSerializerContext.Default.PluginInvokeRequest));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(0, root.GetProperty("exitCode").GetInt32());
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("迁移完成", stderr.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_projectRoot, "themes", "local-theme", "theme.yaml")));
        Assert.True(File.Exists(Path.Combine(_projectRoot, "sites", "local-theme", "site.yaml")));
        Assert.True(File.Exists(Path.Combine(_projectRoot, "sites", "local-theme", "content", "index.md")));
        Assert.Contains(root.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("type").GetString() == "theme"
            && artifact.GetProperty("path").GetString() == "themes/local-theme");
        Assert.Contains(root.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("type").GetString() == "site"
            && artifact.GetProperty("path").GetString() == "sites/local-theme");
    }

    [Fact]
    public void App_InvokeHtmlDemoLocalImportWithSitePathAndLanguage_WritesRequestedSiteConfigAndArtifacts()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html>
          <head><title>Site Options</title></head>
          <body><main><h1>Site Options</h1><p>Configured site path and language.</p></main></body>
        </html>
        """);
        PluginInvokeRequest request = CreateRequest(
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = JsonString("site-theme"),
                ["--force"] = JsonBool(true),
                ["--site-path"] = JsonString("./sites/custom-site"),
                ["--language"] = JsonString("en")
            });

        string json = ImportPluginApp.Handle(JsonSerializer.Serialize(
            request,
            PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string siteYamlPath = Path.Combine(_projectRoot, "sites", "custom-site", "site.yaml");
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(File.Exists(Path.Combine(_projectRoot, "themes", "site-theme", "theme.yaml")));
        Assert.True(File.Exists(siteYamlPath));
        string siteYaml = File.ReadAllText(siteYamlPath);
        Assert.Contains("name: site-theme", siteYaml, StringComparison.Ordinal);
        Assert.Contains("language: en", siteYaml, StringComparison.Ordinal);
        Assert.Contains(root.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("type").GetString() == "theme"
            && artifact.GetProperty("path").GetString() == "themes/site-theme");
        Assert.Contains(root.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("type").GetString() == "site"
            && artifact.GetProperty("path").GetString() == "sites/custom-site");
    }

    [Fact]
    public void App_InvokeHtmlDemoLocalImportWithSitePathOutsideSites_ReturnsDiagnostic()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html>
          <head><title>Invalid Site Path</title></head>
          <body><main><h1>Invalid Site Path</h1></main></body>
        </html>
        """);
        PluginInvokeRequest request = CreateRequest(
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = JsonString("invalid-site-theme"),
                ["--force"] = JsonBool(true),
                ["--site-path"] = JsonString("./custom-site")
            });

        string json = ImportPluginApp.Handle(JsonSerializer.Serialize(
            request,
            PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(2, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("import.sitePathInvalid", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.False(Directory.Exists(Path.Combine(_projectRoot, "custom-site")));
    }

    [Fact]
    public void App_InvokeHtmlDemoLocalImportWithNoExtractContent_DoesNotWriteContentOrReturnContentArtifact()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html>
          <head><title>No Content</title></head>
          <body><main><h1>No Content</h1><p>This body should not be extracted.</p></main></body>
        </html>
        """);
        PluginInvokeRequest request = CreateRequest(
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = JsonString("no-content-theme"),
                ["--force"] = JsonBool(true),
                ["--no-extract-content"] = JsonBool(true)
            });

        string json = ImportPluginApp.Handle(JsonSerializer.Serialize(
            request,
            PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(File.Exists(Path.Combine(_projectRoot, "themes", "no-content-theme", "theme.yaml")));
        Assert.True(File.Exists(Path.Combine(_projectRoot, "sites", "no-content-theme", "site.yaml")));
        Assert.False(Directory.Exists(Path.Combine(_projectRoot, "sites", "no-content-theme", "content")));
        Assert.DoesNotContain(root.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("type").GetString() == "content");
    }

    [Fact]
    public void App_InvokeHtmlDemoLocalImport_WritesImportReportsAndReturnsReportArtifacts()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html>
          <head><title>Report Import</title></head>
          <body><main><h1>Report Import</h1><p>Report body.</p></main></body>
        </html>
        """);
        PluginInvokeRequest request = CreateRequest(
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = JsonString("report-theme"),
                ["--force"] = JsonBool(true)
            });

        string json = ImportPluginApp.Handle(JsonSerializer.Serialize(
            request,
            PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(File.Exists(Path.Combine(_projectRoot, "sites", "report-theme", "import-report.md")));
        string jsonReportPath = Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "import", "html-demo-report.json");
        Assert.True(File.Exists(jsonReportPath));
        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(jsonReportPath));
        Assert.Equal("bukit.import.html-demo.report.v1", report.RootElement.GetProperty("schema").GetString());
        Assert.Equal("report-theme", report.RootElement.GetProperty("theme").GetString());
        Assert.Contains(root.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("type").GetString() == "report"
            && artifact.GetProperty("path").GetString() == "sites/report-theme/import-report.md");
        Assert.Contains(root.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("type").GetString() == "report-json"
            && artifact.GetProperty("path").GetString() == ".bukit/reports/plugin-output/import/html-demo-report.json");
    }

    [Fact]
    public void App_InvokeHtmlDemoLocalImportWithNoReport_DoesNotWriteReportArtifacts()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html>
          <head><title>No Report</title></head>
          <body><main><h1>No Report</h1><p>No report body.</p></main></body>
        </html>
        """);
        PluginInvokeRequest request = CreateRequest(
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = JsonString("no-report-theme"),
                ["--force"] = JsonBool(true),
                ["--no-report"] = JsonBool(true)
            });

        string json = ImportPluginApp.Handle(JsonSerializer.Serialize(
            request,
            PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.False(File.Exists(Path.Combine(_projectRoot, "sites", "no-report-theme", "import-report.md")));
        Assert.False(File.Exists(Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "import", "html-demo-report.json")));
        Assert.DoesNotContain(root.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("type").GetString() == "report"
            || artifact.GetProperty("type").GetString() == "report-json");
    }

    [Fact]
    public void App_InvokeHtmlDemoLocalImport_ReturnsSecurityDiagnostics()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html>
          <head><title>Security Scan</title><script>console.log('inline')</script></head>
          <body>
            <main>
              <h1>Security Scan</h1>
              <a href="https://example.com/offsite">External</a>
              <form action="/lead"><input name="email"></form>
              <script>const api_key = "demo-secret-token-1234567890";</script>
            </main>
          </body>
        </html>
        """);
        PluginInvokeRequest request = CreateRequest(
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = JsonString("security-theme"),
                ["--force"] = JsonBool(true)
            });

        string json = ImportPluginApp.Handle(JsonSerializer.Serialize(
            request,
            PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        string[] codes = root.GetProperty("diagnostics")
            .EnumerateArray()
            .Select(diagnostic => diagnostic.GetProperty("code").GetString()!)
            .ToArray();
        Assert.Contains("INLINE_SCRIPT", codes);
        Assert.Contains("EXTERNAL_URL", codes);
        Assert.Contains("UNSUPPORTED_FORM", codes);
        Assert.Contains("HARDCODED_SECRET", codes);
    }

    [Fact]
    public void App_InvokeHtmlDemoLocalImportWithUse_UpdatesExistingSiteTheme()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html>
          <head><title>Use Theme</title></head>
          <body><main><h1>Use Theme</h1><p>Use body.</p></main></body>
        </html>
        """);
        string siteDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "sites", "current")).FullName;
        File.WriteAllText(Path.Combine(siteDir, "site.yaml"), """
        site:
          name: current
          title: Current
        theme:
          name: old-theme
        """);
        PluginInvokeRequest request = CreateRequest(
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = JsonString("new-theme"),
                ["--site-path"] = JsonString("./sites/current"),
                ["--force"] = JsonBool(true),
                ["--use"] = JsonBool(true)
            });

        string json = ImportPluginApp.Handle(JsonSerializer.Serialize(
            request,
            PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string siteYaml = File.ReadAllText(Path.Combine(siteDir, "site.yaml"));
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Contains("name: new-theme", siteYaml, StringComparison.Ordinal);
        Assert.DoesNotContain("old-theme", siteYaml, StringComparison.Ordinal);
        Assert.Contains(root.GetProperty("diagnostics").EnumerateArray(), diagnostic =>
            diagnostic.GetProperty("code").GetString() == "import.useApplied");
        Assert.Contains(root.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("type").GetString() == "site-config"
            && artifact.GetProperty("path").GetString() == "sites/current/site.yaml");
    }

    [Fact]
    public void App_InvokeHtmlDemoLocalImportWithVerify_ReturnsLightVerifyDiagnosticAndArtifact()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html>
          <head><title>Verify Theme</title></head>
          <body><main><h1>Verify Theme</h1><p>Verify body.</p></main></body>
        </html>
        """);
        PluginInvokeRequest request = CreateRequest(
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = JsonString("verify-theme"),
                ["--force"] = JsonBool(true),
                ["--verify"] = JsonBool(true)
            });

        string json = ImportPluginApp.Handle(JsonSerializer.Serialize(
            request,
            PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Contains(root.GetProperty("diagnostics").EnumerateArray(), diagnostic =>
            diagnostic.GetProperty("code").GetString() == "import.lightVerifyPassed");
        Assert.Contains(root.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("type").GetString() == "verification"
            && artifact.GetProperty("path").GetString() == "sites/verify-theme/site.yaml");
    }

    [Fact]
    public void App_InvokeHtmlDemoLocalImportWithNotionContentSource_WritesHandoffArtifacts()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html>
          <head><title>Notion Handoff</title></head>
          <body><main><h1>Notion Handoff</h1><p>Notion seed body.</p></main></body>
        </html>
        """);
        PluginInvokeRequest request = CreateRequest(
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = JsonString("notion-handoff-theme"),
                ["--force"] = JsonBool(true),
                ["--content-source"] = JsonString("notion"),
                ["--build-source"] = JsonString("markdown")
            });

        string json = ImportPluginApp.Handle(JsonSerializer.Serialize(
            request,
            PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string seedDir = Path.Combine(_projectRoot, "sites", "notion-handoff-theme", "notion-seed");
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(File.Exists(Path.Combine(seedDir, "pages.json")));
        Assert.True(File.Exists(Path.Combine(seedDir, "notion-database-map.yaml")));
        string siteYaml = File.ReadAllText(Path.Combine(_projectRoot, "sites", "notion-handoff-theme", "site.yaml"));
        Assert.Contains("type: markdown", siteYaml, StringComparison.Ordinal);
        Assert.DoesNotContain("type: notion", siteYaml, StringComparison.Ordinal);
        Assert.Contains(root.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("type").GetString() == "notion-seed"
            && artifact.GetProperty("path").GetString() == "sites/notion-handoff-theme/notion-seed");
        Assert.Contains(root.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("type").GetString() == "notion-database-map"
            && artifact.GetProperty("path").GetString() == "sites/notion-handoff-theme/notion-seed/notion-database-map.yaml");
        Assert.Contains(root.GetProperty("diagnostics").EnumerateArray(), diagnostic =>
            diagnostic.GetProperty("code").GetString() == "import.notionHandoffReady");
    }

    [Fact]
    public void App_InvokeHtmlDemoLocalImportWithNotionContentSourceAndNoSeed_DoesNotWriteHandoffArtifacts()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html>
          <head><title>No Seed</title></head>
          <body><main><h1>No Seed</h1><p>No seed body.</p></main></body>
        </html>
        """);
        PluginInvokeRequest request = CreateRequest(
            arguments: ["demo"],
            options: new Dictionary<string, JsonElement>
            {
                ["--theme"] = JsonString("notion-no-seed-theme"),
                ["--force"] = JsonBool(true),
                ["--content-source"] = JsonString("notion"),
                ["--no-seed"] = JsonBool(true)
            });

        string json = ImportPluginApp.Handle(JsonSerializer.Serialize(
            request,
            PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.False(Directory.Exists(Path.Combine(_projectRoot, "sites", "notion-no-seed-theme", "notion-seed")));
        Assert.DoesNotContain(root.GetProperty("artifacts").EnumerateArray(), artifact =>
            artifact.GetProperty("type").GetString() == "notion-seed"
            || artifact.GetProperty("type").GetString() == "notion-database-map");
    }

    private PluginInvokeRequest CreateRequest(
        IReadOnlyList<string>? path = null,
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, JsonElement>? options = null)
        => new(
            Type: PluginProtocolConstants.Invoke,
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-html",
            Host: new PluginHostInfo("Bukit", "1.0.0", "test-rid"),
            Command: new PluginInvokeCommand(
                Name: "html-demo",
                Path: path ?? ["import", "html-demo"],
                Arguments: arguments?.Select(ResolveArgument).ToArray() ?? [],
                Options: options ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal)),
            Context: new PluginInvokeContext(_projectRoot, _projectRoot),
            Permissions: new PluginPermissionSet());

    private string ResolveArgument(string argument)
        => argument == "demo" ? Path.Combine(_projectRoot, "demo") : argument;

    private static JsonElement JsonString(string value)
        => JsonSerializer.SerializeToElement(value);

    private static JsonElement JsonBool(bool value)
        => JsonSerializer.SerializeToElement(value);
}
