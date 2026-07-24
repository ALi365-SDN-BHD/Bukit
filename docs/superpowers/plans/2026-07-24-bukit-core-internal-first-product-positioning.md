# Bukit Core Internal-First Product Positioning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Route 2 the Bukit Core product direction and Route 3 the current internal-first operating mode across every active product, guide, governance, contribution, and release entry point.

**Architecture:** Keep one canonical current policy in `docs/governance/` and place short, semantically equivalent notices in the three READMEs and guide entry points. Preserve historical audit, plan, artifact, and AOT evidence unchanged; release documentation retains technical validation instructions while separating validation from authorization.

**Tech Stack:** Markdown, repository documentation gates, Bash focused/targeted verification, Git.

## Global Constraints

- Route 2, “deterministic trusted-content publishing compiler”, is the product and architecture direction.
- Route 3, “internal stable engine”, is the current operating and investment mode.
- Enterprise internal use has priority.
- The repository and existing open-source license remain public.
- External use is self-directed and has no public support, SLA, compatibility, product-readiness, or fixed release-cadence promise.
- Regular public binary releases are paused; an exceptional public release requires explicit management approval and existing release evidence.
- “Stable” means a governed internal Core contract, not commercial support or a public compatibility SLA.
- Labs, Import, WeChat, and other external-plugin business implementations are not promoted to Core or made release-ready.
- Do not modify runtime behavior, CLI output, schemas, CLR APIs, plugin protocols, persistent formats, workflows, Labs, or plugin implementation code.
- Do not rewrite `docs/analysis/`, existing historical plans/specs, `guide/archive/`, or protected backup/reference directories.
- Preserve historical occurrences of `2.0.0-alpha.1` that describe an earlier decision, command, test, or artifact.
- Run `post-change-focused.sh` after each task.
- Capture one documentation implementation base after the existing version change is committed, and run `post-change-targeted.sh` exactly once for the aggregate documentation diff.
- Do not run full, release, coverage, smoke-all, test-all, or whole-solution gates.

---

### Task 0: Isolate and commit the already approved `2.0.0` version change

**Files:**
- Modify: `Directory.Build.props`
- Modify: `tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs`

**Interfaces:**
- Consumes: the already verified working-tree change from `2.0.0-alpha.1` to `2.0.0`.
- Produces: a clean, committed `2.0.0` product-version baseline for the documentation implementation.

- [ ] **Step 1: Confirm the version diff is isolated**

Run:

```bash
git diff -- Directory.Build.props \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs
```

Expected: only the MSBuild version, test method name, and exact expected version
change from `2.0.0-alpha.1` to `2.0.0`.

- [ ] **Step 2: Re-run the direct version proof**

Run:

```bash
dotnet msbuild src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj \
  -getProperty:Version -nologo
```

Expected:

```text
2.0.0
```

- [ ] **Step 3: Re-run focused verification if the current session lacks fresh evidence**

Run outside the restricted sandbox if NuGet vulnerability-cache access returns
`NU1900`:

```bash
bash scripts/checks/post-change-focused.sh -- \
  Directory.Build.props \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs
```

Expected: format and analyzer self-tests pass; Architecture is 278 passed,
0 failed, 0 skipped.

- [ ] **Step 4: Commit only the version change**

```bash
git add Directory.Build.props \
  tests/Bukit.Architecture.Tests/G04CPublicSurfacePilotTests.cs
git diff --cached --name-only
git commit -m "chore(core): release version 2.0.0"
```

Expected staged paths: exactly the two paths above.

- [ ] **Step 5: Capture the documentation implementation base**

```bash
git rev-parse HEAD
git status --short
```

Expected: clean worktree. Record the exact HEAD as `implementation_base` before
Task 1.

---

### Task 1: Establish the canonical current product-positioning policy

**Files:**
- Create: `docs/governance/bukit-core-product-positioning.md`
- Modify: `CHANGELOG.md`

**Interfaces:**
- Consumes: the approved design in `docs/superpowers/specs/2026-07-24-bukit-core-internal-first-product-positioning-design.md`.
- Produces: the canonical current policy linked by all later entry points.

- [ ] **Step 1: Create the canonical policy**

Create `docs/governance/bukit-core-product-positioning.md` with this normative
structure and meaning:

```markdown
# Bukit Core Product Positioning

> Effective: 2026-07-24
>
> Current mode: enterprise internal-first

## Decision

Bukit Core adopts Route 2 as its product direction and Route 3 as its current
operating mode:

- **Route 2 - deterministic trusted-content publishing compiler:** invest in
  deterministic builds, Markdown and Notion ingestion, canonical publishing
  contracts, routing, rendering, SEO/GEO, audit evidence, and Native AOT.
- **Route 3 - internal stable engine:** prioritize controlled enterprise use,
  existing internal sites, reliability, observability, and bounded maintenance
  over general-purpose SSG expansion.

Route 1, broad general-purpose SSG competition, is not a current objective.

## Internal-First Status

Enterprise-owned publishing projects are the priority consumers. New Core
capability requires a named internal consumer, a concrete business case, an
owner, and verifiable acceptance criteria.

“Stable” means a governed internal Core contract. It does not mean commercial
support or a public compatibility SLA.

## Public Repository And External Use

The repository and its existing open-source license remain public. External
parties may inspect, fork, build, or use Bukit under that license, but use is
self-directed. The project currently provides no public support commitment,
SLA, compatibility guarantee, product-readiness promise, or fixed release
cadence.

## Release Policy

Regular public binary releases are paused. Internal builds, internal
deployments, and internal release-candidate validation may continue.

An exceptional public release requires explicit management approval plus all
applicable technical release evidence. Passing a build, CI, release workflow,
or artifact gate proves technical state only; it does not authorize
publication.

## Scope

This policy applies to Bukit Core. Labs, Import, WeChat, and other
external-plugin business implementations are not promoted to Core and are not
made release-ready by this policy. Their own audits and maturity decisions
remain authoritative.

## Investment Rules

- Fix correctness, safety, contract truth, and observability before adding
  features.
- Prefer controlled simplification over public-surface expansion.
- Do not add general-purpose integrations without proven internal demand.
- Freeze or remove unused capability through versioned governance when its
  internal value is not demonstrated.

## Historical Evidence

Historical audits, plans, release records, version strings, and artifact
evidence retain their original meaning. This document governs current product
operations without rewriting those records.
```

- [ ] **Step 2: Record the current policy in the changelog**

Under `## [Unreleased]` and before `### Security And Reliability`, add:

```markdown
### Product Positioning

- **Internal-first operating mode**: Bukit Core adopts Route 2, a deterministic
  trusted-content publishing compiler, as its product direction and Route 3,
  an internal stable engine, as its current operating mode. The public
  repository and license remain available, but regular public binary releases,
  public support, compatibility guarantees, SLA commitments, and a fixed
  release cadence are paused.
```

- [ ] **Step 3: Run focused documentation verification**

```bash
bash scripts/checks/post-change-focused.sh -- \
  docs/governance/bukit-core-product-positioning.md \
  CHANGELOG.md
```

Expected: exit 0.

- [ ] **Step 4: Commit the canonical policy**

```bash
git add docs/governance/bukit-core-product-positioning.md CHANGELOG.md
git commit -m "docs(core): establish internal-first product policy"
```

---

### Task 2: Synchronize the English, Chinese, and Malay README entry points

**Files:**
- Modify: `README.md`
- Modify: `README.zh-CN.md`
- Modify: `README.ms.md`

**Interfaces:**
- Consumes: `docs/governance/bukit-core-product-positioning.md`.
- Produces: equivalent product-status, stability, roadmap, contribution, and release expectations in all three root entry points.

- [ ] **Step 1: Add equivalent status notices after each opening description**

The English notice must state:

```markdown
> **Current product status:** Bukit Core 2.0 is enterprise internal-first.
> Route 2, a deterministic trusted-content publishing compiler, is the product
> direction; Route 3, an internal stable engine, is the current operating mode.
> The repository and license remain public, but external use is self-directed
> without public support, SLA, compatibility, product-readiness, or release
> cadence commitments. Regular public binary releases are paused.
> An exceptional public release requires explicit management approval. Labs
> and external plugins are outside Core release readiness.
> See [Bukit Core Product Positioning](docs/governance/bukit-core-product-positioning.md).
```

The Chinese notice must express:

```markdown
> **当前产品状态：** Bukit Core 2.0 优先供企业内部使用。路线 2
> “确定性的可信内容发布编译器”是产品方向；路线 3“内部稳定引擎”是当前运营模式。
> 仓库和许可证继续公开，但外部使用者需自行评估，项目不提供公开支持、SLA、兼容性、
> 产品就绪或固定发布节奏承诺。常规公开二进制发布暂停。
> 例外公开发布必须获得明确的管理批准；Labs 和外部插件不属于 Core 发布就绪范围。
> 详见 [Bukit Core 产品定位](docs/governance/bukit-core-product-positioning.md)。
```

The Malay notice must express:

```markdown
> **Status produk semasa:** Bukit Core 2.0 mengutamakan penggunaan dalaman
> perusahaan. Laluan 2, pengkompil penerbitan kandungan dipercayai yang
> deterministik, ialah hala tuju produk; Laluan 3, enjin stabil dalaman, ialah
> mod operasi semasa. Repositori dan lesen kekal terbuka, tetapi penggunaan
> luaran adalah kendiri tanpa komitmen sokongan awam, SLA, keserasian,
> kesediaan produk, atau jadual keluaran tetap. Keluaran binari awam berkala
> dihentikan sementara.
> Keluaran awam luar biasa memerlukan kelulusan pengurusan yang eksplisit;
> Labs dan plugin luaran berada di luar kesediaan keluaran Core.
> Lihat [Kedudukan Produk Bukit Core](docs/governance/bukit-core-product-positioning.md).
```

- [ ] **Step 2: Move current Core wording from 1.0 public stability to 2.0 internal stability**

In all three READMEs:

- change the capability heading from Core 1.0 to Core 2.0;
- describe the command surface as the governed Core 2.0 command surface, not a
  public support promise;
- change local-theme boundary text from Core 1.0 to Core 2.0;
- change agent-skill alignment from Core 1.0 to Core 2.0;
- rename the stability section to “Internal Stability Scope” and translations;
- state that the listed surface is governed for internal use;
- retain the exact command and feature inventories;
- retain the “not included” list but label it as outside internal Core 2.0.

- [ ] **Step 3: Replace the expansion roadmap with Route 2/Route 3 priorities**

Use equivalent tables in all three languages:

| Priority | Status |
|---|---|
| Deterministic build, safety, contract truth, audits | Route 2 - active |
| Markdown/Notion internal publishing | Route 2 - active |
| Existing enterprise sites and controlled deployment | Route 3 - priority |
| Reliability, observability, maintenance simplification | Route 3 - priority |
| General SSG expansion and broad integrations | Not a current objective |
| Theme/plugin marketplace and public ecosystem growth | Paused |
| Labs and external plugins | Separate, not Core release-ready |

- [ ] **Step 4: Replace unconditional contribution language**

The English contribution section must say:

```markdown
The repository remains open under its existing license. External issues and
contributions may be reviewed, but acceptance, response time, compatibility,
support, and release timing are not guaranteed. Internal business priorities
take precedence.
```

Add semantically equivalent Chinese and Malay text before the existing
maintainer links.

- [ ] **Step 5: Run README contract verification**

```bash
bash scripts/checks/readme-sync.sh
bash scripts/checks/post-change-focused.sh -- \
  README.md README.zh-CN.md README.ms.md
```

Expected: README sync and focused checks exit 0.

- [ ] **Step 6: Manually compare the eight normative facts**

Run:

```bash
rg -n \
  'Route 2|Route 3|internal-first|public binary releases|support|SLA|compatibility|Labs|plugin' \
  README.md
rg -n \
  '路线 2|路线 3|企业内部|公开二进制|支持|SLA|兼容性|Labs|插件' \
  README.zh-CN.md
rg -n \
  'Laluan 2|Laluan 3|dalaman perusahaan|binari awam|sokongan|SLA|keserasian|Labs|plugin' \
  README.ms.md
```

Expected: each README contains the current-mode notice, external-use boundary,
release pause, and Core/plugin separation.

- [ ] **Step 7: Commit the synchronized READMEs**

```bash
git add README.md README.zh-CN.md README.ms.md
git commit -m "docs(readme): mark Bukit Core internal-first"
```

---

### Task 3: Align contribution, security, and pull-request expectations

**Files:**
- Modify: `CONTRIBUTING.md`
- Modify: `CONTRIBUTING.zh-CN.md`
- Modify: `CONTRIBUTING.ms.md`
- Modify: `SECURITY.md`
- Modify: `SECURITY.zh-CN.md`
- Modify: `SECURITY.ms.md`
- Modify: `.github/PULL_REQUEST_TEMPLATE.md`

**Interfaces:**
- Consumes: the canonical positioning policy.
- Produces: public collaboration and vulnerability-reporting entry points that preserve technical instructions without promising public service levels.

- [ ] **Step 1: Add equivalent internal-priority notices to the contribution guides**

After each contribution guide title, add an English, Chinese, or Malay notice
with this exact normative meaning:

```markdown
Bukit Core is currently enterprise internal-first. The repository and license
remain public, and external contributions may be reviewed, but review,
acceptance, response time, compatibility, support, and release timing are not
guaranteed. Internal business priorities take precedence. See
[Bukit Core Product Positioning](docs/governance/bukit-core-product-positioning.md).
```

Use the same relative link in all three root contribution files. Preserve every
existing build, test, style, AOT, and pull-request instruction.

- [ ] **Step 2: Replace the public security SLA with a best-effort boundary**

In all three security policies:

- replace the `1.0.x` supported-version row with two rows:
  - `2.0.x`: governed for internal use; no public support SLA;
  - `1.x`: historical; no public support commitment;
- retain private vulnerability reporting and the prohibition on public issue
  disclosure;
- replace the English “acknowledge within 7 days / fix within 30 days” promise
  and its translations with:

```markdown
Good-faith private reports are welcome and may be reviewed on a best-effort
basis. The project does not promise a public acknowledgement deadline,
remediation deadline, support SLA, or release timeline.
```

- add a link to
  `docs/governance/bukit-core-product-positioning.md`;
- preserve all current Core safety, Labs, plugin, and secret-handling guidance.

- [ ] **Step 3: Add a product-positioning checklist to the PR template**

Before `## 质量门禁`, add:

```markdown
## 产品定位

- [ ] 变更服务于明确的内部消费者或维护现有受治理契约
- [ ] 未把 Labs、Import、WeChat 或外部插件表述为 Core 发布就绪
- [ ] 如涉及公开发布，已单独记录明确管理批准；否则按内部制品处理
```

- [ ] **Step 4: Run public-document focused verification**

```bash
bash scripts/checks/post-change-focused.sh -- \
  CONTRIBUTING.md \
  CONTRIBUTING.zh-CN.md \
  CONTRIBUTING.ms.md \
  SECURITY.md \
  SECURITY.zh-CN.md \
  SECURITY.ms.md \
  .github/PULL_REQUEST_TEMPLATE.md
```

Expected: exit 0.

- [ ] **Step 5: Prove the retired SLA text is absent**

```bash
if rg -n \
  'acknowledge your report within 7 days|fix within 30 days|7 天内确认|30 天内|dalam masa 7 hari|dalam masa 30 hari' \
  SECURITY.md SECURITY.zh-CN.md SECURITY.ms.md; then
  echo "stale public security SLA remains" >&2
  exit 1
fi
```

Expected: exit 0 with no matches.

- [ ] **Step 6: Commit collaboration and security policy**

```bash
git add CONTRIBUTING.md \
  CONTRIBUTING.zh-CN.md \
  CONTRIBUTING.ms.md \
  SECURITY.md \
  SECURITY.zh-CN.md \
  SECURITY.ms.md \
  .github/PULL_REQUEST_TEMPLATE.md
git commit -m "docs(policy): align public expectations with internal use"
```

---

### Task 4: Synchronize Core guide entry points and stability semantics

**Files:**
- Modify: `guide/README.md`
- Modify: `guide/user/README.md`
- Modify: `guide/dev/README.md`
- Modify: `guide/dev/public-preview-scope.md`
- Modify: `guide/dev/public-api-governance.md`
- Modify: `guide/dev/documentation-governance.md`
- Modify: `docs/governance/bukit-core-2.0-consumer-declaration.md`
- Modify: `docs/governance/bukit-core-2.0-notion-compatibility-migration.md`

**Interfaces:**
- Consumes: the canonical positioning policy and README terminology.
- Produces: a consistent internal-first interpretation for users, maintainers, and future documentation updates.

- [ ] **Step 1: Add an internal-first status block to `guide/README.md`**

After the opening source-tree paragraph, add:

```markdown
## Current Product Mode

Bukit Core 2.0 uses Route 2, a deterministic trusted-content publishing
compiler, as its product direction and Route 3, an internal stable engine, as
its current operating mode. Enterprise internal use has priority. External use
under the public license is self-directed and carries no public support, SLA,
compatibility, or release-cadence commitment.

Regular public binary releases are paused; an exceptional public release
requires explicit management approval. Labs and external plugins remain
outside Core release readiness.

See [Bukit Core Product Positioning](../docs/governance/bukit-core-product-positioning.md).
```

- [ ] **Step 2: Add the user-facing boundary to `guide/user/README.md`**

Before `## Reading Path`, add:

```markdown
> This guide documents the governed Bukit Core 2.0 surface used by internal
> enterprise sites. It is public reference material, not a public support,
> compatibility, product-readiness, or release-cadence commitment. See the
> [current product positioning](../../docs/governance/bukit-core-product-positioning.md).
> Regular public binary releases are paused, exceptional publication requires
> explicit management approval, and Labs or external plugins are outside Core
> release readiness.
```

Rename `## Stable Core Commands` to `## Governed Core Commands` without
changing the command list.

- [ ] **Step 3: Add the maintainer boundary to `guide/dev/README.md`**

After the opening paragraph, add:

```markdown
Bukit Core currently follows Route 2 for technical direction and Route 3 for
internal-first operation. Maintenance decisions prioritize named internal
consumers, reliability, contract truth, and controlled simplification. See
[Bukit Core Product Positioning](../../docs/governance/bukit-core-product-positioning.md).

Regular public binary releases are paused and require explicit management
approval as an exception. Labs and external plugins remain outside Core release
readiness.
```

Add a “Product positioning” row linking the canonical policy to the documents
table.

- [ ] **Step 4: Rewrite `guide/dev/public-preview-scope.md` as an expectation boundary**

Keep the existing Core and non-Core feature lists, but use this opening:

```markdown
# Core Stability And External Expectation Scope

Core documents describe governed internal behavior. “Stable” means an internal
Core contract protected by source and tests; it does not imply public support,
an SLA, compatibility guarantees, product readiness, or a fixed release
cadence.

Route 2 is the technical direction and Route 3 is the current internal-first
operating mode. Labs and preview material cannot be used as proof of Core
support or release readiness.
```

Rename `## Stable In Core` to `## Governed In Internal Core` and
`## Outside Stable Core` to `## Outside Internal Core`.

- [ ] **Step 5: Clarify public API governance without changing API decisions**

At the start of `guide/dev/public-api-governance.md`, link the canonical policy
and state:

```markdown
The CLI, configuration, theme, template, report, and process-protocol surfaces
below are governed interoperability contracts for internal Core operation.
“Supported” in this document identifies the intended technical contract; it
does not create public support, SLA, compatibility, product-readiness, or
release-cadence commitments.
```

Do not change any type classification, migration decision, baseline count, or
CLR compatibility statement.

- [ ] **Step 6: Add current-policy banners to active 2.0 governance records**

Add a short banner below the title of both:

- `docs/governance/bukit-core-2.0-consumer-declaration.md`;
- `docs/governance/bukit-core-2.0-notion-compatibility-migration.md`.

The banner must say that the document remains authoritative for technical
compatibility and migration, while current product support and release
expectations are governed by
`bukit-core-product-positioning.md`. Do not alter historical counts, decisions,
or migration consequences.

- [ ] **Step 7: Add the policy source to documentation governance**

In `guide/dev/documentation-governance.md`:

- insert `docs/governance/bukit-core-product-positioning.md` after current
  source code/tests in Source Priority for product positioning questions;
- require README, guide entry points, stability wording, contribution wording,
  and release authorization wording to remain synchronized;
- state that historical audit/plan wording is not rewritten when current
  product policy changes.

- [ ] **Step 8: Run guide focused verification**

```bash
bash scripts/checks/post-change-focused.sh -- \
  guide/README.md \
  guide/user/README.md \
  guide/dev/README.md \
  guide/dev/public-preview-scope.md \
  guide/dev/public-api-governance.md \
  guide/dev/documentation-governance.md \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  docs/governance/bukit-core-2.0-notion-compatibility-migration.md
```

Expected: exit 0.

- [ ] **Step 9: Commit guide positioning**

```bash
git add guide/README.md \
  guide/user/README.md \
  guide/dev/README.md \
  guide/dev/public-preview-scope.md \
  guide/dev/public-api-governance.md \
  guide/dev/documentation-governance.md \
  docs/governance/bukit-core-2.0-consumer-declaration.md \
  docs/governance/bukit-core-2.0-notion-compatibility-migration.md
git commit -m "docs(guide): align Core guidance with internal use"
```

---

### Task 5: Separate release validation from public-release authorization

**Files:**
- Modify: `guide/dev/release.md`
- Modify: `guide/dev/release-checklist.md`
- Modify: `docs/release/release-prerelease-template.md`

**Interfaces:**
- Consumes: the canonical release policy.
- Produces: retained internal artifact procedures plus an explicit management authorization gate for any public release.

- [ ] **Step 1: Add the authorization boundary to `guide/dev/release.md`**

After the title, add:

```markdown
## Authorization Boundary

Bukit Core is currently enterprise internal-first. Regular public binary
releases are paused. The procedures below remain active for internal artifacts,
internal deployment validation, and an exceptionally approved public release.

Passing CI, Native AOT, reproducibility, smoke, coverage, security, checksum,
or release-asset verification proves technical state only. It does not
authorize publication. A public release requires explicit management approval.
```

Do not alter any existing command, RID, archive, checksum, or failure-semantics
instruction.

- [ ] **Step 2: Add Step 0 to `guide/dev/release-checklist.md`**

Replace the opening with:

```markdown
# Release Checklist

Use this checklist for internal artifact qualification or an explicitly
approved public release. Regular public binary releases are paused.

0. Record the release purpose:
   - `internal-artifact`, which must not publish a GitHub Release; or
   - `public-release`, which requires explicit management approval before any
     tag, upload, or publication.
```

Retain the current eight technical steps after Step 0. Add a final sentence:

```markdown
Technical success cannot upgrade `internal-artifact` to `public-release`.
```

- [ ] **Step 3: Add the same precondition to the maintainer prerelease template**

Immediately below the title in
`docs/release/release-prerelease-template.md`, add:

```markdown
> 当前默认用途是企业内部制品验证，常规公开二进制发布暂停。
> 若任务未记录明确的管理批准，不得创建公开 tag、上传公开资产或发布 GitHub Release。
> CI、coverage、security、Native AOT、smoke 和资产校验通过只证明技术状态，不构成发布授权。
```

Rename `## 发布前主干 CI 预检（强制）` to
`## 已授权公开发布的主干 CI 预检（强制）`. Preserve all technical commands.

- [ ] **Step 4: Run release-document focused verification**

```bash
bash scripts/checks/post-change-focused.sh -- \
  guide/dev/release.md \
  guide/dev/release-checklist.md \
  docs/release/release-prerelease-template.md
```

Expected: exit 0.

- [ ] **Step 5: Audit technical instruction preservation**

Run:

```bash
rg -n \
  'native-aot.sh|build-repro.sh|prepare-release-assets.sh|verify-release-assets.sh|release-assets-self-test.sh|release-artifacts-self-test.sh' \
  guide/dev/release.md
rg -n \
  'Fast contracts|Core tests|Core coverage|Security check|verify-release-assets.sh' \
  docs/release/release-prerelease-template.md
```

Expected: all existing release validation entry points remain discoverable.

- [ ] **Step 6: Commit release authorization documentation**

```bash
git add guide/dev/release.md \
  guide/dev/release-checklist.md \
  docs/release/release-prerelease-template.md
git commit -m "docs(release): require approval for public publication"
```

---

### Task 6: Aggregate documentation verification and final audit

**Files:**
- Review every path changed since the exact Task 0 implementation base.

**Interfaces:**
- Consumes: Tasks 1-5.
- Produces: one verified, internally consistent current product policy with no historical or runtime drift.

- [ ] **Step 1: Freeze the aggregate path list**

```bash
changed_paths=()
while IFS= read -r path; do
  changed_paths+=("$path")
done < <(git diff --name-only "$implementation_base"..HEAD | LC_ALL=C sort)
printf '%s\n' "${changed_paths[@]}"
```

Expected paths:

```text
.github/PULL_REQUEST_TEMPLATE.md
CHANGELOG.md
CONTRIBUTING.md
CONTRIBUTING.ms.md
CONTRIBUTING.zh-CN.md
README.md
README.ms.md
README.zh-CN.md
SECURITY.md
SECURITY.ms.md
SECURITY.zh-CN.md
docs/governance/bukit-core-2.0-consumer-declaration.md
docs/governance/bukit-core-2.0-notion-compatibility-migration.md
docs/governance/bukit-core-product-positioning.md
docs/release/release-prerelease-template.md
guide/README.md
guide/dev/README.md
guide/dev/documentation-governance.md
guide/dev/public-api-governance.md
guide/dev/public-preview-scope.md
guide/dev/release-checklist.md
guide/dev/release.md
guide/user/README.md
```

Stop if any runtime, workflow, test, historical analysis/plan, Labs, plugin, or
protected backup/reference path appears.

- [ ] **Step 2: Run the one aggregate targeted gate**

On macOS Bash 3.2, populate the array with the repository-compatible loop:

```bash
changed_paths=()
while IFS= read -r path; do
  changed_paths+=("$path")
done < <(git diff --name-only "$implementation_base"..HEAD | LC_ALL=C sort)

bash scripts/checks/post-change-targeted.sh \
  --base "$implementation_base" \
  -- "${changed_paths[@]}"
```

Expected: exit 0. Do not repeat this command.

- [ ] **Step 3: Run direct documentation owner checks**

```bash
bash scripts/checks/docs-consistency.sh
bash scripts/checks/readme-sync.sh
git diff --check "$implementation_base"..HEAD
```

Expected: all exit 0.

- [ ] **Step 4: Prove protected and historical surfaces were not rewritten**

```bash
git diff --name-only "$implementation_base"..HEAD -- \
  docs/analysis \
  docs/superpowers \
  guide/archive \
  guide-0.1 \
  guide-0.2 \
  scripts-0.1 \
  scripts-0.2
```

Expected: no output. The already committed design and implementation-plan
documents precede `implementation_base` and therefore are not part of this
documentation implementation diff.

- [ ] **Step 5: Audit the eight normative facts**

Confirm that the canonical policy, each README, and each guide entry point
answer:

1. Route 2 is the product direction.
2. Route 3 is the operating mode.
3. Enterprise internal use is prioritized.
4. Repository and license remain public.
5. External use has no support, SLA, compatibility, readiness, or cadence
   promise.
6. Regular public binary releases are paused.
7. Exceptional publication requires explicit management approval.
8. Labs and external plugins are outside Core release readiness.

Record any missing fact as a documentation defect and correct only the owning
active document. Do not rerun the aggregate targeted gate after a correction;
instead stop and request a replacement aggregate authorization under repository
governance.

- [ ] **Step 6: Confirm final state**

```bash
git status --short --branch
git log --oneline "$implementation_base"..HEAD
```

Expected: clean tracked worktree and only the five documentation implementation
commits from Tasks 1-5.
