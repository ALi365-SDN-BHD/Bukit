# Bukit Core 2.0 Public Surface Consumer Declaration

Status: `closed`

Target: `2.0.0`

## What This List Means

The closed manifest preserves the 136-type review inventory. It records
CLR-visible types whose governed compatibility classification is
`2.0-candidate`. At declaration-window closure, all 136 entries were review candidates rather than removal decisions.
Inclusion means only that the type may be examined during a separately approved
2.0 compatibility review. G-04C was the first authorized 2.0 removal decision;
at that point, the other 135 candidates were not batch-approved. G-04D1A was a
later independent 2.0 removal decision; the current baseline has the other 133
candidates not batch-approved.

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
current baseline has the other 133 candidates not batch-approved. Both decisions
are 2.0-only and leave all 1.x CLR visibility unchanged.

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
G-04D1A was a later independent 2.0 removal decision; the current baseline has
the other 133 candidates not batch-approved. It follows the historical G-04C
state, where `Bukit.Engine.RouteInventoryInspectEntry` was removed and the other
135 candidates were not batch-approved. It does not authorize a batch change to
the remaining candidates and leaves all 1.x CLR visibility unchanged.

The canonical replacements are `Bukit.Notion.Rendering.NotionColorPalette`
and `Bukit.Notion.Rendering.NotionRichTextRenderer`. The
[G-04D1A decision ledger](../analysis/bukit-core-g04d1a-static-notion-facade-removal-2026-07-22.zh-CN.md)
records the source and binary breaking-change boundary and migration.
Completed cross-boundary validation and independent review evidence is recorded
there. The closed 136-entry manifest remains immutable;
it intentionally retains both historical candidate records and their
private-consumer uncertainty.
