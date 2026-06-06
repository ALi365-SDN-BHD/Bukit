# P3 Publish Projections Completion Checklist

- [x] Red -> Green -> Refactor was followed for every `.cs` logic change.
- [x] No production `.cs` change was made before a relevant failing test.
- [x] No TODO, FIXME, or HACK comments were added to production code.
- [x] No new NuGet dependency was introduced.
- [x] New side-effecting projection writers expose an `I*` interface and use constructor injection or are pure projection descriptors.
- [x] Changed `.cs` files remain at or below 600 lines.
- [x] Dependencies still follow the Bukit project dependency matrix.
- [x] Existing output paths remain compatible.
- [x] User-visible behavior changes have matching guide updates in all required languages.
- [x] Maintainer contract changes have matching `guide/dev/` updates.
- [x] Final verification commands were run and their output was reviewed.
- [ ] Full quality gate passed. Current blocker: aggregate coverage is 69.25%,
      below the required 80% threshold.
