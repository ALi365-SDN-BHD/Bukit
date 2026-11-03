# Bukit Core 2.0 Internal Release Checklist

> Scope: internal first official release of Bukit Core 2.x (enterprise
> internal-first, Route 3). This is not a public release. Do not create a public
> tag, upload public assets, or publish a GitHub Release without recorded
> management approval; see `release-prerelease-template.md` and
> `docs/governance/bukit-core-product-positioning.md`.
>
> Passing CI, coverage, security, Native AOT, smoke, and asset checks proves
> technical state only. It is not release authorization.

## 1. Release identity

- [ ] Target version recorded: `RELEASE_VERSION=` (current `Directory.Build.props`: 2.0.7)
- [ ] Release commit recorded: `RELEASE_COMMIT=`
- [ ] Release owner and approver named, with approval reference
- [ ] Release type decided: internal artifact only / exceptional public release
- [ ] Version in `Directory.Build.props` matches the target version

## 2. Documentation

- [ ] `CHANGELOG.md`, `CHANGELOG.zh-CN.md`, `CHANGELOG.ms.md` contain the target version section; `[Unreleased]` items shipped in this release are moved under it
- [ ] Unreleased items that are internal artifacts (for example the IndexNow `osx-arm64` install manifest) are explicitly kept in or out of the release scope
- [ ] `README*.md` product status still matches the positioning document
- [ ] Public API baseline (`docs/governance/bukit-core-public-api-baseline.v1.json`) matches the shipped surface

## 3. Evidence on the release commit

Reuse GREEN evidence only when `HEAD`, closure content, exact command, relevant
environment state, and toolchain version all match (`codex-workflow.py cache check`).

- [ ] `.github/workflows/ci.yaml` completed success on the release commit (Fast gate, Architecture contracts, Core tests, Core coverage, platform specialties)
- [ ] `bash scripts/security/security-regression-self-test.sh` passes (also in the CI fast gate)
- [ ] `Security check` passes in the Release workflow
- [ ] Core coverage artifact contains `coverage-summary.txt`, `coverage-summary.json`, `docs/coverage-baselines.json`
- [ ] Full or release gate (`scripts/gates/release.sh`) run **only with explicit user authorization**; result recorded here: ____

## 4. Release workflow and assets

- [ ] Release workflow (`workflow_dispatch`, `publish=false`) succeeds for the required RIDs (`linux-x64`, `osx-arm64`, `win-x64`)
- [ ] Assets verified:

```bash
export RELEASE_VERSION="<version>"
export RELEASE_COMMIT="<release_commit_sha>"
bash scripts/release/verify-release-assets.sh "$RELEASE_VERSION" "$RELEASE_COMMIT" ./release-assets
```

- [ ] Native AOT smoke on the produced artifacts passes
- [ ] SHA-256 values in any install manifest match the produced assets

## 5. Go / no-go

- [ ] No open Critical or Important findings
- [ ] Rollback path recorded (previous tag: `v2.0.6`)
- [ ] Approver sign-off: ____ date: ____

## Known gaps at drafting time

- Version sections for 2.0.0 to 2.0.4 in the changelogs were derived from commit subjects; the owner must review them before release.
- Items in section 3 and 4 are unverified until run on the release commit.
