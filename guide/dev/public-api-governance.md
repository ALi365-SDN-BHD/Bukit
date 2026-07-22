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
record the current declaration state as `open`. The window opened at
`2026-07-21T02:19:46Z`, and
[GitHub Issue #60](https://github.com/ALi365-SDN-BHD/Bukit/issues/60) is the
canonical announcement and feedback channel. All 136 candidates remain
review-only; opening the window is not a removal decision.

All 1.x CLR access levels remain unchanged. A `no-public-match-found` result
means only that the recorded public searches found no reviewed external match;
it is not proof that removal is safe and cannot reveal private, unindexed, or
undisclosed consumers.

Calendar time alone cannot close the window. Closure requires at least one later
non-prerelease stable release, disposition of all received feedback, and an
independent evidence audit with no unresolved consumer evidence.
`eligibleAfterRelease` remains unset (`null`) until that later stable release
exists. G-04B2 opens the declaration window only; it does not authorize G-04C.
