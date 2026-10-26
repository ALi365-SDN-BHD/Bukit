## ADDED Requirements

### Requirement: Internal responsibility extraction preserves plugin ownership

The migration SHALL retain the two existing internal plugin entries, identities, interfaces, registration order, namespace, and Engine assembly. The eleven responsibility types in ten helper files and supporting wrappers/contexts MUST remain non-public and MUST NOT own plugin registration.

#### Scenario: Discover built-in implementations after extraction
- **WHEN** the registry and architecture guards inspect the migrated assembly
- **THEN** the original entries remain the sole plugins for these responsibilities and all extracted types are internal implementation details
- **AND** the governed public baseline remains byte-identical with 426 types in 19 assemblies.

### Requirement: DataFiles retains parsing and safety contracts

The migration MUST preserve constructor validation/defaults/error details, the private opener seam, no-follow and verified stream ownership, strict decoding, cancellation, deterministic traversal, all ten limits and exception/disposal behavior. Projection MUST retain typed keys, graph/scalar types, comparers, order, YAML alias copies and first-document projection with validation of later documents.

#### Scenario: Reuse a plugin for mixed language/default inputs
- **WHEN** JSON and multi-document YAML with an alias are loaded across a configured language and default data
- **THEN** byte and projection budgets are shared across those inputs within one call
- **AND** the next call receives fresh budgets and produces the same exact graph consumed by templates and canonical hashing.

### Requirement: Images retain serialized, native, ownership and logger contracts

The migration MUST preserve execution/early-return order, cancellation, native validation and calibration, quality/dimensions, freshness compact UTF-8 bytes and property order, source/variant identities, HTML rewrite behavior, user-file protection, tracking tuples, atomic publication/sidecar order and cleanup nesting. Caller-owned ILogger MUST remain required through discovery and both external runner calls; no replacement/default logger is permitted.

#### Scenario: External resize succeeds
- **WHEN** the public plugin entry generates representative original-format variants
- **THEN** the exact freshness bytes, normalized srcset graph/order, and variant-plus-sidecar tracking tuples match the baseline contract.

#### Scenario: External resize exits nonzero
- **WHEN** a counted caller logger wraps a JSON Warn or Error sink
- **THEN** the original warning payload is counted once before filtering, never bypasses the sink through stdout, and no failed output is published or tracked.

#### Scenario: Inherited resize cleanup boundary
- **WHEN** external execution throws before the existing cleanup try/finally
- **THEN** this extraction retains that inherited behavior and records `I-IMAGE-TEMP` for a separately authorized repair rather than claiming complete temporary-file cleanup.

### Requirement: Verification remains scoped and reproducible

The writer MUST use the exact controller-scheduled complete specialties and direct API check described in design.md and the external commands handoff. Assertion RED MUST precede production extraction. GREEN evidence MUST fingerprint current HEAD, affected/build/read/fixture inputs and dynamic membership, exact commands, environment states only, and SDK/toolchain, with official cache record/check.

#### Scenario: Complete migration handoff
- **WHEN** the writer finishes the approved extraction
- **THEN** full scoped logs/TRX/API/cache evidence and self-review are handed to the parent in review_wait
- **AND** no item 1 work, commits/publication, excluded partial integration tests, historical replay, or full/release/whole-solution/aggregate gates have been performed.
