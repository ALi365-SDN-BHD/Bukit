# Labs Theme YAML context repair

## Why

The user independently authorized this Labs repair on 2026-10-25 after the Template / Theme relocation's complete Labs specialty reported 151 passed / 4 failed / 0 skipped. Valid manifests and registry YAML return null, losing metadata and causing archive installation to use the archive basename. The unchanged base sources were compared, but no execution on base is claimed.

## Changes

- Supply the missing generated implementation for the six existing `ThemeYamlStaticContext` registrations.
- Follow Core Theme's checked-in snapshot and opt-in private generator pattern, using the separate `BukitGenerateLabsYamlStaticContext` property and the existing central generator version 16.3.0.
- Strengthen Labs model regressions and add only this change's exact workflow-policy mapping and owner regression.

## Boundaries

Keep `StaticDeserializerBuilder`, models, underscored naming, ignored unknown properties, null-on-invalid/missing behavior and parameter counting unchanged. No reflection deserializer, runtime dependency, project reference, central version, Core source/project/public-baseline, build-helper, CI/release or gate changes. Preserve the existing Theme diff except the two shared workflow-policy/self-test files. Logger remains paused; Section is untouched. No commits, native publish, full/release/solution gates or extra audits.

Verification is complete Labs, complete Architecture, workflow owner, the subsequently authorized complete `MetaRemovalInventoryTests` source-contract class, direct API check and two prerequisite builds in the external `labs-yaml-repair/commands.json`. The Engine class directly scans Labs source text despite having no Labs runtime dependency. Its fresh result may be composed with unchanged historical Engine runtime/fixture evidence; the old full Engine command is not a fresh cache hit. No full Engine rerun is authorized. Parent owns this composition, repair specialty review and the final delta-only review; unchanged Theme evidence is reused honestly, without refreshing unrelated fixtures.
