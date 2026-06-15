# Bukit Core 1.0 Developer Guide

This directory is the maintainer-facing guide for Bukit Core 1.0. It follows
the enforced source contract, not historical command surfaces.

Core command registry:

`build`, `doctor`, `config`, `preview`, `dev`, `clean`, `version`,
`completion`, `seo`, `geo`, `publish`, `deploy`.

Source anchors:

- `src/Bukit.Cli/Cli/BukitCliSpecs.cs`
- `src/Bukit.Cli/Cli/BukitCliDescriptors.cs`
- `tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs`
- `src/Bukit.Config/AppConfig.cs`
- `src/Bukit.Config/ConfigStrictFieldValidator.cs`
- `src/Bukit.Config/ConfigJsonSchemaGenerator.cs`
- `src/Bukit.Engine/Plugins/PluginRegistry.cs`

## Maintainer Path

1. Read [Architecture](./architecture.md) to understand the Core pipeline and
   module boundaries.
2. Use [CLI](./cli.md) when changing command registration, option binding, or
   command docs.
3. Use [Config](./config-site-yaml.md) before changing `site.yaml`, validation,
   schema generation, or examples.
4. Use [Content](./content.md), [Routing](./routing.md), and
   [Rendering](./rendering-scriban.md) for build behavior.
5. Use [Theme](./theme.md) and [Built-in Plugins](./built-in-plugins.md) for
   runtime extension points that are still part of Core.
6. Use [Testing](./testing.md), [Release](./release.md), and
   [Documentation Governance](./documentation-governance.md) before publishing
   docs or binaries.
7. Use [Agent Task Workflow](./agent-task-workflow.md) when Codex or other
   agents need task sequencing, sub-agent boundaries, verification order, or
   audit rules.

## Core Documents

| Topic | File |
|---|---|
| Architecture and boundaries | [architecture.md](./architecture.md) |
| CLI registry and options | [cli.md](./cli.md) |
| Strict `site.yaml` contract | [config-site-yaml.md](./config-site-yaml.md) |
| Content sources | [content.md](./content.md) |
| Routing and conflict detection | [routing.md](./routing.md) |
| Scriban rendering | [rendering-scriban.md](./rendering-scriban.md) |
| Theme runtime | [theme.md](./theme.md) |
| Built-in plugin runtime | [built-in-plugins.md](./built-in-plugins.md) |
| Engine outputs | [engine-outputs.md](./engine-outputs.md) |
| Incremental build | [incremental-build.md](./incremental-build.md) |
| Cache and clean behavior | [cache-clean.md](./cache-clean.md) |
| Publish and deploy | [publish-deploy.md](./publish-deploy.md) |
| LiveReload dev server | [dev-server.md](./dev-server.md) |
| Observability | [observability.md](./observability.md) |
| Testing | [testing.md](./testing.md) |
| Release process | [release.md](./release.md) |
| Native AOT release concerns | [aot.md](./aot.md) |
| Documentation governance | [documentation-governance.md](./documentation-governance.md) |
| Agent task execution rules | [agent-task-workflow.md](./agent-task-workflow.md) |
| Public preview scope | [public-preview-scope.md](./public-preview-scope.md) |

## Boundary Rule

Historical and experimental workflows are not part of the default Core
developer path. Drafts for those workflows live under `guide/labs/`; retired or
non-buildable material belongs under `guide/archive/`.
