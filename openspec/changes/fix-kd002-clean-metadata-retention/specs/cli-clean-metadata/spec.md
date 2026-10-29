# CLI clean metadata retention

## Requirement: Preserve project-owned metadata

The clean command MUST preserve the project-root `.bukit` directory and its user-managed configuration, lock, unknown and nested files byte-for-byte. It SHALL continue cleaning an eligible selected output directory and the project `.cache`, while retaining current output path/marker/symlink checks and exit codes. Keeping generated metadata alongside user files is deliberate; selective metadata cleanup is not part of this repair.

### Scenario: Default or selected output

WHEN clean uses default output, `--dir`, `--config` or `--site`, THEN plugin configuration, locks and unknown `.bukit` files SHALL remain unchanged while the eligible output and `.cache` are removed. The same retention MUST hold through the real CLI entrypoint.

### Scenario: Metadata selected as output

WHEN the resolved clean output is project-root `.bukit` or its descendant (including case variants), THEN clean SHALL reject it with exit code 2 before deleting output, metadata or cache, even if that target has an output marker. Similarly named sibling output directories SHALL remain eligible under the ordinary safety checks.
