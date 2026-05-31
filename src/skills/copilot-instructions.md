# Bukit Agent Knowledge Base

When the user's task involves Bukit (static site generator for .NET):

1. Use the `skill` tool to load Bukit skills from `src/skills/<skill-name>/SKILL.md`.
2. Start with `using-bukit` as the gateway skill.
3. Route to sub-skills per the gateway's routing table.

Bukit trigger signals — load Bukit skills when any of these are detected:
- User mentions "bukit", "site.yaml", "Scriban"
- Working in a repo with `.csproj` files and `site.yaml`
- Notion-as-CMS conversations
- Static site generation tasks with .NET context

Available skills (19):
using-bukit, bukit-cli-reference, bukit-config, bukit-theme,
bukit-templating, bukit-design-tokens, bukit-content-to-template,
bukit-notion, bukit-routing, bukit-i18n, bukit-plugins-debug,
bukit-deploy, bukit-clone, bukit-seo, bukit-geo, bukit-preview,
bukit-dev, bukit-webhook, theme-component-system

Platform notes:
- Use `skill` tool to load skills (same as Claude Code's `Skill` tool).
- Use `bash` for CLI commands, `view`/`create`/`edit` for file operations.
- Use `task` for sub-agent dispatch when available.
- Use `store_memory` to persist Bukit patterns across sessions.
- All CLI commands should use `bukit` (cross-platform); adapt `.exe` suffix on Windows.

Full catalog: `src/skills/skills-index.yaml`
