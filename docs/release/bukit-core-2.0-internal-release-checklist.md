# Bukit Core 2.0.7 Release Checklist And Evidence Record

> Core remains enterprise internal-first (Route 3); regular public binary
> releases remain paused. This record documents the existing exceptional
> public release of 2.0.7, not authorization for another release. Public tags,
> asset uploads and publication require recorded management approval; see
> [release precheck](release-prerelease-template.md) and
> [product positioning](../governance/bukit-core-product-positioning.md).
>
> Passing CI, coverage, security, Native AOT, smoke, and asset checks proves
> technical state only. It is not release authorization.

Evidence reconciled on 2026-11-04 from existing GitHub run metadata, job logs,
artifact metadata and Environment review history. No gate, test, build,
publication or deployment was rerun for this documentation update. Checked
items below identify observed evidence; unchecked items remain unverified.

## 1. Release identity

- [x] Target version: `RELEASE_VERSION=2.0.7`; tag `v2.0.7` resolves to the release commit.
- [x] Release commit: `RELEASE_COMMIT=8fef343045974a64d0897e95911a171d28b90cbb` on `main`.
- [x] Workflow operator: `ClrsDream` (actor and triggering actor); recorded Environment approver: `ClrsDream`, state `approved`, Environment `public-release`. See [release run](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/41730399717) and its [approval history](https://api.github.com/repos/ALi365-SDN-BHD/Bukit/actions/runs/41730399717/approvals).
- [x] Release type: exceptional public release; [Bukit 2.0.7](https://github.com/ALi365-SDN-BHD/Bukit/releases/tag/v2.0.7) is neither draft nor prerelease. Published at `2026-11-04T06:39:34Z` (`2026-11-04 14:39:34 +08:00`).
- [x] `Directory.Build.props` product version is `2.0.7`.
- [ ] Independently verify the approver's management designation and all required Environment controls (authorized reviewer, prevent self-review, no admin bypass, `main` only). Approval history reports `can_admins_bypass=false`; it does not establish the other controls. The operator and approving account are the same, so this record does not certify compliance with the self-review requirement.

The local documentation-update base is `583ad56575a26b4d7ee6a920f166e76357153e7d`.
Its tree and the release commit's tree are identical:
`f25804a85ec38e66116b3f4eadf27ee96682b966`. Execution evidence below belongs to
the release commit, not to a new commit containing this documentation update.

## 2. Documentation

- [x] This documentation update adds dated `[2.0.7]` sections to `CHANGELOG.md`, `CHANGELOG.zh-CN.md` and `CHANGELOG.ms.md`, moving the shipped version, security self-test and release-doc changes out of Unreleased. These corrections were not present in the original release commit.
- [x] Release assets are the three Core archives plus `checksums.json`, `checksums.txt` and `release-manifest.json`. The separate IndexNow Core/plugin internal install manifest is outside this public asset set; the historical 2.0.6 changelog entry remains internal-only.
- [x] `README*.md` retain the enterprise internal-first status and paused regular-public-release policy.
- [x] Public API baseline (`docs/governance/bukit-core-public-api-baseline.v1.json`): the release Fast gate ran the public API drift check successfully; see [Fast gate](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/41730399717/job/126245965223).

## 3. Evidence on the release commit

These are historical execution results on the exact release commit, not a
local `codex-workflow.py cache check` hit. New execution may reuse cached GREEN
only when `HEAD`, closure content, exact command, relevant environment state
and toolchain version match.

- [x] [CI run 41678322335](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/41678322335), final attempt 2: success on the release commit. Fast gate, Architecture contracts, Core tests/coverage, Linux x64, Darwin arm64, Windows x64 specialties and Windows real Git transport jobs succeeded.
- [x] `bash scripts/security/security-regression-self-test.sh`: CI Fast gate's `Check security regression selectors` step succeeded.
- [x] Release [Core tests](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/41730399717/job/126248789983): 13 projects, **7,190 passed / 0 failed / 0 skipped**, using `BUKIT_CI_FULL_SKIP_FAST=1 bash scripts/gates/ci-full.sh Release` after the separate Fast gate.
- [x] Release [Security check](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/41730399717/job/126248789999): **396 passed / 0 failed / 0 skipped** across seven projects. These selectors overlap Core tests; the totals are not distinct tests to add together.
- [x] Release [Core coverage](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/41730399717/job/126250557172): **90.97% line coverage (43,200 / 47,488)**; summary and safety-layer checks succeeded. Logs show generation of `TestResults/coverage/coverage-summary.txt` and `coverage-summary.json`, followed by successful upload of `TestResults/coverage` and `docs/coverage-baselines.json` to [core-coverage artifact 13277120894](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/41730399717/artifacts/13277120894). The ZIP was not independently downloaded or inspected in this reconciliation.
- [ ] Standalone `scripts/gates/release.sh` execution: no execution record established in this review; not rerun. The successful workflow and Core gate above must not be relabeled as an execution of this script. Full/release gates still require explicit user authorization.

## 4. Release workflow and assets

- [x] [Release run 41730399717](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/41730399717), attempt 1: success. Actual run was `workflow_dispatch`, `publish=true`, with all three RIDs (`linux-x64`, `osx-arm64`, `win-x64`); it was not an artifact-only `publish=false` run.
- [x] [Collect release assets](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/41730399717/job/126253223074) logged `release assets prepare OK` and `release assets verify OK`. The successful verification command used the following identity and expected RID set:

```bash
export RELEASE_VERSION="2.0.7"
export RELEASE_COMMIT="8fef343045974a64d0897e95911a171d28b90cbb"
bash scripts/release/verify-release-assets.sh "$RELEASE_VERSION" "$RELEASE_COMMIT" release-assets linux-x64 osx-arm64 win-x64
```

- [x] Native AOT package and `Smoke packaged archive` steps succeeded on [Linux](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/41730399717/job/126251893096), [macOS](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/41730399717/job/126251893043) and [Windows](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/41730399717/job/126251892990). Smoke covers the basic Markdown fixture's config check, clean build and publish audit, not live Notion or a deployed website.
- [x] [Publish GitHub Release](https://github.com/ALi365-SDN-BHD/Bukit/actions/runs/41730399717/job/126253384569) succeeded after Environment approval and release-tag verification, uploading the exact six assets recorded above.
- [ ] Separate install-manifest SHA-256 comparison: not applicable to this six-asset public release; no install manifest is published in this asset set. No assertion is made about a separate internal/plugin install manifest.

Published archive SHA-256 values below are GitHub Release asset metadata.
Workflow asset verification passed; no local download or rehash was performed.

| Asset | SHA-256 |
|---|---|
| `bukit-2.0.7-linux-x64.tar.gz` | `8b5249fa065bf950e781e58513e158093fe785c2b3d40f1308b850f32f8d02e5` |
| `bukit-2.0.7-osx-arm64.tar.gz` | `399571370a6dc0b7a79f9b9c8b584fcdfde2b37c48c649486349d2c4d1cb7557` |
| `bukit-2.0.7-win-x64.zip` | `ffb998bf448a9d9183925ad5d9e471919731aa52bd541772ac36f6944877a093` |

## 5. Go / no-go

- [x] Technical release evidence is GREEN for the recorded commit and selected three-platform Core artifacts; publication has already completed.
- [ ] Unconditional sign-off that no Critical or Important finding remains: not established. This review found no new Core runtime blocker, but the Environment authorization controls above remain unverified and real-site acceptance is separate.
- [x] Rollback reference recorded: previous tag `v2.0.6`, commit `4268648b4d81a28018e9e63dc021b782820477d4`. Preserve the previous known-good site artifact when adopting 2.0.7; restoring that artifact has not been rehearsed here.
- [x] Existing GitHub Environment approval recorded: `ClrsDream`, state `approved`, `public-release`, observed on 2026-11-04. The API response did not include the review timestamp; publication time is recorded in section 1.
- [ ] Additional management or business-site acceptance sign-off: not supplied or inferred by this documentation update.

## Remaining verification boundaries

- Version sections for 2.0.0 to 2.0.4 were derived from commit subjects; an explicit owner review of those historical sections has not been established.
- This record does not certify a live Notion workspace, a current business site's theme/content, browser or online deployment behavior, rollback rehearsal, crash/power-loss recovery, Labs, external plugins or commercial/public support readiness.
- Future releases continue to require their own release identity, applicable technical evidence and management approval. Existing success records are not authorization to rerun or publish anything.
