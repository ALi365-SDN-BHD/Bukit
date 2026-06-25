# Bukit Import Report/Security Scan Contract

This document defines the `report/security scan` module for the external Import
process plugin.

## Scope

`bukit import html-demo <demo-dir> --theme <name>` now generates import reports
by default and returns report artifacts from the plugin invoke response.

The static and runtime manifests expose:

```text
--no-report
--strict <fail|warn>
```

`--no-report` disables both report files and report artifacts.

`--strict fail` uses the existing importing domain strict diagnostics behavior:
import diagnostics with warning-or-higher severity fail the invoke. `--strict
warn` reports diagnostics but allows the import to complete.

## Report Outputs

The report writer produces:

```text
sites/<site-name>/import-report.md
.bukit/reports/plugin-output/import/html-demo-report.json
```

The plugin does not write `.bukit/reports/plugin-executions`; that directory is
owned by the core PluginHost execution reporter.

## Security Scan

The importing domain scanner reports diagnostics for:

- inline script tags
- remote script tags
- external URLs
- form markup requiring manual review
- external form actions
- dangerous URL protocols
- iframe tags
- inline event handlers
- suspected hardcoded secrets
- sensitive files and directories

Security findings are returned as plugin diagnostics and are also included in
the JSON import report.

## Permissions

The official fixture grants explicit write permission for:

```text
./content
./themes
./sites
.bukit/reports/plugin-output/import
```

## Deferred Scope

This module does not add `--use`, `--verify`, Notion handoff, package smoke,
Clone migration, or `push-notion`.
