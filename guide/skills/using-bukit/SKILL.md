---
name: using-bukit
description: Start here for any Bukit Core website task — what Bukit Core is, ground rules (only real Core commands, Labs opt-in), project layout, the standard edit-build-check loop, and which narrower Bukit skill to load for config, content, routing, templates, themes, i18n, build, outputs, SEO, GEO, analytics, servers, deploy, or troubleshooting.
---

# Using Bukit Core

Bukit is a static site generator. A project is a `site.yaml`, content (Markdown files
or a Notion database), and a local theme of Scriban templates. `bukit build` writes a
static site to `dist/` plus audit reports in `dist/.bukit/`.

## Ground rules

1. Use only the twelve Core commands: `build`, `doctor`, `config`, `preview`, `dev`,
   `clean`, `version`, `completion`, `seo`, `geo`, `publish`, `deploy`. There is no
   `init`, `new`, `theme`, `import`, `clone`, or `lint` command and no `--version` flag.
2. Labs features (site cloning, HTML import, intent files, webhooks, theme wizards,
   registries or marketplaces, external plugins such as IndexNow) are not Core. Use the
   Labs skills only when the user explicitly asks for them.
3. Every `site.yaml` key must exist: unknown keys fail. Check a key in `bukit-config`
   or `bukit config schema` before writing it.
4. Never put secrets in `site.yaml`. Notion uses `NOTION_TOKEN`; deploy uses
   `GITHUB_TOKEN` or `--token-env`.
5. Verify with commands, not assumptions: `bukit config check`, `bukit doctor`,
   `bukit build`, and the audits.

## Project layout

```text
site.yaml
content/              Markdown sources (any dir named in content.sources)
data/                 optional JSON/YAML files -> site.data_files
layouts/              unnamed theme, or overrides for a named theme
themes/<name>/        named theme: theme.yaml, tokens.yaml, layouts/, assets/, static/
dist/                 build output (do not commit)
.cache/               incremental and media cache (do not commit)
.bukit/               optional local files such as --metrics output (do not commit)
```

Core has no scaffolding command; create these files directly.

## Standard loop

```bash
bukit config check
bukit doctor
bukit dev                       # edit with LiveReload at http://localhost:35729
bukit build
bukit seo audit --dir dist
bukit publish audit --dir dist
bukit preview                   # final check at http://localhost:4173
bukit deploy --dry-run          # prints the plan only; not a full preflight
```

`bukit dev` and `bukit preview` serve until Ctrl+C. Do not run this block top to bottom
in one shell: start them in the background or a separate terminal, and stop them before
the next step.

## Skill map

| Task | Skill |
|---|---|
| Exact commands, options, exit codes, env vars | `bukit-cli-reference` |
| `site.yaml` structure, defaults, plugin toggles | `bukit-config` |
| Content sources, Markdown, front matter, data | `bukit-content` |
| Notion, remote media, model schema, route metadata | `bukit-content-model` |
| URLs, collections, lists, pagination, taxonomy | `bukit-routing` |
| Scriban templates and the page model | `bukit-templating` |
| Theme folders, theme.yaml, assets, SCSS, images | `bukit-theme` |
| Languages and hreflang | `bukit-i18n` |
| Build modes, incremental cache, reports | `bukit-build` |
| Sitemap, feeds, search, robots.txt, aliases, 404 | `bukit-outputs` |
| SEO metadata, `seo audit`, `seo diff` | `bukit-seo` |
| Search Console, GA4, AI citation joins | `bukit-seo-insights` |
| llms.txt, AI crawlers, `geo audit` | `bukit-geo` |
| Analytics providers and consent | `bukit-analytics` |
| `dev` and `preview` servers | `bukit-local-server` |
| `publish audit`, GitHub Pages deploy, CI | `bukit-publish-deploy` |
| Errors and wrong output | `bukit-troubleshooting` |

## Pitfalls that affect most new sites

- `site.language` defaults to `zh-CN`; pages tagged with another language are dropped
  unless you set it.
- With `site.collections`, set `collection:` on every content source.
- Only `publishAt` is a publish date; `date:` is not.
- `content/index.md` is not the home page; `/` is the home list template.
- `tags`/`categories` in front matter need taxonomy templates.
- Raw HTML inside Markdown is escaped.
- Templates are Scriban with no auto-escaping; links need `site.base_url`.
- `preview` and `dev` ignore `baseUrl`; audits default to `--dir dist`.
