# Core Plugin Boundary

Bukit Core 1.0 has one plugin rule: Core loads built-in engine plugins only.
External process plugins are not part of the Core runtime, the Core CLI, or the
Core solution until a separate SDK is designed and published.

## Policy

- Core runtime plugin loading is limited to `BuiltInPluginSource` in
  `src/Bukit.Engine/Plugins/PluginRegistry.cs`.
- Core CLI must not reference `Bukit.PluginHost`, `Bukit.Plugin.Abstractions`,
  `Bukit.Importing`, sample process plugins, or plugin marketplace code.
- Core solution must not include process-plugin host projects, process-plugin
  protocol DTO projects, sample external plugins, or their tests.
- External plugins must not reference `src/Bukit.*` Core projects directly.
- External plugins must communicate with a future host only through a stable
  process protocol or a dedicated SDK package.
- The dedicated SDK is future work. Do not add SDK packages or SDK-shaped Core
  dependencies in Core 1.0.

## Core Layer Audit

| Layer | Core 1.0 status | Responsibility |
|---|---|---|
| `Bukit.Cli` | Keep | Stable command registry, argument binding, command orchestration. |
| `Bukit.Cli.Shared` | Keep | CLI metadata and reusable command-line helpers. |
| `Bukit.Config` | Keep | `site.yaml` model, strict validation, schema generation. |
| `Bukit.Content` | Keep | Markdown and Notion content loading into canonical documents. |
| `Bukit.Routing` | Keep | URL, route, and output path security decisions. |
| `Bukit.Rendering` | Keep | Scriban model binding and deterministic static rendering. |
| `Bukit.Theme` | Keep | Local filesystem theme metadata and runtime theme parsing. |
| `Bukit.Engine.Abstractions` | Keep | Built-in plugin contracts and cross-engine Core data types. |
| `Bukit.Engine` | Keep | Build orchestration, incremental rendering, outputs, reports, and built-in plugins. |
| `Bukit.Notion` | Keep | Core Notion content-provider support. Notion push remains out of Core. |
| `Bukit.Shared` | Keep | Diagnostics, logging, shared exceptions. |
| `Bukit.Clone` | Remove from Core solution | Clone/theme workflow is not part of Core 1.0. |
| `Bukit.Importing` | Remove from Core solution | Import seed and HTML demo workflows are not part of Core 1.0. |
| `Bukit.PluginHost` | Remove from Core solution | External process-plugin host machinery; not loaded by Core. |
| `Bukit.Plugin.Abstractions` | Remove from Core solution | Process-plugin protocol DTOs; future SDK material, not Core runtime. |
| `plugins/*` | Remove from Core solution | Sample external process plugins; must not be compiled as Core. |

## `Bukit.PluginHost` Analysis

`Bukit.PluginHost` is host-side external process plugin infrastructure. Its
current responsibilities are manifest-centric:

- load `plugin.yaml` from a plugin root;
- validate plugin id, protocol, kind, and distribution;
- read platform entries, command metadata, and requested permissions;
- reject unsupported protocol versions, non-process plugin kinds, and non
  self-contained distributions;
- validate unsafe permission declarations.

This is useful future infrastructure, but it is not required for Core 1.0
because Core does not load project-local plugin binaries or process hosts. It
must therefore stay outside the Core build path until an explicit external
plugin phase is accepted.

## `Bukit.Plugin.Abstractions` Analysis

`Bukit.Plugin.Abstractions` defines process-protocol DTOs and JSON source
serialization metadata:

- protocol constants such as `bukit-plugin-v1`, `handshake`, `manifest`, and
  `invoke`;
- request and response envelopes;
- manifest, command, argument, option, platform, and identity records;
- invoke command and invoke context records;
- permission records for filesystem, network, and environment access;
- diagnostic, message, artifact, and error records;
- source-generated JSON serialization context.

This assembly has no rendering or engine behavior. Architecturally it belongs to
a future plugin SDK or protocol package, not to Bukit Core. External plugins
must not use Core assemblies as their SDK. When the SDK is created later, it
must be a small standalone package with no `src/Bukit.*` Core project references.

## Allowed Plugin Model In Core 1.0

Core built-in plugins remain ordinary in-process engine extensions behind
`Bukit.Engine.Abstractions` and `PluginRegistry`. They are part of Core because
they implement deterministic build behavior such as taxonomy, pagination,
archives, menus, aliases, image processing, and related content.

External plugins are different. They are out-of-process integrations and must
not be treated as built-in engine extensions. Until the future SDK exists, they
are out of Core scope.

## Regression Guards

`tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs` must enforce that:

- the Core CLI command registry matches the stable whitelist;
- Core CLI has no out-of-core project references;
- Core solution includes only approved Core projects and approved Core tests;
- `PluginRegistry` does not load external protocol plugin sources.

Any change that reintroduces process plugin loading, plugin marketplace behavior,
or external plugin commands into Core must first update the public stability
scope and this boundary document.
