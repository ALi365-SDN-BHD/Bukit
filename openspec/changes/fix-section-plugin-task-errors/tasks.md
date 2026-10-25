# Section plugin Task errors — implementation plan

Goal: observe all hook failures without changing synchronous rendering or public APIs.
Architecture: repair the shared hook boundary and reuse the existing renderer test setup.
Stack: C#, .NET 10, existing Scriban and xunit dependencies.

- [x] Inspect both hook paths, callers, existing failure policy and direct tests; generate mapped closure/classification before editing.
- [x] Add direct-throw/faulted/canceled regressions for both hooks, validation modes and section entry points; retain expected RED (66 passed / 28 failed / 0 skipped).
- [x] Always observe hook Tasks and apply AfterRender replacement only on success.
- [x] Run the complete three-class Rendering specialty fixed in the external scope.json; reconcile TRX, freeze input identities and cache GREEN (94 passed / 0 failed / 0 skipped).
- [x] Complete one specialty review, the delta-only final closure and metrics summary; leave changes uncommitted.

Exact command: `dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release --filter "FullyQualifiedName~Bukit.Rendering.Tests.SectionRenderExtendedTests|FullyQualifiedName~Bukit.Rendering.Tests.RenderSectionTests|FullyQualifiedName~Bukit.Rendering.Tests.ScribanTemplateRendererTests" --logger "trx;LogFileName=section-task.trx" --results-directory /private/var/folders/f6/1xdrtq9j7030l4ww_sgnr0pw0000gn/T/opencode/section-task-39jza7g5/results`.
