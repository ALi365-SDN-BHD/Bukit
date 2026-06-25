# Bukit Import Seed Contract

This document defines the first import seed domain contract for `Bukit.Importing`.
It covers the domain service used by the future external process Import plugin.

## Scope

The seed domain service converts generated seed data into local Markdown content.
It does not parse CLI arguments, invoke plugins, write console output, push to
Notion, call Labs commands, or call Bukit build/theme commands.

## Input Directory

`ImportSeedOptions.SeedDirectory` must exist and must stay inside
`ImportSeedOptions.ProjectRoot`.

The service reads these known seed files when present:

| File | Collection |
| --- | --- |
| `pages.json`, `pages.yaml`, `pages.yml` | `page` |
| `navigation.json`, `navigation.yaml`, `navigation.yml` | `navigation` |
| `posts.json`, `posts.yaml`, `posts.yml` | `post` |
| `companies.json`, `companies.yaml`, `companies.yml` | `company` |
| `services.json`, `services.yaml`, `services.yml` | `service` |

Markdown seed files are not treated as structured seed records in this module.
Unsupported files are ignored.

## Record Schema

Each JSON or YAML seed file must contain a top-level array. Each item must be an
object or mapping. Records without `title` or `name` are skipped.

Core fields:

| Field | Type | Notes |
| --- | --- | --- |
| `title` | string | Preferred display title. |
| `name` | string | Fallback title when `title` is absent. |
| `slug` | string | Optional. Generated from title when absent. |
| `type` | string | Optional collection override. |
| `summary` | string | Optional front matter field. |
| `content` | string | Optional Markdown body. |
| `language` | string | Optional front matter field. |
| `published` | boolean | Defaults to `true`. |
| `seo_title` | string | Optional front matter field. |
| `seo_description` | string | Optional front matter field. |

Scalar fields outside the core field list are preserved as extra front matter.
JSON strings, numbers, and booleans are preserved. YAML scalar strings, numbers,
and booleans are preserved.

Collection aliases:

| Input `type` | Collection |
| --- | --- |
| `home`, `page`, `pages` | `page` |
| `post`, `posts`, `article`, `articles` | `post` |
| `company`, `companies` | `company` |
| `service`, `services` | `service` |
| `navigation`, `nav`, `menu`, `menus` | `navigation` |

Unknown `type` values fall back to the collection inferred from the seed file.

## Output

`ImportSeedOptions.OutputDirectory` is required and must stay inside
`ProjectRoot`. Artifact paths returned by the service are project-relative and
use `/` separators.

Output path rules:

| Collection | Output path |
| --- | --- |
| `navigation` | `<output>/navigation/<slug>.md` |
| `post` | `<output>/posts/<slug>.md` |
| `company` | `<output>/companies/<slug>.md` |
| `service` | `<output>/services/<slug>.md` |
| `page` with slug `index` | `<output>/index.md` |
| Other `page` records | `<output>/pages/<slug>.md` |

The generated Markdown uses YAML front matter:

```markdown
---
title: "Example"
slug: "example"
type: "page"
summary: "Optional summary"
language: "en"
seo_title: "Optional SEO title"
seo_description: "Optional SEO description"
published: true
---

Optional body content.
```

## Force Semantics

If the output directory exists and contains any file or directory, `Force=false`
returns a business failure with `import.outputAlreadyExists`.

`Force=true` allows writing into an existing non-empty output directory. Existing
Markdown files for imported records may be overwritten.

## Diagnostics

The service returns `ImportSeedResult` and does not throw for expected user or
business failures.

Stable diagnostic codes:

| Code | Meaning |
| --- | --- |
| `import.seedDirNotFound` | The seed directory does not exist. |
| `import.seedDirInvalid` | The seed directory is empty, invalid, or outside the project root. |
| `import.missingOutput` | The output directory was not provided. |
| `import.outputOutsideProject` | The output directory escapes the project root. |
| `import.outputAlreadyExists` | The output directory is non-empty and force is disabled. |
| `import.seedRecordInvalid` | A seed JSON or YAML file cannot be parsed. |
| `import.seedWriteFailed` | Writing generated Markdown failed. |
