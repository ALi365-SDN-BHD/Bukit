# Bukit Agent Knowledge Base
# Canonical path: src/skills/AGENTS.md
# This file satisfies Codex CLI's root-level convention.
# See src/skills/AGENTS.md for the full agent knowledge base.

The full Bukit agent instructions live at `src/skills/AGENTS.md`.
Read that file when the user mentions bukit, site.yaml, Scriban,
or any Bukit-related concepts.

Key paths:
- Skills: src/skills/<skill-name>/SKILL.md (19 skills)
- Index: src/skills/skills-index.yaml
- Plugin: src/skills/plugin.json

Platform note for Codex:
- No `Skill` tool — read skill files directly with native file tools.
- Use `spawn_agent(message=...)` with skill content for sub-agents.
