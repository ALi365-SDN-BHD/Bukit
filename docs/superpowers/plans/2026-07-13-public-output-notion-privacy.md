# Public Output Notion Privacy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep Notion UUIDs and provider-source metadata inside Bukit's canonical/audit state while preventing them from entering public projections or the GitHub Pages deployment bundle.

**Architecture:** Introduce one Engine-owned public projection policy that derives stable public IDs from canonical URL keys with a route fallback and strips Notion-shaped entity/relation identifiers. Apply it at every built-in public writer, retain internal IDs in `.bukit` audit reports, extend the existing security report with an exact known-identifier public-output check, and make GitHub Pages staging exclude internal artifacts and reject residual leaks.

**Tech Stack:** C#/.NET, `System.Text.Json`, xUnit, existing Bukit projection/security/deploy infrastructure.

**Status (2026-07-13):** Implemented. The checklists below preserve the TDD execution design; current verification evidence is reported in the task handoff.

## Global Constraints

- Do not modify `guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, or `scripts-0.2/`.
- Preserve canonical records, Notion caches, and `.bukit` audit report identity fields.
- Do not delete every field named `source`; path-valued fields such as `.bukit/assets.json` remain unchanged.
- Use the existing `build.report.securityFailMode` behavior for build-time enforcement and unconditional validation for an actual GitHub Pages deploy.
- Run `bash scripts/checks/post-change-targeted.sh -- <changed paths>` after each code subtask; do not run full/release gates.

---

### Task 1: Publish-safe projection contracts

**Files:**
- Create: `src/Bukit-Core/Bukit.Engine/PublicContentProjectionPolicy.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/ContentProjectionWriter.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/SearchIndexBuilder.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/I18nOutputMerger.cs`
- Test: `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`
- Test: `tests/Bukit.Engine.Tests/SearchIndexBuilderTests.cs`

**Interfaces:**
- Produces: `PublicContentProjectionPolicy.ResolvePublicId(ContentRecord record, string routeUrl)`.
- Produces: publish-safe entity/relation projection helpers that remove UUID-shaped Notion identifiers without mutating canonical records.
- Public JSON keeps `id`/canonical identity fields but uses the public ID; provider `source`/`sourceKey` fields are absent.

- [ ] Add failing projection and search tests using an ID such as `posts:39bfa39a-5013-81ae-9516-fbd448f3bd47`; assert public IDs use the canonical key, `source` and `sourceKey` are absent, and the canonical record remains unchanged.
- [ ] Run the focused tests and confirm failures show the current internal-ID/source serialization.
- [ ] Implement the policy and apply it to content JSON, content Markdown, agent manifest, single/merged search, and i18n root manifest generation.
- [ ] Sanitize UUID-shaped Notion entity IDs and relation target IDs in the content JSON projection while retaining safe names, URLs, and relation targets.
- [ ] Update publish-representation audit rules to validate the new public contract rather than requiring provider source metadata.
- [ ] Run the focused tests, then the targeted post-change gate for the changed Engine/test paths.

### Task 2: Remaining built-in public text and feed outputs

**Files:**
- Modify: `src/Bukit-Core/Bukit.Engine/JsonFeedGenerator.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs`
- Modify: `src/Bukit-Core/Bukit.Content/Notion/BlockRenderers/LinkToPageBlockRenderer.cs`
- Test: `tests/Bukit.Engine.Tests/RssGeneratorTests.cs`
- Test: `tests/Bukit.Engine.Tests/LlmsTxtPluginTests.cs`
- Test: `tests/Bukit.Content.Tests/NotionBlockRenderersTests.cs`
- Test: `tests/Bukit.Content.Tests/NotionBlockRendererEdgeCasesTests.cs`

**Interfaces:**
- JSON Feed `_bukit` retains public entities/review state but never provider `source`.
- `llms-full.txt` omits provider source.
- Notion `link_to_page` renders a neutral linked-page label without exposing page/database IDs.

- [ ] Change the existing tests to the publish-safe expectations and run them to observe the expected failures.
- [ ] Remove JSON Feed provider source emission and its `HasBukitExtension` dependency.
- [ ] Remove `Source: ...` from `llms-full.txt`.
- [ ] Replace raw link-to-page ID attributes/text with neutral markup; keep unknown/empty block behavior unchanged.
- [ ] Run the focused tests, then the targeted post-change gate for Engine/Content/test paths.

### Task 3: Internal report isolation and release privacy gates

**Files:**
- Create: `src/Bukit-Core/Bukit.Engine/PublicOutputPrivacyCheck.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/BuildReporterSecurity.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/BuildReporter.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Deploy/DeploymentPrivacyValidator.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Deploy/GitHubPagesDeployProvider.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Deploy/GitHubPagesDeployProvider.Validation.cs`
- Test: `tests/Bukit.Engine.Tests/BuildReporterTests.cs`
- Test: `tests/Bukit.Cli.Tests/GitHubPagesDeployProviderTests.cs`

**Interfaces:**
- `PublicOutputPrivacyCheck.Evaluate(...)` derives exact sensitive tokens from Notion configuration and canonical records, scans public text files and relative paths, and excludes `.bukit` plus build marker/state files.
- `DeploymentPrivacyValidator.Validate(outputDir, stagedDir)` reads internal audit identity data, validates staged public files, and returns actionable leak paths without echoing sensitive identifiers.
- Deployment fails closed unless `.bukit/publish-audit-report.json` matches the current schema and contains a `documents` array.
- `CopyDirectory` excludes `.git`, `.bukit`, `.bukit-build-state.json`, and `.bukit-output-marker` while preserving normal dotfiles.

- [ ] Add failing Engine tests for exact hyphenated/compact Notion ID detection, provider-source marker detection, safe business UUID handling, and `.bukit` exclusion.
- [ ] Implement the `publicOutputPrivacy` security-report check and include it in status/enforcement calculation.
- [ ] Run Engine tests and the targeted gate.
- [ ] Add failing CLI tests proving internal artifacts are not staged and residual known Notion IDs/provider fields reject deployment.
- [ ] Implement deploy staging exclusions and validator invocation before git staging.
- [ ] Run CLI tests and the targeted gate.
- [ ] Because this changes gate/deploy behavior, request an immediate bounded read-only audit and resolve every critical/important finding.

### Task 4: Compatibility, documentation, and end-to-end proof

**Files:**
- Modify: `src/Bukit-Plugins/Bukit.WechatSyncing/WechatSyncInputLoader.cs` only if the public-ID/source contract requires loader fallback changes.
- Modify: `tests/Bukit.Plugin.WechatSync.Tests/WechatSyncInputLoaderTests.cs`
- Modify: `tests/Bukit.Plugin.WechatSync.Tests/WechatSyncPluginInvokeCompatibilityTests.cs`
- Modify: `guide/user/10-built-in-outputs.md`
- Modify: `guide/user/13-deploy-github-pages.md`

**Interfaces:**
- WeChat sync uses the public content ID/canonical key and collection fallback; it does not require provider source in public files.
- Active documentation defines `.bukit` as internal diagnostics excluded by built-in deployment.

- [ ] Add/update compatibility tests with public projection fixtures that contain no provider source or internal UUID; run them to identify any loader dependency.
- [ ] Make the minimum loader change needed for deterministic public sync identity and rerun focused tests.
- [ ] Update active guides with the public/internal output boundary and manual-hosting warning.
- [ ] Build a Notion-shaped integration fixture and assert all public files/paths exclude its exact UUID and provider-source markers while `.bukit` reports retain internal identity.
- [ ] Run targeted gates for all changed paths.
- [ ] Request one consolidated read-only audit over the complete diff, addressing cross-subtask regressions and unrelated changes.
- [ ] Run fresh final focused tests, targeted gate, `git diff --check`, and requirement-by-requirement leak searches before claiming completion.
