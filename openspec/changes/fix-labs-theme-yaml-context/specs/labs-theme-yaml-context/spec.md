# Labs Theme YAML context

## ADDED Requirements

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

The repair SHALL preserve the existing Theme relocation and Core source/projects/public API baseline, add only the exact repair-document workflow mapping and owner regression, and use only authorized scoped verification. Parent SHALL own specialty and final review; logger and Section work SHALL remain untouched.

### Requirement: Engine source-contract evidence is composed explicitly

The repair SHALL verify the complete `MetaRemovalInventoryTests` class because its repository-text scan consumes the changed Labs model tests and new generated context. This authorization SHALL NOT permit a full Engine suite or Core fixture rerun. Parent SHALL compose the fresh class result with unchanged historical Engine runtime/fixture evidence and SHALL NOT describe the old full Engine command as a current cache hit.

#### Scenario: The independent repair reaches the source inventory
- WHEN the final repair source and contract edits have stopped
- THEN complete Labs, complete Architecture, workflow owner, the exact complete source-inventory class and direct API check run serially with their scoped evidence
- AND the policy entry names that exact class consumer/command, without changing unrelated Engine mappings or Core implementation.
