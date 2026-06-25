# Bukit Import Route Map Contract

This document defines the `route-map` module for the external Import process
plugin.

## Scope

`bukit import html-demo <demo-dir> --theme <name> [--dry-run] [--force] --route-map <file>`
loads a project-local YAML route map and applies it to HTML demo page mapping.

This module only adds route-map input and page mapping. It does not add strict
mode, use/verify, overwrite, Notion handoff, push-notion, Clone migration, Labs
commands, or Bukit build/theme command calls.

## Route Map Schema

The route map may be a mapping with a `pages` sequence:

```yaml
pages:
  - source: legacy.html
    route: /mapped-route/
    type: CompanyList
    template: mapped-companies
    slug: mapped-route
    description: Optional human note
```

It may also be a direct sequence of page entries.

Fields:

- `source`: required HTML source path or basename.
- `route`: target route used by generated report and site mappings.
- `type`: Bukit page type hint.
- `template`: generated page template name.
- `slug`: optional explicit slug override.
- `description`: optional note.

## Plugin Validation

The plugin mapper resolves `--route-map` relative to the project root. The path
must stay inside the project root and must exist before invoke proceeds.

Stable plugin diagnostics:

| Code | Severity | Meaning |
| --- | --- | --- |
| `import.htmlDemoInvalidRouteMap` | error | `--route-map` was not a non-empty string. |
| `import.routeMapPathInvalid` | error | The resolved route-map path leaves the project root. |
| `import.routeMapNotFound` | error | The resolved route-map file does not exist. |

Route-map parse warnings from the importing domain service are written to stderr.

## Manifest Contract

The static and runtime manifests expose `--route-map` only on `import html-demo`.
No Notion, strict, use, verify, or overwrite options are exposed in this module.
