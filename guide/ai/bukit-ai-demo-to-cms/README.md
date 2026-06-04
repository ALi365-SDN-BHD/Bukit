# Bukit AI Demo-to-CMS Final Specification Package v1.2 English Full

This package is the complete English version of the Bukit AI Demo-to-CMS specification. It is intended for production use with ChatGPT, Codex, Claude Code, Cursor, Trae, and other AI agents.

It combines and supersedes previous packages:

```text
v1.0: Full Demo-to-CMS workflow, AI tool rules, and Skill files
v1.1: Detailed site.yaml and seed data configuration contracts
v1.2: Route/map/template/environment specs and machine-readable JSON Schemas
```

## Core Workflow

```text
User requirements
-> AI generates a migratable HTML Demo
-> User confirms style, pages, functionality, URL structure, and content direction
-> AI / Bukit converts the confirmed Demo into theme templates, content data, Notion seed, and configuration files
-> Schema validation
-> Bukit doctor
-> Bukit build
-> Notion CMS push when required
-> Notion-only build when ready
-> Deployment
```

## What This Package Provides

This package gives AI tools a stable operating contract for Bukit website production:

- A staged Demo-first workflow.
- Engineering rules for converting Demo output into Bukit projects.
- Configuration contracts for `site.yaml`, `demo.routes.yaml`, `notion-database-map.yaml`, `bukit.templates.yaml`, and seed data.
- Machine-readable JSON Schemas.
- Codex-compatible `AGENTS.md`.
- Claude Code-compatible `CLAUDE.md`, rules, and Skill.
- Cursor and Trae project rules.
- A generic Skill file that can be reused by other tools.

## Directory Layout

```text
.
|---- README.md
|---- MANIFEST.md
|---- AGENTS.md
|---- CLAUDE.md
|---- docs/
|   `---- ai-demo-to-bukit/
|       |---- README.md
|       |---- engineering-spec.md
|       |---- prompt-template.md
|       |---- checklist.md
|       `---- config/
|           |---- README.md
|           |---- site-yaml-spec.md
|           |---- site-yaml-profiles.md
|           |---- seed-data-spec.md
|           |---- demo-routes-spec.md
|           |---- notion-database-map-spec.md
|           |---- template-manifest-spec.md
|           `---- environment-variables-spec.md
|---- schemas/
|   |---- README.md
|   |---- site.schema.json
|   |---- demo-routes.schema.json
|   |---- notion-database-map.schema.json
|   |---- template-manifest.schema.json
|   `---- seed/
|       |---- pages.schema.json
|       |---- posts.schema.json
|       |---- companies.schema.json
|       `---- services.schema.json
|---- skills/
|   `---- bukit-demo-to-cms/
|       `---- SKILL.md
|---- .agents/
|   `---- skills/
|       `---- bukit-demo-to-cms/
|           `---- SKILL.md
|---- .claude/
|   |---- rules/
|   |   `---- bukit-demo-to-cms.md
|   `---- skills/
|       `---- bukit-demo-to-cms/
|           `---- SKILL.md
|---- .cursor/
|   `---- rules/
|       `---- bukit-demo-to-cms.mdc
`---- .trae/
    `---- rules/
        `---- bukit-demo-to-cms.md
```

## Installation

Copy the package into the root of the Bukit repository while preserving hidden directories:

```bash
rsync -av bukit-ai-demo-to-cms-final-v1.2-en-full/ /path/to/Bukit/
```

Before copying, check whether the target repository already has `AGENTS.md`, `CLAUDE.md`, `.cursor/rules`, `.trae/rules`, or Skill files. If so, merge manually instead of overwriting.

## Recommended First Use

Ask the AI tool:

```text
Use the bukit-demo-to-cms skill. First generate a migratable HTML Demo from my website requirements. Do not generate the final Bukit project until I confirm the Demo.
```

After user confirmation:

```text
The Demo is confirmed. Convert the final Demo into a Bukit theme, content seed, Notion seed, site.yaml, notion-database-map.yaml, and validation commands.
```

## Required Validation

Every generated configuration must be validated:

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

If supported:

```bash
bukit config validate --config sites/<site-name>/site.yaml
bukit doctor --config sites/<site-name>/site.yaml --strict
```
