# Bukit Import Use/Light Verify Contract

This document defines the `use/light verify` module for the external Import
process plugin.

## Scope

`bukit import html-demo <demo-dir> --theme <name>` now exposes:

```text
--use
--verify
```

## `--use`

`--use` updates the target site's `site.yaml` so `theme.name` points at the
newly generated theme.

The update is implemented in `Bukit.Importing` and writes through a temporary
file in the same directory before replacing `site.yaml`. It does not call
`ThemeCommand`, Labs, the CLI, or PluginHost.

Successful use emits:

```text
diagnostic: import.useApplied
artifact: site-config
```

## `--verify`

`--verify` runs a first-stage light verification only. It checks:

- `site.yaml` exists
- `site.yaml` is parseable YAML
- generated theme directory exists
- generated page templates exist
- generated Markdown content exists when content extraction is enabled

It does not run a full build, `bukit doctor`, Labs commands, or host actions.
Full build verification is deferred until a future Core Host Action exists.

Successful light verification emits:

```text
diagnostic: import.lightVerifyPassed
artifact: verification
```

## Deferred Scope

This module does not add Notion handoff, `push-notion`, package build,
cross-platform smoke, Clone migration, or dynamic plugin loading.
