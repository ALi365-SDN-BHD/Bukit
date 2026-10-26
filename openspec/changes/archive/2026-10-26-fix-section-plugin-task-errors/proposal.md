# Fix section plugin Task error handling

The user explicitly authorized this isolated Core bug fix after the read-only audit.
Observe failed/canceled section-hook Tasks even when they have already completed,
and preserve the existing plugin-failure diagnostic after an AfterRender failure.

Scope: SectionRenderHelper and its direct Rendering regressions. There is no
website/cross-repository implementation, public API, protocol, dependency, logging,
or asynchronous-renderer migration. No commit, push, or full/release gate is authorized.
