# Bukit / BukitJalil Module Call Graph

This document visualizes module dependency and call relationships, supplementing the architecture docs with a "diagram-first" overview.

## Solution and Project Boundaries

| Solution | Contains |
|---|---|
| `bukit.slnx` | Bukit mainline projects and tests |

## Top-Level Dependency Graph

```text
Bukit.Cli
  └─ Bukit.Engine
      ├─ Bukit.Config
      ├─ Bukit.Content
      ├─ Bukit.Rendering
      ├─ Bukit.Routing
      ├─ Bukit.Shared
      └─ Bukit.Engine.Abstractions
```

## Build Pipeline Call Sequence

```text
Program.Main
  └─ BuildCommand.RunAsync
      ├─ ConfigPathResolver.Resolve
      ├─ ConfigLoader.Load
      ├─ ConfigValidator.Validate
      ├─ ConfigApplier.Apply (CLI overrides)
      └─ SiteEngine.BuildAsync
          ├─ ContentProviderFactory.Create → LoadAsync
          ├─ I18nOutputMerger.GetLanguages
          └─ BuildVariantAsync (per language)
              ├─ RouteGenerator.Generate
              ├─ TaxonomyTermsInjector
              ├─ DataModuleBuilder
              ├─ PluginRunner.RunDerivePages
              ├─ PageRenderDispatcher
              │   └─ ITemplateRenderer
              └─ PluginRunner.RunAfterBuild
          ├─ I18nOutputMerger.GenerateRootOutputs
          └─ MetricsWriter
```

## Plugin Loading Graph

```text
PluginRegistry.GetAllPlugins
  ├─ BuiltInPluginSource (bundled)
  ├─ GeneratedPluginSource (compile-time)
  ├─ ExternalPluginSource (runtime DLL, Non-AOT only)
  └─ ExternalProtocolPluginSource (process/wasm)
      └─ ProtocolPluginRunner
```

## Content Provider Graph

```text
ContentProviderFactory.Create
  ├─ provider=markdown → MarkdownFolderProvider
  ├─ provider=notion   → NotionContentProvider
  │   ├─ NotionApiClient (query database, fetch blocks)
  │   └─ NotionBlockRenderer (render blocks → HTML)
  └─ provider=sources  → CompositeContentProvider
      └─ Concurrent loading of sub-sources
```

## Data Model Flow

```text
Markdown/Notion/sources
  → ContentDocument (Id, Title, Slug, PublishAt, Record, Fields, ContentHtml, BodyKey)
    → RouteGenerator.Generate
      → RouteInfo (url, outputPath, template)
        → PageRenderDispatcher
          → ITemplateRenderer.Render(string template, object model)
            → ScribanTemplateEngine.RenderAsync
              → HTML output
```

## Key Data Structures Cross-Reference

| Structure | Defined In | Used By |
|---|---|---|
| `ContentDocument` | `Bukit.Engine.Abstractions` | Content, Routing, Rendering, Plugins |
| `ContentField` | `Bukit.Engine.Abstractions` | Content, Rendering |
| `RouteInfo` | `Bukit.Engine.Abstractions` | Routing, Rendering, Plugins |
| `BuildContext` | `Bukit.Engine.Abstractions` | Plugins |
| `SiteModel` | `Bukit.Rendering` | Rendering |
| `PageModel` | `Bukit.Rendering` | Rendering |
| `ListPageModel` | `Bukit.Rendering` | Rendering |
| `AppConfig` | `Bukit.Config` | Config, Engine |
| `BuildManifest` | `Bukit.Engine` | Engine (Incremental) |
| `BuildVariantContext` | `Bukit.Engine` | Engine |
| `BuildVariantResult` | `Bukit.Engine` | Engine |
| `PluginExecutionContext` | `Bukit.Engine` | Engine (Plugins) |

## Repository Boundary Notes

The current repository focuses on the `Bukit` mainline. Not included: [BukitJalil](https://github.com/ALi365-SDN-BHD/BukitJalil) related source and solutions.
