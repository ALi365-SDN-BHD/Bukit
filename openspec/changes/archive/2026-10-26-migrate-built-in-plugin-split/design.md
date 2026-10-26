# Design

## Faithful source and ownership

Use only immutable commit `0619c25ce4583ebc06997a3b211116163a2fe744` as the extraction source. Never copy the dirty legacy worktree or unrelated parent changes. Apply DataFiles first, then ImageProcessing. All helpers stay top-level non-public types in `Bukit.Engine.Plugins.BuiltIn` in the existing Engine assembly; they do not become registered plugins.

DataFiles responsibilities are `DataFileLimits`, `DataDirectoryWalker`, `DataFileReaders`, `DocumentTraversalState`, `ProjectionBudgetState`, `DataJsonProjector`, and `DataYamlProjector`. `DataProjectionBudget.cs` contains the two state types, not a type named DataProjectionBudget. Keep the private plugin `_openDataFile` seam and default `OpenVerifiedDataFile` method name. Move only the three existing test owner references. Limits remain immutable configuration; traversal and projection budgets are shared across languages/default data but fresh per derive call.

Image responsibilities are `ImageVariantGenerator`, `ImageVariantFreshness`, `ImageVariantOwnership`, and `ResponsiveImageHtmlTransform`. Keep entry orchestration's discovery, planning/stale cleanup, tool selection, resize generation, WebP generation/HTML rewrite or prior-WebP preservation, srcset publication, and tracking order. Preserve internal wrappers, freshness record/context, generated regexes, and generated JSON metadata without new exports.

## Invariants

- Data constructor validation precedence/defaults, ten limits, resolved parameter names/messages, synchronous exception timing, cancellation, bounded enumeration/order, strict BOM decoding, no-follow/TOCTOU opening, byte/node/depth/scalar/projection accounting, and stream ownership/disposal stay unchanged.
- JSON/YAML graph types, case-insensitive maps, list order, null handling, first-document projection with later-document validation, alias cloning, typed data key, template alias, and canonical incremental hashing stay unchanged.
- Image native validation/calibration, orientation, dimensions, quality, paths, identity hashes, freshness schema/compact UTF-8 field order, ownership conjunctions, atomic image/sidecar move order, cleanup nesting, HTML escaping/regex behavior, and tracked plugin/hook/path tuples stay unchanged.
- Both external runner calls retain the exact third logger argument (`context.Logger` for resize; `logger` for probe). Discovery requires non-null, non-optional `ILogger logger`, receives `context.Logger`, and uses `logger.Warn`. Counting remains before the caller sink's filtering; no fallback logger or new runtime seam.

## Evidence design

Before production changes, baseline characterizations check constructor details, mixed language/default JSON/YAML alias and extra-document projection, shared budgets and per-call reset, exact freshness bytes/srcset/tracking, and counted JSON Warn/Error caller sinks on external failure. Architecture RED uses string reflection and presence assertions before inspecting absent helpers/files, plus small Roslyn call-site checks; compilation errors are not RED. Workflow RED is the complete owner self-test asserting missing mappings for the four contract files.

The exact scheduled complete classes are:

- Engine: `DataFilesPluginTests`, `ImageProcessingPluginTests`, `AssetToolingSymlinkTests`, `PluginRegistryTests`, `PluginExecutionSessionTests`, `PluginRunnerTests`, `PluginPipelineTests`, `RenderDependencyHasherTests`, `ExternalToolProcessRunnerTests`, `LibraryDiagnosticOutputTests`, `MetaRemovalInventoryTests`.
- Architecture: `BuiltInPluginSplitTests`, `G04D9EBuiltInPluginGraphTests`, `LibraryConsoleOutputTests`, `EngineModuleBoundaryTests`, `DependencyMatrixTests`, `BuildDataKeyCatalogTests` (baseline omits only the new guard).
- Rendering: `ScribanTemplateRendererTests`.
- Owner: `bash scripts/checks/codex-workflow-self-test.sh`.
- API: direct `Bukit.PublicApiDrift` check against the unchanged baseline and built 19 assemblies; do not use the wrapper that builds the solution.

Build only the three scheduled test projects and API tool project as direct prerequisites, serially. Run each specialty alone, with stable before/after build/read/fixture input manifests and official GREEN cache record/check; enumerate MetaRemovalInventory's actual selected text files including ignored outputs. Generic policy routing is not additional execution authority. Full logs, TRX counts/class membership, provenance, API hashes, cache manifests, and final outcomes belong in the assigned external implementation report. Finalize repository docs before stable GREEN snapshots.

## Deferred risk and acceptance

`I-IMAGE-TEMP` is inherited: the external resize await is outside the subsequent temporary cleanup finally in both baseline and extraction. A separate proposed repair would enclose execution and validation in that existing finally and add cancellation/timeout cleanup proof; do not bundle it here. Existing process/diagnostic tests are not proof of an actual unkillable image process.

The parent performs one post-GREEN specialty review (spec compliance before quality) and one generated delta-only final review using external reports. Acceptance, commits, integration, and item 1 are not writer authority. No OpenSpec sync/archive gate is inferred.
