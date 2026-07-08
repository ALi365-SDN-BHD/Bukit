# Testing

Core tests should prove the enforced CLI/config/runtime boundary, not legacy
workflows.

## High-Signal Test Areas

| Area | Tests |
|---|---|
| Core command boundary | `tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs` |
| CLI option behavior | `tests/Bukit.Cli.Tests/` |
| Config loading and schema | `tests/Bukit.Config.Tests/` |
| Theme manifest runtime | `tests/Bukit.Theme.Tests/` |
| Routing and route safety | `tests/Bukit.Routing.Tests/`, engine route tests |
| Rendering and template linting | `tests/Bukit.Rendering.Tests/`, engine linter tests |
| Built-in plugins and reports | `tests/Bukit.Engine.Tests/` |

## Suggested Verification Chain

```bash
dotnet test tests/Bukit.Architecture.Tests
dotnet test tests/Bukit.Config.Tests
dotnet test tests/Bukit.Theme.Tests
dotnet test tests/Bukit.Cli.Tests
dotnet test tests/Bukit.Engine.Tests
dotnet build
```

Use narrower tests first when editing a focused area, then run the broader
chain before release.

## Documentation Contract Checks

Docs should be checked for:

- commands outside `BukitCliSpecs.cs`;
- legacy config fields in Core examples;
- theme-source or site-level inheritance claims in Core docs;
- plugin docs that imply non-built-in Core loading;
- stale development server terminology;
- broken links from `guide/dev/README.md`.

## Fixtures

When test fixtures fail because they still use old config or commands, update
fixtures to the strict Core contract. Do not add compatibility paths just to
keep old fixtures green.

