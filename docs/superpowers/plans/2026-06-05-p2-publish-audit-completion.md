# P2 Publish Audit Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete P2 by making publish audit the primary machine-readability and trust audit surface while keeping SEO/GEO outputs compatible.

**Architecture:** Introduce first-class publish audit report models and writer, keep SEO report generation as a compatibility view, and move publish rules into focused pure rule classes. `bukit publish audit/diff` becomes an independent CLI entrypoint that reads `.bukit/publish-audit-report.json` by default.

**Tech Stack:** .NET, xUnit, System.Text.Json source generation, Bukit Engine, Bukit CLI

---

### Task 1: Spec And Tracking

**Files:**
- Create: `.trae/specs/p2-publish-audit-completion/spec.md`
- Create: `.trae/specs/p2-publish-audit-completion/tasks.md`
- Create: `.trae/specs/p2-publish-audit-completion/checklist.md`

- [ ] **Step 1: Create spec files**
- [ ] **Step 2: Record that P3 projections are out of scope**
- [ ] **Step 3: Track TDD, docs, quality gate, and file-size requirements**

### Task 2: Publish Audit Report Contract

**Files:**
- Create: `src/Bukit.Engine/PublishAuditModels.cs`
- Create: `src/Bukit.Engine/PublishAuditReportWriter.cs`
- Test: `tests/Bukit.Engine.Tests/PublishAuditReportWriterTests.cs`

- [ ] **Step 1: Write failing tests for a distinct publish report JSON shape**
- [ ] **Step 2: Run targeted engine tests and confirm red**
- [ ] **Step 3: Add publish audit models and writer**
- [ ] **Step 4: Update SEO writer to call the publish writer**
- [ ] **Step 5: Run targeted engine tests and confirm green**

### Task 3: Publish Audit Rules

**Files:**
- Create: `src/Bukit.Engine/PublishAuditRules/SemanticHtmlAuditRules.cs`
- Create: `src/Bukit.Engine/PublishAuditRules/TrustAuditRules.cs`
- Create: `src/Bukit.Engine/PublishAuditRules/RepresentationAuditRules.cs`
- Create: `src/Bukit.Engine/PublishAuditRules/SeoCompatibilityAuditRules.cs`
- Modify: `src/Bukit.Engine/SeoAuditReportWriter.Helpers.cs`

- [ ] **Step 1: Add failing tests for new publish gap rules**
- [ ] **Step 2: Move existing rule logic into focused pure classes**
- [ ] **Step 3: Add JSON-LD/content mismatch, output presence, and AI crawler conflict rules**
- [ ] **Step 4: Run targeted engine tests**

### Task 4: Publish CLI Independence

**Files:**
- Modify: `src/Bukit.Cli/Commands/PublishCommand.cs`
- Modify: `src/Bukit.Cli/Commands/SeoCommand.cs`
- Test: `tests/Bukit.Cli.Tests/PublishCommandTests.cs`

- [ ] **Step 1: Add failing publish CLI tests**
- [ ] **Step 2: Implement publish-specific report resolution**
- [ ] **Step 3: Keep SEO explicit-report compatibility**
- [ ] **Step 4: Run targeted CLI tests**

### Task 5: Schema And Documentation

**Files:**
- Create: `docs/schemas/publish-audit-report.v1.schema.json`
- Create: `docs/publish-audit-report-schema.md`
- Modify: `guide/user/12-cli-reference.md`
- Modify: `guide/user/12-cli-reference.zh-CN.md`
- Modify: `guide/user/12-cli-reference.ms.md`
- Modify: `guide/user/11-i18n-seo.md`
- Modify: `guide/user/11-i18n-seo.zh-CN.md`
- Modify: `guide/user/11-i18n-seo.ms.md`
- Modify: `guide/dev/engine-outputs.md`
- Modify: `guide/dev/geo.md`
- Modify: `guide/dev/geo.zh-CN.md`
- Modify: `guide/dev/geo.ms.md`
- Modify: `src/skills/bukit-cli-reference/SKILL.md`
- Modify: `src/skills/bukit-seo/SKILL.md`
- Modify: `src/skills/bukit-geo/SKILL.md`

- [ ] **Step 1: Add publish audit JSON schema and schema docs**
- [ ] **Step 2: Update user CLI docs in three languages**
- [ ] **Step 3: Update developer output and GEO docs**
- [ ] **Step 4: Update Bukit skills to publish-first wording**

### Task 6: Verification

**Files:**
- Verify only

- [ ] **Step 1: Run targeted engine tests**
- [ ] **Step 2: Run targeted CLI tests**
- [ ] **Step 3: Run SiteEngine integration filter**
- [ ] **Step 4: Run final quality gate and format verification**
