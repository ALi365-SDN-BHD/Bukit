# Native AOT

Bukit Core is designed to ship as a CLI binary. Native AOT work must verify:

- No reflection-only command registration assumptions.
- JSON source generation contexts cover plugin protocol DTOs.
- CLI help and errors work without JIT-only behavior.
- File and path operations are cross-platform.
- Release artifacts include the right RID, version, and smoke evidence.

The docs rebuild does not run AOT publishing by default. Native AOT validation
belongs to release tasks.
