# Labs Theme YAML context design

## Cause and repair

`ThemeModels.cs` declares an empty partial static context with six `YamlSerializable` registrations. Attributes mark generation inputs; they do not implement the factory, resolver or inspector. Labs has neither that generated implementation nor a generator package reference. The loaders catch deserialization exceptions and return null, which also hides metadata from commands and manifest names from installation.

Mirror the existing Core Theme project pattern without modifying Core: ordinary builds compile `Commands/Theme/ThemeYamlStaticContext.Generated.cs`; `BukitGenerateLabsYamlStaticContext` defaults to false. Setting only that Labs property to true excludes the snapshot and enables `Vecc.YamlDotNet.Analyzers.StaticGenerator` with `PrivateAssets="all"`, using centrally pinned 16.3.0. It must not enable Core's separate generation property.

Generate through the exact external-artifacts build in `labs-yaml-repair/commands.json`. Normalize the real generator output with the unchanged `scripts/build/normalize-yaml-static-context.py`, then correct only the external result's fixed Input provenance header to `src/Bukit-Labs/Bukit.Labs.Cli/Commands/Theme/ThemeModels.cs`. Check in that normalized result, not a handwritten serializer or a new generation framework.

## Preserved contract and consumers

Both manifest and registry loaders retain static deserialization, underscored names and ignored unknown properties. All six registered models and nested tags/parameters/download/metadata remain usable. Date/version scalars remain strings, malformed YAML returns null, missing manifests return null, and `DeclaredParamCount` is unchanged. The direct consumers are the Labs Theme, registry, pack and install commands; Architecture consumes the Labs assembly. No public Core API changes are expected (426 types / 19 assemblies).

## Proof and scope

The planned repair is nine paths: four documents in this change, the Labs project, generated context, model tests, shared workflow policy and its closure-mapping test. Preimplementation closure/classification and exact commands are external. The only new policy rule maps this OpenSpec change to complete Labs and Architecture tests plus the explicitly authorized complete `MetaRemovalInventoryTests` class; existing Labs and Engine mappings remain intact. Its source-contract scan directly consumes Labs `.cs` text across `src`, `tests` and `guide` (including selected ignored build-output text). The expanded closure includes that exact consumer before these contract edits, without adding a changed path.

Reuse the original four-failure TRX as behavior RED; observe strengthened model/context and mapping regressions before the fix. Run complete Labs, complete Architecture, workflow owner and the complete source-contract class serially after final contract edits, plus the authorized generation/default-source proof and direct API check. The ordinary build must compile the snapshot without emitting `YamlDotNetAutoGraph`. Record fresh scoped cache evidence including untracked generated source and the source inventory's selected text/input set. Keep the sole checklist in `tasks.md`; exclude only that nonconsumed proof checklist from test fingerprints, not from review scope. Preserve old Theme caches and report their invalidated inputs rather than claiming fresh hits or rerunning unchanged Core fixtures. Parent composes the new source-class result with the unchanged historical 2,534-case Engine runtime/fixture evidence; no new full Engine execution or full-command cache hit is claimed. The other eight eligible historical projections and omitted-input bridges remain parent-owned, as documented in the external read-only inventory.
