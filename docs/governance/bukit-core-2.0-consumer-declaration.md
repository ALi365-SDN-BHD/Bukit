# Bukit Core 2.0 Public Surface Consumer Declaration

Status: `closed`

Target: `2.0.0`

## What This List Means

The closed manifest preserves the 136-type review inventory. It records
CLR-visible types whose governed compatibility classification is
`2.0-candidate`. At declaration-window closure, all 136 entries were review candidates rather than removal decisions.
Inclusion means only that the type may be examined during a separately approved
2.0 compatibility review. G-04C was the first authorized 2.0 removal decision;
at that decision, the other 135 candidates were not batch-approved. G-04D1A was
a later independent 2.0 removal decision; immediately after that decision, the
other 133 candidates were not batch-approved. Both counts are historical
post-decision states.

C# `public` visibility does not by itself make these types a supported Bukit
Core SDK. Bukit's supported product contracts remain the documented CLI,
configuration, theme, template, report, and `bukit-plugin-v1` process-protocol
surfaces.

## Current 1.x Compatibility Position

The 1.x visibility of every listed type remains unchanged. This preparation
does not deprecate a type, narrow its access, alter a CLR signature, or change
any supported product contract.

Any future compatibility change requires its own reviewed major-version
decision. Nothing in this declaration authorizes a change in a 1.x release.

## Candidate Inventory

The complete machine-readable inventory is
[Bukit Core 2.0 public surface candidates](bukit-core-2.0-public-surface-candidates.v1.json).
It records the governed identity, owner, review state, and authenticated
read-only public-search evidence for each candidate.

A `no-public-match-found` search result means only that the recorded public
queries did not reveal a reviewed external match. Public code search cannot
observe private repositories, unindexed code, or consumers who have not
voluntarily declared their use. Private-consumer status therefore remains
unknown until voluntary declaration.

## Historical Feedback Channel

The declaration channel was
[GitHub Issue #60](https://github.com/ALi365-SDN-BHD/Bukit/issues/60) in
`ALi365-SDN-BHD/Bukit`. Its observed close event was
`2026-07-22T07:08:31Z`. The Issue is closed and is retained as the historical
feedback record. Its instructions requested the exact type, usage pattern,
Bukit version range, and any reflection, serialization, Native AOT,
inheritance, or cross-assembly dependency involved; credentials, private
source, and other secrets were not to be posted.

New evidence must be handled through a separately opened consumer-declaration
channel or task; it must not be added to the closed Issue as a substitute for a
new governed review.

## Closed Lifecycle And Eligibility Boundary

GitHub Issue #60 opened the declaration window at `2026-07-21T02:19:46Z` and
was observed closed at `2026-07-22T07:08:30Z`, with the close event at
`2026-07-22T07:08:31Z`. The eligible stable release is `v1.0.10`.

At declaration-window closure, all 136 entries were recorded as
`consumer-declaration-pending`, and every private-consumer status was recorded
as `unknown-until-voluntary-declaration`. That historical closure record does
not prove that private consumers do not exist.

The declaration-window closure permitted only G-04C eligibility discussion; it
did not itself authorize a candidate change. G-04C was the first authorized
2.0 removal decision; at that point, the other 135 candidates were not
batch-approved. G-04D1A was a later independent 2.0 removal decision; the
other 133 candidates were not batch-approved immediately after that decision.
These are historical states; both decisions are 2.0-only and leave all 1.x CLR
visibility unchanged.

## What Happens When A Consumer Is Found

Evidence of external use stops that type from being treated as an apparent
zero-consumer candidate. Maintainers must review the exact dependency,
including reflection, serializers, Native AOT, protected members, and public
signatures that may propagate the type.

The resulting path may be continued retention, a supported facade and
migration period, or a separately reviewed obsolete path. The evidence and
migration consequences must be resolved before that type can be reconsidered
for G-04C eligibility.

## Explicit Non-Claims

- At declaration-window closure, this closed declaration lifecycle did not
  approve any candidate for a compatibility change.
- At that time, none of the 136 candidates had been approved for deprecation,
  access narrowing, or removal.
- Public-search results do not establish the absence of private, unindexed, or
  undisclosed consumers.
- Closing G-04B3 does not authorize G-04C.

## G-04C Single-Type Decision

Historical G-04C single-type decision: only `Bukit.Engine.RouteInventoryInspectEntry`
was approved for removal in 2.0; at that point, the other 135 candidates were
not batch-approved.
The [G-04C decision ledger](../analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md)
records the exact drift, migration boundary, verification, and independent review.

The closed 136-entry candidate manifest remains the immutable historical cohort
captured at declaration-window closure. The current public API baseline is the
source of truth for the post-removal CLR surface.

## G-04D1A Two Static Facades

G-04D1A two-static-facade decision: only `Bukit.Content.Notion.NotionColorPalette` and `Bukit.Content.Notion.NotionRichTextRenderer` are approved for removal in 2.0; the other 133 candidates are not batch-approved.
G-04D1A was a later independent 2.0 removal decision. The 133-candidate
remainder was the historical state immediately after that decision. It followed
the historical G-04C state, where
`Bukit.Engine.RouteInventoryInspectEntry` was removed and the other 135
candidates were not batch-approved. At the time, G-04D1A did not authorize a
batch change to those 133 remaining candidates, and it leaves all 1.x CLR
visibility unchanged.

The canonical replacements are `Bukit.Notion.Rendering.NotionColorPalette`
and `Bukit.Notion.Rendering.NotionRichTextRenderer`. The
[G-04D1A decision ledger](../analysis/bukit-core-g04d1a-static-notion-facade-removal-2026-07-22.zh-CN.md)
records the source and binary breaking-change boundary and migration.
Completed cross-boundary validation and independent review evidence is recorded
there. The closed 136-entry manifest remains immutable;
it intentionally retains both historical candidate records and their
private-consumer uncertainty.

## G-04D1B Block Renderer Facades

G-04D1B block-renderer-facade decision: only the 23 `Bukit.Content.Notion.BlockRenderers` facade types recorded in the G-04D1B ledger are approved for removal in 2.0; the other 110 candidates are not batch-approved.

Their canonical namespace is `Bukit.Notion.Rendering.BlockRenderers`. The
closed 136-entry candidate manifest remains the immutable historical cohort;
the G-04C 135-candidate and G-04D1A 133-candidate statements remain historical
snapshots. Immediately after G-04D1B, the public API baseline contained 514
types, including 110 `2.0-candidate` entries.
This 2.0 decision does not change any 1.x CLR
visibility.

The [G-04D1B decision ledger](../analysis/bukit-core-g04d1b-block-renderer-facade-removal-2026-07-23.zh-CN.md)
records the exact identities, source and binary migration boundary, preserved
D1C surface, Task 1 owner checks, and completed G-04D1B cross-boundary
verification. Completed cross-boundary validation and independent review evidence is recorded there. The parent aggregate gate and final aggregate review remain
pending and are not claimed by the G-04D1B ledger.

## G-04D1C-M2 Notion Extension Graph

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

The [G-04D1C-M2 decision ledger](../analysis/bukit-core-g04d1c-m2-five-type-atomic-removal-2026-07-23.zh-CN.md)
records the deliberate approval, exact removal set, migration boundary,
verification evidence, and independent review status. This decision does not
authorize removal of `NotionApiClient`, `NotionProviderOptions`, or
`NotionClientStats`.

## G-04D2A Plugin Secret Masker

G-04D2A single-type internalization decision: only `Bukit.PluginHost.PluginSecretMasker` is narrowed from public to internal in 2.0; the other 104 candidates are not batch-approved.

At the G-04D2A decision, the public API baseline contained 508 types,
including 104 `2.0-candidate` entries.
It covers 14 assemblies. The closed 136-entry candidate manifest remains the
immutable historical cohort, including the original `PluginSecretMasker`
entry and its `unknown-until-voluntary-declaration` private-consumer status.
Public search found no reviewed external match, but private, unindexed, or
undisclosed direct CLR consumers remain unknown until voluntary declaration.

This 2.0-only access narrowing is source and binary breaking for any
undisclosed direct CLR consumer of the helper. No replacement API is needed:
the supported external plugin surface is the `bukit-plugin-v1` process
protocol, not this same-assembly masking helper. The decision preserves
masking behavior and excludes general URL cleaning, protocol or report-shape
changes, and every other `Bukit.PluginHost` candidate.

The [G-04D2A decision ledger](../analysis/bukit-core-g04d2a-plugin-secret-masker-internalization-2026-07-23.zh-CN.md)
records the exact one-token source change, governed baseline delta, consumer
and Native AOT evidence boundary, exclusions, stop conditions, and task-level
verification.

## G-04D2B2 Plugin Host Error Codes

G-04D2B2 single-type internalization decision: only `Bukit.PluginHost.PluginHostErrorCodes` is narrowed from public to internal in 2.0; the other 103 candidates are not batch-approved.

The current public API baseline contains 443 types, including 0 `2.0-candidate` entries.
It covers 14 assemblies. The closed 136-entry candidate manifest remains
immutable with Git blob `7b07d6890562387010b52301e9f8716e9bf10ed1`; private
consumers remain `unknown-until-voluntary-declaration`. The 2026-07-22
authenticated public search found no public match, and no new governance-grade
GitHub Code Search was available on 2026-07-23.

Ordinary const-consuming binaries may retain inlined values, but source
recompilation and public metadata/reflection consumers are breaking in 2.0.
The six vocabulary strings and five runtime Host behaviors remain unchanged.
No other `Bukit.PluginHost` candidate is approved.

The [G-04D2B2 decision ledger](../analysis/bukit-core-g04d2b2-plugin-host-error-codes-internalization-2026-07-23.zh-CN.md)
records the exact visibility narrowing, governed delta, qualification boundary,
and exclusions.

## G-04D3B Notion Client Stats

G-04D3B removes only the duplicate
`Bukit.Content.Notion.NotionClientStats` CLR identity in 2.0. The internal
legacy `NotionApiClient.GetStats()` facade now returns the canonical
`Bukit.Notion.Transport.NotionClientStats`; the other 84 candidates are not
batch-approved.

The current public API baseline contains 443 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`. Authenticated public search found
no reviewed match for the removed full name, but private, unindexed, and
undisclosed direct consumers remain unknown.

This 2.0 migration is source, binary, and reflection breaking for direct
consumers of the legacy CLR identity. It does not change request/throttle
counters, retry, rate limits, Notion API behavior, transport lifetime, or
public `NotionApiClient` members. The
[G-04D3B decision ledger](../analysis/bukit-core-g04d3b-notion-client-stats-resolution-2026-07-23.zh-CN.md)
records the exact replacement and G2 verification boundary.

## G-04D4A Shared Notion Graph

G-04D4A retains all 13 `Bukit.Shared.Notion` model/record identities as public
companion types of `HtmlToNotionBlockConverter.Convert(string)` and
reclassifies them as
`cross-assembly-implementation / 1.x-do-not-narrow`. This does not authorize
their unconditional removal in 2.0.

The duplicate Shared `HtmlTokenizer`, `HtmlToken`, and `HtmlTokenType`
identities are removed atomically. Direct CLR consumers must migrate to
`Bukit.Notion.Conversion.HtmlTokenizer` and its canonical nested types. The
canonical enum ordinals, token defaults, parsing behavior, and exception
behavior remain unchanged.

The current public API baseline contains 443 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private, unindexed, and
undisclosed consumers remain `unknown-until-voluntary-declaration`. The
[G-04D4A decision ledger](../analysis/bukit-core-g04d4a-shared-notion-graph-resolution-2026-07-23.zh-CN.md)
records the exact compatibility and G2 verification boundary.

## G-04D4B Value Coercion

G-04D4B narrows only `Bukit.Shared.ValueCoercion` from public to internal in
2.0. No repository production consumer or runtime metadata root requires the
public identity, the existing `Bukit.Shared.Tests` friend boundary is
unchanged, and no replacement or global conversion abstraction is added.

The current public API baseline contains 443 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private, unindexed, and
undisclosed consumers remain `unknown-until-voluntary-declaration`.

This 2.0-only narrowing is source, binary, and reflection breaking for any
direct CLR consumer. Null, boolean, whitelist casing, whitespace, number,
current culture, custom `ToString`, fallback, and exception propagation
semantics remain unchanged. The
[G-04D4B decision ledger](../analysis/bukit-core-g04d4b-value-coercion-resolution-2026-07-23.zh-CN.md)
records the exact change and G2 verification boundary.

## G-04D5A CLI Parse Graph

G-04D5A narrows `CliBoundCommandFactory`, `SimpleParseResult`, and
`SubcommandParseResult` from public to internal in 2.0. `CliParseResult`
remains public and is reclassified as
`cross-assembly-implementation / 1.x-do-not-narrow`: it is the public return
type of `CliParser.Parse`, the public input of
`CommandDescriptor.DispatchAsync`, and an externally derivable record.

The current public API baseline contains 443 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private, unindexed, and
undisclosed consumers remain `unknown-until-voluntary-declaration`.

Binding, recursive parse, diagnostic order, dispatch, command tree, stderr,
and exit codes remain unchanged. No CLI Shared friend assembly is added. The
[G-04D5A decision ledger](../analysis/bukit-core-g04d5a-cli-parse-graph-resolution-2026-07-23.zh-CN.md)
records the exact compatibility and G2 verification boundary.

## G-04D5B CLI Error Payload

G-04D5B narrows only
`CliErrorRenderer.CliErrorPayload` from public nested record to internal
nested record in 2.0. `CliErrorRenderer`, `CliErrorDiagnostic`, and all public
`RenderJson` overloads remain public; the supported external contract is the
rendered JSON envelope, not the implementation DTO identity.

The current public API baseline contains 443 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private, unindexed, and
undisclosed consumers remain `unknown-until-voluntary-declaration`.

Source-generated serialization, JSON names/order/indentation, null omission,
defaults, escaping, stdout/stderr routing, usage, and exit codes remain
unchanged. The
[G-04D5B decision ledger](../analysis/bukit-core-g04d5b-cli-error-payload-resolution-2026-07-23.zh-CN.md)
records the exact compatibility and G2 verification boundary.

## G-04D6A Rendering File Template Loader

G-04D6A narrows only
`Bukit.Rendering.Scriban.FileTemplateLoader` from public to internal in 2.0.
The type remains sealed and continues to implement Scriban's
`ITemplateLoader` with the same constructor and three interface methods.
`ScribanTemplateRenderer` remains the public Rendering entry point.

The current public API baseline contains 443 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private, unindexed, and
undisclosed consumers remain `unknown-until-voluntary-declaration`.

Override, child, and parent fallback order, missing-template primary path,
path safety, sync/async loading, cache signatures, exceptions, and Scriban
interface dispatch remain unchanged. No friend assembly is added. The
[G-04D6A decision ledger](../analysis/bukit-core-g04d6a-file-template-loader-resolution-2026-07-23.zh-CN.md)
records the exact change and G3 verification boundary.

## G-04D6B Rendering Scriban Model Binder

G-04D6B narrows only
`Bukit.Rendering.Scriban.ScribanModelBinder` from public to internal in 2.0.
The static facade and both `PageModel`/`ListPageModel` overloads remain in
place, and `ScribanTemplateRenderer` keeps both direct static call roots.

The current public API baseline contains 443 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private, unindexed, and
undisclosed consumers remain `unknown-until-voluntary-declaration`.

Explicit template keys, aliases, null handling, read-only and mutable
dictionary projection, list projection, unsupported-object `ToString`
fallback, and exception propagation remain unchanged. No reflection mapper,
replacement facade, or friend assembly is added. The
[G-04D6B decision ledger](../analysis/bukit-core-g04d6b-scriban-model-binder-resolution-2026-07-23.zh-CN.md)
records the exact change and G3 verification boundary.

## G-04D7A Routing Route Generation Result

G-04D7A removes the public nested
`Bukit.Routing.RouteGenerator.RouteGenerationResult` record in 2.0 and
changes only `RouteGenerator.GenerateWithSource`'s return carrier to the
named tuple `(RouteInfo Route, RouteSource Source)`. The method name,
parameters, optional defaults, tuple element names, public `RouteSource`
enum, and route/source behavior remain unchanged.

The current public API baseline contains 443 types, including 0
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
root, schema, or protocol is added. The
[G-04D7A decision ledger](../analysis/bukit-core-g04d7a-route-result-resolution-2026-07-23.zh-CN.md)
records the exact migration and G3 verification boundary.

## G-04D8A Theme Validation Graph

G-04D8A narrows only
`Bukit.Theme.SchemaValidationException` from public to internal in 2.0.
`SchemaValidationError` remains public and is reclassified as
`cross-assembly-implementation / 1.x-do-not-narrow` because public
`SectionSchemaValidator.Validate` returns
`List<SchemaValidationError>`.

The current public API baseline contains 443 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private, unindexed, and
undisclosed consumers remain `unknown-until-voluntary-declaration`.

This is a 2.0 source, binary, and reflection break for consumers that catch or
inspect the concrete exception type. The exception still exists internally
with the same string constructor and strict first-error message. The public
validation-error record, validator signature, warn/off results, ordering,
logger text, theme schema and source-generated JSON roots remain unchanged.
No friend assembly is added. The
[G-04D8A decision ledger](../analysis/bukit-core-g04d8a-theme-validation-graph-resolution-2026-07-23.zh-CN.md)
records the exact compatibility and G3 verification boundary.

## G-04D8B Theme Doctor Result

G-04D8B retains the public nested
`Bukit.Theme.ThemeDoctorCommand.DoctorResult` record and reclassifies it as
`cross-assembly-implementation / 1.x-do-not-narrow`. Public
`ThemeDoctorCommand.Diagnose` returns that exact type, and public
`ThemeDoctorCommand.PrintReport` accepts it; narrowing only the companion
record would make those public signatures inconsistent.

The current public API baseline contains 443 types, including 0
`2.0-candidate` entries across 14 assemblies. The closed 136-entry candidate
manifest remains immutable with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`; private, unindexed, and
undisclosed consumers remain `unknown-until-voluntary-declaration`.

The record remains public, nested and sealed with its original
`bool / bool / List<string>` constructor, mutable `Issues` list and record
reference-equality semantics. Diagnose flags, issue order and Theme doctor
text remain unchanged. Core CLI doctor remains a separate text and integer
exit-code pipeline, and `DoctorResult` is not added to a JSON source-generation
root or Native AOT reflection root. No Theme, CLI, Labs, plugin, schema,
protocol, config or persisted-format production code is changed. The
[G-04D8B decision ledger](../analysis/bukit-core-g04d8b-theme-doctor-result-resolution-2026-07-23.zh-CN.md)
records the exact retained decision and pending G3 verification boundary.

## G-04D9A Engine Build Orchestration Graph

G-04D9A internalizes only `Bukit.Engine.BuildPipeline`,
`Bukit.Engine.BuildPipelineContext`, `Bukit.Engine.RoutePipeline`, and
`Bukit.Engine.RoutePipelineResult` in 2.0. These implementation graph types
remain present inside `Bukit.Engine`; their existing constructor, member,
record, cancellation, route, and result behavior is not replaced.

`BuildOptions`, `BuildVariantSummary`, and `ContentPipelineResult` remain
public and are reclassified as
`cross-assembly-implementation / 1.x-do-not-narrow`. Public
`SiteEngine.BuildAsync(IContentProvider, BuildOptions, ...)`,
`BuildResult.Variants`, and `ContentPipeline.ExecuteAsync(...)` keep their
existing exact types and signatures.

The current public API baseline contains 443 types, including 0 `2.0-candidate` entries.
It covers 14 assemblies. The closed 136-entry candidate manifest remains
immutable with Git blob `7b07d6890562387010b52301e9f8716e9bf10ed1`;
private, unindexed, and undisclosed consumers remain
`unknown-until-voluntary-declaration`.

This approved 2.0 narrowing breaks direct CLR references to the four
internalized types. Consumers must use the retained `SiteEngine`,
`BuildResult`, or `ContentPipeline` public contracts. No public parent
overload, build report, route output, config, schema, plugin protocol,
path/security behavior, friend assembly, Labs, or external plugin code is
modified. The
[G-04D9A decision ledger](../analysis/bukit-core-g04d9a-build-orchestration-resolution-2026-07-23.zh-CN.md)
records the exact compatibility decision and pending G4 verification boundary.

## G-04D9B Engine Content Validation and Stage Contracts

G-04D9B internalizes only `ContentCollectionContractValidator` and
`ContentSchemaValidator` in 2.0. Both static implementation types remain
inside `Bukit.Engine`; collection requirements, schema validation rules,
fail-mode precedence, error ordering, diagnostic codes, and messages remain
unchanged.

`ContentValidationIssue`, `IContentProviderFactory`, `ITemplateRenderer`,
`ContentStageInput`, `ContentStageOutput`, `IContentStage`, and
`TemplateRendererBase` remain public and are reclassified as
`cross-assembly-implementation / 1.x-do-not-narrow`. Existing public
projection methods, `ContentPipeline` constructors, stage signatures,
provider implementation and renderer protected inheritance surface continue
to expose these exact types.

The current public API baseline contains 443 types, including 0 `2.0-candidate` entries.
It covers 14 assemblies. The closed 136-entry candidate manifest remains
immutable with Git blob `7b07d6890562387010b52301e9f8716e9bf10ed1`;
private, unindexed, and undisclosed consumers remain
`unknown-until-voluntary-declaration`.

Direct CLR consumers of either validator must migrate to the retained public
pipeline and schema-projection entry points. This task does not narrow
`ContentPipeline`, stage injection, provider composition, `ITemplateRenderer`,
or `TemplateRendererBase`; no replacement seam or friend assembly is added.
No config, schema semantics, plugin protocol, Labs, or external plugin code is
modified. The
[G-04D9B decision ledger](../analysis/bukit-core-g04d9b-content-stage-contract-resolution-2026-07-23.zh-CN.md)
records the exact compatibility decision and pending G4 verification boundary.

## G-04D9C Engine Filesystem and Output Graph

G-04D9C atomically internalizes `DirectoryCopy`, `DirectoryCopyOptions`,
`FileWriter`, `Incremental.HashUtil`, `IOutputFileSystem`,
`IOutputPathPolicy`, `OutputPathSecurityException`,
`SafeOutputFileSystem`, and `SafePathResolver` in 2.0. Their member
signatures and static Engine call graph remain present.

The current public API baseline contains 443 types, including 0 `2.0-candidate` entries.
It covers 14 assemblies. The immutable historical manifest remains
`closed / 136 / 136` with Git blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`.

Direct CLR consumers of these nine implementation types must migrate to the
supported build and plugin contracts. The task does not alter destructive
clean authority, path comparison, symlink/reparse handling, copy/prune/hash,
collision diagnostics, manifest ownership or direct-write behavior.
`OutputDestinationIdentityComparer` remains the single comparer used by
`AssetOutputPlan` and `BuildManifestTracker`. No schema, protocol, global
path utility, Labs, or external plugin code is modified. See the
[G-04D9C decision ledger](../analysis/bukit-core-g04d9c-output-filesystem-resolution-2026-07-23.zh-CN.md).

## G-04D9D Engine Feed, SEO, and Sitemap Graph

G-04D9D internalizes `AtomFeedGenerator`, `JsonFeedGenerator`,
`SitemapGenerator` and its two nested records, `SeoAlternatesService`, and
`SeoInjectionPolicy`. `RssGenerator` and its stable public nested `Post`
record remain public; the outer type is reclassified as
`cross-assembly-implementation / 1.x-do-not-narrow`.

The current public API baseline contains 443 types, including 0 `2.0-candidate` entries.
The historical manifest remains `closed / 136 / 136` with blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`.

Direct CLR consumers of the seven internalized types must use retained build
and feed entry points. Output bytes, URLs, ordering, locale alternates, safe
JSON Feed destination handling and SEO network boundary do not change.
External images remain un-fetched and produce
`seo.og_image_external_unverified` /
`seo.twitter_image_external_unverified`. See the
[G-04D9D decision ledger](../analysis/bukit-core-g04d9d-feed-seo-sitemap-resolution-2026-07-24.zh-CN.md).

## G-04D9E Engine Built-in Plugin Graph

G-04D9E internalizes 13 built-in plugin implementation classes. Nine are
registry-owned and continue to be created by `BuiltInPluginSource`; Feed,
LLMs.txt, SearchIndex, and Sitemap remain four aggregate-only implementations.
The noncandidate `AnalyticsPlugin` remains registered.

The current public API baseline contains 443 types, including 0 `2.0-candidate` entries.
The historical manifest remains `closed / 136 / 136` with blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`.

Direct CLR construction of the 13 classes is no longer supported in 2.0.
Stable `Bukit.Engine.Abstractions` plugin contracts, registration order,
names/versions, hooks, capabilities, reports and output ownership remain
unchanged. No dynamic CLR plugin SDK, process protocol, Labs, or external
plugin code is modified. See the
[G-04D9E decision ledger](../analysis/bukit-core-g04d9e-built-in-plugin-resolution-2026-07-24.zh-CN.md).

## G-04D9F Engine Notion Fetch Integration

G-04D9F atomically internalizes `INotionPageFetcher` and
`NotionFetchedPage` after their only production owner, `PagesIndexPlugin`,
became internal. The current public API baseline contains 443 types, including 0 `2.0-candidate` entries.
The historical manifest remains `closed / 136 / 136` with unchanged blob
`7b07d6890562387010b52301e9f8716e9bf10ed1`.

Direct CLR implementations of the interface are no longer supported in 2.0.
The existing adapter, pagination, cancellation, cache and PagesIndex output
remain unchanged; no second Notion client, Labs or external plugin code is
added. See the
[G-04D9F decision ledger](../analysis/bukit-core-g04d9f-notion-fetch-resolution-2026-07-24.zh-CN.md).

## G-04D9G Engine Plugin Source and Capability Graph

G-04D9G internalizes `BuiltInPluginSource`, `IPluginSource`, and
`PluginCapability`. The current public API baseline contains 443 types, including 0 `2.0-candidate` entries.
The historical manifest and blob
`7b07d6890562387010b52301e9f8716e9bf10ed1` remain unchanged.

`PluginRegistry.GetAllPlugins` continues to expose only the stable
`IBukitPlugin/string` tuple. Static registration, `emit-outputs` /
`derive-pages` strings, CG-019 Native AOT boundary and process-plugin
isolation remain unchanged. See the
[G-04D9G decision ledger](../analysis/bukit-core-g04d9g-plugin-source-capability-resolution-2026-07-24.zh-CN.md).

## G-04D9H Engine List and Template Capability Graph

G-04D9H internalizes only `SpecialListRouteBuilder`.
`TemplateCapabilitiesResolver.ListPageContentResolution`,
`TemplateCapabilityFlags`, `TemplateFieldDeclaration`, and
`TemplateVariableWarning` remain public and are reclassified as
`cross-assembly-implementation / 1.x-do-not-narrow`.

The current public API baseline contains 443 types, including 0 `2.0-candidate` entries.
The historical 136-entry manifest and blob
`7b07d6890562387010b52301e9f8716e9bf10ed1` remain unchanged.

The four companion types stay exposed by stable resolver/linter parent
methods. List/taxonomy routing, route precedence, template fields and warning
text remain unchanged; parent facade redesign requires a separate migration
task. See the
[G-04D9H decision ledger](../analysis/bukit-core-g04d9h-list-template-capability-resolution-2026-07-24.zh-CN.md).
