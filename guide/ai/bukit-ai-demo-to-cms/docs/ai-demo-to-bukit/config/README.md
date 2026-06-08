# Bukit AI Demo-to-CMS Configuration Contracts

This directory constrains AI-generated Bukit configuration files and content data files. Its purpose is to prevent plausible but invalid configuration output.

## Files

| File | Purpose |
|---|---|
| `site-yaml-spec.md` | `site.yaml` fields, hierarchy, legal combinations, and common errors |
| `site-yaml-profiles.md` | Standard `site.yaml` Profiles that AI must choose from |
| `seed-data-spec.md` | Content seed field contracts |
| `demo-routes-spec.md` | `demo.routes.yaml` route mapping specification |
| `notion-database-map-spec.md` | `notion-database-map.yaml` specification |
| `template-manifest-spec.md` | `bukit.templates.yaml` template manifest specification |
| `environment-variables-spec.md` | Notion and build environment variable naming rules |

## Machine-readable Schemas

Schemas are located at:

```text
schemas/
  site.schema.json
  demo-routes.schema.json
  notion-database-map.schema.json
  template-manifest.schema.json
  seed/
    pages.schema.json
    posts.schema.json
    companies.schema.json
    services.schema.json
```

## Core Rules

1. AI must not invent `site.yaml` fields.
2. AI must select a standard Profile before generating `site.yaml`.
3. `content.sources[]` must be used; `content.provider` must not appear.
4. `--build-source notion` requires `--content-source notion`.
5. Every content mode must use `content.sources[]`.
6. Generated configuration must pass schema validation, `bukit doctor`, and `bukit build`.
7. If validation fails, AI must repair the configuration.
