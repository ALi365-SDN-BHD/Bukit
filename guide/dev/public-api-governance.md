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

## Check

`bash scripts/checks/public-api-drift.sh check Release`

The check compares the compiled public and protected surfaces with
`docs/governance/bukit-core-public-api-baseline.v1.json`. It is a
maintainer-local governance tool, not a general CLR SDK declaration.
Both `check` and `snapshot` require the exact policy-owned, ordered mapping of
the twelve Core assemblies to their projects before any assembly is captured.

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
snapshots. The current public API baseline contains 514 types, including 110 `2.0-candidate` entries.
This 2.0 decision does not change any 1.x CLR
visibility.

See the [G-04D1B decision ledger](../../docs/analysis/bukit-core-g04d1b-block-renderer-facade-removal-2026-07-23.zh-CN.md)
for the exact removal set, canonical migration, preserved D1C surface, Task 1
owner checks, and completed G-04D1B cross-boundary verification. Completed cross-boundary validation and independent review evidence is recorded there. The
parent aggregate gate and final aggregate review remain pending and are not
claimed by the G-04D1B ledger.
