# Bukit Public Release Authorization Enforcement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert Bukit Core's documented management-approval requirement into a fail-closed GitHub Actions publication boundary.

**Architecture:** Keep build, test, packaging, and internal artifact jobs unchanged. Gate only `publish-release` through a protected `public-release` GitHub Environment, reject public publication requests outside `main`, and retain `contents: write` exclusively on the final publication job. Repository contract tests protect the versioned workflow; GitHub Environment settings supply the external reviewer identity and deployment protection.

**Tech Stack:** GitHub Actions YAML, YamlDotNet-based xUnit architecture contracts, repository shell owner checks, Markdown release governance.

## Global Constraints

- Scope is release governance only; do not modify Bukit Core runtime, CLI, config schema, CLR APIs, plugin protocols, persistent formats, Labs, or plugin implementation.
- Keep `publish` defaulting to `"false"`.
- Keep public publication requiring `rids=all` and a non-`0.0.0-ci` version.
- Keep global workflow permissions at `contents: read`; grant `contents: write` only to `publish-release`.
- A public release must run from `refs/heads/main`.
- `publish-release` must reference the GitHub Environment named exactly `public-release`.
- The live `public-release` Environment must have at least one management reviewer, prevent self-review, disallow administrator bypass, and allow only the `main` branch.
- Do not execute a real public release, create a public tag, upload public assets, or approve a deployment during verification.
- Run only direct workflow owner checks, affected Architecture tests, focused checks for non-blocked paths, and one aggregate targeted gate after all code tasks.
- Do not run full, release, coverage, smoke-all, test-all, or whole-solution gates without separate explicit authorization.

---

### Task 1: Protect the versioned release workflow contract

**Files:**
- Modify: `tests/Bukit.Architecture.Tests/ReleaseWorkflowContractTests.cs`
- Modify: `.github/workflows/release.yaml`

**Interfaces:**
- Consumes: the existing `workflow_dispatch` inputs and `publish-release` job.
- Produces: `public-release` Environment binding and a `refs/heads/main` publication invariant.

- [ ] **Step 1: Add failing workflow contract tests**

Add these tests to `ReleaseWorkflowContractTests`:

```csharp
[Fact]
public void PublicRelease_RequiresProtectedEnvironmentAndMain()
{
    var publishJob = Job("publish-release");

    Assert.Equal(
        "${{ inputs.publish == 'true' && github.ref == 'refs/heads/main' }}",
        Scalar(publishJob, "if"));
    Assert.Equal(
        "public-release",
        Scalar(Mapping(publishJob, "environment"), "name"));
    Assert.Equal(
        "write",
        Scalar(Mapping(publishJob, "permissions"), "contents"));
}

[Fact]
public void ValidateInputs_RejectsPublicReleaseOutsideMain()
{
    var validate = Step(Job("validate-inputs"), "Validate release request");
    var env = Mapping(validate, "env");
    var run = Scalar(validate, "run");

    Assert.Equal("${{ github.ref }}", Scalar(env, "REF"));
    Assert.Contains(
        "if [[ \"$PUBLISH\" == \"true\" && \"$REF\" != \"refs/heads/main\" ]]; then exit 1; fi",
        run,
        StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the tests and verify RED**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release \
  --filter FullyQualifiedName~ReleaseWorkflowContractTests
```

Expected: exactly the two new tests fail because `environment`, the `main`
condition, and `REF` validation do not yet exist; the existing 13 tests pass.

- [ ] **Step 3: Add the minimal workflow enforcement**

In the `Validate release request` step, add:

```yaml
          REF: ${{ github.ref }}
```

and append:

```bash
          if [[ "$PUBLISH" == "true" && "$REF" != "refs/heads/main" ]]; then exit 1; fi
```

Change the publication job to:

```yaml
  publish-release:
    name: Publish GitHub Release
    runs-on: ubuntu-latest
    needs: collect-assets
    if: ${{ inputs.publish == 'true' && github.ref == 'refs/heads/main' }}
    environment:
      name: public-release
    timeout-minutes: 10
    permissions:
      contents: write
```

Do not alter its asset download or `softprops/action-gh-release` steps.

- [ ] **Step 4: Run the workflow contracts and verify GREEN**

Run the Task 1 test command again.

Expected: 15 passed, 0 failed.

- [ ] **Step 5: Run direct workflow owner checks**

Run:

```bash
bash scripts/checks/active-workflow-boundary-self-test.sh
bash scripts/checks/active-workflow-boundary.sh
```

Expected: both exit 0.

- [ ] **Step 6: Commit the workflow contract**

```bash
git add \
  tests/Bukit.Architecture.Tests/ReleaseWorkflowContractTests.cs \
  .github/workflows/release.yaml
git commit -m "ci(release): require protected publication approval"
```

---

### Task 2: Document the platform-enforced publication boundary

**Files:**
- Modify: `guide/dev/release.md`
- Modify: `guide/dev/release-checklist.md`
- Modify: `docs/release/release-prerelease-template.md`

**Interfaces:**
- Consumes: the `public-release` Environment contract from Task 1.
- Produces: an operator-visible setup, review, rejection, and evidence contract.

- [ ] **Step 1: Add the Environment requirements to the release guide**

After `## Authorization Boundary` in `guide/dev/release.md`, add:

```markdown
### GitHub Enforcement

The `publish-release` job targets the `public-release` GitHub Environment and
accepts only `refs/heads/main`. Repository administrators must configure that
Environment with:

- at least one explicitly authorized management reviewer;
- prevent self-review enabled;
- administrator bypass disabled;
- a deployment branch policy allowing only `main`.

The workflow contract is not operationally complete until those external
settings are present. Internal artifact runs keep `publish=false` and do not
enter the protected Environment.
```

- [ ] **Step 2: Extend the release checklist**

After Step 0 in `guide/dev/release-checklist.md`, add:

```markdown
   For `public-release`, verify the `public-release` GitHub Environment names
   the authorized management reviewer, prevents self-review, disables
   administrator bypass, and allows only `main`. Retain the deployment review
   record as authorization evidence.
```

Add this final sentence after the current technical-success boundary:

```markdown
Do not approve a test deployment; verification must stop while the publication
job is waiting for the protected Environment.
```

- [ ] **Step 3: Extend the maintainer prerelease template**

After the opening authorization warning in
`docs/release/release-prerelease-template.md`, add:

```markdown
> `publish-release` 必须等待 `public-release` GitHub Environment 审批，并且只允许
> `main` 分支。该 Environment 必须指定获授权的管理审批者、禁止自批、禁止管理员
> 绕过，并仅允许 `main`；部署审批记录是发布授权证据。
```

- [ ] **Step 4: Run focused documentation verification**

Run:

```bash
bash scripts/checks/post-change-focused.sh -- \
  guide/dev/release.md \
  guide/dev/release-checklist.md \
  docs/release/release-prerelease-template.md
```

Expected: exit 0.

- [ ] **Step 5: Commit the operator contract**

```bash
git add \
  guide/dev/release.md \
  guide/dev/release-checklist.md \
  docs/release/release-prerelease-template.md
git commit -m "docs(release): define protected publication approval"
```

---

### Task 3: Configure and prove the live GitHub Environment

**Files:**
- No repository file changes.

**Interfaces:**
- Consumes: the exact authorized management reviewer identity supplied by the repository owner.
- Produces: a live `public-release` Environment with protected reviewers and `main`-only deployment.

- [ ] **Step 1: Obtain the exact reviewer identity**

Before changing GitHub settings, obtain one explicit GitHub user login or
organization team slug that represents management approval. Do not infer this
identity from commit history or contributor listings.

- [ ] **Step 2: Configure the Environment**

Using repository administration access, create or update `public-release` with:

- the exact user or team explicitly approved in Step 1 as a required reviewer;
- prevent self-review enabled;
- administrator bypass disabled;
- custom deployment branch policies enabled;
- exactly one branch policy, named `main` with type `branch`.

- [ ] **Step 3: Verify the live settings read-only**

Use the public GitHub REST API to confirm:

- Environment name is `public-release`;
- at least one required reviewer is present;
- `prevent_self_review` is true;
- administrator bypass is disabled;
- custom branch policies are enabled;
- the only branch policy is `main`.

Do not dispatch the release workflow and do not approve a deployment.

---

### Task 4: Aggregate verification and read-only audit

**Files:**
- Review every path changed from the branch base.

**Interfaces:**
- Consumes: Tasks 1-3.
- Produces: one auditable release-authorization enforcement change.

- [ ] **Step 1: Run the affected Architecture tests**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c Release \
  --filter FullyQualifiedName~ReleaseWorkflowContractTests
```

Expected: 15 passed, 0 failed.

- [ ] **Step 2: Run direct workflow owner checks**

Run:

```bash
bash scripts/checks/active-workflow-boundary-self-test.sh
bash scripts/checks/active-workflow-boundary.sh
```

Expected: both exit 0.

- [ ] **Step 3: Run one aggregate targeted gate**

Run exactly once after Tasks 1-3:

```bash
bash scripts/checks/post-change-targeted.sh \
  --base 84ff847b702e0117a54a4e63c5d34e2fdb0a5d10 \
  -- \
  .github/workflows/release.yaml \
  tests/Bukit.Architecture.Tests/ReleaseWorkflowContractTests.cs \
  guide/dev/release.md \
  guide/dev/release-checklist.md \
  docs/release/release-prerelease-template.md \
  docs/superpowers/plans/2026-07-24-bukit-public-release-authorization-enforcement.md
```

Expected: exit 0. If repository governance rejects the release workflow path
without a broader release-gate authorization, report the exact blocker and do
not substitute a full or release gate.

- [ ] **Step 4: Audit the aggregate diff**

Confirm:

- only the five implementation files and this implementation plan changed;
- `publish=false`, `rids=all`, version validation, packaging, checksums, smoke,
  asset collection, and release creation steps remain otherwise unchanged;
- only `publish-release` has `contents: write`;
- no Core runtime, schema, API, protocol, Labs, plugin, or historical file
  changed;
- `git diff --check` passes;
- the worktree is clean after commits.
