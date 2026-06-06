# Bukit Code Wiki

Repository structure, core architecture, module responsibilities, key classes and functions.

## Repository Structure

```text
Bukit
├─ src/
│  ├─ Bukit.Cli/                 # CLI entry, command dispatch
│  ├─ Bukit.Config/              # site.yaml parsing, defaults, validation
│  ├─ Bukit.Content/             # Markdown/Notion/multi-source loading
│  ├─ Bukit.Engine.Abstractions/ # ContentItem, RouteInfo, plugin contracts
│  ├─ Bukit.Engine/              # Build orchestration, incremental, plugins
│  ├─ Bukit.Rendering/           # Template models, Scriban binding
│  ├─ Bukit.Routing/             # Content-to-route mapping
│  ├─ Bukit.Shared/              # Logging, exceptions, infrastructure
│  └─ plugins/                   # Optional plugin implementations
├─ tests/
├─ examples/starter/
├─ guide/dev/                    # Developer docs
├─ guide/user/                   # User docs
├─ scripts/
├─ tools/scriban/
└─ docs/
```

## Solution: `bukit.slnx`

## Dependency Direction

```text
Bukit.Cli → Bukit.Engine → Bukit.Config + Content + Rendering + Routing + Shared + Abstractions
```

## Core Module Responsibilities

| Module | Key Entry Points |
|---|---|
| `Bukit.Cli` | `Program.cs`, `Commands/*`, `ConfigPathResolver.cs` |
| `Bukit.Config` | `AppConfig.cs`, `ConfigLoader.cs`, `ConfigValidator.cs` |
| `Bukit.Content` | `MarkdownFolderProvider.cs`, `NotionContentProvider.cs`, `CompositeContentProvider.cs` |
| `Bukit.Engine` | `SiteEngine.cs`, `PageRenderDispatcher.cs`, `RouteInventoryValidator.cs`, `Plugins/*` |
| `Bukit.Rendering` | `Models.cs`, `Scriban/*` |
| `Bukit.Routing` | `RouteGenerator.cs`, `RoutePathBuilder.cs` |
| `Bukit.Shared` | `Logger.cs`, exceptions, security utilities |

## Key Classes and Functions

### CLI/Config
- `Program` — CLI main entry
- `BuildCommand.RunAsync` — Build command entry
- `ConfigPathResolver.Resolve` — `--config`/`--site` resolution
- `ConfigLoader.Load` — YAML → AppConfig
- `ConfigValidator.Validate` — Full config validation

### Content
- `MarkdownFolderProvider.LoadAsync` — Scan Markdown, parse front matter
- `NotionContentProvider.LoadAsync` — Fetch Notion pages, render blocks
- `CompositeContentProvider.LoadAsync` — Concurrent multi-source aggregation
- `ContentProviderFactory.Create` — Provider selection

### Engine
- `SiteEngine.BuildAsync` — Main build entry
- `SiteEngine.BuildVariantAsync` — Single-language variant
- `PageRenderDispatcher.RenderPages` — Concurrent page rendering
- `DataModuleBuilder.BuildModules` — `mode=data` → `site.modules`
- `I18nOutputMerger.GenerateRootOutputs` — Merged i18n artifacts
- `MetricsWriter.WriteIfRequested` — Build metrics JSON

### Routing/Rendering/Plugins
- `RouteGenerator.Generate` — ContentItem → RouteInfo
- `RoutePathBuilder` — Shared URL/path normalization utilities (used by all plugins)
- `RouteInventoryValidator.ValidateContentRoutes` / `ValidateFinalRoutes` — Route conflict detection
- `ScribanModelBinder` — C# models → Scriban
- `ScribanTemplateRendererAdapter` — Renderer → engine interface
- `PluginRegistry.GetAllPlugins` — Assemble built-in/generated/external/protocol
- `PluginRunner.RunDerivePages` / `RunAfterBuild`

### Built-in Plugins
| Plugin | Type | Output |
|---|---|---|
| PagesIndex | derive-pages | `site.data.pages_by_id` |
| Pagination | derive-pages | Pagination pages |
| Archive | derive-pages | Archive pages |
| Taxonomy | derive+after | `/tags/`, `/categories/` |
| Sitemap | publish projection | `sitemap.xml` |
| RSS / Atom / JSON Feed | publish projection | `rss.xml`, `feed/atom.xml`, `feed/feed.json` |
| SearchIndex | publish projection | `search.json` |

## Key Dependencies
- `YamlDotNet` — YAML parsing
- `Microsoft.Extensions.Http` — HTTP calls
- `tools/scriban` — Vendored template engine
- `xunit` — Testing

## Local Run
```bash
dotnet build bukit.slnx -c Release
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c Release -- preview --dir examples/starter/dist --port auto
```

## Test and Smoke
```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
pwsh ./scripts/smoke.ps1
```

## AOT Publish
```bash
dotnet publish src/Bukit.Cli -c AOT -r linux-x64 -o out/bukit
```

## Recommended Reading Order
1. `README.md` → 2. `Program.cs` → 3. `BuildCommand.cs` → 4. `SiteEngine.cs` → 5. `RouteGenerator.cs` → 6. `maintainer-entrypoints.md`
