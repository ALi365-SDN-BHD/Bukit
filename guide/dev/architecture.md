# Architecture

Bukit Core is split into focused projects under `src/Bukit-Core`.

| Project | Responsibility |
|---|---|
| `Bukit.Cli` | User command entry point, command binding, dev server, deploy provider. |
| `Bukit.Cli.Shared` | CLI metadata, parser, help renderer, config path resolver. |
| `Bukit.Config` | Strict YAML loading, defaults, schema generation, validation. |
| `Bukit.Content` | Content-source composition, Markdown provider, body stores and media localization. |
| `Bukit.Content.Notion` | Notion content adapter and source-to-content mapping. |
| `Bukit.Notion` | Notion transport, request lifetime, retries and source contracts. |
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
6. Collect render entries and preflight aggregate output ownership.
7. Build SEO models.
8. Render pages, list routes, and static templates.
9. Sync assets, static files, media, and generated theme tokens.
10. Run after-build plugins.
11. Execute document/aggregate projections, then let reports consume actual output locations.
12. Finish all variants, root aggregates, stale-owned cleanup, required reports/security validation and body-store disposal; only then persist manifests, marker and completed state.

## Configuration Ownership

`Bukit.Engine.Abstractions` does not reference `Bukit.Config`.
`BuildContext` carries content, routes, logger state, and plugin projections,
but not the effective `AppConfig`.

Configuration stays at the Engine/CLI composition boundary. Core production
builds bind it to an Engine-internal per-variant plugin execution session and
pass that session explicitly through derive, HTML-transform, and after-build
stages. Core built-in writers must not use `BuildContext.Data` as an ambient
configuration channel.

The public config-free `PluginRegistry` and `PluginRunner` facades are
compatibility entry points with deterministic defaults; site-aware production
execution uses the explicit effective configuration path.

The output preflight uses `AssetOutputPlan` before publication writes. It checks
render/static/assets/media/token/projection claims for exact and structural conflicts under
the destination filesystem's actual case semantics. The same destination
comparer is passed into incremental manifest tracking.

Default recursive publication discovery uses `SafeFileEnumerator`, which skips
directory symlinks and reparse points. Explicit symlink following remains a
separate capability of supported copy paths.

## Boundary Rule

Core extension points inside `Bukit.Engine.Abstractions` are not the same as
the external process plugin protocol. Documentation must name those paths
separately.

See [Core Safety And Reliability Invariants](core-safety-reliability-invariants.md)
for cleanup, DOM, ownership, symlink, cache, concurrency, and report boundaries.
The approved 2.0 CLR migration and its limits are recorded in the
[AD-01 final closure ledger](../../docs/analysis/bukit-core-ad01-config-decoupling-final-closure-2026-07-24.zh-CN.md).

## Public output and site delivery contracts

Document projections retain the complete validated route filename: `news/acme/index.html`
becomes `content/news/acme/index.html.json` and `.md`. BaseUrl and language prefixes
are applied to URLs consistently; reports consume actual document and aggregate output
paths. Source JSON relations remain directional; a site template may compute reverse
HTML navigation without rewriting those source relations. Scriban exposes the canonical
slug as `page.content_model.slug`, not a top-level `page.slug` identity.

The internal v3 build manifest tracks explicitly owned public outputs independently of
incremental rendering and detailed reports. Required deletion failure fails the build;
ordinary cleanup preserves untracked files. Legacy or invalid ownership triggers existing
marker-protected clean recovery; a nonempty unmarked directory requiring cleanup is
refused. Draft exclusion/deletion withdraws pages and projections; expiry/noindex excludes
content from indexing aggregates while retaining the document files.

Deployment readiness requires a supported completed build state and the existing privacy
checks. An audit report alone does not prove a failed output is deployable. Builds may
leave incomplete local files: Core does not provide whole-site atomic staging. A site's
separate immutable delivery candidate is a site workflow, not an added Core runtime
capability. Git push success is distinct from read-only verification of the hosted files.

SRBiz-specific relationship, artifact comparison and delivery requirements are recorded in
`openspec/changes/align-srbiz-publishing-contracts/`; site implementation does not modify
Core code or the external plugin protocol. VerifiedAt remains a site business field and
must not be silently mapped to Core reviewedAt or source edit timestamps.
