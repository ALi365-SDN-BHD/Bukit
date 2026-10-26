# Fix Notion relation fallback diagnostics

## Why

The source-bounded relation fallback catches canonical Notion 403/404 errors, but its real client wraps those errors in `ContentException`. The inherited mismatch reports `notion.relation.unresolved` instead of the existing `notion.relation.permission_denied` diagnostic.

## Approved scope

The user explicitly approved a separate Core repair and recompile after accepting the Notion compatibility migration. Recognize the canonical exception directly or as the direct inner exception of `ContentException` at the existing resolver catch. Cover real Source-to-Composite HTTP execution, generic failures and caller cancellation. Add only this change's four exact workflow mappings and their owner assertions.

## Impact and non-goals

- Preserve the accepted assembly merge and binary break; no compatibility shell or old DLL restoration.
- Keep public API baseline bytes unchanged: 425 types in 18 assemblies. No schema, project, client exception contract, transport, logger or retry changes.
- Recompile the Core CLI entry and the two affected test projects, then run the four frozen specialties: workflow owner, four complete Content.Notion classes, four complete Content classes and direct current API check.
- No website, external consumer source, cross-repository implementation, Notion writes, publication, commit or integration. Private binary-only consumers are unavailable for recompilation.
- The accepted migration's reports stay immutable and historical when affected sources/binaries change. This task uses its own eight-path pre-edit baseline and external evidence. No previous migration/full/release/solution gate replay.
