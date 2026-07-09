# Architecture

Bukit Core is split into focused projects under `src/Bukit-Core`.

| Project | Responsibility |
|---|---|
| `Bukit.Cli` | User command entry point, command binding, dev server, deploy provider. |
| `Bukit.Cli.Shared` | CLI metadata, parser, help renderer, config path resolver. |
| `Bukit.Config` | Strict YAML loading, defaults, schema generation, validation. |
| `Bukit.Content` | Markdown and Notion providers, body stores, media localization. |
| `Bukit.Engine.Abstractions` | Content, routing, and plugin models shared by runtime layers. |
| `Bukit.Engine` | Build orchestration, routing, rendering pipeline, plugins, reports. |
| `Bukit.Plugin.Abstractions` | External plugin config, manifest, protocol, runtime, security DTOs. |
| `Bukit.PluginHost` | Process plugin validation, protocol invocation, permissions, locking. |
| `Bukit.Rendering` | Scriban renderer and template models. |
| `Bukit.Routing` | Route generation and route path safety. |
| `Bukit.Shared` | Diagnostics, exceptions, URL/path helpers, Notion helpers. |
| `Bukit.Theme` | Theme manifest, components, sections, tokens, catalog, doctor helpers. |

## Build Flow

`Program` binds a command. `BuildCommand` loads config, applies CLI overrides,
and calls the `BuildAsync` method on `SiteEngine`. `SiteEngine` plans the build,
loads content, chooses single-language or multi-language flow, and delegates
each variant to `VariantBuildPipeline`.

Variant stages:

1. Bootstrap theme.
2. Build data modules.
3. Generate content and list routes.
4. Inject taxonomy terms.
5. Run derive-page plugins.
6. Build SEO models.
7. Render pages and list routes.
8. Sync assets and static files.
9. Run after-build plugins.
10. Write projections and reports.

## Boundary Rule

Core extension points inside `Bukit.Engine.Abstractions` are not the same as
the external process plugin protocol. Documentation must name those paths
separately.
