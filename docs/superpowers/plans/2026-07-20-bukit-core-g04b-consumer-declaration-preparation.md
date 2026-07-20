# Bukit Core G-04B1 Consumer Declaration Preparation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an exact, publicly traceable preparation package for the 136 remaining Bukit Core 2.0 public-surface candidates without opening the declaration window or changing CLR visibility.

**Architecture:** Treat the governed public API baseline as the only candidate-identity source, add authenticated GitHub evidence as an append-only observation layer, and publish the resulting snapshot as a machine manifest plus a prepared-but-not-open consumer notice and a Chinese audit report. Keep external publication, deprecation, access-level changes, and G-04C in separately approved tasks.

**Tech Stack:** JSON, Markdown, `jq`, Git, authenticated GitHub connector search, existing Bukit shell gates, .NET 10 Architecture Tests.

## Global Constraints

- Work only in `/Users/ali/mydev/Git/Github/Bukit/.worktrees/g04b-consumer-declaration-preparation` on `codex/g04b-consumer-declaration-preparation`.
- The candidate source is `docs/governance/bukit-core-public-api-baseline.v1.json`; select only entries where `compatibility == "2.0-candidate"`.
- The final candidate set must contain exactly 136 unique identities with assembly counts `Bukit.Cli.Shared=5`, `Bukit.Content=35`, `Bukit.Engine=57`, `Bukit.PluginHost=16`, `Bukit.Rendering=2`, `Bukit.Routing=1`, `Bukit.Shared=17`, and `Bukit.Theme=3`.
- Migration target is exactly `2.0.0`; declaration state is exactly `prepared-not-open`.
- `Bukit.Engine.RouteInventoryInspectEntry` remains `consumer-declaration-pending / review-only`; it is not approved for G-04C.
- The six G-04A corrected types must not appear in the candidate manifest.
- GitHub operations are authenticated and read-only. Do not create or edit an Issue, PR, Discussion, Release, branch, tag, comment, label, reaction, or repository file.
- Do not push local commits or open the declaration window.
- Do not modify Core/Labs/plugin source, tests, access levels, CLR signatures, project references, the governed public API baseline, product schemas, plugin protocol, persistence formats, asset URLs, or backup/reference directories.
- A successful public search does not prove the absence of private consumers. Every candidate keeps `privateConsumerStatus = "unknown-until-voluntary-declaration"`.
- Do not close G-04B1 with any `search-incomplete` candidate. Truncated searches must be conservatively classified or resolved with a narrower successful query.
- Do not run `ci-full`, release, `test-all`, `smoke-all`, or whole-solution tests.

---

### Task 1: Freeze and verify the 136-candidate source set

**Files:**
- Read: `docs/governance/bukit-core-public-api-baseline.v1.json`
- Read: `docs/analysis/bukit-core-public-api-inventory-2026-07-20.json`
- Produce outside repository: `/tmp/g04b-candidate-seed.json`
- Produce outside repository: `/tmp/g04b-search-batches/batch-01-small-assemblies.json`
- Produce outside repository: `/tmp/g04b-search-batches/batch-02-content.json`
- Produce outside repository: `/tmp/g04b-search-batches/batch-03-engine-first.json`
- Produce outside repository: `/tmp/g04b-search-batches/batch-04-engine-second.json`
- Produce outside repository: `/tmp/g04b-search-batches/batch-05-pluginhost.json`
- Produce outside repository: `/tmp/g04b-search-batches/batch-06-shared.json`
- Test: command-only `jq` and `cmp` assertions

**Interfaces:**
- Consumes: governed baseline entries with `name`, `assembly`, `owner`, `classification`, `compatibility`, and `migrationHorizon`.
- Produces: one sorted 136-entry seed and six non-overlapping search batches consumed by Task 2.

- [ ] **Step 1: Prove the baseline has the expected candidate totals before generating anything**

Run:

```bash
jq -e '
  [.types[] | select(.compatibility == "2.0-candidate")] as $c |
  ($c | length) == 136 and
  ($c | group_by(.assembly) | map({key: .[0].assembly, value: length}) | from_entries) == {
    "Bukit.Cli.Shared": 5,
    "Bukit.Content": 35,
    "Bukit.Engine": 57,
    "Bukit.PluginHost": 16,
    "Bukit.Rendering": 2,
    "Bukit.Routing": 1,
    "Bukit.Shared": 17,
    "Bukit.Theme": 3
  }
' docs/governance/bukit-core-public-api-baseline.v1.json
```

Expected: prints `true` and exits `0`. Any other result stops the task and requires a separate governance-correction decision.

- [ ] **Step 2: Generate the canonical temporary seed**

Run:

```bash
jq -S '[
  .types[]
  | select(.compatibility == "2.0-candidate")
  | {
      assembly,
      fullName: .name,
      simpleName: (.name | split("+")[-1] | split(".")[-1]),
      owner,
      classification,
      compatibility,
      migrationHorizon
    }
] | sort_by(.assembly, .fullName)' \
  docs/governance/bukit-core-public-api-baseline.v1.json \
  > /tmp/g04b-candidate-seed.json
```

Expected: `/tmp/g04b-candidate-seed.json` contains 136 sorted entries and does not modify the repository.

- [ ] **Step 3: Prove the G-04A exclusions and G-04C pilot identity**

Run:

```bash
jq -e '
  ([.[] | select(.fullName == "Bukit.Engine.BuildEnvironmentInfo" or
                 .fullName == "Bukit.Engine.BuildIncrementalSummary" or
                 .fullName == "Bukit.Engine.BuildProjectInfo" or
                 .fullName == "Bukit.Engine.BuildSummary" or
                 .fullName == "Bukit.Theme.ThemeManifestException" or
                 .fullName == "Bukit.Theme.ThemeTokensProcessor")] | length) == 0 and
  ([.[] | select(.fullName == "Bukit.Engine.RouteInventoryInspectEntry")] | length) == 1
' /tmp/g04b-candidate-seed.json
```

Expected: prints `true` and exits `0`.

- [ ] **Step 4: Split the seed into exact non-overlapping search batches**

Run:

```bash
mkdir -p /tmp/g04b-search-batches
jq '[.[] | select(.assembly == "Bukit.Cli.Shared" or .assembly == "Bukit.Rendering" or .assembly == "Bukit.Routing" or .assembly == "Bukit.Theme")]' /tmp/g04b-candidate-seed.json > /tmp/g04b-search-batches/batch-01-small-assemblies.json
jq '[.[] | select(.assembly == "Bukit.Content")]' /tmp/g04b-candidate-seed.json > /tmp/g04b-search-batches/batch-02-content.json
jq '[.[] | select(.assembly == "Bukit.Engine")][0:29]' /tmp/g04b-candidate-seed.json > /tmp/g04b-search-batches/batch-03-engine-first.json
jq '[.[] | select(.assembly == "Bukit.Engine")][29:]' /tmp/g04b-candidate-seed.json > /tmp/g04b-search-batches/batch-04-engine-second.json
jq '[.[] | select(.assembly == "Bukit.PluginHost")]' /tmp/g04b-candidate-seed.json > /tmp/g04b-search-batches/batch-05-pluginhost.json
jq '[.[] | select(.assembly == "Bukit.Shared")]' /tmp/g04b-candidate-seed.json > /tmp/g04b-search-batches/batch-06-shared.json
jq -s -S 'add | sort_by(.assembly, .fullName)' /tmp/g04b-search-batches/batch-*.json > /tmp/g04b-search-batches/recombined.json
cmp /tmp/g04b-candidate-seed.json /tmp/g04b-search-batches/recombined.json
```

Expected: `cmp` exits `0`; batch counts are 11, 35, 29, 28, 16, and 17.

- [ ] **Step 5: Record source identity evidence for later manifest assembly**

Run:

```bash
git rev-parse HEAD
git rev-parse HEAD:docs/governance/bukit-core-public-api-baseline.v1.json
shasum -a 256 docs/governance/bukit-core-public-api-baseline.v1.json
```

Expected: capture the exact commit, Git blob SHA, and content SHA-256 in the task evidence; do not guess or copy an older commit.

### Task 2: Collect and review authenticated GitHub evidence

**Files:**
- Read: `/tmp/g04b-search-batches/batch-01-small-assemblies.json`
- Read: `/tmp/g04b-search-batches/batch-02-content.json`
- Read: `/tmp/g04b-search-batches/batch-03-engine-first.json`
- Read: `/tmp/g04b-search-batches/batch-04-engine-second.json`
- Read: `/tmp/g04b-search-batches/batch-05-pluginhost.json`
- Read: `/tmp/g04b-search-batches/batch-06-shared.json`
- Produce outside repository: `/tmp/g04b-search-evidence/batch-01.json` through `/tmp/g04b-search-evidence/batch-06.json`
- Produce outside repository: `/tmp/g04b-repository-dependency-evidence.json`
- Test: command-only evidence-shape and coverage assertions

**Interfaces:**
- Consumes: six exact candidate batches from Task 1 and authenticated GitHub connector `github_search`.
- Produces: one reviewed evidence record per candidate plus repository-level dependency queries consumed by Tasks 3 and 5.

- [ ] **Step 1: Verify authenticated connector access without exposing credentials**

Call the connector profile action and require a successful response. Do not log tokens or cookies.

Expected: authenticated profile request succeeds. If it fails, mark the task blocked; do not downgrade to unauthenticated web search and do not create a manifest claiming complete evidence.

- [ ] **Step 2: Search both exact identities for every candidate**

For every entry in each batch, call authenticated `github_search` twice with `topn = 20`:

```text
query = candidate.fullName
query = candidate.simpleName
```

Do not set `repository_name` or `org` for these two searches; they are public cross-repository evidence searches. Preserve the exact query, UTC execution time, returned result count, repository and path for each result, and whether the count reached 20.

Expected: 272 successful primary searches covering all 136 identities. A connector error is `search-incomplete`, not zero results.

- [ ] **Step 3: Triage matches using the fixed status vocabulary**

For each candidate, produce exactly one status:

```text
no-public-match-found
owner-repository-only
fork-or-mirror-observed
external-match-needs-review
confirmed-external-consumer
search-incomplete
```

Review namespace, declaration/use context, repository identity, `.csproj` or other dependency text, and whether the hit is Bukit itself. A simple-name collision without Bukit namespace/reference evidence is documented as an excluded false positive. A 20-result query is truncated; resolve it with a narrower namespace/context query or conservatively use `external-match-needs-review`.

Expected: no candidate is classified solely from result count or snippet text.

- [ ] **Step 4: Capture repository-level dependency signals**

Run authenticated global searches with `topn = 20` for each exact query:

```text
ALi365-SDN-BHD/Bukit
github.com/ALi365-SDN-BHD/Bukit
Bukit.Engine
Bukit.Content
Bukit.PluginHost
```

Review external repositories for actual source/package/submodule/dependency context and store results in `/tmp/g04b-repository-dependency-evidence.json`. Owner-repository hits and unrelated word matches are not external consumer proof.

- [ ] **Step 5: Write canonical temporary batch evidence**

Each `/tmp/g04b-search-evidence/batch-NN.json` entry must use this exact shape:

```json
{
  "fullName": "Bukit.Engine.RouteInventoryInspectEntry",
  "authenticated": true,
  "searchedAtUtc": "2026-07-20T00:00:00Z",
  "queries": [
    {
      "kind": "full-name",
      "query": "Bukit.Engine.RouteInventoryInspectEntry",
      "topn": 20,
      "returned": 0,
      "truncated": false,
      "repositories": []
    },
    {
      "kind": "simple-name",
      "query": "RouteInventoryInspectEntry",
      "topn": 20,
      "returned": 0,
      "truncated": false,
      "repositories": []
    }
  ],
  "searchStatus": "no-public-match-found",
  "reviewedRepositories": [],
  "excludedFalsePositives": [],
  "limitations": [
    "private repositories and voluntarily undisclosed consumers are not observable"
  ]
}
```

Use actual timestamps and actual results; the example values are the required field shape, not evidence to copy.

- [ ] **Step 6: Prove evidence completeness before repository changes**

Run:

```bash
jq -s -S 'add | sort_by(.fullName)' /tmp/g04b-search-evidence/batch-*.json > /tmp/g04b-search-evidence/all.json
jq -e '
  length == 136 and
  (map(.fullName) | unique | length) == 136 and
  all(.[]; .authenticated == true) and
  all(.[]; (.queries | length) >= 2) and
  all(.[]; all(.queries[]; .topn == 20 and (.returned | type) == "number")) and
  all(.[]; .searchStatus != "search-incomplete")
' /tmp/g04b-search-evidence/all.json
```

Expected: prints `true` and exits `0`. Stop and retry only affected searches if it fails.

### Task 3: Create the governed candidate manifest

**Files:**
- Create: `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
- Read: `docs/governance/bukit-core-public-api-baseline.v1.json`
- Read: `/tmp/g04b-candidate-seed.json`
- Read: `/tmp/g04b-search-evidence/all.json`
- Test: exact identity, shape, ordering, count, exclusion, and status assertions

**Interfaces:**
- Consumes: verified seed and reviewed evidence from Tasks 1-2.
- Produces: the machine-readable source linked by Tasks 4-5.

- [ ] **Step 1: Join candidate governance fields with external evidence outside the repository**

Run this exact join. Every variable is derived from reviewed Task 1-2 evidence; no timestamp, hash, count, or candidate is typed manually.

```bash
SOURCE_COMMIT=$(git rev-parse HEAD)
SOURCE_BLOB=$(git rev-parse HEAD:docs/governance/bukit-core-public-api-baseline.v1.json)
SOURCE_SHA256=$(shasum -a 256 docs/governance/bukit-core-public-api-baseline.v1.json | awk '{print $1}')
PREPARED_AT_UTC=$(jq -r 'map(.searchedAtUtc) | max' /tmp/g04b-search-evidence/all.json)

jq -n -S \
  --slurpfile seed /tmp/g04b-candidate-seed.json \
  --slurpfile evidence /tmp/g04b-search-evidence/all.json \
  --arg sourceCommit "$SOURCE_COMMIT" \
  --arg sourceBlob "$SOURCE_BLOB" \
  --arg sourceSha256 "$SOURCE_SHA256" \
  --arg preparedAtUtc "$PREPARED_AT_UTC" '
  ($seed[0] | map(
    . as $candidate
    | ($evidence[0] | map(select(.fullName == $candidate.fullName)) | first) as $externalEvidence
    | $candidate + {
        declarationStatus: "consumer-declaration-pending",
        proposedAction: "review-only",
        externalEvidence: $externalEvidence,
        privateConsumerStatus: "unknown-until-voluntary-declaration"
      }
  ) | sort_by(.assembly, .fullName)) as $candidates
  | {
      schema: "bukit-core-2.0-public-surface-candidates/v1",
      schemaVersion: 1,
      preparedAtUtc: $preparedAtUtc,
      sourceBaseline: {
        path: "docs/governance/bukit-core-public-api-baseline.v1.json",
        repositoryCommit: $sourceCommit,
        gitBlobSha: $sourceBlob,
        sha256: $sourceSha256
      },
      selection: "compatibility == 2.0-candidate",
      candidateCount: ($candidates | length),
      migrationTarget: "2.0.0",
      declarationState: "prepared-not-open",
      windowPolicy: {
        openRequiresSeparateApproval: true,
        minimumStableReleaseCycles: 1,
        calendarTimeAloneIsInsufficient: true,
        openedAtUtc: null,
        announcementUrl: null,
        eligibleAfterRelease: null
      },
      feedbackChannel: {
        kind: "github-issue",
        repository: "ALi365-SDN-BHD/Bukit",
        issueNumber: null,
        state: "not-created"
      },
      assemblyCounts: ($candidates | group_by(.assembly) | map({key: .[0].assembly, value: length}) | from_entries),
      ownerCounts: ($candidates | group_by(.owner) | map({key: .[0].owner, value: length}) | from_entries),
      candidates: $candidates
    }
' > /tmp/g04b-manifest.candidate.json
```

Expected: `/tmp/g04b-manifest.candidate.json` is canonical JSON with recomputed counts and one matched evidence object per candidate.

- [ ] **Step 2: Add the inspected canonical JSON with `apply_patch`**

Inspect `/tmp/g04b-manifest.candidate.json` for secrets, local absolute paths, unexpected repositories, malformed URLs, and `search-incomplete`. Add its complete canonical content to `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` using `apply_patch`.

Expected: only the new governance JSON appears in `git status` in addition to the already committed design/plan documents.

- [ ] **Step 3: Assert exact identity equivalence and fixed policy fields**

Run:

```bash
jq -S '[.types[] | select(.compatibility == "2.0-candidate") | .name] | sort' docs/governance/bukit-core-public-api-baseline.v1.json > /tmp/g04b-baseline-identities.json
jq -S '[.candidates[].fullName] | sort' docs/governance/bukit-core-2.0-public-surface-candidates.v1.json > /tmp/g04b-manifest-identities.json
cmp /tmp/g04b-baseline-identities.json /tmp/g04b-manifest-identities.json
jq -e '
  .schema == "bukit-core-2.0-public-surface-candidates/v1" and
  .schemaVersion == 1 and
  .candidateCount == 136 and
  .migrationTarget == "2.0.0" and
  .declarationState == "prepared-not-open" and
  .feedbackChannel.issueNumber == null and
  .feedbackChannel.state == "not-created" and
  (.candidates | length) == 136 and
  ([.candidates[].fullName] | unique | length) == 136 and
  all(.candidates[]; .classification == "implementation-public" and .compatibility == "2.0-candidate" and .migrationHorizon == "2.0-review" and .declarationStatus == "consumer-declaration-pending" and .proposedAction == "review-only" and .privateConsumerStatus == "unknown-until-voluntary-declaration" and .externalEvidence.authenticated == true and .externalEvidence.searchStatus != "search-incomplete")
' docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
```

Expected: `cmp` exits `0`; `jq` prints `true` and exits `0`.

- [ ] **Step 4: Commit the independently valid manifest**

```bash
git add docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
git commit -m "docs(governance): add G-04B1 public surface candidates"
```

### Task 4: Write the prepared-but-not-open consumer declaration

**Files:**
- Create: `docs/governance/bukit-core-2.0-consumer-declaration.md`
- Read: `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
- Read: `guide/dev/public-api-governance.md`
- Test: declaration-state, link, forbidden-claim, and placeholder scans

**Interfaces:**
- Consumes: final candidate manifest from Task 3.
- Produces: public-facing wording linked by the active guide and audit report.

- [ ] **Step 1: Write the declaration with fixed sections and claims**

Create the Markdown file with these sections:

```text
# Bukit Core 2.0 Public Surface Consumer Declaration
Status: prepared-not-open
Target: 2.0.0
## What This List Means
## Current 1.x Compatibility Position
## Candidate Inventory
## How Feedback Will Work
## Window Opening And Closing Rules
## What Happens When A Consumer Is Found
## Explicit Non-Claims
```

The declaration must link `bukit-core-2.0-public-surface-candidates.v1.json`, state that all 136 entries are review candidates rather than removal decisions, preserve 1.x visibility, and explain that the dedicated GitHub Issue does not yet exist. It must state that G-04B2 requires separate approval and one later stable release cycle before any G-04C eligibility decision.

- [ ] **Step 2: Prove the declaration cannot be mistaken for an open window**

Run:

```bash
rg -n "prepared-not-open|2\.0\.0|136|one later stable release|separate approval" docs/governance/bukit-core-2.0-consumer-declaration.md
if rg -n "window is open|approved for removal|safe to delete|no external consumers|issue #[0-9]+" docs/governance/bukit-core-2.0-consumer-declaration.md; then exit 1; fi
if rg -n "T[B]D|T[O]DO|F[I]XME|\\x3c[^\\x3e]+\\x3e" docs/governance/bukit-core-2.0-consumer-declaration.md; then exit 1; fi
```

Expected: required phrases are found; forbidden claims and placeholders are absent.

- [ ] **Step 3: Run the declaration document's targeted gate**

Run in the non-sandbox environment:

```bash
bash scripts/checks/post-change-targeted.sh -- docs/governance/bukit-core-2.0-consumer-declaration.md
```

Expected: exit `0`.

- [ ] **Step 4: Commit the declaration**

```bash
git add docs/governance/bukit-core-2.0-consumer-declaration.md
git commit -m "docs(governance): prepare 2.0 consumer declaration"
```

### Task 5: Write the G-04B1 evidence report and active governance entry

**Files:**
- Create: `docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md`
- Modify: `guide/dev/public-api-governance.md`
- Read: `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
- Read: `/tmp/g04b-repository-dependency-evidence.json`
- Test: count reconciliation, link, scope, placeholder, and wording assertions

**Interfaces:**
- Consumes: final manifest, declaration, and repository-level search evidence.
- Produces: audit closure evidence and an active maintainer entry point.

- [ ] **Step 1: Calculate report tables from the manifest**

Run `jq` to calculate:

- candidate counts by assembly and owner;
- counts by `externalEvidence.searchStatus`;
- the exact lists for `confirmed-external-consumer`, `external-match-needs-review`, and `fork-or-mirror-observed`;
- truncated query count and excluded false-positive count;
- the RouteInventoryInspectEntry evidence summary.

Save only temporary calculations under `/tmp`; do not hand-type totals that can be derived.

- [ ] **Step 2: Write the Chinese G-04B1 report**

Create the report with these sections:

```text
# Bukit Core G-04B1 外部消费者声明准备与证据报告
## 执行摘要
## 范围与非目标
## 136 项候选对账
## 已认证 GitHub 检索方法
## 外部命中与误报复核
## 仓库级依赖信号
## 私有消费者与证据限制
## 声明窗口状态
## 风险与处置
## 验证记录
## G-04B2 前置条件
```

Every numeric claim must come from the manifest or recorded search evidence. The report must distinguish no public match, owner-only hits, forks/mirrors, possible external matches, confirmed consumers, and private-consumer uncertainty. It must say `prepared-not-open`, not claim a GitHub Issue exists, and not authorize G-04C.

- [ ] **Step 3: Add the active guide entry**

Append a focused `## 2.0 Consumer Declaration Preparation` section to `guide/dev/public-api-governance.md` that:

- links both `../../docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` and `../../docs/governance/bukit-core-2.0-consumer-declaration.md`;
- says the current state is `prepared-not-open`;
- says 1.x access levels remain unchanged;
- says no public match is not removal proof;
- directs maintainers to obtain separate approval before G-04B2 publication.

- [ ] **Step 4: Reconcile all report numbers and links**

Run:

```bash
rg -n "136|prepared-not-open|G-04B2|G-04C|private|私有" docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md guide/dev/public-api-governance.md
if rg -n "T[B]D|T[O]DO|F[I]XME|\\x3c[^\\x3e]+\\x3e" docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md guide/dev/public-api-governance.md; then exit 1; fi
test -f docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
test -f docs/governance/bukit-core-2.0-consumer-declaration.md
```

Expected: required status/boundary terms are present, placeholders are absent, and both linked files exist.

- [ ] **Step 5: Run the report/guide targeted gate and commit**

Run in the non-sandbox environment:

```bash
bash scripts/checks/post-change-targeted.sh -- docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md guide/dev/public-api-governance.md
```

Expected: exit `0`.

Commit:

```bash
git add docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md guide/dev/public-api-governance.md
git commit -m "docs(governance): document G-04B1 consumer evidence"
```

### Task 6: Run aggregate verification and independent read-only review

**Files:**
- Verify: `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
- Verify: `docs/governance/bukit-core-2.0-consumer-declaration.md`
- Verify: `docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md`
- Verify: `guide/dev/public-api-governance.md`
- Verify: `docs/superpowers/specs/2026-07-20-bukit-core-g04b-consumer-declaration-preparation-design.zh-CN.md`
- Verify: `docs/superpowers/plans/2026-07-20-bukit-core-g04b-consumer-declaration-preparation.md`
- Test: exact machine assertions, public API drift, Architecture Tests, aggregate targeted gate, diff audit

**Interfaces:**
- Consumes: all previous task deliverables and the original design specification.
- Produces: verified G-04B1 branch eligible for local merge consideration, not G-04B2 publication.

- [ ] **Step 1: Rerun exact machine reconciliation**

Repeat Task 3's identity and policy assertions, recompute assembly/owner/status counts from `.candidates`, verify `sourceBaseline.sha256` against the current governed baseline, and assert that no query is incomplete or silently truncated.

Expected: all assertions exit `0`; candidate count remains 136.

- [ ] **Step 2: Prove the CLR surface and governed baseline did not change**

Run:

```bash
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
```

Expected: both exit `0`; Release build reports zero warnings and zero errors.

- [ ] **Step 3: Run Architecture Tests**

Run:

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore
```

Expected: 81 passed, 0 failed, 0 skipped.

- [ ] **Step 4: Run the full G-04B1 aggregate targeted gate**

Run in the non-sandbox environment:

```bash
bash scripts/checks/post-change-targeted.sh -- \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md \
  guide/dev/public-api-governance.md \
  docs/superpowers/specs/2026-07-20-bukit-core-g04b-consumer-declaration-preparation-design.zh-CN.md \
  docs/superpowers/plans/2026-07-20-bukit-core-g04b-consumer-declaration-preparation.md
```

Expected: exit `0`; do not substitute a full or release gate.

- [ ] **Step 5: Audit scope and formatting**

Run:

```bash
git diff --check main...HEAD
git diff --name-only main...HEAD
git status --short
```

Expected: only the six planned paths appear, no `src/`, `tests/`, schema, protocol, baseline, workflow, active script, or backup path appears, and the worktree is clean after commits.

- [ ] **Step 6: Request one independent read-only aggregate review**

The reviewer must compare the entire branch against this plan and design, inspect all 136 identities and evidence statuses, verify no external GitHub write occurred, confirm `prepared-not-open`, and report Critical/Important/Minor findings with file/line evidence. The reviewer must not modify files or create commits.

Expected: no unresolved findings. If a finding is valid, fix only the affected governance path, rerun its targeted gate, and repeat the necessary review.

- [ ] **Step 7: Record the final branch state**

Run:

```bash
git log --oneline main..HEAD
git status --short --branch
```

Expected: the branch contains only G-04B1 design/plan/manifest/declaration/report/guide commits and is ready for an explicit merge decision. Do not push or merge automatically.
