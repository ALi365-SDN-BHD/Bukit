# Bukit Import HTML Demo Local Import Contract

This document defines the first non-dry-run `html-demo` plugin contract. The
scope is local filesystem import only.

## Scope

`bukit import html-demo <demo-dir> --theme <name> [--force]` imports a static HTML
demo into local Bukit theme, site, and Markdown content files through the
external process plugin.

This module does not call Labs commands, Bukit build/theme commands, Notion
commands, or Clone migration code. It does not push to Notion and does not expose
use, verify, overwrite, or Notion options. Route-map support is defined
separately in `docs/plugins/import-route-map-contract.md`. Content extraction is
defined in `docs/plugins/import-content-extraction-contract.md`.

## Outputs

The plugin may write these project-local outputs:

- `themes/<theme-name>/`
- `sites/<theme-name>/site.yaml`
- `sites/<theme-name>/content/`

The plugin sets the importer to markdown build output for this module:

- `ContentSource = json`
- `BuildSource = markdown`
- `GenerateSeed = false`
- `GenerateReport = false`
- `PreserveHtml = false`

This avoids generating Notion handoff artifacts or import report/security-scan
artifacts before their dedicated modules.

## Command Contract

The static and runtime manifests expose:

```text
import html-demo <demo-dir> --theme <name> [--dry-run] [--force]
```

`--dry-run` remains optional and preserves the scan-only behavior from the
previous module. When `--dry-run` is absent or false, the plugin performs local
import.

`--force` allows replacing an existing generated theme. Without `--force`, an
existing theme causes a user-input failure from the importing domain service.

## Diagnostics And Logging

User-input failures return `exitCode = 2` with a plugin diagnostic. Unexpected
failures return `exitCode = 1`.

The legacy importing domain service writes progress messages with
`Console.WriteLine`. The plugin redirects those messages to stderr while keeping
stdout reserved for the single JSON protocol response.

## Permissions

The plugin requires explicit filesystem permissions:

- read: `.`
- write: `./content`, `./themes`, `./sites`

Network access is false. Environment reads are empty.
