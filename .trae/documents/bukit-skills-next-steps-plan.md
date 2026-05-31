# Bukit Skills Next Steps — Implementation Plan

## Overview

Four tasks identified from the quality report. All are read/write operations on existing files (no new skills needed).

---

## Task 1: Document Missing Notion/Config Fields

### 1.1 `site.analytics.disableInPreview`

**Source**: `AnalyticsConfig` in [AppConfig.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/AppConfig.cs#L84)
- Type: `bool`, Default: `true`
- Controls whether analytics scripts are suppressed during `bukit preview`

**Target file**: `src/skills/bukit-config/SKILL.md`
- Add `disableInPreview` to the Analytics Configuration field table (after `google_analytics_id`)
- Add a brief note: "Analytics are disabled during `bukit preview` by default. Set to `false` to enable analytics in preview mode."

### 1.2 `content.notion.filterValue`

**Source**: `NotionConfig.FilterValue` in [AppConfig.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/AppConfig.cs#L237)
- Type: `string?`, Default: `null`
- Required when `filterType` is `select_equals`, `status_equals`, or `rich_text_equals`

**Target file**: `src/skills/bukit-config/SKILL.md`
- Add `filterValue` to the `content.notion` field reference table (after `filterType`)
- Description: `Filter value (required for select_equals/status_equals/rich_text_equals)`

**Target file**: `src/skills/bukit-notion/SKILL.md`
- Add `filterValue` mention in the Notion configuration section

### 1.3 `content.notion.propertyMap`

**Source**: `NotionPropertyMapConfig` in [AppConfig.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/AppConfig.cs#L247-L261)
- 12 fields: Title, Slug, Type, PublishAt, Language, I18nKey, Summary, Collection, SeoTitle, SeoDescription, SeoImage, Canonical
- All `string?`, all default `null`
- Maps Notion property names to internal Bukit fields

**Target file**: `src/skills/bukit-config/SKILL.md`
- Add `propertyMap` to the `content.notion` field reference table
- Add a sub-table or description listing the 12 mappable fields with examples

**Target file**: `src/skills/bukit-notion/SKILL.md`
- Add `propertyMap` section explaining how to map custom Notion property names to Bukit internal fields

### Execution Plan for Task 1
```
1.1 Edit bukit-config/SKILL.md analytics section → add disableInPreview
1.2 Edit bukit-config/SKILL.md Notion fields → add filterValue + propertyMap
1.3 Edit bukit-notion/SKILL.md → add filterValue + propertyMap references
1.4 Run validate-skills-strict.sh to verify no regressions
```

---

## Task 2: Add Missing CLI Commands to Reference

Three commands exist in [BukitCliSpecs.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs) but are missing from `bukit-cli-reference/SKILL.md`.

### 2.1 `clone` command

- **Source**: [L78-L100](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs#L78-L100)
- **Description**: "从目标网站提取数据生成 Bukit 主题与内容" (Extract data from target website to generate Bukit themes and content)
- **17 options**: --tokens, --theme, --layout, --page, --sections, --behaviors, --icons, --assets, --brand, --use, --force, --verify, --visual-threshold, --fail-on-visual-diff, --fidelity, --config, --site
- **Status label**: `beta` (website cloning is a newer feature, depends on Browser MCP)

### 2.2 `docs check` command

- **Source**: [L559-L575](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs#L559-L575)
- **Parent**: `docs`, **child**: `check`
- **Description**: "检查 README/guide/skills 之间的一致性" (Check consistency between README/guide/skills)
- **5 flags**: --cli, --config-fields, --file-refs, --examples, --skills
- **Status label**: `beta` (documentation checking tool, internal use)

### 2.3 `route inspect` command

- **Source**: [L521-L541](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Cli/BukitCliSpecs.cs#L521-L541)
- **Parent**: `route`, **child**: `inspect`
- **Description**: "列出所有路由" (List all routes)
- **2 options** (on inspect): --json, --collection
- **Parent options**: --config, --site, --json, --collection
- **Status label**: `stable` (route inspection is a core diagnostic feature)

### Execution Plan for Task 2
```
2.1 Add `clone` to Quick Reference table in bukit-cli-reference/SKILL.md
    - Tag as (beta) in the description
2.2 Add `docs check` to Quick Reference table
    - Tag as (beta) — internal documentation tool
2.3 Add `route inspect` to Quick Reference table
    - Tag as (stable)
2.4 Add detailed sections for each command (similar to existing build/dev/preview sections)
2.5 Add `clone` to using-bukit key commands quick reference (already partially listed)
2.6 Run validate-skills-strict.sh
```

---

## Task 3: Run `dotnet test`

Verify no test regressions from the skills documentation changes.

### Execution
```bash
dotnet test bukit.slnx --no-restore 2>&1
```

Skills changes are documentation-only (no source code changes), so test failures are unlikely. But this is required as a verification step.

---

## Task 4: Add Skills Validation to CI Pipeline

### Current CI Structure
- `.github/workflows/ci.yml` → `quality-gate` job → `scripts/quality-gate.sh`
- `scripts/quality-gate.sh` has 9 sequential checks (file-size, encoding, build, test+coverage, format, doc-assets, smoke, coverage-threshold, smoke-all)

### Proposed Change
Add a new check after the "Encoding check" step in `scripts/quality-gate.sh`:

```bash
# Skills validation
echo "=== Skills validation ==="
bash src/skills/scripts/validate-skills-strict.sh || { echo "ERROR: Skills validation failed"; exit 1; }
```

### Execution Plan for Task 4
```
4.1 Check if quality-gate.sh already runs from repo root
4.2 Add skills validation step after encoding check
4.3 Verify the step fails gracefully (exit code propagation)
4.4 Run quality-gate.sh locally to verify the new step works
```

---

## Execution Order

```
Task 1 (document fields)  →  Task 2 (CLI commands)  →  Task 3 (dotnet test)  →  Task 4 (CI pipeline)
```

Tasks 1 and 2 are independent and can be done in parallel. Task 3 depends on 1+2 being complete. Task 4 is last.

## Verification

After all tasks complete:
```bash
# 1. Validate skills
bash src/skills/scripts/validate-skills.sh
bash src/skills/scripts/validate-skills-strict.sh

# 2. Run tests
dotnet test bukit.slnx --no-restore

# 3. Run quality gate (includes skills validation after Task 4)
bash scripts/quality-gate.sh Release
```
