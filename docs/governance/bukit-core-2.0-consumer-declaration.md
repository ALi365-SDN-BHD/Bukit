# Bukit Core 2.0 Public Surface Consumer Declaration

Status: `closed`

Target: `2.0.0`

## What This List Means

Bukit has prepared a review inventory of 136 CLR-visible types whose governed
compatibility classification is `2.0-candidate`. All 136 entries are review
candidates, not removal decisions. Inclusion means only that the type may be
examined during a separately approved 2.0 compatibility review.

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

All 136 entries remain `consumer-declaration-pending`, and every
private-consumer status remains `unknown-until-voluntary-declaration`. Closure
does not prove that private consumers do not exist.

Closure permits only G-04C eligibility discussion. It does not authorize a
candidate change: G-04C remains separately authorized, 2.0-only, and limited
to a single-type decision. All 1.x CLR visibility remains unchanged.

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

- This closed declaration lifecycle does not approve any candidate for a
  compatibility change.
- The 136 candidates are not approved for deprecation, access narrowing, or
  removal.
- Public-search results do not establish the absence of private, unindexed, or
  undisclosed consumers.
- `Bukit.Engine.RouteInventoryInspectEntry` remains
  `consumer-declaration-pending / review-only`; its inclusion does not authorize
  G-04C work.
- Closing G-04B3 does not authorize G-04C.
