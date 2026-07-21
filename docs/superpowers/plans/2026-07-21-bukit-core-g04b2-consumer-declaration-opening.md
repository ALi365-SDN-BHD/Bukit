# Bukit Core G-04B2 Consumer Declaration Opening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Open the Bukit Core 2.0 public-surface consumer declaration through one dedicated GitHub Issue and publish the Issue's real metadata into the governed repository state without changing any CLR surface or authorizing G-04C.

**Architecture:** Prepare and verify every deterministic artifact locally before the first external write. After an explicit final publication approval, create exactly one Issue, treat the returned number, URL, and `createdAt` as immutable inputs, update four lifecycle documents in one publication commit, review the aggregate diff, and fast-forward that commit to `origin/main`. If repository publication cannot complete after Issue creation, close the Issue with an opening-paused notice and require a new Issue for any retry.

**Tech Stack:** Markdown, JSON, `jq`, Git, authenticated GitHub connector Issue APIs, existing Bukit shell gates, .NET 10 Architecture Tests.

## Global Constraints

- Work only in `/Users/ali/mydev/Git/Github/Bukit/.worktrees/g04b2-open-consumer-declaration` on `codex/g04b2-open-consumer-declaration`.
- The target repository is exactly `ALi365-SDN-BHD/Bukit`; the target remote branch is exactly `origin/main`.
- Do not touch, stage, stash, commit, or clean the unrelated Preview changes in the main worktree.
- The Issue title is exactly `[G-04B2] Bukit Core 2.0 public surface consumer declaration`.
- The Issue uses an English body plus one concise Chinese section.
- Do not create or modify a Release, Release Note, tag, PR, Discussion, milestone, or label.
- Do not assign the Issue and do not attach a milestone or label.
- Do not modify Core, Labs, plugin, or test source; access modifiers; CLR types, members, signatures, assemblies, or project references.
- Do not modify the governed public API baseline, product schemas, plugin protocol, persistence formats, asset URLs, or backup/reference directories.
- The candidate set remains exactly 136 unique identities. Every candidate remains `consumer-declaration-pending / review-only / unknown-until-voluntary-declaration`.
- `declarationState` and `feedbackChannel.state` become exactly `open` only after the Issue exists.
- `windowPolicy.openedAtUtc` comes from the Issue `createdAt`; do not use a client clock.
- `windowPolicy.announcementUrl` is the Issue's canonical HTTPS URL.
- `windowPolicy.eligibleAfterRelease` remains `null` until a later non-prerelease stable release actually exists.
- G-04B2 does not deprecate, narrow, remove, or approve any type and does not authorize G-04C.
- No external write is allowed before the user sees the exact Issue title/body and state-change preview and gives final publication approval.
- Push only with fast-forward semantics. Never force-push, rewrite remote history, or merge unreviewed remote changes.
- If a post-Issue failure prevents repository publication, prepend an opening-paused notice, close that Issue, and require a new Issue for any retry. Never reopen the failed Issue as the declaration window.
- Do not run `ci-full`, release, `test-all`, `smoke-all`, or whole-solution tests.

---

### Task 1: Freeze the remote baseline and pre-opening invariants

**Files:**
- Read: `docs/governance/bukit-core-public-api-baseline.v1.json`
- Read: `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
- Read: `docs/governance/bukit-core-2.0-consumer-declaration.md`
- Read: `docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md`
- Read: `guide/dev/public-api-governance.md`
- Produce outside repository: `/tmp/g04b2-origin-main.txt`
- Produce outside repository: `/tmp/g04b2-candidates-before.json`
- Produce outside repository: `/tmp/g04b2-manifest-before.json`
- Test: read-only Git, `jq`, GitHub profile/repository/Issue-search calls

**Interfaces:**
- Consumes: the approved G-04B2 design and current `origin/main` G-04B1 materials.
- Produces: one immutable remote base SHA and before-state snapshots used by Tasks 3-6.

- [ ] **Step 1: Refresh only the target remote branch**

Run in the non-sandbox environment:

```bash
git fetch origin main
```

Expected: exit `0`; no local branch, file, or Issue changes.

- [ ] **Step 2: Prove the G-04B2 branch contains the current remote base**

Run:

```bash
test "$(git merge-base HEAD origin/main)" = "$(git rev-parse origin/main)"
git rev-parse origin/main > /tmp/g04b2-origin-main.txt
git status --short --branch
```

Expected: the ancestry assertion exits `0`; status contains no tracked or untracked work beyond the already committed design/plan work. If `origin/main` is not an ancestor of `HEAD`, stop before external writes and rebase only through a separately reviewed local preparation step.

- [ ] **Step 3: Freeze the exact candidate and manifest before-state**

Run:

```bash
jq -S '.candidates' docs/governance/bukit-core-2.0-public-surface-candidates.v1.json > /tmp/g04b2-candidates-before.json
jq -S '.' docs/governance/bukit-core-2.0-public-surface-candidates.v1.json > /tmp/g04b2-manifest-before.json
shasum -a 256 /tmp/g04b2-candidates-before.json
```

Expected: both files are valid canonical JSON generated from the current worktree.

- [ ] **Step 4: Assert the exact pre-opening policy state**

Run:

```bash
jq -e '
  .candidateCount == 136 and
  .migrationTarget == "2.0.0" and
  .declarationState == "prepared-not-open" and
  .feedbackChannel == {
    "issueNumber": null,
    "kind": "github-issue",
    "repository": "ALi365-SDN-BHD/Bukit",
    "state": "not-created"
  } and
  .windowPolicy.minimumStableReleaseCycles == 1 and
  .windowPolicy.calendarTimeAloneIsInsufficient == true and
  .windowPolicy.openRequiresSeparateApproval == true and
  .windowPolicy.openedAtUtc == null and
  .windowPolicy.announcementUrl == null and
  .windowPolicy.eligibleAfterRelease == null and
  (.candidates | length) == 136 and
  ([.candidates[].fullName] | unique | length) == 136 and
  all(.candidates[];
    .declarationStatus == "consumer-declaration-pending" and
    .proposedAction == "review-only" and
    .privateConsumerStatus == "unknown-until-voluntary-declaration" and
    .externalEvidence.authenticated == true and
    .externalEvidence.searchStatus != "search-incomplete")
' docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
```

Expected: prints `true` and exits `0`.

- [ ] **Step 5: Recheck GitHub authentication, permissions, and duplicates using read-only calls**

Call:

```text
github_get_profile({})
github_get_repo({ repository_full_name: "ALi365-SDN-BHD/Bukit" })
github_search_issues({
  repository_full_name: "ALi365-SDN-BHD/Bukit",
  query: "\"Bukit Core 2.0 public surface consumer declaration\"",
  state: "open",
  sort: "updated",
  order: "desc",
  topn: 20
})
```

Expected: profile succeeds; repository is public with `default_branch = main` and Issue-write permission; duplicate search returns zero matching open Issues. A connector error or possible duplicate stops the task before any external write.

- [ ] **Step 6: Run the read-only public-surface preflight**

Run:

```bash
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore
```

Expected: both drift checks exit `0`; Release build has zero warnings/errors; Architecture Tests report 81 passed, 0 failed, 0 skipped. Use non-sandbox execution if NuGet cache or process observation is restricted.

### Task 2: Prepare the exact publication preview and obtain final approval

**Files:**
- Read: `docs/governance/bukit-core-2.0-consumer-declaration.md`
- Read: `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
- Produce outside repository: `/tmp/g04b2-issue-title.txt`
- Produce outside repository: `/tmp/g04b2-issue-body.md`
- Produce outside repository: `/tmp/g04b2-publication-preview.md`
- Test: exact title, link, forbidden-claim, secret, placeholder, and local-path scans

**Interfaces:**
- Consumes: Task 1's verified remote baseline and policy state.
- Produces: the exact title/body later passed without rewriting to `github_create_issue`, plus an explicit user publication approval.

- [ ] **Step 1: Create the exact temporary Issue title**

Use `apply_patch` to create `/tmp/g04b2-issue-title.txt` with exactly:

```text
[G-04B2] Bukit Core 2.0 public surface consumer declaration
```

Expected: one line plus final newline; no tracked repository change.

- [ ] **Step 2: Create the exact temporary Issue body**

Use `apply_patch` to create `/tmp/g04b2-issue-body.md` with this complete body:

```markdown
## Purpose

Bukit is opening a consumer declaration window for 136 CLR-visible types classified as `2.0-candidate`. We are collecting evidence before any separately reviewed Bukit 2.0 compatibility decision.

## What is being reviewed

The candidate list contains implementation-visible CLR types that are not currently declared as a general-purpose Bukit SDK. Inclusion is a request for usage evidence, not a removal, deprecation, or access-narrowing decision.

- [Machine-readable candidate manifest](https://github.com/ALi365-SDN-BHD/Bukit/blob/main/docs/governance/bukit-core-2.0-public-surface-candidates.v1.json)
- [Consumer declaration and compatibility boundaries](https://github.com/ALi365-SDN-BHD/Bukit/blob/main/docs/governance/bukit-core-2.0-consumer-declaration.md)

## Current 1.x compatibility commitment

This Issue does not change Bukit 1.x CLR visibility, signatures, supported CLI behavior, configuration/theme shapes, template objects, report schemas, or the `bukit-plugin-v1` process protocol.

## How to report usage

If your code uses one or more listed types, please comment with whatever can be shared safely:

1. the full CLR type name;
2. how the type is reached or instantiated;
3. the Bukit version or commit in use;
4. whether usage involves a project reference, copied DLL/source, reflection, inheritance, serialization, or Native AOT;
5. migration constraints for a facade, replacement entry point, or future obsolete path.

Do not post credentials, tokens, private source code, customer data, or other sensitive business information. A high-level dependency description is sufficient to start the review.

## Reflection, inheritance, serialization, and Native AOT

Please report indirect usage even when the full type name is absent from ordinary source search. Reflection strings, serializers, derived classes, public signature propagation, source generators, trimming, and Native AOT can create dependencies that public code search cannot observe.

## Window lifecycle

This Issue opens the declaration window. It has no calendar-only deadline. The window cannot become eligible for closure until at least one later non-prerelease stable Bukit release has completed, all received reports have been classified, and an independent closure audit has no unresolved consumer evidence.

No response is not proof that removal is safe.

## Explicit non-claims

- The 136 entries are review candidates, not a deletion list.
- Public-search results do not prove the absence of private, unindexed, copied, or undisclosed consumers.
- This Issue does not deprecate, narrow, or remove any type.
- This Issue does not authorize G-04C or any Bukit 2.0 access-level change.

## 中文说明

本 Issue 正式开启 Bukit Core 2.0 CLR 公共面消费者声明窗口。136 项均为复核候选，不是删除或弃用决定；1.x 可见性和现有产品契约保持不变。如有项目引用、DLL/源码复制、反射、继承、序列化或 Native AOT 使用，请在不泄露凭据、私有源码或业务敏感信息的前提下说明依赖。窗口至少跨越一个后续正式稳定版本，并完成反馈处置和独立审计后，才可能讨论后续单类型决策；本 Issue 不授权 G-04C。
```

Expected: body content is final; later tasks pass it without editorial rewriting.

- [ ] **Step 3: Create the exact state-change preview**

Use `apply_patch` to create `/tmp/g04b2-publication-preview.md` containing:

```markdown
# G-04B2 Publication Preview

- Create one open Issue in `ALi365-SDN-BHD/Bukit` with the exact reviewed title and body.
- Change manifest `declarationState` from `prepared-not-open` to `open`.
- Set `feedbackChannel.issueNumber` to the returned number.
- Set `feedbackChannel.state` from `not-created` to `open`.
- Set `windowPolicy.openedAtUtc` to the returned `createdAt`.
- Set `windowPolicy.announcementUrl` to the returned canonical Issue URL.
- Keep `windowPolicy.eligibleAfterRelease` as `null`.
- Keep all 136 candidate objects unchanged.
- Update the declaration, append-only B1 report follow-up, and active guide to the same Issue metadata.
- Create no Release, PR, Discussion, tag, milestone, label, assignee, deprecation, access-level change, G-04C authorization, or other external write.
```

Expected: preview contains no guessed number, URL, timestamp, or future release.

- [ ] **Step 4: Validate the exact publication material**

Run:

```bash
test "$(cat /tmp/g04b2-issue-title.txt)" = "[G-04B2] Bukit Core 2.0 public surface consumer declaration"
rg -n "Purpose|What is being reviewed|Current 1.x compatibility commitment|How to report usage|Reflection, inheritance, serialization, and Native AOT|Window lifecycle|Explicit non-claims|中文说明" /tmp/g04b2-issue-body.md
rg -n "bukit-core-2.0-public-surface-candidates.v1.json|bukit-core-2.0-consumer-declaration.md|136|one later non-prerelease stable|No response is not proof|does not authorize G-04C" /tmp/g04b2-issue-body.md
if rg -n "T[B]D|T[O]DO|F[I]XME|/Users/|file://|token=|Authorization:|approved for removal|safe to delete|no external consumers" /tmp/g04b2-issue-body.md /tmp/g04b2-publication-preview.md; then exit 1; fi
```

Expected: required content is found; forbidden scan exits without a match.

- [ ] **Step 5: Stop and request final publication approval**

Show the user the complete contents of:

```text
/tmp/g04b2-issue-title.txt
/tmp/g04b2-issue-body.md
/tmp/g04b2-publication-preview.md
```

Ask exactly whether to execute the external write now. Do not call `github_create_issue` until the user explicitly approves this exact preview. This is a blocking checkpoint, not a non-blocking commentary question.

### Task 3: Create the dedicated GitHub Issue and freeze returned metadata

**Files:**
- Read outside repository: `/tmp/g04b2-issue-title.txt`
- Read outside repository: `/tmp/g04b2-issue-body.md`
- Produce outside repository: `/tmp/g04b2-created-issue.json`
- Test: connector response and immediate read-back assertions

**Interfaces:**
- Consumes: Task 2's exact material and explicit final publication approval.
- Produces: canonical `issueNumber: number`, `issueUrl: string`, and `createdAt: RFC3339 string` used without substitution by Tasks 4-6.

- [ ] **Step 1: Repeat the duplicate search immediately before creation**

Call:

```text
github_search_issues({
  repository_full_name: "ALi365-SDN-BHD/Bukit",
  query: "\"Bukit Core 2.0 public surface consumer declaration\"",
  state: "open",
  sort: "updated",
  order: "desc",
  topn: 20
})
```

Expected: zero matching open Issues. Any match stops creation and requires user review.

- [ ] **Step 2: Create exactly one Issue**

Call exactly once:

```text
github_create_issue({
  repository_full_name: "ALi365-SDN-BHD/Bukit",
  title: contents of /tmp/g04b2-issue-title.txt without the final newline,
  body: complete contents of /tmp/g04b2-issue-body.md
})
```

Omit assignees, labels, and milestone. Do not retry blindly if the connector returns an error; first search for the exact title to determine whether creation succeeded remotely.

Expected: one normalized open Issue snapshot containing a positive numeric Issue number, a canonical URL formed from `https://github.com/ALi365-SDN-BHD/Bukit/issues/` plus that same number, and an RFC3339 `createdAt`.

- [ ] **Step 3: Normalize and preserve the returned metadata**

Use `apply_patch` to write `/tmp/g04b2-created-issue.json` with exactly six keys:

- `repository`, fixed to `ALi365-SDN-BHD/Bukit`;
- `title`, fixed to the approved title;
- `issueNumber`, the positive integer returned by GitHub;
- `issueUrl`, the canonical Issue HTTPS URL returned by GitHub;
- `createdAt`, the complete RFC3339 value returned by GitHub, including any fractional seconds;
- `state`, fixed to the returned `open` state.

Do not introduce any example, guessed, client-generated, or rounded value. Canonicalize the completed runtime evidence file with `jq -S` after writing.

- [ ] **Step 4: Read the Issue back before repository mutation**

Call:

```text
github_fetch_issue({
  repository_full_name: "ALi365-SDN-BHD/Bukit",
  issue_number: actual issueNumber
})
```

Assert the fetched title, body, state, URL, number, and `createdAt` match both the create response and `/tmp/g04b2-created-issue.json`. If they do not match, stop and run the compensation procedure in Task 5 Step 5.

### Task 4: Publish the exact Issue metadata into the governed files

**Files:**
- Modify: `docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`
- Modify: `docs/governance/bukit-core-2.0-consumer-declaration.md`
- Modify: `docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md`
- Modify: `guide/dev/public-api-governance.md`
- Read outside repository: `/tmp/g04b2-created-issue.json`
- Read outside repository: `/tmp/g04b2-candidates-before.json`
- Test: exact metadata, candidate immutability, documentation consistency, drift, Architecture Tests, aggregate targeted gate

**Interfaces:**
- Consumes: Task 3's verified canonical Issue metadata.
- Produces: one independently valid publication commit consumed by Task 5.

- [ ] **Step 1: Extract actual values without retyping**

Run:

```bash
jq -e '
  .repository == "ALi365-SDN-BHD/Bukit" and
  .title == "[G-04B2] Bukit Core 2.0 public surface consumer declaration" and
  (.issueNumber | type) == "number" and .issueNumber > 0 and
  (.issueUrl | test("^https://github\\.com/ALi365-SDN-BHD/Bukit/issues/[1-9][0-9]*$")) and
  (.createdAt | test("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]+)?Z$")) and
  .state == "open"
' /tmp/g04b2-created-issue.json
```

Expected: prints `true` and exits `0`. If GitHub returns fractional-second RFC3339, validate it with an RFC3339-capable parser rather than deleting precision; preserve the returned string exactly.

- [ ] **Step 2: Update only the allowed manifest root fields**

Use `apply_patch` to change the five allowed values in
`docs/governance/bukit-core-2.0-public-surface-candidates.v1.json`:

```text
declarationState -> "open"
feedbackChannel.issueNumber -> actual issueNumber
feedbackChannel.state -> "open"
windowPolicy.announcementUrl -> actual issueUrl
windowPolicy.openedAtUtc -> actual createdAt
```

Do not reformat or regenerate the 35,000-line file and do not modify `.candidates`.

- [ ] **Step 3: Update the active consumer declaration**

Use `apply_patch` to make these exact semantic changes in
`docs/governance/bukit-core-2.0-consumer-declaration.md`:

- top status becomes `open`;
- `How Feedback Works` links the actual Issue and instructs consumers to comment there;
- `Window Opening And Closing Rules` records actual `createdAt` and says the Issue is open;
- `eligibleAfterRelease` is explicitly still unknown/`null` until a later stable release exists;
- the 1.x compatibility and non-claim sections remain intact;
- `RouteInventoryInspectEntry` remains pending/review-only;
- remove claims that the Issue does not exist or G-04B2 is not authorized.

Use the exact number, URL, and timestamp from `/tmp/g04b2-created-issue.json`; do not type them from memory.

- [ ] **Step 4: Add an append-only G-04B2 lifecycle follow-up to the B1 report**

Use `apply_patch` on
`docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md`:

1. Add a top note stating that the report's G-04B1 evidence snapshot closed at `prepared-not-open`, while the current lifecycle is now `open` under G-04B2.
2. Append `## G-04B2 后续状态（2026-07-21）` containing the actual Issue number, URL, `createdAt`, `eligibleAfterRelease = null`, unchanged 136 candidates, unchanged 1.x visibility, and no G-04C authorization.
3. Do not rewrite any G-04B1 query count, evidence status, risk finding, or historical conclusion.

- [ ] **Step 5: Update the active governance guide**

Use `apply_patch` on `guide/dev/public-api-governance.md` so the
`## 2.0 Consumer Declaration Preparation` section becomes
`## 2.0 Consumer Declaration Window` and states:

- current state `open`;
- actual Issue link and opening timestamp;
- 136 candidates remain review-only;
- 1.x access levels remain unchanged;
- no-public-match is not removal proof;
- closure requires at least one later non-prerelease stable release, all feedback disposition, and independent audit;
- `eligibleAfterRelease` remains unset;
- G-04B2 does not authorize G-04C.

- [ ] **Step 6: Prove candidate immutability and exact root-level drift**

Run:

```bash
jq -S '.candidates' docs/governance/bukit-core-2.0-public-surface-candidates.v1.json > /tmp/g04b2-candidates-after.json
cmp /tmp/g04b2-candidates-before.json /tmp/g04b2-candidates-after.json
jq -S 'del(.declarationState, .feedbackChannel.issueNumber, .feedbackChannel.state, .windowPolicy.openedAtUtc, .windowPolicy.announcementUrl)' /tmp/g04b2-manifest-before.json > /tmp/g04b2-manifest-before-invariants.json
jq -S 'del(.declarationState, .feedbackChannel.issueNumber, .feedbackChannel.state, .windowPolicy.openedAtUtc, .windowPolicy.announcementUrl)' docs/governance/bukit-core-2.0-public-surface-candidates.v1.json > /tmp/g04b2-manifest-after-invariants.json
cmp /tmp/g04b2-manifest-before-invariants.json /tmp/g04b2-manifest-after-invariants.json
jq -e --slurpfile issue /tmp/g04b2-created-issue.json '
  .candidateCount == 136 and
  .declarationState == "open" and
  .feedbackChannel.kind == "github-issue" and
  .feedbackChannel.repository == "ALi365-SDN-BHD/Bukit" and
  .feedbackChannel.state == "open" and
  .feedbackChannel.issueNumber == $issue[0].issueNumber and
  .windowPolicy.openedAtUtc == $issue[0].createdAt and
  .windowPolicy.announcementUrl == $issue[0].issueUrl and
  .windowPolicy.eligibleAfterRelease == null and
  .windowPolicy.minimumStableReleaseCycles == 1 and
  (.candidates | length) == 136 and
  all(.candidates[]; .declarationStatus == "consumer-declaration-pending" and .proposedAction == "review-only")
' docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
```

Expected: both `cmp` commands exit `0`; `jq` prints `true` and exits `0`.

- [ ] **Step 7: Prove document lifecycle consistency and forbidden claims**

Run a script that reads the actual Issue URL, number, and `createdAt` from
`/tmp/g04b2-created-issue.json` and asserts all three appear where required in the declaration, B1 follow-up, and active guide. Then run:

```bash
rg -n 'Status: `open`|current lifecycle|当前生命周期|eligibleAfterRelease|later non-prerelease stable|后续正式稳定版本|G-04C' \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md \
  guide/dev/public-api-governance.md
if rg -n "T[B]D|T[O]DO|F[I]XME|Issue does not yet exist|window remains closed|approved for removal|safe to delete|no external consumers|G-04C is approved" \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  guide/dev/public-api-governance.md; then exit 1; fi
```

Expected: required state is present; forbidden scan has no matches in active documents. Historical G-04B1 text may still say the Issue did not exist at that snapshot only when clearly scoped by the new lifecycle banner.

- [ ] **Step 8: Run the task-owned verification**

Run in the non-sandbox environment:

```bash
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore
bash scripts/checks/post-change-targeted.sh -- \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md \
  guide/dev/public-api-governance.md \
  docs/superpowers/specs/2026-07-21-bukit-core-g04b2-consumer-declaration-opening-design.zh-CN.md \
  docs/superpowers/plans/2026-07-21-bukit-core-g04b2-consumer-declaration-opening.md
git diff --check
```

Expected: every command exits `0`; public API build has zero warnings/errors; Architecture Tests report 81/81; no full or release gate runs.

- [ ] **Step 9: Commit the independently valid publication state**

Run:

```bash
git add \
  docs/governance/bukit-core-2.0-public-surface-candidates.v1.json \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md \
  guide/dev/public-api-governance.md
git commit -m "docs(governance): open G-04B2 consumer declaration"
```

Expected: one commit contains only the four lifecycle files. The design and plan remain separate earlier commits.

### Task 5: Audit, publish, and compensate on failure

**Files:**
- Review: all commits in `/tmp/g04b2-origin-main.txt..HEAD`
- Read outside repository: `/tmp/g04b2-created-issue.json`
- Test: aggregate read-only review, remote ancestry, push result

**Interfaces:**
- Consumes: Task 4's verified publication commit and Task 3's live Issue.
- Produces: either a successfully fast-forwarded `origin/main`, or a closed opening-paused Issue with no completion claim.

- [ ] **Step 1: Request one independent read-only aggregate review before push**

Give the reviewer:

- the approved design and this plan;
- base SHA from `/tmp/g04b2-origin-main.txt` and current `HEAD`;
- `/tmp/g04b2-created-issue.json`, title, body, and fetched Issue snapshot;
- the exact gate outputs from Task 4;
- the full branch diff.

Require the reviewer to check the external-write approval, exact metadata, candidate immutability, four-file lifecycle consistency, no Release/source/schema/protocol/baseline/G-04C drift, compensation readiness, and path scope. The reviewer is read-only and must report Critical/Important/Minor findings with file/line evidence.

Expected: no unresolved Critical or Important finding. Fix valid findings only in the affected governance paths, rerun Task 4's assertions/gate, and repeat the necessary review.

- [ ] **Step 2: Refresh origin immediately before push**

Run in the non-sandbox environment:

```bash
git fetch origin main
test "$(git rev-parse origin/main)" = "$(cat /tmp/g04b2-origin-main.txt)"
git merge-base --is-ancestor origin/main HEAD
git status --short --branch
```

Expected: remote SHA is unchanged from Task 1, origin is an ancestor of `HEAD`, and the worktree is clean. If the remote changed, do not rebase, merge, or push in this opening attempt; close the Issue through Step 5 and require a new reviewed attempt.

- [ ] **Step 3: Prove the final diff is exactly in scope**

Run:

```bash
git diff --check "$(cat /tmp/g04b2-origin-main.txt)"..HEAD
git diff --name-only "$(cat /tmp/g04b2-origin-main.txt)"..HEAD
git log --oneline "$(cat /tmp/g04b2-origin-main.txt)"..HEAD
```

Expected paths are exactly:

```text
docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md
docs/governance/bukit-core-2.0-consumer-declaration.md
docs/governance/bukit-core-2.0-public-surface-candidates.v1.json
docs/superpowers/plans/2026-07-21-bukit-core-g04b2-consumer-declaration-opening.md
docs/superpowers/specs/2026-07-21-bukit-core-g04b2-consumer-declaration-opening-design.zh-CN.md
guide/dev/public-api-governance.md
```

No `src/`, `tests/`, `.github/workflows/`, Release, schema, protocol, baseline, asset, or backup path may appear.

- [ ] **Step 4: Fast-forward the reviewed branch to remote main**

Run only after Steps 1-3 pass:

```bash
git push origin HEAD:main
```

Expected: push succeeds as a fast-forward update. Never add `--force` or `--force-with-lease`.

- [ ] **Step 5: Compensate if any post-creation stage cannot complete**

If Task 3 read-back, Task 4 verification/commit, Task 5 review/pre-push/push, or immediate post-push consistency cannot complete, build a replacement body by prepending this exact notice to the original Issue body:

```markdown
> **Opening paused**
>
> Repository publication did not complete consistently, so this Issue is closed and is not an active consumer declaration window. The governed repository remains authoritative. A future retry will use a new Issue and a new opening timestamp.

```

Then call:

```text
github_update_issue({
  repository_full_name: "ALi365-SDN-BHD/Bukit",
  issue_number: actual issueNumber,
  body: paused notice followed by the complete original body,
  state: "closed"
})
```

Fetch the Issue again and require `state = closed` plus the opening-paused notice. Do not reopen this Issue and do not claim G-04B2 complete. If the compensation call itself fails, report the unresolved external state immediately and stop.

### Task 6: Verify the published remote state and record the handoff

**Files:**
- Verify on `origin/main`: the six planned paths
- Read outside repository: `/tmp/g04b2-created-issue.json`
- Test: Issue read-back, remote commit equality, remote file reconciliation

**Interfaces:**
- Consumes: Task 5's successful fast-forward push.
- Produces: evidence that G-04B2 is open and consistent; it does not produce G-04C eligibility.

- [ ] **Step 1: Confirm the remote branch contains the reviewed HEAD**

Run:

```bash
git fetch origin main
test "$(git rev-parse origin/main)" = "$(git rev-parse HEAD)"
```

Expected: equality assertion exits `0`.

- [ ] **Step 2: Fetch the live Issue again**

Call:

```text
github_fetch_issue({
  repository_full_name: "ALi365-SDN-BHD/Bukit",
  issue_number: actual issueNumber
})
```

Expected: Issue is open; title, URL, number, body, and `createdAt` match Task 3.

- [ ] **Step 3: Reconcile the remote manifest against the live Issue**

Read the manifest from `origin/main` without trusting the worktree copy:

```bash
git show origin/main:docs/governance/bukit-core-2.0-public-surface-candidates.v1.json > /tmp/g04b2-remote-manifest.json
jq -e --slurpfile issue /tmp/g04b2-created-issue.json '
  .declarationState == "open" and
  .feedbackChannel.state == "open" and
  .feedbackChannel.issueNumber == $issue[0].issueNumber and
  .windowPolicy.announcementUrl == $issue[0].issueUrl and
  .windowPolicy.openedAtUtc == $issue[0].createdAt and
  .windowPolicy.eligibleAfterRelease == null and
  (.candidates | length) == 136 and
  all(.candidates[]; .declarationStatus == "consumer-declaration-pending" and .proposedAction == "review-only")
' /tmp/g04b2-remote-manifest.json
```

Expected: prints `true` and exits `0`.

- [ ] **Step 4: Reconcile all remote documents**

Read the three remote documents explicitly:

```bash
git show origin/main:docs/governance/bukit-core-2.0-consumer-declaration.md > /tmp/g04b2-remote-declaration.md
git show origin/main:docs/analysis/bukit-core-g04b-external-consumer-declaration-preparation-2026-07-20.zh-CN.md > /tmp/g04b2-remote-report.md
git show origin/main:guide/dev/public-api-governance.md > /tmp/g04b2-remote-guide.md
```

Assert each contains the actual Issue URL and correct lifecycle wording; assert the declaration and guide contain no prepared/closed-window claim; assert the B1 report preserves its historical snapshot and clearly labels the current open follow-up.

Expected: all assertions exit `0` and no local absolute path or placeholder appears.

- [ ] **Step 5: Record final state without extending authorization**

Report:

- Issue number and URL;
- Issue `createdAt` and current state;
- remote `main` commit SHA;
- gate/test results;
- candidate count and unchanged candidate checksum;
- `eligibleAfterRelease = null`;
- G-04C remains unauthorized;
- the local main worktree's unrelated dirty changes were untouched;
- the feature worktree remains available until an explicit cleanup decision.

Do not edit a Release, start a closure timer based only on days, mark G-04C eligible, or create a follow-on issue automatically.
