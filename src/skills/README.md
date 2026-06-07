> **Status: Beta** — These skills are actively maintained and verified against source code,
> but the knowledge base structure and validation tooling may evolve. See [QUALITY_REPORT.md](QUALITY_REPORT.md)
> for known issues.

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
  bukit-theme/            # Theme directories, static assets, creation wizard, distribution
  bukit-templating/       # Scriban template development
  bukit-design-tokens/    # CSS variables, color palettes, typography, spacing, dark mode
  bukit-content-to-template/  # Schema-aware template generation
  bukit-notion/           # Notion content source
  bukit-routing/          # URL routing and permalinks
  bukit-i18n/             # Multilingual sites
  bukit-plugins-debug/    # Plugins, incremental build, diagnostics
  bukit-deploy/           # GitHub Pages deployment
  bukit-clone/            # Website design cloning → Bukit theme
  bukit-import/           # Local HTML demo import → Bukit site draft
  bukit-seo/              # Traditional search engine optimization (SEO)
  bukit-geo/              # Generative engine optimization (GEO)
  bukit-preview/          # Local preview server
  bukit-dev/              # HMR development server
  bukit-webhook/          # Webhook-triggered automated builds
  theme-component-system/ # Componentized theme system (V2)
```

## Skill Responsibilities

| Skill | Responsibility | Typical use case |
|---|---|---|
| `using-bukit` | Gateway skill that identifies Bukit work and routes to sub-skills | The user explicitly says "using bukit" or the task is clearly Bukit-specific |
| `bukit-cli-reference` | CLI detection, installation guidance, command reference, output and exit-code interpretation | Running any `bukit` command including `theme wizard/pack/install/search`, `template create/list/show/validate/snippets/hints/sync` |
| `bukit-config` | `site.yaml` structure, scenario templates, and field explanations | Creating or editing config, explaining fields, fixing validation errors |
| `bukit-theme` | Theme directory structure, static assets, wizard-based creation, theme distribution (pack/install), registry search, template snippets | Creating themes via wizard/preset, listing theme info/params, packaging themes for sharing, installing from registry, browsing template snippets |
| `bukit-templating` | Scriban syntax, layout inheritance, data access, and template patterns | Writing page templates, list pages, pagination, or fixing template rendering errors |
| `bukit-design-tokens` | Design token systems for Bukit themes: CSS variables, color palettes, typography scales, spacing systems, and dark mode configuration | Creating a consistent visual identity, defining `:root {}` CSS variables, setting up dark mode, choosing color palettes |
| `bukit-content-to-template` | Schema-driven template generation: maps content content model field scopes to precise Scriban template patterns | Generating post/page/list/card templates from `site.yaml` content model field scope definitions, ensuring every field is correctly rendered |
| `bukit-notion` | Notion integration, property mapping, block rendering, and image localization | Using Notion as CMS or troubleshooting Notion fetch and mapping issues |
| `bukit-routing` | Permalinks, collection routes, URL encoding, and output path behavior | Customizing URLs, fixing 404s, handling route conflicts, configuring list pages |
| `bukit-i18n` | Language detection, per-language builds, sitemap/RSS/search merging | Building multilingual sites and debugging language switch or merged output issues |
| `bukit-plugins-debug` | Plugin lifecycle, incremental build behavior, performance diagnostics, troubleshooting | Plugins do not run, build output looks wrong, or build performance regresses |
| `bukit-deploy` | GitHub Pages deployment via `bukit deploy` command, site.yaml deploy config, environment variables, CI/CD integration | Deploying site, pushing to gh-pages, configuring CNAME, troubleshooting deploy failures |
| `bukit-clone` | Browser MCP extraction → `bukit clone` CLI → verification pipeline for cloning any website's visual design into a Bukit theme | Cloning a website's appearance, replicating a design, creating a theme from an existing live site |
| `bukit-import` | Local HTML demo import, seed review, `import-report.md`, and optional Notion seed push | Converting an offline HTML demo directory into a Bukit theme/site draft |
| `bukit-seo` | Traditional SEO configuration (site.seo node), inject/theme render modes, front matter SEO fields, 6 Schema.org JSON-LD types, build-time diagnostics (11 codes), post-build audit (~40 codes), CLI seo audit/diff | Configuring SEO, running seo audit/diff, interpreting seo.* diagnostic codes, setting up OG/Twitter/JSON-LD/sitemap |
| `bukit-geo` | Generative engine optimization for AI search engines: llms.txt/llms-full.txt generation, AI crawler robots.txt rules, FAQ/HowTo structured data, geo audit with GEO Score (10 diagnostic codes) | Optimizing for AI search (ChatGPT Search/Perplexity/Google AI Overviews), generating llms.txt, adding FAQ/HowTo schema, running geo audit |
| `bukit-preview` | Local preview server — serves dist/ at localhost:4173, MIME type handling, analytics disabling, port conflict resolution | Previewing build output locally before deployment, troubleshooting port conflicts |
| `bukit-dev` | HMR development server — file watching with 300ms debounce, incremental rebuild, WebSocket live browser refresh at localhost:35729 | Active development with automatic rebuild and live browser refresh |
| `bukit-webhook` | Webhook server — authenticated Notion-style trigger → GitHub repository_dispatch for automated CI/CD builds | Setting up webhook-triggered auto-deploy from Notion updates |
| `theme-component-system` | Componentized theme system — theme.yaml V2 manifest, sections, components, pageTemplates, data bindings, tokens, theme-catalog.json, section schemas, page composer, theme inheritance chains | Building modular, AI-consumable themes with structured sections and components |

## Loading Rules

These skills are designed to be combined with clear boundaries:

1. Start from `using-bukit` when the task is confirmed to be a Bukit task
2. Use `bukit-cli-reference` for every command-related step instead of duplicating command guidance elsewhere
3. Treat `bukit-config` as background knowledge for `bukit-theme`, `bukit-design-tokens`, `bukit-content-to-template`, `bukit-notion`, `bukit-routing`, `bukit-i18n`, `bukit-plugins-debug`, `bukit-import`, `bukit-seo`, and `bukit-geo`
4. Read `bukit-theme` before `bukit-templating` when template work depends on theme structure
5. Load `bukit-design-tokens` when visual consistency is a goal — it provides palettes, scales, and dark mode patterns
6. Load `bukit-content-to-template` when generating templates from content model field scopes — it bridges schema field definitions to Scriban code
7. Load `bukit-seo` for traditional SEO tasks and `bukit-geo` for AI search optimization tasks — they share `site.seo` config but target different audiences
8. Load `theme-component-system` for V2 componentized theme work — it depends on `bukit-theme` and `bukit-templating` as prerequisites

One common flow looks like this:

```text
using-bukit
  -> bukit-cli-reference
  -> bukit-config
  -> bukit-theme / bukit-notion / bukit-routing / bukit-i18n / bukit-plugins-debug
  -> bukit-templating
```

## Usage Guide

### File Layout

```
src/skills/
├── CLAUDE.md                    ← Claude Code agent entry (full)
├── AGENTS.md                    ← Codex CLI agent entry (full)
├── GEMINI.md                    ← Gemini CLI agent entry (full)
├── copilot-instructions.md      ← Copilot CLI entry (full)
│
├── plugin.json                  ← Claude Code / Copilot plugin manifest
├── skills-index.yaml            ← Machine-readable skill catalog (single source of truth)
├── skills-index.json            ← JSON version (auto-generated from YAML)
│
├── using-bukit/SKILL.md           ← Gateway skill
├── bukit-*/SKILL.md               ← Bukit domain skills
├── theme-component-system/SKILL.md ← V2 componentized theme skill
│
└── scripts/
    ├── validate-skills.sh       ← CI: validates all skill files
    └── generate-index-json.sh   ← CI: YAML → JSON conversion
```

The root of the repository also contains lightweight redirect files (`CLAUDE.md`, `AGENTS.md`, `GEMINI.md`, `.github/copilot-instructions.md`) that satisfy each platform's root-level convention and point to the full files here.

### Per-Platform Usage

#### Trae

Trae auto-discovers skills via `.trae/rules/project_rules.md`. No extra configuration needed — the agent will find and load `using-bukit` and its sub-skills through the `Skill` tool when the user mentions Bukit.

```bash
# No installation required. Just open the repo in Trae and say:
"using bukit, help me build a blog"
```

#### Claude Code

**Option A — Project-level (automatic):**
The root `CLAUDE.md` file is auto-loaded at session start. It redirects to `src/skills/CLAUDE.md` which contains the full loading rules. No action needed — just open this repository in Claude Code.

**Option B — Plugin installation (recommended for Bukit users):**
```bash
# Install the skills as a Claude Code plugin
claude plugins install src/skills

# Or from GitHub (when published)
claude plugins install github.com/ALi365-SDN-BHD/Bukit
```

After installation, all 20 Bukit skills become available via the `Skill` tool whenever you mention Bukit-related concepts.

#### Codex CLI

Codex loads skills natively — there is no `Skill` tool. The root `AGENTS.md` is auto-detected. It tells Codex to read the full file at `src/skills/AGENTS.md`.

```bash
# In a Codex CLI session, just mention Bukit:
"help me configure a Bukit site.yaml for a blog"

# For sub-agent dispatch (requires multi_agent = true in ~/.codex/config.toml):
# The agent will read the relevant SKILL.md and pass it as spawn_agent instructions.
```

#### Copilot CLI

Copilot discovers skills via `plugin.json`. The root `.github/copilot-instructions.md` redirects to `src/skills/copilot-instructions.md`.

```bash
# Install the plugin
copilot plugin install src/skills

# Then use the skill tool to load skills
copilot "using bukit, deploy my site to GitHub Pages"
```

#### Gemini CLI

Gemini CLI activates skills via `activate_skill`. The root `GEMINI.md` redirects to `src/skills/GEMINI.md` which lists all available skills and trigger keywords.

```bash
# In a Gemini CLI session, just mention Bukit:
"set up a multilingual Bukit site with Chinese and English"
```

### Programmatic Access

The `skills-index.yaml` file is the machine-readable catalog. Platform entry files (CLAUDE.md, AGENTS.md, etc.) are currently maintained manually and validated for consistency with the index. A future release will auto-generate them. Use the catalog to:

- **Query skill metadata**: name, type, triggers, dependencies, guide chapter cross-references
- **Resolve dependency chains**: each skill declares its `requires` list; the `workflows` section defines common task chains
- **Generate platform entries**: the catalog drives all platform entry files (CLAUDE.md, AGENTS.md, etc.)

```bash
# Parse with yq
yq '.skills[] | select(.type == "gateway") | .name' skills-index.yaml

# Parse with python
python3 -c "
import yaml, json
with open('skills-index.yaml') as f:
    data = yaml.safe_load(f)
print(json.dumps(data['workflows'], indent=2))
"
```

### CI Verification

```bash
# Basic validation (format, triggers, common errors)
bash src/skills/scripts/validate-skills.sh

# Strict validation (15 semantic checks — see below)
bash src/skills/scripts/validate-skills-strict.sh

# Regenerate JSON index after YAML changes
bash src/skills/scripts/generate-index-json.sh
```

The strict validator runs 15 checks: skill count, plugin.json sync, Front Matter completeness, source_anchors paths, guide_chapters paths, local absolute paths, platform tool names, JSON sync, requires dependencies, workflow chains, Markdown table consistency, CLI commands consistency, status consistency, YAML code block validation, status keyword consistency.

The basic validate script checks:
- Front Matter completeness (`name` + `description`)
- `description` starts with "Use when…"
- Multilingual Triggers section present
- Common Errors section present
- No hardcoded platform-specific tool names
- `plugin.json` paths all resolve to existing files
- `skills-index.yaml` entries match existing SKILL.md files

### Quick Start (Any Platform)

1. Open this repository in your AI agent
2. Say: **"using bukit, help me build a blog"**
3. The agent will:
   - Read the gateway skill (`using-bukit`)
   - Detect CLI availability (`bukit-cli-reference`)
   - Generate `site.yaml` (`bukit-config`)
   - Create the theme and templates (`bukit-theme` + `bukit-templating`)
   - Build the site
4. Say: **"bukit dev"** to start the HMR dev server and preview

---

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

### Configure SEO and run audits

1. `using-bukit`
2. `bukit-seo`
3. `bukit-config` (for `site.seo` node)
4. `bukit-cli-reference` (for `bukit seo audit` / `bukit seo diff`)

### Set up GEO for AI search engines

1. `using-bukit`
2. `bukit-geo`
3. `bukit-config` (for `site.seo.geo` node)
4. `bukit-cli-reference` (for `bukit geo audit`)

### Clone a website's design

1. `using-bukit`
2. `bukit-clone`
3. `bukit-theme`
4. `bukit-cli-reference`

### Create a custom theme (interactive)

1. `using-bukit`
2. `bukit-theme` (wizard + presets)
3. `bukit-cli-reference`

### Install a theme from the community registry

1. `using-bukit`
2. `bukit-theme` (search + install)
3. `bukit-cli-reference`

### Build a consistent design system

1. `using-bukit`
2. `bukit-design-tokens`
3. `bukit-theme`
4. `bukit-config`

### Generate templates from content schema

1. `using-bukit`
2. `bukit-content-to-template`
3. `bukit-config` (for content model field scope)
4. `bukit-templating`
5. `bukit-design-tokens` (for visual styling)

### Build a componentized theme

1. `using-bukit`
2. `theme-component-system`
3. `bukit-theme`
4. `bukit-templating`

## Skill Layer Structure

Bukit skills are organized in five layers for clear responsibility boundaries:

| Layer | Skills | Purpose |
|---|---|---|
| **Gateway** | `using-bukit` | Entry point — routes to sub-skills, prevents other SSG skills from loading |
| **Core Reference** | `bukit-cli-reference`, `bukit-config` | Foundation — CLI commands and configuration model |
| **Build Authoring** | `bukit-theme`, `bukit-templating`, `bukit-design-tokens`, `bukit-content-to-template`, `theme-component-system` | Visual layer — themes, templates, design tokens, and componentized themes |
| **Data / Site Features** | `bukit-notion`, `bukit-routing`, `bukit-i18n`, `bukit-seo`, `bukit-geo` | Content and optimization — content sources, URL routing, multilingual, search engine optimization |
| **Operations / Debug** | `bukit-plugins-debug`, `bukit-preview`, `bukit-dev`, `bukit-deploy`, `bukit-webhook`, `bukit-clone`, `bukit-import` | Runtime — debugging, preview, development, deployment, webhooks, website cloning, HTML demo import |

## Maintenance Notes

- Keep each skill at src/skills/&lt;skill-name&gt;/SKILL.md
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
