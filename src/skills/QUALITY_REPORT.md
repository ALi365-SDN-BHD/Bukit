# Bukit Skills Upgrade — Quality Report

**Date**: 2026-05-31
**Version**: skills-index v3.0.0
**Audit Source**: Full codebase audit of CLI, Config, SEO/GEO, Plugins, Webhook, Deploy, Dev, Preview

## Summary

Completed a systematic upgrade of Bukit's `src/skills/` agent knowledge system from 19 manually-maintained Markdown guides to a verifiable, maintainable, code-aligned knowledge base. All 19 skills now have consistent Front Matter with status metadata, source anchors, and guide chapter references.

## Files Changed

| File | Change Type |
|------|-------------|
| `src/skills/CLAUDE.md` | Added theme-component-system, Quick Reference entry |
| `src/skills/GEMINI.md` | 18→19, added theme-component-system, Quick Reference |
| `src/skills/copilot-instructions.md` | 18→19, added theme-component-system |
| `src/skills/README.md` | Directory Layout, Skill Responsibilities table, 5-layer structure, 18→19 count |
| `src/skills/skills-index.yaml` | GEO count 7→10 diagnostic codes |
| `src/skills/skills-index.json` | Regenerated from YAML |
| `src/skills/using-bukit/SKILL.md` | Added 5-layer structure reference, beta skill warnings |
| `src/skills/bukit-cli-reference/SKILL.md` | 14→13 plugins, webhook desc fix, GEO exit codes, MIME+gzip, duplicate geo command removed |
| `src/skills/bukit-config/SKILL.md` | Six→Seven nodes, filterType 2→6 values, deploy field fix |
| `src/skills/bukit-geo/SKILL.md` | GEO diagnostic count 7→10 |
| `src/skills/bukit-dev/SKILL.md` | Added Bahasa Melayu multilingual triggers |
| `src/skills/theme-component-system/SKILL.md` | Removed file:// links and local absolute paths |
| `src/skills/*/SKILL.md` (19 files) | Added status, since, verified_by, source_anchors, guide_chapters |
| `src/skills/scripts/validate-skills-strict.sh` | **New** — 10 strict validation checks |
| `src/skills/scripts/add-status-metadata.py` | **New** — status metadata injection tool |
| `src/skills/MAINTENANCE.md` | **New** — maintenance procedures, checklist, standards |
| `src/skills/QUALITY_REPORT.md` | **New** — this file |

## Key Fixes

### P0 — Critical (all fixed)
- **Skill count 18→19**: GEMINI.md, copilot-instructions.md, README.md
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
All 19 skills now have:
```yaml
status: stable|beta
since: "v3.0.0"
verified_by: ["path/to/source"]
source_anchors: ["path/to/source"]
guide_chapters: ["guide/user/XX-chapter.md"]
```

Status distribution: 15 stable, 4 beta (bukit-content-to-template, bukit-clone, bukit-geo, theme-component-system)

### New Infrastructure
- `validate-skills-strict.sh`: 15 checks (skill count, plugin.json sync, Front Matter, source paths, guide paths, local paths, tool names, JSON sync, dependencies, workflows, Markdown tables, CLI commands, status consistency, YAML validation, keyword consistency)
- `MAINTENANCE.md`: Full maintenance procedures, pre-release checklist, status definitions

## Validation Results

| Validator | Result |
|-----------|--------|
| `validate-skills.sh` | ✅ 19/19 skills passed, 0 errors |
| `validate-skills-strict.sh` | ✅ 15/15 checks passed, 0 errors, 0 warnings |
| `skills-index.json` sync | ✅ In sync with skills-index.yaml |
| plugin.json sync | ✅ Consistent with skills-index.yaml |
| Front Matter completeness | ✅ All 19 files have 7 required fields |
| requires dependencies | ✅ All valid |
| workflow chains | ✅ All valid |

## Remaining Risks

| Risk | Status |
|------|--------|
| CLI quick reference table merge (clone||geo, docs||version) | Fixed (2026-05-31) |
| clone/docs check/route inspect missing from CLI reference | Fixed |
| propertyMap/filterValue/analytics.disableInPreview docs missing | Fixed |
| CLI semantic validation not hard-gating | Fixed — check-cli-commands.py now hard-gates |
| theme planned commands (doctor, list-components, export-catalog) | Mitigated — marked as planned in skill |
| Tailwind CDN in external_css example | Mitigated — replaced with font CDN example |
| check-cli-commands.py does not parse full command paths | Fixed — parses parent.child paths with whitelist |
| check-cli-commands.py inline subcommand parsing | Fixed — inline Name: detection added; parser still regex-based |
| V2 componentized theme stability | Marked beta; may need reassessment as implementation stabilizes |

## Recommended Next Steps

1. **Run `dotnet test`**: Verify no regressions from content changes
2. **Add `validate-skills-strict.sh` to CI pipeline**: Already added to quality-gate.sh
