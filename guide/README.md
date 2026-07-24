# Bukit Core Guide

This guide is generated for the current Bukit Core source tree. A historical
`guide-0.2` snapshot informed its information architecture, but that snapshot
is not required to exist. The contracts below are taken from the live Core
projects under `src/Bukit-Core` and the tests under `tests`.

## Current Product Mode

Bukit Core 2.0 uses Route 2, a deterministic trusted-content publishing
compiler, as its product direction and Route 3, an internal stable engine, as
its current operating mode. Enterprise internal use has priority. External use
under the public license is self-directed and carries no public support, SLA,
compatibility, or release-cadence commitment.

Regular public binary releases are paused; an exceptional public release
requires explicit management approval. Labs and external plugins remain
outside Core release readiness.

See [Bukit Core Product Positioning](../docs/governance/bukit-core-product-positioning.md).

If present, `guide-0.1`, `guide-0.2`, `scripts-0.1`, and `scripts-0.2` are
historical backup/reference trees only. Do not create or synchronize them by
default. They may be maintained only when explicitly requested and must never
be used as official documentation, CI gates, release scripts, or runtime
behavior sources.

## Entry Points

| Area | Use |
|---|---|
| [User guide](user/README.md) | Build, configure, preview, audit, and deploy a Bukit site. |
| [Developer guide](dev/README.md) | Maintain Core modules, command contracts, validation, and release gates. |
| [Agent skills](skills/README.md) | Core-only instructions for coding agents. |
| [Labs](labs/README.md) | Explicitly non-Core or preview workflows. |
| [Archive](archive/README.md) | Historical material that is not a current contract. |

## Source Of Truth

- CLI commands: `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs`.
- CLI dispatch: `src/Bukit-Core/Bukit.Cli/Cli/BukitCliDescriptors.cs` and `Program.cs`.
- Config fields: `src/Bukit-Core/Bukit.Config/AppConfig.cs`.
- Config validation: `ConfigStrictFieldValidator`, `ConfigValidator`, `ProviderValidators`, and `CollectionsValidator`.
- Build flow: `SiteEngine`, `BuildPlanner`, `VariantBuildPipeline`, `RoutePipeline`, and `BuildReportPipeline`.
- Template model: `src/Bukit-Core/Bukit.Rendering/Models.cs`.
- Built-in plugins: `src/Bukit-Core/Bukit.Engine/Plugins/PluginRegistry.cs`.

## Boundary

Core documents must not present Labs, clone, import, intent, webhook, theme
marketplace, or remote theme workflows as stable Core behavior. Dynamic plugin
commands can exist when a project exposes them through plugin config, but they
are not part of the static Core command table.
