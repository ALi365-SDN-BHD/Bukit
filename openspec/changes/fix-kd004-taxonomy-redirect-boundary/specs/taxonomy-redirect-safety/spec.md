# Taxonomy redirect writer safety

## Requirement: Preflight the entire redirect write set

Within one `WriteRedirects` call, all nonblank aliases MUST be validated before any output directory/file creation, overwrite or generated-path addition. Rooted aliases, drive/colon-qualified names, backslash forms and dot/dot-dot segments SHALL be rejected, including dangerous aliases equal to their term slug. Final index paths MUST pass the existing output containment and symlink checks. Input rejection SHALL produce `ConfigException` with `BuildOutputUnsafe`, preserving any inner path error.

### Scenario: Unsafe alias appears after valid work

WHEN a later alias or a later term is unsafe, THEN all previously existing bytes and the supplied generated-path collection SHALL remain unchanged and no new directory/file SHALL be created by this writer call.

### Scenario: Output path escapes through a prefix or existing link

WHEN the final index path escapes output containment lexically or through an existing directory/file symlink, THEN the writer SHALL reject the entire call before writing and SHALL leave external target bytes unchanged.

## Requirement: Preserve ordinary redirect behavior

Safe aliases, including forward-slash hierarchy and Unicode, SHALL retain current output locations and escaped redirect HTML/UTF-8 encoding. Blank and valid self aliases SHALL be skipped. Configured route prefixes and normalized generated-path tracking SHALL retain current behavior. No production aliases-ingestion route, public API, schema, dependency or shared path-policy change is introduced.
