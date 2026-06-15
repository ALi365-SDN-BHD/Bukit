# Release

Release work must keep binaries, tests, skills, schemas, and guide docs aligned
with the Core 1.0 contract.

## Pre-Release Checks

Before any repository release tagging, complete: [Release Precheck Template](../release/release-prerelease-template.md).
For the final maintainer sequence, use [Release Checklist](release-checklist.md).

```bash
dotnet test tests/Bukit.Architecture.Tests
dotnet test tests/Bukit.Config.Tests
dotnet test tests/Bukit.Cli.Tests
dotnet test tests/Bukit.Engine.Tests
dotnet build
```

Run any repository release gate script only after checking that it still targets
the current script layout and Core command surface.

## Contract Surfaces

| Surface | Source |
|---|---|
| CLI commands | `src/Bukit.Cli/Cli/BukitCliSpecs.cs` |
| CLI handlers | `src/Bukit.Cli/Cli/BukitCliDescriptors.cs` |
| Config fields | `src/Bukit.Config/AppConfig.cs` |
| Strict config fields | `src/Bukit.Config/ConfigStrictFieldValidator.cs` |
| JSON Schema | `src/Bukit.Config/ConfigJsonSchemaGenerator.cs` |
| Built-in plugins | `src/Bukit.Engine/Plugins/PluginRegistry.cs` |
| Core boundary | `tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs` |
| Agent skills | `guide/skills/` |
| Developer guide | `guide/dev/` |

## Release Notes

Release notes should clearly state:

- Core command whitelist;
- strict `site.yaml` validation;
- built-in-only plugin runtime;
- GitHub Pages as the only Core deploy provider;
- LiveReload behavior for `dev`;
- Labs and archive material as opt-in or historical.

## Do Not Ship With

- Core docs that mention commands outside the registry as default workflows;
- Core config examples with unknown fields;
- plugin docs that imply non-built-in Core loading;
- theme docs that imply site-level remote source support;
- release workflow examples that confuse Bukit CLI release with user site
  deployment.
