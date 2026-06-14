#!/usr/bin/env python3
"""Rewrite QUALITY_REPORT.md — update remaining risks and next steps"""
path = 'src/skills/QUALITY_REPORT.md'
with open(path) as f:
    content = f.read()

# Find and replace the Remaining Risks section
old_risks = """## Remaining Risks

| Risk | Notes |
|------|-------|
| `bukit clone` command in CLI reference | Missing from command table but confirmed in source. Intentionally hidden? Needs decision. |
| `docs check` and `route inspect` commands | Missing from CLI reference. Consider adding if stable. |
| `theme doctor`, `theme list-components`, `theme export-catalog` | Undocumented subcommands in ThemeCommand.cs. Intentionally hidden? |
| `bukit-notion` propertyMap field | Not documented in any skill (12 field mappings: Title, Slug, Type, etc.) |
| `bukit-config` analytics.disableInPreview | Field exists in source but not in skill docs |
| `bukit-config` content.notion.filterValue | Not documented (required for select_equals/status_equals/rich_text_equals) |
| V2 componentized theme stability | Marked beta; may need reassessment as implementation stabilizes |
| `using-bukit` cross-reference table | Missing guide chapters for bukit-design-tokens and bukit-content-to-template |"""

new_risks = """## Remaining Risks

| Risk | Status |
|------|--------|
| CLI quick reference table merge (clone||geo, docs||version) | Fixed (2026-05-31) |
| clone/docs check/route inspect missing from CLI reference | Fixed |
| propertyMap/filterValue/analytics.disableInPreview docs missing | Fixed |
| CLI semantic validation not hard-gating | Remaining — planned for next validator version |
| theme planned commands (doctor, list-components, export-catalog) | Mitigated — marked as planned in skill |
| Tailwind CDN in external_css example | Mitigated — replaced with font CDN example |
| check-cli-commands.py does not parse full command paths | Remaining — planned |
| V2 componentized theme stability | Marked beta; may need reassessment as implementation stabilizes |"""

content = content.replace(old_risks, new_risks)

# Find and replace the Recommended Next Steps section
old_next = """## Recommended Next Steps

1. **Add missing CLI commands**: Document `clone`, `docs check`, `route inspect` in CLI reference with appropriate status labels
2. **Document Notion propertyMap**: Add to bukit-notion and bukit-config
3. **Document analytics.disableInPreview**: Add to bukit-config analytics section
4. **Document filterValue**: Add to bukit-config Notion field table
5. **Decide on hidden theme subcommands**: Either document or mark as internal
6. **Run `dotnet test`**: Verify no regressions from content changes
7. **Consider adding CI check**: Add `validate-skills-strict.sh` to CI pipeline"""

new_next = """## Recommended Next Steps

1. **Upgrade check-cli-commands.py**: Parse parent.child command paths correctly (e.g., `theme create`, `seo audit`)
2. **Add Markdown table column-count consistency** to check-markdown-tables.py validator
3. **Add YAML example parsing validation** to catch malformed YAML code blocks
4. **Run `dotnet test`**: Verify no regressions from content changes
5. **Add `validate-skills-strict.sh` to CI pipeline**: Already added to quality-gate.sh"""

content = content.replace(old_next, new_next)

with open(path, 'w') as f:
    f.write(content)
print("Done: QUALITY_REPORT.md updated")
