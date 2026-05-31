# Bukit Skills Maintenance Guide

## Overview

This file documents the maintenance procedures for the Bukit `src/skills/` agent knowledge system. Skills must stay aligned with source code, CLI, config model, and user documentation.

## Maintenance Flow

### When Source Code Changes

1. **CLI changes** (new/removed/modified commands or parameters):
   - Update `src/skills/bukit-cli-reference/SKILL.md` — command table and detailed sections
   - Update `src/skills/skills-index.yaml` — trigger patterns if needed
   - Regenerate: `bash src/skills/scripts/generate-index-json.sh`

2. **Config model changes** (new fields, renamed fields, changed defaults):
   - Update `src/skills/bukit-config/SKILL.md` — field reference tables
   - Update scenario templates if affected
   - Check `src/skills/bukit-seo/SKILL.md`, `src/skills/bukit-geo/SKILL.md`, `src/skills/bukit-notion/SKILL.md` for field references
   - Regenerate: `bash src/skills/scripts/generate-index-json.sh`

3. **User guide changes** (guide/user/*.md):
   - Update `guide_chapters` in the relevant SKILL.md Front Matter
   - Update skills-index.yaml `guide_chapter` references
   - Update `src/skills/using-bukit/SKILL.md` cross-reference table

4. **Plugin changes** (new plugins, removed plugins, lifecycle changes):
   - Update `src/skills/bukit-plugins-debug/SKILL.md`
   - Update plugin count in `src/skills/bukit-cli-reference/SKILL.md`
   - Update `src/skills/skills-index.yaml` descriptions

5. **Theme system changes**:
   - Update `src/skills/bukit-theme/SKILL.md`
   - If V2 componentized changes: update `src/skills/theme-component-system/SKILL.md`
   - Update `src/skills/bukit-templating/SKILL.md` if Scriban model changes

### Adding a New Skill

1. Create `src/skills/<skill-name>/SKILL.md` with required Front Matter:
   ```yaml
   ---
   name: <skill-name>
   description: Use when...
   status: stable|beta|experimental|planned
   since: "vX.Y.Z"
   verified_by:
     - "path/to/source"
   source_anchors:
     - "path/to/source"
   guide_chapters:
     - "guide/user/XX-chapter.md"
   ---
   ```
2. Add to `src/skills/skills-index.yaml` in the correct section
3. Update `skill_count` in skills-index.yaml
4. Add to `src/skills/plugin.json` `skills` array
5. Add to platform entry files:
   - `src/skills/CLAUDE.md` — skill list + Quick Reference
   - `src/skills/AGENTS.md` — if needed
   - `src/skills/GEMINI.md` — skill table + Quick Reference
   - `src/skills/copilot-instructions.md` — skill list
6. Add to `src/skills/README.md`:
   - Directory Layout
   - Skill Responsibilities table
   - Loading Rules (if needed)
   - Suggested Reading Paths (if needed)
7. Add workflow chain to skills-index.yaml if applicable
8. Add to `src/skills/using-bukit/SKILL.md` skill table + guide cross-reference
9. Regenerate: `bash src/skills/scripts/generate-index-json.sh`
10. Run validations:
    ```bash
    bash src/skills/scripts/validate-skills.sh
    bash src/skills/scripts/validate-skills-strict.sh
    ```

### Merging or Removing a Skill

1. Ensure no other skill depends on it (`requires` in skills-index.yaml)
2. Remove workflow chains that reference it
3. Remove from all platform entry files
4. Remove directory
5. Update `skill_count` in skills-index.yaml
6. Run validations as above

### Pre-Release Checklist

- [ ] All 19 SKILL.md files have complete Front Matter (name, description, status, since, verified_by, source_anchors, guide_chapters)
- [ ] `skill_count` matches actual SKILL.md count
- [ ] plugin.json skills list matches skills-index.yaml
- [ ] All `requires` dependencies are valid
- [ ] All workflow chains reference valid skills
- [ ] No local absolute paths in any skill file
- [ ] No hardcoded platform tool names ("Bash tool", "TodoWrite")
- [ ] skills-index.json is regenerated and in sync
- [ ] CLI commands match actual CLI `--help` output
- [ ] Config fields match actual config model
- [ ] `bash src/skills/scripts/validate-skills.sh` passes
- [ ] `bash src/skills/scripts/validate-skills-strict.sh` passes

### Status Definitions

| Status | Meaning | When to Use |
|--------|---------|-------------|
| `stable` | Production-ready, API stable, well-tested | Core features with established interfaces |
| `beta` | Implemented and working, but API may evolve | Newer features still settling |
| `experimental` | Exists in code but not for public dependency | Internal/hidden features |
| `planned` | Documented but not yet implemented | Roadmap items — must include warning in skill body |

**Never mark a planned feature as stable.** When in doubt, use the more conservative status.

### Validation Commands

```bash
# Basic validation (format, triggers, common errors)
bash src/skills/scripts/validate-skills.sh

# Strict validation (skill count, dependencies, paths, status)
bash src/skills/scripts/validate-skills-strict.sh

# Regenerate JSON index from YAML
bash src/skills/scripts/generate-index-json.sh
```
