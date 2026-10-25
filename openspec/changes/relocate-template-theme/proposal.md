# Template and Theme ownership relocation

## Why

Template implementation belongs to Rendering and theme resolution/manifest validation belongs to Theme, rather than Shared, Config or Engine. This records the user-approved independent migration adapted to base `ad13645205e8c61220a92835eb6f7d4e813dc1dc` on 2026-10-25.

## Changes

- Move template helpers and linting to `Bukit.Rendering.Scriban`, keep helpers internal, and move public `ITemplateRenderer` to `Bukit.Rendering`.
- Delete unused `TemplateRendererBase` and its dedicated tests.
- Move theme resolution/bootstrap/name types to `Bukit.Theme`, including their tests.
- Move the complete theme.yaml validation entry and strict validator from Config to Theme.
- Rename `ThemeDoctorCommand` to `ThemeDoctor`; retain diagnosis and its nested mutable-list record, and move report printing unchanged to Labs CLI.
- Update direct consumers, architecture guards, workflow owner mappings, current API baseline and migration guidance.

## Compatibility and boundaries

These are approved breaking namespace/assembly/member changes, classified `cross-assembly-implementation / 2.0-review`. No type forwarding or compatibility facades are added. Private consumers remain unknown and must migrate/recompile. Expected governed baseline is 426 types in 19 assemblies, down from 428. Historical candidate manifests/reports remain historical.

No new project references, dependencies, logger implementation, Section changes, CI/release changes, commits or full/solution gates are authorized. Verification is the complete affected specialties and owner self-tests defined by the external command/closure evidence.
