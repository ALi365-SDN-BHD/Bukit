# Bukit Agent Knowledge Base
# Canonical path: src/skills/GEMINI.md
# This file satisfies Gemini CLI's root-level convention.
# See src/skills/GEMINI.md for the full agent knowledge base.

The full Bukit agent instructions live at `src/skills/GEMINI.md`.
Read that file and use `activate_skill("using-bukit")` when the user
mentions bukit, site.yaml, Scriban, or any Bukit-related concepts.

Key paths:
- Skills: src/skills/<skill-name>/SKILL.md (18 skills)
- Index: src/skills/skills-index.yaml
- Plugin: src/skills/plugin.json

Platform note for Gemini CLI:
- No subagent support — run all tasks in single session.
- Use `run_shell_command` for CLI, `read_file`/`write_file` for files.
