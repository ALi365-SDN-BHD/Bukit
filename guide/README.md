# Bukit Core Guide

This guide is generated for the current Bukit Core source tree. It uses the
`guide-0.2` directory only as an information architecture reference; the
contracts below are taken from the live Core projects under `src/Bukit-Core`
and the tests under `tests`.

`guide-0.1`, `guide-0.2`, `scripts-0.1`, and `scripts-0.2` are backup/reference
trees only. They must not be used as official documentation, CI gates, release
scripts, or runtime behavior sources.

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
