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

The current public API baseline contains 507 types, including 103 `2.0-candidate` entries.
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
