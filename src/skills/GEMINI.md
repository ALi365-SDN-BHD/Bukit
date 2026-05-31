# Bukit Agent Knowledge Base

## Skill Activation

When the user mentions Bukit or Bukit-related concepts:

1. **Activate the gateway skill first**: `activate_skill("using-bukit")`
2. Follow the gateway's routing instructions to activate sub-skills as needed.
3. Commands use `run_shell_command` with `bukit` prefix.

## Trigger Keywords

Recognize these keywords and activate Bukit skills:

- English: bukit, site.yaml, Scriban, scriban, static site generator,
  Notion CMS, permalink, content collection, SSG, blog generator
- Chinese: 使用 bukit, bukit 静态站点, Scriban 模板, site.yaml 配置
- Malay: guna bukit, bukit penjana laman statik

## File/Context Signals

- User is editing or asking about `site.yaml`
- User mentions any `bukit` CLI command (build, init, deploy, preview, dev, etc.)
- User is working with `.scriban` or Scriban template files
- User is working with Notion-as-CMS

## Skill Paths

All skills: `src/skills/<skill-name>/SKILL.md`

Available skills (19 total):

| Skill | Purpose |
|-------|---------|
| using-bukit | Gateway — route to correct sub-skill |
| bukit-cli-reference | CLI commands, detection, installation |
| bukit-config | site.yaml configuration model |
| bukit-theme | Theme structure, static assets, wizard |
| bukit-templating | Scriban template development |
| bukit-design-tokens | CSS variables, color palettes, dark mode |
| bukit-content-to-template | Schema-driven template generation |
| bukit-notion | Notion content source integration |
| bukit-routing | URL routing and permalinks |
| bukit-i18n | Multilingual site setup |
| bukit-plugins-debug | Plugin system and build debugging |
| bukit-deploy | GitHub Pages deployment |
| bukit-clone | Website design cloning |
| bukit-seo | Traditional search engine optimization |
| bukit-geo | Generative engine optimization (AI search) |
| bukit-preview | Local preview server |
| bukit-dev | HMR development server |
| bukit-webhook | Webhook automated deployment |
| theme-component-system | Componentized theme system (V2) |

## Platform Notes

- Gemini CLI has no subagent support — run all Bukit tasks in a single session.
- Use `run_shell_command` for CLI operations.
- Use `read_file` / `write_file` for file operations.
- Use `save_memory` to persist key Bukit patterns across sessions.
- Use `tracker_create_task` for rich task management during complex Bukit workflows.

## Quick Reference

Common task → skill chains:

| Task | Activate Skills In Order |
|------|--------------------------|
| Create new site | using-bukit → cli-reference → config → theme → templating |
| Configure Notion | using-bukit → notion → config → cli-reference |
| Fix template error | using-bukit → templating → theme |
| Debug build | using-bukit → plugins-debug → config → cli-reference |
| Deploy | using-bukit → deploy → config → cli-reference |
| Clone website | using-bukit → clone → theme → cli-reference |
| SEO | using-bukit → seo → config → cli-reference |
| GEO (AI search) | using-bukit → geo → config → cli-reference |
| Design system | using-bukit → design-tokens → theme → config |
| Componentized theme | using-bukit → theme-component-system → theme → templating |

See `src/skills/skills-index.yaml` for the complete machine-readable catalog.
