# Bukit Import Full Pluginization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将现有 `bukit import` 能力完整接入 `bukit-plugin-v1` 插件系统，保持 `Bukit.Importing` 为一个完整项目，不拆分项目，不删减功能，不改变已实现命令参数。

**Architecture:** `Bukit.Plugin.Import` 是外部进程插件和协议适配层；`Bukit.Importing` 是完整 Import 应用/领域项目，承载 html-demo、seed、theme use、Notion push handoff/push、verify orchestration。为满足完整兼容，允许 `Bukit.Importing` 收敛当前 Labs import 所需依赖；仍禁止 `Bukit.Plugin.Import` 直接引用 Labs、Host、Cli、Engine 或 Config。

**Tech Stack:** .NET 10, C#, `Bukit.Plugin.Abstractions`, `Bukit.Importing`, `Bukit.Config`, `Bukit.Engine`, `Bukit.Shared.Notion`, YamlDotNet, xUnit, YAML plugin manifests.

---

## Non-Negotiable Requirements

- 不拆分 `src/Bukit-Plugins/Bukit.Importing/` 为多个项目。
- 可以在 `Bukit.Importing` 项目内新增文件、移动当前 Labs import 私有 helper、补齐 orchestration；这不是拆分，是归并。
- `Bukit.Plugin.Import` 必须接入 `Bukit.Importing` 的完整功能面，而不是只接 html-demo 子集。
- 命令、参数、默认值、互斥校验和退出码必须与当前 `src/Bukit-Labs/Bukit.Labs.Cli/Commands/Import/ImportCommand.cs` 保持一致。
- `Bukit.Plugin.Import` 不得直接引用 `Bukit.Labs.Cli`，也不得 shell out 到 Labs CLI 来伪装支持。
- `Bukit.Plugin.Import` stdout 必须只输出单个协议 JSON；所有业务输出都转为 plugin messages 或 stderr。
- `guide-0.1/` 和 `scripts-0.1/` 仍是备份区，不修改。

## Command Compatibility Contract

`import html-demo <demo-dir>` 必须支持这些当前已实现参数：

```text
--config
--site
--theme
--force
--use
--verify
--no-extract-content
--no-seed
--content-source
--build-source
--site-path
--language
--dry-run
--strict
--overwrite
--no-preserve-html
--no-report
--base-url
--route-map
--push-notion
--notion-database-id
--notion-database-map
--create-missing-notion-databases
--notion-parent-page-id
--notion-generated-database-map
--notion-token-env
--notion-report
--no-validate-notion-schema
```

`import seed <seed-dir>` 必须支持这些当前已实现参数：

```text
--output
--force
```

默认值必须保持：
- `--content-source` 默认 `notion`
- `--build-source` 默认 `markdown`
- `--language` 默认 `zh`
- `--notion-token-env` 默认 `NOTION_TOKEN`
- `--strict` 有值且不是 `warn` 时按 `fail`
- `--no-extract-content`、`--no-seed`、`--no-preserve-html`、`--no-report`、`--no-validate-notion-schema` 保持当前反向 flag 语义

## Current Implementation Facts

- 当前 `Bukit.Plugin.Import` 只是协议 shell：`ImportPluginInvoker.InvokeNotImplemented` 返回 `plugin.import.notImplemented`。
- 当前 `Bukit.Importing` 已有 html-demo 领域能力：scan、layout、component、theme、asset、content extraction、seed generation、route map、report、safety scan。
- 当前 `import seed` 的 reader/writer 还在 Labs import 命令目录，必须归并进 `Bukit.Importing`。
- 当前 `--use` 依赖 Labs `ThemeCommand.SetThemeAsync` 的 YAML 修改行为，必须在 `Bukit.Importing` 内实现等价服务。
- 当前 `--push-notion` 依赖 Labs `NotionCommand` 和 `Bukit.Shared.Notion`，必须在 `Bukit.Importing` 内实现等价 import-owned push workflow。
- 当前 `--verify` 依赖 `Bukit.Config`、`Bukit.Engine`、`Bukit.Engine.Abstractions`，完整兼容要求 `Bukit.Importing` 承担该 orchestration。
- 当前插件 Host 按 `.bukit/plugins.yaml` 的 `environment.read` 传递环境变量，进程环境默认被清空。`--push-notion` 因此必须在 plugin config 中显式 grant token env。

## Target File Structure

Create inside `src/Bukit-Plugins/Bukit.Importing/`:
- `ImportCommandOptions.cs` - complete command options matching current Labs import command.
- `ImportCommandResult.cs` - exit code, messages, diagnostics, artifacts.
- `ImportCommandOutput.cs` - captures stdout/stderr-like domain messages without writing plugin stdout.
- `ImportCommandWorkflow.cs` - full `html-demo` and `seed` orchestration.
- `ImportSeedRecord.cs` - public seed record model moved from Labs.
- `ImportSeedRecordReader.cs` - seed json/yaml reader moved from Labs.
- `ImportSeedContentWriter.cs` - seed markdown writer moved from Labs.
- `ImportThemeSelectionService.cs` - equivalent `--use` config update behavior.
- `ImportNotionPushWorkflow.cs` - equivalent `--push-notion` behavior used by import.
- `ImportVerifyWorkflow.cs` - equivalent `--verify` behavior.
- `ImportPathResolver.cs` - current import path resolution rules without plugin protocol dependency.

Create inside `src/Bukit-Plugins/Bukit.Plugin.Import/`:
- `ImportPluginCommandSpecs.cs` - full static/runtime command spec.
- `ImportPluginOptionsMapper.cs` - maps `PluginInvokeRequest` to `ImportCommandOptions`.
- `ImportPluginPathGuard.cs` - project-root and granted permission checks.
- `ImportPluginResponseMapper.cs` - maps `ImportCommandResult` to `PluginInvokeResponse`.
- `ImportPluginConsoleCapture.cs` - prevents domain `Console.WriteLine` from corrupting protocol stdout.

Modify:
- `src/Bukit-Plugins/Bukit.Importing/Bukit.Importing.csproj`
- `src/Bukit-Plugins/Bukit.Plugin.Import/Bukit.Plugin.Import.csproj`
- `src/Bukit-Plugins/Bukit.Plugin.Import/ImportPluginApp.cs`
- `src/Bukit-Plugins/Bukit.Plugin.Import/ImportPluginInvoker.cs`
- `src/Bukit-Plugins/Bukit.Plugin.Import/ImportPluginManifestProvider.cs`
- `src/Bukit-Plugins/Bukit.Plugin.Import/examples/minimal/plugins/import/plugin.yaml`
- `src/Bukit-Plugins/Bukit.Plugin.Import/examples/minimal/.bukit/plugins.yaml`
- `src/Bukit-Labs/Bukit.Labs.Cli/Commands/Import/ImportCommand.cs`
- `tests/Bukit.Architecture.Tests/PluginBoundaryTests.cs`
- `tests/Bukit.Importing.Tests/*`
- `tests/Bukit.Plugin.Import.Tests/*`
- `tests/Bukit.Labs.Cli.Tests/*Import*`
- `tests/Bukit.Cli.Tests/PluginCliIntegrationTests.cs`

Do not create:
- `Bukit.Importing.Core`
- `Bukit.Importing.Notion`
- `Bukit.Importing.Engine`
- any new split project derived from `Bukit.Importing`

---

### Task 1: Lock Full Compatibility With Tests

**Files:**
- Create: `tests/Bukit.Plugin.Import.Tests/ImportPluginCommandCompatibilityTests.cs`
- Create: `tests/Bukit.Importing.Tests/ImportCommandOptionsCompatibilityTests.cs`
- Modify: `tests/Bukit.Plugin.Import.Tests/ImportPluginSkeletonTests.cs`

- [ ] **Step 1: Add command option parity test for plugin manifest**

Create `tests/Bukit.Plugin.Import.Tests/ImportPluginCommandCompatibilityTests.cs`:

```csharp
using Bukit.Plugin.Import;
using Xunit;

namespace Bukit.Plugin.Import.Tests;

public sealed class ImportPluginCommandCompatibilityTests
{
    private static readonly string[] HtmlDemoOptions =
    [
        "--config",
        "--site",
        "--theme",
        "--force",
        "--use",
        "--verify",
        "--no-extract-content",
        "--no-seed",
        "--content-source",
        "--build-source",
        "--site-path",
        "--language",
        "--dry-run",
        "--strict",
        "--overwrite",
        "--no-preserve-html",
        "--no-report",
        "--base-url",
        "--route-map",
        "--push-notion",
        "--notion-database-id",
        "--notion-database-map",
        "--create-missing-notion-databases",
        "--notion-parent-page-id",
        "--notion-generated-database-map",
        "--notion-token-env",
        "--notion-report",
        "--no-validate-notion-schema"
    ];

    private static readonly string[] SeedOptions = ["--output", "--force"];

    [Fact]
    public void Manifest_DeclaresFullCurrentImportCommandSurface()
    {
        var response = ImportPluginManifestProvider.CreateManifestResponse("req-compat");
        var import = Assert.Single(response.Commands);
        Assert.Equal("import", import.Name);

        var htmlDemo = Assert.Single(import.Subcommands, command => command.Name == "html-demo");
        Assert.Contains(htmlDemo.Arguments, argument => argument.Name == "demo-dir" && argument.Required);
        Assert.Equal(
            HtmlDemoOptions.OrderBy(value => value, StringComparer.Ordinal),
            htmlDemo.Options.Select(option => option.Name).OrderBy(value => value, StringComparer.Ordinal));

        var seed = Assert.Single(import.Subcommands, command => command.Name == "seed");
        Assert.Contains(seed.Arguments, argument => argument.Name == "seed-dir" && argument.Required);
        Assert.Equal(
            SeedOptions.OrderBy(value => value, StringComparer.Ordinal),
            seed.Options.Select(option => option.Name).OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void Manifest_RequiresPermissionsNeededByFullImportWorkflow()
    {
        var response = ImportPluginManifestProvider.CreateManifestResponse("req-perms");

        Assert.True(response.RequiredPermissions.Network);
        Assert.Contains(".", response.RequiredPermissions.FileSystem.Read);
        Assert.Contains("./themes", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains("./sites", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains("./content", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains("./data", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains("./docs/research", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains(".bukit/reports/plugin-output/import", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains("NOTION_TOKEN", response.RequiredPermissions.Environment.Read);
    }
}
```

- [ ] **Step 2: Add default-value compatibility test in `Bukit.Importing`**

Create `tests/Bukit.Importing.Tests/ImportCommandOptionsCompatibilityTests.cs`:

```csharp
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class ImportCommandOptionsCompatibilityTests
{
    [Fact]
    public void HtmlDemoOptions_DefaultsMatchCurrentLabsImportCommand()
    {
        var options = new ImportCommandOptions
        {
            Subcommand = "html-demo",
            RootDir = "/repo",
            WorkingDir = "/repo",
            DemoDir = "/repo/demo",
            ThemeName = "demo"
        };

        Assert.Equal("notion", options.ContentSource);
        Assert.Equal("markdown", options.BuildSource);
        Assert.Equal("zh", options.Language);
        Assert.Equal("NOTION_TOKEN", options.NotionTokenEnv);
        Assert.True(options.ExtractContent);
        Assert.True(options.GenerateSeed);
        Assert.True(options.PreserveHtml);
        Assert.True(options.GenerateReport);
        Assert.True(options.ValidateNotionSchema);
    }
}
```

- [ ] **Step 3: Run tests and verify failure**

Run:

```bash
dotnet test tests/Bukit.Plugin.Import.Tests/Bukit.Plugin.Import.Tests.csproj -c Release --filter ImportPluginCommandCompatibilityTests
dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj -c Release --filter ImportCommandOptionsCompatibilityTests
```

Expected: both FAIL because the manifest and `ImportCommandOptions` do not yet exist in the required shape.

- [ ] **Step 4: Commit compatibility tests**

```bash
git add tests/Bukit.Plugin.Import.Tests tests/Bukit.Importing.Tests
git commit -m "test(import-plugin): lock full import command compatibility"
```

---

### Task 2: Consolidate Current Import Command Options Into Bukit.Importing

**Files:**
- Create: `src/Bukit-Plugins/Bukit.Importing/ImportCommandOptions.cs`
- Create: `src/Bukit-Plugins/Bukit.Importing/ImportCommandResult.cs`
- Create: `src/Bukit-Plugins/Bukit.Importing/ImportCommandOutput.cs`
- Create: `src/Bukit-Plugins/Bukit.Importing/ImportPathResolver.cs`
- Modify: `src/Bukit-Plugins/Bukit.Importing/Bukit.Importing.csproj`

- [ ] **Step 1: Add project references required for full parity**

Modify `src/Bukit-Plugins/Bukit.Importing/Bukit.Importing.csproj`:

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\Bukit-Core\Bukit.Cli.Shared\Bukit.Cli.Shared.csproj" />
    <ProjectReference Include="..\..\Bukit-Core\Bukit.Config\Bukit.Config.csproj" />
    <ProjectReference Include="..\..\Bukit-Core\Bukit.Engine\Bukit.Engine.csproj" />
    <ProjectReference Include="..\..\Bukit-Core\Bukit.Engine.Abstractions\Bukit.Engine.Abstractions.csproj" />
    <ProjectReference Include="..\..\Bukit-Core\Bukit.Shared\Bukit.Shared.csproj" />
  </ItemGroup>
```

Keep existing package references. This intentionally broadens `Bukit.Importing` because full `--verify`, `--use`, and path resolution compatibility cannot be achieved while keeping it domain-only.

- [ ] **Step 2: Add command option model**

Create `src/Bukit-Plugins/Bukit.Importing/ImportCommandOptions.cs`:

```csharp
namespace Bukit.Importing;

public sealed record ImportCommandOptions
{
    public required string Subcommand { get; init; }
    public required string RootDir { get; init; }
    public required string WorkingDir { get; init; }
    public string? ConfigPath { get; init; }
    public string? Site { get; init; }
    public string? DemoDir { get; init; }
    public string? SeedDir { get; init; }
    public string? OutputDir { get; init; }
    public string? ThemeName { get; init; }
    public bool Force { get; init; }
    public bool Use { get; init; }
    public bool Verify { get; init; }
    public bool ExtractContent { get; init; } = true;
    public bool GenerateSeed { get; init; } = true;
    public string ContentSource { get; init; } = "notion";
    public string BuildSource { get; init; } = "markdown";
    public string? SitePath { get; init; }
    public string Language { get; init; } = "zh";
    public bool DryRun { get; init; }
    public string? StrictMode { get; init; }
    public bool Overwrite { get; init; }
    public bool PreserveHtml { get; init; } = true;
    public bool GenerateReport { get; init; } = true;
    public string? BaseUrl { get; init; }
    public string? RouteMapPath { get; init; }
    public bool PushNotion { get; init; }
    public string? NotionDatabaseId { get; init; }
    public string? NotionDatabaseMap { get; init; }
    public bool CreateMissingNotionDatabases { get; init; }
    public string? NotionParentPageId { get; init; }
    public string? NotionGeneratedDatabaseMap { get; init; }
    public string NotionTokenEnv { get; init; } = "NOTION_TOKEN";
    public string? NotionReport { get; init; }
    public bool ValidateNotionSchema { get; init; } = true;
}
```

- [ ] **Step 3: Add result and output models**

Create `src/Bukit-Plugins/Bukit.Importing/ImportCommandResult.cs`:

```csharp
namespace Bukit.Importing;

public sealed record ImportCommandResult
{
    public int ExitCode { get; init; }
    public ImportResult? HtmlDemoResult { get; init; }
    public ImportSeedResult? SeedResult { get; init; }
    public IReadOnlyList<ImportCommandMessage> Messages { get; init; } = [];
    public IReadOnlyList<ImportCommandDiagnostic> Diagnostics { get; init; } = [];
    public IReadOnlyList<ImportCommandArtifact> Artifacts { get; init; } = [];

    public bool Success => ExitCode == 0;
}

public sealed record ImportCommandMessage(string Level, string Message);
public sealed record ImportCommandDiagnostic(string Code, string Severity, string Message, string? Path = null);
public sealed record ImportCommandArtifact(string Type, string Path, string? Description = null);
```

Create `src/Bukit-Plugins/Bukit.Importing/ImportCommandOutput.cs`:

```csharp
namespace Bukit.Importing;

public sealed class ImportCommandOutput
{
    private readonly List<ImportCommandMessage> _messages = [];

    public IReadOnlyList<ImportCommandMessage> Messages => _messages;

    public void Info(string message) => _messages.Add(new ImportCommandMessage("info", message));
    public void Warn(string message) => _messages.Add(new ImportCommandMessage("warning", message));
    public void Error(string message) => _messages.Add(new ImportCommandMessage("error", message));
}
```

- [ ] **Step 4: Add path resolver matching current command behavior**

Create `src/Bukit-Plugins/Bukit.Importing/ImportPathResolver.cs`:

```csharp
using Bukit.Cli.Shared;

namespace Bukit.Importing;

public static class ImportPathResolver
{
    public static (string RootDir, string FullConfigPath) ResolveRoot(string? configPath, string? site)
    {
        var resolved = ConfigPathResolver.Resolve(configPath, site);
        return (resolved.RootDir, resolved.FullConfigPath);
    }

    public static string ResolveInputFromWorkingDir(string workingDir, string value)
        => Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(workingDir, value));

    public static string? ResolveSitePath(string rootDir, string? sitePath)
        => string.IsNullOrWhiteSpace(sitePath)
            ? null
            : Path.GetFullPath(Path.IsPathRooted(sitePath) ? sitePath : Path.Combine(rootDir, sitePath));

    public static string? ResolveRouteMapPath(string demoDir, string? routeMapPath)
        => string.IsNullOrWhiteSpace(routeMapPath)
            ? null
            : Path.GetFullPath(Path.IsPathRooted(routeMapPath) ? routeMapPath : Path.Combine(demoDir, routeMapPath));
}
```

- [ ] **Step 5: Run options test**

Run:

```bash
dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj -c Release --filter ImportCommandOptionsCompatibilityTests
```

Expected: PASS.

- [ ] **Step 6: Commit task**

```bash
git add src/Bukit-Plugins/Bukit.Importing tests/Bukit.Importing.Tests
git commit -m "feat(importing): add full import command option contract"
```

---

### Task 3: Move Seed Command Implementation Into Bukit.Importing Without Splitting It

**Files:**
- Create: `src/Bukit-Plugins/Bukit.Importing/ImportSeedRecord.cs`
- Create: `src/Bukit-Plugins/Bukit.Importing/ImportSeedRecordReader.cs`
- Create: `src/Bukit-Plugins/Bukit.Importing/ImportSeedContentWriter.cs`
- Create: `src/Bukit-Plugins/Bukit.Importing/ImportSeedService.cs`
- Modify: `src/Bukit-Labs/Bukit.Labs.Cli/Commands/Import/ImportCommand.cs`
- Modify/Delete: `src/Bukit-Labs/Bukit.Labs.Cli/Commands/Import/ImportSeedRecords.cs`
- Modify/Delete: `src/Bukit-Labs/Bukit.Labs.Cli/Commands/Import/ImportSeedContentWriter.cs`
- Move tests from `tests/Bukit.Labs.Cli.Tests/ImportSeed*` to `tests/Bukit.Importing.Tests/ImportSeed*`

- [ ] **Step 1: Copy existing seed reader/writer behavior into `Bukit.Importing`**

Move the current code from Labs into the same `Bukit.Importing` project. Keep these behavior points identical:
- known files: `pages`, `navigation`, `posts`, `companies`, `services`
- `.json`, `.yaml`, `.yml` support
- collection normalization
- title/name fallback
- slug fallback through `SlugHelper.Slugify`
- markdown output folders
- `overwrite=false` skips existing files

The public writer signature must preserve count behavior while also exposing written file paths:

```csharp
public sealed record ImportSeedResult
{
    public required string InputDir { get; init; }
    public required string OutputDir { get; init; }
    public int RecordsRead { get; init; }
    public int FilesWritten { get; init; }
    public IReadOnlyList<string> WrittenFiles { get; init; } = [];
}
```

- [ ] **Step 2: Add `ImportSeedService`**

Create a service that implements current `import seed` behavior:

```csharp
namespace Bukit.Importing;

public static class ImportSeedService
{
    public static ImportSeedResult Import(string inputDir, string outputDir, bool force)
    {
        if (string.IsNullOrWhiteSpace(inputDir))
            throw new ImportException("缺少必填参数: <seed-dir>", ImportErrorKind.UserInput);
        if (!Directory.Exists(inputDir))
            throw new ImportException($"seed 目录不存在: {inputDir}", ImportErrorKind.UserInput);
        if (string.IsNullOrWhiteSpace(outputDir))
            throw new ImportException("缺少必填选项: --output <content-dir>", ImportErrorKind.UserInput);

        var records = ImportSeedRecordReader.ReadDirectory(inputDir);
        var writtenFiles = ImportSeedContentWriter.WriteMarkdown(outputDir, records, force);
        return new ImportSeedResult
        {
            InputDir = inputDir,
            OutputDir = outputDir,
            RecordsRead = records.Count,
            FilesWritten = writtenFiles.Count,
            WrittenFiles = writtenFiles
        };
    }
}
```

- [ ] **Step 3: Make Labs import command use `ImportSeedService`**

In `ImportCommand.SeedAsync`, replace direct reader/writer calls with:

```csharp
try
{
    var result = ImportSeedService.Import(inputDir, outputDir, command.GetBool("--force"));
    Console.WriteLine($"seed import 完成: records={result.RecordsRead} written={result.FilesWritten} output={result.OutputDir}");
    return Task.FromResult(0);
}
catch (ImportException ex) when (ex.Kind == ImportErrorKind.UserInput)
{
    Console.Error.WriteLine(ex.Message);
    return Task.FromResult(2);
}
```

- [ ] **Step 4: Run seed parity tests**

Run:

```bash
dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj -c Release --filter ImportSeed
dotnet test tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj -c Release --filter "ImportCommandTests|ImportSeed"
```

Expected: PASS. Labs behavior remains externally identical.

- [ ] **Step 5: Commit task**

```bash
git add src/Bukit-Plugins/Bukit.Importing src/Bukit-Labs/Bukit.Labs.Cli/Commands/Import tests/Bukit.Importing.Tests tests/Bukit.Labs.Cli.Tests
git commit -m "refactor(importing): consolidate seed import command behavior"
```

---

### Task 4: Move Theme Use, Notion Push, And Verify Orchestration Into Bukit.Importing

**Files:**
- Create: `src/Bukit-Plugins/Bukit.Importing/ImportThemeSelectionService.cs`
- Create: `src/Bukit-Plugins/Bukit.Importing/ImportNotionPushWorkflow.cs`
- Create: `src/Bukit-Plugins/Bukit.Importing/ImportVerifyWorkflow.cs`
- Test: `tests/Bukit.Importing.Tests/ImportThemeSelectionServiceTests.cs`
- Test: `tests/Bukit.Importing.Tests/ImportNotionPushWorkflowTests.cs`
- Test: `tests/Bukit.Importing.Tests/ImportVerifyWorkflowTests.cs`

- [ ] **Step 1: Implement `--use` equivalent service**

Create `ImportThemeSelectionService` by moving the behavior of `ThemeCommand.SetThemeAsync` into `Bukit.Importing`:
- fails with exit code 2 equivalent when theme directory is missing
- fails with exit code 2 equivalent when config file is missing
- writes `theme.name`
- preserves existing YAML
- writes optional `theme.params` only when optional params are supplied
- emits `Theme set: <name>` equivalent message

Tests must cover:
- missing theme
- missing config
- successful `theme.name` update
- existing YAML preservation

- [ ] **Step 2: Implement `--push-notion` equivalent workflow**

Create `ImportNotionPushWorkflow` by moving import-owned logic from `ImportCommand.PushGeneratedSeedToNotionAsync` and the required `NotionCommand push` support into `Bukit.Importing`.

Preserve these behaviors:
- `--push-notion` cannot combine with `--dry-run`
- `--push-notion` requires seed generation
- `--create-missing-notion-databases` requires `--notion-parent-page-id`
- default seed dir is `sites/<theme>/notion-seed` for `content-source notion`, otherwise `sites/<theme>/data`
- default database map is `notion-database-map.yaml` when present
- missing database ids fail unless `--create-missing-notion-databases` is set
- token env defaults to `NOTION_TOKEN`
- non-dry-run push fails when token env is missing
- `--no-validate-notion-schema` disables schema validation
- `--notion-report` path behavior matches current command
- `--notion-generated-database-map` behavior matches current command

Use `Bukit.Shared.Notion` types directly. Do not call `NotionCommand.RunAsync` from `Bukit.Importing`.

- [ ] **Step 3: Implement `--verify` equivalent workflow**

Create `ImportVerifyWorkflow` with current behavior:

```csharp
var siteDir = string.IsNullOrWhiteSpace(result.SitePath)
    ? Path.Combine(rootDir, "sites", themeName)
    : result.SitePath;
var siteConfig = Path.Combine(siteDir, "site.yaml");
var resolved = ConfigPathResolver.Resolve(siteConfig, site: null);
var config = ConfigLoader.Load(resolved.FullConfigPath);
ConfigValidator.Validate(config);
var engine = new SiteEngine(new ConsoleLogger(LogLevel.Warn));
await engine.BuildAsync(config, resolved.RootDir, new ConfigOverrides { IsCI = true });
```

The service must map `ConfigException` to exit code 1, matching current `VerifyImportAsync`.

- [ ] **Step 4: Run orchestration tests**

Run:

```bash
dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj -c Release --filter "ImportThemeSelectionServiceTests|ImportNotionPushWorkflowTests|ImportVerifyWorkflowTests"
```

Expected: PASS.

- [ ] **Step 5: Commit task**

```bash
git add src/Bukit-Plugins/Bukit.Importing tests/Bukit.Importing.Tests
git commit -m "feat(importing): consolidate full import orchestration"
```

---

### Task 5: Add Full Import Workflow Service In Bukit.Importing

**Files:**
- Create: `src/Bukit-Plugins/Bukit.Importing/ImportCommandWorkflow.cs`
- Modify: `src/Bukit-Labs/Bukit.Labs.Cli/Commands/Import/ImportCommand.cs`
- Test: `tests/Bukit.Importing.Tests/ImportCommandWorkflowTests.cs`
- Test: `tests/Bukit.Labs.Cli.Tests/ImportCommandTests.cs`

- [ ] **Step 1: Implement `RunAsync` dispatcher**

Create `ImportCommandWorkflow`:

```csharp
namespace Bukit.Importing;

public static class ImportCommandWorkflow
{
    public static async Task<ImportCommandResult> RunAsync(ImportCommandOptions options)
    {
        return options.Subcommand switch
        {
            "html-demo" => await HtmlDemoAsync(options),
            "seed" => Seed(options),
            _ => new ImportCommandResult
            {
                ExitCode = 2,
                Messages =
                [
                    new ImportCommandMessage("error", $"未知的 import 子命令: {options.Subcommand}"),
                    new ImportCommandMessage("error", "可用: html-demo")
                ]
            }
        };
    }
}
```

- [ ] **Step 2: Implement html-demo flow with full current behavior**

Inside `HtmlDemoAsync`, preserve current command order:
1. validate `<demo-dir>`
2. validate `--theme`
3. validate content source and build source
4. resolve `--site-path`
5. resolve `--route-map`
6. validate `--push-notion` combinations
7. validate existing theme and `--force`
8. call `HtmlDemoImporter.Import`
9. run `--use` when requested and not dry-run
10. run `--push-notion` when requested
11. run `--verify` when requested

The `HtmlDemoImportOptions` mapping must set every current property:

```csharp
var importOptions = new HtmlDemoImportOptions
{
    InputPath = demoDir,
    ThemeName = options.ThemeName!,
    RootDir = rootDir,
    Force = options.Force,
    Use = options.Use,
    Verify = options.Verify,
    ExtractContent = options.ExtractContent,
    GenerateSeed = options.GenerateSeed,
    ContentSource = options.ContentSource,
    SitePath = sitePath,
    Language = options.Language,
    DryRun = options.DryRun,
    StrictMode = options.StrictMode,
    Overwrite = options.Overwrite,
    PreserveHtml = options.PreserveHtml,
    GenerateReport = options.GenerateReport,
    BaseUrl = options.BaseUrl,
    BuildSource = options.BuildSource.ToLowerInvariant(),
    RouteMapPath = routeMapPath,
    NotionDatabaseId = options.NotionDatabaseId,
    NotionDatabaseMap = options.NotionDatabaseMap,
    NotionTokenEnv = options.NotionTokenEnv
};
```

- [ ] **Step 3: Implement seed flow with exact output message**

`Seed(options)` must return message:

```text
seed import 完成: records=<records> written=<written> output=<outputDir>
```

This must match the current Labs command message.

- [ ] **Step 4: Convert Labs ImportCommand into a thin wrapper**

`src/Bukit-Labs/Bukit.Labs.Cli/Commands/Import/ImportCommand.cs` should only:
- bind `CliBoundCommand` into `ImportCommandOptions`
- call `ImportCommandWorkflow.RunAsync`
- print `Messages` to stdout/stderr based on level
- return `ExitCode`

This keeps Labs command behavior while making `Bukit.Importing` the single source of import behavior.

- [ ] **Step 5: Run workflow parity tests**

Run:

```bash
dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj -c Release --filter ImportCommandWorkflowTests
dotnet test tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj -c Release --filter ImportCommandTests
```

Expected: PASS.

- [ ] **Step 6: Commit task**

```bash
git add src/Bukit-Plugins/Bukit.Importing src/Bukit-Labs/Bukit.Labs.Cli/Commands/Import tests/Bukit.Importing.Tests tests/Bukit.Labs.Cli.Tests
git commit -m "feat(importing): make import workflow the compatibility source"
```

---

### Task 6: Update Architecture Policy For Full Importing Orchestration

**Files:**
- Modify: `tests/Bukit.Architecture.Tests/PluginBoundaryTests.cs`
- Modify: architecture documentation if the test currently documents Importing as domain-only.

- [ ] **Step 1: Update boundary assertion**

Change the plugin-domain boundary rule from “`Bukit.Importing` must not reference Core runtime” to:

```text
Bukit.Importing may reference Bukit.Cli.Shared, Bukit.Config, Bukit.Engine, Bukit.Engine.Abstractions, and Bukit.Shared because it is the full official import workflow implementation.
Bukit.Importing still must not reference Bukit.Plugin.Abstractions, Bukit.PluginHost, Bukit.Plugin.Import, Bukit.Labs.Cli, or other official plugin implementations.
```

`Bukit.Plugin.Import` remains strict:

```text
Bukit.Plugin.Import may reference Bukit.Plugin.Abstractions and Bukit.Importing.
Bukit.Plugin.Import must not reference Bukit.Cli, Bukit.Cli.Shared, Bukit.Config, Bukit.Engine, Bukit.Engine.Abstractions, Bukit.PluginHost, or Bukit.Labs.Cli directly.
```

- [ ] **Step 2: Run architecture tests**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --filter PluginBoundaryTests
```

Expected: PASS.

- [ ] **Step 3: Commit task**

```bash
git add tests/Bukit.Architecture.Tests docs
git commit -m "test(importing): allow full import workflow dependencies"
```

---

### Task 7: Implement Full Plugin Manifest And Options Mapper

**Files:**
- Create: `src/Bukit-Plugins/Bukit.Plugin.Import/ImportPluginCommandSpecs.cs`
- Create: `src/Bukit-Plugins/Bukit.Plugin.Import/ImportPluginOptionsMapper.cs`
- Create: `src/Bukit-Plugins/Bukit.Plugin.Import/ImportPluginPathGuard.cs`
- Modify: `src/Bukit-Plugins/Bukit.Plugin.Import/ImportPluginManifestProvider.cs`
- Modify: `src/Bukit-Plugins/Bukit.Plugin.Import/examples/minimal/plugins/import/plugin.yaml`
- Modify: `src/Bukit-Plugins/Bukit.Plugin.Import/examples/minimal/.bukit/plugins.yaml`

- [ ] **Step 1: Implement command specs with all current options**

`ImportPluginCommandSpecs.CreateCommands()` must declare every option in the compatibility contract. Required flags:
- `html-demo` argument `demo-dir` required
- `--theme` required
- `seed` argument `seed-dir` required
- `--output` required

`RequiredPermissions` must declare:

```csharp
new PluginPermissionSet(
    FileSystem: new PluginFileSystemPermission(
        Read: ["."],
        Write:
        [
            "./themes",
            "./sites",
            "./content",
            "./data",
            "./docs/research",
            ".bukit/reports/plugin-output/import"
        ]),
    Network: true,
    Environment: new PluginEnvironmentPermission(Read: ["NOTION_TOKEN"]))
```

Reason: plugin protocol v1 permissions are plugin-wide, not per-command. Full `--push-notion` support requires network and default token env.

- [ ] **Step 2: Update static `plugin.yaml` to match runtime manifest**

Update `src/Bukit-Plugins/Bukit.Plugin.Import/examples/minimal/plugins/import/plugin.yaml` with the same command tree and options. Static manifest and runtime manifest must match because `PluginCommandManifestValidator` enforces subset behavior.

- [ ] **Step 3: Update example host config permissions**

Update `.bukit/plugins.yaml`:

```yaml
permissions:
  fileSystem:
    read:
      - .
    write:
      - ./themes
      - ./sites
      - ./content
      - ./data
      - ./docs/research
      - .bukit/reports/plugin-output/import
  network: true
  environment:
    read:
      - NOTION_TOKEN
```

When users invoke `--notion-token-env CUSTOM_TOKEN`, they must add `CUSTOM_TOKEN` to `environment.read`. The plugin must fail with exit code 2 when the requested env name is not granted or empty.

- [ ] **Step 4: Implement mapper**

`ImportPluginOptionsMapper` must:
- map `Command.Path=["import","html-demo"]` to `Subcommand="html-demo"`
- map `Command.Path=["import","seed"]` to `Subcommand="seed"`
- preserve all string options exactly
- map absent reverse flags to current defaults
- convert `--strict warn` to `StrictMode="warn"` and any other present value to `StrictMode="fail"`
- reject unsupported command path with exit code 2
- check `request.Permissions.Environment.Read` contains `options.NotionTokenEnv` when `PushNotion=true`
- resolve and guard paths without allowing root escape

- [ ] **Step 5: Run compatibility tests**

Run:

```bash
dotnet test tests/Bukit.Plugin.Import.Tests/Bukit.Plugin.Import.Tests.csproj -c Release --filter ImportPluginCommandCompatibilityTests
bash scripts/checks/official-plugin-packages.sh
```

Expected: PASS.

- [ ] **Step 6: Commit task**

```bash
git add src/Bukit-Plugins/Bukit.Plugin.Import tests/Bukit.Plugin.Import.Tests
git commit -m "feat(import-plugin): declare full import command surface"
```

---

### Task 8: Connect Plugin Invoke To Full Bukit.Importing Workflow

**Files:**
- Modify: `src/Bukit-Plugins/Bukit.Plugin.Import/Bukit.Plugin.Import.csproj`
- Modify: `src/Bukit-Plugins/Bukit.Plugin.Import/ImportPluginApp.cs`
- Modify: `src/Bukit-Plugins/Bukit.Plugin.Import/ImportPluginInvoker.cs`
- Create: `src/Bukit-Plugins/Bukit.Plugin.Import/ImportPluginConsoleCapture.cs`
- Create: `src/Bukit-Plugins/Bukit.Plugin.Import/ImportPluginResponseMapper.cs`
- Test: `tests/Bukit.Plugin.Import.Tests/ImportPluginInvokeCompatibilityTests.cs`

- [ ] **Step 1: Add project reference**

Modify `Bukit.Plugin.Import.csproj`:

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\Bukit-Core\Bukit.Plugin.Abstractions\Bukit.Plugin.Abstractions.csproj" />
    <ProjectReference Include="..\Bukit.Importing\Bukit.Importing.csproj" />
  </ItemGroup>
```

Do not add direct references to Config, Engine, Labs, Cli.Shared, or PluginHost.

- [ ] **Step 2: Implement invoke path**

`ImportPluginInvoker.Invoke(PluginInvokeRequest request)` must:
1. map request to `ImportCommandOptions`
2. capture all `Console.Out` during workflow execution
3. call `await ImportCommandWorkflow.RunAsync(options)`
4. map result to `PluginInvokeResponse`
5. return `ExitCode` unchanged

- [ ] **Step 3: Map output without changing behavior**

`ImportPluginResponseMapper` must:
- convert info messages to `PluginMessage("info", ...)`
- convert warning messages to `PluginMessage("warning", ...)`
- convert error messages to `PluginDiagnostic(..., "error", ...)`
- include project-relative artifacts for generated themes, sites, reports, seed files, markdown files, notion reports, and generated database maps
- never return absolute artifact paths

- [ ] **Step 4: Add invoke parity tests**

Create tests for:
- missing `html-demo` argument returns exit 2
- missing `--theme` returns exit 2
- `--push-notion --dry-run` returns exit 2 with current message
- `--create-missing-notion-databases` without parent page returns exit 2
- `seed` without `--output` returns exit 2
- successful `seed` writes the same markdown layout
- `html-demo --dry-run` returns success and does not write theme
- unsupported custom `--notion-token-env` without env grant returns exit 2

- [ ] **Step 5: Run plugin invoke tests**

Run:

```bash
dotnet test tests/Bukit.Plugin.Import.Tests/Bukit.Plugin.Import.Tests.csproj -c Release --filter ImportPluginInvokeCompatibilityTests
```

Expected: PASS.

- [ ] **Step 6: Commit task**

```bash
git add src/Bukit-Plugins/Bukit.Plugin.Import tests/Bukit.Plugin.Import.Tests
git commit -m "feat(import-plugin): invoke full importing workflow"
```

---

### Task 9: Add CLI/PluginHost End-To-End Coverage

**Files:**
- Modify: `tests/Bukit.Cli.Tests/PluginCliIntegrationTests.cs`
- Use fixture: `src/Bukit-Plugins/Bukit.Plugin.Import/examples/minimal/`

- [ ] **Step 1: Add manifest validation integration test**

Add a test that copies the official Import fixture and runs:

```bash
bukit plugin validate-manifest plugins/import
```

Expected:
- exit code 0
- stdout contains `Plugin manifest OK:`
- stderr empty

- [ ] **Step 2: Add command invocation tests**

Add CLI tests for:
- `bukit import seed <seed-dir> --output content --force`
- `bukit import html-demo <demo-dir> --theme demo --dry-run`
- `bukit import html-demo <demo-dir> --theme demo --push-notion --dry-run`

Expected:
- first two follow current command behavior
- third returns exit 2 with current incompatibility message
- plugin lock and execution report are written
- report masks env values

- [ ] **Step 3: Run CLI plugin tests**

Run:

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter Plugin
```

Expected: PASS.

- [ ] **Step 4: Commit task**

```bash
git add tests/Bukit.Cli.Tests src/Bukit-Plugins/Bukit.Plugin.Import/examples/minimal
git commit -m "test(import-plugin): verify full import plugin through cli"
```

---

### Task 10: Full Verification And Final Audit

**Files:**
- All files changed by Tasks 1-9.

- [ ] **Step 1: Build solutions**

Run:

```bash
dotnet build bukit-plugins.slnx -c Release
dotnet build bukit-test.slnx -c Release
```

Expected: PASS.

- [ ] **Step 2: Run targeted tests**

Run:

```bash
dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj -c Release
dotnet test tests/Bukit.Plugin.Import.Tests/Bukit.Plugin.Import.Tests.csproj -c Release
dotnet test tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj -c Release --filter Import
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter Plugin
dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj -c Release
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release
bash scripts/checks/official-plugin-packages.sh
```

Expected: PASS.

- [ ] **Step 3: Run repository gate**

Run:

```bash
bash scripts/gates/ci-fast.sh Release
```

Expected: PASS.

If release packaging or gate-owned files were changed, run:

```bash
bash scripts/gates/release.sh Release
```

Expected: PASS.

- [ ] **Step 4: Audit no split and no boundary violations**

Run:

```bash
find src/Bukit-Plugins -maxdepth 1 -type d | sort
rg -n "ProjectReference Include=.*Bukit\\.Labs|Bukit\\.Labs\\.Cli|Bukit\\.PluginHost" src/Bukit-Plugins/Bukit.Plugin.Import src/Bukit-Plugins/Bukit.Importing
rg -n "Bukit\\.Plugin\\.Abstractions" src/Bukit-Plugins/Bukit.Importing
rg -n "Bukit\\.Engine|Bukit\\.Config|Bukit\\.Cli\\.Shared" src/Bukit-Plugins/Bukit.Plugin.Import
```

Expected:
- no new `Bukit.Importing.*` project directory
- no Labs or PluginHost references in Importing or Plugin.Import
- no plugin abstractions reference in Importing
- no Engine/Config/Cli.Shared references in Plugin.Import

- [ ] **Step 5: Audit command parameter parity**

Run:

```bash
rg -n -- "--push-notion|--verify|--use|--notion-token-env|--no-validate-notion-schema|--create-missing-notion-databases" src/Bukit-Plugins/Bukit.Plugin.Import src/Bukit-Plugins/Bukit.Importing tests
```

Expected:
- all current parameters are present in plugin manifest, mapper, workflow tests, and compatibility tests.

- [ ] **Step 6: Audit stdout safety**

Run:

```bash
rg -n "Console\\.WriteLine|Console\\.Out|Console\\.SetOut" src/Bukit-Plugins/Bukit.Plugin.Import src/Bukit-Plugins/Bukit.Importing
```

Expected:
- `Program.cs` writes only final protocol response to stdout.
- `ImportPluginConsoleCapture.cs` owns `Console.SetOut`.
- any remaining `Console.WriteLine` in `Bukit.Importing` is either removed or covered by capture tests.

- [ ] **Step 7: Commit final fixes**

If any audit fix was required:

```bash
git add .
git commit -m "chore(import-plugin): finish full compatibility audit"
```

If no fix was required, do not create an empty commit.

---

## Acceptance Criteria

- `Bukit.Plugin.Import` no longer returns `plugin.import.notImplemented`.
- `bukit import html-demo` through the plugin supports every current parameter listed in this plan.
- `bukit import seed` through the plugin supports every current parameter listed in this plan.
- `Bukit.Importing` remains one project under `src/Bukit-Plugins/Bukit.Importing/`.
- No new split `Bukit.Importing.*` project exists.
- Existing Labs import command remains behavior-compatible because it delegates to `Bukit.Importing`.
- `--push-notion`, `--verify`, and `--use` are implemented, not excluded.
- Plugin permissions account for network and default `NOTION_TOKEN`.
- Custom `--notion-token-env` requires explicit `environment.read` grant and fails clearly when not granted.
- Static `plugin.yaml` and runtime manifest expose the same complete command surface.
- Architecture tests encode the new intended boundary: full workflow dependencies allowed in `Bukit.Importing`, but not in `Bukit.Plugin.Import`.
- Repository gate passes.

## Self-Review

- Spec coverage: the revised plan explicitly enforces full functionality, no project split, unchanged command parameters, and full plugin-system integration.
- Placeholder scan: this plan contains no placeholder tasks; every task has concrete files, commands, and expected outcomes.
- Boundary consistency: the plan moves full import orchestration into the existing `Bukit.Importing` project and keeps plugin protocol concerns in `Bukit.Plugin.Import`.
- Compatibility consistency: all current `html-demo` and `seed` parameters are listed and locked by tests.
