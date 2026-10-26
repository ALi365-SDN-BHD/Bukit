## ADDED Requirements

### Requirement: Resize execution enters invocation-owned cleanup

The resize generator SHALL protect backend execution, output validation and publication with the existing cleanup `finally`. On exit it MUST attempt deletion of exactly the temporary image and temporary freshness paths created for that invocation, using the existing best-effort deletion policy.

#### Scenario: External resize times out after writing a partial image

- **WHEN** the real process runner reaches the unchanged ten-second resize timeout after the tool writes its temporary destination
- **THEN** the generator SHALL attempt temporary-file cleanup before returning through its existing warning path
- **AND** it SHALL leave source bytes unchanged and SHALL NOT publish, track or project the failed variant or freshness sidecar
- **AND** the existing timeout warning and process-tree termination semantics SHALL remain unchanged

#### Scenario: Caller cancels after a partial image exists

- **WHEN** the caller cancels a resize after its invocation-owned partial image exists
- **THEN** cleanup SHALL run before cancellation propagates with the same caller token
- **AND** no temporary image or temporary freshness file SHALL remain in the writable fixture before teardown
- **AND** no final variant or sidecar SHALL be published and the existing descendant late-write proof SHALL still pass

### Requirement: Preserve existing image and ownership contracts

The repair MUST preserve public API, successful image/freshness bytes, validation, atomic publication, quality/orientation/sizing, ownership checks, stale-managed cleanup, logger behavior and srcset/tracking semantics. It MUST NOT add wildcard cleanup of user or historical files or change the process runner, optimizer or separate WebP cleanup.

#### Scenario: Successful or rejected resize follows existing behavior

- **WHEN** a resize succeeds or output validation rejects its temporary image
- **THEN** existing byte, freshness, logger, tracking and projection assertions SHALL remain satisfied
- **AND** unowned existing image/sidecar bytes SHALL remain protected by the existing ownership policy

#### Scenario: Cleanup cannot delete an invocation-owned path

- **WHEN** deletion encounters an existing filesystem or permission failure
- **THEN** the existing best-effort `TryDelete` behavior SHALL remain unchanged, without expanding deletion scope or promising crash/historical cleanup
