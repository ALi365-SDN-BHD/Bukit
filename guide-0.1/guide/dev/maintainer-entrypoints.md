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

## Testing and CI Entry Points

### Primary Scripts

| Script | Purpose |
|--------|---------|
| `scripts/test-all.sh Release` | Full pipeline: restore → build → test → quality gate → smoke → smoke-all → AOT publish |
| `scripts/quality-gate.sh Release` | Coverage threshold (65%), encoding checks, dotnet format |
| `scripts/smoke-all.sh Release` | Build all 7 examples + 9 fixture sites, validate outputs |
| `scripts/security-regression.sh Release` | Isolated security tests (Shared/Config/CLI/Engine/Content) |
| `scripts/stress-test.sh 20 Release` | Repeat full suite N times for stability verification |

### CI Verification

GitHub Actions (`ci.yml`):

```bash
# Quality gate
dotnet test bukit.slnx -c Release
bash scripts/quality-gate.sh Release

# Cross-platform (ubuntu/windows/macos)
dotnet test bukit.slnx -c Release

# Smoke
bash scripts/smoke.sh Release
bash scripts/smoke-all.sh Release

# AOT
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c Release -p:PublishAot=true

# Stress (manual trigger only)
bash scripts/stress-test.sh 20 Release
```

### Fixture Sites

10 fixture sites at `tests/fixtures/` for deterministic end-to-end validation. See `guide/dev/testing-smoke.md` for the full catalog.

### Test Protocol Plugin

`tests/ProtocolEchoPlugin/Program.cs` — deterministic external plugin with modes for derive-pages, after-build, handshake, environment reporting, and error testing. See `guide/dev/testing-smoke.md` for the mode catalog.
