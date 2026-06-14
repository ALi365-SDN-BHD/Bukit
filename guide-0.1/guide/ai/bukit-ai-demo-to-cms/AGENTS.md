# AGENTS.md

## Bukit AI Demo-to-CMS Instructions

When working in this repository, treat Bukit website generation as a staged engineering workflow.

```text
User requirements
-> Generate a migratable HTML Demo
-> User confirms style and functionality
-> Convert the confirmed Demo into a Bukit theme and content project
-> Validate with Bukit
-> Push content to Notion when required
-> Build and publish
```

## Do Not

- Do not generate disposable HTML that cannot be migrated.
- Do not skip the Demo confirmation stage unless explicitly requested.
- Do not keep long-term business content inside templates.
- Do not merge list and detail pages into a single static template.
- Do not use unstable or random template names.
- Do not create a Notion-only build with non-Notion seed sources.


## Configuration Generation Rules

When generating Bukit configuration files, the AI must follow these rules:

1. Do not invent `site.yaml` fields.
2. Select a standard Profile before generating `site.yaml`.
3. Reference `docs/ai-demo-to-bukit/config/site-yaml-spec.md`.
4. Generate only `content.sources[]`; never generate `legacy content provider field`.
5. `--build-source notion` requires `--content-source notion`.
6. Notion multi-database mode must use `content.sources`.
7. After generating configuration, run schema validation, `bukit doctor`, and `bukit build`.
8. If validation fails, fix the configuration. Do not ignore errors.

Required validation commands:

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

If supported:

```bash
bukit config validate --config sites/<site-name>/site.yaml
bukit doctor --config sites/<site-name>/site.yaml --strict
```

Expected future diagnostics from Bukit doctor:

```text
Unknown field: content.notion.database
Missing required field: content.sources[0].collection
Removed field: legacy content provider field
Invalid build source: notion requires content source notion
```

Required configuration references:

```text
docs/ai-demo-to-bukit/config/site-yaml-spec.md
docs/ai-demo-to-bukit/config/site-yaml-profiles.md
docs/ai-demo-to-bukit/config/seed-data-spec.md
docs/ai-demo-to-bukit/config/demo-routes-spec.md
docs/ai-demo-to-bukit/config/notion-database-map-spec.md
docs/ai-demo-to-bukit/config/template-manifest-spec.md
docs/ai-demo-to-bukit/config/environment-variables-spec.md
schemas/
```


## Validation

Before considering a task complete:

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

Review `import-report.md`, especially:

```text
Hardcoded Content Residue
Diagnostics
Link Validation
Visual Verification
Manual Review Required
```
