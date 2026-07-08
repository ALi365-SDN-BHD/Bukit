# Bukit Core 1.0 Agent Skills

This directory is the Core 1.0 replacement for the historical skills pack. It keeps the useful stable guidance, but rewrites the gateway, indexes, CLI reference, theme/dev/debug guidance, and validators around the current Core boundary.

## Core Boundary

The default pack only treats these commands as Core:

`build`, `doctor`, `config`, `preview`, `dev`, `clean`, `version`, `completion`, `seo`, `geo`, `publish`, `deploy`.

These are not Core defaults: clone, import, webhook, plugin marketplace, theme registry, theme wizard, theme packaging/install, and template command tooling. They belong under `guide/labs-skills/` and must not be auto-loaded unless the user explicitly asks for Labs or experimental work.

## Directory Layout

```text
guide/skills/
  using-bukit/            # Core gateway
  bukit-cli-reference/    # Source-aligned CLI table
  bukit-config/           # site.yaml contract
  bukit-content/          # Markdown and multi-source content
  bukit-notion/           # Notion provider
  bukit-routing/          # URLs, permalinks, conflicts
  bukit-theme/            # themes/<name>/ and theme.yaml
  bukit-templating/       # Scriban template authoring
  bukit-i18n/             # multilingual output
  bukit-seo/              # seo audit/diff
  bukit-geo/              # geo audit and llms outputs
  bukit-preview/          # static preview server
  bukit-dev/              # LiveReload development server
  bukit-deploy/           # GitHub Pages deploy
  bukit-debug/            # doctor/build/built-in plugin diagnostics
  scripts/                # validators and index generation
```

## Loading Rules

1. Start with `using-bukit` for any Bukit implementation task.
2. Load `bukit-cli-reference` before suggesting or running a CLI command.
3. Load `bukit-config` before content, theme, routing, i18n, SEO, GEO, deploy, or debug work.
4. Load `bukit-theme` before `bukit-templating`.
5. Use `bukit-debug` for build output, derived pages, built-in plugins, route conflicts, output security, and doctor diagnostics.
6. Do not load `guide/labs-skills/*` unless the user explicitly asks for Labs or experimental capabilities.

## Validation

```bash
bash guide/skills/scripts/validate-skills-strict.sh
```

The strict validator checks metadata, index/plugin sync, source anchors, guide chapters, Core command drift, accidental non-Core command references, and inaccurate dev-server wording in Core skills.
