# notion-compatibility-migration Specification

## Purpose
Consolidate canonical Notion content ownership without legacy compatibility shells while preserving transport, cache and projection behavior and the explicitly approved 2.0 CLR binary break.

The original migration's verification and installation milestones below are historical; accepted current joint proof is recorded separately in the external `joint-migration-validation-3f819sjx/joint-acceptance.json`. Closeout does not require delivery, publication or acceptance work for unknown consumers. The approved consumer binding and recompilation compatibility facts remain unchanged.

## Requirements

### Requirement: Consolidated canonical content ownership

The implementation SHALL move the 21 current canonical C# files to
Bukit.Content/Notion, remove the old runtime project/DLL mapping and five
compatibility shells, and retain the existing test project. It MUST NOT add a
shell, type forwarder, equivalent public parser, dependency or framework.

#### Scenario: Approved CLR break
- **WHEN** a 2.0 consumer migrates from the removed parser or old source/options assembly
- **THEN** it MUST update references and reflection/serializer/assembly-qualified bindings and recompile
- **AND** source/options full names, moved metadata, source constructor and options' 31 public members SHALL remain unchanged
- **AND** the governed inventory SHALL be 425 types / 18 assemblies with the other 423 entries unchanged.

### Requirement: Preserve source and transport behavior

NotionContentSource SHALL implement IContentProvider and explicitly implement
the internal fallback provider without exposing a new public property. Runtime
HTTP ownership/security, retry/read-write semantics, cancellation/errors, cache
formats/atomicity, raw/body/field/date/schema contracts, projection and ordering
MUST remain unchanged, including current ILogger forwarding and diagnostics,
subject to the accepted typed permission classification in
`notion-relation-fallback-diagnostics`.

#### Scenario: Real source fallback after composition
- **WHEN** a configured non-taxonomy relation has valid, missing and duplicate remote targets
- **THEN** Composite SHALL discover the real source's fallback contract, fetch distinct targets through that source and preserve ordered projections and unresolved target IDs/null fields.
- **AND** direct or direct-inner canonical HTTP 403/404 failures SHALL emit `notion.relation.permission_denied` under `notion-relation-fallback-diagnostics`, while HTTP 401/500 retain generic `notion.relation.unresolved` behavior without message parsing or recursive unwrapping.

#### Scenario: Page-fetcher null warning
- **WHEN** the default page fetcher receives a null client
- **THEN** its existing try/catch SHALL preserve the ArgumentNullException warning through the supplied logger rather than dereference Transport first.

### Requirement: Bounded governance and verification

The change SHALL retain historical manifests and the original AD-03C5 byte
prefix, append dated section 8 for explicit approval, and document unknown
private/binary/reflection/serializer consumers without claiming a CLR SDK or a
one-to-one parser replacement. Only the finalized scoped owner/complete-class
commands SHALL be executed; final outcomes SHALL remain external.

#### Scenario: Independent compiled API proof
- **WHEN** current scoped projects have materialized all 18 governed DLLs
- **THEN** compiled capture SHALL structurally equal the independent oracle before canonical installation
- **AND** only parser deletion, the two owner moves, required source interfaces and the compiler-derived existing LoadRawAsync virtual-final flag SHALL differ.
