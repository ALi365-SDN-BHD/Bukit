# Bukit Core Developer Guide

This directory documents the current Core implementation for maintainers. It is
not a historical plan and not a Labs guide.

Bukit Core currently follows Route 2 for technical direction and Route 3 for
internal-first operation. Maintenance decisions prioritize named internal
consumers, reliability, contract truth, and controlled simplification. See
[Bukit Core Product Positioning](../../docs/governance/bukit-core-product-positioning.md).

Regular public binary releases are paused and require explicit management
approval as an exception. Labs and external plugins remain outside Core release
readiness.

## Core Source Anchors

| Area | Source |
|---|---|
| Command registry | `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs` |
| Command dispatch | `BukitCliDescriptors.cs`, `Program.cs` |
| Config model | `Bukit.Config/AppConfig.cs` |
| Config validation | `ConfigStrictFieldValidator`, `ConfigValidator`, `ProviderValidators` |
| Build orchestration | `SiteEngine`, `BuildPlanner`, `VariantBuildPipeline` |
| Routing | `Bukit.Routing/RouteGenerator.cs`, `Engine/RoutePipeline.cs` |
| Rendering model | `Bukit.Rendering/Models.cs` |
| Built-in plugins | `Engine/Plugins/PluginRegistry.cs` |
| External process protocol | `Bukit.PluginHost`, `Bukit.Plugin.Abstractions` |

## Documents

| Topic | File |
|---|---|
| Product positioning | [Bukit Core Product Positioning](../../docs/governance/bukit-core-product-positioning.md) |
| Architecture | [architecture.md](architecture.md) |
| CLI contract | [cli.md](cli.md) |
| Config contract | [config-site-yaml.md](config-site-yaml.md) |
| Content pipeline | [content.md](content.md) |
| Routing | [routing.md](routing.md) |
| Rendering | [rendering-scriban.md](rendering-scriban.md) |
| Theme runtime | [theme.md](theme.md) |
| Built-in plugins | [built-in-plugins.md](built-in-plugins.md) |
| Plugin host boundary | [plugins.md](plugins.md) |
| Engine outputs | [engine-outputs.md](engine-outputs.md) |
| Incremental builds | [incremental-build.md](incremental-build.md) |
| Cache and clean | [cache-clean.md](cache-clean.md) |
| Dev server | [dev-server.md](dev-server.md) |
| Publish and deploy | [publish-deploy.md](publish-deploy.md) |
| Observability | [observability.md](observability.md) |
| Testing | [testing.md](testing.md) |
| Code quality governance | [code-quality-governance.md](code-quality-governance.md) |
| Release | [release.md](release.md) |
| Release checklist | [release-checklist.md](release-checklist.md) |
| Native AOT | [aot.md](aot.md) |
| Documentation governance | [documentation-governance.md](documentation-governance.md) |
| Agent workflow | [agent-task-workflow.md](agent-task-workflow.md) |
| Public preview boundary | [public-preview-scope.md](public-preview-scope.md) |
| Core safety and reliability invariants | [core-safety-reliability-invariants.md](core-safety-reliability-invariants.md) |
