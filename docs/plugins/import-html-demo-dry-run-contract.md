# Bukit Import HTML Demo Dry-Run Contract

This document defines the first HTML demo import plugin contract. The scope is
input scanning only.

## Scope

`bukit import html-demo <demo-dir> --theme <name> --dry-run` scans a static HTML
demo and returns diagnostics plus a scan-report artifact reference. It must not
write theme files, content files, site config, seed files, lock files outside the
normal plugin host execution lock/report flow, or call Labs commands.

Full local import is defined separately in
`docs/plugins/import-html-demo-local-import-contract.md`.

## Supported Input

`<demo-dir>` must exist and must stay inside the project root.

The scanner supports:

- `index.html`
- multiple `.html` files, including nested directories
- local CSS, JavaScript, and image references
- relative links between local HTML pages

External `http`, `https`, and `data:` references are ignored by this scanner.

## Deferred Input

Route-map semantics are defined in `docs/plugins/import-route-map-contract.md`.
When `--route-map` is provided, dry-run scanning applies the route-map to page
type and slug discovery without writing files.

## Diagnostics

Stable diagnostic codes:

| Code | Severity | Meaning |
| --- | --- | --- |
| `import.htmlDemoDirNotFound` | error | The demo directory does not exist. |
| `import.htmlDemoDirInvalid` | error | The demo directory is outside the project root. |
| `import.htmlDemoNoHtmlFiles` | error | The directory contains no `.html` files. |
| `import.htmlDemoMissingIndex` | warning | The directory has HTML files but no `index.html`. |
| `import.htmlDemoAssetMissing` | warning | A local CSS, JS, or image reference is missing. |

## Plugin Contract

The static and runtime manifests expose only:

```text
import html-demo <demo-dir> --theme <name> --dry-run [--route-map <file>]
```

No `--use`, `--verify`, `--overwrite`, `--push-notion`, or Notion options are
declared in this module.

Successful dry-run returns a `scan-report` artifact reference:

```text
reports/import/html-demo-dry-run.json
```

The artifact is an execution-report reference for host/report consumers. The
dry-run scanner itself does not write this report file.
