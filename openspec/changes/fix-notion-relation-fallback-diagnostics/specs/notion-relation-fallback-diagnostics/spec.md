## ADDED Requirements

### Requirement: Classify direct canonical permission failures

The source-bounded relation fallback SHALL classify a canonical `NotionApiException` with typed HTTP status 403 or 404, directly or as the direct `ContentException.InnerException`, as `notion.relation.permission_denied` without changing the client wrapping contract.

#### Scenario: Real client wraps a forbidden or missing target
- **WHEN** a source-bound fallback request returns HTTP 403 or 404 and the real client wraps its canonical exception
- **THEN** Composite projection keeps the unresolved target ID/null fields and emits `notion.relation.permission_denied`

#### Scenario: Other HTTP failures retain generic behavior
- **WHEN** the real fallback request returns HTTP 401 or 500
- **THEN** the resolver retains its generic warning/unresolved behavior and emits `notion.relation.unresolved`, without message parsing or recursive unwrapping

### Requirement: Preserve cancellation and source/security boundaries

The fallback MUST propagate caller cancellation with the same token and preserve source-bound authorization, canonical URLs, ordered duplicate links, aliases, deduplicated requests, body-store disposal and safe generic diagnostics.

#### Scenario: Caller cancels an active fallback request
- **WHEN** the caller cancels after the real Source fallback's in-memory HTTP request starts
- **THEN** `OperationCanceledException` escapes with that caller token instead of becoming a failure diagnostic

#### Scenario: Projection receives a sensitive error response
- **WHEN** a missing-target error response contains body/token sentinels
- **THEN** the diagnostic exposes neither sentinel and all existing ordered projection and source-bound request assertions remain valid

### Requirement: Retain the accepted migration and verify only this Core task

This repair SHALL preserve the accepted assembly migration and byte-identical 425-type/18-assembly public baseline, and SHALL use only the approved three project rebuilds, four frozen specialties and parent scoped reviews for this eight-path delta.

#### Scenario: Recompile and hand off current proof
- **WHEN** the approved repair is frozen for rebuild and verification
- **THEN** its own build/test/cache evidence records current inputs and binaries, the source-only contracts remain review inputs, and predecessor evidence remains unchanged historical evidence rather than being relabelled current
