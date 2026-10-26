# Template / Theme relocation

## Purpose

Keep template helpers and linting in Rendering, theme resolution and validation in Theme, and console presentation in Labs while retaining the relocated behavior.

The migration's API-location changes and measured 426-type/19-assembly acceptance are historical, not a current baseline or fresh test result. Current accepted governance is 425 types / 18 assemblies after the separately approved Notion CLR migration and later public-member update. Relocation intentionally changed CLR identities; unknown private, binary, reflection and serializer consumers are not claimed tested or recompiled. The verification and baseline-update clauses below describe the original migration step, not an API recapture or runtime gate for this document closeout.

## Requirements

### Requirement: Ownership follows the dependency layers

The implementation SHALL define template helpers/linting in Rendering and theme resolution/bootstrap/manifest validation in Theme, without new project references or an Engine/Primitives reference from Theme. Shared SHALL NOT define Scriban helpers, Config SHALL NOT implement theme.yaml validation, and Engine SHALL NOT retain relocated definitions or `TemplateRendererBase`.

#### Scenario: Reflection reports the intended owners
- WHEN the affected assemblies are built
- THEN public `Bukit.Rendering.ITemplateRenderer`, public linting/warning types in `Bukit.Rendering.Scriban`, and public Theme resolver/bootstrap/YAML validator types exist in their new assemblies
- AND helper types remain internal and former definitions are absent.

### Requirement: Behavior survives relocation

The implementation SHALL preserve lexical path validation, null handling, separators, platform case rules, diagnostics, YAML issue ordering, diagnosis and record/list semantics. It SHALL NOT replace the enforced-root algorithm with symlink-aware resolution.

#### Scenario: Custom paths remain bounded
- WHEN any of layouts/assets/static resolves outside the root, with or without a theme name
- THEN resolution throws the original `ConfigException` with `ConfigPathTraversal` and exact message
- AND root equality and nested paths remain valid.

### Requirement: Console presentation belongs to Labs

ThemeDoctor SHALL expose diagnosis but no report printer. Labs SHALL own an internal report printer with the former output body.

#### Scenario: Reports retain exact output
- WHEN Labs prints error/warning/clean results
- THEN headings, issue order, indentation, blank lines, colors and summary priority remain unchanged.

### Requirement: Approved public breaks are explicit

The migration SHALL document old/new API locations and private-consumer uncertainty, update the current governed baseline to the measured intentional surface, and preserve historical candidate/report evidence. Verification SHALL use only the approved scoped commands, never a full/solution/release gate.

#### Scenario: Relocation acceptance records intentional API moves
- **WHEN** the template/theme migration is accepted
- **THEN** its old/new API locations and private-consumer uncertainty are documented, its measured intentional surface becomes the governed baseline at that migration step, and historical candidate/report evidence is preserved using only approved scoped verification.
