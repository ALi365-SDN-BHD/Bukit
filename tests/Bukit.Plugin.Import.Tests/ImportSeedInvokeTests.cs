using System.Text.Json;
using Bukit.Importing.Seed;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Xunit;

namespace Bukit.Plugin.Import.Tests;

public sealed class ImportSeedInvokeTests : IDisposable
{
    private readonly string _projectRoot;

    public ImportSeedInvokeTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-import-plugin-seed-" + Guid.NewGuid().ToString("N"));
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
    public void Mapper_ValidSeedCommand_MapsOptions()
    {
        PluginInvokeRequest request = CreateRequest(
            arguments: ["seed"],
            options: new Dictionary<string, JsonElement>
            {
                ["--output"] = JsonString("content"),
                ["--force"] = JsonBool(true)
            });

        ImportOptionsMapperResult result = ImportOptionsMapper.MapSeedOptions(request);

        Assert.True(result.Success);
        Assert.NotNull(result.Options);
        Assert.Equal(Path.Combine(_projectRoot, "seed"), result.Options!.SeedDirectory);
        Assert.Equal(Path.Combine(_projectRoot, "content"), result.Options.OutputDirectory);
        Assert.True(result.Options.Force);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Mapper_WhenForceAbsent_DefaultsFalse()
    {
        PluginInvokeRequest request = CreateRequest(
            arguments: ["seed"],
            options: new Dictionary<string, JsonElement>
            {
                ["--output"] = JsonString("content")
            });

        ImportOptionsMapperResult result = ImportOptionsMapper.MapSeedOptions(request);

        Assert.True(result.Success);
        Assert.False(result.Options!.Force);
    }

    [Theory]
    [InlineData("missing-seed-dir")]
    [InlineData("missing-output")]
    [InlineData("wrong-force-type")]
    [InlineData("wrong-command-path")]
    public void Mapper_InvalidRequest_ReturnsDiagnostics(string caseName)
    {
        PluginInvokeRequest request = caseName switch
        {
            "missing-seed-dir" => CreateRequest(
                arguments: [],
                options: new Dictionary<string, JsonElement> { ["--output"] = JsonString("content") }),
            "missing-output" => CreateRequest(arguments: ["seed"], options: EmptyOptions()),
            "wrong-force-type" => CreateRequest(
                arguments: ["seed"],
                options: new Dictionary<string, JsonElement>
                {
                    ["--output"] = JsonString("content"),
                    ["--force"] = JsonString("true")
                }),
            "wrong-command-path" => CreateRequest(
                path: ["import", "html-demo"],
                arguments: ["seed"],
                options: new Dictionary<string, JsonElement> { ["--output"] = JsonString("content") }),
            _ => throw new InvalidOperationException(caseName)
        };

        ImportOptionsMapperResult result = ImportOptionsMapper.MapSeedOptions(request);

        Assert.False(result.Success);
        Assert.Null(result.Options);
        Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("error", diagnostic.Severity));
    }

    [Fact]
    public void Handler_WhenDomainSucceeds_MapsArtifactsAndDiagnostics()
    {
        PluginInvokeRequest request = CreateRequest(
            arguments: ["seed"],
            options: new Dictionary<string, JsonElement> { ["--output"] = JsonString("content") });
        var service = new StubImportSeedService(new ImportSeedResult(
            Success: true,
            ExitCode: 0,
            Diagnostics:
            [
                new ImportSeedDiagnostic("import.note", "info", "note", "seed/pages.json")
            ],
            Artifacts:
            [
                new ImportSeedArtifact("markdown", "content/pages/home.md", "Imported page.")
            ]));

        PluginInvokeResponse response = ImportSeedCommandHandler.Handle("req-seed", request, service);

        Assert.True(response.Success);
        Assert.Equal(0, response.ExitCode);
        Assert.Equal("import.note", Assert.Single(response.Diagnostics).Code);
        PluginArtifact artifact = Assert.Single(response.Artifacts);
        Assert.Equal("markdown", artifact.Type);
        Assert.Equal("content/pages/home.md", artifact.Path);
    }

    [Fact]
    public void Handler_WhenMapperFails_ReturnsExitCodeTwo()
    {
        PluginInvokeRequest request = CreateRequest(arguments: [], options: EmptyOptions());

        PluginInvokeResponse response = ImportSeedCommandHandler.Handle("req-bad", request, new StubImportSeedService());

        Assert.False(response.Success);
        Assert.Equal(2, response.ExitCode);
        Assert.NotEmpty(response.Diagnostics);
    }

    [Fact]
    public void Handler_WhenDomainFails_MapsBusinessFailure()
    {
        PluginInvokeRequest request = CreateRequest(
            arguments: ["seed"],
            options: new Dictionary<string, JsonElement> { ["--output"] = JsonString("content") });
        var service = new StubImportSeedService(ImportSeedResult.Failed(
            new ImportSeedDiagnostic("import.outputAlreadyExists", "error", "exists", "content")));

        PluginInvokeResponse response = ImportSeedCommandHandler.Handle("req-domain", request, service);

        Assert.False(response.Success);
        Assert.Equal(2, response.ExitCode);
        Assert.Equal("import.outputAlreadyExists", Assert.Single(response.Diagnostics).Code);
    }

    [Fact]
    public void Handler_WhenDomainThrows_ReturnsImportFailedDiagnostic()
    {
        PluginInvokeRequest request = CreateRequest(
            arguments: ["seed"],
            options: new Dictionary<string, JsonElement> { ["--output"] = JsonString("content") });

        PluginInvokeResponse response = ImportSeedCommandHandler.Handle(
            "req-throw",
            request,
            new ThrowingImportSeedService());

        Assert.False(response.Success);
        Assert.Equal(1, response.ExitCode);
        Assert.Equal("import.seedImportFailed", Assert.Single(response.Diagnostics).Code);
    }

    [Fact]
    public void App_InvokeImportSeed_ReturnsJsonResponseAndWritesContent()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "seed")).FullName;
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  {
    "title": "Home",
    "slug": "index",
    "content": "Welcome."
  }
]
""");
        PluginInvokeRequest request = CreateRequest(
            requestId: "req-app",
            arguments: ["seed"],
            options: new Dictionary<string, JsonElement>
            {
                ["--output"] = JsonString("content"),
                ["--force"] = JsonBool(true)
            });
        string input = JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginInvokeRequest);

        string json = ImportPluginApp.Handle(input);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal("invokeResponse", root.GetProperty("type").GetString());
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(0, root.GetProperty("exitCode").GetInt32());
        Assert.True(File.Exists(Path.Combine(_projectRoot, "content", "index.md")));
    }

    [Fact]
    public void App_InvokeWrongImportPath_ReturnsJsonDiagnostic()
    {
        PluginInvokeRequest request = CreateRequest(
            requestId: "req-wrong-path",
            path: ["import", "unknown"],
            arguments: ["seed"],
            options: new Dictionary<string, JsonElement> { ["--output"] = JsonString("content") });
        string input = JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginInvokeRequest);

        string json = ImportPluginApp.Handle(input);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal("invokeResponse", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(1, root.GetProperty("exitCode").GetInt32());
        Assert.Equal(
            "plugin.import.unsupportedCommand",
            root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.Equal(
            "Unsupported import command path. Supported commands: import seed, import html-demo.",
            root.GetProperty("diagnostics")[0].GetProperty("message").GetString());
    }

    private PluginInvokeRequest CreateRequest(
        string requestId = "req-1",
        IReadOnlyList<string>? path = null,
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, JsonElement>? options = null)
        => new(
            Type: PluginProtocolConstants.Invoke,
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Host: new PluginHostInfo("Bukit", "1.0.0", "test-rid"),
            Command: new PluginInvokeCommand(
                Name: "import",
                Path: path ?? ["import", "seed"],
                Arguments: arguments is null ? [Path.Combine(_projectRoot, "seed")] : arguments.Select(ResolveArgument).ToArray(),
                Options: options ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal)),
            Context: new PluginInvokeContext(_projectRoot, _projectRoot),
            Permissions: new PluginPermissionSet());

    private string ResolveArgument(string argument)
        => argument == "seed" ? Path.Combine(_projectRoot, "seed") : argument;

    private static JsonElement JsonString(string value)
        => JsonSerializer.SerializeToElement(value);

    private static JsonElement JsonBool(bool value)
        => JsonSerializer.SerializeToElement(value);

    private static IReadOnlyDictionary<string, JsonElement> EmptyOptions()
        => new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    private sealed class StubImportSeedService : IImportSeedService
    {
        private readonly ImportSeedResult _result;

        public StubImportSeedService()
            : this(ImportSeedResult.Succeeded([]))
        {
        }

        public StubImportSeedService(ImportSeedResult result)
        {
            _result = result;
        }

        public ImportSeedResult Import(ImportSeedOptions options) => _result;
    }

    private sealed class ThrowingImportSeedService : IImportSeedService
    {
        public ImportSeedResult Import(ImportSeedOptions options)
            => throw new InvalidOperationException("domain exploded");
    }
}
