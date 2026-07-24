# Bukit Core Internal-First Product Positioning Design

> Date: 2026-07-24
>
> Status: approved design
>
> Scope: active product-positioning documentation only

## 1. Decision

Bukit adopts both of the following routes:

1. **Route 2 - deterministic trusted-content publishing compiler** is the
   product and architecture direction.
2. **Route 3 - internal stable engine** is the current operating and investment
   mode.

These routes are complementary. Route 2 defines what Bukit is built to do.
Route 3 defines how the project is currently distributed, supported, and
funded.

Bukit does not currently pursue Route 1, a general-purpose static-site
generator competing primarily on breadth of integrations or feature count.

## 2. Current Product Status

Bukit Core 2.0 is internal-first and is intended primarily for controlled use
inside the enterprise and its owned publishing projects.

The repository and its existing open-source license remain public. An external
party may inspect, fork, build, or use the source under that license, but the
project currently provides no public:

- service-level agreement;
- support commitment;
- compatibility guarantee;
- fixed public release cadence;
- general product-readiness promise.

Regular public binary releases are paused. A public release is an exception
that requires explicit management approval and the existing release evidence.
Internal artifacts, internal deployments, and internal release-candidate
validation may continue.

## 3. Route 2 Product Boundary

Route 2 concentrates Core investment on:

- deterministic and reproducible builds;
- Markdown and Notion content ingestion;
- canonical content, routing, and representation contracts;
- Scriban rendering and local filesystem themes;
- SEO, GEO, feed, sitemap, search, and machine-readable publishing outputs;
- publish, security, and build audit evidence;
- Native AOT distribution;
- safe, observable, and repeatable internal publishing operations.

New Core capabilities require a named internal consumer, a concrete business
case, an owner, and verifiable acceptance criteria. Feature breadth alone is
not an investment justification.

## 4. Route 3 Operating Boundary

Route 3 means:

- optimize for stability of existing internal sites before ecosystem growth;
- prefer fixes, contract truth, observability, and controlled simplification
  over new general-purpose features;
- avoid expanding the public CLR surface without a proven internal need;
- avoid promising a theme marketplace, plugin marketplace, broad direct
  knowledge-source catalogue, hosted SaaS, or public support programme;
- retain a stop-loss decision point: if a capability lacks internal adoption,
  it may be frozen or removed through the applicable versioned governance
  process.

“Stable” in active documentation means a governed internal Core contract. It
does not imply commercial support or a public compatibility SLA.

## 5. Core, Labs, and Plugin Status

The internal-first declaration applies to Bukit Core.

Labs, Import, WeChat, and other external-plugin business implementations are
not promoted to Core and are not made release-ready by this documentation
change. Their own audits and maturity decisions remain authoritative.

Active documentation must not use Core stability as evidence that one of those
surfaces is supported or ready for public release.

## 6. Documentation Architecture

### 6.1 Canonical governance source

Create:

- `docs/governance/bukit-core-product-positioning.md`

This file is the canonical current statement for product direction, operating
mode, external-use boundary, release policy, and scope.

### 6.2 Entry-point summaries

Synchronize concise summaries and a link to the canonical source in:

- `README.md`;
- `README.zh-CN.md`;
- `README.ms.md`;
- `guide/README.md`;
- `guide/user/README.md`;
- `guide/dev/README.md`.

The three README translations must express the same normative meaning. They do
not need to be literal word-for-word translations.

### 6.3 Stability and release semantics

Update:

- `guide/dev/public-preview-scope.md`;
- `guide/dev/release.md`;
- `guide/dev/release-checklist.md`;
- `docs/release/release-prerelease-template.md`;
- `guide/dev/documentation-governance.md`;
- `CHANGELOG.md`.

Release instructions remain available because internal artifact production and
exceptionally approved public releases still require them. They must clearly
state that executing a release workflow does not itself authorize a public
release.

### 6.4 Historical evidence

Do not rewrite:

- `docs/analysis/`;
- existing `docs/superpowers/plans/` or historical specs;
- `guide/archive/`;
- `guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, or `scripts-0.2/`;
- historical version, test, artifact, and AOT evidence.

Those files describe decisions and facts at their original time. The new
governance statement supersedes their product-direction recommendations only
for current operations; it does not alter historical evidence.

## 7. Required Wording Properties

Every active entry point must make the following facts unambiguous:

1. Route 2 is the product direction.
2. Route 3 is the current operating mode.
3. Enterprise internal use has priority.
4. The repository and license remain public.
5. External use is self-directed and has no support, SLA, compatibility, or
   release-cadence promise.
6. Regular public binary releases are paused.
7. An exceptional public release needs explicit management approval.
8. Labs and external plugins are not covered by Core release readiness.

The wording must not claim that:

- external use is prohibited by policy when the license permits it;
- the repository is private;
- all development or artifact generation is frozen;
- existing Core behavior is unstable;
- a public release can never occur;
- plugin audit findings are closed by this policy decision.

## 8. Compatibility and Runtime Impact

This task changes documentation only. It must not modify:

- runtime behavior;
- CLI commands or help output;
- configuration schemas;
- public CLR APIs;
- plugin protocols;
- persistent formats;
- release workflow logic;
- Labs or plugin implementation code.

The already approved product version change to `2.0.0` remains a separate
working-tree change and must not be silently absorbed into the documentation
positioning commit.

## 9. Verification

Implementation acceptance requires:

- all intended active files contain consistent positioning;
- all three README translations agree on the eight required facts;
- the canonical governance link resolves from each entry point;
- historical evidence remains unchanged;
- protected reference trees remain unchanged;
- active documentation links and public absolute-path scans pass;
- documentation consistency and public documentation contracts pass;
- release documentation preserves technical validation instructions while
  adding the authorization boundary;
- `git diff --check` passes;
- capture the exact implementation base with `git rev-parse HEAD` before the
  first documentation edit, then run one aggregate
  `post-change-targeted.sh --base "$implementation_base" -- "${changed_paths[@]}"`
  after all documentation edits, as required by repository governance.

## 10. Success Criterion

A reader entering through any README, the Core guide, the user guide, the
developer guide, or the release documentation can distinguish:

- what Bukit Core is technically designed to do;
- who it currently prioritizes;
- what “stable” means;
- what external users may expect;
- whether a public release is authorized;
- whether Labs or a plugin is covered.

No reader should need a historical audit report to discover the current
internal-first product policy.
