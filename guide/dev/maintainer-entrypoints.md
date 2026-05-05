# Source Entry Points by Change Type

This document helps maintainers locate source entry points **by change type**.

## 1. Changing CLI / Parameters / Configuration

### Primary Entry Points
- `src/Bukit.Cli/Program.cs`
- `src/Bukit.Cli/Commands/BuildCommand.cs`
- `src/Bukit.Cli/ConfigPathResolver.cs`
- `src/Bukit.Config/ConfigLoader.cs`
- `src/Bukit.Config/ConfigValidator.cs`

| Need | Look Here First |
|---|---|
| New command/dispatch | `Program.cs` |
| build parameters | `BuildCommand.cs` |
| `--config`/`--site` rules | `ConfigPathResolver.cs` |
| YAML reading/assembly | `ConfigLoader.cs` |
| Field validity/errors | `ConfigValidator.cs` |
| CLI override priority | `ConfigOverrides.cs` |

### Verification
```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release
```

## 2. Changing Content Source Ingestion

### Primary Entry Points
- `src/Bukit.Engine/ContentProviderFactory.cs`
- `src/Bukit.Content/Markdown/MarkdownFolderProvider.cs`
- `src/Bukit.Content/Notion/NotionContentProvider.cs`
- `src/Bukit.Content/CompositeContentProvider.cs`

### Verification
```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release
```

## 3. Changing Routing / URL / Output Path

### Primary Entry Points
- `src/Bukit.Routing/RouteGenerator.cs`
- `src/Bukit.Engine/SiteEngine.cs`

### Verification
```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter RouteGenerator
```

## 4. Changing Rendering / Themes / Template Variables

### Primary Entry Points
- `src/Bukit.Engine/BuildPathUtils.cs`
- `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs`
- `src/Bukit.Rendering/Scriban/ScribanModelBinder.cs`
- `src/Bukit.Rendering/Scriban/FileTemplateLoader.cs`
- `src/Bukit.Engine/PageRenderDispatcher.cs`

### Verification
```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release
```

## 5. Changing Plugins / Output Artifacts

### Primary Entry Points
- `src/Bukit.Engine/Plugins/PluginRegistry.cs`
- `src/Bukit.Engine/Plugins/PluginRunner.cs`
- `src/Bukit.Engine.Abstractions/Plugins/BuildContext.cs`
- `src/Bukit.Engine/Plugins/BuiltIn/*`

### Verification
```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
```

## 6. Changing Incremental Builds / Render Skipping / Caching

### Primary Entry Points
- `src/Bukit.Engine/SiteEngine.cs`
- `src/Bukit.Engine/PageRenderDispatcher.cs`
- `src/Bukit.Engine/Incremental/BuildManifest.cs`
- `src/Bukit.Engine/Incremental/HashUtil.cs`

### Verification
```bash
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --no-clean --incremental
```

## Quick Decision Table

| What You Want to Change | First Stop |
|---|---|
| Commands or parameters | `Program.cs` / `BuildCommand.cs` |
| Config fields or validation | `ConfigLoader.cs` / `ConfigValidator.cs` |
| Markdown/Notion ingestion | `ContentProviderFactory.cs` / Markdown/Notion providers |
| Page URLs and output paths | `RouteGenerator.cs` |
| Template variables and rendering | `ScribanModelBinder.cs` / `ScribanTemplateRenderer.cs` |
| Page writing and list pages | `PageRenderDispatcher.cs` |
| Search, RSS, sitemap, taxonomy | `PluginRunner.cs` + `Plugins/BuiltIn/*` |
| Caching and render skipping | `SiteEngine.cs` / `PageRenderDispatcher.cs` |
