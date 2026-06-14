# Bukit Core 1.0 Skills Entry

Use this directory when an agent needs Bukit-specific guidance.

Core command whitelist: `build`, `doctor`, `config`, `preview`, `dev`, `clean`, `version`, `completion`, `seo`, `geo`, `publish`, `deploy`.

Rules:

1. Load `using-bukit/SKILL.md` first for Bukit implementation work.
2. Load `bukit-cli-reference/SKILL.md` before any command execution or command advice.
3. Load `bukit-config/SKILL.md` before editing or explaining `site.yaml`.
4. Load `bukit-theme/SKILL.md` before `bukit-templating/SKILL.md`.
5. Use `bukit-debug/SKILL.md` for build, doctor, derived page, output security, route conflict, and built-in plugin issues.
6. Do not load `guide/labs-skills/*` unless the user explicitly asks for Labs or experimental capabilities.
7. Describe `dev` as a LiveReload development server, not module-level hot replacement.
