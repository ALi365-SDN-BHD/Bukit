# Bukit Docs And Scripts Deep Audit - 2026-07-09

## Scope

This audit covers the current working tree documentation and script surfaces:

- Root docs: `README*`, `CONTRIBUTING*`, `SECURITY*`, `AGENTS.md`.
- Current guide/script candidates: `guide/`, `scripts/`.
- Backup/reference trees: `guide-0.1/`, `scripts-0.1/`, `guide-0.2/`, `scripts-0.2/`.
- CI and release workflow files under `.github/`.
- Governance and analysis docs under `docs/`, especially docs that claim current source-of-truth status.

Per `AGENTS.md`, `guide-0.1/`, `scripts-0.1/`, `guide-0.2/`, and `scripts-0.2/` are backup/reference inputs only. They were read for comparison and must not be used as official documentation or active script sources.

## Verification Run

Commands run during the audit:

| Command | Result |
|---|---|
| `bash scripts/gates/ci-fast.sh Release` | Passed |
| `bash scripts/gates/ci-full.sh Release` | Passed |
| Active-doc path scanner over `README*`, `CONTRIBUTING*`, `SECURITY*`, `.github/PULL_REQUEST_TEMPLATE.md`, and `guide/**/*.md` | 24 missing/path-placeholder hits; real active-doc failures are in `CONTRIBUTING*` and `SECURITY*` |
| Active `guide/` + `scripts/` line-count scan | No active file above 600 lines; largest active guide/script file is `guide/user/04-site-yaml-config.md` at 305 lines |

`ci-full` passed 3281 Core tests locally through the current working-tree `scripts/` scripts. This proves local script executability, not CI activation, because the current `scripts/` tree is not tracked.

## Executive Findings

### P0 - Mainline guide/scripts are untracked

`AGENTS.md` says future documentation and script work should prefer `guide/` and `scripts/` as mainline paths (`AGENTS.md:16-19`). Root README files also link users to `guide/user`, `guide/dev`, `guide/skills`, `guide/labs`, and `guide/archive` (`README.md:163-174`).

Current Git state disagrees with that contract:

- `git ls-files guide scripts | wc -l` returns `0`.
- `git status --short --untracked-files=all -- guide scripts .github/workflows` lists all `guide/**` and `scripts/**` files as untracked.
- `git ls-files guide-0.2 scripts-0.2 | wc -l` returns `196`, so the tracked tree is still `*-0.2`, not the documented mainline tree.

Impact: a clean checkout from the tracked branch would not contain the documented `guide/` and `scripts/` mainline, so README links, local gates, and release packaging assumptions can break outside this working tree.

### P0 - GitHub workflows are inactive and not closed over current scripts

`.github/workflows/` exists locally but is empty and has no tracked files. The tracked workflow YAML files are under `.github/workflows-0.1/`, which GitHub Actions will not treat as active workflows.

Evidence:

- `git ls-files .github/workflows` returns no files.
- `git ls-files .github/workflows-0.1` returns `.github/workflows-0.1/ci.yml` and `.github/workflows-0.1/release.yml`.
- `.github/workflows-0.1/ci.yml:57` calls `scripts/checks/ci-workflow-evidence.sh`, but current `scripts/` does not contain that file.
- `.github/workflows-0.1/ci.yml:134` calls `scripts/stress-test.sh`, but current `scripts/` does not contain that file.
- `.github/workflows-0.1/release.yml:178` calls `scripts/checks/release-assets.sh`, but current `scripts/` does not contain that file.

Impact: CI/release is both inactive by directory placement and stale against the current script tree if moved back without repair.

### P1 - CONTRIBUTING docs are stale and broken

The three contribution docs still describe an older script and developer-doc layout.

Examples:

- `CONTRIBUTING.md:25`, `CONTRIBUTING.zh-CN.md:25`, and `CONTRIBUTING.ms.md:25` call `scripts/smoke.ps1`, which does not exist in current `scripts/`.
- `CONTRIBUTING.md:28`, `CONTRIBUTING.zh-CN.md:28`, and `CONTRIBUTING.ms.md:28` link `guide/dev/new-developer-30min.md`, which does not exist in current `guide/dev`.
- `CONTRIBUTING.md:44`, `CONTRIBUTING.zh-CN.md:44` link `guide/dev/maintainer-entrypoints.md`, which does not exist in current `guide/dev`.
- `CONTRIBUTING.md:48-49`, `CONTRIBUTING.zh-CN.md:48-49`, and `CONTRIBUTING.ms.md:42-43` link `guide/dev/code-wiki.md` and `guide/dev/governance-checklist.md`, neither of which exists in current `guide/dev`.
- `CONTRIBUTING.md:55` and `CONTRIBUTING.zh-CN.md:55` link `guide/dev/testing-smoke.md`; current docs use `guide/dev/testing.md`.
- `CONTRIBUTING.md:62` and `CONTRIBUTING.zh-CN.md:62` tell contributors to run `scripts/check-aot-warnings.sh`, which is absent from current `scripts/`.
- `CONTRIBUTING.zh-CN.md:67` tells contributors to run `scripts/check-doc-asset-consistency.ps1`, also absent.

Impact: contributor onboarding sends users to missing files and obsolete scripts. This is not caught by `ci-fast`, because the current docs gate does not scan `CONTRIBUTING*`.

### P1 - SECURITY docs conflict with Core/Labs and token contracts

`SECURITY*` currently describes Labs-only or wrong-contract surfaces as if they were active Core security guidance.

Evidence:

- `SECURITY.md:19-26`, `SECURITY.zh-CN.md:19-26`, and `SECURITY.ms.md:19-26` document `bukit webhook` and link `guide/dev/webhook.md`.
- Current static Core commands are listed in `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs:210`; `webhook` is not included.
- `guide/labs/webhook.md:3-5` explicitly says webhook services are outside the stable Core documentation.
- `SECURITY.md:33`, `SECURITY.zh-CN.md:33`, and `SECURITY.ms.md:33` tell users to export `BUKIT_NOTION_TOKEN`.
- Current code uses `NOTION_TOKEN`: `src/Bukit-Core/Bukit.Shared/EnvironmentHelper.cs:5-12`, and Notion provider failure text requires `NOTION_TOKEN` in `src/Bukit-Core/Bukit.Engine/ContentProviderFactory.cs:124-127`.
- `SECURITY.md:40`, `SECURITY.zh-CN.md:40`, and `SECURITY.ms.md:40` say external plugins run as separate processes or WASM modules. Current plugin docs describe external process protocol only (`guide/dev/plugins.md:5-7`), and the v1 plugin spec excludes WASM (`docs/plugins/Bukit 插件协议 v1 规范.md:93-115`, `docs/plugins/Bukit 插件配置规范.md:937-952`).

Impact: security guidance names a non-working Notion env var and presents Labs/Webhook/WASM boundaries as current Core security surfaces.

### P1 - Gate names no longer match claimed behavior

Current gate scripts are intentionally thin:

- `scripts/quality-gate.sh:1-4` only calls `scripts/gates/ci-fast.sh`.
- `scripts/gates/ci-fast.sh:7-13` runs docs/config/CLI/skills/README/Core CLI contract checks only.
- `scripts/gates/release.sh:4-5` runs `ci-fast` and prints that release artifact validation must be invoked explicitly.
- `guide/dev/release.md:16-24` and `guide/dev/testing.md:15-34` correctly describe the thin-gate model.

But root contributor surfaces still describe old/full behavior:

- `CONTRIBUTING.md:67` and `CONTRIBUTING.zh-CN.md:68` say `scripts/quality-gate.sh` verifies build + test + coverage + format + smoke.
- `.github/PULL_REQUEST_TEMPLATE.md:32-35` requires local `quality-gate`, coverage >= 80%, `dotnet format`, and single-file <= 600 lines, and says file-size is automatically checked by quality-gate.

Current `scripts/checks/` no longer contains `coverage.sh`, `file-size.sh`, `repo-hygiene.sh`, or `ci-workflow-action-pin.sh`; those exist only under `scripts-0.2/checks/`.

Impact: reviewers and contributors can believe a local `quality-gate` run proves much more than it actually proves.

### P1 - Governance check coverage excludes drifting root docs and workflows

The current docs gate is focused but too narrow for the public documentation surface:

- `scripts/checks/docs-consistency.sh:7-9` only composes required guide paths, Core command boundary checks, and local artifact scans.
- `scripts/checks/docs/no-core-drift.sh` scans only `README.md`, `README.zh-CN.md`, `README.ms.md`, `guide/user`, `guide/dev`, and `guide/skills`.
- `scripts/checks/readme-sync.sh` checks README entry points only.
- `guide/dev/documentation-governance.md:13-22` lists `guide/user`, `guide/dev`, `guide/skills`, README links, and focused scripts, but not `CONTRIBUTING*`, `SECURITY*`, `.github/PULL_REQUEST_TEMPLATE.md`, or workflows.

Impact: the active broken links and stale command guidance in `CONTRIBUTING*` and `SECURITY*` are invisible to the current fast gate.

### P2 - `guide-0.2/` and `scripts-0.2/` must stay backup/reference only

`AGENTS.md` now defines `guide-0.2/` and `scripts-0.2/` as backup/reference areas alongside `guide-0.1/` and `scripts-0.1/`. Mainline changes, official docs, CI gates, release scripts, and runtime behavior references must target `guide/` and `scripts/`, not any `*-0.x` tree.

Residual risk remains because the tracked tree still makes `*-0.2` look current:

- `guide/README.md:3-6` says `guide-0.2` is only an information architecture reference.
- `guide-0.2/` and `scripts-0.2/` are the tracked trees.
- Current file counts are `guide/` 88 files vs `guide-0.2/` 136 files, and `scripts/` 30 files vs `scripts-0.2/` 54 files.
- `diff -qr guide-0.2 guide` and `diff -qr scripts-0.2 scripts` show broad content and script-set differences, not a pure rename.

Impact: maintainers now have a clear rule, but CI, docs, and release wiring must still avoid executing or citing `*-0.2` as an official source.

### P2 - Governance docs still contain stale absolute source links

`docs/compatibility-governance.md` and `docs/compatibility-governance.zh-CN.md` still present old source paths as current code locations:

- `docs/compatibility-governance.md:34-53` links to `/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/...`, `/src/Bukit.Engine/...`, `/src/Bukit.Cli/...`, etc.
- `docs/compatibility-governance.zh-CN.md:32-51` has the same pattern.
- Current source lives under `src/Bukit-Core/...`; for example, `src/Bukit-Core/Bukit.Config/ConfigLoader.cs` exists, while `src/Bukit.Config/ConfigLoader.cs` does not.

`docs/bukit-1.0-security-boundary-audit.md:12-15` and `:26-60` also list old `src/Bukit.*` paths.

Impact: documents that still look like governance/security references point maintainers to dead paths unless readers already know they are historical.

### P2 - Local artifact scan is too narrow

Untracked `.DS_Store` files exist in the working tree, including root, `src/`, `tests/`, `examples/`, and `guide-0.2/ai/**`. They are not tracked, but current local artifact scanning only covers `guide` and `scripts`:

- `scripts/checks/docs/no-local-artifacts.sh:4` runs `find guide scripts ...`.

Impact: fast gate can report "guide/scripts local artifact scan OK" while the repo still contains local artifact noise outside those two directories.

### P2 - Oversize policy is unclear outside active guide/scripts

Active `guide/` and `scripts/` are not oversized by the old 600-line standard. However, many tracked docs under `docs/` are large:

- `docs/analysis/bukit-core-plugin-system-full-audit-2026-06-29.md`: 2231 lines.
- `docs/demo_to_bukit_workflow.md`: 1931 lines.
- Several `docs/plugins/*` files are above 1500 lines.

This may be acceptable for historical reports and specifications, but `.github/PULL_REQUEST_TEMPLATE.md:35` still says single-file <= 600 lines is automatically checked by quality-gate. If the 600-line rule should not apply to long reports/specs, the rule needs documented exceptions; if it should apply, there is no current gate enforcing it.

## Areas That Are Currently Healthy

- Current `README*`, `guide/user`, `guide/dev`, and `guide/skills` align with the static Core CLI registry: `scripts/checks/cli-docs-sync.sh` passed.
- Current config docs align with the `site.yaml` field contract: `scripts/checks/config-docs-contract.sh` passed.
- Current Core skills index and strict validation passed.
- Current `guide/` correctly treats Notion as a content provider through `bukit-content`; the missing standalone `bukit-notion` skill in `guide/skills` is not a defect by itself.
- Current `guide/` does not present Labs clone/import/intent/webhook/theme registry workflows as stable Core behavior; the drift is in root `SECURITY*` and older/historical docs.

## Recommended Repair Order

1. Close the tree migration first: track `guide/` and `scripts/` as the true mainline, and keep `guide-0.2/` + `scripts-0.2/` as backup/reference only. Do not start by polishing individual docs while the mainline tree is untracked.
2. Reactivate or explicitly retire workflows. If reactivating, move/restore YAML under `.github/workflows/` and make every referenced script exist in current `scripts/`.
3. Fix root active docs: `CONTRIBUTING*`, `SECURITY*`, and `.github/PULL_REQUEST_TEMPLATE.md`.
4. Update docs/script governance so fast checks include root active docs and workflow script references, not only README + `guide`.
5. Decide whether ideas from old checks in `scripts-0.2/checks/` should be ported into current `scripts/`: coverage, file-size, repo-hygiene, action-pin, workflow evidence, release-assets. Do not execute `scripts-0.2` directly as an official gate.
6. Mark historical/stale `docs/` reports explicitly, or refresh governance docs that still claim current status with old `src/Bukit.*` paths.
7. Re-run `bash scripts/gates/ci-fast.sh Release` and `bash scripts/gates/ci-full.sh Release`; run release artifact validation only if release scripts/workflows are changed.
