# CLAUDE.md

@AGENTS.md

## Bukit Demo-to-CMS Workflow

When the user asks to design, generate, convert, validate, or publish a website for Bukit:

- Use the `bukit-demo-to-cms` skill.
- Follow the staged workflow:
  1. Analyze requirements.
  2. Generate a migratable HTML Demo.
  3. Wait for user confirmation of style and functionality.
  4. Convert the confirmed Demo into a Bukit theme, content data, Notion seed, and configuration.
  5. Validate with Bukit.
  6. Push to Notion when required.
  7. Build and publish.

Do not skip the Demo confirmation stage unless the user explicitly requests direct Bukit project generation.

Detailed rules are available in:

```text
.claude/rules/bukit-demo-to-cms.md
```
