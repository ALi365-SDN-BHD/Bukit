# Bukit Agent Knowledge Base

## Skill Loading Rules

When the user mentions bukit, Bukit site generation, Scriban templates,
site.yaml files, or any related Bukit concepts:

1. **LOAD `src/skills/using-bukit/SKILL.md` FIRST via Skill tool**
   - This is the gateway skill that routes to the correct sub-skill.
   - It prevents conflicts with other SSG skills (Hugo, Jekyll, Astro, etc.).

2. **THEN load sub-skills as needed via Skill tool:**
   - `bukit-cli-reference` — for any CLI command execution (load BEFORE any bukit command)
   - `bukit-config` — for site.yaml configuration
   - `bukit-theme` — for theme structure, static assets, theme creation/distribution
   - `bukit-templating` — for Scriban template development
   - `bukit-design-tokens` — for CSS variables, color palettes, typography, dark mode
   - `bukit-content-to-template` — for schema-driven template generation
   - `bukit-notion` — for Notion content integration
   - `bukit-routing` — for URL routing, permalinks, 404 troubleshooting
   - `bukit-i18n` — for multilingual sites, sitemap/RSS merging
   - `bukit-plugins-debug` — for plugin debugging, incremental builds, performance
   - `bukit-deploy` — for GitHub Pages deployment
   - `bukit-clone` — for website design cloning (requires Browser MCP)
   - `bukit-import` — for local HTML demo import and seed workflow
   - `bukit-seo` — for traditional SEO, audit/diff, JSON-LD, sitemap
   - `bukit-geo` — for generative engine optimization, llms.txt, AI search
   - `bukit-preview` — for local preview server at localhost:4173
   - `bukit-dev` — for HMR development server with live reload
   - `bukit-webhook` — for webhook-triggered automated builds
   - `theme-component-system` — for componentized theme system (theme.yaml V2, sections, components, page composer)

3. **Trigger keywords** — Load when user mentions ANY of:
   - "bukit", "site.yaml", "Scriban", "scriban"
   - "static site generator", "SSG", "blog generator"
   - ".csproj" (in context of static site generation)
   - Bukit-specific concepts: "permalink", "content collection", "Notion CMS"

4. **Skill file paths**: src/skills/&lt;skill-name&gt;/SKILL.md

5. **CLI command policy**: ALL CLI command execution MUST reference `bukit-cli-reference`.
   Other skills provide knowledge and configuration guidance only — never duplicate
   command instructions.

6. **Platform**: Shell commands use `bukit` (cross-platform). Adapt for OS-specific
   suffixes (e.g., `bukit.exe` on Windows).

7. **Machine-readable catalog**: See `src/skills/skills-index.yaml` for the complete
   skill inventory with triggers, dependencies, and platform loading instructions.

## Quick Reference

| Task | Recommended Skill Load Order |
|------|------------------------------|
| Create new site | using-bukit → cli-reference → config → theme → templating |
| Configure Notion | using-bukit → notion → config → cli-reference |
| Fix template error | using-bukit → templating → theme |
| Debug build failure | using-bukit → plugins-debug → config → cli-reference |
| Deploy to GitHub Pages | using-bukit → deploy → config → cli-reference |
| Clone website design | using-bukit → clone → theme → cli-reference |
| Import local HTML demo | using-bukit → cli-reference → import → theme → templating |
| Set up SEO | using-bukit → seo → config → cli-reference |
| Set up GEO (AI search) | using-bukit → geo → config → cli-reference |
| Create design system | using-bukit → design-tokens → theme → config |
| Generate templates from schema | using-bukit → content-to-template → config → templating → design-tokens |
| Local preview | using-bukit → preview → cli-reference |
| HMR development | using-bukit → dev → cli-reference |
| Automated webhook builds | using-bukit → webhook → notion → cli-reference |
| Customize URL routing | using-bukit → routing → config → templating |
| Multilingual site | using-bukit → i18n → config → cli-reference |
| Componentized theme | using-bukit → theme-component-system → theme → templating |

## Environment Detection

Before operating on a Bukit site, verify the environment:

```bash
test -f site.yaml && echo "Bukit site detected" || echo "Not a Bukit site"
```

All bukit commands should be executed from the directory containing `site.yaml`.
