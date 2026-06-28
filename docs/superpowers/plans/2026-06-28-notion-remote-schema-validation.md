# Notion Remote Schema Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `bukit notion schema validate` so every local database-map entry is checked against its remote Notion data-source schema and produces JSON/Markdown evidence before push.

**Architecture:** Extend the existing injectable Notion HTTP client with a read-only data-source retrieve call. A new `Bukit.Notion.RemoteSchema` domain service validates the local map, token, remote property names/types, title uniqueness, and `uniqueField`, then writes deterministic reports. The external process plugin only declares the nested command, maps safe options, invokes the service, and translates domain results to protocol diagnostics/artifacts.

**Tech Stack:** .NET 10, C# records, `HttpClient`, `System.Text.Json` source generation, YamlDotNet-backed existing map validation, xUnit, Bukit process-plugin protocol v1, Bash repository gates.

---

## Execution Boundary

The current checkout contains unrelated, uncommitted RC work in files that this
feature also needs to touch. Do not stage, reset, or rewrite those changes.
Execute the plan inline in an isolated worktree created from the current `HEAD`:

```text
branch: codex/notion-remote-schema-validation
worktree: .worktrees/notion-remote-schema-validation
```

The feature does not change package versions or release metadata. Each task
commit must contain only files changed inside the isolated worktree.

## Planned File Structure

### Client contract

- Modify `src/Bukit.Notion/Client/INotionClient.cs`: expose one retrieve method.
- Modify `src/Bukit.Notion/Client/NotionModels.cs`: add typed data-source schema result.
- Modify `src/Bukit.Notion/Client/NotionHttpClient.cs`: issue and parse the GET request.
- Modify `tests/Bukit.Notion.Tests/NotionHttpClientTests.cs`: prove URL, headers, parsing, and error behavior.

### Remote-schema domain

- Create `src/Bukit.Notion/RemoteSchema/NotionRemoteSchemaModels.cs`: options, diagnostics, comparison records, result, artifacts, and report DTO.
- Create `src/Bukit.Notion/RemoteSchema/INotionRemoteSchemaValidationService.cs`: handler-facing service contract.
- Create `src/Bukit.Notion/RemoteSchema/NotionRemoteSchemaValidationService.cs`: orchestration and comparison logic.
- Create `src/Bukit.Notion/RemoteSchema/NotionRemoteSchemaReportWriter.cs`: JSON and Markdown writers.
- Create `src/Bukit.Notion/RemoteSchema/NotionRemoteSchemaJsonSerializerContext.cs`: AOT-safe JSON metadata.
- Create `tests/Bukit.Notion.Tests/NotionRemoteSchemaValidationTests.cs`: domain behavior with fake clients/tokens.
- Create `tests/Bukit.Notion.Tests/NotionRemoteSchemaReportWriterTests.cs`: report shape and redaction.

### Process-plugin surface

- Modify `plugins/Bukit.Plugin.Notion/NotionCommandSpecFactory.cs`: declare `schema validate`.
- Modify `plugins/Bukit.Plugin.Notion/NotionPluginInvoker.cs`: dispatch the exact three-segment path.
- Modify `plugins/Bukit.Plugin.Notion/NotionOptionsMapper.cs`: map token, map path, and report path.
- Create `plugins/Bukit.Plugin.Notion/NotionRemoteSchemaValidateCommandHandler.cs`: translate service results.
- Modify `src/Bukit.Notion/NotionPluginConstants.cs`: centralize default report paths.
- Modify `tests/Bukit.Plugin.Notion.Tests/NotionPluginManifestTests.cs`: prove runtime/static parity.
- Create `tests/Bukit.Plugin.Notion.Tests/NotionRemoteSchemaValidateInvokeTests.cs`: prove option failures and handler translation.

### Static contract and documentation

- Modify `plugins/Bukit.Plugin.Notion/examples/minimal/plugins/notion/plugin.yaml`: static command contract.
- Modify `plugins/Bukit.Plugin.Notion/plugin.yaml.template`: package template contract.
- Modify `tests/Bukit.Cli.Tests/PluginCliIntegrationTests.cs`: prove nested path forwarding.
- Modify `plugins/Bukit.Plugin.Notion/README.md`: command and report usage.
- Modify `docs/plugins/Bukit.Plugin.Notion 开发技术书.md`: v1.1 architecture/error contract.

---

### Task 0: Create an isolated execution worktree

**Files:**
- No repository file changes.

- [ ] **Step 1: Confirm current Git topology and dirty-state boundary**

Run:

```bash
git rev-parse --show-toplevel
git rev-parse --git-dir
git rev-parse --git-common-dir
git branch --show-current
git status --short --branch
```

Expected: repository root is Bukit, current branch is `main`, and the existing
RC changes remain visible only in the current checkout.

- [ ] **Step 2: Create the feature branch worktree**

Run through the `superpowers:using-git-worktrees` workflow:

```bash
git worktree add .worktrees/notion-remote-schema-validation -b codex/notion-remote-schema-validation HEAD
```

Expected: worktree creation succeeds without modifying or staging the current
checkout's RC changes.

- [ ] **Step 3: Verify the isolated baseline**

Run from `.worktrees/notion-remote-schema-validation`:

```bash
git status --short --branch
git log -1 --oneline
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false
dotnet test tests/Bukit.Plugin.Notion.Tests/Bukit.Plugin.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false
```

Expected: clean feature branch and both baseline suites pass. If restore assets
are missing, run `dotnet restore bukit.slnx` once and rerun the same tests.

---

### Task 1: Retrieve typed remote data-source schemas

**Files:**
- Modify: `tests/Bukit.Notion.Tests/NotionHttpClientTests.cs`
- Modify: `src/Bukit.Notion/Client/INotionClient.cs`
- Modify: `src/Bukit.Notion/Client/NotionModels.cs`
- Modify: `src/Bukit.Notion/Client/NotionHttpClient.cs`

- [ ] **Step 1: Write the failing HTTP contract test**

Add this test to `NotionHttpClientTests`:

```csharp
[Fact]
public async Task RetrieveDataSourceAsync_GetsSchemaWithRequiredHeaders()
{
    using var handler = new RecordingHandler(_ => JsonResponse(
        HttpStatusCode.OK,
        """
        {
          "object": "data_source",
          "id": "ds-pages",
          "properties": {
            "Name": { "id": "title", "name": "Name", "type": "title", "title": {} },
            "Slug": { "id": "slug", "name": "Slug", "type": "rich_text", "rich_text": {} }
          }
        }
        """));
    using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.notion.com") };
    var client = new NotionHttpClient(httpClient, new NotionRequestOptions("secret-token", "2026-03-11"));

    NotionDataSourceResult result = await client.RetrieveDataSourceAsync("ds-pages", CancellationToken.None);

    HttpRequestMessage request = Assert.Single(handler.Requests);
    Assert.Equal(HttpMethod.Get, request.Method);
    Assert.Equal("/v1/data_sources/ds-pages", request.RequestUri!.PathAndQuery);
    Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
    Assert.Equal("2026-03-11", Assert.Single(request.Headers.GetValues("Notion-Version")));
    Assert.Equal("ds-pages", result.Id);
    Assert.Equal("title", result.Properties["Name"]);
    Assert.Equal("rich_text", result.Properties["Slug"]);
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false --filter FullyQualifiedName~NotionHttpClientTests.RetrieveDataSourceAsync_GetsSchemaWithRequiredHeaders
```

Expected: compilation fails because `NotionDataSourceResult` and
`RetrieveDataSourceAsync` do not exist.

- [ ] **Step 3: Add the client contract and model**

Add to `INotionClient`:

```csharp
Task<NotionDataSourceResult> RetrieveDataSourceAsync(
    string dataSourceId,
    CancellationToken cancellationToken);
```

Add to `NotionModels.cs`:

```csharp
public sealed record NotionDataSourceResult(
    string Id,
    IReadOnlyDictionary<string, string?> Properties);
```

- [ ] **Step 4: Implement the GET and narrow parser**

Add this method to `NotionHttpClient`:

```csharp
public async Task<NotionDataSourceResult> RetrieveDataSourceAsync(
    string dataSourceId,
    CancellationToken cancellationToken)
{
    using HttpResponseMessage response = await SendAsync(
        HttpMethod.Get,
        $"/v1/data_sources/{Uri.EscapeDataString(dataSourceId)}",
        json: null,
        cancellationToken).ConfigureAwait(false);
    string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    using JsonDocument document = JsonDocument.Parse(json);
    JsonElement root = document.RootElement;
    string id = root.TryGetProperty("id", out JsonElement idElement)
        && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString() ?? dataSourceId
            : dataSourceId;
    var properties = new Dictionary<string, string?>(StringComparer.Ordinal);
    if (root.TryGetProperty("properties", out JsonElement propertyObject)
        && propertyObject.ValueKind == JsonValueKind.Object)
    {
        foreach (JsonProperty property in propertyObject.EnumerateObject())
        {
            string? type = property.Value.TryGetProperty("type", out JsonElement typeElement)
                && typeElement.ValueKind == JsonValueKind.String
                    ? typeElement.GetString()
                    : null;
            properties[property.Name] = type;
        }
    }

    return new NotionDataSourceResult(id, properties);
}
```

- [ ] **Step 5: Run focused and full client tests GREEN**

Run:

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false --filter FullyQualifiedName~NotionHttpClientTests
```

Expected: all `NotionHttpClientTests` pass, including existing 401, 409, 429,
pagination, and secret-redaction assertions.

- [ ] **Step 6: Commit the client slice**

```bash
git add src/Bukit.Notion/Client/INotionClient.cs src/Bukit.Notion/Client/NotionModels.cs src/Bukit.Notion/Client/NotionHttpClient.cs tests/Bukit.Notion.Tests/NotionHttpClientTests.cs
git diff --cached --check
git commit -m "feat(notion): retrieve remote data source schema"
```

---

### Task 2: Build deterministic remote-schema validation and reports

**Files:**
- Create: `src/Bukit.Notion/RemoteSchema/NotionRemoteSchemaModels.cs`
- Create: `src/Bukit.Notion/RemoteSchema/INotionRemoteSchemaValidationService.cs`
- Create: `src/Bukit.Notion/RemoteSchema/NotionRemoteSchemaValidationService.cs`
- Create: `src/Bukit.Notion/RemoteSchema/NotionRemoteSchemaReportWriter.cs`
- Create: `src/Bukit.Notion/RemoteSchema/NotionRemoteSchemaJsonSerializerContext.cs`
- Create: `tests/Bukit.Notion.Tests/NotionRemoteSchemaValidationTests.cs`
- Create: `tests/Bukit.Notion.Tests/NotionRemoteSchemaReportWriterTests.cs`

- [ ] **Step 1: Write the failing exact-match service test**

Create `NotionRemoteSchemaValidationTests.cs` with a temporary project root,
valid map writer, fake token provider, fake client factory, and this first test:

```csharp
[Fact]
public async Task ValidateAsync_ExactRemoteSchema_WritesSuccessfulReports()
{
    string mapPath = WriteMap("dataSourceId: ds-pages");
    var client = new FakeNotionClient(new Dictionary<string, NotionDataSourceResult>(StringComparer.Ordinal)
    {
        ["ds-pages"] = new("ds-pages", new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Title"] = "title",
            ["Slug"] = "rich_text",
            ["Published"] = "checkbox"
        })
    });
    var service = new NotionRemoteSchemaValidationService(
        new FakeNotionClientFactory(client),
        new FakeTokenProvider("secret-token"));
    string reportPath = Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "notion", "schema.json");

    NotionRemoteSchemaValidationResult result = await service.ValidateAsync(
        new NotionRemoteSchemaOptions(_projectRoot, mapPath, reportPath, "NOTION_TOKEN"),
        CancellationToken.None);

    Assert.True(result.Success);
    Assert.Equal(0, result.ExitCode);
    Assert.Equal(["ds-pages"], client.RetrievedIds);
    NotionRemoteSchemaDataSourceResult dataSource = Assert.Single(result.DataSources);
    Assert.True(dataSource.Success);
    Assert.Equal("Title", dataSource.TitleProperty);
    Assert.All(dataSource.Properties, property => Assert.Equal("matched", property.Status));
    Assert.True(File.Exists(reportPath));
    Assert.True(File.Exists(Path.ChangeExtension(reportPath, ".md")));
}
```

The fake client implements all existing `INotionClient` members; methods not
used by this feature throw `InvalidOperationException`, while
`RetrieveDataSourceAsync` records the ID and returns the configured result.

Create `NotionRemoteSchemaReportWriterTests.cs` with this redaction/shape test:

```csharp
[Fact]
public void WriteJsonAndMarkdown_UseStableSchemaWithoutSecretValues()
{
    string jsonPath = Path.Combine(_projectRoot, "schema-report.json");
    var dataSource = new NotionRemoteSchemaDataSourceResult(
        "pages",
        "page",
        "ds-pages",
        "dataSourceId",
        true,
        "Title",
        "Slug",
        [new NotionRemoteSchemaPropertyResult("Slug", "rich_text", "rich_text", "matched")],
        []);
    var report = new NotionRemoteSchemaReport(
        "bukit.notion.schema.validation.report.v1",
        true,
        "sites/demo/notion-database-map.yaml",
        [dataSource],
        []);

    NotionRemoteSchemaReportWriter.WriteJson(jsonPath, report);
    NotionRemoteSchemaReportWriter.WriteMarkdown(Path.ChangeExtension(jsonPath, ".md"), report);

    string json = File.ReadAllText(jsonPath);
    string markdown = File.ReadAllText(Path.ChangeExtension(jsonPath, ".md"));
    Assert.Contains("\"schema\": \"bukit.notion.schema.validation.report.v1\"", json, StringComparison.Ordinal);
    Assert.Contains("# Notion Remote Schema Validation Report", markdown, StringComparison.Ordinal);
    Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
    Assert.DoesNotContain("secret-token", markdown, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the service test and verify RED**

Run:

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false --filter FullyQualifiedName~NotionRemoteSchemaValidationTests.ValidateAsync_ExactRemoteSchema_WritesSuccessfulReports
```

Expected: compilation fails because the `RemoteSchema` types do not exist.

- [ ] **Step 3: Add the complete domain model**

Create `NotionRemoteSchemaModels.cs` with these exact public shapes:

```csharp
namespace Bukit.Notion.RemoteSchema;

public sealed record NotionRemoteSchemaOptions(
    string ProjectRoot,
    string DatabaseMapPath,
    string ReportPath,
    string TokenEnvironmentVariable);

public sealed record NotionRemoteSchemaDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Path = null);

public sealed record NotionRemoteSchemaPropertyResult(
    string Name,
    string? ExpectedType,
    string? ActualType,
    string Status);

public sealed record NotionRemoteSchemaDataSourceResult(
    string Entry,
    string? Collection,
    string DataSourceId,
    string IdentifierSource,
    bool Success,
    string? TitleProperty,
    string? UniqueField,
    IReadOnlyList<NotionRemoteSchemaPropertyResult>? Properties = null,
    IReadOnlyList<NotionRemoteSchemaDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<NotionRemoteSchemaPropertyResult> Properties { get; init; } = Properties ?? [];
    public IReadOnlyList<NotionRemoteSchemaDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}

public sealed record NotionRemoteSchemaArtifact(string Type, string Path, string Description);

public sealed record NotionRemoteSchemaValidationResult(
    bool Success,
    int ExitCode,
    IReadOnlyList<NotionRemoteSchemaDataSourceResult>? DataSources = null,
    IReadOnlyList<NotionRemoteSchemaDiagnostic>? Diagnostics = null,
    IReadOnlyList<NotionRemoteSchemaArtifact>? Artifacts = null)
{
    public IReadOnlyList<NotionRemoteSchemaDataSourceResult> DataSources { get; init; } = DataSources ?? [];
    public IReadOnlyList<NotionRemoteSchemaDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
    public IReadOnlyList<NotionRemoteSchemaArtifact> Artifacts { get; init; } = Artifacts ?? [];
}

public sealed record NotionRemoteSchemaReport(
    string Schema,
    bool Success,
    string DatabaseMap,
    IReadOnlyList<NotionRemoteSchemaDataSourceResult>? DataSources = null,
    IReadOnlyList<NotionRemoteSchemaDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<NotionRemoteSchemaDataSourceResult> DataSources { get; init; } = DataSources ?? [];
    public IReadOnlyList<NotionRemoteSchemaDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}
```

Create `INotionRemoteSchemaValidationService.cs`:

```csharp
namespace Bukit.Notion.RemoteSchema;

public interface INotionRemoteSchemaValidationService
{
    NotionRemoteSchemaValidationResult Validate(NotionRemoteSchemaOptions options);
}
```

- [ ] **Step 4: Add AOT-safe report serialization and writers**

Create `NotionRemoteSchemaJsonSerializerContext.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Bukit.Notion.RemoteSchema;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(NotionRemoteSchemaReport))]
public sealed partial class NotionRemoteSchemaJsonSerializerContext : JsonSerializerContext;
```

Create `NotionRemoteSchemaReportWriter.cs` with `WriteJson`, `WriteMarkdown`, and
`CreateReport`. `WriteJson` must serialize with
`NotionRemoteSchemaJsonSerializerContext.Default.NotionRemoteSchemaReport`.
`WriteMarkdown` must emit:

```text
# Notion Remote Schema Validation Report

- Success: true|false
- Database map: <project-relative map>

| Entry | Collection | Data source | Identifier source | Success | Title property | Unique field |
| --- | --- | --- | --- | --- | --- | --- |

## Properties: <entry>

| Property | Expected type | Actual type | Status |
| --- | --- | --- | --- |

## Diagnostics

| Severity | Code | Message | Path |
| --- | --- | --- | --- |
```

Escape `|` and line endings with the same strategy as
`NotionPushReportWriter`. Create parent directories before either write.

- [ ] **Step 5: Implement successful comparison orchestration**

Create `NotionRemoteSchemaValidationService.cs` with constructor injection and
sync/async entry points:

```csharp
public sealed class NotionRemoteSchemaValidationService : INotionRemoteSchemaValidationService
{
    private const string ReportSchema = "bukit.notion.schema.validation.report.v1";
    private readonly INotionClientFactory _clientFactory;
    private readonly INotionTokenProvider _tokenProvider;

    public NotionRemoteSchemaValidationService()
        : this(new HttpNotionClientFactory(), new EnvironmentNotionTokenProvider())
    {
    }

    public NotionRemoteSchemaValidationService(
        INotionClientFactory clientFactory,
        INotionTokenProvider tokenProvider)
    {
        _clientFactory = clientFactory;
        _tokenProvider = tokenProvider;
    }

    public NotionRemoteSchemaValidationResult Validate(NotionRemoteSchemaOptions options)
        => ValidateAsync(options, CancellationToken.None).GetAwaiter().GetResult();

    public async Task<NotionRemoteSchemaValidationResult> ValidateAsync(
        NotionRemoteSchemaOptions options,
        CancellationToken cancellationToken)
    {
        NotionDatabaseMapValidationResult mapValidation = NotionDatabaseMapValidator.Validate(
            options.ProjectRoot,
            options.DatabaseMapPath);
        if (!mapValidation.Success)
        {
            return WriteResult(options, false, 2, [], mapValidation.Diagnostics
                .Select(diagnostic => new NotionRemoteSchemaDiagnostic(
                    diagnostic.Code,
                    diagnostic.Severity,
                    diagnostic.Message,
                    diagnostic.Path))
                .ToArray());
        }

        if (!NotionPluginConstants.IsAllowedTokenEnvironmentVariable(options.TokenEnvironmentVariable))
        {
            return WriteResult(options, false, 2, [],
            [
                new NotionRemoteSchemaDiagnostic(
                    "notion.tokenEnvNotAllowed",
                    NotionDiagnosticSeverity.Error,
                    "Remote schema validation token must come from an allowlisted environment variable.")
            ]);
        }

        string? token = _tokenProvider.GetToken(options.TokenEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(token))
        {
            return WriteResult(options, false, 2, [],
            [
                new NotionRemoteSchemaDiagnostic(
                    "notion.tokenMissing",
                    NotionDiagnosticSeverity.Error,
                    $"Environment variable {options.TokenEnvironmentVariable} is required for remote schema validation.")
            ]);
        }

        INotionClient client = _clientFactory.Create(new NotionRequestOptions(token));
        var dataSources = new List<NotionRemoteSchemaDataSourceResult>();
        foreach (NotionDatabaseMapEntry entry in mapValidation.DatabaseMap!.Databases.Values.OrderBy(entry => entry.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            NotionDataSourceResult remote = await client.RetrieveDataSourceAsync(
                entry.EffectiveDataSourceId!,
                cancellationToken).ConfigureAwait(false);
            dataSources.Add(Compare(entry, remote));
        }

        IReadOnlyList<NotionRemoteSchemaDiagnostic> diagnostics = dataSources
            .SelectMany(dataSource => dataSource.Diagnostics)
            .ToArray();
        bool success = diagnostics.Count == 0;
        return WriteResult(options, success, success ? 0 : 2, dataSources, diagnostics);
    }
}
```

Implement `Compare` using ordinal dictionary lookup and enumerate local mappings
with `OrderBy(mapping => mapping.Key, StringComparer.Ordinal)`. Produce one property result
for every local mapping. Set `TitleProperty` only when exactly one remote
property has type `title`. A successful entry has no diagnostics and all property
statuses are `matched`.

Implement `WriteResult` so every unsuccessful result appends exactly one summary
diagnostic `notion.remoteSchemaValidationFailed`, writes JSON/Markdown, and
returns both report artifacts. Set the report `DatabaseMap` to a normalized
project-relative path with:

```csharp
Path.GetRelativePath(options.ProjectRoot, options.DatabaseMapPath)
    .Replace(Path.DirectorySeparatorChar, '/')
```

- [ ] **Step 6: Run the exact-match and report tests GREEN**

Run:

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false --filter FullyQualifiedName~NotionRemoteSchema
```

Expected: exact-match validation and report writer tests pass; report JSON uses
`bukit.notion.schema.validation.report.v1` and contains no `secret-token`.

- [ ] **Step 7: Commit the successful domain slice**

```bash
git add src/Bukit.Notion/RemoteSchema tests/Bukit.Notion.Tests/NotionRemoteSchemaValidationTests.cs tests/Bukit.Notion.Tests/NotionRemoteSchemaReportWriterTests.cs
git diff --cached --check
git commit -m "feat(notion): validate matching remote schemas"
```

---

### Task 3: Complete mismatch, title, unique-field, legacy-ID, and API failure behavior

**Files:**
- Modify: `tests/Bukit.Notion.Tests/NotionRemoteSchemaValidationTests.cs`
- Modify: `src/Bukit.Notion/RemoteSchema/NotionRemoteSchemaValidationService.cs`

- [ ] **Step 1: Add the failing schema-mismatch matrix**

Add tests with these exact assertions:

```csharp
[Fact]
public async Task ValidateAsync_MissingAndMismatchedProperties_EmitsGranularAndSummaryDiagnostics()
{
    NotionRemoteSchemaValidationResult result = await ValidateWithRemotePropertiesAsync(
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Title"] = "title",
            ["Slug"] = "url"
        });

    Assert.False(result.Success);
    Assert.Equal(2, result.ExitCode);
    Assert.Contains(result.Diagnostics, d => d.Code == "notion.remoteSchemaPropertyTypeMismatch");
    Assert.Contains(result.Diagnostics, d => d.Code == "notion.remoteSchemaPropertyMissing");
    Assert.Contains(result.Diagnostics, d => d.Code == "notion.remoteSchemaValidationFailed");
}

[Theory]
[InlineData(false, "notion.remoteSchemaTitleMissing")]
[InlineData(true, "notion.remoteSchemaTitleNotUnique")]
public async Task ValidateAsync_InvalidTitleCardinality_EmitsStableDiagnostic(
    bool duplicateTitle,
    string expectedCode)
{
    var properties = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["Slug"] = "rich_text",
        ["Published"] = "checkbox"
    };
    if (duplicateTitle)
    {
        properties["Title"] = "title";
        properties["Name"] = "title";
    }

    NotionRemoteSchemaValidationResult result = await ValidateWithRemotePropertiesAsync(properties);

    Assert.Contains(result.Diagnostics, d => d.Code == expectedCode);
}

[Fact]
public async Task ValidateAsync_MissingRemoteUniqueField_EmitsDedicatedDiagnostic()
{
    NotionRemoteSchemaValidationResult result = await ValidateWithRemotePropertiesAsync(
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Title"] = "title",
            ["Published"] = "checkbox"
        });

    Assert.Contains(result.Diagnostics, d => d.Code == "notion.remoteSchemaUniqueFieldMissing");
    Assert.Contains(result.Diagnostics, d => d.Code == "notion.remoteSchemaPropertyMissing");
}

[Fact]
public async Task ValidateAsync_PropertyMatchingIsOrdinalAndExtraRemotePropertiesAreIgnored()
{
    NotionRemoteSchemaValidationResult result = await ValidateWithRemotePropertiesAsync(
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Title"] = "title",
            ["slug"] = "rich_text",
            ["Published"] = "checkbox",
            ["Owner"] = "people"
        });

    Assert.Contains(result.Diagnostics, d => d.Code == "notion.remoteSchemaPropertyMissing");
    Assert.DoesNotContain(result.DataSources[0].Properties, property => property.Name == "Owner");
}
```

- [ ] **Step 2: Run mismatch tests and verify RED**

Run:

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false --filter FullyQualifiedName~NotionRemoteSchemaValidationTests
```

Expected: assertions fail because `Compare` does not yet emit every required
diagnostic and status.

- [ ] **Step 3: Implement the complete comparison rules**

For each mapped property, emit:

```csharp
if (!remote.Properties.TryGetValue(mapping.Name, out string? actualType))
{
    properties.Add(new NotionRemoteSchemaPropertyResult(
        mapping.Name,
        mapping.Type,
        null,
        "missing"));
    diagnostics.Add(Error(
        "notion.remoteSchemaPropertyMissing",
        $"Remote property {mapping.Name} does not exist.",
        $"{entry.Name}.properties.{mapping.Name}"));
}
else if (!string.Equals(mapping.Type, actualType, StringComparison.Ordinal))
{
    properties.Add(new NotionRemoteSchemaPropertyResult(
        mapping.Name,
        mapping.Type,
        actualType,
        "type-mismatch"));
    diagnostics.Add(Error(
        "notion.remoteSchemaPropertyTypeMismatch",
        $"Remote property {mapping.Name} has type {actualType ?? "unknown"}; expected {mapping.Type}.",
        $"{entry.Name}.properties.{mapping.Name}"));
}
else
{
    properties.Add(new NotionRemoteSchemaPropertyResult(
        mapping.Name,
        mapping.Type,
        actualType,
        "matched"));
}
```

Count title properties from the full remote schema and emit missing/not-unique
diagnostics. Independently test `remote.Properties.ContainsKey(entry.UniqueField!)`
and emit `notion.remoteSchemaUniqueFieldMissing` when false.

- [ ] **Step 4: Add failing aggregation, legacy alias, and API mapping tests**

Add tests that prove:

```csharp
Assert.Equal(["ds-a", "ds-b"], client.RetrievedIds);
Assert.Equal(["a", "b"], result.DataSources.Select(item => item.Entry).ToArray());
Assert.Equal("databaseId", Assert.Single(legacyResult.DataSources).IdentifierSource);
Assert.Contains(notFound.Diagnostics, d => d.Code == "notion.remoteSchemaDataSourceNotFound");
Assert.Equal(2, notFound.ExitCode);
Assert.Contains(unauthorized.Diagnostics, d => d.Code == "notion.apiUnauthorized");
Assert.Equal(1, unauthorized.ExitCode);
Assert.Contains(httpFailure.Diagnostics, d => d.Code == "notion.httpError");
Assert.Equal(1, httpFailure.ExitCode);
```

Configure the fake client to return results or throw per identifier. Include a
two-entry map ordered `b`, then `a`, and prove the service requests/reports `a`,
then `b`.

Add local-input tests with these assertions:

```csharp
Assert.False(invalidMap.Success);
Assert.Empty(client.RetrievedIds);
Assert.True(File.Exists(reportPath));
Assert.Contains(invalidMap.Diagnostics, d => d.Code == "notion.databaseMapInvalidYaml");

Assert.False(missingToken.Success);
Assert.Empty(client.RetrievedIds);
Assert.Equal("notion.tokenMissing", missingToken.Diagnostics[0].Code);

Assert.False(disallowedToken.Success);
Assert.Empty(client.RetrievedIds);
Assert.Equal("notion.tokenEnvNotAllowed", disallowedToken.Diagnostics[0].Code);
```

- [ ] **Step 5: Run failure tests and verify RED**

Run the same `NotionRemoteSchemaValidationTests` filter.

Expected: API exceptions currently escape and aggregation/exit-code assertions
fail.

- [ ] **Step 6: Implement per-entry failure aggregation**

Wrap each retrieve call with:

```csharp
try
{
    NotionDataSourceResult remote = await client.RetrieveDataSourceAsync(
        entry.EffectiveDataSourceId!,
        cancellationToken).ConfigureAwait(false);
    dataSources.Add(Compare(entry, remote));
}
catch (NotionApiException ex)
{
    string code = ex.StatusCode == HttpStatusCode.NotFound
        ? "notion.remoteSchemaDataSourceNotFound"
        : MapApiDiagnosticCode(ex);
    runtimeFailure |= ex.StatusCode != HttpStatusCode.NotFound;
    dataSources.Add(FailedDataSource(entry, code, ex.Message));
}
catch (HttpRequestException ex)
{
    runtimeFailure = true;
    dataSources.Add(FailedDataSource(entry, "notion.httpError", ex.Message));
}
```

Use the existing push mappings for 401/403/409/429/5xx values, without exposing
response bodies. Compute final exit code as `1` when any runtime failure exists,
otherwise `2` for validation failure. Continue every entry unless cancellation
is requested.

- [ ] **Step 7: Run the entire domain suite GREEN**

Run:

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false
```

Expected: all `Bukit.Notion.Tests` pass, including old push/client behavior and
new schema validation behavior.

- [ ] **Step 8: Commit the failure-complete domain slice**

```bash
git add src/Bukit.Notion/RemoteSchema/NotionRemoteSchemaValidationService.cs tests/Bukit.Notion.Tests/NotionRemoteSchemaValidationTests.cs
git diff --cached --check
git commit -m "feat(notion): report remote schema mismatches"
```

---

### Task 4: Expose and invoke `notion schema validate`

**Files:**
- Modify: `src/Bukit.Notion/NotionPluginConstants.cs`
- Modify: `plugins/Bukit.Plugin.Notion/NotionCommandSpecFactory.cs`
- Modify: `plugins/Bukit.Plugin.Notion/NotionPluginInvoker.cs`
- Modify: `plugins/Bukit.Plugin.Notion/NotionOptionsMapper.cs`
- Create: `plugins/Bukit.Plugin.Notion/NotionRemoteSchemaValidateCommandHandler.cs`
- Modify: `tests/Bukit.Plugin.Notion.Tests/NotionPluginManifestTests.cs`
- Create: `tests/Bukit.Plugin.Notion.Tests/NotionRemoteSchemaValidateInvokeTests.cs`

- [ ] **Step 1: Write failing runtime-manifest assertions**

Add this helper and invoke it for both runtime and static manifests:

```csharp
private static void AssertSchemaValidateContract(PluginCommandSpec notion)
{
    PluginCommandSpec schema = Assert.Single(notion.Subcommands, command => command.Name == "schema");
    PluginCommandSpec validate = Assert.Single(schema.Subcommands, command => command.Name == "validate");
    Assert.Empty(validate.Arguments);

    PluginOptionSpec databaseMap = Assert.Single(validate.Options, option => option.Name == "--database-map");
    Assert.Equal("string", databaseMap.Type);
    Assert.True(databaseMap.Required);

    PluginOptionSpec tokenEnv = Assert.Single(validate.Options, option => option.Name == "--token-env");
    Assert.Equal(["NOTION_TOKEN"], tokenEnv.AllowedValues);

    PluginOptionSpec report = Assert.Single(validate.Options, option => option.Name == "--report");
    Assert.Equal("string", report.Type);
    Assert.False(report.Required);
}
```

For the RED stage, call the helper only against the runtime manifest; static YAML
is updated in Task 5.

- [ ] **Step 2: Run manifest test and verify RED**

Run:

```bash
dotnet test tests/Bukit.Plugin.Notion.Tests/Bukit.Plugin.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false --filter FullyQualifiedName~NotionPluginManifestTests.RuntimeManifest
```

Expected: failure because no `schema` subcommand exists.

- [ ] **Step 3: Declare the nested runtime command**

Add `CreateSchemaCommand` and `CreateSchemaValidateCommand` to
`NotionCommandSpecFactory`:

```csharp
private static PluginCommandSpec CreateSchemaCommand()
    => new(
        Name: "schema",
        Description: "Inspect and validate remote Notion data-source schemas.",
        Subcommands: [CreateSchemaValidateCommand()]);

private static PluginCommandSpec CreateSchemaValidateCommand()
    => new(
        Name: "validate",
        Description: "Validate a local database map against remote Notion schemas.",
        Options:
        [
            new PluginOptionSpec(
                "--database-map",
                "string",
                "Path to notion-database-map.yaml.",
                Required: true),
            new PluginOptionSpec(
                "--token-env",
                "string",
                "Allowlisted environment variable containing the Notion token.",
                AllowedValues: [NotionPluginConstants.TokenEnvironmentVariable]),
            new PluginOptionSpec(
                "--report",
                "string",
                "Optional JSON report output path.")
        ]);
```

Add `CreateSchemaCommand()` to the top-level Notion subcommands.

- [ ] **Step 4: Write failing option-mapper and handler tests**

Create `NotionRemoteSchemaValidateInvokeTests.cs` with tests proving:

```csharp
Assert.Equal("notion.remoteSchemaMissingDatabaseMap", Assert.Single(missing.Diagnostics).Code);
Assert.Equal("notion.tokenEnvNotAllowed", Assert.Single(disallowedToken.Diagnostics).Code);
Assert.Equal("notion.reportPathOutsideAllowedOutput", Assert.Single(outsideReport.Diagnostics).Code);
Assert.Equal(
    Path.Combine(projectRoot, ".bukit", "reports", "plugin-output", "notion", "notion-schema-validation-report.json"),
    valid.Options!.ReportPath);
```

Add a fake `INotionRemoteSchemaValidationService` and prove the handler returns
the fake result's exit code, diagnostics, and project-relative artifact paths.

- [ ] **Step 5: Run mapper/handler tests and verify RED**

Run:

```bash
dotnet test tests/Bukit.Plugin.Notion.Tests/Bukit.Plugin.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false --filter FullyQualifiedName~NotionRemoteSchemaValidateInvokeTests
```

Expected: compilation fails because mapper records, mapper method, and handler
do not exist.

- [ ] **Step 6: Implement option mapping and safe defaults**

Add constants:

```csharp
public const string RemoteSchemaReportFileName = "notion-schema-validation-report.json";
```

Add mapper records and a `MapRemoteSchemaValidateOptions` method. It must:

```csharp
if (!request.Command.Path.SequenceEqual(["notion", "schema", "validate"], StringComparer.Ordinal))
{
    diagnostics.Add(Error("plugin.notion.unsupportedCommand", "Expected notion schema validate command path."));
}
```

Read required string `--database-map`, optional allowlisted `--token-env`, and
optional `--report`. Default report path is:

```csharp
Path.Combine(
    root,
    ".bukit",
    "reports",
    "plugin-output",
    "notion",
    NotionPluginConstants.RemoteSchemaReportFileName)
```

Resolve both paths with `NotionPathGuard`. Permit reports only under the same
two roots already accepted by push reports.

- [ ] **Step 7: Implement handler and invoker dispatch**

Create the handler with default and injectable overloads:

```csharp
public static PluginInvokeResponse Handle(string requestId, PluginInvokeRequest request)
    => Handle(requestId, request, new NotionRemoteSchemaValidationService());

public static PluginInvokeResponse Handle(
    string requestId,
    PluginInvokeRequest request,
    INotionRemoteSchemaValidationService service)
{
    NotionRemoteSchemaValidateMapperResult mapped = NotionOptionsMapper.MapRemoteSchemaValidateOptions(request);
    if (!mapped.Success || mapped.Options is null)
    {
        return new PluginInvokeResponse(
            "invokeResponse",
            PluginProtocolConstants.ProtocolVersion,
            requestId,
            false,
            2,
            Diagnostics: mapped.Diagnostics);
    }

    NotionRemoteSchemaValidationResult result = service.Validate(mapped.Options);
    return new PluginInvokeResponse(
        "invokeResponse",
        PluginProtocolConstants.ProtocolVersion,
        requestId,
        result.Success,
        result.ExitCode,
        Artifacts: result.Artifacts.Select(artifact => new PluginArtifact(
            artifact.Type,
            NotionPluginPathFormatter.ToProjectRelativePath(mapped.Options.ProjectRoot, artifact.Path),
            artifact.Description)).ToArray(),
        Diagnostics: result.Diagnostics.Select(diagnostic => new PluginDiagnostic(
            diagnostic.Code,
            diagnostic.Severity,
            diagnostic.Message,
            diagnostic.Path)).ToArray());
}
```

Dispatch the exact path in `NotionPluginInvoker` before the unsupported fallback.
Update its unsupported-command message to list `notion schema validate`.

- [ ] **Step 8: Run plugin tests GREEN**

Run:

```bash
dotnet test tests/Bukit.Plugin.Notion.Tests/Bukit.Plugin.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false --filter "FullyQualifiedName~NotionPluginManifestTests.RuntimeManifest|FullyQualifiedName~NotionRemoteSchemaValidateInvokeTests"
```

Expected: runtime command contract, option mapping, handler translation, and
artifact paths pass.

- [ ] **Step 9: Commit the runtime plugin slice**

```bash
git add src/Bukit.Notion/NotionPluginConstants.cs plugins/Bukit.Plugin.Notion/NotionCommandSpecFactory.cs plugins/Bukit.Plugin.Notion/NotionPluginInvoker.cs plugins/Bukit.Plugin.Notion/NotionOptionsMapper.cs plugins/Bukit.Plugin.Notion/NotionRemoteSchemaValidateCommandHandler.cs tests/Bukit.Plugin.Notion.Tests/NotionPluginManifestTests.cs tests/Bukit.Plugin.Notion.Tests/NotionRemoteSchemaValidateInvokeTests.cs
git diff --cached --check
git commit -m "feat(notion): expose schema validation command"
```

---

### Task 5: Synchronize static manifests, nested CLI forwarding, and operator docs

**Files:**
- Modify: `plugins/Bukit.Plugin.Notion/examples/minimal/plugins/notion/plugin.yaml`
- Modify: `plugins/Bukit.Plugin.Notion/plugin.yaml.template`
- Modify: `tests/Bukit.Plugin.Notion.Tests/NotionPluginManifestTests.cs`
- Modify: `tests/Bukit.Cli.Tests/PluginCliIntegrationTests.cs`
- Modify: `plugins/Bukit.Plugin.Notion/README.md`
- Modify: `docs/plugins/Bukit.Plugin.Notion 开发技术书.md`

- [ ] **Step 1: Enable the failing static-manifest assertion**

Call `AssertSchemaValidateContract(notion)` from
`StaticManifest_DeclaresNotionCommandSurfaceAndPermissions`.

- [ ] **Step 2: Run static manifest test and verify RED**

Run:

```bash
dotnet test tests/Bukit.Plugin.Notion.Tests/Bukit.Plugin.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false --filter FullyQualifiedName~NotionPluginManifestTests.StaticManifest_DeclaresNotionCommandSurfaceAndPermissions
```

Expected: failure because static YAML has no `schema` subcommand.

- [ ] **Step 3: Add identical nested YAML to both manifests**

Under the top-level `notion` subcommands, add:

```yaml
      - name: schema
        description: Inspect and validate remote Notion data-source schemas.
        subcommands:
          - name: validate
            description: Validate a local database map against remote Notion schemas.
            options:
              - name: --database-map
                type: string
                description: Path to notion-database-map.yaml.
                required: true
              - name: --token-env
                type: string
                description: Allowlisted environment variable containing the Notion token.
                required: false
                allowedValues:
                  - NOTION_TOKEN
              - name: --report
                type: string
                description: Optional JSON report output path.
                required: false
```

Do not change existing version, platform, permission, push, or validation fields.

- [ ] **Step 4: Add the three-level CLI characterization test**

Add to `PluginCliIntegrationTests`:

```csharp
[Fact]
public async Task PluginNestedSubcommandInvoke_UsesFullThreeSegmentPathAndOptions()
{
    var client = new RuntimePermissionProtocolClient(new PluginPermissionSet());
    var plugin = new ResolvedPlugin(
        "notion",
        "1.0.0",
        "test-rid",
        "/tmp/notion",
        _tempDir,
        new PluginHostInfo("Bukit", "1.0.0", "test-rid"));
    var command = new PluginCommandSpec(
        "notion",
        "Notion",
        Subcommands:
        [
            new PluginCommandSpec(
                "schema",
                "Schema",
                Subcommands:
                [
                    new PluginCommandSpec(
                        "validate",
                        "Validate",
                        Options:
                        [
                            new PluginOptionSpec("--database-map", "string", "Map", Required: true),
                            new PluginOptionSpec("--token-env", "string", "Token")
                        ])
                ])
        ]);
    CommandDescriptor descriptor = PluginCommandDescriptorFactory.Create(plugin, command, client);
    var parsed = Bukit.Cli.Shared.Cli.Parsing.CliParser.Parse(
        descriptor.Spec,
        ["schema", "validate", "--database-map", "./map.yaml", "--token-env", "NOTION_TOKEN"]);

    Assert.True(parsed.IsSuccess);
    Assert.Equal(0, await descriptor.DispatchAsync(parsed));
    Assert.Equal(["notion", "schema", "validate"], client.LastInvokeRequest!.Command.Path);
    Assert.Equal("./map.yaml", client.LastInvokeRequest.Command.Options["--database-map"].GetString());
    Assert.Equal("NOTION_TOKEN", client.LastInvokeRequest.Command.Options["--token-env"].GetString());
}
```

- [ ] **Step 5: Run the CLI characterization test**

Run:

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false --filter FullyQualifiedName~PluginNestedSubcommandInvoke_UsesFullThreeSegmentPathAndOptions
```

Expected: pass because recursive descriptor/binding is already implemented. If
it fails, stop and add a focused failing Core regression test, fix the smallest
recursion defect within Task 5, and rerun both tests; do not flatten the approved
command to avoid fixing Core.

- [ ] **Step 6: Update user-facing documentation**

Add this workflow before dry-run/live push in the README and document the same
sequence in the technical book:

```bash
bukit notion schema validate \
  --database-map ./sites/demo/notion-seed/notion-database-map.yaml \
  --token-env NOTION_TOKEN
```

Document both report paths, exact case-sensitive name/type matching, title
cardinality, `uniqueField`, `dataSourceId` precedence, legacy `databaseId` alias,
and that extra remote properties are deferred to `schema diff`. Add a v1.1
section to the technical book with all required error codes and exit codes.

- [ ] **Step 7: Run static/plugin/CLI suites GREEN**

Run serially:

```bash
dotnet test tests/Bukit.Plugin.Notion.Tests/Bukit.Plugin.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false --filter "FullyQualifiedName~PluginSubcommandInvoke|FullyQualifiedName~PluginNestedSubcommandInvoke"
```

Expected: all plugin tests and both two-/three-level forwarding tests pass.

- [ ] **Step 8: Commit manifests and documentation**

```bash
git add plugins/Bukit.Plugin.Notion/examples/minimal/plugins/notion/plugin.yaml plugins/Bukit.Plugin.Notion/plugin.yaml.template tests/Bukit.Plugin.Notion.Tests/NotionPluginManifestTests.cs tests/Bukit.Cli.Tests/PluginCliIntegrationTests.cs plugins/Bukit.Plugin.Notion/README.md "docs/plugins/Bukit.Plugin.Notion 开发技术书.md"
git diff --cached --check
git commit -m "docs(notion): document remote schema validation"
```

---

### Task 6: Full verification, requirement audit, and branch closeout

**Files:**
- Audit all files changed by Tasks 1–5.
- Do not modify `guide-0.1/` or `scripts-0.1/`.

- [ ] **Step 1: Run focused suites serially**

```bash
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false
dotnet test tests/Bukit.Plugin.Notion.Tests/Bukit.Plugin.Notion.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --no-restore -maxcpucount:1 -nodeReuse:false --filter "FullyQualifiedName~PluginSubcommandInvoke|FullyQualifiedName~PluginNestedSubcommandInvoke"
```

Expected: all selected tests pass with zero failures.

- [ ] **Step 2: Run repository formatting and diff checks**

```bash
dotnet format bukit.slnx --verify-no-changes --no-restore
git diff --check HEAD~5..HEAD
git status --short --branch
```

Expected: formatter and diff checks pass; only intentional feature files differ
from the branch base and the worktree has no uncommitted changes.

- [ ] **Step 3: Run the repository development gate**

```bash
bash scripts/quality-gate.sh Release
```

Expected: the authoritative `ci-full` development gate exits `0`. If it fails,
fix only the current feature, rerun the targeted failing command, then rerun this
same gate before continuing.

- [ ] **Step 4: Audit every explicit requirement**

Record evidence for this checklist:

```text
[ ] local database map is read and validated before network access
[ ] every valid map entry causes one retrieve-data-source request
[ ] dataSourceId wins and databaseId remains a legacy alias
[ ] property names are matched ordinal/case-sensitive
[ ] property types are matched ordinal/case-sensitive
[ ] exactly one remote title property is required
[ ] uniqueField must exist remotely
[ ] JSON and Markdown reports exist for success and validation failure
[ ] all six requested error codes are covered
[ ] title-not-unique has a dedicated stable error code
[ ] schema mismatch exits 2; remote operational failure exits 1
[ ] reports contain no token or raw response body
[ ] Import/Core/Labs/plugin boundaries remain unchanged
[ ] no backup-only directory changed
[ ] v1.2-v1.6 features were not implemented
```

Use tests, generated temporary reports, source inspection, and `git diff` as
evidence. A missing proof item means the task is not complete.

- [ ] **Step 5: Inspect final branch history and diff**

```bash
git log --oneline --decorate --max-count=8
git diff --stat HEAD~5..HEAD
git diff --name-only HEAD~5..HEAD
git status --short --branch
```

Expected: five focused feature commits after the design/plan baseline, no
unrelated RC files, no backup paths, and a clean worktree.

- [ ] **Step 6: Use the finishing workflow**

Invoke `superpowers:verification-before-completion`, then
`superpowers:finishing-a-development-branch`. Because the original main checkout
contains unrelated uncommitted RC work, do not merge into it automatically.
Report the feature branch name, commit list, test/gate evidence, and any honest
environment blocker.
