# Bukit Agent Skills

`src/skills/` contains Bukit-specific guidance for AI agents, not runtime source code. It breaks common Bukit tasks into focused `SKILL.md` files so an agent can choose the right knowledge boundary for site creation, configuration, theming, content integration, routing, multilingual output, and debugging.

If you use Bukit from Trae, Claude Code, Copilot CLI, Codex CLI, Gemini CLI, or another skill-aware environment, treat this directory as the agent-facing navigation layer:

- Start with `using-bukit` when the task explicitly uses Bukit
- Use `bukit-cli-reference` as the single source of truth for command execution
- Load the matching sub-skill for config, theme, templating, Notion, routing, i18n, or plugin/debug work

## Directory Layout

```text
src/skills/
  using-bukit/            # Unified Bukit entry point
  bukit-cli-reference/    # Single source of truth for CLI operations
  bukit-config/           # site.yaml configuration model
  bukit-theme/            # Theme directories and static assets
  bukit-templating/       # Scriban template development
  bukit-notion/           # Notion content source
  bukit-routing/          # URL routing and permalinks
  bukit-i18n/             # Multilingual sites
  bukit-plugins-debug/    # Plugins, incremental build, diagnostics
  bukit-deploy/           # GitHub Pages deployment
```

## Skill Responsibilities

| Skill | Responsibility | Typical use case |
|---|---|---|
| `using-bukit` | Gateway skill that identifies Bukit work and routes to sub-skills | The user explicitly says "using bukit" or the task is clearly Bukit-specific |
| `bukit-cli-reference` | CLI detection, installation guidance, command reference, output and exit-code interpretation | Running `bukit build`, `doctor`, `preview`, `theme`, `webhook`, and related commands |
| `bukit-config` | `site.yaml` structure, scenario templates, and field explanations | Creating or editing config, explaining fields, fixing validation errors |
| `bukit-theme` | Theme directory structure, static assets, and theme parameters | Creating or migrating themes, fixing CSS or static asset issues, using `theme.params` |
| `bukit-templating` | Scriban syntax, layout inheritance, data access, and template patterns | Writing page templates, list pages, pagination, or fixing template rendering errors |
| `bukit-notion` | Notion integration, property mapping, block rendering, and image localization | Using Notion as CMS or troubleshooting Notion fetch and mapping issues |
| `bukit-routing` | Permalinks, collection routes, URL encoding, and output path behavior | Customizing URLs, fixing 404s, handling route conflicts, configuring list pages |
| `bukit-i18n` | Language detection, per-language builds, sitemap/RSS/search merging | Building multilingual sites and debugging language switch or merged output issues |
| `bukit-plugins-debug` | Plugin lifecycle, incremental build behavior, performance diagnostics, troubleshooting | Plugins do not run, build output looks wrong, or build performance regresses |
| `bukit-deploy` | GitHub Pages deployment via `bukit deploy` command, site.yaml deploy config, environment variables, CI/CD integration | Deploying site, pushing to gh-pages, configuring CNAME, troubleshooting deploy failures |

## Loading Rules

These skills are designed to be combined with clear boundaries:

1. Start from `using-bukit` when the task is confirmed to be a Bukit task
2. Use `bukit-cli-reference` for every command-related step instead of duplicating command guidance elsewhere
3. Treat `bukit-config` as background knowledge for `bukit-theme`, `bukit-notion`, `bukit-routing`, `bukit-i18n`, and `bukit-plugins-debug`
4. Read `bukit-theme` before `bukit-templating` when template work depends on theme structure

One common flow looks like this:

```text
using-bukit
  -> bukit-cli-reference
  -> bukit-config
  -> bukit-theme / bukit-notion / bukit-routing / bukit-i18n / bukit-plugins-debug
  -> bukit-templating
```

## Suggested Reading Paths

### Create a new site

1. `using-bukit`
2. `bukit-cli-reference`
3. `bukit-config`
4. `bukit-theme`
5. `bukit-templating`

### Configure Notion as content source

1. `using-bukit`
2. `bukit-notion`
3. `bukit-config`
4. `bukit-cli-reference`

### Customize routing and list pages

1. `using-bukit`
2. `bukit-routing`
3. `bukit-config`
4. `bukit-templating`

### Debug build or plugin issues

1. `using-bukit`
2. `bukit-plugins-debug`
3. `bukit-config`
4. `bukit-cli-reference`

### Deploy site to GitHub Pages

1. `using-bukit`
2. `bukit-deploy`
3. `bukit-config`
4. `bukit-cli-reference`

## Maintenance Notes

- Keep each skill at `src/skills/<skill-name>/SKILL.md`
- Use `description` only for trigger conditions, not for generic summaries
- Centralize CLI instructions in `bukit-cli-reference`
- Keep theme paths, config fields, and CLI parameters aligned with the codebase and user-facing docs
- When Bukit gains new capabilities, decide whether to extend an existing skill or add a new one with a clear responsibility boundary

## Related Docs

- Repo entry: [`README.md`](../../README.md)
- Chinese reference: [`README.zh-CN.md`](../../README.zh-CN.md)
- User guide: [`guide/user`](../../guide/user/README.md)
- Developer guide: [`guide/dev`](../../guide/dev/README.md)
- Skills design doc: [`docs/superpowers/specs/2026-05-05-bukit-skills-distillation-design.md`](../../docs/superpowers/specs/2026-05-05-bukit-skills-distillation-design.md)
