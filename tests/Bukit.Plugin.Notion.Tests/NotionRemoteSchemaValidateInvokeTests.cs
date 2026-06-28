using System.Text.Json;
using Bukit.Notion.RemoteSchema;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Bukit.Plugin.Notion;
using Xunit;

namespace Bukit.Plugin.Notion.Tests;

public sealed class NotionRemoteSchemaValidateInvokeTests : IDisposable
{
    private readonly string _projectRoot;

    public NotionRemoteSchemaValidateInvokeTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-schema-invoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("NOTION_TOKEN", null);
        if (Directory.Exists(_projectRoot))
        {
            Directory.Delete(_projectRoot, recursive: true);
        }
    }

    [Fact]
    public void Mapper_MissingDatabaseMap_ReturnsStableDiagnostic()
    {
        NotionRemoteSchemaValidateMapperResult result = NotionOptionsMapper.MapRemoteSchemaValidateOptions(
            CreateRequest(new Dictionary<string, JsonElement>(StringComparer.Ordinal)));

        Assert.False(result.Success);
        Assert.Equal("notion.remoteSchemaMissingDatabaseMap", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Mapper_DisallowedTokenEnvironment_ReturnsStableDiagnostic()
    {
        NotionRemoteSchemaValidateMapperResult result = NotionOptionsMapper.MapRemoteSchemaValidateOptions(
            CreateRequest(Options(
                ("--database-map", "./map.yaml"),
                ("--token-env", "OTHER_TOKEN"))));

        Assert.False(result.Success);
        Assert.Equal("notion.tokenEnvNotAllowed", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Mapper_ReportOutsideAllowedRoots_ReturnsStableDiagnostic()
    {
        NotionRemoteSchemaValidateMapperResult result = NotionOptionsMapper.MapRemoteSchemaValidateOptions(
            CreateRequest(Options(
                ("--database-map", "./map.yaml"),
                ("--report", "./outside.json"))));

        Assert.False(result.Success);
        Assert.Equal("notion.reportPathOutsideAllowedOutput", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Mapper_ValidOptions_UsesSafeDefaultReportAndToken()
    {
        NotionRemoteSchemaValidateMapperResult result = NotionOptionsMapper.MapRemoteSchemaValidateOptions(
            CreateRequest(Options(("--database-map", "./map.yaml"))));

        Assert.True(result.Success);
        Assert.NotNull(result.Options);
        Assert.Equal("NOTION_TOKEN", result.Options!.TokenEnvironmentVariable);
        Assert.Equal(Path.Combine(_projectRoot, "map.yaml"), result.Options.DatabaseMapPath);
        Assert.Equal(
            Path.Combine(
                _projectRoot,
                ".bukit",
                "reports",
                "plugin-output",
                "notion",
                "notion-schema-validation-report.json"),
            result.Options.ReportPath);
    }

    [Fact]
    public void Handler_TranslatesDomainResultToPluginResponse()
    {
        string reportPath = Path.Combine(
            _projectRoot,
            ".bukit",
            "reports",
            "plugin-output",
            "notion",
            "notion-schema-validation-report.json");
        var service = new FakeRemoteSchemaValidationService(new NotionRemoteSchemaValidationResult(
            false,
            2,
            Diagnostics:
            [
                new NotionRemoteSchemaDiagnostic(
                    "notion.remoteSchemaPropertyMissing",
                    "error",
                    "Slug is missing.",
                    "pages.properties.Slug")
            ],
            Artifacts:
            [
                new NotionRemoteSchemaArtifact(
                    "notion-schema-validation-report",
                    reportPath,
                    "Schema report.")
            ]));

        PluginInvokeResponse response = NotionRemoteSchemaValidateCommandHandler.Handle(
            "req-schema",
            CreateRequest(Options(("--database-map", "./map.yaml"))),
            service);

        Assert.False(response.Success);
        Assert.Equal(2, response.ExitCode);
        Assert.Equal("notion.remoteSchemaPropertyMissing", Assert.Single(response.Diagnostics).Code);
        Assert.Equal("notion-schema-validation-report", Assert.Single(response.Artifacts).Type);
        Assert.Equal(".bukit/reports/plugin-output/notion/notion-schema-validation-report.json", response.Artifacts[0].Path);
        Assert.NotNull(service.LastOptions);
    }

    [Fact]
    public void App_InvokeSchemaValidate_DispatchesToRemoteSchemaHandler()
    {
        Environment.SetEnvironmentVariable("NOTION_TOKEN", null);
        string mapPath = Path.Combine(_projectRoot, "map.yaml");
        File.WriteAllText(mapPath, """
        databases:
          pages:
            seed: pages.json
            collection: page
            dataSourceId: ds-pages
            uniqueField: Slug
            properties:
              Title: { source: title, type: title }
              Slug: { source: slug, type: rich_text }
        """);
        PluginInvokeRequest request = CreateRequest(Options(("--database-map", "./map.yaml")));
        string input = JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginInvokeRequest);

        string json = NotionPluginApp.Handle(input);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(2, root.GetProperty("exitCode").GetInt32());
        Assert.Contains(
            root.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "notion.tokenMissing");
        Assert.Equal("notion-schema-validation-report", root.GetProperty("artifacts")[0].GetProperty("type").GetString());
        Assert.True(File.Exists(Path.Combine(
            _projectRoot,
            ".bukit",
            "reports",
            "plugin-output",
            "notion",
            "notion-schema-validation-report.json")));
    }

    private PluginInvokeRequest CreateRequest(IReadOnlyDictionary<string, JsonElement> options)
        => new(
            Type: PluginProtocolConstants.Invoke,
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-schema",
            Host: new PluginHostInfo("Bukit", "1.0.0", "test-rid"),
            Command: new PluginInvokeCommand(
                Name: "notion",
                Path: ["notion", "schema", "validate"],
                Arguments: [],
                Options: options),
            Context: new PluginInvokeContext(_projectRoot, _projectRoot),
            Permissions: new PluginPermissionSet());

    private static IReadOnlyDictionary<string, JsonElement> Options(
        params (string Name, string Value)[] values)
        => values.ToDictionary(
            value => value.Name,
            value => JsonSerializer.SerializeToElement(value.Value),
            StringComparer.Ordinal);

    private sealed class FakeRemoteSchemaValidationService : INotionRemoteSchemaValidationService
    {
        private readonly NotionRemoteSchemaValidationResult _result;

        public FakeRemoteSchemaValidationService(NotionRemoteSchemaValidationResult result)
        {
            _result = result;
        }

        public NotionRemoteSchemaOptions? LastOptions { get; private set; }

        public NotionRemoteSchemaValidationResult Validate(NotionRemoteSchemaOptions options)
        {
            LastOptions = options;
            return _result;
        }
    }
}
