using System.Text.Json;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Xunit;

namespace Bukit.Plugin.Notion.Tests;

public sealed class NotionPushDryRunInvokeTests : IDisposable
{
    private const string SecretToken = "secret-token-should-not-appear";
    private readonly string _projectRoot;

    public NotionPushDryRunInvokeTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-plugin-dry-run-" + Guid.NewGuid().ToString("N"));
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
    public void App_PushDryRunCreate_PlansRecordsAndWritesReport()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        string reportPath = Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "notion", "notion-push-report.json");
        PluginInvokeRequest request = CreatePushRequest(seedDir, mapPath, mode: "create");

        string json = NotionPluginApp.Handle(Serialize(request));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(0, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("notion-push-report", root.GetProperty("artifacts")[0].GetProperty("type").GetString());
        Assert.Equal("notion-push-report-md", root.GetProperty("artifacts")[1].GetProperty("type").GetString());
        Assert.True(File.Exists(reportPath));
        Assert.True(File.Exists(Path.ChangeExtension(reportPath, ".md")));

        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(reportPath));
        JsonElement reportRoot = report.RootElement;
        Assert.True(reportRoot.GetProperty("dryRun").GetBoolean());
        Assert.Equal(2, reportRoot.GetProperty("plannedCreate").GetInt32());
        Assert.Equal(0, reportRoot.GetProperty("plannedUpdate").GetInt32());
        Assert.Equal(0, reportRoot.GetProperty("plannedReplace").GetInt32());
        Assert.Equal(2, reportRoot.GetProperty("records").GetArrayLength());
    }

    [Fact]
    public void App_PushDryRun_DoesNotRequireTokenAndDoesNotWriteSecret()
    {
        using var token = new EnvironmentVariableScope("NOTION_TOKEN", SecretToken);
        (string seedDir, string mapPath) = WriteValidHandoff();
        PluginInvokeRequest request = CreatePushRequest(seedDir, mapPath, mode: "create");

        string json = NotionPluginApp.Handle(Serialize(request));

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("success").GetBoolean());

        string reportPath = Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "notion", "notion-push-report.json");
        string report = File.ReadAllText(reportPath);
        Assert.DoesNotContain(SecretToken, report, StringComparison.Ordinal);
    }

    [Fact]
    public void App_PushDryRun_InvalidSeedFails()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "notion-seed")).FullName;
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  {
    "slug": "missing-title"
  }
]
""");
        string mapPath = WriteValidMap(seedDir);
        PluginInvokeRequest request = CreatePushRequest(seedDir, mapPath, mode: "create");

        string json = NotionPluginApp.Handle(Serialize(request));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("notion.seedMissingTitle", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void App_PushDryRun_InvalidMapFails()
    {
        string seedDir = WriteValidSeed();
        string mapPath = Path.Combine(seedDir, "notion-database-map.yaml");
        File.WriteAllText(mapPath, """
databases:
  pages:
    seed: pages.json
    collection: page
    uniqueField: Slug
""");
        PluginInvokeRequest request = CreatePushRequest(seedDir, mapPath, mode: "create");

        string json = NotionPluginApp.Handle(Serialize(request));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("notion.databaseMapMissingDataSource", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void App_PushDryRun_ReplacePlansReplaceRecords()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        PluginInvokeRequest request = CreatePushRequest(seedDir, mapPath, mode: "replace");

        string json = NotionPluginApp.Handle(Serialize(request));

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("success").GetBoolean());

        string reportPath = Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "notion", "notion-push-report.json");
        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(reportPath));
        Assert.Equal(0, report.RootElement.GetProperty("plannedCreate").GetInt32());
        Assert.Equal(0, report.RootElement.GetProperty("plannedUpdate").GetInt32());
        Assert.Equal(2, report.RootElement.GetProperty("plannedReplace").GetInt32());
    }

    [Fact]
    public void App_PushDryRun_UpsertPlansUpdateRecords()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        PluginInvokeRequest request = CreatePushRequest(seedDir, mapPath, mode: "upsert");

        string json = NotionPluginApp.Handle(Serialize(request));

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("success").GetBoolean());

        string reportPath = Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "notion", "notion-push-report.json");
        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(reportPath));
        Assert.Equal(0, report.RootElement.GetProperty("plannedCreate").GetInt32());
        Assert.Equal(2, report.RootElement.GetProperty("plannedUpdate").GetInt32());
        Assert.Equal(0, report.RootElement.GetProperty("plannedReplace").GetInt32());
    }

    [Fact]
    public void App_PushDryRun_ReportAbsolutePathOutsideAllowedOutputFails()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        string outsideReportPath = Path.Combine(Path.GetTempPath(), "bukit-notion-report-" + Guid.NewGuid().ToString("N") + ".json");
        PluginInvokeRequest request = CreatePushRequest(
            seedDir,
            mapPath,
            mode: "create",
            options => options["--report"] = JsonSerializer.SerializeToElement(outsideReportPath));

        string json = NotionPluginApp.Handle(Serialize(request));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(2, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("notion.reportPathOutsideAllowedOutput", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.False(File.Exists(outsideReportPath));
    }

    [Fact]
    public void App_PushDryRun_ReportRelativeTraversalOutsideAllowedOutputFails()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        string escapedReportPath = Path.Combine("..", "notion-report.json");
        PluginInvokeRequest request = CreatePushRequest(
            seedDir,
            mapPath,
            mode: "create",
            options => options["--report"] = JsonSerializer.SerializeToElement(escapedReportPath));

        string json = NotionPluginApp.Handle(Serialize(request));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(2, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("notion.reportPathOutsideAllowedOutput", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void App_PushDryRun_ReportParentSymlinkOutsideAllowedOutputFails()
    {
        string outside = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bukit-notion-report-outside-" + Guid.NewGuid().ToString("N"))).FullName;
        string allowedRoot = Directory.CreateDirectory(Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "notion")).FullName;
        string linkedParent = Path.Combine(allowedRoot, "linked-parent");

        try
        {
            Directory.CreateSymbolicLink(linkedParent, outside);
            (string seedDir, string mapPath) = WriteValidHandoff();
            PluginInvokeRequest request = CreatePushRequest(
                seedDir,
                mapPath,
                mode: "create",
                options => options["--report"] = JsonSerializer.SerializeToElement(Path.Combine(linkedParent, "notion-push-report.json")));

            string json = NotionPluginApp.Handle(Serialize(request));

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.Equal("notion.reportPathOutsideAllowedOutput", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
            Assert.False(File.Exists(Path.Combine(outside, "notion-push-report.json")));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return;
        }
        finally
        {
            if (Directory.Exists(linkedParent))
            {
                Directory.Delete(linkedParent);
            }

            if (Directory.Exists(outside))
            {
                Directory.Delete(outside, recursive: true);
            }
        }
    }

    private (string SeedDir, string MapPath) WriteValidHandoff()
    {
        string seedDir = WriteValidSeed();
        return (seedDir, WriteValidMap(seedDir));
    }

    private string WriteValidSeed()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "notion-seed")).FullName;
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  {
    "title": "Home",
    "slug": "home",
    "published": true
  },
  {
    "title": "About",
    "slug": "about",
    "published": false
  }
]
""");
        return seedDir;
    }

    private static string WriteValidMap(string seedDir)
    {
        string mapPath = Path.Combine(seedDir, "notion-database-map.yaml");
        File.WriteAllText(mapPath, """
databases:
  pages:
    seed: pages.json
    collection: page
    dataSourceId: ds-pages
    uniqueField: Slug
    properties:
      Title:
        source: title
        type: title
      Slug:
        source: slug
        type: rich_text
      Published:
        source: published
        type: checkbox
""");
        return mapPath;
    }

    private PluginInvokeRequest CreatePushRequest(
        string seedDir,
        string mapPath,
        string mode,
        Action<Dictionary<string, JsonElement>>? configureOptions = null)
    {
        var options = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["--seed"] = JsonSerializer.SerializeToElement(seedDir),
            ["--database-map"] = JsonSerializer.SerializeToElement(mapPath),
            ["--mode"] = JsonSerializer.SerializeToElement(mode),
            ["--dry-run"] = JsonSerializer.SerializeToElement(true)
        };
        configureOptions?.Invoke(options);

        return CreatePushRequest(seedDir, mapPath, mode, options);
    }

    private PluginInvokeRequest CreatePushRequest(
        string seedDir,
        string mapPath,
        string mode,
        Dictionary<string, JsonElement> options)
        => new(
            Type: PluginProtocolConstants.Invoke,
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-push",
            Host: new PluginHostInfo("Bukit", "1.0.0", "test-rid"),
            Command: new PluginInvokeCommand(
                Name: "notion",
                Path: ["notion", "push"],
                Arguments: [],
                Options: options),
            Context: new PluginInvokeContext(_projectRoot, _projectRoot),
            Permissions: new PluginPermissionSet());

    private static string Serialize(PluginInvokeRequest request)
        => JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginInvokeRequest);

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
            => Environment.SetEnvironmentVariable(_name, _originalValue);
    }
}
