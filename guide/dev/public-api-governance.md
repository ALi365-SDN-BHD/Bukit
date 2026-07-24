# Public API Governance

C# `public` is CLR visibility, not an automatic supported SDK promise.

Bukit's supported external surfaces are CLI behavior, configuration and theme
shapes, template objects, report schemas, and the `bukit-plugin-v1` process
protocol. Bukit does not currently distribute a general-purpose Core CLR SDK.
Third-party process plugins exchange JSON and do not reference Bukit CLR
assemblies.

## Notion Assembly Distribution Boundary

`Bukit.Notion` and `Bukit.Content.Notion` are monorepo Core components. They
provide canonical implementation boundaries inside Bukit's source and build
graph, but they are not supported NuGet SDKs. Both projects explicitly set
`IsPackable=false`; the release workflow continues to distribute the Core CLI,
not independent Notion library packages.

Their existing exported types remain governed as `1.x-do-not-narrow` through
the 1.x line. This preserves current source and assembly consumers without
turning CLR visibility into a new external SDK support promise. Independent
package metadata, installation documentation, target-framework support,
semantic-versioning commitments, and publication automation require a separate
productization decision and review.

## Legacy Notion Facade Freeze

Through the 1.x line, `Bukit.Shared.Notion.*` types in `Bukit.Shared.dll` and
legacy `Bukit.Content.Notion.*` types in `Bukit.Content.dll` are frozen
compatibility facades. They may receive compatibility, correctness, and security fixes only,
and those fixes must preserve their existing public and protected
surface, namespace, and assembly identity.

New Notion capabilities must be implemented in the canonical projects:
protocol, transport, conversion, diagnostics, rendering, and write behavior in
`Bukit.Notion`; Bukit content projection and source adaptation in
`Bukit.Content.Notion`. Legacy facades must delegate to those owners and must not
acquire a second transport, endpoint list, renderer registry, projection path,
or cache format.

Removing a legacy facade or the compatibility references from `Bukit.Shared`
and `Bukit.Content` remains a separately reviewed 2.0 change. The open consumer
declaration and public-surface governance process must authorize that change;
the freeze itself is not a deprecation or removal decision.

### AD-03C5 Notion Property Parser Retention

`Bukit.Content.Notion.NotionPropertyParser` in `Bukit.Content.dll` is
retain-by-design. It remains a public static implementation facade with only
`ExtractFields(JsonElement)` and `ExtractAllFields(JsonElement)` as its public
declared methods. Its governed classification is
`implementation-public / 1.x-do-not-narrow / 2.0-review`; this is CLR
visibility for a retained implementation detail, not a general-purpose SDK
promise.

There is no public canonical replacement for this parser in the canonical
`Bukit.Content.Notion` adapter. Its existing `NotionContentPropertyParser` and
`NotionPropertyTypeParser` implementations remain internal, and this decision
does not expose either implementation or create another public parser API.
Repository and reviewed public evidence found no direct current production
caller, but private, unindexed, binary-only, reflection-based, and undisclosed
consumers remain unknown.

Re-review requires a separate task.

Any real security or correctness defect starts a re-review.

Any direct consumer declaration starts a re-review.

Any separately approved CLR SDK productization decision with a migration and versioning plan starts a re-review.

The resulting review determines whether a defect has a compatible fix, how
consumer evidence changes the migration analysis, and whether productization
includes an approved replacement. Those are review questions, not filters that
can prevent one of the three events from starting review. Consumer-search
silence alone is not permission to delete, internalize, or replace the facade.

The exact decision and evidence limits are recorded in the
[AD-03C5 retention ledger](../../docs/analysis/bukit-core-ad03c5-notion-property-parser-retention-decision-2026-07-24.zh-CN.md).

## Check

`bash scripts/checks/public-api-drift.sh check Release`

The check compares the compiled public and protected surfaces with
`docs/governance/bukit-core-public-api-baseline.v1.json`. It is a
maintainer-local governance tool, not a general CLR SDK declaration.
Both `check` and `snapshot` require the exact policy-owned, ordered mapping of
the fourteen Core assemblies to their projects before any assembly is captured.

## Diagnostics And Exit Codes

Diagnostics are sorted as `<category>: <assembly>::<type>: <detail>`.

| Category | Meaning |
|---|---|
| `breaking` | An exported type or public member was removed. |
| `review-required` | An exported type, public member, or governance metadata changed. |
| `protected-review` | A protected member changed and needs review. |
| `type-shape-review` | An exported type signature changed. |
| `contract-shape-review` | A `plugin-wire-contract` or `serialized-contract` type changed. |
| `aot-review` | An `aot-serialization-surface` type changed. |
| `unclassified` | A new type has no approved classification. |
| `gate-error` | Input, baseline, capture, or snapshot processing failed. |

The command exits `0` for an exact match, `1` for valid drift requiring
review, and `2` for invalid input or a gate error.

## Review A Legitimate Change

1. Run `bash scripts/checks/public-api-drift.sh snapshot OUTPUT Release`.
2. Review every type/member diff and assign owner, classification,
   compatibility, migration horizon, and reason.
3. Run the relevant schema, protocol, or AOT contract tests.
4. Replace the governed baseline only in the reviewed change.
5. Run the self-test, real check, `ci-fast`, and Architecture tests.

Never infer removal safety from zero repository-local consumers. Access
narrowing remains a separate major-version task.

## AD-01 2.0 Configuration-Decoupling Migration

The approved AD-01 change removes
`BuildContext.Config : Bukit.Config.AppConfig` and the public
`SiteEngine.GetListRoutes(BuildContext, ThemeTemplateResolver?)` overload.
The replacement list-route overload accepts routed documents, collections,
output-path encoding, and the optional template resolver explicitly.

The existing collections-only overload, public `PluginRegistry` and
`PluginRunner` facades, and in-process plugin interfaces remain unchanged.
The governed baseline still contains 14 assemblies and 425 public types; its
AD-01 diff has exactly one removed property, one removed overload, and one
added overload.

Known reviewed site repositories use the Bukit CLI and did not expose a direct
CLR match, but private, unindexed, binary-only, and undisclosed consumers remain
unknown. Direct CLR consumers must remove `BuildContext.Config` access and pass
list-route inputs explicitly. They must not recreate the removed ambient
configuration channel through `BuildContext.Data`.

See the
[AD-01 final closure and migration ledger](../../docs/analysis/bukit-core-ad01-config-decoupling-final-closure-2026-07-24.zh-CN.md)
for exact signatures, evidence, exclusions, rollback boundaries, and migration
examples.

## Baseline Review Vocabulary

Every governed type uses one classification and one compatibility value.

| Classification | Use |
|---|---|
| `aot-serialization-surface` | AOT serializer context surface. |
| `cross-assembly-implementation` | CLR-visible implementation consumed across Bukit assemblies. |
| `implementation-public` | Public implementation detail, not an external SDK promise. |
| `persisted-internal-format` | Internal persisted-format surface. |
| `plugin-wire-contract` | `bukit-plugin-v1` JSON protocol surface. |
| `serialized-contract` | Serialized report or payload shape. |

| Compatibility | Review policy |
|---|---|
| `1.x-do-not-narrow` | Keep accessible through 1.x. |
| `1.x-migration-safe` | Change only with an approved 1.x migration. |
| `1.x-shape-stable` | Preserve the serialized or protocol shape through 1.x. |
| `2.0-candidate` | Consider narrowing only in a reviewed 2.0 change. |
| `not-a-clr-contract` | CLR-visible implementation with no external CLR contract promise. |

## Snapshot Safety Boundary

`snapshot` requires an explicit `OUTPUT` path. It will not overwrite the
governed baseline, an existing file, directory, or link; it accepts a new path
only inside the repository or the system temporary directory and creates it
with no-overwrite semantics. Canonicalization resolves existing links/reparse
points and rejects aliases or path escapes. Path comparison is ordinal on every
host, so differently-cased aliases may fail closed rather than risk escape.

This boundary defends ordinary maintainer mistakes, existing links/reparse
points, aliases, path escapes, and overwrites. It does not claim resistance to
a malicious same-account process that races to replace a validated parent path
between validation and file creation.

## CI Scope

`ci-fast` runs the fixture-only self-test first, then one real configured Core
surface check. The self-test must not run a real Core snapshot or check.

## 2.0 Consumer Declaration Window

The [2.0 public surface candidate manifest](../../docs/governance/bukit-core-2.0-public-surface-candidates.v1.json)
and [consumer declaration](../../docs/governance/bukit-core-2.0-consumer-declaration.md)
record the current declaration state as `closed`. The window opened at
`2026-07-21T02:19:46Z`; [GitHub Issue #60](https://github.com/ALi365-SDN-BHD/Bukit/issues/60)
was observed closed at `2026-07-22T07:08:30Z`, with its close event at
`2026-07-22T07:08:31Z`. The eligible stable release is `v1.0.10`. At
declaration-window closure, all 136 candidates were recorded as review-only
and `consumer-declaration-pending`; closing the window itself was not a removal
decision.

All 1.x CLR access levels remain unchanged. A `no-public-match-found` result
means only that the recorded public searches found no reviewed external match;
it is not proof that removal is safe and cannot reveal private, unindexed, or
undisclosed consumers.

The closed lifecycle preserves the limit of public evidence: a
`no-public-match-found` result cannot prove the absence of private, unindexed,
or undisclosed consumers. New evidence requires a separately opened channel or
task rather than use of the closed Issue.

At declaration-window closure, it permitted only a G-04C eligibility
discussion and did not authorize a candidate change. G-04C was the first
authorized 2.0 removal decision; at that point, the other 135
candidates were not batch-approved. G-04D1A was a later independent 2.0
removal decision; immediately after that decision, the other 133 candidates
were not batch-approved. Both counts are historical post-decision states.
Neither decision authorizes a batch access-level change. All 1.x CLR visibility
remains unchanged.

### G-04C Single-Type Pilot

Historical G-04C single-type decision: only `Bukit.Engine.RouteInventoryInspectEntry`
was approved for removal in 2.0; at that point, the other 135 candidates were
not batch-approved.
See the [decision ledger](../../docs/analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md)
for the breaking-change evidence, migration boundary, targeted verification,
and independent review.

The closed 136-entry candidate manifest is an immutable declaration-window
snapshot. It intentionally retains the removed type and its original search
evidence. The governed public API baseline, not that historical cohort, is the
current CLR surface inventory.

### G-04D1A Two Static Facades

G-04D1A two-static-facade decision: only `Bukit.Content.Notion.NotionColorPalette` and `Bukit.Content.Notion.NotionRichTextRenderer` are approved for removal in 2.0; the other 133 candidates are not batch-approved.
G-04D1A was a later independent 2.0 removal decision. The 133-candidate
remainder was the historical state immediately after that decision. It followed
the historical G-04C state, which removed one type while
the other 135 candidates were not batch-approved. At the time, G-04D1A did not
batch-authorize those 133 remaining candidates, and it does not change any 1.x
CLR visibility.

The canonical replacements are `Bukit.Notion.Rendering.NotionColorPalette`
and `Bukit.Notion.Rendering.NotionRichTextRenderer`. See the
[G-04D1A decision ledger](../../docs/analysis/bukit-core-g04d1a-static-notion-facade-removal-2026-07-22.zh-CN.md)
for the breaking-change evidence and canonical-test migration.
Completed cross-boundary validation and independent review evidence is recorded
there. The closed 136-entry candidate manifest remains an
immutable historical cohort; it is not the current baseline.

### G-04D1B Block Renderer Facades

G-04D1B block-renderer-facade decision: only the 23 `Bukit.Content.Notion.BlockRenderers` facade types recorded in the G-04D1B ledger are approved for removal in 2.0; the other 110 candidates are not batch-approved.

Their canonical namespace is `Bukit.Notion.Rendering.BlockRenderers`. The
closed 136-entry candidate manifest remains the immutable historical cohort;
the G-04C 135-candidate and G-04D1A 133-candidate statements remain historical
snapshots. Immediately after G-04D1B, the public API baseline contained 514
types, including 110 `2.0-candidate` entries.
This 2.0 decision does not change any 1.x CLR
visibility.

See the [G-04D1B decision ledger](../../docs/analysis/bukit-core-g04d1b-block-renderer-facade-removal-2026-07-23.zh-CN.md)
for the exact removal set, canonical migration, preserved D1C surface, Task 1
owner checks, and completed G-04D1B cross-boundary verification. Completed cross-boundary validation and independent review evidence is recorded there. The
parent aggregate gate and final aggregate review remain pending and are not
claimed by the G-04D1B ledger.

### G-04D1C-M2 Notion Extension Graph

G-04D1C-M2 five-type atomic decision: only the five approved `Bukit.Content.Notion` renderer-extension CLR identities are removed in 2.0; the other 105 candidates are not batch-approved.

The approved identities are:

- `Bukit.Content.Notion.INotionBlockRenderer`;
- `Bukit.Content.Notion.NotionBlockTransformer`;
- `Bukit.Content.Notion.NotionBlockRendererRegistry`;
- `Bukit.Content.Notion.NotionRenderContext`;
- `Bukit.Content.Notion.NotionBlocksRenderer`.

Their canonical replacements are in `Bukit.Notion.Rendering`.
The current public API baseline contains 509 types, including 105 `2.0-candidate` entries.
The closed 136-entry candidate manifest remains the immutable historical
cohort and continues to record `unknown-until-voluntary-declaration` for
private consumers. This decision does not modify any 1.x CLR visibility.

See the [G-04D1C-M2 decision ledger](../../docs/analysis/bukit-core-g04d1c-m2-five-type-atomic-removal-2026-07-23.zh-CN.md)
for the deliberate approval, exact removal set, migration boundary,
verification evidence, and independent review status. This decision does not
authorize removal of `NotionApiClient`, `NotionProviderOptions`, or
`NotionClientStats`.

### G-04D2A Plugin Secret Masker

G-04D2A single-type internalization decision: only `Bukit.PluginHost.PluginSecretMasker` is narrowed from public to internal in 2.0; the other 104 candidates are not batch-approved.

At the G-04D2A decision, the public API baseline contained 508 types,
including 104 `2.0-candidate` entries.
It covers 14 assemblies. The closed 136-entry candidate manifest remains
immutable and intentionally retains the historical `PluginSecretMasker`
record. Its private-consumer status remains
`unknown-until-voluntary-declaration`; the absence of a reviewed public-search
match does not establish that no private, unindexed, or undisclosed direct CLR
consumer exists.

The narrowing is 2.0-only and is source and binary breaking for such a direct
CLR consumer. It needs no replacement API because the supported external
plugin surface is the `bukit-plugin-v1` process protocol, not this
same-assembly helper. Masking behavior, general URL cleaning, schema and
report-shape changes, and all other `Bukit.PluginHost` candidates are outside
the decision.

See the [G-04D2A decision ledger](../../docs/analysis/bukit-core-g04d2a-plugin-secret-masker-internalization-2026-07-23.zh-CN.md)
for the exact one-token source change, governed baseline delta, consumer and
Native AOT evidence boundary, exclusions, stop conditions, and task-level
verification.

### G-04D2B2 Plugin Host Error Codes

G-04D2B2 single-type internalization decision: only `Bukit.PluginHost.PluginHostErrorCodes` is narrowed from public to internal in 2.0; the other 103 candidates are not batch-approved.

The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.
It covers 14 assemblies. The closed 136-entry candidate manifest remains
immutable with Git blob `7b07d6890562387010b52301e9f8716e9bf10ed1`; private
consumers remain `unknown-until-voluntary-declaration`. The 2026-07-22
authenticated public search found no public match, and no new governance-grade
GitHub Code Search was available on 2026-07-23.

Ordinary const-consuming binaries may retain inlined values, but source
recompilation and public metadata/reflection consumers are breaking in 2.0.
The six vocabulary strings and five runtime Host behaviors remain unchanged.
G-04D2B2 itself approved no other `Bukit.PluginHost` candidate; later D2
decisions are recorded in the
[PluginHost aggregate ledger](../../docs/analysis/bukit-core-g04d2-pluginhost-final-aggregate-closure-2026-07-23.zh-CN.md).

See the [G-04D2B2 decision ledger](../../docs/analysis/bukit-core-g04d2b2-plugin-host-error-codes-internalization-2026-07-23.zh-CN.md)
for the exact visibility narrowing, governed delta, qualification boundary, and
exclusions.

### G-04D3B Notion Client Stats

G-04D3B removes only the duplicate
`Bukit.Content.Notion.NotionClientStats` CLR identity in 2.0. The internal
legacy `NotionApiClient.GetStats()` facade now returns the canonical
`Bukit.Notion.Transport.NotionClientStats`; the other 84 candidates are not
batch-approved.

The current public API baseline contains 425 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`. Direct consumers of the removed
legacy CLR identity must migrate namespaces; private and undisclosed consumers
remain unknown.

This decision does not change request/throttle counters, retry, rate limits,
Notion API behavior, transport lifetime, or public `NotionApiClient` members.
See the
[G-04D3B decision ledger](../../docs/analysis/bukit-core-g04d3b-notion-client-stats-resolution-2026-07-23.zh-CN.md)
for the migration contract and G2 verification boundary.

### G-04D4A Shared Notion Graph

At G-04D4A closure, all 13 `Bukit.Shared.Notion` model/record identities were
retained as companion types of `HtmlToNotionBlockConverter.Convert(string)`.
This records the historical G-04D4A state, not the current 2.0 public surface.
The later independently authorized AD-03C 2.0 compatibility cleanup superseded that retention outcome
and removed the 13 models plus the Shared converter in 2.0. See the
[2.0 Notion compatibility migration](../../docs/governance/bukit-core-2.0-notion-compatibility-migration.md).

The duplicate Shared `HtmlTokenizer`, `HtmlToken`, and `HtmlTokenType`
identities are removed together in 2.0. Direct CLR consumers must migrate to
`Bukit.Notion.Conversion.HtmlTokenizer` and its canonical nested types. Enum
ordinals, token defaults, parsing behavior, and exception behavior are
unchanged.

The current public API baseline contains 425 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private and undisclosed consumers
remain unknown. See the
[G-04D4A decision ledger](../../docs/analysis/bukit-core-g04d4a-shared-notion-graph-resolution-2026-07-23.zh-CN.md)
for the atomic migration contract and G2 verification boundary.

### G-04D4B Value Coercion

G-04D4B narrows only `Bukit.Shared.ValueCoercion` from public to internal in
2.0. Repository production code has no direct consumer, and the existing
`Bukit.Shared.Tests` friend boundary continues to characterize its behavior.
No replacement or global conversion abstraction is introduced.

The current public API baseline contains 425 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private and undisclosed consumers
remain unknown.

This 2.0-only narrowing is source, binary, and reflection breaking for a direct
CLR consumer. Null, boolean, whitelist casing, whitespace, number, current
culture, custom `ToString`, fallback, and exception propagation semantics are
unchanged. See the
[G-04D4B decision ledger](../../docs/analysis/bukit-core-g04d4b-value-coercion-resolution-2026-07-23.zh-CN.md)
for the exact one-token change and G2 verification boundary.

### G-04D5A CLI Parse Graph

G-04D5A narrows `CliBoundCommandFactory`, `SimpleParseResult`, and
`SubcommandParseResult` from public to internal in 2.0. The public
`CliParseResult` base is retained and reclassified as
`cross-assembly-implementation / 1.x-do-not-narrow` because
`CliParser.Parse` returns it, `CommandDescriptor.DispatchAsync` accepts it,
and external record derivation is an existing contract.

The current public API baseline contains 425 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private and undisclosed consumers
remain unknown.

Argument binding, subcommand recursion, diagnostic order, dispatch, command
tree, stderr, and exit-code behavior are unchanged. Tests use the public
parser/dispatcher contract; no CLI Shared friend assembly is added. See the
[G-04D5A decision ledger](../../docs/analysis/bukit-core-g04d5a-cli-parse-graph-resolution-2026-07-23.zh-CN.md)
for the exact migration and G2 verification boundary.

### G-04D5B CLI Error Payload

G-04D5B narrows only
`CliErrorRenderer.CliErrorPayload` from public nested record to internal
nested record in 2.0. `CliErrorRenderer`, `CliErrorDiagnostic`, all public
`RenderJson` overloads, and the machine-readable JSON contract remain public
and unchanged.

The current public API baseline contains 425 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private and undisclosed consumers
remain unknown.

The source-generated serializer root remains explicit. Property names, order,
indentation, null omission, defaults, escaping, stdout/stderr routing, usage,
and exit codes are unchanged. See the
[G-04D5B decision ledger](../../docs/analysis/bukit-core-g04d5b-cli-error-payload-resolution-2026-07-23.zh-CN.md)
for the exact one-token change and G2 verification boundary.

### G-04D6A Rendering File Template Loader

G-04D6A narrows only
`Bukit.Rendering.Scriban.FileTemplateLoader` from public to internal in 2.0.
The type remains sealed and continues to implement Scriban's
`ITemplateLoader` with the same constructor and three interface methods.
`ScribanTemplateRenderer` remains the public Rendering entry point.

The current public API baseline contains 425 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private, unindexed, and
undisclosed consumers remain `unknown-until-voluntary-declaration`.

Override, child, and parent fallback order, missing-template primary path,
path safety, sync/async loading, cache signatures, exceptions, and Scriban
interface dispatch remain unchanged. No friend assembly is added. The
[G-04D6A decision ledger](../../docs/analysis/bukit-core-g04d6a-file-template-loader-resolution-2026-07-23.zh-CN.md)
records the exact change and G3 verification boundary.

### G-04D6B Rendering Scriban Model Binder

G-04D6B narrows only
`Bukit.Rendering.Scriban.ScribanModelBinder` from public to internal in 2.0.
The static facade and both `PageModel`/`ListPageModel` overloads remain in
place, and `ScribanTemplateRenderer` keeps both direct static call roots.

The current public API baseline contains 425 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private, unindexed, and
undisclosed consumers remain `unknown-until-voluntary-declaration`.

Explicit template keys, aliases, null handling, read-only and mutable
dictionary projection, list projection, unsupported-object `ToString`
fallback, and exception propagation remain unchanged. No reflection mapper,
replacement facade, or friend assembly is added. The
[G-04D6B decision ledger](../../docs/analysis/bukit-core-g04d6b-scriban-model-binder-resolution-2026-07-23.zh-CN.md)
records the exact change and G3 verification boundary.

### G-04D7A Routing Route Generation Result

G-04D7A removes the public nested
`Bukit.Routing.RouteGenerator.RouteGenerationResult` record in 2.0 and
changes only `RouteGenerator.GenerateWithSource`'s return carrier to the
named tuple `(RouteInfo Route, RouteSource Source)`. The method name,
parameters, optional defaults, tuple element names, public `RouteSource`
enum, and route/source behavior remain unchanged.

The current public API baseline contains 425 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private, unindexed, and
undisclosed consumers remain `unknown-until-voluntary-declaration`.

This is a 2.0 binary break. Consumers that explicitly name or construct the
record, use record/reference/null semantics, reflect its full name, or
serialize it directly must migrate. Inferred `.Route`/`.Source` access and
deconstruction retain the same C# call shape after recompilation. Route
precedence, collision, locale, encoding, normalization, and security
validation are unchanged. No friend assembly, replacement DTO, serializer
root, schema, or protocol is added. See the
[G-04D7A decision ledger](../../docs/analysis/bukit-core-g04d7a-route-result-resolution-2026-07-23.zh-CN.md)
for the exact migration and G3 verification boundary.

### G-04D8A Theme Validation Graph

G-04D8A narrows only
`Bukit.Theme.SchemaValidationException` from public to internal in 2.0.
`SchemaValidationError` remains public and is reclassified as
`cross-assembly-implementation / 1.x-do-not-narrow` because public
`SectionSchemaValidator.Validate` returns
`List<SchemaValidationError>`.

The current public API baseline contains 425 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private, unindexed, and
undisclosed consumers remain `unknown-until-voluntary-declaration`.

The internal exception type remains present and retains its string
constructor. Warn/off results, strict first-error timing, error text and
ordering, logger output, public validator signature, theme schema and
source-generated JSON roots remain unchanged. No friend assembly is added.
The
[G-04D8A decision ledger](../../docs/analysis/bukit-core-g04d8a-theme-validation-graph-resolution-2026-07-23.zh-CN.md)
records the exact change and G3 verification boundary.

### G-04D8B Theme Doctor Result

G-04D8B retains the public nested
`Bukit.Theme.ThemeDoctorCommand.DoctorResult` record and reclassifies it as
`cross-assembly-implementation / 1.x-do-not-narrow`. Public `Diagnose`
returns that exact companion type, and public `PrintReport` accepts it, so
the nested record cannot be narrowed independently of its public facade.

The current public API baseline contains 425 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private, unindexed, and
undisclosed consumers remain `unknown-until-voluntary-declaration`.

The `HasErrors`, `HasWarnings` and mutable `List<string> Issues` shape,
record/list reference semantics, diagnostic ordering, glyphs, spacing and
summary priority remain unchanged. Core CLI doctor remains an independent
text/exit-code pipeline. No JSON source-generation root, Native AOT reflection
root, CLI command, Theme schema, Labs or plugin surface is added. See the
[G-04D8B decision ledger](../../docs/analysis/bukit-core-g04d8b-theme-doctor-result-resolution-2026-07-23.zh-CN.md)
for the exact retained decision and pending G3 verification boundary.

### G-04D9A Engine Build Orchestration Graph

G-04D9A narrows only `Bukit.Engine.BuildPipeline`,
`Bukit.Engine.BuildPipelineContext`, `Bukit.Engine.RoutePipeline`, and
`Bukit.Engine.RoutePipelineResult` from public to internal in 2.0. The two
pipeline/result pairs remain present with the same constructors, members,
record shapes, cancellation flow and route behavior.

`BuildOptions`, `BuildVariantSummary`, and `ContentPipelineResult` remain
public and are reclassified as
`cross-assembly-implementation / 1.x-do-not-narrow`. They are propagated by
the existing public `SiteEngine`, `BuildResult`, and `ContentPipeline`
contracts; those parent APIs are unchanged.

The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.
It covers 14 assemblies. The closed 136-entry candidate manifest remains
immutable with Git blob `7b07d6890562387010b52301e9f8716e9bf10ed1`;
private, unindexed, and undisclosed consumers remain
`unknown-until-voluntary-declaration`.

This is a 2.0 source, binary, and reflection break only for consumers that
directly construct or name the four internalized orchestration types.
Supported consumers continue through `SiteEngine`, `BuildResult`, and
`ContentPipeline`. No build report, route, config, schema, plugin protocol,
path/security behavior, JSON/AOT root, friend assembly, Labs, or external
plugin surface is changed. See the
[G-04D9A decision ledger](../../docs/analysis/bukit-core-g04d9a-build-orchestration-resolution-2026-07-23.zh-CN.md)
for the exact decision and pending G4 verification boundary.

### G-04D9B Engine Content Validation and Stage Contracts

G-04D9B narrows only `ContentCollectionContractValidator` and
`ContentSchemaValidator` from public to internal in 2.0. Both validators
remain present with the same validation entry points, field rules, ordering,
fail-mode resolution, diagnostics and messages.

`ContentValidationIssue`, `IContentProviderFactory`, `ITemplateRenderer`,
`ContentStageInput`, `ContentStageOutput`, `IContentStage`, and
`TemplateRendererBase` remain public and are reclassified as
`cross-assembly-implementation / 1.x-do-not-narrow`. Public projection,
pipeline constructor, stage interface, provider implementation, and renderer
inheritance signatures require these exact companion contracts.

The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.
It covers 14 assemblies. The closed 136-entry candidate manifest remains
immutable with Git blob `7b07d6890562387010b52301e9f8716e9bf10ed1`;
private, unindexed, and undisclosed consumers remain
`unknown-until-voluntary-declaration`.

This is a 2.0 CLR break only for direct references to the two validator
implementation types. `ContentPipeline` keeps both public constructors and
its public result; the renderer protected extension surface remains intact.
No schema rule, content ordering, renderer behavior, config, plugin protocol,
friend assembly, Labs, or external plugin code is changed. See the
[G-04D9B decision ledger](../../docs/analysis/bukit-core-g04d9b-content-stage-contract-resolution-2026-07-23.zh-CN.md)
for the exact decision and pending G4 verification boundary.

### G-04D9C Engine Filesystem and Output Graph

G-04D9C atomically narrows `DirectoryCopy`, `DirectoryCopyOptions`,
`FileWriter`, `Incremental.HashUtil`, `IOutputFileSystem`,
`IOutputPathPolicy`, `OutputPathSecurityException`,
`SafeOutputFileSystem`, and `SafePathResolver` from public to internal in
2.0. All nine types remain present with their existing member signatures.

The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.
It covers 14 assemblies. The immutable historical manifest remains
`closed / 136 / 136` with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`.

This task changes only CLR accessibility. F-01/F-03/F-04 destructive guards,
symlink/reparse policy, copy/prune/hash behavior, collision-before-write,
diagnostic codes and direct-write semantics remain unchanged.
`OutputDestinationIdentityComparer` continues to be shared by
`AssetOutputPlan` and `BuildManifestTracker`; no second comparer, path tool,
atomic-writer claim, config/schema/protocol change, Labs, or external plugin
change is introduced. See the
[G-04D9C decision ledger](../../docs/analysis/bukit-core-g04d9c-output-filesystem-resolution-2026-07-23.zh-CN.md).

### G-04D9D Engine Feed, SEO, and Sitemap Graph

G-04D9D internalizes `AtomFeedGenerator`, `JsonFeedGenerator`,
`SitemapGenerator` and its `Alternate`/`UrlEntry` records,
`SeoAlternatesService`, and `SeoInjectionPolicy`. `RssGenerator` remains
public and is reclassified as
`cross-assembly-implementation / 1.x-do-not-narrow` because its stable public
nested `RssGenerator.Post` companion must remain reachable.

The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.
The historical manifest remains `closed / 136 / 136` with blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`.

Only CLR accessibility changes. RSS/Atom/JSON Feed and sitemap bytes, URL
canonicalization, ordering, locale alternates, SEO injection and JSON Feed
safe destination resolution remain unchanged. SEO audit still does not fetch
true external images and continues to emit `seo.og_image_external_unverified`
and `seo.twitter_image_external_unverified`. See the
[G-04D9D decision ledger](../../docs/analysis/bukit-core-g04d9d-feed-seo-sitemap-resolution-2026-07-24.zh-CN.md).

### G-04D9E Engine Built-in Plugin Graph

G-04D9E internalizes 13 built-in implementation classes. The ownership model
remains explicit: 9 registry-owned candidates are created by
`BuiltInPluginSource`, while Feed, LLMs.txt, SearchIndex, and Sitemap are
4 aggregate-only implementations. `AnalyticsPlugin` remains the noncandidate
registry entry.

The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.
The historical manifest remains `closed / 136 / 136` with blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`.

Only class accessibility changes. Registration order, plugin names/versions,
interfaces, hooks, capabilities, reports and output ownership remain
unchanged; no reflection, dynamic assembly loading, process-plugin protocol,
Labs or external plugin code is added or modified. See the
[G-04D9E decision ledger](../../docs/analysis/bukit-core-g04d9e-built-in-plugin-resolution-2026-07-24.zh-CN.md).

### G-04D9F Engine Notion Fetch Integration

G-04D9F atomically internalizes `INotionPageFetcher` and
`NotionFetchedPage` after `PagesIndexPlugin` became internal. The current
public API baseline contains 425 types, including 0 `2.0-candidate` entries.
The historical manifest remains `closed / 136 / 136` with unchanged blob.

The interface/record shape, default Notion adapter, pagination, cancellation,
cache and PagesIndex projection remain unchanged. No second Notion client,
schema, config, Labs or external plugin change is introduced. See the
[G-04D9F decision ledger](../../docs/analysis/bukit-core-g04d9f-notion-fetch-resolution-2026-07-24.zh-CN.md).

### G-04D9G Engine Plugin Source and Capability Graph

G-04D9G internalizes `BuiltInPluginSource`, `IPluginSource`, and
`PluginCapability`. The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.
The historical manifest and blob remain unchanged.

`PluginRegistry.GetAllPlugins` keeps its stable `IBukitPlugin/string` tuple
contract, static registration and capability strings. CG-019 continues to
forbid dynamic assembly plugins under the Native AOT boundary; process
plugins remain isolated. See the
[G-04D9G decision ledger](../../docs/analysis/bukit-core-g04d9g-plugin-source-capability-resolution-2026-07-24.zh-CN.md).

### G-04D9H Engine List and Template Capability Graph

G-04D9H internalizes only `SpecialListRouteBuilder`.
`ListPageContentResolution`, `TemplateCapabilityFlags`,
`TemplateFieldDeclaration`, and `TemplateVariableWarning` remain public and
are reclassified as
`cross-assembly-implementation / 1.x-do-not-narrow` because stable public
resolver/linter methods expose them.

The current public API baseline contains 425 types, including 0 `2.0-candidate` entries.
The historical 136-entry manifest remains immutable with unchanged blob.

List/taxonomy routing, route precedence, template field detection and warning
text remain unchanged. Parent facade redesign is outside G-04D9H. See the
[G-04D9H decision ledger](../../docs/analysis/bukit-core-g04d9h-list-template-capability-resolution-2026-07-24.zh-CN.md).
