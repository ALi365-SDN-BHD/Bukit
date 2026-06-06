# Compliance Hardening vNext

## Goal

Close the non-content-model compliance gaps that remain after the vNext content model migration:

- documented `.trae/specs` coverage for this structural change set
- `InternalsVisibleTo` restricted to test assemblies only
- `catch {}` blocks in `src` and `tests` replaced with explicit, observable or named best-effort handling
- dependency matrix reviewed and documented as enforceable architecture policy

## Scope

This spec covers repository governance and architecture hardening. It does not change Bukit's public content authoring model, generated site output, or SEO/publish report schemas beyond the work already covered by the vNext content specs.

## Requirements

1. A dedicated `.trae/specs/compliance-hardening-vnext/` spec suite must exist with `spec.md`, `tasks.md`, and `checklist.md`.
2. Production assemblies must not expose internals to other production assemblies through `InternalsVisibleTo`.
3. Architecture tests must reject production-to-production `InternalsVisibleTo` targets.
4. Empty `catch {}` blocks in production and test code must be replaced by explicit handling:
   - log or write a targeted warning when observable cleanup failure matters
   - intentionally ignore only narrow best-effort cleanup exceptions through a named helper
5. Dependency matrix status must be rechecked against current architecture tests and documented if any intentional exceptions remain.
6. The repository must still pass `dotnet build` and relevant tests after the cleanup.

## Non-Goals

- Rewriting all project references into a new package boundary.
- Removing intentional test-only `InternalsVisibleTo`.
- Changing public CLI command names or site output contracts.

## Verification

- `rg -n "InternalsVisibleTo" src tests -g '!bin' -g '!obj'`
- `rg -n "catch\\s*(\\([^)]*\\))?\\s*\\{\\s*\\}" src tests -g '*.cs' -g '!bin' -g '!obj'`
- `dotnet build bukit.slnx -p:UseSharedCompilation=false -m:1 --no-restore`
- `dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -p:UseSharedCompilation=false --no-restore --no-build`
- `dotnet test bukit.slnx -p:UseSharedCompilation=false -m:1 --no-restore --no-build`
