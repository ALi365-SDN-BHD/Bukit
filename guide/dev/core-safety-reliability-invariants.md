# Core Safety And Reliability Invariants

This document records maintainer-facing invariants established by the F-01
through F-08 hardening work. It complements the user contract in
`guide/user/20-core-safety-reliability.md` and the dated audit artifacts under
`docs/analysis`.

## Invariant Matrix

| ID | Invariant | Primary implementation | Regression surface |
|---|---|---|---|
| F-01 | Every user-selected/configured output cleanup reaches one guarded cleaner. | `CleanCommand`, `BuildPlanner`, `OutputDirectoryCleaner` | `CleanCommandTests`, security regression |
| F-02 | Untrusted search title/snippet data never enters an HTML interpretation sink in the default UI. | `SearchIndexPlugin` | `SearchIndexPluginExtendedTests` |
| F-03 | Asset/render destinations have one preflight owner under actual output-filesystem identity. | `AssetOutputPlan`, `OutputDestinationIdentityComparer`, `BuildManifestTracker`, `VariantBuildPipeline` | `AssetPipelineTests`, `BuildManifestTests`, integration collision tests |
| F-04 | Default Core publication walkers do not descend through directory symlinks/reparse points. | `SafeFileEnumerator` and consuming content/engine paths | Markdown, static, media, manifest, reporter, tooling symlink tests |
| F-05 | Template decisions are no older than their manifest/template content snapshot. | `TemplateCapabilitiesResolver`, `TemplateStaticAnalysisService` | resolver, static-analysis, same-engine rebuild tests |
| F-06 | The configured search content cap reaches every Core search representation. | `SearchIndexBuilder`, `SearchIndexPlugin`, `I18nOutputMerger`, publish projection writers | search builder/plugin/i18n/config tests |
| F-07 | Configured media concurrency limits active localization calls, not only document transforms. | `ContentImageRewritePipeline`, `LocalizedContentBodyStore` | pipeline and body-store concurrency/cancellation tests |
| F-08 | Build health and public output inventory are derived from the current build. | `BuildDiagnosticLogger`, `BuildOutputInventory`, `SiteEngine`, `BuildReporter` | reporter and build integration tests |

## F-01 Cleanup Authority

`OutputDirectoryCleaner` is the authority for configured output deletion,
explicit `clean --dir`, normal build clean, and recovery clean. Callers must not
reintroduce raw recursive deletion for a user-selectable output path.

The cleaner checks the project boundary, root/home/project directories, every
`.git` path segment, reparse-point segments below root, and the output marker.
The marker is necessary for a non-empty directory but is not sufficient to
override the other safety checks.

## F-02 Search DOM Boundary

The generated default search UI may construct elements, text nodes, and
`<mark>` nodes. Content-derived title/snippet values and configured placeholder
text must remain text or encoded attribute data. Do not restore `innerHTML`,
`outerHTML`, `insertAdjacentHTML`, `document.write()`, or equivalent parsing for
dynamic results.

This invariant covers the Core default search UI. It is not a sanitizer or CSP
guarantee for user themes, custom scripts, or third-party plugin output.

## F-03 Output Ownership Preflight

The variant pipeline collects render entries and creates an `AssetOutputPlan`
before page rendering or asset copying. Claims include static, assets, media,
generated theme tokens, content/list render outputs, and rendered static
templates.

The plan preserves parent/site override only within the same category. Exact
cross-category collisions and file/descendant structural collisions fail with
`BuildAssetOutputCollision`. `OutputDestinationIdentityComparer` probes the
actual output filesystem and the same comparer must be passed to
`BuildManifestTracker`; OS-name heuristics are insufficient.

The identity probe briefly creates and removes a hidden probe under the writable
output root. It is not a publication target. Arbitrary after-build third-party
plugin outputs remain outside this ownership plan and must not be documented as
covered.

## F-04 Safe Recursive Enumeration

`SafeFileEnumerator` is the default helper for recursive publication discovery.
It skips the `ReparsePoint` flag from `FileAttributes` before descending and
does not silently ignore inaccessible ordinary paths. Content, static, media,
hashes, template lint/tooling, and output inventory use this policy where
covered by the build contract.

`build.followSymlinks=true` is implemented only by supported copy paths with
real-path/source-root checks. Do not route every scanner through a global
follow mode. Some CLI auxiliary scans have their own boundary and are not part
of the F-04 closure claim.

## F-05 Template Decision Freshness

`TemplateCapabilitiesResolver` reads `bukit.templates.yaml`, computes a SHA-256
fingerprint of current text, and reuses a cached manifest only for an identical
snapshot. Missing, created, deleted, invalid, corrected, and same-length changed
files must be observed on the next call. Invalid manifests are not preserved as
a valid cached decision.

`TemplateStaticAnalysisService` creates an analyzer per public analysis call;
its root/include/layout dependency cache is local to that graph. Capability
results returned to callers contain an independent `Fields` list.

The process-global dictionaries currently have no eviction policy. Do not
describe this correctness fix as removal of all template caches or a memory
bound.

## F-06 Search Cap Propagation

`site.search.maxContentLength` must be passed through document and list builders,
the built-in search plugin, publish projections, and i18n merged/split/index
writers. Only `content` is truncated. The unit is UTF-16 code units; truncation
backs off rather than splitting a valid surrogate pair. Runtime validation must
continue to reject values less than one, matching schema `minimum: 1`.

## F-07 Download-Level Concurrency

Each public rewrite operation owns a download semaphore shared by its documents,
HTML, and media fields. `LocalizedContentBodyStore` owns a lazy store-level gate
so concurrent `GetAsync` calls share the configured limit. Every `LocalizeAsync`
call on `ImageAssetLocalizer` in these paths must acquire the relevant gate and
release only after successful acquisition.

The document-transform gate and download gate serve different purposes.
Operation-local gates must not be replaced with process-global state without a
new contract. No fairness, global site-wide budget, or ordering guarantee is
implied.

## F-08 Build Health Snapshot

`SiteEngine` creates a `BuildDiagnosticLogger` per build invocation. Variant
forwarders share only that invocation's atomic counters. Single- and
multi-language flows snapshot counts before constructing the report.

`BuildOutputInventory` safely enumerates final public output with stable sorting
and excludes `.bukit`, state, marker, and symlink-only files. `BuildReporter`
writes the frozen `build-report.v1` shape, then artifact-manifest hashing reads
the final report bytes. Reporter diagnostics emitted after the count snapshot
are not retroactively included.

## Change Review Checklist

For changes touching these paths:

1. identify the affected invariant and its explicit exclusions;
2. add or update a focused regression before changing behavior;
3. run `scripts/checks/post-change-targeted.sh` with the changed paths;
4. run the security regression for F-01, F-02, or F-04 changes;
5. repeat concurrency-sensitive tests for F-03 or F-07 changes;
6. confirm schema/API/protocol stability for F-06 or F-08 changes;
7. update user, developer, skill, security/compatibility, and changelog surfaces
   when observable behavior changes.
