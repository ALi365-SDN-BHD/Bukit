# Bukit 1.0 Trust Hardening Plan

## Summary

Bukit 1.0 will treat `BukitJalil` as out of scope, and bring the rest of Bukit onto a stable public contract. The release strategy is: allow one final cleanup pass before 1.0, then freeze the core contracts under strict SemVer. Core surfaces must hit the full GA bar first; ecosystem surfaces such as theme registry, AI intent, clone/import, and the external plugin ecosystem may remain shipped, but only with explicit support tiers and no ambiguity about what is or is not covered by the 1.0 compatibility promise.

Current repo facts to build from:
- The codebase already has strong foundations: stable diagnostic codes, route safety, output safety, remote theme lock files, plugin capability gating, build reports, security fixtures, smoke scripts, and architecture/governance docs.
- `dotnet test bukit.slnx -c Release --no-restore` currently passes.
- `bash scripts/smoke.sh Release` currently fails on the starter/example contract, mainly due to repo assets and doctor/governance rules drifting out of sync rather than a hard engine crash.
- Running `dotnet` workflows in parallel exposed build artifact contention, which should be treated as a reproducibility/stability issue for 1.0 CI orchestration.

## Implementation Changes

### 1. Freeze the 1.0 compatibility surface
- Publish a single 1.0 compatibility matrix covering: `site.yaml` schema, unified `ContentItem` model and reserved meta fields, routing precedence/rule resolution, theme manifest and template capability contracts, plugin lifecycle/protocol contracts, build report and audit artifact schemas, diagnostic code/exit code behavior.
- Split every public surface into one of three support tiers:
  - `GA-locked`: core build/config/content/routing/theme/plugin/runtime contracts
  - `GA-limited`: shipped but narrower promise, with explicit documented constraints
  - `Experimental`: not part of the 1.0 promise
- Do one deliberate pre-1.0 cleanup pass to remove ambiguous or conflicting behaviors, then prohibit further breaking changes on `GA-locked` surfaces except through a future major version.

### 2. Align implementation, examples, docs, and doctor rules
- Treat “repo ships contradictory truth” as a release blocker.
- Fix starter/example/theme assets so that smoke, doctor, and template governance all agree on the same expected contract.
- Make the compatibility docs the source of truth, then update:
  - README/public-preview messaging
  - maintainer docs
  - skill docs under `src/skills/`
  - example sites and starter scaffolds
  - doctor/docs-check/template-sync expectations
- Add a release rule that no example, starter theme, or skill doc may rely on behavior not covered by the declared support tier.

### 3. Version and lock public extension contracts
- `site.yaml`: define a frozen 1.0 field contract with explicit deprecation policy, field lifecycle states, and migration notes for anything touched in the cleanup pass.
- Content model: freeze reserved meta keys, field normalization behavior, schema validation semantics, and provider parity rules across Markdown/Notion/composite sources.
- Routing: freeze precedence order, list/taxonomy/pagination derivation behavior, output path encoding, and conflict policy semantics.
- Themes: define the supported 1.0 contract for `theme.yaml`, `extends`, template capability manifests, required templates, theme source locking, and inheritance behavior.
- Plugins: define the supported 1.0 contract for built-in plugins, source-generated plugins, external protocol plugins, handshake/version negotiation, capability enforcement, env isolation, output manifest tracking, and failure modes.
- Audit/report artifacts: version JSON schemas for `build-report.json`, `routes.json`, `assets.json`, `incremental-manifest.json`, `security-report.json`, `seo-report.json`, and `geo-report.json` so they are safe for CI and downstream tooling.

### 4. Make builds reproducible, auditable, and rollbackable
- Define a deterministic build baseline for the same input tree:
  - stable output file set
  - stable route inventory
  - stable audit/report schemas
  - stable hashing/manifest behavior
- Add CI checks for:
  - clean build vs incremental build equivalence on output inventory
  - repeated build stability
  - serialized orchestration of commands that currently contend on shared build outputs
  - remote theme lock enforcement
  - plugin stale output cleanup correctness
- Promote `.bukit/` artifacts into the official rollback/audit story:
  - artifact manifest
  - route inventory
  - asset hash inventory
  - security report
  - version/build metadata
- Define a release artifact bundle format so every public release can be inspected, compared, and rolled back with evidence.

### 5. Raise the error and security bar to 1.0 quality
- Complete diagnostic coverage so all user-facing failure classes on GA-locked surfaces produce stable error codes, actionable messages, and a precise location or config path.
- Standardize message shape across config/content/render/plugin/build failures, including remediation hints.
- Expand hardening where trust matters most:
  - config path resolution
  - route/output path validation
  - theme inheritance/name/path boundaries
  - plugin entry/capability/env/output constraints
  - remote fetch/SSRF boundaries
  - sensitive file leakage and unsafe URL output
- Make security regressions first-class release gates, not best-effort tests.

### 6. Reframe the test system around public contracts
- Keep the current unit/integration breadth, but add contract-focused suites:
  - config contract snapshots
  - content normalization parity tests
  - route inventory golden tests
  - theme manifest compatibility tests
  - plugin protocol compatibility tests across schema negotiation versions
  - output artifact schema validation tests
- Convert current fixture/example coverage into explicit release gates:
  - starter site must pass doctor/build/smoke without manual fixes
  - fixtures for output safety, route security, plugin policy, dotfile leaks, i18n, taxonomy, incremental builds must all remain green
  - repeated runs must detect flakes
- Add a compatibility lane that runs old supported inputs against the 1.0 engine and verifies either unchanged behavior or a documented deprecation/migration path.

## Public APIs / Contracts To Version Explicitly

- `site.yaml` public schema and override precedence
- `ContentItem`-level public behavior: reserved meta keys, field normalization, schema validation outcomes
- routing contract: precedence, derived route semantics, output encoding, conflict handling
- theme contract: `theme.yaml`, `bukit.templates.yaml`, required templates, `extends`, `min_engine_version`, source lock behavior
- plugin contract: hook names, protocol request/response schema, handshake negotiation, capability names, env policy, output limits, failure semantics
- build/audit artifact JSON schemas under `dist/.bukit/`
- diagnostic code ranges and CLI exit code mapping

## Test Plan

- `dotnet test bukit.slnx -c Release --no-restore` remains required and green.
- `bash scripts/smoke.sh Release`, `bash scripts/smoke-all.sh Release`, and `bash scripts/security-regression.sh Release` become hard release gates.
- Add a deterministic-build job:
  - clean build twice, compare route/assets/report inventories
  - clean build vs incremental build, compare public outputs
  - repeat selected smoke scenarios to catch flakes
- Add a compatibility job:
  - previous supported examples/fixtures/themes/plugins run against current engine
  - validate JSON artifact schemas and diagnostic code presence
- Add orchestration tests or CI rules to prevent parallel command layouts that contend on shared `dotnet` outputs unless isolated.

## Assumptions and Defaults

- `BukitJalil` is excluded from Bukit 1.0.
- A single pre-1.0 cleanup window is allowed; after that, core contracts freeze.
- Core surfaces must meet full GA quality before release.
- Non-core but still shipped surfaces may stay available, but must carry explicit support tiers instead of vague “preview” wording.
- The first execution milestone should focus on fixing contract drift in starter/examples/docs/tests before adding new features.
