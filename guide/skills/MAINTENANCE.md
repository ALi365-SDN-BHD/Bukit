# Skills Maintenance

## When to update

Update the affected skill in the same change whenever a Core command, option, exit
code, `site.yaml` key or default, front matter field, route rule, template model
field, theme manifest rule, output file, report, or log event changes. Also update the
user and developer guides (see `guide/dev/documentation-governance.md`).

## Writing rules

- One skill per task area; `using-bukit` routes to the others. Keep each `SKILL.md`
  focused and under about 200 lines.
- Frontmatter needs `name` (matching the directory) and a `description` that says
  when to use the skill.
- Verify every key, default, and behavior against `src/Bukit-Core`, not against older
  prose. State known inert keys and surprising defaults explicitly.
- Examples must be valid: camelCase in `site.yaml`, snake_case in `theme.yaml`, Scriban
  (not Liquid) in templates.
- Never write `bukit <name>` for an unregistered command.

## Adding or renaming a skill

1. Create `<name>/SKILL.md`.
2. Add it to `skills-index.yaml`, `skills-index.json`, the table in `README.md`, and
   the skill map in `using-bukit`.

`bukit-cli-reference` must keep its path and name every command, subcommand, and
option in backticks; `scripts/checks/cli-docs/validate_docs.py` enforces this.

## Validate

```bash
bash guide/skills/scripts/validate-skills-strict.sh
bash scripts/checks/cli-docs-sync.sh
```
