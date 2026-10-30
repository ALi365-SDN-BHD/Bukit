# Template directory hash budget

## Requirement: Never reuse an incomplete content digest

Directory hashing MUST retain its default 10,000-file / 100-MiB content budgets. It SHALL return the normal deterministic digest only when all enumerated eligible file contents are included. Exceeding either budget, including an oversized first file, MUST signal an identifiable internal budget condition rather than return a path/length-only digest. A failed hash SHALL NOT be cached as a complete digest. Missing/empty-directory, complete under-budget and symlink-discovery semantics SHALL remain unchanged.

### Scenario: Same-length edit above a budget

WHEN an over-budget layout file changes contents without changing length or mtime, THEN no normal partial digest SHALL be available to justify reuse, both before and after that edit.

## Requirement: Continue the affected variant without incremental reuse

WHEN child, parent or user layout hashing exceeds a budget, THEN the manifest planner SHALL warn, disable that variant's incremental reuse and continue using its existing full-render path. Existing render entries SHALL be cleared, no incomplete template hash SHALL be stored, and concurrent render entries SHALL be null. Existing ownership and non-render manifest state SHALL be retained when the output root matches. Public/serialized schemas SHALL not change.

### Scenario: Real render equivalence and recovery

After same-length template edits, affected real page and list output bytes SHALL match rendering identical final inputs with incremental mode explicitly disabled. Complete under-budget inputs SHALL still reuse unchanged output, and subsequent eligible builds SHALL recover incremental reuse rather than inherit a poisoned partial digest.

### Scenario: Non-budget error or explicit nonincremental build

Ordinary I/O, permission and unrelated invalid-operation errors SHALL propagate unchanged, not masquerade as a budget downgrade. An explicit nonincremental build SHALL not hash layout directories merely to decide whether incremental reuse is safe.
