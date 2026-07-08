# Bukit AI Demo-to-CMS Machine-readable Schemas

This directory provides JSON Schema files for validating AI-generated configuration and data files.

## Files

```text
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

Recommended validation flow:

```bash
bukit config validate --config sites/<site-name>/site.yaml
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```
