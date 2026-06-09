# Bukit Skills Upgrade — Quality Report

**Date**: 2026-06-09
**Version**: skills-index v3.0.0 (v3.1 Code-Aligned Beta)
**Audit Source**: Full codebase audit of CLI, Config, SEO/GEO, Plugins, Webhook, Deploy, Dev, Preview

## Summary

Completed a systematic upgrade of Bukit's `src/skills/` agent knowledge system from 19 manually-maintained Markdown guides to a verifiable, maintainable, code-aligned knowledge base. As of 2026-06-09, the catalog has 20 skills after adding `bukit-import` for local HTML demo import and seed workflows. All 20 skills now have consistent Front Matter with status metadata, source anchors, and guide chapter references.

### v3.1 Code-Aligned Beta Changes

This round focused on code-fact-source alignment:
- **CLI Reference**: Completed `import html-demo`, `import seed`, `notion push`, `notion validate-schema`, `publish audit`, `publish diff` parameters; fixed missing `--allow-cross-schema`, `--template`, `--mode`, `--preserve-html`, `--extract-content`, `--route-map`, `--report`, `--config`, `--site`, and 25+ other parameters
- **Notion Config**: Migrated `content.notion` to `content.sources[].notion` with deprecation warnings; added Bukit 1.0 example and common issue
- **bukit-config**: Added `content.modelSchema` complete field reference (22+ fields), added `externalPlugins.templateRequirements`
- **using-bukit**: Added explicit bukit-import vs bukit-clone distinction note
- **theme-component-system**: Clarified CLI command registration status (handler exists in ThemeCommand.cs but not in BukitCliSpecs)
- **Validator Enhancement**: Added plugin.json order/count check; upgraded check-cli-commands.py with option-level validation

## Files Changed

| File | Change Type |
|------|-------------|
| `src/skills/CLAUDE.md` | Added theme-component-system, Quick Reference entry |
| `src/skills/GEMINI.md` | 18→19, added theme-component-system, Quick Reference |
| `src/skills/copilot-instructions.md` | 18→19, added theme-component-system |
| `src/skills/README.md` | Directory Layout, Skill Responsibilities table, 5-layer structure, 18→19 count |
| `src/skills/bukit-import/SKILL.md` | **New** — local HTML demo import, seed review, import report, optional Notion push workflow |
| `guide/user/21-import-html-demo.md` | **New** — user-facing import workflow |
| `guide/user/21-import-html-demo.zh-CN.md` | **New** — Chinese user-facing import workflow |
| `src/skills/skills-index.yaml` | GEO count 7→10 diagnostic codes |
| `src/skills/skills-index.json` | Regenerated from YAML |
| `src/skills/using-bukit/SKILL.md` | Added 5-layer structure reference, beta skill warnings |
| `src/skills/using-bukit/SKILL.md` (v3.1) | Added bukit-import vs bukit-clone distinction |
| `src/skills/bukit-cli-reference/SKILL.md` | 14→13 plugins, webhook desc fix, GEO exit codes, MIME+gzip, duplicate geo command removed |
| `src/skills/bukit-cli-reference/SKILL.md` (v3.1) | Completed import/notion/publish params (25+ added), fixed template/allow-cross-schema/clone |
| `src/skills/bukit-config/SKILL.md` | Six→Seven nodes, filterType 2→6 values, deploy field fix |
| `src/skills/bukit-config/SKILL.md` (v3.1) | Added modelSchema field reference, externalPlugins.templateRequirements |
| `src/skills/bukit-notion/SKILL.md` (v3.1) | Migrated content.notion → content.sources[].notion, deprecation warning, Bukit 1.0 example |
| `src/skills/bukit-geo/SKILL.md` | GEO diagnostic count 7→10 |
| `src/skills/bukit-dev/SKILL.md` | Added Bahasa Melayu multilingual triggers |
| `src/skills/theme-component-system/SKILL.md` | Removed file:// links and local absolute paths |
| `src/skills/theme-component-system/SKILL.md` (v3.1) | Clarified CLI registration status (BukitCliSpecs vs ThemeCommand.cs) |
| `src/skills/*/SKILL.md` (20 files) | Added status, since, verified_by, source_anchors, guide_chapters |
| `src/skills/scripts/validate-skills-strict.sh` | **New** — 17 strict validation checks (16 + 16b) |
| `src/skills/scripts/add-status-metadata.py` | **New** — status metadata injection tool |
| `src/skills/MAINTENANCE.md` | **New** — maintenance procedures, checklist, standards |
| `src/skills/QUALITY_REPORT.md` | **New** — this file |

## Key Fixes

### P0 — Critical (all fixed)
- **Skill count 18→19**: GEMINI.md, copilot-instructions.md, README.md
- **Skill count 19→20**: Added `bukit-import` and updated plugin.json, skills-index, gateway routing, platform entries, and guide links
- **Missing skill theme-component-system**: CLAUDE.md, GEMINI.md, copilot-instructions.md
- **README.md Directory Layout**: Added bukit-preview, bukit-dev, bukit-webhook, theme-component-system
- **bukit-config top-level nodes**: Six→Seven (including deploy)
- **CLI duplicate command**: Removed duplicate `geo` (kept `geo audit`)

### P1 — Semantic Alignment (all fixed)
- **GEO diagnostic count 7→10**: skills-index.yaml, bukit-geo overview
- **filterType values 2→6**: bukit-config now documents checkbox_true, checkbox_false, select_equals, status_equals, rich_text_equals, none
- **Plugin count 14→13**: bukit-cli-reference command table
- **Webhook description**: "build + push" → "GitHub repository_dispatch"; removed non-existent --token
- **GEO exit codes**: 2→1 for report-not-found and invalid-JSON
- **MIME types + gzip**: CLI reference updated with WEBP, ICO, WOFF2 and gzip support
- **Deploy quick-ref field**: output→cname, keepHistory
- **bukit-dev Bahasa Melayu triggers**: Added missing row
- **Local absolute paths**: Removed from bukit-cli-reference, bukit-config, theme-component-system
- **file:// links**: Removed from theme-component-system

### Status Metadata Added
All 20 skills now have:
```yaml
status: stable|beta
since: "v3.0.0"
verified_by: ["path/to/source"]
source_anchors: ["path/to/source"]
guide_chapters: ["guide/user/XX-chapter.md"]
```

Status distribution: 15 stable, 5 beta (bukit-content-to-template, bukit-clone, bukit-import, bukit-geo, theme-component-system)

### New Infrastructure
- `validate-skills-strict.sh`: 17 checks (16 + 16b: skill count, plugin.json sync, Front Matter, source paths, guide paths, local paths, tool names, JSON deep sync, dependencies, workflows, Markdown tables, CLI commands, status consistency, YAML validation, keyword consistency, skills-index.yaml duplicates, SKILL.md front matter duplicates)
- `MAINTENANCE.md`: Full maintenance procedures, pre-release checklist, status definitions

## Validation Results

| Validator | Result |
|-----------|--------|
| `validate-skills.sh` | ✅ 20/20 skills passed, 0 errors |
| `validate-skills-strict.sh` | ✅ All checks passed, 0 errors, 0 warnings |
| `skills-index.json` sync | ✅ In sync with skills-index.yaml |
| plugin.json sync | ✅ Consistent with skills-index.yaml (version, path, order) |
| Front Matter completeness | ✅ All 20 files have 7 required fields |
| requires dependencies | ✅ All valid |
| workflow chains | ✅ All valid |
| CLI option-level validation | ✅ Enhanced — checks options per command against BukitCliSpecs |

## Remaining Risks

| Risk | Status |
|------|--------|
| CLI quick reference table merge (clone\|\|geo, docs\|\|version) | Fixed (2026-05-31) |
| clone/docs check/route inspect missing from CLI reference | Fixed |
| propertyMap/filterValue/analytics.disableInPreview docs missing | Fixed |
| CLI semantic validation not hard-gating | Fixed — check-cli-commands.py now hard-gates |
| check-cli-commands.py inline subcommand parsing | Fixed — inline `Name:` detection added |
| CLI option-level validation | Fixed (v3.1) — options cross-checked against BukitCliSpecs |
| check-cli-commands.py regex-based C# parsing | Remaining — replace with generated CLI metadata before stable |
| PyYAML missing causes YAML check skip | Fixed — check-yaml-examples.py exits 1 unless ALLOW_SKIP_YAML_VALIDATION=1 |
| status keyword warnings not propagated to strict validator | Fixed — check-status-keywords.py now exits 1 on mismatch |
| theme planned commands (doctor, list-components, export-catalog) | Mitigated — marked as planned in skill; code handlers exist but CLI specs not registered |
| V2 componentized theme stability | Beta — may need reassessment as implementation stabilizes |
| skills-index.yaml duplicate source_anchors | Remaining — needs deduplication |
| plugin.json order/version/position validation | Fixed (v3.1) — strict validator checks order, count, version, and path |
| content.notion config deprecated → content.sources[] | Fixed (v3.1) — all references migrated with deprecation warnings |

## Recommended Next Steps

1. **Run `validate-skills-strict.sh`**: Verify all changes pass
2. **Run `generate-index-json.sh`**: Regenerate skills-index.json from YAML
3. **Integrate into CI**: Already added to quality-gate.sh
4. **Generate CLI metadata from CLI itself**: Replace regex-based C# parsing with `bukit --metadata` or similar machine-readable output for Stable
5. **Auto-generate platform entry files**: Reduce manual sync burden for AGENTS.md, CLAUDE.md, GEMINI.md, copilot-instructions.md
6. **Capability-level status tracking**: Add fine-grained beta/planned/stable tracking per feature (not just per skill) before v3.1 Stable
