# Labs Theme YAML context

## Purpose

Make the existing Labs theme YAML graphs statically deserializable through an explicitly refreshed, checked-in generated context while preserving public models and Theme ownership.

The independent repair's approval, verification procedure and 426-type/19-assembly evidence are historical. Current accepted governance is 425 types / 18 assemblies after the separately approved Notion CLR migration and later public-member update; this closeout neither recaptures the API nor asserts private, binary or reflection consumers were tested or recompiled.

## Requirements

### Requirement: Existing Labs YAML graphs are statically deserializable

Labs SHALL provide the generated factory, type resolver and type inspector for the six existing `ThemeYamlStaticContext` registrations without replacing static deserialization or changing the public models.

#### Scenario: Theme manifest metadata loads
- WHEN a valid manifest contains scalar metadata, tags and declared parameters
- THEN `ThemeManifest.Load` returns those values and the existing declared parameter count
- AND unknown properties are ignored, malformed YAML returns null and a missing manifest returns null.

#### Scenario: Registry graph loads
- WHEN valid registry YAML contains metadata, entries, tags, parameters and downloads
- THEN `RegistryIndex.Parse` returns all nested values with date/version scalars preserved as strings
- AND unknown properties are ignored and malformed YAML returns null.

### Requirement: Ordinary builds consume the generated snapshot

Ordinary Labs builds SHALL compile a checked-in normalized context without running the static generator. Only `BukitGenerateLabsYamlStaticContext=true` SHALL exclude that source and enable the private centrally pinned 16.3.0 generator. That Labs flag SHALL NOT enable Core generation.

#### Scenario: Refresh is explicit and reproducible
- WHEN the authorized generation build runs
- THEN real generator output is normalized by the unchanged house helper with accurate Labs input provenance
- AND the ordinary build compiles the snapshot without emitting `YamlDotNetAutoGraph`.

### Requirement: Repair remains independently scoped

This requirement records the finished independent repair's implementation and evidence boundary. Its preservation and isolation clauses apply at that repair step; they do not undo later accepted logger or Section changes, restore an older API baseline, or authorize runtime verification for this documentation closeout.

The repair SHALL preserve the existing Theme relocation and Core source/projects/public API baseline, add only the exact repair-document workflow mapping and owner regression, and use only authorized scoped verification. Parent SHALL own specialty and final review; logger and Section work SHALL remain untouched.

#### Scenario: Independent repair preserves its boundary
- **WHEN** the independent Labs YAML repair is prepared for parent review
- **THEN** it preserves the existing Theme relocation and Core source/projects/public API baseline, with only its exact workflow mapping and owner regression and authorized scoped verification, leaving logger and Section work untouched.

### Requirement: Engine source-contract evidence is composed explicitly

This is the finished repair's historical verification and evidence-composition procedure, not a new instruction to rerun those runtime suites during document closeout. Its scenario describes that repair handoff; unchanged historical runtime evidence is not a current cache hit.

The repair SHALL verify the complete `MetaRemovalInventoryTests` class because its repository-text scan consumes the changed Labs model tests and new generated context. This authorization SHALL NOT permit a full Engine suite or Core fixture rerun. Parent SHALL compose the fresh class result with unchanged historical Engine runtime/fixture evidence and SHALL NOT describe the old full Engine command as a current cache hit.

#### Scenario: The independent repair reaches the source inventory
- WHEN the final repair source and contract edits have stopped
- THEN complete Labs, complete Architecture, workflow owner, the exact complete source-inventory class and direct API check run serially with their scoped evidence
- AND the policy entry names that exact class consumer/command, without changing unrelated Engine mappings or Core implementation.
