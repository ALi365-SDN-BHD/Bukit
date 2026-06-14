# Documentation Governance

Core docs must follow the current source contract. When source and docs
disagree, docs are wrong until proven otherwise.

## Source of Truth

| Topic | Source |
|---|---|
| Commands and options | `src/Bukit.Cli/Cli/BukitCliSpecs.cs` |
| Command handlers | `src/Bukit.Cli/Cli/BukitCliDescriptors.cs` |
| Config fields | `src/Bukit.Config/AppConfig.cs` |
| Strict field validation | `src/Bukit.Config/ConfigStrictFieldValidator.cs` |
| Generated schema | `src/Bukit.Config/ConfigJsonSchemaGenerator.cs` |
| Built-in plugins | `src/Bukit.Engine/Plugins/PluginRegistry.cs` |
| Theme manifest | `src/Bukit.Theme/ThemeManifestLoader.cs` |
| Boundary tests | `tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs` |

## Update Order

1. Change source contract.
2. Update or add tests.
3. Update guide/skills.
4. Update guide/dev.
5. Move experimental or historical material to Labs or Archive.
6. Run focused tests and doc checks.

## Core Docs Rules

- Only document Core commands that exist in `BukitCliSpecs.cs`.
- Only document `site.yaml` fields allowed by strict validation.
- Describe `dev` as LiveReload or browser reload.
- Describe plugins as built-in runtime unless explicitly writing Labs docs.
- Keep Labs and Archive opt-in.
- Do not keep compatibility language for removed fields or commands.

## Labs and Archive

Use `guide/labs/` when a workflow may return as an explicit experimental path.
Use `guide/archive/` when material is retired, historical, or no longer
buildable.

The Core guide may link to Labs/Archive only as boundary context. It must not
make those workflows part of the default maintainer path.

