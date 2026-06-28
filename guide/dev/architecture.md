# Architecture

Bukit Core 1.0 is a static site build pipeline with a strict CLI and config
surface. The default Core path is:

```text
CLI command
  -> config path resolution
  -> ConfigLoader and strict validation
  -> content sources
  -> routing
  -> theme and Scriban rendering
  -> built-in derive-pages plugins
  -> assets, publish projections, and after-build plugins
  -> reports and metrics
```

## Source Boundaries

| Assembly | Responsibility |
|---|---|
| `Bukit.Cli` | Command registry, argument binding, command orchestration |
| `Bukit.Config` | `site.yaml` model, strict field validation, JSON Schema generation |
| `Bukit.Content` | Markdown and Notion source loading into `ContentDocument` |
| `Bukit.Routing` | URL, output path, and route security decisions |
| `Bukit.Rendering` | Scriban model binding and template rendering |
| `Bukit.Theme` | `theme.yaml` parsing and theme runtime metadata |
| `Bukit.Engine` | Build orchestration, incremental rendering, outputs, reports, plugins |
| `Bukit.Engine.Abstractions` | Built-in plugin contracts and cross-engine Core data types |
| `Bukit.Shared` | Logging, diagnostics, shared exceptions |

## Out-of-Core Layers

The following assemblies are not Core 1.0 layers and must not be referenced by
`Bukit.Cli` or loaded by `Bukit.Engine` in the default build path:

| Assembly or directory | Reason |
|---|---|
| `Bukit.PluginHost` | External process-plugin host infrastructure; not used by Core built-in plugins. |
| `Bukit.Plugin.Abstractions` | External process-plugin protocol DTOs; future SDK material, not a Core runtime dependency. |
| `Bukit.Importing` | Import seed and HTML demo workflows are Labs/future work. |
| `Bukit.Clone` | Clone/theme workflow is outside the Core 1.0 command surface. |
| `plugins/*` | Sample process plugins; external plugins must not compile against Core projects. |

If an external plugin integration is needed later, create a dedicated SDK package
with no `src/Bukit.*` Core project references. Do not expose Core internals as a
plugin SDK.

## Enforced Core Boundary

`tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs` is the boundary smoke
test for Core 1.0. It verifies that:

- the Core command registry matches the stable whitelist;
- removed command types are absent from the Core CLI assembly;
- Labs command implementations do not live under Core CLI namespaces;
- Core CLI does not reference out-of-core plugin or import projects;
- the Core solution includes only approved Core projects and tests;
- the plugin registry does not load non-built-in plugin sources;
- deploy is limited to `github-pages`;
- site-level remote theme source and site-level theme inheritance are absent
  from `ThemeConfig`;
- remote theme source tooling is absent from `Bukit.Engine`.

## Build Flow

1. `BukitCliDescriptors` maps registry specs to command handlers.
2. Commands resolve `site.yaml` by `--config` or `--site`.
3. `ConfigLoader` loads YAML into `AppConfig`.
4. `ConfigStrictFieldValidator` fails on unknown fields before build behavior
   can silently drift.
5. Content providers produce canonical `ContentDocument` instances.
6. Routing turns documents into `RouteInfo`.
7. Built-in derive-pages plugins create taxonomy, pagination, archive, alias,
   and data/index pages when configured.
8. `PageRenderDispatcher` renders page, list, and static HTML entries.
9. Asset and static pipelines copy output with output-root guards.
10. Publish projections write search, feeds, sitemap, robots, llms, and
    manifest artifacts.
11. Reports and metrics are written under the output directory.

## Design Invariants

- Unknown config fields fail fast.
- Core docs and skills must follow `BukitCliSpecs.cs`.
- Output paths are derived from URLs and validated before writes.
- Built-in plugins are the only Core plugin source.
- External plugins do not reference Core projects; a standalone SDK is future
  work.
- `dev` is a LiveReload browser-refresh workflow over the static build output.
- Labs and archive documents must be explicit opt-in material.
