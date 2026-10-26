# Migrate the built-in plugin responsibility split

## Approval and scope

The user approved migrating item 3 first: the behavior-preserving DataFiles and ImageProcessing extraction from immutable commit `0619c25ce4583ebc06997a3b211116163a2fe744` onto `afb7430b21a6ae710b752801532c2946a8042087`. Approval covers this migration only, not item 1, integration, publication, or deployment.

## Why

Both internal plugin entries currently combine orchestration with traversal, parsing, encoding, ownership, and serialization. Move those existing responsibilities into internal helpers without changing their consumers or behavior.

## Changes

- Keep the entries, registration order, namespace, Engine assembly, interfaces, names, and versions.
- Extract six DataFiles helper files (seven responsibility types) and four image helper files.
- Preserve the current required caller-owned ILogger plumbing rather than restoring the source commit's older optional logger.
- Add compact baseline characterizations and an assertion-based architecture guard; add one exact workflow contract mapping and its owner assertions.

## Non-goals and risk

No public API/assembly/baseline, Theme, Labs, Notion, logger implementation, dependency, IVT, parser, image calibration, or configuration change. The governed baseline remains 426 types in 19 assemblies, byte-identical. Item 1 remains a separate compatibility task. Inherited `I-IMAGE-TEMP` is deferred: external resize execution currently precedes temporary cleanup's try/finally; this extraction does not repair that nesting or promise complete cancellation cleanup.

## Verification boundary

`tasks.md` is the sole repository implementation checklist. The external controller handoff's `commands.json` fixes exact commands, filters, configuration, and result paths. Run only complete scheduled specialties: Engine's eleven classes, Architecture's five existing classes plus `BuiltInPluginSplitTests`, Rendering's `ScribanTemplateRendererTests`, complete workflow owner self-test, and the direct public API tool check against built assemblies. No whole-solution/full/release/aggregate gate, historical SRBiz replay, or partial `SiteEngineIntegrationTests` execution is authorized.
