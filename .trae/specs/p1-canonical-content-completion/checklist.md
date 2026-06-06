# P1 Canonical Content Completion Checklist

- [x] Red -> Green -> Refactor was followed for every `.cs` logic change.
- [x] No production `.cs` change was made before a relevant failing test.
- [x] No TODO, FIXME, or HACK comments were added to production code.
- [x] No new side-effecting service class was added without an `I*` interface
      and constructor injection.
- [x] Changed `.cs` files remain at or below 600 lines.
- [x] Dependencies still follow the Bukit project dependency matrix.
- [x] Direct `Meta` reads are limited to provider input, legacy compatibility,
      or documented fallback semantics.
- [x] User-visible behavior changes have matching guide updates in all required
      languages.
- [x] Maintainer contract changes have matching `guide/dev/` updates.
- [ ] Final verification commands were run and their output was reviewed.
