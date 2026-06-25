# Bukit Import Theme/Site Generation Contract

This document defines the `theme/site generation` module for the external
Import process plugin.

## Scope

`bukit import html-demo <demo-dir> --theme <name>` generates a Bukit theme and
site skeleton through the mainline importing domain service.

Default output paths are:

```text
themes/<theme-name>/
sites/<theme-name>/site.yaml
sites/<theme-name>/content/
```

The plugin exposes site generation controls for this module:

```text
--site-path <dir>
--language <lang>
```

`--site-path` must resolve inside the project's `./sites` directory. This keeps
process-plugin writes inside the granted `./sites`/`./themes` workspace boundary
and prevents using the plugin as an arbitrary filesystem writer.

`--language` sets the generated `site.language` value in `site.yaml`. When it is
omitted, the importer keeps the existing default language.

## Generated Site Config

The generated `site.yaml` configures:

- site metadata and language
- markdown content sources
- build output settings
- generated theme name

The plugin keeps the local import path fixed to:

```text
ContentSource = json
BuildSource = markdown
GenerateSeed = false
GenerateReport = false
PreserveHtml = false
```

## Artifacts

Successful local imports return at least:

```text
type: theme
path: themes/<theme-name>

type: site
path: <site-path>
```

When content extraction remains enabled and Markdown content is written, the
response also includes:

```text
type: content
path: <site-path>/content
```

## Deferred Scope

This module does not add report/security scanning, `--use`, `--verify`,
strict mode, Notion handoff, package smoke, Clone migration, or
`push-notion`.
